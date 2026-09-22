import { readdirSync, readFileSync, statSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';

/**
 * A structural guard against a future author
 * "fixing" a flaky sign-in by fabricating `mustask.session` or stubbing the
 * session endpoints, leaving a green suite that proves nothing about
 * the real login flow. A plain Vitest test, not a Playwright spec — it
 * never runs the suite, only reads its source — so it lands inside
 * `npm test`/`scripts/verify.sh`, the same pass that already lints/type-checks
 * `e2e/**` (Playwright's own suite stays excluded from both, unchanged).
 *
 * Guard boundary, stated up front (read this before treating the guard as
 * more than it is): a text scan over source cannot stop a determined rewrite — string
 * concatenation, a cookie name assembled from a template literal, a
 * dynamically-built RegExp handed to `page.route`, or moving the pattern
 * into a helper this scan does not cover all defeat it with no real effort.
 * This is not a security control against an adversarial contributor —
 * anyone editing `e2e/**` could equally delete this test. Its job is
 * catching the *innocent* fix: a future author who reaches for
 * `context.addCookies([{ name: 'mustask.session', ... }])` or
 * `page.route('**\/api/session/callback', ...)` as the obvious-looking way
 * to make a red spec green, and a reviewer skimming the diff who might not
 * notice it.
 */

const HERE = path.dirname(fileURLToPath(import.meta.url));
const E2E_ROOT = path.resolve(HERE, '..', '..', 'e2e');

// Assignment-shaped, not the read-shaped `cookie.name === 'mustask.session'`
// comparison that cookies.ts legitimately uses to observe an already-set
// cookie.
const SETS_MUSTASK_SESSION_DIRECTLY = /name:\s*['"]mustask\.session['"]/;

// Playwright's own mocking primitive, aimed at the `/api/session/` path
// prefix as a whole — not the three literal endpoint names under it. A live
// reproduction found the hole in the earlier, narrower version of this pattern
// (`\/api\/session\/(login|callback|token)`): `page.route('**/api/session/*',
// ...)` stubs all three endpoints at once via Playwright's own glob `*`, but
// matched none of the three literal words immediately after the prefix, so
// it sailed through green — and it is the *natural* way to write "stub the
// session calls", not a contrived evasion. The criterion is "touches this
// path prefix at all", so the check now follows `/api/session` with either
// `/` (anything under it — `callback`, `token`, `*`, `**`, ...) or an
// immediate closing quote (the bare prefix, no segment at all), rather than
// enumerating names it has to keep in sync with the real endpoint list.
const STUBS_SESSION_ENDPOINT = /\.route\(\s*[`'"][^`'"]*\/api\/session(\/|[`'"])/;

function listTsFiles(dir: string): string[] {
  const entries = readdirSync(dir);
  const files: string[] = [];
  for (const entry of entries) {
    const full = path.join(dir, entry);
    if (statSync(full).isDirectory()) {
      files.push(...listTsFiles(full));
    } else if (entry.endsWith('.ts')) {
      files.push(full);
    }
  }
  return files;
}

describe('e2e/** never fabricates mustask.session or stubs the session endpoints', () => {
  const files = listTsFiles(E2E_ROOT);

  it('found e2e/**/*.ts files to scan (guard against a vacuous pass)', () => {
    expect(files.length).toBeGreaterThan(0);
  });

  it.each(files.map((file) => [path.relative(E2E_ROOT, file), file] as const))(
    '%s does not set mustask.session directly or stub a session endpoint',
    (_relative, file) => {
      const source = readFileSync(file, 'utf8');
      expect(source).not.toMatch(SETS_MUSTASK_SESSION_DIRECTLY);
      expect(source).not.toMatch(STUBS_SESSION_ENDPOINT);
    },
  );
});
