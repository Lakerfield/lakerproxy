# LakerProxy

Self-hosted reverse proxy for .NET, built on [YARP](https://github.com/dotnet/yarp), with automatic
Let's Encrypt certificates and live configuration from MongoDB. Ships with a REST management API
and a Windows desktop manager.

Point a wildcard DNS record at one server, add websites in the manager, and LakerProxy takes care of
routing, TLS certificates and renewals without restarts or config files per site.

## Features

- **Live configuration** — websites live in MongoDB; the proxy watches the collection and rebuilds
  its YARP routes on every change. No restarts, no appsettings edits.
- **Automatic TLS** — Let's Encrypt certificates per hostname via `http-01` or `dns-01`
  (a built-in ACME DNS server answers the `_acme-challenge` records). Single-domain, wildcard and
  wildcard-with-apex scopes. Certificates are stored with the website and renewed automatically.
- **SNI with the full chain** — the certificate is chosen per TLS handshake from the hostname and
  served with the complete issued chain, not a locally rebuilt one.
- **Multiple website kinds** — reverse proxy (optionally keeping the original `Host` header),
  temporary and permanent redirects, static site hosting, info/health pages, ACL-filtered
  redirects, and offline/404 placeholders.
- **Static sites from a zip** — deploy a zip from CI (GitHub Actions, curl) to a per-site
  endpoint with a site-bound deploy key, or upload it from the manager; served from MongoDB with
  pre-compressed Brotli/gzip assets, default documents, SPA fallback and custom 404.
  See [Hosting a static site](#hosting-a-static-site).
- **HTTP/1.1, HTTP/2 and HTTP/3** on port 443, plain HTTP on port 80 with optional HTTPS redirect.
- **Per-website upload limits**, destination protocol selection and tags/filters for organising
  many sites.
- **Built-in protection** — IP blocklist feeds (DShield format or plain IP/CIDR lists) combined
  with a runtime block index fed by suspicious-request detection; allow-list for private ranges.
- **Management REST API** with bearer-token API users and per-user rights, Swagger UI in Debug
  builds.
- **Desktop manager** (WPF) — manage servers, websites, certificates, ACME accounts, API users and
  live logs from one window; multiple proxy servers can be managed from a single manager.
- **Runs as a systemd service** on Linux; the proxy itself is cross-platform (.NET 10).

## Repository layout

| Project | Purpose |
| --- | --- |
| `src/LakerProxy` | Core library: domain models, enums, settings. No ASP.NET dependencies. |
| `src/LakerProxy.Storage` | MongoDB persistence (collections, BSON class maps). |
| `src/LakerProxy.Runtime` | Bootstraps the application runtime and layered ini configuration. |
| `src/LakerProxy.WebApi` | REST API client and DTOs, shared by server and manager. |
| `src/LakerProxy.ProxyApp` | The proxy server executable (ASP.NET Core + YARP), also hosts the management API. |
| `src/LakerProxy.ProxyPlayground` | The WPF desktop manager (`LakerProxyManager.exe`, Windows only). |

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download) to build; `aspnetcore-runtime-10.0` on the
  target server for framework-dependent deployments.
- A MongoDB instance (any recent version; a replica set is not required).
- Ports 80 and 443 reachable from the internet for `http-01` certificates, or UDP/TCP 53 delegated
  to the proxy for `dns-01` via the built-in ACME DNS server.
- Windows for the desktop manager.

### Package feeds

LakerProxy depends on a few `Lakerfield.*` packages. `Lakerfield.Acme` is on nuget.org; the
`Lakerfield.Core`, `Lakerfield.Reactive`, `Lakerfield.Storage` and `Lakerfield.Runtime` packages are
currently served from the Azure DevOps feed listed in `nuget.config`. Read access to that feed
requires credentials, which are **not** part of this repository. Add them to your user-level
NuGet configuration instead of editing `nuget.config`:

```powershell
dotnet nuget update source LakerfieldFeed `
  --username <user> --password <token> --store-password-in-clear-text `
  --configfile "$env:APPDATA\NuGet\NuGet.Config"
```

(Run `dotnet nuget add source` with the feed URL from `nuget.config` first if the source does not
exist in that file yet.) Publishing these packages to nuget.org is on the roadmap.

## Build

```powershell
dotnet build lakerproxy.slnx                          # everything
dotnet build src/LakerProxy.ProxyApp                  # server only
dotnet run --project src/LakerProxy.ProxyApp          # run the server (binds 80/443)
dotnet run --project src/LakerProxy.ProxyPlayground   # run the manager (Windows)
```

Shared package versions are pinned in `Directory.Build.targets`.

## Install on Ubuntu

Server builds are published as [GitHub Releases](https://github.com/lakerfield/lakerproxy/releases)
(`lakerproxy-server-<version>-linux-x64.tar.gz`, self-contained, no .NET runtime needed on the
server). `deploy/install.sh` installs and updates from those releases on Ubuntu or Debian with
systemd.

```bash
curl -fsSL https://raw.githubusercontent.com/lakerfield/lakerproxy/master/deploy/install.sh | sudo bash
```

The first run creates the `lakerproxy` system user, downloads the latest release, verifies its
SHA-256, installs the systemd unit and writes `/opt/lakerproxy/config/app-configuration.ini`.
Edit that file (MongoDB connection string, `initial-admin-token`, `acme-mail`) and start the
service:

```bash
sudo nano /opt/lakerproxy/config/app-configuration.ini
sudo systemctl start lakerproxy
sudo systemctl status lakerproxy
```

Layout on the server:

```
/opt/lakerproxy/
  releases/<version>/             one directory per installed release
  current -> releases/<version>   switched atomically on update
  config/app-configuration.ini    your settings, survive updates
/etc/systemd/system/lakerproxy.service
/usr/local/bin/lakerproxy-update  copy of install.sh from the installed release
```

### Update, rollback, automation

```bash
sudo lakerproxy-update                   # update to the latest release
sudo lakerproxy-update --check           # exit 0 = up to date, 10 = update available
sudo lakerproxy-update --version 2.0.1   # install a specific release, also for rollback
```

An update downloads the release next to the current one, switches the `current` symlink,
restarts the service and waits until the server answers on port 80. If it does not come up, the
script switches back to the previous release and restarts it. The last three releases are kept.

For unattended updates add a cron job, for example nightly:

```
0 4 * * * /usr/local/bin/lakerproxy-update >> /var/log/lakerproxy-update.log 2>&1
```

Notes:

- The service runs as an unprivileged user with `CAP_NET_BIND_SERVICE` for ports 80, 443 and 53.
- HTTP/3 needs `libmsquic` from the Microsoft package repository; without it the server serves
  HTTP/1.1 and HTTP/2 only.
- The script honours `LAKERPROXY_ROOT`, `LAKERPROXY_SERVICE`, `LAKERPROXY_USER` and
  `LAKERPROXY_KEEP` environment variables for non-default locations.

## Install the desktop manager

The Windows manager is distributed with [Velopack](https://velopack.io) and keeps itself up to
date: on start it checks `https://download.lakerproxy.net/manager`, downloads a newer release in
the background and offers a restart. Install it with

```
https://download.lakerproxy.net/manager/LakerProxyManager-win-Setup.exe
```

The default build is framework-dependent: Setup.exe installs the .NET 10 desktop runtime when it
is missing, which keeps the download around 11 MB. Releases can optionally include a portable zip
(`LakerProxyManager-win-Portable.zip`) for machines where nothing may be installed; the portable
build does not self-update.

## Configuration

The server reads `src/LakerProxy/app-configuration.ini` (committed defaults) and layers
`app-configuration-override.ini` on top (git-ignored, put your secrets there). Both are copied
next to the executable on build.

```ini
environment=dev

[storage]
mode=ConnectionString
connectionstring=mongodb://localhost
database=lakerproxy

[data]
# used once to create the first admin API user; log in with this token from the manager
initial-admin-token=
initial-hostname=localhost
initial-hostname-with-certificate=false

acme-mail=
acme-organization=
acme-country=NL

[security]
blocklist-feeds=https://feeds.dshield.org/block.txt
blocklist-refresh-minutes=60
blocklist-allow-list=127.0.0.0/8 ::1 10.0.0.0/8 172.16.0.0/12 192.168.0.0/16
```

## Getting started

1. Start MongoDB and create `app-configuration-override.ini` (development) or edit
   `/opt/lakerproxy/config/app-configuration.ini` (installed server) with your connection string
   and a long random `initial-admin-token`.
2. Run the proxy server. On first start it registers itself as a `Server` document keyed by the
   machine name and creates the initial admin API user from the token.
3. Enable the `Proxy` and `LetsEncrypt` services on that server (via the manager or the API) and
   add a website of kind `ProxyApi` on the hostname where you want to reach the management API.
4. Start the manager, add the proxy server with its API URL and the admin token, and begin adding
   websites. Certificates are requested in the background once a website is saved with a
   certificate scope.

## Hosting a static site

LakerProxy can serve a static site straight from a zip file, no web server or file share on the
proxy host needed. The zip is stored in MongoDB next to the website, so every proxy server in the
cluster serves the same content and a new upload goes live immediately.

### Prepare the zip

- Put the site files at the **root** of the archive: `index.html`, not `dist/index.html`. Paths
  inside the zip map one to one onto URLs.
- `index.html` is the default document for `/` and for every directory. A request for `/contact`
  falls back to `/contact/index.html` when no file named `contact` exists.
- Add a `200.html` for single-page apps: unknown routes are served from it with status 200
  (Surge convention). Without it, unknown routes return `404.html` with status 404 if present
  (GitHub Pages / Netlify convention), otherwise an empty 404.
- Text assets (`.html`, `.js`, `.css`, `.svg`, `.json`, `.webmanifest`) are pre-compressed to
  Brotli and gzip when the zip is loaded and served according to `Accept-Encoding`.
- Cache headers follow common conventions: HTML and `service-worker.js` are `no-store`, other
  `.js` files are cached for a year (use hashed file names), images and fonts for a week.
- The uncompressed size is limited per website via **Max website data size** (default 20 MB,
  hard limit 256 MB). The limit is checked against the real decompressed bytes, not the zip
  headers.

### Upload

1. Create the website with kind **StaticSite** and request a certificate as usual.
2. In the manager, create an API user with role **WebsiteDeploy** and bind it to that website.
   The resulting key can only upload content for this one site.
3. Deploy by posting the zip to the site's own hostname:

   ```bash
   curl -sS -X POST "https://www.example.com/.well-known/lakerproxy/upload-website" \
     -H "Authorization: Bearer <deploy-key>" \
     -F "file=@site.zip"
   ```

The endpoint only answers over HTTPS and only to a valid key bound to that website; everything
else falls through to the normal site, so it is invisible to scanners. Invalid keys count towards
the per-IP block threshold. Each upload replaces the previous content atomically, the old zip is
removed once nothing references it anymore, and uploads are written to the audit log.

For a one-off upload without a deploy key, select the website in the manager and use *Upload zip*;
*Delete zip* detaches the content again.

### Deploy from GitHub Actions

Store the deploy key as a repository secret (for example `LAKERPROXY_DEPLOY_KEY`) and add a step
after your build:

```yaml
- name: Package site
  run: cd dist && zip -r ../site.zip .

- name: Deploy to LakerProxy
  run: |
    curl -sS --fail-with-body -X POST "https://www.example.com/.well-known/lakerproxy/upload-website" \
      -H "Authorization: Bearer ${{ secrets.LAKERPROXY_DEPLOY_KEY }}" \
      -F "file=@site.zip"
```

Zip from inside the output directory so the files land at the root of the archive.

## How it works

1. **Startup** — the server loads or creates its own `Server` document from MongoDB. Its service
   flags decide which background services run: the YARP proxy, the Let's Encrypt renewer and the
   ACME DNS server.
2. **Reactive config** — the proxy subscribes to changes on the `Website` collection and rebuilds
   YARP's in-memory routes/clusters and the hostname → website index on every change.
3. **TLS** — Kestrel's TLS handshake callback resolves the hostname to a website and hands back
   that website's certificate and chain.
4. **Request pipeline** — malicious-IP filter → suspicious-request detection → website resolution
   by hostname → ACME `http-01` challenge handler → HTTPS redirect → per-website body-size limit →
   dispatch on `WebsiteKind` (proxy, redirect, static site, info, API, 404).
5. **Certificates** — issued and renewed by a background service; PFX/PEM material is stored in
   the website document.

## Releasing (maintainers)

Server releases are built by the **Release server** workflow (`.github/workflows/release.yaml`):
GitHub → Actions → Release server → *Run workflow*, enter the semver (for example `2.0.0`; a
`-beta.1` suffix marks a pre-release). The workflow validates the version, publishes the server
for linux-x64, packs it with `version.txt` and `install.sh`, writes a `.sha256` file, tags the
commit as `v<version>` and creates the GitHub Release with generated notes. Servers pick it up
with `lakerproxy-update`.

The workflow needs two repository secrets to restore the `Lakerfield.*` packages:
`LAKERFIELD_FEED_USER` and `LAKERFIELD_FEED_PAT` (a read-only Azure DevOps PAT).

### Manager releases

The **Release manager** workflow (`.github/workflows/release-manager.yaml`) publishes the manager
for win-x64, packs it with `vpk`, signs it and uploads the Velopack feed to the S3 bucket that is
served as `https://download.lakerproxy.net/manager/`. It downloads the previous releases first
so Velopack can produce delta packages, keeps the last ten releases, and finally creates a GitHub
Release tagged `manager-v<version>` with `LakerProxyManager-win-Setup.exe` (and the portable zip
when built) attached. Installed managers keep updating from the S3 feed; the GitHub Release is
for release notes and manual downloads. Server and manager versions may diverge; each workflow takes its own
version input. Two optional checkboxes: **self_contained** bundles the .NET runtime (about
140 MB instead of 11 MB) and **portable** adds the portable zip; both are off by default.

The signing certificate lives on a SafeNet USB token, so this workflow runs on a self-hosted
Windows runner (labels `self-hosted`, `windows`, `signing`) that is started in an interactive
session, not as a service, otherwise the PIN prompt never appears. One-time setup on that machine:

1. Install the GitHub Actions runner and register it with those labels; start it with `run.cmd`.
2. .NET 10 SDK, and the `LakerfieldFeed` credentials in the user-level NuGet config (see
   [Package feeds](#package-feeds)). The workflow installs `vpk` itself.
3. SafeNet Authentication Client with the token inserted; note the certificate thumbprint.

Repository variables: `S3_ENDPOINT` (the provider's S3 API endpoint, not the download domain),
`S3_BUCKET`, optional `S3_PREFIX` (empty = bucket root), optional `S3_REGION`, and
`SIGN_THUMBPRINT` (leave empty for an unsigned test build). Repository secrets: `S3_KEY_ID` and
`S3_SECRET`. When `download.lakerproxy.net` exposes the storage account with the bucket name as
first path segment, a bucket named `manager` with an empty prefix yields exactly the
`https://download.lakerproxy.net/manager/` feed URL that `AppUpdater.cs` expects.
Keep the `vpk` version in the workflow equal to the `Velopack` package version in
`LakerProxy.ProxyPlayground.csproj`.

`assets/download/index.html` is the landing page for `https://download.lakerproxy.net/`: upload it
manually to the bucket root once (it is not part of the workflow). It links to the installer and
reads the current version from `manager/releases.win.json`.

Because the repository is public, restrict who can run workflows on the self-hosted runner:
Settings → Actions → require approval for all outside collaborators, and keep this workflow on
`workflow_dispatch` only.

## Status

LakerProxy has been running production sites for several years but the public repository is new.
Expect rough edges in documentation and no test suite yet. Issues and pull requests are welcome.

## License

To be decided — see the repository's LICENSE file once added.
