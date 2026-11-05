# Runbook: MUSTASK on one plain VM

> A Russian translation for DevOps lives next to this file:
> [vm.ru.md](vm.ru.md). Every change here goes there in the same change.

Deploying MUSTASK on a single Linux VM with no Docker and no Kubernetes,
against the company's **existing shared Keycloak realm**. What is ours in that
realm is only two clients and four client roles; everything else in the realm
belongs to the company ([ADR-0033](../adr/0033-shared-realm-client-roles.md)).

Layout: one domain, one reverse proxy with TLS. The proxy serves the built
panel (static files) and forwards `/api/` to `Mustask.Api` on a loopback port.
PostgreSQL runs on the same VM.

```
browser ──https──> nginx (mustask.example.com; under a path: portal.example.com/mustask, see below)
                     ├─ /        → /opt/mustask/current/panel (panel build)
                     └─ /api/    → http://127.0.0.1:5000 (Mustask.Api, systemd)
Mustask.Api ──> PostgreSQL            Mustask.Api ──https──> shared Keycloak
```

`mustask.example.com` below stands for your domain; `keycloak.example.com` for
the shared Keycloak host.

**Sections 2–9 are scripted** by the kit in
[`deploy/vm/`](../../deploy/vm/): a build script, the systemd unit, the nginx
site, the env template, an install/update script and a smoke check. The whole
path, once the Keycloak side (section 1) is ready:

```bash
# build machine (section 4)
deploy/vm/build-release.sh v0.1.0 https://mustask.example.com
scp out/mustask-v0.1.0.tar.gz <vm>:

# VM, once (sections 2, 3, 5, 7)
tar -xzf mustask-v0.1.0.tar.gz
sudo bash mustask-v0.1.0/kit/install.sh prepare --host mustask.example.com \
    --keycloak-host keycloak.example.com --realm <realm> --install-packages --create-db
sudo nano /etc/mustask/mustask.env        # Keycloak__ClientSecret
sudo bash mustask-v0.1.0/kit/install.sh nginx --host mustask.example.com --cert <…> --key <…>

# VM, every release (sections 8, 9)
sudo bash mustask-v0.1.0/kit/install.sh deploy mustask-v0.1.0
sudo bash mustask-v0.1.0/kit/smoke.sh
```

The kit files are the source of truth for the unit, the site and the env
keys; the sections below say what each step does and why.

**Two layouts.** MUSTASK is served either at the root of its own host name
(`https://mustask.example.com`) or under a path of a host that already has a
server (`https://portal.example.com/mustask`, ADR-0037). The path is one
setting: the release is built for the full address (section 4), the API gets
the same path as `Hosting__PathBase` (section 5), and nginx passes it
unchanged (section 7). The same flow under a path:

```bash
# build machine
deploy/vm/build-release.sh v0.1.0 https://portal.example.com/mustask

# VM, once
sudo bash mustask-v0.1.0/kit/install.sh prepare --host portal.example.com --path /mustask \
    --keycloak-host keycloak.example.com --realm <realm> --install-packages --create-db
sudo nano /etc/mustask/mustask.env        # Keycloak__ClientSecret
sudo bash mustask-v0.1.0/kit/install.sh nginx --path /mustask   # prints the include line
```

Whoever owns the portal's server adds that one `include` line (section 7.2).
One deployment serves one path; moving it means rebuilding the panel and
editing the env file.

This runbook assumes a release that contains tech-024 (client roles) and
tech-025 (the user projection filled at sign-in, ADR-0034). On a release
without them the Keycloak side differs.

## 1. What to ask the Keycloak admin for

Everything in this section is done once by whoever administers the shared
realm. Nothing outside the two clients and four roles is touched. `<realm>`
below is the realm name the company already has; the API only ever sees it as
the last segment of `Keycloak__Authority`.

### Client `mustask-api` (the resource server)

- Protocol OpenID Connect, client authentication **on**, **bearer-only**
  behaviour: standard flow, direct access grants, implicit flow and service
  accounts all **off**. It never signs anyone in; it carries the roles and is
  the token audience.
- Exactly four **client roles**, spelled exactly like this:

  | Client role | What it gives |
  |---|---|
  | `user` | Signing in at all, and reading dictionaries. **Without it there is no session**, whatever else the user holds. |
  | `organization-creator` | Creating an organization. |
  | `dictionaries-admin` | Managing dictionaries. |
  | `system-admin` | Instance administration. |

