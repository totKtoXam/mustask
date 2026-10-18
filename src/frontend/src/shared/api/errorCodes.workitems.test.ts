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

/**
 * mod-003 api-contract § 4, feat-001 api-contract § 5, feat-022 api-contract
 * § 6, feat-027 api-contract § 5 and mod-005 api-contract § 4, row by row: code, status and parameters
 * (only the cycle and the depth bound have any).
 */
const CONTRACT_TABLE: readonly (readonly [string, number, readonly string[]])[] = [
  ['workitems.workitem.not_found', 404, []],
  ['workitems.validation_failed', 400, []],
  ['workitems.workitem.title_required', 400, []],
  ['workitems.workitem.title_too_long', 400, []],
  ['workitems.workitem.title_multiline', 400, []],
  ['workitems.workitem.description_too_long', 400, []],
  ['workitems.workitem.request_id_invalid', 400, []],
  ['workitems.invalid_page', 400, []],
  ['workitems.workitem.type_not_found', 404, []],
  ['workitems.workitem.state_not_allowed', 409, []],
  ['workitems.workitem.assignee_unavailable', 409, []],
  ['workitems.link.parent_not_found', 404, []],
  ['workitems.link.parent_level_not_allowed', 409, []],
  ['workitems.link.self_parent', 409, []],
  ['workitems.link.cycle', 409, ['item', 'parent', 'path']],
  ['workitems.link.parent_changed', 409, []],
  ['workitems.link.item_deleted', 409, []],
  ['workitems.link.depth_exceeded', 409, ['limit']],
  ['workitems.workitem.number_out_of_range', 400, []],
  ['workitems.workitem.number_too_precise', 400, []],
  ['workitems.workitem.integer_required', 400, []],
  ['workitems.workitem.value_not_in_list', 400, []],
  ['workitems.workitem.date_out_of_range', 400, []],
  ['workitems.workitem.target_before_start', 400, []],
  ['workitems.workitem.text_too_long', 400, []],
  ['workitems.workitem.field_not_on_type', 400, []],
  ['workitems.workitem.deleted', 409, []],
  ['workitems.workitem.not_deleted', 409, []],
];

describe('mod-003 api-contract § 4: the panel holds the workitems codes one to one with the catalog', () => {
  it('holds every catalog code as a constant, and no constant the catalog lacks', () => {
    const catalog = readCatalog();
    const constants = Object.values(workItemErrorCodes);

    expect(catalog.module).toBe('workitems');
    expect([...constants].sort()).toEqual(Object.keys(catalog.codes).sort());
    expect(new Set(constants).size).toBe(constants.length);
  });

  it('matches the contracts row by row: the 28 codes, their statuses, their parameters', () => {
    const catalog = readCatalog();

    expect(Object.values(workItemErrorCodes)).toHaveLength(CONTRACT_TABLE.length);
    for (const [code, status, params] of CONTRACT_TABLE) {
      expect(Object.values(workItemErrorCodes)).toContain(code);
      expect(catalog.codes[code]?.statuses).toEqual([status]);
      expect(Object.keys(catalog.codes[code]?.params ?? {}).sort()).toEqual(params);
    }
  });
});
