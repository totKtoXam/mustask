import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import { INSTANCE_ROLE_CODES } from '../shared/api/tokenClaims';
import { isOrganizationScope, resourceScopeOf, roleBundles } from '../shared/lib/roleBundles';
import {
  ADMIN_CODES,
  MEMBER_CODES,
  VIEWER_CODES,
  checkFor,
  servedFor,
  withoutCodes,
  type TestRole,
} from './organizationRoles';

// Since mod-019 the bundles are not copied by hand: they are read from contracts/access/role-bundles.json
// (api-contract § 9, ADR-0038), generated from the domain by the backend's contract test. This test pins
// the panel's reading of that file, and that the file is the one the panel imports.

const CONTRACT = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  '../../../../contracts/access/role-bundles.json',
);

describe('the role bundles, read from contracts/access/role-bundles.json', () => {
  it('are the file’s organization roles and instance table, not a copy', () => {
    const file = JSON.parse(readFileSync(CONTRACT, 'utf8')) as typeof roleBundles;

    expect([...ADMIN_CODES]).toEqual(file.organizationRoles.admin);
    expect([...MEMBER_CODES]).toEqual(file.organizationRoles.member);
    expect([...VIEWER_CODES]).toEqual(file.organizationRoles.viewer);
    expect(INSTANCE_ROLE_CODES).toEqual(file.instanceRoles);
  });

  it('nest: the reader’s codes are the member’s, the member’s are the administrator’s', () => {
    expect(VIEWER_CODES.every((code) => MEMBER_CODES.includes(code))).toBe(true);
    expect(MEMBER_CODES.every((code) => ADMIN_CODES.includes(code))).toBe(true);
    expect(
      VIEWER_CODES.some((code) => /\.(write|deactivate|reorder|delete|purge|grant)$/.test(code)),
    ).toBe(false);
    expect(MEMBER_CODES).not.toContain('workitems.workitem.purge');
  });

  it('every code of every bundle, scoped ones included, is in the catalog', () => {
    const catalog = new Set(roleBundles.catalog.map((entry) => entry.code));
    const scoped = Object.values(roleBundles.scopedRoles).flatMap((roles) =>
      Object.values(roles).flat(),
    );
    for (const code of [...ADMIN_CODES, ...Object.values(INSTANCE_ROLE_CODES).flat(), ...scoped]) {
      expect(catalog.has(code), code).toBe(true);
    }
  });

  it('tell an organization-scope code from an object-scope one (BR-SA-18)', () => {
    expect(isOrganizationScope('organizations.area.read')).toBe(true);
    expect(isOrganizationScope('organizations.area.create')).toBe(true);
    expect(isOrganizationScope('organizations.project.write')).toBe(false);
    expect(isOrganizationScope('workitems.workitem.write')).toBe(false);
    expect(isOrganizationScope('organizations.team.write')).toBe(false);
    expect(isOrganizationScope('dictionaries.languages.read')).toBe(false);
    expect(resourceScopeOf('boards.backlog.reorder')).toBe('team');
  });

  it('serve the codes in ordinal order with the role, as GET …/access does', () => {
    for (const role of ['admin', 'member', 'viewer'] as const satisfies readonly TestRole[]) {
      const served = servedFor(role);
      expect(served.role).toBe(role);
      expect(served.permissions).toEqual([...served.permissions].sort());
      expect(served.permissions).toHaveLength(codesCount(role));
    }
  });

  it('leave codes out of a served body, and ask an organization code from the bundle', () => {
    expect(withoutCodes('member', 'boards.backlog.reorder').permissions).not.toContain(
      'boards.backlog.reorder',
    );

    const member = checkFor('member', ['dictionaries.languages.read']);
    expect(member('workitems.workitem.write', 'organization')).toBe(true);
    expect(member('organizations.project.write', 'organization')).toBe(false);
    expect(member('dictionaries.languages.read', 'instance')).toBe(true);
    expect(member('dictionaries.languages.write', 'instance')).toBe(false);
  });
});

function codesCount(role: TestRole): number {
  return roleBundles.organizationRoles[role].length;
}
