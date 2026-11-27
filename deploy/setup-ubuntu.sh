#!/usr/bin/env bash
#
# LakerProxy host setup for a fresh Ubuntu server (22.04 or newer, systemd,
# netplan). Prepares the machine before install.sh installs the proxy:
#
#   system    hostname, timezone, base packages, unattended security updates,
#             libmsquic for HTTP/3, free port 53 (systemd-resolved stub listener)
#   network   /etc/netplan/00-installer-config.yaml: static IPv4, static IPv6 on
#             the WAN when an address is given (otherwise IPv6 off),
#             eth0 = WAN (/27, Google DNS, gateway = first address of the range),
#             eth1 = LAN (/24) with routes to the kubernetes clusters
#   firewall  ufw: 80/tcp, 443/tcp, 443/udp (HTTP/3), 53/udp (acme dns) for IPv4
#             and IPv6, 22/tcp only from the management addresses
#
# Usage:
#   sudo ./setup-ubuntu.sh                 ask per section
#   sudo ./setup-ubuntu.sh --network       only one or more sections:
#                                          --system --network --firewall
#
# Every question shows the current or suggested value, edit it inline or press
# enter to accept. Safe to re-run; existing files are backed up with a
# timestamp. A reboot is needed after renaming the interfaces to eth0/eth1.

set -euo pipefail

NETPLAN_FILE="/etc/netplan/00-installer-config.yaml"
WAN_PREFIX_DEFAULT=27
LAN_PREFIX_DEFAULT=24
DNS_DEFAULT="8.8.8.8 8.8.4.4"
DNS6_DEFAULT="2001:4860:4860::8888 2001:4860:4860::8844"
STAMP="$(date +%Y%m%d-%H%M%S)"

DO_SYSTEM=0
DO_NETWORK=0
DO_FIREWALL=0
ASK_SECTIONS=1

log()  { printf '==> %s\n' "$*"; }
warn() { printf 'WARNING: %s\n' "$*" >&2; }
die()  { printf 'ERROR: %s\n' "$*" >&2; exit 1; }

usage() {
  sed -n '2,/^$/p' "$0" | sed 's/^# \{0,1\}//'
  exit 0
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --system)   DO_SYSTEM=1; ASK_SECTIONS=0; shift ;;
    --network)  DO_NETWORK=1; ASK_SECTIONS=0; shift ;;
    --firewall) DO_FIREWALL=1; ASK_SECTIONS=0; shift ;;
    -h|--help)  usage ;;
    *)          die "unknown argument: $1 (see --help)" ;;
  esac
done

[[ "$(id -u)" -eq 0 ]] || die "run as root (sudo)"

# the questions read from /dev/tty, so `curl ... | sudo bash` works too (stdin is
# the script itself there, so it must never be redirected)
{ : < /dev/tty; } 2>/dev/null || die "this script needs a terminal"

# ---------------------------------------------------------------------------
# helpers
# ---------------------------------------------------------------------------

