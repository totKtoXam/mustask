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

```bash
sudo useradd --system --create-home --home-dir /var/lib/mustask --shell /usr/sbin/nologin mustask
sudo mkdir -p /opt/mustask/api /var/www/mustask /etc/mustask
```

`/var/lib/mustask` must persist across restarts and upgrades: the API encrypts
its session cookie with ASP.NET Core Data Protection keys, which by default
live in the service user's home directory. If that directory is wiped, every
open session is invalidated and users sign in again.

## 3. Database

Create an empty database and a login that owns it:

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
export ConnectionStrings__mustask='Host=<db host>;Port=5432;Database=mustask;Username=mustask;Password=<…>'
dotnet ef database update --project src/backend/Mustask.Infrastructure
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
root and readable by `mustask`).

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
    listen 443 ssl;
    http2 on;
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
4. After the first sign-in the user appears in member search (the directory
   fills at sign-in; there is no import).

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
| Keycloak shows "Invalid redirect URI" | `mustask-panel` lacks `https://mustask.example.com/api/session/callback`, or `ASPNETCORE_FORWARDEDHEADERS_ENABLED` is missing and the API sent an `http://` address. |
| Every API call is 401 after sign-in | The access token has no `aud = mustask-api`, or `Keycloak__Authority`/`Keycloak__Issuer` differ from the token's `iss`. |
| Sign-in comes back `authError=no_access` | The user does not hold the `mustask-api` client role `user` (assign it directly). |
| Signed in, but a role does not take effect | `resource_access.mustask-api.roles` is missing from the access token (mapper, section 1). |
| Everyone is signed out after a restart | `/var/lib/mustask` (Data Protection keys) is not persistent or not writable by the service. |

## What this runbook does not verify

It was written from the code and configuration of the repository (tech-024 and
tech-025) and has not been executed on a real VM against a real shared realm.
The first deployment should walk through section 8 and fix this document where
it differs.
