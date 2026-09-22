// ADR-0023: obtains a real Keycloak SSO session for the E2E
// fixture user through the stand-only client's impersonation right —
// replaces typing E2E_PASSWORD into Keycloak's own form. Everything
// here runs on the Node side only: every call to Keycloak
// is made from this module, never something a browser context executes — so
// the client's secret can never appear in a page, a request the browser
// made, or a trace of one.
//
// Only the credential-typing step is replaced: after this
// module hands the cookie to the browser context, the panel still walks its
// own real OIDC round trip (redirect to Keycloak's authorization endpoint,
// callback, `Set-Cookie: mustask.session`) — see auth.ts.
//
// **Every call here preserves the `Host` header the browser will actually
// use.** Live-found while wiring this up: Keycloak binds a token's/cookie's
// `iss` claim to the request's own `Host` header at issuance, and
// re-validates it against the *current* request's `Host` on every later
// use — a mismatch is not a loud error, it is silently treated as "no
// session" (for the impersonation cookie) or "unauthorized" (for a bearer
// token whose issuer doesn't match the host presenting it). `nodeUrl()`
// (env.ts) rewrites `localhost` to `127.0.0.1` so Node's own `fetch` can
// connect at all (a Windows-only DNS quirk, see its own comment) — but
// plain `fetch` silently overwrites any caller-supplied `Host` header to
// match the connection's real authority. `node:http`'s `Host` header is not
// similarly protected — confirmed live (`curl --resolve
// localhost:5010:127.0.0.1` reproduces the same fact from the shell) — so
// every request below connects to the `nodeUrl()` address but sends `Host:
// <the browser-facing host>`, keeping the token's and the cookie's issuer
// identical to what the browser will present them against.
import http from 'node:http';
import { env, nodeUrl } from './env';
import { StaleStandCredentialError, withStandFile } from './standFile';

export interface InjectableCookie {
  name: string;
  value: string;
  domain: string;
  path: string;
  secure: boolean;
  httpOnly: boolean;
  sameSite: 'Strict' | 'Lax' | 'None';
}

interface RawResponse {
  status: number;
  headers: http.IncomingHttpHeaders;
  body: string;
}

function keycloakRequest(
  keycloakUrl: string,
  path: string,
  init: { method: 'GET' | 'POST'; headers?: Record<string, string>; body?: string },
): Promise<RawResponse> {
  const browserFacing = new URL(keycloakUrl);
  const connectTo = new URL(nodeUrl(keycloakUrl));
  return new Promise((resolve, reject) => {
    const req = http.request(
      {
        hostname: connectTo.hostname,
        port: connectTo.port,
        path,
        method: init.method,
        headers: { ...init.headers, Host: browserFacing.host },
      },
      (res) => {
        const chunks: Buffer[] = [];
        res.on('data', (chunk: Buffer) => chunks.push(chunk));
        res.on('end', () => {
          resolve({ status: res.statusCode ?? 0, headers: res.headers, body: Buffer.concat(chunks).toString('utf8') });
        });
      },
    );
    req.on('error', reject);
    if (init.body) {
      req.write(init.body);
    }
    req.end();
  });
}

async function getServiceAccountToken(keycloakUrl: string, realm: string, clientId: string, clientSecret: string): Promise<string> {
  const response = await keycloakRequest(keycloakUrl, `/realms/${realm}/protocol/openid-connect/token`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({
      grant_type: 'client_credentials',
      client_id: clientId,
      client_secret: clientSecret,
    }).toString(),
  });
  // 401 here is Keycloak's `unauthorized_client` shape — a stale secret,
  // the one signal `withStandFile` (standFile.ts) retries on. Any other
  // status is a different, real failure and must not be retried silently.
  if (response.status === 401) {
    throw new StaleStandCredentialError(`'${clientId}' rejected as unauthorized_client: HTTP 401 ${response.body}`);
  }
  if (response.status < 200 || response.status >= 300) {
    throw new Error(`'${clientId}' could not obtain a service-account token: HTTP ${response.status} ${response.body}`);
  }
  return (JSON.parse(response.body) as { access_token: string }).access_token;
}

