// Controls the stand's `keycloak` container — specs 5/6,
// gated by E2E_ALLOW_DOCKER=1.
//
// Resolves the container by the *exact* name AppHost
// assigned it (`docs/templates/examples/tech-159-e2e-without-password/plan-backend.md` §3a: `mustask-e2e-keycloak-{port}`, written to
// the stand file), not `docker ps --filter name=keycloak`'s first match —
// that filter also matched a stopped container left by an earlier or
// sibling worktree's stand, in no defined order. An exact name removes the
// ambiguity structurally: `docker inspect` on it either finds exactly one
// container or none, never "the first one found."
//
// First real run (2026-09-11): `docker start` returns as soon as
// the container process launches, long before Keycloak itself is serving
// requests, so `startContainer` polls the realm endpoint itself rather than
// trusting the command's return.
//
// Second real run (2026-09-11): `stopContainer` used to wait for
// the panel's own readiness monitor (`/health`) to flip
// to not-ready. It never does — `RealmReadinessMonitor`
// (src/backend/Mustask.Api/Authentication/Readiness/RealmReadinessMonitor.cs,
// around line 129) latches readiness: once Ready, realm unavailability alone
// does not reset it (only a foreign issuer does), because bearer-token
// validation keeps using the cached JWKS while Keycloak is down. Waiting on
// `/health` to go red after a stop can therefore never succeed. Specs 5 and
// 6 need Keycloak actually unreachable, not `/health` red, so
// `stopContainer` confirms the outage by polling the realm endpoint until
// the request itself fails.
//
// First live run, two compounding findings from running specs 5 and
// 6 back to back (never reproduced running either alone against a stand
// that had been up for a while):
//
// 1. The public realm endpoint (`GET /realms/{realm}`) answers a little
//    before `mustask-e2e`'s own client-credentials grant does — the same
//    "endpoint up is not the same as fully ready" shape backend QA already
//    found once at AppHost's own start (`plan-backend.md` §10), here
//    at the narrower scope of one container's own restart mid-run.
// 2. Aspire's DCP notices this suite's own direct `docker stop`/`docker
//    start` (outside DCP's own start/stop path) and re-fires
//    `ResourceReadyEvent` once the container is healthy again — so AppHost
//    re-provisions `mustask-e2e` with a **brand-new secret**, confirmed live by
//    reading the client's secret from the live realm (master-realm admin
//    token) and finding it had changed from what the stand file held before
//    the restart. `waitForKeycloak` must therefore not just wait for the
//    client to become usable, it must refresh the cached stand file
//    (`standFile.ts`) once it is — otherwise every later `signIn()`/admin
//    call in the run keeps using the pre-restart secret and fails with
//    `unauthorized_client`.
import { execFile } from 'node:child_process';
import { promisify } from 'node:util';
import { env, nodeUrl } from './env';
import { loadStandFile, refreshStandFile } from './standFile';

const run = promisify(execFile);

const KEYCLOAK_BOOT_TIMEOUT_MS = 120_000;
const KEYCLOAK_DOWN_TIMEOUT_MS = 30_000;
const KEYCLOAK_POLL_INTERVAL_MS = 2_000;
const KEYCLOAK_DOWN_POLL_INTERVAL_MS = 1_000;

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function realmUrl(): string {
  const { keycloakUrl, realm } = env();
  return `${nodeUrl(keycloakUrl)}/realms/${realm}`;
}

/**
 * Polls the realm endpoint itself — `docker start` returns long before
 * Keycloak is serving — then re-reads and re-verifies the stand file
 * (`refreshStandFile`, not the cached `loadStandFile`) until it succeeds,
 * which both waits out the stand-only client's own readiness lag and picks
 * up a secret AppHost may have just rotated (see this file's header
 * comment) — replacing the stale cached one for the rest of the run.
 */
async function waitForKeycloak(timeoutMs = KEYCLOAK_BOOT_TIMEOUT_MS): Promise<void> {
  const url = realmUrl();
  const start = Date.now();
  for (;;) {
    try {
      const response = await fetch(url);
      if (response.ok) {
        break;
      }
    } catch {
      // Not listening yet.
    }
    if (Date.now() - start > timeoutMs) {
      throw new Error(`Keycloak at ${url} did not start serving within ${timeoutMs}ms of 'docker start'.`);
    }
    await sleep(KEYCLOAK_POLL_INTERVAL_MS);
  }
  let lastError: Error | undefined;
  for (;;) {
    try {
      await refreshStandFile();
      return;
    } catch (error) {
      lastError = error as Error;
    }
    if (Date.now() - start > timeoutMs) {
      throw new Error(
        `The stand-only client 'mustask-e2e' was still not usable ${timeoutMs}ms after 'docker start': ${lastError?.message}`,
      );
    }
    await sleep(KEYCLOAK_POLL_INTERVAL_MS);
  }
}

/** Polls the realm endpoint until the request itself fails — `docker stop` returns before that. */
async function waitForKeycloakUnreachable(timeoutMs = KEYCLOAK_DOWN_TIMEOUT_MS): Promise<void> {
  const url = realmUrl();
  const start = Date.now();
  for (;;) {
    try {
      await fetch(url);
    } catch {
      return;
    }
    if (Date.now() - start > timeoutMs) {
      throw new Error(`Keycloak at ${url} was still reachable ${timeoutMs}ms after 'docker stop'.`);
    }
    await sleep(KEYCLOAK_DOWN_POLL_INTERVAL_MS);
  }
}

/** The exact name AppHost assigned this stand's `keycloak` container (`plan-backend.md` §3a), verified to exist. */
export async function resolveKeycloakContainer(): Promise<string> {
  const stand = await loadStandFile();
  try {
    await run('docker', ['inspect', '--format', '{{.Name}}', stand.keycloakContainerName]);
  } catch {
    throw new Error(
      `Docker container '${stand.keycloakContainerName}' (named in the stand file) was not found. Is the stand ` +
        'up (dotnet run --project src/backend/Mustask.AppHost)?',
    );
  }
  return stand.keycloakContainerName;
}

/** `running`/`exited`/... (`docker inspect --format {{.State.Status}}`) — used by global setup's reconciliation. */
export async function inspectContainerState(name: string): Promise<string> {
  const { stdout } = await run('docker', ['inspect', '--format', '{{.State.Status}}', name]);
  return stdout.trim();
}

/** Stops the container, then confirms Keycloak is really unreachable (`/health` never goes red — see above). */
export async function stopContainer(name: string): Promise<void> {
  await run('docker', ['stop', name]);
  await waitForKeycloakUnreachable();
}

/** Starts the container and waits for Keycloak itself to serve the realm again. */
export async function startContainer(name: string): Promise<void> {
  await run('docker', ['start', name]);
  await waitForKeycloak();
}