- No realm roles, no groups, no default roles, no raw permission codes. The
  mapping from these names to permissions lives in the code.
- **Assign `user` directly to every person who should use MUSTASK** (to the
  user itself, not through a group or a default role). Add the other roles on
  top as needed.
- **The first administrator** is an existing employee account of the realm:
  no new account is created for MUSTASK. Name that person in the request, and
  the Keycloak admin assigns them **all four** `mustask-api` client roles
  (`user`, `organization-creator`, `dictionaries-admin`, `system-admin`)
  directly. After their first sign-in they create the first organization and
  add everyone else to it; nobody else needs `organization-creator` until
  another organization is wanted.

### Client `mustask-panel` (the sign-in client)

- Protocol OpenID Connect, **confidential** (client authentication on). The
  admin hands the generated **client secret** to whoever deploys, over a
  private channel.
- Standard flow **on**; implicit flow, direct access grants and service
  accounts **off**. PKCE method `S256`.
- **Valid redirect URI:** `https://mustask.example.com/api/session/callback`
  (exactly this, no wildcard). **Web origins:** empty.
- **Valid post-logout redirect URI:** `https://mustask.example.com/*`.
  **Root URL:** `https://mustask.example.com`.
- **Under a path of a shared host** (`https://portal.example.com/mustask`,
  ADR-0037) the same addresses carry the path:
  - root URL `https://portal.example.com/mustask`;
  - valid redirect URI `https://portal.example.com/mustask/api/session/callback`;
  - valid post-logout redirect URI `https://portal.example.com/mustask/*`;
  - web origins: empty, and if the realm's policy wants one, the bare host
    `https://portal.example.com`, never with the path (an origin has none).
- Default client scopes: `basic`, `acr`, `profile`, `email`, `roles`,
  `web-origins`. `profile` and `email` are required: the sign-in writes the
  user's name and e-mail into MUSTASK from the token claims (ADR-0034).
- Mappers on this client (or on a client scope it uses):
  1. **Audience** mapper, included client audience `mustask-api`, added to the
     **access token** (the ID token does not need it). The API rejects tokens
     without `aud = mustask-api`.
  2. **Client roles in `resource_access`**: the "client roles" mapper
     (`oidc-usermodel-client-role-mapper`) of the standard `roles` scope must
     put client roles into the **access token** under `resource_access`. The
     API reads `resource_access.mustask-api.roles`. The default `roles` scope
     does this; check that the access-token claim is on and that the mapper
     is not limited to another client id.

The sign-in uses pushed authorization requests (PAR) and
`response_mode=form_post`; Keycloak allows both by default, so nothing needs
switching on; just make sure neither is disabled for this client.

How to check: in the console, Clients → `mustask-panel` → Client scopes →
Evaluate. Pick a test user, generate an access token and confirm it contains
`"aud"` with `mustask-api`, `resource_access.mustask-api.roles` with the roles
above, and the name and `email` claims.

## 2. The VM

Assumptions: Linux with systemd (the kit is rehearsed on Ubuntu 24.04), root
through `sudo`, ports 80/443 open to the world, the API bound to
`127.0.0.1:5000` only, PostgreSQL on the same VM.

Unpack the release (section 4) anywhere, for example in your home directory,
and prepare the machine once:

```bash
tar -xzf mustask-v0.1.0.tar.gz
sudo bash mustask-v0.1.0/kit/install.sh prepare \
    --host mustask.example.com --keycloak-host keycloak.example.com --realm <realm> \
    --install-packages --create-db
```

Under a path of a shared host add `--path /mustask` and give the bare host:
`--host portal.example.com --path /mustask`. `prepare` then writes
`Session__PanelUrl=https://portal.example.com/mustask` and
`Hosting__PathBase=/mustask` (section 5). The path is the one the release was
built for (section 4); leave `--path` out at a host root.

`prepare` creates the non-login service user `mustask` (home
`/var/lib/mustask`), `/opt/mustask/releases/` and `/etc/mustask/`, and writes
`/etc/mustask/mustask.env` from the template (section 5); an existing env file
is left as it is, so `prepare` can be run again. With `--run-as <user>` the service
runs as an account that already exists instead, for a VM whose other apps all
run as one account (`--run-as ubuntu`): no `mustask` user is created, the env
file goes to that user's group and `/var/lib/mustask` to the user. `prepare`
never creates an account other than `mustask` and refuses root. The choice is
kept in `/etc/mustask/run-as`, and `deploy` writes it into the unit's
`User=`/`Group=`; to change it, run `prepare` again with another `--run-as`
and deploy. The two flags are the parts
that change the machine, and each is explicit:

