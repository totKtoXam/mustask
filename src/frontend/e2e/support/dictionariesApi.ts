// Specs 17-18: Node-side access to the real `/api/dictionaries/*` routes,
// used to set up and restore rows around a test instead of driving the
// panel's own UI for that — more robust, and it can play "another
// administrator" writing concurrently.
//
// The panel's business routes take a Bearer access token, not the
// `mustask.session` cookie (`shared/api/httpClient.ts`, `credentials: 'omit'`).
// This module exchanges the browser context's own session cookie for one via
// `POST /api/session/token` — the same endpoint the panel itself calls
// (`shared/api/session.ts`) — using Playwright's `APIRequestContext`
// (`page.request`), which shares the browser context's cookie jar. So a
// prior `signIn(page, ...)` is what makes every call here succeed; nothing
// here talks to Keycloak or reads a password.
//
// The token never touches a page, is never logged, and is never returned to
// a caller — it lives only in the `Authorization` header of the request each
// function below makes. A fresh token is fetched per call, not cached:
// nothing here is a hot path, and the stand's access-token lifespan is only
// 120s (`src/frontend/AGENTS.md` § E2E test suite) — caching would just
// reintroduce `shared/api/session.ts`'s own expiry bookkeeping for no benefit
// to a handful of setup/restore calls per test.
import type { APIRequestContext } from '@playwright/test';
import { env } from './env';

export interface LanguageDto {
  code: string;
  endonym: string;
  isDefault: boolean;
  isActive: boolean;
  sortOrder: number;
  version: string;
  updatedAt: string;
  updatedBy: string;
}

export interface ReplaceLanguageBody {
  endonym: string;
  sortOrder: number;
  isActive: boolean;
}

async function accessToken(request: APIRequestContext): Promise<string> {
  const response = await request.post(`${env().apiUrl}/api/session/token`);
  if (!response.ok()) {
    throw new Error(
      `POST /api/session/token failed: HTTP ${response.status()} — is the caller signed in (signIn(page, ...)) before calling into dictionariesApi.ts?`,
    );
  }
  const body = (await response.json()) as { accessToken: string };
  return body.accessToken;
}

async function authorized<T>(
  request: APIRequestContext,
  method: 'GET' | 'PUT',
  path: string,
  options: { body?: unknown; ifMatch?: string } = {},
): Promise<T> {
  const token = await accessToken(request);
  const headers: Record<string, string> = { Authorization: `Bearer ${token}` };
  if (options.ifMatch !== undefined) {
    headers['If-Match'] = options.ifMatch;
  }
  const response = await request.fetch(`${env().apiUrl}${path}`, {
    method,
    headers,
    data: options.body,
  });
  if (!response.ok()) {
    const text = await response.text();
    throw new Error(`${method} ${path} failed: HTTP ${response.status()} ${text}`);
  }
  if (response.status() === 204) {
    return undefined as T;
  }
  return (await response.json()) as T;
}

/** `GET /languages?filter=code=<code>&pageSize=1` — there is no `GET /languages/{code}`. */
export async function getLanguageByCode(request: APIRequestContext, code: string): Promise<LanguageDto | null> {
  const params = new URLSearchParams({ filter: `code=${code}`, page: '1', pageSize: '1' });
  const page = await authorized<{ data: LanguageDto[] }>(request, 'GET', `/api/dictionaries/languages?${params.toString()}`);
  return page.data[0] ?? null;
}

export function replaceLanguage(
  request: APIRequestContext,
  code: string,
  body: ReplaceLanguageBody,
  ifMatch: string,
): Promise<LanguageDto> {
  return authorized<LanguageDto>(request, 'PUT', `/api/dictionaries/languages/${encodeURIComponent(code)}`, { body, ifMatch });
}

/** `PUT /languages/default` — no `If-Match` (the API contract's stated exception). */
export function setDefaultLanguage(request: APIRequestContext, code: string): Promise<LanguageDto> {
  return authorized<LanguageDto>(request, 'PUT', '/api/dictionaries/languages/default', { body: { code } });
}
