import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';

const SOURCE = readFileSync(
  path.resolve(path.dirname(fileURLToPath(import.meta.url)), 'TreeRegistry.tsx'),
  'utf8',
);

describe('mod-002 US-5 Scenario: Shown as the tree registry — the pattern names nothing of areas', () => {
  it.each([['Област'], ['Владел'], ['Команд'], ['area'], ['owner'], ['team']])(
    'TreeRegistry.tsx holds no «%s», in any letter case',
    (word) => {
      expect(SOURCE.toLowerCase()).not.toContain(word.toLowerCase());
    },
  );
});
