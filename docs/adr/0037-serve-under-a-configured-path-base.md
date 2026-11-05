# ADR-0037: Serve the panel and the API under one configured path base

- Status: accepted (the human, gate-1 of tech-057, 2026-11-05)
- Date: 2026-11-05
- Context: `tech-057-panel-api-sub-path` ([status](../features/tech-057-panel-api-sub-path/status.md)). The target address is `https://portal.devexpert.kz/mustask` (the human, 2026-11-05, chose a sub-path over a subdomain). Builds on ADR-0022 (the token-mediating backend: its cookie and callback paths) and ADR-0033 (the shared realm: only the two clients' addresses change). Accepted only by the human (`docs/adr/README.md`, rule 2).

## Problem

Today MUSTASK works only at the root of its host name. Five things assume `/`:

1. The panel is built with Vite's default `base` (`/`), so `index.html` asks for `/assets/...`, and the router has no `basename`.
2. The panel takes `window.location.pathname` as the path it came from (`returnUrl`), checks `/signed-out` literally, and builds the copied link of a work item as `origin + path`.
3. The API's session cookie has the literal path `/api/session`. Under `/mustask/api/session` the browser would never send it.
4. The OIDC handler builds its `redirect_uri` from `Request.PathBase` + `/api/session/callback`. Behind a proxy that strips the prefix, `PathBase` is empty and the address points at the portal's root.
5. The deploy kit (tech-058) takes a bare host: the release build, the env template, the nginx site (a whole `server {}`) and the smoke check.

On `portal.devexpert.kz` the `server {}` already belongs to the portal, so MUSTASK has to fit into it as locations under `/mustask/`.

## Options considered

### Option 1: nginx strips the prefix; the API learns it from `X-Forwarded-Prefix`

- Pros: the API process needs no setting of its own; the standard forwarded-headers middleware supports the header.
- Cons: the API trusts a request header for its own addresses, so it needs known proxies. When the header is missing (a proxy edited by someone else, a test call), the prefix is lost without a sound. The sign-in then breaks only on the server, with `redirect_uri` mismatch.

### Option 2: One configured path base; nginx passes the path unchanged

- Pros: one value, in configuration, read at start and checked against `Session:PanelUrl`. The API calls `UsePathBase` with it, so `Request.PathBase` is right for every request that carries the prefix. The OIDC handler's `redirect_uri` and its correlation cookies then follow the prefix with no code of their own. A request without the prefix (`/health` from the VM itself) still works. Nothing depends on a header.
- Cons: the same value has to reach two places, the API's configuration and the panel's build. The kit derives both from the one site URL it already takes.

### Option 3: Own host name (subdomain)

- Pros: no code changes.
- Cons: the human rejected it for the target address (2026-11-05).

## Decision

Option 2.

1. **One value: the path base.** It is the path part of the public address, with a leading `/` and no trailing `/`, for example `/mustask`. **Empty means the root**, which is today's behaviour. The local stand (Aspire) and CI leave it empty.
2. **API.** The configuration key is `Hosting:PathBase` (env `Hosting__PathBase`). When it is not empty, `app.UsePathBase(...)` is the first middleware. The session cookie path is `{PathBase}/api/session`. At start the API refuses to run when the value is malformed, or when `Session:PanelUrl` does not end with it. Every redirect the session module builds is already `{PanelUrl}{returnUrl}`. Sign-out goes to `{PanelUrl}/signed-out`. Both stay as they are, because `PanelUrl` carries the prefix.
3. **Panel.** The build takes `VITE_BASE_PATH` (default `/`) as Vite's `base`. The router's `basename` comes from `import.meta.env.BASE_URL`. `returnUrl` and the `/signed-out` check are paths *inside* the panel, with the base removed. A copied absolute link adds the base. `VITE_API_BASE_URL` is the full public address with the prefix.
4. **nginx.** The proxy passes the path unchanged (`proxy_pass http://127.0.0.1:5000;`, no URI part). The kit ships two layouts:
   - a whole `server {}`, when MUSTASK owns its host name;
   - location blocks, when it lives under a prefix in someone else's server. These are `location /mustask/api/` and `location /mustask/`, which serves the panel through an `alias` and falls back to `index.html`. The owner of that server includes them.
5. **Keycloak (ADR-0033 unchanged).** The `mustask-panel` client's addresses carry the prefix:
   - root URL `https://<host>/mustask`;
   - redirect URI `https://<host>/mustask/api/session/callback`;
   - post-logout redirect URI `https://<host>/mustask/*`.

   The web origin stays the bare `https://<host>`.

## Consequences

- One deployment serves one prefix. Moving it is a rebuild of the panel and an edit of the env file, not a code change.
- The prefix is checked at the API's start, against `PanelUrl`. A mismatch fails the deploy's health wait, not the first sign-in.
- The panel reads `BASE_URL` in the router, the bootstrap check and the copied link. New code that builds an absolute address of the panel has to do the same.
- The location layout depends on the owner of the portal's server agreeing to the include. That is an organizational step of the deployment, which the runbook names.
