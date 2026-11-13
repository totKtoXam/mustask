import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import { importErrorCodes } from './errorCodes';

const CATALOG = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  '../../../../../contracts/errors/imports.json',
);

describe('importErrorCodes', () => {
  it('every constant is a code of contracts/errors/imports.json', () => {
    const catalog = JSON.parse(readFileSync(CATALOG, 'utf8')) as { codes: Record<string, unknown> };
    for (const code of Object.values(importErrorCodes)) {
      expect(Object.keys(catalog.codes), code).toContain(code);
    }
  });
});
