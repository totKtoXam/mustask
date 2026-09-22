// Reads the stand-only Keycloak
// client's credentials from the one gitignored file AppHost writes at stand
// start — `.e2e-stand/keycloak-e2e-client.json`. This is the literal
// contract `plan-backend.md` fixes and this module repeats verbatim, not a
// second, independently-guessed path. (`plan-backend.md`/`qa.md` below are
// the design notes in `docs/templates/examples/tech-159-e2e-without-password/`.)
//
// Anchor rule (plan-backend.md §6, "Suite side"): resolved from this
// module's own file location, never `process.cwd()` — `dotnet run
// --project src/backend/Mustask.AppHost` and `npm run e2e` (from src/frontend)
// start with different working directories, and a cwd-relative path would
// silently resolve to two different places.
import { readFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { env, nodeUrl } from './env';

export interface StandFile {
  issuer: string;
  clientId: string;
  clientSecret: string;
  keycloakContainerName: string;
}

function standFilePath(): string {
  const here = path.dirname(fileURLToPath(import.meta.url));
  // e2e/support/standFile.ts -> four levels up reaches the repo root (one
  // deeper than AppHost's three, since this file sits one directory deeper
  // under src/ — plan-backend.md §6's own count).
  return path.resolve(here, '..', '..', '..', '..', '.e2e-stand', 'keycloak-e2e-client.json');
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}

function parseStandFile(raw: string, filePath: string): StandFile {
  let parsed: unknown;
  try {
    parsed = JSON.parse(raw);
  } catch (error) {
    throw new Error(`The e2e stand file at ${filePath} is not valid JSON: ${(error as Error).message}`);
  }
  if (
    !isRecord(parsed) ||
    typeof parsed.issuer !== 'string' ||
    typeof parsed.clientId !== 'string' ||
    typeof parsed.clientSecret !== 'string' ||
    typeof parsed.keycloakContainerName !== 'string'
  ) {
    throw new Error(
      `The e2e stand file at ${filePath} is missing one of issuer/clientId/clientSecret/keycloakContainerName.`,
    );
  }
  return {
    issuer: parsed.issuer,
    clientId: parsed.clientId,
    clientSecret: parsed.clientSecret,
    keycloakContainerName: parsed.keycloakContainerName,
  };
}

const STAND_FILE_POLL_TIMEOUT_MS = 120_000;
const STAND_FILE_POLL_INTERVAL_MS = 2_000;

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

/**
 * Polls for the file rather than reading it once (`plan-backend.md`
 * §10/`qa.md`): AppHost deletes it as the very first
 * statement at stand start and rewrites it only once Keycloak is healthy and
 * the client/fixture user are provisioned — QA measured up to ~90s between
 * Aspire's own "Distributed application started" banner and the fresh file
 * appearing, on a cold or force-killed restart. A single read here would
 * misreport "client missing" while the stand is merely still starting.
 */
async function waitForStandFile(): Promise<StandFile> {
  const filePath = standFilePath();
  const deadline = Date.now() + STAND_FILE_POLL_TIMEOUT_MS;
  for (;;) {
    try {
      const raw = await readFile(filePath, 'utf8');
      return parseStandFile(raw, filePath);
    } catch (error) {
      const code = (error as NodeJS.ErrnoException).code;
      if (code !== 'ENOENT') {
        // Not a "still starting" case — the file exists but is broken, or
        // some other I/O error. AppHost writes it in one step after
        // provisioning succeeds, never partially, so this is a real defect,
        // not a timing artifact worth retrying.
        throw error;
      }
      if (Date.now() > deadline) {
        throw new Error(
          `No e2e stand file at ${filePath} after waiting ${STAND_FILE_POLL_TIMEOUT_MS / 1000}s. Is the stand up ` +
            "(dotnet run --project src/backend/Mustask.AppHost), and did it finish provisioning the stand-only client " +
            "('mustask-e2e')? See docs/templates/examples/tech-159-e2e-without-password/plan-backend.md §6.",
        );
      }
      await sleep(STAND_FILE_POLL_INTERVAL_MS);
    }
  }
}

async function verifyClientSecret(keycloakUrl: string, realm: string, stand: StandFile): Promise<void> {
  const response = await fetch(`${nodeUrl(keycloakUrl)}/realms/${realm}/protocol/openid-connect/token`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({
      grant_type: 'client_credentials',
      client_id: stand.clientId,
      client_secret: stand.clientSecret,
    }),
  });
  if (!response.ok) {
    throw new Error(
      `The stand-only client '${stand.clientId}' rejected the secret in the stand file: HTTP ${response.status}. ` +
        'The stand file may belong to a stand that no longer exists — restart the stand this suite points at.',
    );
  }
}

