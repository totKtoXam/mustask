import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import { workItemErrorCodes } from './errorCodes';

/** The repository's error catalog of the work items module (ADR-0006). */
const WORKITEMS_CATALOG = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  '../../../../../contracts/errors/workitems.json',
);

interface ErrorCatalog {
  module: string;
  codes: Record<string, { statuses: number[]; params: Record<string, unknown> }>;
}

function readCatalog(): ErrorCatalog {
  return JSON.parse(readFileSync(WORKITEMS_CATALOG, 'utf8')) as ErrorCatalog;
}

/** mod-003 api-contract § 4, row by row: code and status; no code has parameters. */
const CONTRACT_TABLE: readonly (readonly [string, number])[] = [
  ['workitems.workitem.not_found', 404],
  ['workitems.validation_failed', 400],
  ['workitems.workitem.title_required', 400],
  ['workitems.workitem.title_too_long', 400],
  ['workitems.workitem.title_multiline', 400],
  ['workitems.workitem.description_too_long', 400],
  ['workitems.workitem.request_id_invalid', 400],
  ['workitems.invalid_page', 400],
  ['workitems.workitem.type_not_found', 404],
  ['workitems.workitem.state_not_allowed', 409],
  ['workitems.workitem.assignee_unavailable', 409],
];

describe('mod-003 api-contract § 4: the panel holds the workitems codes one to one with the catalog', () => {
  it('holds every catalog code as a constant, and no constant the catalog lacks', () => {
    const catalog = readCatalog();
    const constants = Object.values(workItemErrorCodes);

    expect(catalog.module).toBe('workitems');
    expect([...constants].sort()).toEqual(Object.keys(catalog.codes).sort());
    expect(new Set(constants).size).toBe(constants.length);
  });

  it('matches api-contract § 4 row by row: the 11 codes, their statuses, no parameters', () => {
    const catalog = readCatalog();

    expect(Object.values(workItemErrorCodes)).toHaveLength(CONTRACT_TABLE.length);
    for (const [code, status] of CONTRACT_TABLE) {
      expect(Object.values(workItemErrorCodes)).toContain(code);
      expect(catalog.codes[code]?.statuses).toEqual([status]);
      expect(Object.keys(catalog.codes[code]?.params ?? {})).toEqual([]);
    }
  });
});
