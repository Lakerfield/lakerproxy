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

if [[ "$CURRENT_VERSION" == "$VERSION" && $FORCE -eq 0 ]]; then
  log "version $VERSION is already installed, nothing to do (use --force to reinstall)"
  exit 0
fi

log "installed: ${CURRENT_VERSION:-none}, installing: $VERSION"

# ---------------------------------------------------------------------------
# user, directories, configuration
# ---------------------------------------------------------------------------

if ! id -u "$SVC_USER" >/dev/null 2>&1; then
  log "creating system user $SVC_USER"
  useradd --system --no-create-home --home-dir "$ROOT" --shell /usr/sbin/nologin "$SVC_USER"
fi

install -d -m 755 "$ROOT" "$ROOT/releases"
install -d -m 750 -o root -g "$SVC_USER" "$ROOT/config"

CONFIG_FILE="$ROOT/config/app-configuration.ini"
CONFIG_CREATED=0
if [[ ! -f "$CONFIG_FILE" ]]; then
  CONFIG_CREATED=1
  log "writing initial configuration to $CONFIG_FILE"
  cat > "$CONFIG_FILE" <<'EOF'
# LakerProxy server configuration. Values here override the defaults shipped
# with the release (app-configuration.ini). Edit, then: systemctl restart lakerproxy
environment=production

[storage]
mode=ConnectionString
connectionstring=mongodb://localhost
database=lakerproxy

[data]
# used once to create the first admin API user; make it long and random
initial-admin-token=
initial-hostname=localhost
initial-hostname-with-certificate=false

acme-mail=
acme-organization=
acme-locality=
acme-state=
acme-country=NL
EOF
  chmod 640 "$CONFIG_FILE"
  chown root:"$SVC_USER" "$CONFIG_FILE"
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
systemctl enable "$SERVICE" >/dev/null 2>&1 || true

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

if [[ $CONFIG_CREATED -eq 1 ]]; then
  log "first install: edit $CONFIG_FILE (MongoDB connection string, initial-admin-token, acme-mail)"
  log "then start the server with: systemctl start $SERVICE"
  exit 0
fi

if [[ $NO_START -eq 1 ]]; then
  log "service not restarted (--no-start); run: systemctl restart $SERVICE"
  exit 0
fi

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
