#!/usr/bin/env bash
#
# LakerProxy server installer and updater for Ubuntu / Debian (systemd).
#
# Installs or updates the proxy server from the GitHub Releases of
# lakerfield/lakerproxy. Safe to re-run: when the installed version equals the
# requested one nothing happens.
#
# Usage:
#   sudo ./install.sh                  install or update to the latest release
#   sudo ./install.sh --version 2.0.1  install a specific release (also for rollback)
#   sudo ./install.sh --check          only report whether an update is available
#   sudo ./install.sh --no-start       install but do not (re)start the service
#   sudo ./install.sh --force          reinstall the current version
#   sudo ./install.sh --configure      (re)run the configuration questions
#   sudo ./install.sh --non-interactive  never ask; write a template config on first install
#
# On the first install the script asks for the MongoDB connection, the hostname
# of the management API, the admin token and the Let's Encrypt account details
# and only starts the server when those are complete. The initial-* and acme-*
# values are written to MongoDB at the very first start of the server; after
# that they are managed in the manager and the ini values are ignored.
#
# Environment overrides: LAKERPROXY_REPO, LAKERPROXY_ROOT, LAKERPROXY_SERVICE,
# LAKERPROXY_USER, LAKERPROXY_KEEP (number of releases to keep, default 3).
#
# Layout:
#   /opt/lakerproxy/
#     releases/<version>/              one directory per release
#     current -> releases/<version>    atomically switched symlink
#     config/app-configuration.ini     server specific settings, survives updates
#   /etc/systemd/system/lakerproxy.service
#   /usr/local/bin/lakerproxy-update   copy of this script from the installed release

set -euo pipefail

REPO="${LAKERPROXY_REPO:-lakerfield/lakerproxy}"
ROOT="${LAKERPROXY_ROOT:-/opt/lakerproxy}"
SERVICE="${LAKERPROXY_SERVICE:-lakerproxy}"
SVC_USER="${LAKERPROXY_USER:-lakerproxy}"
KEEP="${LAKERPROXY_KEEP:-3}"

REQUESTED_VERSION=""
CHECK_ONLY=0
NO_START=0
FORCE=0
CONFIGURE=0
NON_INTERACTIVE=0

log()  { printf '==> %s\n' "$*"; }
warn() { printf 'WARNING: %s\n' "$*" >&2; }
die()  { printf 'ERROR: %s\n' "$*" >&2; exit 1; }

usage() {
  sed -n '2,/^$/p' "$0" | sed 's/^# \{0,1\}//'
  exit 0
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --version)   [[ $# -ge 2 ]] || die "--version needs a value"; REQUESTED_VERSION="${2#v}"; shift 2 ;;
    --version=*) REQUESTED_VERSION="${1#--version=}"; REQUESTED_VERSION="${REQUESTED_VERSION#v}"; shift ;;
    --check)     CHECK_ONLY=1; shift ;;
    --no-start)  NO_START=1; shift ;;
    --force)     FORCE=1; shift ;;
    --configure) CONFIGURE=1; shift ;;
    --non-interactive) NON_INTERACTIVE=1; shift ;;
    -h|--help)   usage ;;
    *)           die "unknown argument: $1 (see --help)" ;;
  esac
done

# ---------------------------------------------------------------------------
# preconditions
# ---------------------------------------------------------------------------

[[ "$(id -u)" -eq 0 ]] || die "run as root (sudo)"
for tool in curl tar sha256sum systemctl install; do
  command -v "$tool" >/dev/null 2>&1 || die "missing required tool: $tool"
done

# ---------------------------------------------------------------------------
# resolve versions
# ---------------------------------------------------------------------------

