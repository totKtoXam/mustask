// Fails fast, with one readable message, instead of twenty red tests when
// the stand (or the stand-only client, ADR-0023) isn't ready — see
// src/frontend/AGENTS.md § E2E test suite. Not run by `playwright test
// --list`, only by an actual run, so it never blocks proving the suite
// well-formed.
import { env, nodeUrl } from './support/env';
import { loadStandFile } from './support/standFile';
import { reconcileStand } from './support/reconcile';
import type { FullConfig } from '@playwright/test';
import { standRecordDir, waitForApiRealmReadiness, writeStandRecord } from './support/apiReadiness';

async function checkReachable(url: string, label: string): Promise<void> {
  let response: Response;
  try {
    response = await fetch(url, { redirect: 'manual' });
  } catch (error) {
    throw new Error(
      `${label} is not reachable at ${url}. Is the stand up ` +
        `(dotnet run --project src/backend/Mustask.AppHost)? (${(error as Error).message})`,
    );
  }
  if (response.status >= 500) {
    throw new Error(`${label} at ${url} responded with HTTP ${response.status}.`);
  }
}

export default async function globalSetup(config: FullConfig): Promise<void> {
  const { panelUrl, apiUrl, keycloakUrl, realm } = env();
  await checkReachable(nodeUrl(panelUrl), 'The panel');
  await checkReachable(nodeUrl(`${apiUrl}/alive`), 'Mustask.Api (/alive)');

  // Repairs what an aborted or force-killed prior run left
  // behind — a stopped keycloak container — before checking the realm is
  // reachable, not after: a stopped container is
  // exactly what makes that check fail, so the repair has to run while it
  // can still fire, not once the run has already died on it. Best-effort —
  // reads the stand file without verifying the secret (Keycloak may not be
  // up yet), and does not fail the run on its own.
  await reconcileStand();

  await checkReachable(nodeUrl(`${keycloakUrl}/realms/${realm}`), `The Keycloak realm '${realm}'`);

  // The realm answering *us* is not the same fact as Mustask.Api knowing it —
  // see waitForApiRealmReadiness. This is the check that makes the suite
  // safe to start immediately after the stand comes up.
  // The observation goes to disk as well as to the terminal: a run a human
  // started is read back from Playwright's report, which never carries what
  // global setup printed.
  await writeStandRecord(
    await waitForApiRealmReadiness(apiUrl),
    standRecordDir(config.configFile),
  );

  // Fails fast, naming the cause, before any spec runs —
  // the stand-only client missing entirely, a stand file from another
  // stand, or a secret Keycloak rejects.
  await loadStandFile();
}
