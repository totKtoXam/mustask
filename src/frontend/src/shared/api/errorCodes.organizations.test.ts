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
  // mod-017 api-contract § 0.2 and § 2.7: the two codes the owning team adds. The four codes above are deprecated in the catalog
  // and still held, one to one.
  ['organizations.team.not_in_project', 409, []],
  ['organizations.team.last_team_cannot_be_deleted', 409, []],
  ['organizations.area.name_taken', 409, []],
  ['organizations.area.move_into_own_subtree', 409, []],
  ['organizations.area.is_team_default', 409, ['team']],
  ['organizations.area.not_empty', 409, []],
  ['organizations.area.root_is_fixed', 409, []],
  // mod-016 api-contract § 5: the scoped check, the memberships and the team members.
  ['organizations.access.forbidden', 403, []],
  ['organizations.membership.already_member', 409, []],
  ['organizations.membership.last_admin', 409, []],
  ['organizations.membership.user_unavailable', 409, []],
  ['organizations.membership.not_found', 404, []],
  ['organizations.membership.role_invalid', 400, []],
  ['organizations.membership.user_invalid', 400, []],
  ['organizations.team.member_exists', 409, []],
  ['organizations.team.member_not_in_organization', 409, []],
  ['organizations.team.member_not_found', 404, []],
  // feat-028 api-contract § 3: the two field reasons the descriptions and the unlink add. The deprecated guard
  // code above stays held, one to one with the catalog.
  ['organizations.description_length', 400, []],
  ['organizations.team.destination_required', 400, []],
];

describe('mod-002 api-contract § 6: the panel holds the organizations codes one to one with the catalog', () => {
  it('holds every catalog code as a constant, and no constant the catalog lacks', () => {
    const catalog = readCatalog();
    const constants = Object.values(organizationErrorCodes);

    expect(catalog.module).toBe('organizations');
    expect([...constants].sort()).toEqual(Object.keys(catalog.codes).sort());
    expect(new Set(constants).size).toBe(constants.length);
  });

  it('matches api-contract § 6 row by row: the 37 codes (mod-002’s 23, mod-016’s 10, mod-017’s 2 and feat-028’s 2), their statuses and parameters', () => {
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
