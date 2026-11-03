import { describe, expect, it } from 'vitest';
import { objectsOf } from './workContext';

// feat-043: the context is the four selectors of ADR-0032; the lists' defaults, the "first entry"
// fallbacks and the level resets of feat-040 / mod-020 are retired with the old selector bar.
describe('objectsOf (ADR-0032 § 1)', () => {
  it('reads the three object selectors, «все» being null', () => {
    expect(objectsOf({ organizationId: 'org-1' })).toEqual({
      department: null,
      team: null,
      project: null,
    });
  });

  it('carries every chosen object, whatever the others', () => {
    expect(
      objectsOf({
        organizationId: 'org-1',
        departmentId: 'dep-1',
        teamId: 'team-1',
        projectId: 'prj-1',
      }),
    ).toEqual({ department: 'dep-1', team: 'team-1', project: 'prj-1' });
  });

  it('does not default a team: an organization alone has none chosen', () => {
    expect(objectsOf({ organizationId: 'org-1', projectId: 'prj-1' }).team).toBeNull();
  });
});