- `--install-packages` installs only what is missing of
  `aspnetcore-runtime-10.0`, PostgreSQL, nginx and curl, with
  `apt-get install --no-upgrade`: a PostgreSQL or nginx already on the VM is
  neither upgraded nor restarted. On Ubuntu 24.04 all of them come from the
  distribution's own archive (the rehearsal got ASP.NET Core 10.0.14,
  PostgreSQL 16, nginx 1.24); `/usr/bin/dotnet` is what the unit runs. Leave
  the flag out when the packages are installed another way.
- `--create-db`: section 3.

**The VM must trust the certificate of the Keycloak host.** The API calls
`keycloak.example.com` over HTTPS (discovery, signing keys, token exchange)
and .NET checks the certificate against the system store. If the company
Keycloak uses an internal CA, install that CA on the VM (on Ubuntu: copy it
as a `.crt` into `/usr/local/share/ca-certificates/` and run
`sudo update-ca-certificates`) before the first deploy; otherwise `deploy`
stops waiting for `/health`, which reports the realm as unreachable.

`/var/lib/mustask` must persist across restarts and upgrades: the API encrypts
its session cookie with ASP.NET Core Data Protection keys, which live there
(the unit sets `HOME=/var/lib/mustask` whatever user it runs as). If that directory is wiped, every open session
is invalidated and users sign in again.

## 3. Database

With `--create-db`, `prepare` creates the login role and the database
`mustask` it owns on the local PostgreSQL, under a generated password that it
writes straight into the connection string in `mustask.env`. The password is
never printed; nobody has to know it. A role that already exists is left as
it is, provided `mustask.env` already holds its password (otherwise `prepare`
stops and says so); a missing database is created. An existing database
named `mustask` is taken as it is, whoever owns it: check that it is the one
you mean.

For a PostgreSQL elsewhere, create them yourself and put the string into
`mustask.env` by hand:

```sql
CREATE ROLE mustask LOGIN PASSWORD '<strong password>';
CREATE DATABASE mustask OWNER mustask;
```

The API reads the connection string `ConnectionStrings__mustask` (a Npgsql
string), for example
`Host=127.0.0.1;Port=5432;Database=mustask;Username=mustask;Password=<…>`.

Migrations are **applied explicitly, never at API startup**
([ADR-0002](../adr/0002-explicit-database-migrations.md)). `install.sh deploy`
does it with the migrations bundle of the release (`efbundle`, built with the
release in section 4) before it switches to the new code: on the VM itself,
with the connection string from `mustask.env`, no SDK, no checkout and no
tunnel to the database. A bundle applies only the migrations the database
does not have yet, so running it on every deploy is safe.

Without the kit, `dotnet ef database update --project
src/backend/Mustask.Infrastructure` from a checkout at the release does the
same; on a fresh checkout run `dotnet restore src/backend/Mustask.Infrastructure`
first, or `dotnet ef` stops with `NETSDK1004`.

## 4. Build

On the build machine (any OS with bash — Git Bash on Windows works — plus git,
the .NET 10 SDK, `dotnet-ef` 10.x and Node.js with npm):

```bash
git fetch --tags
dotnet tool install --global dotnet-ef        # once per build machine
deploy/vm/build-release.sh v0.1.0 https://mustask.example.com
```

The script builds **the tag, not the working tree**: it checks the tag out
into a throwaway worktree, so local edits never reach the VM, and removes it
afterwards. The second argument is the site address, no trailing slash: the
host alone (`https://mustask.example.com`) or the host with a path
(`https://portal.example.com/mustask`; segments of letters, digits, `.`, `_`
and `-`, no port). The address is baked into the panel as
`VITE_API_BASE_URL` (panel and API share one address, so it is the site
itself), and the path, `/` when there is none, as `VITE_BASE_PATH`, the base
the panel is served under. It is also recorded in `VERSION`, and `install.sh
deploy` refuses a release whose address differs from `Session__PanelUrl`. The script also checks that the path really reached
the panel build (the scripts in `panel/index.html` sit under it). In Git Bash
it keeps the path from being rewritten into a Windows path
(`C:/Program Files/Git/mustask`) on its way to Node; do not set
`MSYS_NO_PATHCONV=1` for the whole script, `git` then cannot find the
repository. The result is one file, `out/mustask-v0.1.0.tar.gz`:

