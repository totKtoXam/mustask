import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import { iterationErrorCodes } from './errorCodes';

/** The repository's error catalog of the iterations module (ADR-0006). */
const ITERATIONS_CATALOG = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  '../../../../../contracts/errors/iterations.json',
);

interface ErrorCatalog {
  module: string;
  codes: Record<string, { statuses: number[]; params: Record<string, unknown> }>;
}

describe('mod-007 api-contract § 4: the panel holds the iterations codes one to one with the catalog', () => {
  it('holds every catalog code as a constant, and no constant the catalog lacks', () => {
    const catalog = JSON.parse(readFileSync(ITERATIONS_CATALOG, 'utf8')) as ErrorCatalog;
    const constants = Object.values(iterationErrorCodes);

    expect(catalog.module).toBe('iterations');
    expect([...constants].sort()).toEqual(Object.keys(catalog.codes).sort());
    expect(new Set(constants).size).toBe(constants.length);
  });
});
