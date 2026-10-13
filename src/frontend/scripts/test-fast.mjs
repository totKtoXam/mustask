// `npm run test:fast` — the suite without the files in src/test/slowFiles.json.
// Meant for the loop while developing; verify.sh and `npm test` run everything.
// Arguments go to `vitest run` as they are (a path filter, `--changed`, ...).
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const vitest = fileURLToPath(new URL('../node_modules/vitest/vitest.mjs', import.meta.url));
const result = spawnSync(process.execPath, [vitest, 'run', ...process.argv.slice(2)], {
  stdio: 'inherit',
  env: { ...process.env, VITEST_FAST: '1' },
});
process.exit(result.status ?? 1);
