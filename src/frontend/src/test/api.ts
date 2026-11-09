// The API side of the frontend test kit (tech-035, docs/conventions/testing.md § 3).
// Five helpers over MSW, for NEW tests: the URL of an endpoint, a problem body whose text comes from the
// error catalog, a handler that refuses, a recorder of what was sent, and a held answer. Existing tests
// keep their local copies until phase 2.
import { readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { HttpResponse, http, type HttpHandler } from 'msw';
import { gate } from './gate';
import { server } from './mocks/server';

export type Method = 'get' | 'post' | 'put' | 'patch' | 'delete';

/**
 * The URL of an endpoint as MSW matches it: the API's base (`test.env.VITE_API_BASE_URL`,
 * vite.config.ts) and `route`. `route` may carry MSW parameters (`/api/organizations/:id/access`).
 */
export function apiUrl(route: string): string {
  const base = import.meta.env.VITE_API_BASE_URL as string;
  return `${base.replace(/\/$/, '')}/${route.replace(/^\//, '')}`;
}

interface CatalogEntry {
  statuses: number[];
  ru: string;
}

/** Every module's error catalog (`contracts/errors/*.json`, ADR-0006), read once. */
const CATALOG: Readonly<Record<string, CatalogEntry>> = (() => {
  const dir = path.resolve(
    path.dirname(fileURLToPath(import.meta.url)),
    '../../../../contracts/errors',
  );
  const codes: Record<string, CatalogEntry> = {};
  for (const file of readdirSync(dir).filter((name) => name.endsWith('.json'))) {
    const parsed = JSON.parse(readFileSync(path.join(dir, file), 'utf8')) as {
      codes?: Record<string, CatalogEntry>;
    };
    Object.assign(codes, parsed.codes ?? {});
  }
  return codes;
})();

function entryOf(code: string): CatalogEntry {
  const entry = CATALOG[code];
  if (!entry) {
    throw new Error(
      `No code ${code} in contracts/errors/*.json: the test names a retired or misspelled code.`,
    );
  }
  return entry;
}

/** The catalog's Russian text of `code` with its `{param}`s filled in: what the screen shows. */
export function problemText(code: string, params: Record<string, string> = {}): string {
  return entryOf(code).ru.replace(
    /\{(\w+)\}/g,
    (_match, name: string) => params[name] ?? `{${name}}`,
  );
}

/** A problem body as the API answers it (api-contract § 0.5). */
export interface ProblemBody {
  status: number;
  title: string;
  detail: string;
  errorCode: string;
  params?: Record<string, { value: string; type: 'string' }>;
}

/** The answer for `code`: the catalog's first status (or `status`) and its text, never a typed string. */
export function problem(
  code: string,
  params: Record<string, string> = {},
  status?: number,
): ProblemBody {
  const entry = entryOf(code);
  const body: ProblemBody = {
    status: status ?? entry.statuses[0] ?? 500,
    title: code,
    detail: problemText(code, params),
    errorCode: code,
  };
  if (Object.keys(params).length > 0) {
    body.params = Object.fromEntries(
      Object.entries(params).map(([name, value]) => [name, { value, type: 'string' as const }]),
    );
  }
  return body;
}

/** Answers `method route` with the problem for `code` until the test ends (`once`: for the first request only). */
export function serveProblem(
  method: Method,
  route: string,
  code: string,
  params: Record<string, string> = {},
  options: { status?: number; once?: boolean } = {},
): ProblemBody {
  const body = problem(code, params, options.status);
  server.use(
    http[method](
      apiUrl(route),
      () =>
        HttpResponse.json(body, {
          status: body.status,
          headers: { 'Content-Type': 'application/problem+json' },
        }),
      { once: options.once ?? false },
    ),
  );
  return body;
}

/** A request the API received. */
export interface RecordedRequest {
  method: string;
  /** The path as sent, ids still encoded. */
  path: string;
  search: string;
  /** The parsed JSON body; `undefined` when there was none. */
  body: unknown;
}

/**
 * Serves `method route` with `answer` (default 204) and keeps what arrives. Assert on `requests`
 * for both «this was sent» (`toEqual([...])`) and «nothing was sent» (`toEqual([])`).
 */
export function recordRequests(
  method: Method,
  route: string,
  answer: (request: RecordedRequest) => Response | object | undefined = () => undefined,
): { requests: RecordedRequest[] } {
  const requests: RecordedRequest[] = [];
  const handler: HttpHandler = http[method](apiUrl(route), async ({ request }) => {
    const text = request.method === 'GET' ? '' : await request.text();
    const url = new URL(request.url);
    const recorded: RecordedRequest = {
      method: request.method,
      path: url.pathname,
      search: url.search,
      body: text ? JSON.parse(text) : undefined,
    };
    requests.push(recorded);
    const result = answer(recorded);
    if (result instanceof Response) return result;
    return result === undefined
      ? new HttpResponse(null, { status: 204 })
      : HttpResponse.json(result);
  });
  server.use(handler);
  return { requests };
}

/**
 * Keeps the answer of `method route` pending until `release()`. `reached` settles when the request has
 * arrived, so a test sees the loading state without a fixed delay (a `setTimeout` of N ms is flaky
 * and slow). Every test that holds one must release it, or the request outlives the test.
 */
export function hold(
  method: Method,
  route: string,
  body?: object,
): { reached: Promise<void>; release: () => void } {
  const reached = gate();
  const opened = gate();
  server.use(
    http[method](apiUrl(route), async () => {
      reached.open();
      await opened.opened;
      return body === undefined ? new HttpResponse(null, { status: 204 }) : HttpResponse.json(body);
    }),
  );
  return { reached: reached.opened, release: opened.open };
}