resolve_latest_version() {
  # follow the redirect of /releases/latest to /releases/tag/vX.Y.Z, no API quota involved
  local url
  url="$(curl -fsSL -o /dev/null -w '%{url_effective}' "https://github.com/$REPO/releases/latest")" \
    || die "cannot reach https://github.com/$REPO/releases/latest"
  local tag="${url##*/}"
  [[ "$tag" =~ ^v[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$ ]] || die "no release found for $REPO (got '$url')"
  printf '%s' "${tag#v}"
}

if [[ -n "$REQUESTED_VERSION" ]]; then
  [[ "$REQUESTED_VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$ ]] || die "'$REQUESTED_VERSION' is not a valid version"
  VERSION="$REQUESTED_VERSION"
else
  VERSION="$(resolve_latest_version)"
fi

CURRENT_VERSION=""
if [[ -L "$ROOT/current" && -f "$ROOT/current/version.txt" ]]; then
  CURRENT_VERSION="$(tr -d '[:space:]' < "$ROOT/current/version.txt")"
fi

if [[ $CHECK_ONLY -eq 1 ]]; then
  if [[ "$CURRENT_VERSION" == "$VERSION" ]]; then
    log "up to date: $VERSION"
    exit 0
  fi
  log "installed: ${CURRENT_VERSION:-none}, available: $VERSION"
  exit 10
fi

if [[ "$CURRENT_VERSION" == "$VERSION" && $FORCE -eq 0 && $CONFIGURE -eq 0 ]]; then
  log "version $VERSION is already installed, nothing to do (use --force to reinstall)"
  exit 0
fi

log "installed: ${CURRENT_VERSION:-none}, installing: $VERSION"

# ---------------------------------------------------------------------------
# user and directories
# ---------------------------------------------------------------------------

if ! id -u "$SVC_USER" >/dev/null 2>&1; then
  log "creating system user $SVC_USER"
  useradd --system --no-create-home --home-dir "$ROOT" --shell /usr/sbin/nologin "$SVC_USER"
fi

install -d -m 755 "$ROOT" "$ROOT/releases"
install -d -m 750 -o root -g "$SVC_USER" "$ROOT/config"

CONFIG_FILE="$ROOT/config/app-configuration.ini"

# ---------------------------------------------------------------------------
# configuration
#
# The questions read from /dev/tty, so they also work for
# `curl ... | sudo bash` where stdin is the script itself.
# ---------------------------------------------------------------------------

have_tty() {
  [[ $NON_INTERACTIVE -eq 0 ]] && { : < /dev/tty; } 2>/dev/null
}

# value of a key in the existing config file, or the given default
cfg() {
  local value=""
  if [[ -f "$CONFIG_FILE" ]]; then
    value="$(sed -n "s/^[[:space:]]*$1[[:space:]]*=[[:space:]]*//p" "$CONFIG_FILE" | tail -n 1)"
  fi
  printf '%s' "${value:-$2}"
}

trim() {
  local s="$1"
  s="${s#"${s%%[![:space:]]*}"}"
  s="${s%"${s##*[![:space:]]}"}"
  printf '%s' "$s"
}

# ask VARIABLE "question" "default" [validator]; repeats until the validator accepts the answer
ask() {
  local -n __ref="$1"
  local __question="$2" __default="$3" __validator="${4:-}" __answer
  while true; do
    if [[ -n "$__default" ]]; then
      printf '%s [%s]: ' "$__question" "$__default" > /dev/tty
    else
      printf '%s: ' "$__question" > /dev/tty
    fi
    IFS= read -r __answer < /dev/tty || die "no input (use --non-interactive to write a template config)"
    __answer="$(trim "$__answer")"
    __answer="${__answer:-$__default}"
    if [[ -z "$__validator" ]] || "$__validator" "$__answer"; then
      __ref="$__answer"
      return 0
    fi
  done
}

# ask_yesno "question" y|n; returns 0 for yes
ask_yesno() {
  local yn
  ask yn "$1" "$2" v_yesno
  [[ "$yn" =~ ^[Yy] ]]
}

v_required() { [[ -n "$1" ]] || { echo "  a value is required" > /dev/tty; return 1; }; }
v_yesno()    { [[ "$1" =~ ^([Yy]([Ee][Ss])?|[Nn][Oo]?)$ ]] || { echo "  answer y or n" > /dev/tty; return 1; }; }
v_mail()     { [[ "$1" == *@*.* && "$1" != *" "* ]] || { echo "  enter a valid e-mail address" > /dev/tty; return 1; }; }
v_country()  { [[ "$1" =~ ^[A-Za-z]{2}$ ]] || { echo "  enter the two letter country code, for example NL" > /dev/tty; return 1; }; }
v_token()    { [[ ${#1} -ge 20 && "$1" != *" "* ]] || { echo "  the token must be at least 20 characters without spaces" > /dev/tty; return 1; }; }
v_hostname() {
  [[ "$1" =~ ^[A-Za-z0-9]([A-Za-z0-9-]*[A-Za-z0-9])?(\.[A-Za-z0-9]([A-Za-z0-9-]*[A-Za-z0-9])?)*$ ]] \
    || { echo "  enter a hostname such as proxy1.example.com" > /dev/tty; return 1; }
}

random_token() {
  # 36 random bytes -> 48 base64 characters, minus the url-unfriendly ones
  head -c 36 /dev/urandom | base64 | tr -d '/+=\n'
}

# configuration values; filled by the questions or the template
CONNECTIONSTRING=""
DATABASE=""
INITIAL_HOSTNAME=""
INITIAL_HOSTNAME_WITH_CERTIFICATE=""
INITIAL_ADMIN_TOKEN=""
ACME_MAIL=""
ACME_ORGANIZATION=""
ACME_ORGANIZATION_UNIT=""
ACME_LOCALITY=""
ACME_STATE=""
ACME_COUNTRY=""

# load_config [defaults]: values from the existing file; with "defaults" empty
# values get a suggestion (used for the questions), without it they stay empty
# (used to validate what is really in the file)
load_config() {
  local fqdn="" conn="" db="" cert="" unit="" country=""
  if [[ "${1:-}" == defaults ]]; then
    fqdn="$(hostname -f 2>/dev/null || hostname)"
    conn=mongodb://localhost
    db=lakerproxy
    cert=false
    unit=IT
    country=NL
  fi
  CONNECTIONSTRING="$(cfg connectionstring "$conn")"
  DATABASE="$(cfg database "$db")"
  INITIAL_HOSTNAME="$(cfg initial-hostname "$fqdn")"
  INITIAL_HOSTNAME_WITH_CERTIFICATE="$(cfg initial-hostname-with-certificate "$cert")"
  INITIAL_ADMIN_TOKEN="$(cfg initial-admin-token "")"
  ACME_MAIL="$(cfg acme-mail "")"
  ACME_ORGANIZATION="$(cfg acme-organization "")"
  ACME_ORGANIZATION_UNIT="$(cfg acme-organization-unit "$unit")"
  ACME_LOCALITY="$(cfg acme-locality "")"
  ACME_STATE="$(cfg acme-state "")"
  ACME_COUNTRY="$(cfg acme-country "$country")"
}

ask_configuration() {
  local with_certificate_default=n generated_token
  [[ "${INITIAL_HOSTNAME_WITH_CERTIFICATE,,}" == "true" ]] && with_certificate_default=y
  generated_token="${INITIAL_ADMIN_TOKEN:-$(random_token)}"

  cat > /dev/tty <<EOF

LakerProxy configuration ($CONFIG_FILE)
Press enter to accept the value between brackets. The hostname, token and
Let's Encrypt values are stored in MongoDB at the very first start of the
server; after that they are managed in the manager.

EOF
  ask CONNECTIONSTRING "MongoDB connection string" "$CONNECTIONSTRING" v_required
  ask DATABASE "MongoDB database" "$DATABASE" v_required
  echo > /dev/tty
  ask INITIAL_HOSTNAME "Public hostname of this server (the manager connects here)" "$INITIAL_HOSTNAME" v_hostname
  INITIAL_HOSTNAME="${INITIAL_HOSTNAME,,}"
  if ask_yesno "Request a Let's Encrypt certificate for $INITIAL_HOSTNAME right away (needs a public A record)?" "$with_certificate_default"; then
    INITIAL_HOSTNAME_WITH_CERTIFICATE=true
  else
    INITIAL_HOSTNAME_WITH_CERTIFICATE=false
  fi
  ask INITIAL_ADMIN_TOKEN "Admin API token (generated when left as is)" "$generated_token" v_token
  echo > /dev/tty
  echo "Let's Encrypt account and certificate subject:" > /dev/tty
  ask ACME_MAIL "  e-mail address" "$ACME_MAIL" v_mail
  ask ACME_ORGANIZATION "  organization" "$ACME_ORGANIZATION" v_required
  ask ACME_ORGANIZATION_UNIT "  organization unit" "$ACME_ORGANIZATION_UNIT" v_required
  ask ACME_LOCALITY "  locality (city)" "$ACME_LOCALITY" v_required
  ask ACME_STATE "  state or province" "$ACME_STATE" v_required
  ask ACME_COUNTRY "  country code" "$ACME_COUNTRY" v_country
  ACME_COUNTRY="${ACME_COUNTRY^^}"
  echo > /dev/tty
}

write_config() {
  local tmp="$CONFIG_FILE.tmp"
  cat > "$tmp" <<EOF
# LakerProxy server configuration. Values here override the defaults shipped
# with the release (app-configuration.ini). Edit, then: systemctl restart $SERVICE
# Re-run the questions with: lakerproxy-update --configure
environment=production

[storage]
mode=ConnectionString
connectionstring=$CONNECTIONSTRING
database=$DATABASE

[data]
# the values below are used once, at the very first start with an empty database
# public hostname of this server; the management api is created as a website of
# kind ProxyApi on it and the acme challenge zone becomes acme.<hostname>
initial-hostname=$INITIAL_HOSTNAME
initial-hostname-with-certificate=$INITIAL_HOSTNAME_WITH_CERTIFICATE
# creates the first admin API user; log in with this token from the manager
initial-admin-token=$INITIAL_ADMIN_TOKEN

# Let's Encrypt account (staging and live) and certificate subject
acme-mail=$ACME_MAIL
acme-organization=$ACME_ORGANIZATION
acme-organization-unit=$ACME_ORGANIZATION_UNIT
acme-locality=$ACME_LOCALITY
acme-state=$ACME_STATE
acme-country=$ACME_COUNTRY

#[security]
# space/comma separated feed urls (dshield block.txt format or plain ip/cidr lines), empty disables
#blocklist-feeds=https://feeds.dshield.org/block.txt
#blocklist-refresh-minutes=60
# ip/cidr entries that are never blocked
#blocklist-allow-list=127.0.0.0/8 ::1 10.0.0.0/8 172.16.0.0/12 192.168.0.0/16
EOF
  chmod 640 "$tmp"
  chown root:"$SVC_USER" "$tmp"
  mv -f "$tmp" "$CONFIG_FILE"
}

# the required values a first start needs; empty means complete
config_problems() {
  local problems=()
  [[ -n "$CONNECTIONSTRING" ]] || problems+=("connectionstring is empty")
  [[ -n "$DATABASE" ]] || problems+=("database is empty")
  [[ -n "$INITIAL_HOSTNAME" ]] || problems+=("initial-hostname is empty")
  [[ ${#INITIAL_ADMIN_TOKEN} -ge 20 ]] || problems+=("initial-admin-token must be at least 20 characters")
  [[ "$ACME_MAIL" == *@* ]] || problems+=("acme-mail is not an e-mail address")
  local key name
  for key in ACME_ORGANIZATION ACME_ORGANIZATION_UNIT ACME_LOCALITY ACME_STATE ACME_COUNTRY; do
    name="${key,,}"
    [[ -n "${!key}" ]] || problems+=("${name//_/-} is empty")
  done
  [[ ${#problems[@]} -eq 0 ]] || printf '%s\n' "${problems[@]}"
}

CONFIG_CREATED=0
CONFIG_COMPLETE=1
if [[ ! -f "$CONFIG_FILE" || $CONFIGURE -eq 1 ]]; then
  [[ -f "$CONFIG_FILE" ]] || CONFIG_CREATED=1
  load_config defaults
  if have_tty; then
    ask_configuration
    if [[ $CONFIG_CREATED -eq 0 ]]; then
      cp -p "$CONFIG_FILE" "$CONFIG_FILE.bak"
      log "previous configuration saved as $CONFIG_FILE.bak"
    fi
    write_config
    log "configuration written to $CONFIG_FILE"
    EDITOR_CMD="${EDITOR:-nano}"
    if command -v "$EDITOR_CMD" >/dev/null 2>&1 && ask_yesno "Review the file in $EDITOR_CMD before continuing?" n; then
      "$EDITOR_CMD" "$CONFIG_FILE" < /dev/tty > /dev/tty 2>&1 || warn "$EDITOR_CMD exited with an error"
      load_config
    fi
  else
    if [[ $CONFIG_CREATED -eq 1 ]]; then
      log "no terminal available, writing a template configuration to $CONFIG_FILE"
      write_config
    else
      die "--configure needs a terminal"
    fi
  fi
  if [[ -n "$(config_problems)" ]]; then
    CONFIG_COMPLETE=0
  fi
fi

# ---------------------------------------------------------------------------
# download and unpack the release
# ---------------------------------------------------------------------------

RELEASE_DIR="$ROOT/releases/$VERSION"
ARTIFACT="lakerproxy-server-$VERSION-linux-x64.tar.gz"
BASE_URL="https://github.com/$REPO/releases/download/v$VERSION"

if [[ -x "$RELEASE_DIR/LakerProxy.ProxyApp" && $FORCE -eq 0 ]]; then
  log "release $VERSION is already present in $RELEASE_DIR, reusing it"
else
  TMP="$(mktemp -d)"
  trap 'rm -rf "$TMP"' EXIT

  log "downloading $BASE_URL/$ARTIFACT"
  curl -fsSL --retry 3 -o "$TMP/$ARTIFACT" "$BASE_URL/$ARTIFACT" || die "download failed, does release v$VERSION exist?"
  curl -fsSL --retry 3 -o "$TMP/$ARTIFACT.sha256" "$BASE_URL/$ARTIFACT.sha256" || die "checksum download failed"

  log "verifying checksum"
  ( cd "$TMP" && sha256sum -c --quiet "$ARTIFACT.sha256" ) || die "checksum mismatch for $ARTIFACT"

  log "unpacking to $RELEASE_DIR"
  rm -rf "$RELEASE_DIR.tmp"
  mkdir -p "$RELEASE_DIR.tmp"
  tar -xzf "$TMP/$ARTIFACT" -C "$RELEASE_DIR.tmp"
  [[ -f "$RELEASE_DIR.tmp/LakerProxy.ProxyApp" ]] || die "archive does not contain LakerProxy.ProxyApp"
  chmod 755 "$RELEASE_DIR.tmp/LakerProxy.ProxyApp"
  chown -R root:root "$RELEASE_DIR.tmp"
  rm -rf "$RELEASE_DIR"
  mv "$RELEASE_DIR.tmp" "$RELEASE_DIR"
fi

# the app looks for config/app-configuration.ini next to its executable
ln -sfn "$ROOT/config" "$RELEASE_DIR/config"

if [[ -f "$RELEASE_DIR/install.sh" ]]; then
  install -m 755 "$RELEASE_DIR/install.sh" /usr/local/bin/lakerproxy-update
fi

# ---------------------------------------------------------------------------
# systemd unit
# ---------------------------------------------------------------------------

UNIT_FILE="/etc/systemd/system/$SERVICE.service"
UNIT_CONTENT="[Unit]
Description=LakerProxy reverse proxy server
After=network-online.target
Wants=network-online.target

[Service]
Type=notify
User=$SVC_USER
Group=$SVC_USER
WorkingDirectory=$ROOT/current
ExecStart=$ROOT/current/LakerProxy.ProxyApp
Restart=always
RestartSec=5
TimeoutStartSec=120
KillSignal=SIGINT
LimitNOFILE=65536
Environment=DOTNET_CLI_TELEMETRY_OPTOUT=1
Environment=HOME=%S/$SERVICE
StateDirectory=$SERVICE
# binds ports 80, 443 and 53 without running as root
AmbientCapabilities=CAP_NET_BIND_SERVICE
CapabilityBoundingSet=CAP_NET_BIND_SERVICE
NoNewPrivileges=yes
PrivateTmp=yes
ProtectSystem=full
ProtectHome=yes

[Install]
WantedBy=multi-user.target
"

if [[ ! -f "$UNIT_FILE" ]] || [[ "$(cat "$UNIT_FILE")" != "$UNIT_CONTENT" ]]; then
  log "writing $UNIT_FILE"
  printf '%s' "$UNIT_CONTENT" > "$UNIT_FILE"
  systemctl daemon-reload
fi

# ---------------------------------------------------------------------------
# switch the current symlink and restart
# ---------------------------------------------------------------------------

PREVIOUS_TARGET=""
if [[ -L "$ROOT/current" ]]; then
  PREVIOUS_TARGET="$(readlink "$ROOT/current")"
fi

switch_current() {
  ln -sfn "$1" "$ROOT/current.tmp"
  mv -Tf "$ROOT/current.tmp" "$ROOT/current"
}

switch_current "$RELEASE_DIR"
log "current -> $RELEASE_DIR"

# the service is only enabled once the configuration is complete, so a reboot
# never starts the server with placeholder values that would end up in MongoDB
if [[ $CONFIG_COMPLETE -eq 0 ]]; then
  warn "the configuration is incomplete, the service is not started:"
  config_problems | sed 's/^/  - /' >&2
  log "edit $CONFIG_FILE or run: lakerproxy-update --configure"
  log "then start the server with: systemctl enable --now $SERVICE"
  exit 0
fi

if [[ $NO_START -eq 1 ]]; then
  log "service not (re)started (--no-start); run: systemctl enable --now $SERVICE"
  exit 0
fi

if [[ $CONFIG_CREATED -eq 1 || $CONFIGURE -eq 1 ]] && have_tty; then
  if ! ask_yesno "Start $SERVICE now?" y; then
    log "start it later with: systemctl enable --now $SERVICE"
    exit 0
  fi
fi

systemctl enable "$SERVICE" >/dev/null 2>&1 || true

health_check() {
  local i code
  for i in $(seq 1 30); do
    if ! systemctl is-active --quiet "$SERVICE"; then
      sleep 1
      continue
    fi
    # any HTTP status means Kestrel answers on port 80; 000 means no connection
    code="$(curl -s -o /dev/null -m 3 -w '%{http_code}' http://127.0.0.1/ || true)"
    if [[ "$code" != "000" ]]; then
      return 0
    fi
    sleep 1
  done
  return 1
}

log "restarting $SERVICE"
systemctl restart "$SERVICE" || true

if health_check; then
  log "service is up"
else
  warn "service did not become healthy, last log lines:"
  journalctl -u "$SERVICE" -n 30 --no-pager >&2 || true
  if [[ -n "$PREVIOUS_TARGET" && -x "$PREVIOUS_TARGET/LakerProxy.ProxyApp" ]]; then
    warn "rolling back to $PREVIOUS_TARGET"
    switch_current "$PREVIOUS_TARGET"
    systemctl restart "$SERVICE" || true
    if health_check; then
      die "update to $VERSION failed, rolled back to ${CURRENT_VERSION:-previous release}"
    fi
    die "update to $VERSION failed and rollback did not come up either; check: journalctl -u $SERVICE"
  fi
  die "service failed to start; check: journalctl -u $SERVICE"
fi

# ---------------------------------------------------------------------------
# prune old releases
# ---------------------------------------------------------------------------

if [[ "$KEEP" =~ ^[0-9]+$ && "$KEEP" -gt 0 ]]; then
  CURRENT_TARGET="$(readlink "$ROOT/current")"
  # newest first, keep $KEEP of them, never delete the active one
  mapfile -t ALL < <(ls -1 "$ROOT/releases" | grep -E '^[0-9]+\.[0-9]+\.[0-9]+' | sort -rV)
  if [[ ${#ALL[@]} -gt $KEEP ]]; then
    for old in "${ALL[@]:$KEEP}"; do
      if [[ "$ROOT/releases/$old" != "$CURRENT_TARGET" ]]; then
        log "removing old release $old"
        rm -rf "$ROOT/releases/$old"
      fi
    done
  fi
fi

log "LakerProxy $VERSION installed. Status: systemctl status $SERVICE"

if [[ $CONFIG_CREATED -eq 1 ]]; then
  scheme=http
  [[ "${INITIAL_HOSTNAME_WITH_CERTIFICATE,,}" == "true" ]] && scheme=https
  cat <<EOF

First start done. Connect the manager to:
  API url:     $scheme://$INITIAL_HOSTNAME
  admin token: $INITIAL_ADMIN_TOKEN
DNS records this server needs (check them in the manager's Servers tab):
  $INITIAL_HOSTNAME        A     <public ip of this server>
  acme.$INITIAL_HOSTNAME   NS    $INITIAL_HOSTNAME
EOF
fi
