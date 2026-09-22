// Environment variables the E2E suite reads — documented in
// src/frontend/AGENTS.md § E2E test suite. Only URLs have defaults here;
// there is no password to default (E2E_PASSWORD is gone, the suite signs
// in through the stand-only client's impersonation right instead,
// e2e/support/impersonation.ts).
//
// Nothing in this module is called at import time — Playwright imports every
// spec file to build `--list`, and a not-yet-provisioned stand must not
// break listing, only running.

export interface E2EEnv {
  panelUrl: string;
  apiUrl: string;
  keycloakUrl: string;
  /** The only realm the suite touches. */
  realm: string;
  /** The E2E fixture user (ADR-0023) — no password credential exists for it at all. */
  user: string;
  allowDocker: boolean;
}

function trimTrailingSlash(url: string): string {
  return url.replace(/\/$/, '');
}

export function env(): E2EEnv {
  return {
    panelUrl: trimTrailingSlash(process.env.E2E_PANEL_URL ?? 'http://localhost:3000'),
    apiUrl: trimTrailingSlash(process.env.E2E_API_URL ?? 'http://localhost:5000'),
    keycloakUrl: trimTrailingSlash(process.env.E2E_KEYCLOAK_URL ?? 'http://localhost:5010'),
    realm: 'mustask',
    user: process.env.E2E_USER ?? 'e2e-fixture',
    allowDocker: process.env.E2E_ALLOW_DOCKER === '1',
  };
}

/**
 * For HTTP calls the test runner makes itself, from Node — the global setup,
 * the Keycloak admin API, the readiness poll — never for URLs the browser
 * opens. Measured on the stand machine (2026-09-11): on Windows,
 * Node's `dns.lookup('localhost', { all: true })` returns only `::1`, while
 * Mustask.Api and Keycloak under Aspire/Docker listen on IPv4 only, so a Node
 * `fetch` to `localhost` is refused although curl and the browser fall back
 * to 127.0.0.1. `dns.setDefaultResultOrder('ipv4first')` does not help: the
 * lookup has no IPv4 address to reorder. The browser keeps `localhost` — the
 * session cookie and Keycloak's redirect URIs live on that host.
 */
export function nodeUrl(url: string): string {
  const parsed = new URL(url);
  if (parsed.hostname === 'localhost') {
    parsed.hostname = '127.0.0.1';
  }
  return parsed.toString().replace(/\/$/, '');
}

