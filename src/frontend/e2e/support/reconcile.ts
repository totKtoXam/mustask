// Repairs what an aborted or force-killed prior run left
// behind, before any spec starts — a run's own `afterEach`/`afterAll` never
// fires when the process is killed mid-spec. Best-effort: a failure here
// should not fail the whole run the way a missing/mismatched stand-only
// client does (standFile.ts) — that is a different, sharper failure
// mode this module does not duplicate.
//
// `accessTokenLifespan` needed the same treatment once (a spec that shortens
// it, killed before its own `afterEach`/`afterAll` restored it) — that
// need was removed outright rather than adding a repair: AppHost now sets
// that value itself at every stand start, including a restart of the
// `keycloak` container alone (`KeycloakStandProvisioner.SetAccessTokenLifespanAsync`,
// live-verified across a restart, `docs/templates/examples/tech-159-e2e-without-password/plan-backend.md` §11), so there is
// nothing left for this module to find "wrong" and no spec left that could
// leave it that way. Only the container's own running/stopped state still
// needs reconciling here.
//
// This must run — and be able to find the
// container's name — *before* global setup knows Keycloak is reachable. A
// stopped `keycloak` container is exactly the state that needs repairing,
// and it is exactly the state in which the realm-reachability check fails,
// so the repair reads the stand file with `peekStandFile()` (a plain,
// non-polling read — no secret verification, no requirement that Keycloak
// already answer) rather than `loadStandFile()`.
import { peekStandFile } from './standFile';
import { inspectContainerState, startContainer } from './docker';

export async function reconcileStand(): Promise<void> {
  await reconcileKeycloakContainer();
}

async function reconcileKeycloakContainer(): Promise<void> {
  const stand = await peekStandFile();
  if (!stand) {
    // No stand file at all — either a fresh stand still provisioning it, or
    // no stand running. Nothing this module can repair; the reachability
    // checks that follow name the cause.
    return;
  }
  try {
    const state = await inspectContainerState(stand.keycloakContainerName);
    if (state !== 'running') {
      await startContainer(stand.keycloakContainerName);
      console.log(
        `e2e global setup: container '${stand.keycloakContainerName}' was '${state}', started it (an earlier run ` +
          'likely aborted mid-outage, e.g. specs 5/6).',
      );
    }
  } catch (error) {
    console.warn(`e2e global setup: could not reconcile the keycloak container's state (${(error as Error).message}).`);
  }
}
