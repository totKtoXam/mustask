import { mkdir, writeFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { nodeUrl } from './env';

/**
 * Where the stand record goes: beside Playwright's own reports, which it
 * resolves against the **config directory** and not the working directory
 * (`reporter: [['json', { outputFile: 'e2e-results/results.json' }]]`). Taking
 * the same anchor — `playwright.config.ts`'s own path, handed to global setup
 * in `FullConfig` — is what makes "beside the report" true from any cwd.
 *
 * A cwd-relative path would agree only while the suite is started from
 * `src/frontend`. Every documented command does; `npx playwright test -c
 * src/frontend` from the repository root does not, and that one would scatter
 * the record, break the path the Definition of Done names, and leave an
 * untracked copy behind — `/e2e-results/` is anchored in
 * `src/frontend/.gitignore` and does not cover a root-level one.
 *
 * `cwd` is the fallback for a run with no config file at all, where there is
 * no better anchor to be had.
 */
export function standRecordDir(configFile: string | undefined, cwd = process.cwd()): string {
  return join(configFile ? dirname(configFile) : cwd, 'e2e-results');
}

export interface RealmReadinessGateOptions {
  timeoutMs?: number;
  pollMs?: number;
}

/**
 * What the gate saw. `cold` means `/health` was not green at the first check,
 * so the suite started against an `Mustask.Api` that was not ready — the state a
 * warm stand can never reproduce. It does **not** say which of `/health`'s
 * checks was red: the endpoint is an aggregate over the Keycloak realm *and*
 * the database and answers with one word either way.
 */
export interface RealmReadinessObservation {
  stand: 'cold' | 'warm';
  /** The first non-green answer, verbatim. Absent on a warm stand. */
  firstSeen?: string;
  waitedMs: number;
  url: string;
  checkedAt: string;
}

/** Four `RealmReadinessMonitor.CheckInterval`s (30s each) — slack, not a guess. */
const DEFAULT_TIMEOUT_MS = 120_000;
/**
 * `/health` carries no tag filter, so every poll also hits Postgres through
 * the `MustaskDbContext` check — registered in
 * `Mustask.Infrastructure/DependencyInjection.cs` by `AddNpgsqlDbContext`, and
 * called out in `Mustask.ServiceDefaults/Extensions.cs`. Harmless on a stand,
 * but 3s keeps the gate honest company to that comment and costs it nothing
 * against a 30s `CheckInterval`.
 */
const DEFAULT_POLL_MS = 3_000;

/**
 * Waits until `Mustask.Api` itself considers the Keycloak realm ready — not
 * until *we* can reach the realm.
 *
 * Found live on a cold stand: the two
 * checks are not the same fact and can disagree for up to 30 seconds.
 * `Mustask.Api` learns the realm is up only from `RealmReadinessMonitor`, whose
 * `CheckInterval` is 30s, and its readiness *latches* — once ready, a
 * Keycloak outage never takes it back (`RealmReadinessMonitor.cs`, the
 * `readiness.Current.IsReady` guard). So on a warm stand this is invisible:
 * the API latched ready long ago and `docker stop`/`start` cannot unlatch
 * it. On a cold one the API's first check fails while Keycloak is still
 * starting, and until the next tick `/api/session/login` answers
 * `authError=idp_unavailable` *without* redirecting to Keycloak — the panel
 * renders "Сервис аутентификации недоступен" and no sign-in ever begins.
 * Specs 1 and 2 then die on their own 30s timeouts, which is the same
 * number as the gap, so this is a coin flip rather than a rare race.
 *
 * `/alive` cannot stand in for this: it is deliberately limited to the
 * `live` tag and documented as not seeing realm readiness
 * (`RealmReadinessHealthCheck.cs`). `/health` is the endpoint that carries
 * the `keycloak-realm` check, and it is anonymous.
 *
 * And `/health` is the exact signal rather than a good proxy: that check and
 * the login endpoint read the *same* singleton `RealmReadiness`
 * (`Program.cs`, `AddSingleton<RealmReadiness>()`), so "the realm is ready"
 * and "a login will actually start" are one fact, not two that usually
 * agree. Nothing here depends on winning a timing race to observe.
 *
 * What this does *not* cover: the stand still starting. The reachability
 * checks in `global-setup.ts` ahead of this one are single-shot and fail
 * fast by design, naming what did not answer.
 *
 * Every run says which stand it got and returns the same fact, for
 * `global-setup.ts` to write beside Playwright's report — see
 * `writeStandRecord` for why a console line alone is not enough.
 *
 * What `cold stand` claims, exactly: `/health` was not green at the first
 * check, so the suite started against an `Mustask.Api` that was not ready. That is
 * a superset of the window this gate exists for, not a proof of it — `/health`
 * is an aggregate over the Keycloak realm *and* the database, so a slow
 * Postgres produces the same line, and the endpoint cannot say which check was
 * red. `warm stand` is the exact negation and carries no such slack: the API
 * was ready at the first check, so the run cannot have exercised the window at
 * all. Neither line is a verdict on how the stand was started — one brought up
 * from nothing can still go green before the suite gets there. The procedure
 * is what makes a run cold (`docs/ai-workflow/stages/05-frontend.md`, § 7);
 * these lines only keep a warm run from being silent, which is how three green
 * ones came to be read as evidence of something they had never touched.
 */
export async function waitForApiRealmReadiness(
  apiUrl: string,
  options: RealmReadinessGateOptions = {},
): Promise<RealmReadinessObservation> {
  const timeoutMs = options.timeoutMs ?? DEFAULT_TIMEOUT_MS;
  const pollMs = options.pollMs ?? DEFAULT_POLL_MS;
  const url = nodeUrl(`${apiUrl}/health`);
  const startedAt = Date.now();
  const deadline = startedAt + timeoutMs;
  let lastSeen = 'no response yet';
  // The *first* non-green answer, kept apart from the last one: the line says
  // "at the first check", and `lastSeen` is by then several polls old.
  let firstSeen: string | undefined;
  let waited = false;

  for (;;) {
    let terminal: string | undefined;
    try {
      const response = await fetch(url, { redirect: 'manual' });
      if (response.ok) {
        const observation: RealmReadinessObservation = {
          stand: waited ? 'cold' : 'warm',
          firstSeen,
          waitedMs: Date.now() - startedAt,
          url,
          checkedAt: new Date().toISOString(),
        };
        // Every run says which of the two it was — see the note above the
        // function for why the warm line is not noise, and for the exact
        // claim each one makes.
        console.log(
          observation.stand === 'cold'
            ? `e2e global setup: cold stand — ${url} was not green at the first check ` +
                `(${firstSeen ?? lastSeen}) and went green after ` +
                `${Math.round(observation.waitedMs / 1000)}s. /health is an aggregate over the ` +
                `realm and the database, so this says Mustask.Api was not ready, not which check ` +
                `was red.`
            : `e2e global setup: warm stand — ${url} was green at the first check, so this ` +
                `run never started against an Mustask.Api that was not ready.`,
        );
        return observation;
      }
      const body = (await response.text()).trim();
      lastSeen = `HTTP ${response.status}${body ? `: ${body}` : ''}`;
      firstSeen ??= lastSeen;
      if (response.status >= 400 && response.status < 500) {
        // Not "not ready yet" — terminal, and exactly the regression
        // `Program.cs` watches for (someone dropped `.AllowAnonymous()` from
        // `MapHealthChecks` or moved the route). Grinding through the whole
        // deadline would report it as a Keycloak problem two minutes late.
        terminal =
          `${url} answered ${lastSeen}. The E2E readiness gate needs /health anonymous ` +
          `and on this path (Mustask.ServiceDefaults/Extensions.cs) — this is a broken route, ` +
          `not a stand that is still coming up, so nothing was waited for.`;
      }
    } catch (error) {
      // A connection error is not terminal: Mustask.Api may still be restarting.
      lastSeen = (error as Error).message;
      firstSeen ??= lastSeen;
    }

    if (terminal !== undefined) {
      throw new Error(terminal);
    }

    waited = true;

    if (Date.now() >= deadline) {
      throw new Error(
        `Mustask.Api's ${url} is still not green after ${Math.round(timeoutMs / 1000)}s ` +
          `(last seen: ${lastSeen}). While it is not, /api/session/login can answer ` +
          `authError=idp_unavailable instead of starting a login, and every spec that ` +
          `signs in fails on a timeout. /health covers the Keycloak realm *and* the ` +
          `database and answers with one word either way, so it cannot say which is red — ` +
          `the Aspire dashboard can.`,
      );
    }
    await new Promise((resolve) => setTimeout(resolve, pollMs));
  }
}

/**
 * Writes the observation beside Playwright's own report, under the name
 * `stand.json`.
 *
 * Why a file and not just the console line: Playwright's JSON reporter
 * serialises stdout per test, and global setup runs before any test exists, so
 * nothing it prints reaches `e2e-results/results.json` — the HTML report draws
 * on the same source and has the same hole. The line survives only in the
 * terminal of whoever started the run. That is fine when an agent runs the
 * suite itself, and useless in the other path the process explicitly allows,
 * where a human runs it and the agent reads the report from disk — a
 * Definition of Done once quoted a line from a report that could not
 * contain it.
 *
 * `e2e-results/` is gitignored, so this leaves nothing behind in the tree.
 */
export async function writeStandRecord(
  observation: RealmReadinessObservation,
  outputDir: string,
): Promise<string> {
  const path = join(outputDir, 'stand.json');
  await mkdir(outputDir, { recursive: true });
  await writeFile(path, `${JSON.stringify(observation, null, 2)}\n`, 'utf8');
  return path;
}