/** Not fixture-specific despite the historical name of its one caller — looks up any user by exact username. */
async function findUserId(keycloakUrl: string, realm: string, token: string, username: string): Promise<string> {
  const response = await keycloakRequest(
    keycloakUrl,
    `/admin/realms/${realm}/users?username=${encodeURIComponent(username)}&exact=true`,
    { method: 'GET', headers: { Authorization: `Bearer ${token}` } },
  );
  if (response.status < 200 || response.status >= 300) {
    throw new Error(`Failed to look up user '${username}': HTTP ${response.status}`);
  }
  const users = JSON.parse(response.body) as Array<{ id: string }>;
  const user = users[0];
  if (!user) {
    throw new Error(`User '${username}' not found in realm '${realm}'.`);
  }
  return user.id;
}

/** Only `name=value` — Keycloak's own attributes (Path/Secure/SameSite) are reconstructed deliberately below, not trusted verbatim from the wire. */
function parseSetCookieNamesAndValues(headers: string[]): Array<{ name: string; value: string }> {
  return headers.map((raw) => {
    const [pair] = raw.split(';');
    const separatorIndex = pair.indexOf('=');
    return { name: pair.slice(0, separatorIndex).trim(), value: pair.slice(separatorIndex + 1).trim() };
  });
}

/**
 * The cookies to hand to `page.context().addCookies(...)` — scoped to
 * Keycloak's own hostname and its realm path only, never a broader domain
 * (the code's own intent; Chromium's cookie jar is not port-scoped on a
 * shared `localhost` host — a pre-existing platform fact, confirmed by a
 * `:5099` probe, and not something this module can fix). Attributes
 * (`Secure`, `SameSite=None`, `Path=/realms/{realm}/`) are set to match what
 * Keycloak itself sends for these two cookies (confirmed live) rather than
 * trusted from the wire — `Secure` cookies work over this stand's plain
 * `http://localhost` because Chromium treats `localhost` as a potentially
 * trustworthy origin (the same fact `mustask.session` already relies on).
 *
 * `username` defaults to the fixture user (`E2E_USER`, every existing spec's
 * only caller) — the permission-gating spec (16) is the one caller that
 * passes a different, ephemeral username (`keycloakAdmin.ts`'s
 * `createEphemeralReaderUser`), to obtain a real Keycloak session for an
 * identity with fewer permissions than the fixture's own admin group, without
 * touching the fixture's own group membership (a shared identity other
 * specs/suites depend on).
 */
export async function obtainKeycloakSsoCookies(username?: string): Promise<InjectableCookie[]> {
  const { keycloakUrl, realm, user } = env();
  const targetUser = username ?? user;
  return withStandFile(async (stand) => {
    const token = await getServiceAccountToken(keycloakUrl, realm, stand.clientId, stand.clientSecret);
    const userId = await findUserId(keycloakUrl, realm, token, targetUser);

    const response = await keycloakRequest(keycloakUrl, `/admin/realms/${realm}/users/${userId}/impersonation`, {
      method: 'POST',
      headers: { Authorization: `Bearer ${token}` },
    });
    if (response.status < 200 || response.status >= 300) {
      throw new Error(`Impersonation of '${targetUser}' failed: HTTP ${response.status} ${response.body}`);
    }
    const setCookie = response.headers['set-cookie'] ?? [];
    if (setCookie.length === 0) {
      throw new Error('Keycloak impersonation succeeded but returned no Set-Cookie header — cannot inject an SSO session.');
    }

    const keycloakHost = new URL(keycloakUrl).hostname;
    return parseSetCookieNamesAndValues(setCookie)
      .filter((cookie) => cookie.name.startsWith('KEYCLOAK_'))
      .map((cookie) => ({
        name: cookie.name,
        value: cookie.value,
        domain: keycloakHost,
        path: `/realms/${realm}/`,
        secure: true,
        httpOnly: true,
        sameSite: 'None' as const,
      }));
  });
}
