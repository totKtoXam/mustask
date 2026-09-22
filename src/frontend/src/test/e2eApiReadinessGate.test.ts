import { mkdtemp, readFile, rm } from 'node:fs/promises';
import { createServer, type Server } from 'node:http';
import type { AddressInfo } from 'node:net';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { http, passthrough } from 'msw';
import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  standRecordDir,
  waitForApiRealmReadiness,
  writeStandRecord,
} from '../../e2e/support/apiReadiness';
import { server as mswServer } from './mocks/server';

/**
 * Found live on a cold stand: the E2E global setup used to
 * start the suite as soon as *it* could reach Keycloak, which is not the
 * same fact as `Mustask.Api` knowing the realm is up — the API learns that only
 * from `RealmReadinessMonitor` (30s `CheckInterval`, and its readiness
 * latches, so a warm stand can never show the problem). In the gap
 * `/api/session/login` answers `authError=idp_unavailable` without
 * redirecting to Keycloak and every spec that signs in dies on its own 30s
 * timeout — which is what a human's cold-stand run hit on specs 1 and 2.
 *
 * This test is here so the gate stays a fact rather than a promise — code
 * that nobody has executed is only a promise. It runs under Vitest, not
 * Playwright — no stand, no Docker, just a stub answering the way `/health`
 * answers.
 */
