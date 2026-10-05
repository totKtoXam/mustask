import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import { organizationErrorCodes } from './errorCodes';

/** The repository's error catalog of the organizations module (ADR-0006). */
const ORGANIZATIONS_CATALOG = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  '../../../../../contracts/errors/organizations.json',
);

interface ErrorCatalog {
  module: string;
  codes: Record<string, { statuses: number[]; params: Record<string, unknown> }>;
}

function readCatalog(): ErrorCatalog {
  return JSON.parse(readFileSync(ORGANIZATIONS_CATALOG, 'utf8')) as ErrorCatalog;
}

/** api-contract § 6, row by row: code, status, parameters. */
const CONTRACT_TABLE: readonly (readonly [string, number, readonly string[]])[] = [
  ['organizations.organization.not_found', 404, []],
  ['organizations.project.not_found', 404, []],
  ['organizations.team.not_found', 404, []],
  ['organizations.area.not_found', 404, []],
  ['organizations.validation_failed', 400, []],
  ['organizations.name_length', 400, []],
  ['organizations.organization.create_forbidden', 403, []],
  ['organizations.organization.create_failed', 500, []],
  ['organizations.organization.name_taken', 409, ['name']],
  ['organizations.project.name_taken', 409, []],
  ['organizations.project.name_taken_by_deactivated', 409, []],
  ['organizations.project.key_required', 400, []],
  ['organizations.project.key_invalid', 400, []],
  ['organizations.project.key_taken', 409, ['key']],
  ['organizations.team.name_taken', 409, []],
  ['organizations.team.default_area_not_owned', 409, []],
  ['organizations.team.first_team_default_fixed', 409, []],
  ['organizations.team.first_team_cannot_be_deleted', 409, []],
  ['organizations.area.name_taken', 409, []],
  ['organizations.area.move_into_own_subtree', 409, []],
  ['organizations.area.is_team_default', 409, ['team']],
  ['organizations.area.not_empty', 409, []],
  ['organizations.area.root_is_fixed', 409, []],
];

describe('mod-002 api-contract § 6: the panel holds the organizations codes one to one with the catalog', () => {
  it('holds every catalog code as a constant, and no constant the catalog lacks', () => {
    const catalog = readCatalog();
    const constants = Object.values(organizationErrorCodes);

    expect(catalog.module).toBe('organizations');
    expect([...constants].sort()).toEqual(Object.keys(catalog.codes).sort());
    expect(new Set(constants).size).toBe(constants.length);
  });

  it('matches api-contract § 6 row by row: the 23 codes, their statuses and parameters', () => {
    const catalog = readCatalog();

    expect(Object.values(organizationErrorCodes)).toHaveLength(CONTRACT_TABLE.length);
    for (const [code, status, params] of CONTRACT_TABLE) {
      expect(Object.values(organizationErrorCodes)).toContain(code);
      expect(catalog.codes[code]?.statuses).toEqual([status]);
      expect(Object.keys(catalog.codes[code]?.params ?? {})).toEqual(params);
    }
  });

  it('keeps mod-001’s name for the organization not-found code', () => {
    expect(organizationErrorCodes.notFound).toBe('organizations.organization.not_found');
  });
});
