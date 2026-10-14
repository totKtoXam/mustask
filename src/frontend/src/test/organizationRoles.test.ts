import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import {
  ADMIN_CODES,
  MEMBER_CODES,
  VIEWER_CODES,
  checkFor,
  servedFor,
  withoutCodes,
  type TestRole,
} from './organizationRoles';

// The bundles are hand-copied from api-contract § 0.2 (plan-frontend OP-7).
// There is no machine-readable bundle in contracts/, so this test reads the
// contract's own table and fails when the copy drifts from it.

const CONTRACT = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  '../../../../docs/features/mod-016-organization-access/api-contract.md',
);

/** `code -> [admin, member, viewer]` from the table of § 0.2. */
function contractMatrix(): Map<string, [boolean, boolean, boolean]> {
  const text = readFileSync(CONTRACT, 'utf8');
  const start = text.indexOf('### 0.2 ');
  const end = text.indexOf('Team membership grants no code', start);
  const matrix = new Map<string, [boolean, boolean, boolean]>();
  for (const line of text.slice(start, end).split('\n')) {
    const match = /^\| `([a-z.]+)`[^|]*\| (yes|no) \| (yes|no) \| (yes|no) \|/.exec(line);
    if (match) {
      matrix.set(match[1] as string, [match[2] === 'yes', match[3] === 'yes', match[4] === 'yes']);
    }
  }
  return matrix;
}

describe('the hand-copied role bundles (api-contract § 0.2)', () => {
  it('agree with the contract’s table, role by role', () => {
    const matrix = contractMatrix();

    expect(matrix.size).toBe(17);
    const heldBy = (index: number) =>
      [...matrix].filter(([, held]) => held[index]).map(([code]) => code);
    expect([...ADMIN_CODES].sort()).toEqual(heldBy(0).sort());
    expect([...MEMBER_CODES].sort()).toEqual(heldBy(1).sort());
    expect([...VIEWER_CODES].sort()).toEqual(heldBy(2).sort());
  });

  it('nest: the reader’s codes are the member’s, the member’s are the administrator’s', () => {
    expect(VIEWER_CODES.every((code) => MEMBER_CODES.includes(code))).toBe(true);
    expect(MEMBER_CODES.every((code) => ADMIN_CODES.includes(code))).toBe(true);
    expect(VIEWER_CODES.some((code) => /\.(write|deactivate|reorder)$/.test(code))).toBe(false);
  });

  it('serve the codes in ordinal order with the role, as GET …/access does', () => {
    for (const role of ['admin', 'member', 'viewer'] as const satisfies readonly TestRole[]) {
      const served = servedFor(role);
      expect(served.role).toBe(role);
      expect(served.permissions).toEqual([...served.permissions].sort());
    }
    expect(servedFor('viewer').permissions).toHaveLength(7);
    expect(servedFor('member').permissions).toHaveLength(10);
    expect(servedFor('admin').permissions).toHaveLength(17);
  });

  it('leave codes out of a served body, and ask an organization code from the bundle', () => {
    expect(withoutCodes('member', 'boards.backlog.reorder').permissions).not.toContain(
      'boards.backlog.reorder',
    );
    expect(withoutCodes('member', 'boards.backlog.reorder').permissions).toHaveLength(9);

    const member = checkFor('member', ['dictionaries.languages.read']);
    expect(member('workitems.workitem.write', 'organization')).toBe(true);
    expect(member('organizations.project.write', 'organization')).toBe(false);
    expect(member('dictionaries.languages.read', 'instance')).toBe(true);
    expect(member('dictionaries.languages.write', 'instance')).toBe(false);
  });
});