describe('E2E global setup: the Mustask.Api realm-readiness gate', () => {
  let server: Server | undefined;

  afterEach(async () => {
    vi.restoreAllMocks();
    await new Promise<void>((resolve) => server?.close(() => resolve()) ?? resolve());
    server = undefined;
  });

  /**
   * Answers exactly as `Mustask.Api` does: the endpoint has no custom
   * `ResponseWriter`, so the body is the single word `Healthy`/`Unhealthy`
   * and never says which check is red (`Mustask.ServiceDefaults/Extensions.cs`;
   * pinned by `HealthEndpointTests`). Answers `Healthy` from the
   * `healthyFrom`-th request on; `status` overrides the not-ready code.
   */
  async function stubApi(
    healthyFrom: number,
    status = 503,
  ): Promise<{ url: string; paths: string[] }> {
    const paths: string[] = [];
    let seen = 0;
    server = createServer((request, response) => {
      paths.push(request.url ?? '');
      seen += 1;
      if (seen >= healthyFrom) {
        response.writeHead(200, { 'content-type': 'text/plain' }).end('Healthy');
      } else {
        response.writeHead(status, { 'content-type': 'text/plain' }).end('Unhealthy');
      }
    });
    await new Promise<void>((resolve) => server!.listen(0, '127.0.0.1', resolve));
    const url = `http://127.0.0.1:${(server!.address() as AddressInfo).port}`;
    // The suite's MSW setup answers every unhandled request with an error
    // (src/test/setup.ts). The gate is built to survive exactly that and keep
    // polling, so without a passthrough this test would measure MSW, not the
    // gate — and would grind to the deadline instead of asserting anything.
    mswServer.use(http.get(`${url}/health`, () => passthrough()));
    return { url, paths };
  }

  it('waits while Mustask.Api reports the realm not ready, then proceeds', async () => {
    const log = vi.spyOn(console, 'log').mockImplementation(() => {});
    const { url, paths } = await stubApi(3);

    await waitForApiRealmReadiness(url, { pollMs: 200 });

    // Proceeding on the first 503 is exactly the defect; it must poll.
    expect(paths.length).toBe(3);
    // It asks /health — /alive is tag-limited to `live` and cannot see this.
    expect(new Set(paths)).toEqual(new Set(['/health']));
    // A slow start explains itself in the run log instead of looking like a
    // hang — and names what it saw, so the line is evidence and not a label.
    expect(log).toHaveBeenCalledWith(expect.stringMatching(/cold stand.*HTTP 503: Unhealthy/s));
    // Exactly one line, and not the other one. Asserting only that the right
    // string appears would stay green if both were printed — the two lines
    // contradict each other, so a run that printed both would be worthless.
    expect(log).toHaveBeenCalledTimes(1);
    expect(log.mock.calls[0]?.[0]).not.toContain('warm stand');
  });

  it('names a warm stand, so a run that could not show the defect says so', async () => {
    const log = vi.spyOn(console, 'log').mockImplementation(() => {});
    const { url, paths } = await stubApi(1);

    await waitForApiRealmReadiness(url);

    expect(paths.length).toBe(1);
    // Silence used to be the warm case. It left the two runs
    // indistinguishable in a report, which is how three green runs on a
    // warm stand came to be read as evidence that the suite works on any
    // stand — the run that mattered, on a cold one, failed two specs.
    expect(log).toHaveBeenCalledWith(expect.stringContaining('warm stand'));
    expect(log).toHaveBeenCalledTimes(1);
    expect(log.mock.calls[0]?.[0]).not.toContain('cold stand');
  });

  it('writes the observation where an agent reading the run from disk finds it', async () => {
    vi.spyOn(console, 'log').mockImplementation(() => {});
    const { url } = await stubApi(2);
    const directory = await mkdtemp(join(tmpdir(), 'e2e-stand-'));

    try {
      // A directory that does not exist yet — the first run on a fresh stand.
      // Handing it an existing one would never exercise the writer's own
      // mkdir, and dropping that line would throw ENOENT in production while
      // this test stayed green.
      const outputDir = join(directory, 'e2e-results');
      const path = await writeStandRecord(
        await waitForApiRealmReadiness(url, { pollMs: 50 }),
        outputDir,
      );

      // The name is not an implementation detail: three doc surfaces quote
      // `stand.json` verbatim, so a rename has to break something here.
      expect(path).toBe(join(outputDir, 'stand.json'));
      // The console line never reaches e2e-results/results.json: Playwright's
      // JSON reporter serialises stdout per test, and global setup runs before
      // any test exists. The Definition of Done quotes this record, so a run a
      // human started has to leave it behind.
      expect(JSON.parse(await readFile(path, 'utf8'))).toMatchObject({
        stand: 'cold',
        firstSeen: 'HTTP 503: Unhealthy',
        url: `${url}/health`,
      });
    } finally {
      await rm(directory, { recursive: true, force: true });
    }
  });

  it('anchors the record to the Playwright config, not to the working directory', () => {
    // Playwright resolves reporter paths against the config directory, so
    // results.json lands in src/frontend/e2e-results whatever the cwd. Taking
    // the same anchor is what makes "beside the report" true — and the
    // Definition of Done names that path literally.
    expect(standRecordDir(join('C:', 'repo', 'src', 'frontend', 'playwright.config.ts'))).toBe(
      join('C:', 'repo', 'src', 'frontend', 'e2e-results'),
    );
    // No config file leaves no better anchor than the cwd.
    expect(standRecordDir(undefined, join('C:', 'elsewhere'))).toBe(
      join('C:', 'elsewhere', 'e2e-results'),
    );
  });

  it('gives up with a message naming the endpoint and what it last saw', async () => {
    const { url } = await stubApi(Number.MAX_SAFE_INTEGER);

    // The branch that carries all the diagnostic value, and the one a run
    // only ever reaches when something is actually wrong with the stand.
    await expect(waitForApiRealmReadiness(url, { timeoutMs: 600, pollMs: 100 })).rejects.toThrow(
      /\/health is still not green after 1s .*last seen: HTTP 503: Unhealthy/s,
    );
  });

  it('does not grind through the deadline when /health answers 4xx', async () => {
    const { url, paths } = await stubApi(Number.MAX_SAFE_INTEGER, 404);

    const startedAt = Date.now();
    // 401/403/404 is the regression Program.cs watches for — someone dropped
    // AllowAnonymous or moved the route. Terminal, not "not ready yet".
    await expect(waitForApiRealmReadiness(url, { timeoutMs: 10_000, pollMs: 100 })).rejects.toThrow(
      /answered HTTP 404.*anonymous/s,
    );

    expect(paths.length).toBe(1);
    expect(Date.now() - startedAt).toBeLessThan(2_000);
  });
});
