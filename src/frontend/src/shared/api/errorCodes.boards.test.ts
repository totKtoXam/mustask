import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import { boardErrorCodes } from './errorCodes';

/** The repository's error catalog of the boards module (ADR-0006). */
const BOARDS_CATALOG = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  '../../../../../contracts/errors/boards.json',
);

interface ErrorCatalog {
  module: string;
  codes: Record<string, { statuses: number[]; params: Record<string, unknown> }>;
}

function readCatalog(): ErrorCatalog {
  return JSON.parse(readFileSync(BOARDS_CATALOG, 'utf8')) as ErrorCatalog;
}

/** mod-006 api-contract § 4, row by row: code and status; no code has parameters. */
const CONTRACT_TABLE: readonly (readonly [string, number])[] = [
  ['boards.backlog.reorder_stale', 409],
  ['boards.level.not_found', 404],
  ['boards.validation_failed', 400],
  ['boards.backlog.position_invalid', 400],
];

describe('mod-006 api-contract § 4: the panel holds the boards codes one to one with the catalog', () => {
  it('holds every catalog code as a constant, and no constant the catalog lacks', () => {
    const catalog = readCatalog();
    const constants = Object.values(boardErrorCodes);

    expect(catalog.module).toBe('boards');
    expect([...constants].sort()).toEqual(Object.keys(catalog.codes).sort());
    expect(new Set(constants).size).toBe(constants.length);
  });

  it('matches api-contract § 4 row by row: the 4 codes, their statuses, no parameters', () => {
    const catalog = readCatalog();

    expect(Object.values(boardErrorCodes)).toHaveLength(CONTRACT_TABLE.length);
    for (const [code, status] of CONTRACT_TABLE) {
      expect(Object.values(boardErrorCodes)).toContain(code);
      expect(catalog.codes[code]?.statuses).toEqual([status]);
      expect(Object.keys(catalog.codes[code]?.params ?? {})).toEqual([]);
    }
  });
});