| Inside | What |
|---|---|
| `api/` | `dotnet publish` of `Mustask.Api`, framework-dependent: it runs on the VM through `dotnet` |
| `panel/` | the panel build |
| `efbundle` | the migrations bundle, linux-x64, self-contained |
| `kit/` | `install.sh`, `smoke.sh`, the unit, the nginx site and the nginx locations snippet, the env template, taken from the checkout the script runs in |
| `VERSION` | tag, commit, kit commit, site address, build time |

Copy it to the VM and unpack it:

```bash
scp out/mustask-v0.1.0.tar.gz <vm>:
ssh <vm> tar -xzf mustask-v0.1.0.tar.gz
```

## 5. Configuration: environment variables

The API is configured only through `/etc/mustask/mustask.env` (root-owned,
mode `0640`, group `mustask`), written by `prepare` from
[`deploy/vm/mustask.env.template`](../../deploy/vm/mustask.env.template).
`__` is how the environment spells `:` in a configuration key. `prepare` fills
the site address, the Keycloak addresses and, with `--create-db`, the
connection string. **One value is left for you: `Keycloak__ClientSecret`**,
the `mustask-panel` secret from the Keycloak admin. Edit the file on the VM
(`sudo nano /etc/mustask/mustask.env`); never paste the secret into a chat,
a ticket or the repository. `install.sh deploy` refuses to run while a
setting still holds `CHANGE_ME`.

| Key | Value |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ASPNETCORE_URLS` | `http://127.0.0.1:5000`: loopback only, nginx is in front. Keep the port: the nginx site and `install.sh` use it |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | `true`: the proxy terminates TLS; without it the API sees `http://` and builds redirect addresses that do not match the ones registered in Keycloak |
| `ConnectionStrings__mustask` | section 3 |
| `Keycloak__Authority` | `https://keycloak.example.com/realms/<realm>`, no trailing slash |
| `Keycloak__Audience` | `mustask-api` |
| `Keycloak__Issuer` | optional, defaults to the authority; set it only when tokens carry another `iss` |
| `Keycloak__ClientSecret` | the `mustask-panel` secret. Outside Development the API refuses to start if it is shorter than 32 characters or a known placeholder |
| `Session__PanelUrl` | `https://mustask.example.com`, no trailing slash; under a path of a shared host it carries the path: `https://portal.example.com/mustask`. Every redirect after sign-in is built as `{PanelUrl}{returnUrl}` |
| `Hosting__PathBase` | the path part of `Session__PanelUrl`: `/mustask`, or empty at a host root. The API refuses to start when it differs from the path of `PanelUrl` (ADR-0037). `prepare --path` fills both |

Notes:

- `Keycloak__Authority`, `Keycloak__Audience`, `Keycloak__ClientSecret` and
  `Session__PanelUrl` are mandatory: a missing one stops the host at startup
  with a message naming the key. `Keycloak__RequireHttpsMetadata` defaults to
  on outside Development; leave it.
- **`Cors__AllowedOrigins__0` is not needed in this layout.** `Mustask.Api`
  enables CORS only in Development; with the panel and the API under one
  domain there is no cross-origin call. If you ever serve the panel from
  another origin, the CORS headers must come from the proxy.
- The file is in systemd's format, not shell: values hold `;`. Do not
  `source` it.
- Secrets live only in this file (or a secret store feeding it), never in the
  repository or in `appsettings*.json`.

## 6. systemd unit

[`deploy/vm/mustask-api.service`](../../deploy/vm/mustask-api.service),
installed by `install.sh deploy` as `/etc/systemd/system/mustask-api.service`
and enabled. It runs `/usr/bin/dotnet /opt/mustask/current/api/Mustask.Api.dll`
as the user of `/etc/mustask/run-as` (`mustask` by default, section 2), with
the env file of section 5, `ProtectSystem=strict` and
`/var/lib/mustask` as the only writable path (the Data Protection keys). If
`dotnet` is installed elsewhere, fix `ExecStart` in the kit file before the
build.

```bash
journalctl -u mustask-api -f       # structured JSON log lines on the console
```

## 7. Reverse proxy and TLS (nginx)

Two layouts; pick the one that matches the address you built the release for
(section 4).