trim() {
  local s="$1"
  s="${s#"${s%%[![:space:]]*}"}"
  s="${s%"${s##*[![:space:]]}"}"
  printf '%s' "$s"
}

# ask VARIABLE "question" "default" [validator]; the default is editable inline
ask() {
  local -n __ref="$1"
  local __question="$2" __default="$3" __validator="${4:-}" __answer
  while true; do
    IFS= read -r -e -i "$__default" -p "$__question: " __answer < /dev/tty || die "no input"
    __answer="$(trim "$__answer")"
    if [[ -z "$__validator" ]] || "$__validator" "$__answer"; then
      __ref="$__answer"
      return 0
    fi
  done
}

# ask_yesno "question" y|n; enter takes the default (shown in capitals), returns 0 for yes
ask_yesno() {
  local hint yn
  [[ "$2" == y ]] && hint="Y/n" || hint="y/N"
  while true; do
    IFS= read -r -p "$1 [$hint]: " yn < /dev/tty || die "no input"
    yn="$(trim "$yn")"
    yn="${yn:-$2}"
    if v_yesno "$yn"; then
      [[ "$yn" =~ ^[Yy] ]] && return 0 || return 1
    fi
  done
}

v_yesno()     { [[ "$1" =~ ^([Yy]([Ee][Ss])?|[Nn][Oo]?)$ ]] || { echo "  answer y or n"; return 1; }; }
v_required()  { [[ -n "$1" ]] || { echo "  a value is required"; return 1; }; }
v_hostname()  { [[ "$1" =~ ^[A-Za-z0-9]([A-Za-z0-9-]*[A-Za-z0-9])?(\.[A-Za-z0-9]([A-Za-z0-9-]*[A-Za-z0-9])?)*$ ]] || { echo "  enter a hostname such as proxy1 or proxy1.example.com"; return 1; }; }
v_timezone()  { [[ -f "/usr/share/zoneinfo/$1" ]] || { echo "  unknown timezone, see: timedatectl list-timezones"; return 1; }; }

is_ipv4() {
  [[ "$1" =~ ^([0-9]{1,3})\.([0-9]{1,3})\.([0-9]{1,3})\.([0-9]{1,3})$ ]] || return 1
  local o
  for o in "${BASH_REMATCH[@]:1}"; do [[ $o -le 255 ]] || return 1; done
}
is_cidr() {
  [[ "$1" == */* ]] || return 1
  is_ipv4 "${1%/*}" && [[ "${1#*/}" =~ ^[0-9]{1,2}$ && ${1#*/} -le 32 ]]
}
v_ipv4()        { is_ipv4 "$1" || { echo "  enter an IPv4 address such as 192.168.1.40"; return 1; }; }
v_cidr()        { is_cidr "$1" || { echo "  enter address/prefix such as 203.0.113.34/27"; return 1; }; }
v_cidr_or_bare() { is_cidr "$1" || is_ipv4 "$1" || { echo "  enter address/prefix such as 203.0.113.34/27"; return 1; }; }
# loose IPv6 checks: hex groups and colons, optional embedded IPv4, at least one "::" or 7 colons
is_ipv6() {
  [[ "$1" =~ ^[0-9A-Fa-f:.]+$ && "$1" == *:*:* && "$1" != *:::* ]] || return 1
  local colons="${1//[^:]/}"
  [[ "$1" == *::* || ${#colons} -eq 7 ]]
}
is_ipv6_cidr() {
  [[ "$1" == */* ]] || return 1
  is_ipv6 "${1%/*}" && [[ "${1#*/}" =~ ^[0-9]{1,3}$ && ${1#*/} -le 128 ]]
}
v_ipv6()          { is_ipv6 "$1" || { echo "  enter an IPv6 address such as 2001:db8:1:2::1"; return 1; }; }
v_ipv6_cidr_or_empty() { [[ -z "$1" ]] || is_ipv6_cidr "$1" || { echo "  enter address/prefix such as 2001:db8:1:2::34/64, or nothing to disable IPv6"; return 1; }; }
v_ip_list() {
  local ip
  [[ -n "$1" ]] || { echo "  at least one address is required"; return 1; }
  for ip in $1; do is_ipv4 "$ip" || is_ipv6 "$ip" || { echo "  '$ip' is not an IP address"; return 1; }; done
}
v_ipv4_or_cidr_list() {
  local ip
  for ip in ${1//,/ }; do is_ipv4 "$ip" || is_cidr "$ip" || { echo "  '$ip' is not an IPv4 address or network"; return 1; }; done
}

ip2int() {
  local IFS=. a b c d
  read -r a b c d <<< "$1"
  printf '%d' $(( (a << 24) | (b << 16) | (c << 8) | d ))
}
int2ip() {
  printf '%d.%d.%d.%d' $(( ($1 >> 24) & 255 )) $(( ($1 >> 16) & 255 )) $(( ($1 >> 8) & 255 )) $(( $1 & 255 ))
}
prefix2mask() { printf '%d' $(( (0xFFFFFFFF << (32 - $1)) & 0xFFFFFFFF )); }
cidr_network() { local m; m="$(prefix2mask "${1#*/}")"; printf '%d' $(( $(ip2int "${1%/*}") & m )); }
cidr_broadcast() { local m; m="$(prefix2mask "${1#*/}")"; printf '%d' $(( $(cidr_network "$1") | (~m & 0xFFFFFFFF) )); }
cidr_first_host() { int2ip $(( $(cidr_network "$1") + 1 )); }
cidr_contains() {
  # cidr_contains a.b.c.d/nn x.y.z.w
  local ip; ip="$(ip2int "$2")"
  [[ $ip -gt $(cidr_network "$1") && $ip -lt $(cidr_broadcast "$1") ]]
}
cidr_network_text() { printf '%s/%s' "$(int2ip "$(cidr_network "$1")")" "${1#*/}"; }

backup_file() {
  if [[ -f "$1" ]]; then
    cp -p "$1" "$1.bak-$STAMP"
    log "backup: $1.bak-$STAMP"
  fi
}

apt_install() {
  local missing=() pkg
  for pkg in "$@"; do
    dpkg -s "$pkg" >/dev/null 2>&1 || missing+=("$pkg")
  done
  if [[ ${#missing[@]} -gt 0 ]]; then
    log "installing: ${missing[*]}"
    # </dev/null: with `curl | bash` stdin is the script, no command may read from it
    DEBIAN_FRONTEND=noninteractive apt-get install -y -q "${missing[@]}" < /dev/null
  fi
}

# physical network interfaces: name mac state ipv4
list_interfaces() {
  local dir name
  for dir in /sys/class/net/*; do
    name="${dir##*/}"
    [[ "$name" != lo && -e "$dir/device" && ! -e "$dir/wireless" ]] || continue
    printf '%s %s %s %s\n' "$name" "$(cat "$dir/address")" "$(cat "$dir/operstate")" \
      "$(ip -o -4 addr show dev "$name" 2>/dev/null | awk '{print $4; exit}')"
  done
}

# ---------------------------------------------------------------------------
# system
# ---------------------------------------------------------------------------

setup_system() {
  echo
  log "system"
  local current_hostname new_hostname current_tz new_tz
  current_hostname="$(hostname)"
  ask new_hostname "Hostname (machine name, the proxy registers itself under it)" "$current_hostname" v_hostname
  if [[ "$new_hostname" != "$current_hostname" ]]; then
    hostnamectl set-hostname "$new_hostname"
    backup_file /etc/hosts
    if grep -qE '^127\.0\.1\.1[[:space:]]' /etc/hosts; then
      sed -i -E "s/^127\.0\.1\.1[[:space:]].*/127.0.1.1 $new_hostname/" /etc/hosts
    else
      printf '127.0.1.1 %s\n' "$new_hostname" >> /etc/hosts
    fi
    log "hostname set to $new_hostname"
  fi

  current_tz="$(timedatectl show -p Timezone --value 2>/dev/null || echo Etc/UTC)"
  ask new_tz "Timezone" "${current_tz:-Europe/Amsterdam}" v_timezone
  if [[ "$new_tz" != "$current_tz" ]]; then
    timedatectl set-timezone "$new_tz"
    log "timezone set to $new_tz"
  fi

  log "updating package lists"
  apt-get update -q < /dev/null
  apt_install curl ca-certificates nano ufw tar

  if ask_yesno "Install unattended security updates (unattended-upgrades)?" y; then
    apt_install unattended-upgrades
    cat > /etc/apt/apt.conf.d/20auto-upgrades <<'EOF'
APT::Periodic::Update-Package-Lists "1";
APT::Periodic::Unattended-Upgrade "1";
EOF
    systemctl enable --now unattended-upgrades >/dev/null 2>&1 || true
    log "unattended-upgrades enabled (security pocket only, no automatic reboot)"
  fi

  if ask_yesno "Install libmsquic from the Microsoft repository (needed for HTTP/3)?" y; then
    if ! dpkg -s libmsquic >/dev/null 2>&1; then
      local release deb
      release="$(. /etc/os-release && echo "$VERSION_ID")"
      deb="$(mktemp)"
      if curl -fsSL -o "$deb" "https://packages.microsoft.com/config/ubuntu/$release/packages-microsoft-prod.deb"; then
        dpkg -i "$deb" >/dev/null < /dev/null
        apt-get update -q < /dev/null
        apt_install libmsquic
      else
        warn "no Microsoft package repository for Ubuntu $release, libmsquic skipped (HTTP/1.1 and HTTP/2 still work)"
      fi
      rm -f "$deb"
    else
      log "libmsquic is already installed"
    fi
  fi

  cat <<'EOF'

The proxy answers dns-01 challenges itself on port 53. Ubuntu's systemd-resolved
keeps a stub DNS listener on 127.0.0.53:53 that can keep that port occupied.
Turning it off makes the server use the netplan nameservers directly.
EOF
  if ask_yesno "Disable the systemd-resolved stub listener?" y; then
    install -d /etc/systemd/resolved.conf.d
    cat > /etc/systemd/resolved.conf.d/lakerproxy.conf <<'EOF'
# written by lakerproxy setup-ubuntu.sh: port 53 is used by the proxy's acme dns server
[Resolve]
DNSStubListener=no
EOF
    ln -sf /run/systemd/resolve/resolv.conf /etc/resolv.conf
    systemctl restart systemd-resolved
    log "stub listener disabled, /etc/resolv.conf -> /run/systemd/resolve/resolv.conf"
  fi
}

# ---------------------------------------------------------------------------
# network
# ---------------------------------------------------------------------------

setup_network() {
  echo
  log "network ($NETPLAN_FILE)"

  local -a nics=()
  mapfile -t nics < <(list_interfaces)
  [[ ${#nics[@]} -gt 0 ]] || die "no physical network interfaces found"

  echo "Interfaces:"
  local line
  for line in "${nics[@]}"; do
    printf '  %-10s %s  %-5s %s\n' $line
  done
  echo

  local -a names=()
  for line in "${nics[@]}"; do names+=("${line%% *}"); done
  v_interface() { [[ " ${names[*]} " == *" $1 "* ]] || { echo "  choose one of: ${names[*]}"; return 1; }; }
  v_interface_or_empty() { [[ -z "$1" ]] || v_interface "$1"; }

  local wan_if lan_if wan_mac lan_mac
  ask wan_if "WAN interface (becomes eth0)" "${names[0]}" v_interface
  ask lan_if "LAN interface (becomes eth1, empty = none)" "${names[1]:-}" v_interface_or_empty
  [[ "$lan_if" != "$wan_if" ]] || die "WAN and LAN cannot be the same interface"
  wan_mac="$(cat "/sys/class/net/$wan_if/address")"
  [[ -n "$lan_if" ]] && lan_mac="$(cat "/sys/class/net/$lan_if/address")"

  # --- WAN ---
  local wan_cidr wan_gw wan_dns current
  current="$(ip -o -4 addr show dev "$wan_if" 2>/dev/null | awk '{print $4; exit}')"
  echo
  ask wan_cidr "WAN address/prefix (a bare address gets /$WAN_PREFIX_DEFAULT)" "$current" v_cidr_or_bare
  [[ "$wan_cidr" == */* ]] || wan_cidr="$wan_cidr/$WAN_PREFIX_DEFAULT"
  # suggestion: the current gateway when it lies inside the entered range (nothing changed),
  # otherwise the first address of that range so usually only the last number needs editing
  current="$(ip -4 route show default dev "$wan_if" 2>/dev/null | awk '{print $3; exit}')"
  if [[ -z "$current" ]] || ! cidr_contains "$wan_cidr" "$current"; then
    current="$(cidr_first_host "$wan_cidr")"
  fi
  ask wan_gw "WAN gateway" "$current" v_ipv4
  cidr_contains "$wan_cidr" "$wan_gw" || warn "gateway $wan_gw is outside $(cidr_network_text "$wan_cidr")"
  [[ "$wan_gw" != "${wan_cidr%/*}" ]] || die "gateway equals the WAN address"

  local wan6_cidr wan6_gw="" dns_default="$DNS_DEFAULT"
  current="$(ip -o -6 addr show dev "$wan_if" scope global 2>/dev/null | awk '{print $4; exit}')"
  ask wan6_cidr "WAN IPv6 address/prefix (empty = IPv6 disabled)" "$current" v_ipv6_cidr_or_empty
  if [[ -n "$wan6_cidr" ]]; then
    # suggestion: the address with its last group replaced by 1 (2001:db8:1:2::34 -> 2001:db8:1:2::1);
    # the current default route is usually a link-local router address, not useful as static gateway
    local wan6_addr="${wan6_cidr%/*}" suggestion
    suggestion="${wan6_addr%:*}:1"
    [[ "$suggestion" != "$wan6_addr" ]] || suggestion=""
    ask wan6_gw "WAN IPv6 gateway" "$suggestion" v_ipv6
    [[ "$wan6_gw" != "${wan6_cidr%/*}" ]] || die "IPv6 gateway equals the WAN IPv6 address"
    dns_default="$DNS_DEFAULT $DNS6_DEFAULT"
  fi
  ask wan_dns "DNS servers (space separated)" "$dns_default" v_ip_list

  # --- LAN ---
  local lan_cidr="" lan_net=""
  local -a routes=()
  if [[ -n "$lan_if" ]]; then
    current="$(ip -o -4 addr show dev "$lan_if" 2>/dev/null | awk '{print $4; exit}')"
    echo
    ask lan_cidr "LAN address/prefix (a bare address gets /$LAN_PREFIX_DEFAULT)" "$current" v_cidr_or_bare
    [[ "$lan_cidr" == */* ]] || lan_cidr="$lan_cidr/$LAN_PREFIX_DEFAULT"
    lan_net="$(cidr_network_text "$lan_cidr")"

    # existing routes via the LAN interface (not the connected network itself)
    mapfile -t routes < <(ip -4 route show dev "$lan_if" 2>/dev/null | awk '$2 == "via" && $1 != "default" {print $1 " " $3}')
    if [[ ${#routes[@]} -gt 0 ]]; then
      echo
      echo "Current routes via $lan_if:"
      printf '  %s\n' "${routes[@]}"
      if ! ask_yesno "Keep these routes?" y; then
        routes=()
      fi
    fi

    echo
    echo "Routes to the kubernetes clusters via the LAN, for example 10.42.0.0/16 via ${lan_net%.*/*}.40."
    echo "The previous entry stays filled in, edit it and press enter to add a similar one."
    local to via last_to="" last_via=""
    if [[ ${#routes[@]} -gt 0 ]]; then
      last_to="${routes[-1]%% *}"
      last_via="${routes[-1]##* }"
    fi
    while ask_yesno "Add a route?" "$([[ ${#routes[@]} -eq 0 ]] && echo y || echo n)"; do
      ask to "  destination network" "$last_to" v_cidr
      ask via "  via" "$last_via" v_ipv4
      cidr_contains "$lan_cidr" "$via" || warn "$via is not inside $lan_net"
      routes+=("$(cidr_network_text "$to") $via")
      last_to="$to"
      last_via="$via"
    done
  fi

  # --- write netplan ---
  echo
  local tmp
  tmp="$(mktemp)"
  {
    echo "# written by lakerproxy setup-ubuntu.sh on $(date -Is); re-run the script to change it"
    echo "network:"
    echo "  version: 2"
    echo "  renderer: networkd"
    echo "  ethernets:"
    echo "    eth0:"
    echo "      match:"
    echo "        macaddress: \"$wan_mac\""
    echo "      set-name: eth0"
    echo "      dhcp4: false"
    echo "      dhcp6: false"
    echo "      accept-ra: false"
    if [[ -n "$wan6_cidr" ]]; then
      # the link-local address is needed to reach the IPv6 gateway
      echo "      link-local: [ipv6]"
      echo "      addresses: [$wan_cidr, \"$wan6_cidr\"]"
      echo "      routes:"
      echo "        - to: default"
      echo "          via: $wan_gw"
      echo "        - to: \"::/0\""
      echo "          via: \"$wan6_gw\""
    else
      echo "      link-local: []"
      echo "      addresses: [$wan_cidr]"
      echo "      routes:"
      echo "        - to: default"
      echo "          via: $wan_gw"
    fi
    echo "      nameservers:"
    echo "        addresses: [$(printf '"%s", ' $wan_dns | sed 's/, $//')]"
    if [[ -n "$lan_if" ]]; then
      echo "    eth1:"
      echo "      match:"
      echo "        macaddress: \"$lan_mac\""
      echo "      set-name: eth1"
      echo "      dhcp4: false"
      echo "      dhcp6: false"
      echo "      accept-ra: false"
      echo "      link-local: []"
      echo "      addresses: [$lan_cidr]"
      if [[ ${#routes[@]} -gt 0 ]]; then
        echo "      routes:"
        local r
        for r in "${routes[@]}"; do
          echo "        - to: ${r%% *}"
          echo "          via: ${r##* }"
        done
      fi
    fi
  } > "$tmp"

  echo "----- $NETPLAN_FILE -----"
  cat "$tmp"
  echo "-------------------------"
  if ! ask_yesno "Write this configuration?" y; then
    rm -f "$tmp"
    log "network configuration not written"
    return 0
  fi

  # other netplan files would merge with ours (typically the installer's dhcp config)
  local -a others=()
  local f
  for f in /etc/netplan/*.yaml /etc/netplan/*.yml; do
    [[ -f "$f" && "$f" != "$NETPLAN_FILE" ]] && others+=("$f")
  done
  if [[ ${#others[@]} -gt 0 ]]; then
    echo "Other netplan files found: ${others[*]}"
    if ask_yesno "Move them to /etc/netplan/disabled-$STAMP (they would merge with the new config)?" y; then
      install -d -m 700 "/etc/netplan/disabled-$STAMP"
      mv "${others[@]}" "/etc/netplan/disabled-$STAMP/"
    fi
  fi

  backup_file "$NETPLAN_FILE"
  install -m 600 -o root -g root "$tmp" "$NETPLAN_FILE"
  rm -f "$tmp"

  # keep cloud-init from writing its own network config back on the next boot
  if [[ -d /etc/cloud/cloud.cfg.d ]]; then
    printf 'network: {config: disabled}\n' > /etc/cloud/cloud.cfg.d/99-disable-network-config.cfg
    log "cloud-init network configuration disabled"
  fi

  if ! netplan generate; then
    warn "netplan rejected the configuration, restoring the previous file"
    if [[ -f "$NETPLAN_FILE.bak-$STAMP" ]]; then
      mv -f "$NETPLAN_FILE.bak-$STAMP" "$NETPLAN_FILE"
    else
      rm -f "$NETPLAN_FILE"
    fi
    die "network configuration not applied"
  fi
  log "$NETPLAN_FILE written and validated"

  NETWORK_WRITTEN=1
  if [[ "$wan_if" != eth0 || ( -n "$lan_if" && "$lan_if" != eth1 ) ]]; then
    NETWORK_NEEDS_REBOOT=1
    log "the interfaces are renamed to eth0/eth1; that takes effect after a reboot"
  elif ask_yesno "Apply the network configuration now (an ssh session on a changed address drops)?" n; then
    netplan apply
    log "network configuration applied"
  else
    NETWORK_NEEDS_REBOOT=1
  fi
}

# ---------------------------------------------------------------------------
# firewall
# ---------------------------------------------------------------------------

setup_firewall() {
  echo
  log "firewall (ufw)"
  apt_install ufw

  local mgmt current
  current="$(who -m 2>/dev/null | sed -n 's/.*(\([0-9.]*\)).*/\1/p')"
  echo "ssh (22/tcp) is only allowed from the management addresses; everything else only gets 80, 443 and 53/udp."
  ask mgmt "Management addresses for ssh (space separated, address or network, empty = no ssh rule)" "$current" v_ipv4_or_cidr_list
  mgmt="${mgmt//,/ }"
  if [[ -z "$mgmt" ]]; then
    warn "no management address: ssh will be blocked as soon as ufw is enabled"
    ask_yesno "Continue without an ssh rule?" n || { log "firewall not changed"; return 0; }
  fi

  if ask_yesno "Reset the existing ufw rules first (recommended, this script defines the complete rule set)?" y; then
    ufw --force reset >/dev/null < /dev/null
  fi

  # IPV6=yes makes every rule without an address apply to IPv4 and IPv6
  sed -i -E 's/^IPV6=.*/IPV6=yes/' /etc/default/ufw
  grep -qE '^IPV6=yes' /etc/default/ufw || echo 'IPV6=yes' >> /etc/default/ufw

  ufw default deny incoming >/dev/null
  ufw default allow outgoing >/dev/null
  local ip
  for ip in $mgmt; do
    ufw allow from "$ip" to any port 22 proto tcp comment 'ssh management' >/dev/null
  done
  ufw allow 80/tcp comment 'http' >/dev/null
  ufw allow 443/tcp comment 'https' >/dev/null
  ufw allow 443/udp comment 'https http/3' >/dev/null
  ufw allow 53/udp comment 'acme dns' >/dev/null
  ufw --force enable >/dev/null < /dev/null
  ufw status verbose
}

# ---------------------------------------------------------------------------
# run
# ---------------------------------------------------------------------------

NETWORK_WRITTEN=0
NETWORK_NEEDS_REBOOT=0

if [[ $ASK_SECTIONS -eq 1 ]]; then
  ask_yesno "Configure system (hostname, timezone, packages, port 53)?" y && DO_SYSTEM=1
  ask_yesno "Configure firewall (ufw)?" y && DO_FIREWALL=1
  ask_yesno "Configure network (netplan)?" y && DO_NETWORK=1
fi

# plain if's: a function called in an && list would not stop on errors (set -e)
if [[ $DO_SYSTEM -eq 1 ]]; then setup_system; fi
if [[ $DO_FIREWALL -eq 1 ]]; then setup_firewall; fi
if [[ $DO_NETWORK -eq 1 ]]; then setup_network; fi

echo
log "setup done"
if [[ $NETWORK_NEEDS_REBOOT -eq 1 ]]; then
  log "reboot to activate the network configuration, then install the proxy with:"
else
  log "install the proxy with:"
fi
echo "  curl -fsSL https://raw.githubusercontent.com/lakerfield/lakerproxy/master/deploy/install.sh | sudo bash"
if [[ $NETWORK_NEEDS_REBOOT -eq 1 ]] && ask_yesno "Reboot now?" n; then
  reboot
fi