/** Fails naming the cause — a missing client (timeout above), a file from another stand (issuer), or a secret Keycloak rejects. */
async function loadVerifiedStandFile(): Promise<StandFile> {
  const stand = await waitForStandFile();
  const { keycloakUrl, realm } = env();
  // Compared against the raw E2E_KEYCLOAK_URL, not nodeUrl()'s 127.0.0.1
  // substitution: AppHost writes the issuer as `http://localhost:{port}/...`
  // literally — it has no reason to know about this
  // suite's own Windows/Node localhost workaround (env.ts's own comment).
  // Comparing against the substituted form would call every genuinely
  // matching stand a "different stand".
  const expectedIssuer = `${keycloakUrl}/realms/${realm}`;
  if (stand.issuer !== expectedIssuer) {
    throw new Error(
      `The e2e stand file belongs to another stand (expected issuer '${expectedIssuer}', found '${stand.issuer}'). ` +
        'Restart the stand this suite points at, or point E2E_KEYCLOAK_URL at the right one.',
    );
  }
  await verifyClientSecret(keycloakUrl, realm, stand);
  return stand;
}

/**
 * Single, non-polling read of the stand file — no wait for it to appear, no
 * secret verification against Keycloak. The container repair (`reconcile.ts`) needs only
 * `keycloakContainerName` and has to run *before* Keycloak is known
 * reachable — a stopped `keycloak` container is exactly the case
 * `checkReachable` fails on, so the repair must not wait behind that check,
 * and it cannot go through `loadStandFile()`/`loadVerifiedStandFile()`
 * either, since verifying the secret means a `client_credentials` grant
 * against Keycloak, which needs Keycloak already up. Returns `undefined`
 * when the file does not exist (nothing to repair yet, or no stand at all —
 * global setup's own reachability check names that cause next); a file that
 * exists but fails to parse is a real defect and still throws.
 */
export async function peekStandFile(): Promise<StandFile | undefined> {
  const filePath = standFilePath();
  try {
    const raw = await readFile(filePath, 'utf8');
    return parseStandFile(raw, filePath);
  } catch (error) {
    if ((error as NodeJS.ErrnoException).code === 'ENOENT') {
      return undefined;
    }
    throw error;
  }
}

let cached: StandFile | undefined;

/**
 * Cached per worker process (workers: 1 — effectively once per run): reads
 * and validates the file once, reused by every `signIn()`/admin call.
 */
export async function loadStandFile(): Promise<StandFile> {
  if (!cached) {
    cached = await loadVerifiedStandFile();
  }
  return cached;
}

/**
 * Forces a fresh read, replacing the cache — a live
 * finding: Aspire's DCP notices a `keycloak` container that specs 5/6
 * stop/start directly (outside its own start/stop path) coming back
 * healthy, and re-fires `ResourceReadyEvent` — so AppHost re-provisions
 * `mustask-e2e` with a **brand-new secret** mid-run, exactly when the
 * suite restarts that container. `loadStandFile()`'s cache would otherwise
 * keep every later `signIn()`/admin call on the pre-restart secret for the
 * rest of the run, failing with `unauthorized_client` — reproduced live by
 * running specs 5 and 6 back to back. `docker.ts`'s `startContainer` calls
 * this in a retry loop after every restart it performs, so the cache is
 * refreshed exactly when it can go stale, not on every unrelated call.
 */