- **Own host name** (`https://mustask.example.com`): MUSTASK gets a whole
  `server {}` of its own. Subsection 7.1.
- **A path of a shared host** (`https://portal.example.com/mustask`): the
  host already has a server, owned by someone else; MUSTASK adds locations
  to it. Subsection 7.2.

In both, nginx passes the path to the API unchanged (`proxy_pass` with no URI
part). The API itself serves under its path base (`Hosting__PathBase`,
section 5), so nothing is stripped or rewritten on the way.

HTTPS is not optional: the session cookie `mustask.session` is `Secure`,
`HttpOnly`, `SameSite=Lax`, path `{path}/api/session` (`/api/session` at a
host root), and is never sent over plain HTTP.

### 7.1 Own host name

The site is [`deploy/vm/nginx-mustask.conf`](../../deploy/vm/nginx-mustask.conf):
port 80 redirects to HTTPS; on 443 `/api/` goes to `127.0.0.1:5000` with the
forwarded headers, everything else is the panel from
`/opt/mustask/current/panel` with a fallback to `index.html`. `/health` and
`/alive` are not exposed. The certificate is yours (the company's, or
certbot's); point the script at its files:

```bash
sudo bash mustask-v0.1.0/kit/install.sh nginx --host mustask.example.com \
    --cert /etc/letsencrypt/live/mustask.example.com/fullchain.pem \
    --key  /etc/letsencrypt/live/mustask.example.com/privkey.pem
```

It renders `/etc/nginx/sites-available/mustask`, enables it, runs `nginx -t`
and reloads nginx. If the test fails, the site from an earlier run is put
back (or, on a first run, the new one is disabled), so a broken site never
stays enabled for the next reload. Two things in the site do touch the
other sites of that nginx: on nginx before 1.25.1 `http2` on a `listen 443`
line turns HTTP/2 on for every server on that port, and if no other server
is the `default_server` for 443, this one answers names nobody configured. Two settings in the file are there for a reason found by
the rehearsal (tech-056):

- `listen 443 ssl http2;` is for the nginx that Ubuntu 24.04 and Debian 12
  ship (1.24 and 1.22): they stop at `http2 on;` with `unknown directive
  "http2"`. On 1.25.1 and later the `listen` form still works and only logs a
  deprecation warning.
- `proxy_buffer_size 32k` and the two lines after it: the sign-in callback
  answers with the session cookie (~3.5 KB on a bare realm, more with every
  claim the shared realm adds) plus the OIDC cookies it clears; with nginx's
  default 4–8 KB header buffer every sign-in gets a 502.

### 7.2 A path of a shared host

The server for `portal.example.com` (its names, its certificate, its port-80
redirect) is not MUSTASK's, and the kit does not write it. It installs only
the locations, from
[`deploy/vm/nginx-mustask-locations.conf`](../../deploy/vm/nginx-mustask-locations.conf):

- `location /mustask/api/` goes to `127.0.0.1:5000` with the forwarded
  headers and the same three proxy buffer settings as in 7.1 (the reason is
  the same: without them every sign-in gets a 502);
- `location /mustask/` serves the panel from
  `/opt/mustask/current/panel/` through `alias`, and an unknown path falls
  back to `/mustask/index.html`; `/mustask` without the slash redirects to
  `/mustask/`;
- `/mustask/health` is not exposed: under the prefix it is the panel's
  `index.html`, as at a root. `/health` and `/alive` stay on the loopback.

```bash
sudo bash mustask-v0.1.0/kit/install.sh nginx --path /mustask
```

There is no `--host`, `--cert` or `--key` here, and no `default_server`: the
script renders the locations into
`/etc/nginx/snippets/mustask-locations.conf`, checks them with `nginx -t`
inside a throwaway server of their own (nothing includes them yet), checks
the whole current configuration, and prints the one line the owner of the
portal's server must add inside its `server { … }` block:

```nginx
include /etc/nginx/snippets/mustask-locations.conf;
```

**The owner of the portal's server has to agree to this include.** It is an
organizational step of the deployment: ask for it together with the Keycloak
request of section 1. After they add it, `nginx -t && systemctl reload
nginx` makes it live. A later run of the script reloads nginx itself once the
include exists. If either test fails, the snippet installed earlier is
restored (or the new one removed), so a broken snippet is never live.

The `/mustask` locations are `^~` prefix locations, so a regex location of the
portal's server (`location ~* .js$`) does not take their paths; a prefix
location of the portal's own at `/mustask` would still clash with the include.

## 8. First start and smoke check

```bash
sudo bash mustask-v0.1.0/kit/install.sh deploy mustask-v0.1.0
sudo bash mustask-v0.1.0/kit/smoke.sh
```

`deploy` copies the release to `/opt/mustask/releases/v0.1.0`, applies the
migrations (section 3), installs the unit, points `/opt/mustask/current` at
the release, restarts the service and waits up to a minute for `/health`. If
it stays unhealthy, `deploy` prints the last lines of the service log and
stops; the log says whether the database or Keycloak is the problem.

`smoke.sh` checks, from the VM: `/health` answers `Healthy` (it covers the
database and the reachability of the realm); `http://` redirects to
`https://`; the panel is served at the site address, its first script is served from the
path `index.html` names (a panel built for another path base shows a blank
page) and a client route falls back to `index.html`; the API answers through the proxy (401 without a
token); `/api/session/login` redirects to the realm; `/health` is not
reachable through the proxy. Under a path, every address is the site address
with its path (`https://portal.example.com/mustask/…`), and the `http://`
check expects the redirect from the portal's own port-80 server. Before DNS points at the VM add
`--resolve-local`; `--insecure` accepts a certificate the VM does not trust
(rehearsals only).

The rest needs a person in a browser; `smoke.sh` prints it as a list:

1. `https://mustask.example.com/` (under a path:
   `https://portal.example.com/mustask/`) shows the panel; signing in goes to
   `keycloak.example.com` and back to the panel after the login.
2. A user with `user` signs in and lands in the panel. A user **without**
   `user` comes back with `authError=no_access` and has no session.
3. The roles take effect: a user with only `user` opens the dictionaries but
   is refused creating an organization ("Недостаточно прав"); the first
   administrator (all four roles, section 1) creates the first organization.
   If the administrator is refused too, run the Evaluate check from section 1
   for them: `resource_access.mustask-api.roles` must list all four roles.
4. After the first sign-in the user appears in member search (the directory
   fills at sign-in; there is no import): the organization's creator finds
   the other test user by name or e-mail when adding a member.

## 9. Updating and rolling back

Build the new tag (section 4), copy it to the VM, then:

```bash
sudo bash mustask-v0.2.0/kit/install.sh deploy mustask-v0.2.0
sudo bash mustask-v0.2.0/kit/smoke.sh
```

`deploy` applies the new migrations, switches `current` to the new release
and keeps the one it replaced as `/opt/mustask/previous`, if that one ever
turned healthy: deploying a fix over a failed release keeps the last good one
as the rollback target. Deploying a tag
that is already installed replaces it in place.

```bash
sudo bash mustask-v0.2.0/kit/install.sh rollback    # current <-> previous, restart
sudo bash mustask-v0.2.0/kit/install.sh status      # the active VERSION and the service state
```

**Rolling back does not revert migrations.** The previous code runs against
the newer schema, which works while the migrations are additive; a release
with a destructive migration needs its own rollback note. Old releases stay
under `/opt/mustask/releases/`; delete the ones you no longer need by hand
(never `current` or `previous`).

## 10. Troubleshooting

| Symptom | Likely cause |
|---|---|
| `build-release: dotnet-ef is missing` | `dotnet tool install --global dotnet-ef` (section 4). |
| `install: … still has CHANGE_ME on line(s) …` | `mustask.env` still has a template value: usually `Keycloak__ClientSecret` (section 5). |
| `install: the release is built for … but Session__PanelUrl is …` | The tarball was built for another site address: rebuild it with the address of this VM (section 4). |
| `install: nginx -t failed; the site is written but not enabled` | The certificate or key file is not what nginx can load; `nginx -t` above says which line. |
| `install: cannot reach the local PostgreSQL` | PostgreSQL is not running: `systemctl status postgresql`. |
| `deploy` stops: `/health did not turn healthy in 60 s` | The log printed above it names the cause: the database string or the realm (next rows). `install.sh rollback` returns the previous release if there is one. |
| API exits at start naming a `Keycloak:*` or `Session:PanelUrl` key | Missing variable in `mustask.env`. |
| API exits at start about a "known placeholder or shorter than 32" secret | `Keycloak__ClientSecret` is not the real `mustask-panel` secret. |
| `nginx -t`: `unknown directive "http2"` | nginx older than 1.25.1 with an edited site: keep `listen 443 ssl http2;` (section 7). |
| After the Keycloak login the browser gets **502** on `/api/session/callback`; nginx's error log says "upstream sent too big header" | The proxy buffers from section 7 are missing: the session cookie does not fit nginx's default header buffer. |
| `/health` is unhealthy naming the realm; the log has a TLS / certificate error | The VM does not trust the CA of the Keycloak certificate (section 2). |
| `dotnet ef` fails with `NETSDK1004` (without the kit) | Run `dotnet restore src/backend/Mustask.Infrastructure` first (section 3). |
| API exits at start about `Hosting:PathBase` | The path of `Session__PanelUrl` and `Hosting__PathBase` differ, or the base is malformed (section 5). Under a path both carry it: `https://portal.example.com/mustask` and `/mustask`; at a host root `Hosting__PathBase` is empty. |
| `install: the snippet failed nginx -t` (with `--path`) | The snippet did not pass `nginx -t` inside a server of its own; the message above names the line. The snippet installed earlier is left as it was. |
| `install: nginx -t fails on the current configuration` (with `--path`) | The portal's configuration does not pass with the new snippet included; the previous snippet is restored (or the new one removed). The message above names the line. |
| Under a path: the panel loads but its scripts are 404, or the sign-in loops | The panel was built for another path than the one nginx serves, or the `include` is missing in the portal's server (section 7). Rebuild with the full address (section 4). |
| Keycloak shows "Invalid redirect URI" | `mustask-panel` lacks `https://mustask.example.com/api/session/callback` (under a path: `https://portal.example.com/mustask/api/session/callback`), or `ASPNETCORE_FORWARDEDHEADERS_ENABLED` is missing and the API sent an `http://` address. |
| Every API call is 401 after sign-in | The access token has no `aud = mustask-api`, or `Keycloak__Authority`/`Keycloak__Issuer` differ from the token's `iss`. |
| Sign-in comes back `authError=no_access` | The user does not hold the `mustask-api` client role `user` (assign it directly). |
| Signed in, but a role does not take effect | `resource_access.mustask-api.roles` is missing from the access token (mapper, section 1). |
| Everyone is signed out after a restart | `/var/lib/mustask` (Data Protection keys) is not persistent or not writable by the service. |

## What this runbook does not verify

Sections 1–8 were first walked by hand on 2026-11-05 (tech-056) on a clean
Ubuntu 24.04 with systemd standing in for the VM, against a separate
Keycloak 26.4 whose realm was set up exactly as section 1 asks; that
rehearsal also signed users in through the real login form by a script and
checked the roles and the first administrator. The same day the kit
(tech-058) was walked on another clean Ubuntu 24.04 the same way: release
`v0.1.0` (`8aed774b`) built from a Windows machine, then `prepare` with both
flags, the CA, the secret by hand, `nginx`, `deploy`, `smoke.sh` (passed), a
second `deploy`, `rollback` and a redeploy of the same tag. The records are
in `docs/features/tech-056-vm-runbook-rehearsal/status.md` and
`docs/features/tech-058-vm-deploy-kit/status.md`.

Still unverified, because a rehearsal cannot have them:

- **The company network and realm.** Real DNS names, the company's
  certificate chain, firewalls between the VM and Keycloak, and a realm that
  already carries the company's own scopes, mappers, policies, identity
  providers and required actions. Any of them can change the token (its size
  too, see the proxy buffers in section 7) or the login flow.
- **A browser sign-in.** The rehearsals' certificates were self-signed, so
  sign-ins went through the login form by a script, not a browser.
- **An update with a new migration.** The update was rehearsed with the same
  build deployed under a second tag, so its bundle had nothing new to apply.
- **The portal's own nginx.** The sub-path layout (tech-057, ADR-0037) was
  rehearsed on 2026-11-05 on a clean Ubuntu 24.04 with systemd, against Keycloak
  26.4 with a realm set up by section 1 for `https://portal.t057.localhost/mustask`:
  release built at that address, `prepare --path`, `nginx --path`, one `include`
  in a server block playing the portal, `deploy`, `smoke.sh` passed, and scripted
  sign-ins (administrator, `user`-only, no role) behaved as section 8 says, the
  cookie path `/mustask/api/session` included. What a rehearsal cannot have is
  the real portal's server (its regex locations, its own limits) and its owner's
  agreement to the `include`.

The first real deployment should walk section 8 again and fix this document
where it differs.
