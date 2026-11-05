# Runbook: MUSTASK on one plain VM

Deploying MUSTASK on a single Linux VM with no Docker and no Kubernetes,
against the company's **existing shared Keycloak realm**. What is ours in that
realm is only two clients and four client roles; everything else in the realm
belongs to the company ([ADR-0033](../adr/0033-shared-realm-client-roles.md)).

Layout: one domain, one reverse proxy with TLS. The proxy serves the built
panel (static files) and forwards `/api/` to `Mustask.Api` on a loopback port.
PostgreSQL is a separate service on the same VM or elsewhere.

```
browser ──https──> nginx (mustask.example.com)
                     ├─ /        → /var/www/mustask (panel build)
                     └─ /api/    → http://127.0.0.1:5000 (Mustask.Api, systemd)
Mustask.Api ──> PostgreSQL            Mustask.Api ──https──> shared Keycloak
```

`mustask.example.com` below stands for your domain; `keycloak.example.com` for
the shared Keycloak host.

**MUSTASK must own the root of its host name.** The current release cannot be
served under a sub-path such as `https://portal.example.com/mustask`: the panel
bundle and its routes, the API paths, the session cookie path and the sign-in
redirect addresses all assume `/`, and proxy rewrites do not fix that.
Sub-path support is its own task (tech-057); until it ships, give MUSTASK a
host name of its own (for example `mustask.example.com`).

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

Assumptions (adjust paths freely): Linux with systemd, a non-login service user
`mustask`, ports 80/443 open to the world, the API bound to `127.0.0.1:5000`
only.

Install:

- the **.NET 10 ASP.NET Core runtime** (the SDK on the machine that builds);
- PostgreSQL itself if it is on this VM, the client tools if it is remote;
- nginx (any reverse proxy works the same way) and a certificate for
  `mustask.example.com` (for example certbot).

On Ubuntu 24.04 all three come from the distribution's own archive, no extra
repository (the rehearsal got ASP.NET Core 10.0.14, PostgreSQL 16, nginx
1.24); `dotnet` lands in `/usr/lib/dotnet` with `/usr/bin/dotnet` linked to it,
which is what the unit in section 6 runs:

```bash
sudo apt-get install -y aspnetcore-runtime-10.0 postgresql nginx
```

**The VM must trust the certificate of the Keycloak host.** The API calls
`keycloak.example.com` over HTTPS (discovery, signing keys, token exchange)
and .NET checks the certificate against the system store. If the company
Keycloak uses an internal CA, install that CA on the VM (on Ubuntu: copy it
as a `.crt` into `/usr/local/share/ca-certificates/` and run
`sudo update-ca-certificates`) before the first start; otherwise `/health`
in section 8 reports the realm as unreachable.

```bash
sudo useradd --system --create-home --home-dir /var/lib/mustask --shell /usr/sbin/nologin mustask
sudo mkdir -p /opt/mustask/api /var/www/mustask /etc/mustask
```

`/var/lib/mustask` must persist across restarts and upgrades: the API encrypts
its session cookie with ASP.NET Core Data Protection keys, which by default
live in the service user's home directory. If that directory is wiped, every
open session is invalidated and users sign in again.

## 3. Database

Create an empty database and a login that owns it (on a PostgreSQL installed
on the VM: `sudo -u postgres psql`):

```sql
CREATE ROLE mustask LOGIN PASSWORD '<strong password>';
CREATE DATABASE mustask OWNER mustask;
```

The API reads the connection string `ConnectionStrings__mustask` (a Npgsql
string), for example
`Host=127.0.0.1;Port=5432;Database=mustask;Username=mustask;Password=<…>`.

Migrations are **applied explicitly, never at API startup**
([ADR-0002](../adr/0002-explicit-database-migrations.md); see also
[local-migrations.md](../runbooks/local-migrations.md), "External database").
Apply them from a machine that has the .NET 10 SDK, the repository checked out
at the release you deploy, and network access to the database:

```bash
dotnet tool install --global dotnet-ef        # once per build machine
dotnet restore src/backend/Mustask.Infrastructure
export ConnectionStrings__mustask='Host=<db host>;Port=5432;Database=mustask;Username=mustask;Password=<…>'
dotnet ef database update --project src/backend/Mustask.Infrastructure
```

The `dotnet restore` is not optional on a fresh checkout: without it
`dotnet ef` stops with `NETSDK1004` (`project.assets.json` not found) before it
touches the database.

A PostgreSQL installed on the VM by the package listens on `127.0.0.1` only,
so the build machine does not reach it directly. Do not open it to the
network for this; forward the port over SSH for the duration of the command
and point the string at the local end:

```bash
ssh -N -L 15432:127.0.0.1:5432 <vm> &      # stop it after the update
export ConnectionStrings__mustask='Host=127.0.0.1;Port=15432;Database=mustask;Username=mustask;Password=<…>'
```

Run this before the first start and before every release that adds a
migration. `dotnet ef migrations bundle` can produce a single executable to run
on a VM without the SDK; this repository does not publish one today, so treat
that as an option to set up, not as something verified here.

## 4. Build and install