export async function refreshStandFile(): Promise<StandFile> {
  cached = await loadVerifiedStandFile();
  return cached;
}

const STAND_FILE_INSTABILITY_WINDOW_MS = 30_000;
const STAND_FILE_INSTABILITY_RETRY_INTERVAL_MS = 2_000;

/**
 * Thrown by a `withStandFile` action **only** when Keycloak rejects the
 * stand file's own `clientId`/`clientSecret` at the token endpoint (a
 * `401`/`unauthorized_client`) — the one signal `withStandFile` below
 * retries on. Never thrown for "the fixture user wasn't found", a 500, a
 * network error, or anything else: those are real failures this module has
 * no business masking.
 */
export class StaleStandCredentialError extends Error {}

/**
 * Runs `action` against the cached stand file; retries in a bounded loop,
 * not just once, **only when `action` throws `StaleStandCredentialError`** —
 * Live findings, two separate runs: `docker.ts`'s own post-restart
 * wait already refreshes the cache once a restart it performs finishes, but
 * AppHost's `ResourceReadyEvent` could keep re-firing — and therefore keep
 * rotating `mustask-e2e`'s secret — for a few more seconds after that, a
 * narrower, *ongoing* instability window the boot-time wait did not cover.
 * The first live run caught this on `08-cookie-size.spec.ts` (right after
 * specs 5/6); a single extra retry fixed that case but the second live run
 * then caught the same class on `06-renewal-keycloak-down.spec.ts` instead —
 * a different spec, same shape — showing a single retry only narrowed the
 * window, it did not close it.
 *
 * QA (`qa.md` § Frontend): the original version retried on
 * *any* error from `action`, silently, for up to 30s — the backend has
 * since fixed the rotation at its source (`KeycloakStandClientSecret`, a
 * `Lazy<string>` stable for the process's lifetime, `plan-backend.md` §12),
 * so a blind catch-all here was both broader than any remaining
 * justification and undiagnosable: a genuine bug (a fixture user that no
 * longer exists, a 500, a real programming error) would get the identical
 * silent 30-second treatment before finally surfacing, reading in the
 * report exactly like a slow success. Narrowed to the one signal that ever
 * justified it (`StaleStandCredentialError`, thrown only on a 401 from the
 * token endpoint — `impersonation.ts`/`keycloakAdmin.ts`); every other
 * error propagates immediately, no delay, no retry. Each retry attempt is
 * also logged, so even this narrow, now-rare case is never a silent stall —
 * belt and suspenders, not an either/or. Kept at 30s rather than removed
 * outright: `plan-backend.md` §12 itself does not claim the rotation can
 * never repeat again, only that the specific bug it fixed is closed: a
 * bounded, logged, precisely-scoped safety net costs nothing when it never
 * fires and explains itself on the rare run where it does.
 */
export async function withStandFile<T>(action: (stand: StandFile) => Promise<T>): Promise<T> {
  const stand = await loadStandFile();
  try {
    return await action(stand);
  } catch (firstError) {
    if (!(firstError instanceof StaleStandCredentialError)) {
      throw firstError;
    }
    const deadline = Date.now() + STAND_FILE_INSTABILITY_WINDOW_MS;
    let lastError: Error = firstError;
    let attempt = 0;
    for (;;) {
      if (Date.now() > deadline) {
        throw lastError;
      }
      attempt += 1;
      console.warn(
        `e2e: stand-only client credential rejected (attempt ${attempt}), retrying with a freshly-read stand ` +
          `file — ${lastError.message}`,
      );
      await sleep(STAND_FILE_INSTABILITY_RETRY_INTERVAL_MS);
      let fresh: StandFile;
      try {
        fresh = await refreshStandFile();
      } catch (error) {
        lastError = error as Error;
        continue;
      }
      try {
        return await action(fresh);
      } catch (error) {
        if (!(error instanceof StaleStandCredentialError)) {
          throw error;
        }
        lastError = error;
      }
    }
  }
}