On the build machine, at the release you deploy:

```bash
# API
dotnet publish src/backend/Mustask.Api -c Release -o publish/api

# Panel: VITE_API_BASE_URL is baked into the bundle at build time and the
# build fails without it. One domain, so it is the site itself (no trailing
# slash).
cd src/frontend
npm ci
VITE_API_BASE_URL=https://mustask.example.com npm run build   # output: dist/
```

Copy `publish/api/` to `/opt/mustask/api/` and `src/frontend/dist/` to
`/var/www/mustask/` (replace the old contents; keep the API directory owned by
root and readable by `mustask`). The build machine may be any OS: the publish
is framework-dependent, and the VM runs `Mustask.Api.dll` through `dotnet`.
`publish/` is not in `.gitignore`; build in a throwaway checkout or delete it
afterwards.

## 5. Configuration: environment variables

Put them in `/etc/mustask/mustask.env` (root-owned, mode `0640`, group
`mustask`). `__` is how the environment spells `:` in a configuration key.

```ini
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://127.0.0.1:5000
# The proxy terminates TLS; without this the API sees http:// and builds
# redirect addresses that do not match the ones registered in Keycloak.
ASPNETCORE_FORWARDEDHEADERS_ENABLED=true

ConnectionStrings__mustask=Host=127.0.0.1;Port=5432;Database=mustask;Username=mustask;Password=CHANGE_ME

# Shared realm. Authority is the realm address, no trailing slash.
Keycloak__Authority=https://keycloak.example.com/realms/<realm>
Keycloak__Audience=mustask-api
# Optional: defaults to Authority. Set it only when tokens carry another iss.
Keycloak__Issuer=https://keycloak.example.com/realms/<realm>
# Secret of the confidential client mustask-panel (from the Keycloak admin).
# Outside Development the API refuses to start if it is shorter than 32
# characters or is a known placeholder.
Keycloak__ClientSecret=CHANGE_ME_AT_LEAST_32_CHARACTERS

# Absolute address of the panel, no trailing slash. Every redirect after
# sign-in is built as {PanelUrl}{returnUrl}.
Session__PanelUrl=https://mustask.example.com
```

Notes:

- `Keycloak__Authority`, `Keycloak__Audience`, `Keycloak__ClientSecret` and
  `Session__PanelUrl` are mandatory: a missing one stops the host at startup
  with a message naming the key. `Keycloak__RequireHttpsMetadata` defaults to
  on outside Development; leave it.
- **`Cors__AllowedOrigins__0` is not needed in this layout.** `Mustask.Api`
  enables CORS only in Development; in Production CORS is the deployment's job,
  and with the panel and the API under one domain there is no cross-origin
  call. Setting the variable in Production does nothing. If you ever serve the
  panel from another origin, the CORS headers must come from the proxy.
- Secrets live only in this file (or a secret store feeding it), never in the
  repository or in `appsettings*.json`.

## 6. systemd unit

`/etc/systemd/system/mustask-api.service`:

```ini
[Unit]
Description=MUSTASK API
After=network-online.target postgresql.service
Wants=network-online.target

[Service]
Type=exec
User=mustask
Group=mustask
WorkingDirectory=/opt/mustask/api
EnvironmentFile=/etc/mustask/mustask.env
Environment=HOME=/var/lib/mustask
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false
ExecStart=/usr/bin/dotnet /opt/mustask/api/Mustask.Api.dll
Restart=on-failure
RestartSec=5
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ProtectHome=true
ReadWritePaths=/var/lib/mustask
LimitNOFILE=65536

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now mustask-api
journalctl -u mustask-api -f       # structured JSON log lines on the console
```

If `dotnet` is installed elsewhere, fix `ExecStart`. `ReadWritePaths` keeps the
Data Protection key store writable under `ProtectSystem=strict`.

## 7. Reverse proxy and TLS (nginx)

`/etc/nginx/sites-available/mustask`:

```nginx
server {
    listen 80;
    server_name mustask.example.com;
    return 301 https://$host$request_uri;
}

server {
    # nginx 1.25.1 and later: `listen 443 ssl;` plus `http2 on;` instead.
    listen 443 ssl http2;
    server_name mustask.example.com;

    ssl_certificate     /etc/letsencrypt/live/mustask.example.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/mustask.example.com/privkey.pem;

    # The API: only /api/ is exposed. /health and /alive stay internal.
    location /api/ {
        proxy_pass http://127.0.0.1:5000;
        proxy_set_header Host              $host;
        proxy_set_header X-Real-IP         $remote_addr;
        proxy_set_header X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-Host  $host;
        proxy_read_timeout 60s;
        # The sign-in callback answers with the session cookie (the encrypted
        # ticket with the tokens, ~3.5 KB on a bare realm, more with every
        # claim the shared realm adds) plus the OIDC cookies it clears. The
        # default 4–8 KB header buffer is too small: nginx answers 502.
        proxy_buffer_size 32k;
        proxy_buffers 8 32k;
        proxy_busy_buffers_size 64k;
    }

    # The panel: a single-page app, unknown paths fall back to index.html.
    root /var/www/mustask;
    location / {
        try_files $uri /index.html;
    }
}
```

```bash
sudo ln -s /etc/nginx/sites-available/mustask /etc/nginx/sites-enabled/
sudo nginx -t && sudo systemctl reload nginx
```

`ssl http2` on the `listen` line is for the nginx that Ubuntu 24.04 and
Debian 12 ship (1.24 and 1.22): they stop at `http2 on;` with `unknown
directive "http2"`. On 1.25.1 and later the `listen` form still works and
only logs a deprecation warning.

HTTPS is not optional: the session cookie `mustask.session` is `Secure`,
`HttpOnly`, `SameSite=Lax`, path `/api/session`, and is never sent over plain
HTTP.

## 8. Smoke check

1. On the VM: `curl -fsS http://127.0.0.1:5000/health` answers healthy. It
   covers the database and the reachability of the realm, so a failure here is
   a database or Keycloak connection problem (the log says which).
2. `https://mustask.example.com/` shows the panel; signing in goes to
   `keycloak.example.com` and back to the panel after the login.
3. A user with `user` signs in and lands in the panel. A user **without**
   `user` comes back with `authError=no_access` and has no session.
4. The roles take effect: a user with only `user` opens the dictionaries but
   is refused creating an organization ("Недостаточно прав"); the first
   administrator (all four roles, section 1) creates the first organization. If the second user is refused
   too, run the Evaluate check from section 1 for that user:
   `resource_access.mustask-api.roles` must list both roles.
5. After the first sign-in the user appears in member search (the directory
   fills at sign-in; there is no import): the organization's creator finds
   the other test user by name or e-mail when adding a member.

## 9. Updating

1. Build the new release as in section 4.
2. Apply new migrations (section 3).
3. Replace `/var/www/mustask/` and `/opt/mustask/api/`, then
   `sudo systemctl restart mustask-api`.

Rolling back the code is putting the two directories back; migrations are not
reverted automatically, so a release with a destructive migration needs its own
rollback note.

## 10. Troubleshooting

| Symptom | Likely cause |
|---|---|
| API exits at start naming a `Keycloak:*` or `Session:PanelUrl` key | Missing variable in `mustask.env`. |
| API exits at start about a "known placeholder or shorter than 32" secret | `Keycloak__ClientSecret` is not the real `mustask-panel` secret. |
| `nginx -t`: `unknown directive "http2"` | nginx older than 1.25.1: use `listen 443 ssl http2;` (section 7). |
| After the Keycloak login the browser gets **502** on `/api/session/callback`; nginx's error log says "upstream sent too big header" | The proxy buffers from section 7 are missing: the session cookie does not fit nginx's default header buffer. |
| `/health` is unhealthy naming the realm; the log has a TLS / certificate error | The VM does not trust the CA of the Keycloak certificate (section 2). |
| `dotnet ef` fails with `NETSDK1004` | Run `dotnet restore src/backend/Mustask.Infrastructure` first (section 3). |
| Keycloak shows "Invalid redirect URI" | `mustask-panel` lacks `https://mustask.example.com/api/session/callback`, or `ASPNETCORE_FORWARDEDHEADERS_ENABLED` is missing and the API sent an `http://` address. |
| Every API call is 401 after sign-in | The access token has no `aud = mustask-api`, or `Keycloak__Authority`/`Keycloak__Issuer` differ from the token's `iss`. |
| Sign-in comes back `authError=no_access` | The user does not hold the `mustask-api` client role `user` (assign it directly). |
| Signed in, but a role does not take effect | `resource_access.mustask-api.roles` is missing from the access token (mapper, section 1). |
| Everyone is signed out after a restart | `/var/lib/mustask` (Data Protection keys) is not persistent or not writable by the service. |

## What this runbook does not verify

Sections 1–8 were walked literally on 2026-11-05 (tech-056) on a clean
Ubuntu 24.04 with systemd standing in for the VM, deploying `main` at
`927cf1d7`, against a separate Keycloak 26.4 whose realm was set up by hand
exactly as section 1 asks, with nothing of MUSTASK's but the two clients and
four client roles. Section 8 passed there, the Evaluate check of section 1
included; the divergences found are fixed above (the record is in
`docs/features/tech-056-vm-runbook-rehearsal/status.md`).

Still unverified, because the rehearsal could not have them:

- **The company network and realm.** Real DNS names, the company's
  certificate chain, firewalls between the VM, PostgreSQL and Keycloak, and
  a realm that already carries the company's own scopes, mappers, policies,
  identity providers and required actions. Any of them can change the token
  (its size too, see the proxy buffers in section 7) or the login flow.
- **A browser sign-in.** The rehearsal's certificates were self-signed, so the
  sign-ins went through the real login form, callback and session cookie by a
  script, not a browser; the panel was checked to load, not clicked through.
- **The SSH tunnel for migrations** (section 3): the rehearsal reached the
  database through a temporary port mapping instead.
- **A migrations bundle, upgrades and rollback** (sections 3 and 9): not
  executed.
- **A sub-path address**: not supported by this release (see the top of
  this document, tech-057).

The first real deployment should walk section 8 again and fix this document
where it differs.
