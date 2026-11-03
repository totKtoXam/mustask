import { describe, expect, it } from 'vitest';
import type { WorkContextOffers } from '../../../shared/lib/workContext';
import { unlinkResetsDepartmentTeam } from './linkResets';

// feat-043 D-4: the page that unlinks a team from a department stays quiet when the shell resets.

const OFFERS: WorkContextOffers = {
  departments: [{ id: 'log', name: 'Логистика' }],
  teams: [{ id: 'mars', name: 'MARS', isMember: true, departmentIds: ['log'] }],
  projects: [],
};
const CHOSEN = { organizationId: 'o', departmentId: 'log', teamId: 'mars' };

describe('unlinkResetsDepartmentTeam', () => {
  it('is true when both are chosen and nothing else keeps them fitting', () => {
    expect(
      unlinkResetsDepartmentTeam({ selection: CHOSEN, offers: OFFERS }, 'o', 'log', 'mars'),
    ).toBe(true);
  });

  it('is false when a project placed in the department is linked to the team', () => {
    const through: WorkContextOffers = {
      ...OFFERS,
      projects: [{ id: 'a', name: 'A', key: 'A', departmentId: 'log', teamIds: ['mars'] }],
    };
    expect(
      unlinkResetsDepartmentTeam({ selection: CHOSEN, offers: through }, 'o', 'log', 'mars'),
    ).toBe(false);
  });

  it('is false when the pair is not the chosen one, or the offers are not read', () => {
    const other = { ...CHOSEN, teamId: 'venus' };
    expect(
      unlinkResetsDepartmentTeam({ selection: other, offers: OFFERS }, 'o', 'log', 'mars'),
    ).toBe(false);
    expect(
      unlinkResetsDepartmentTeam({ selection: CHOSEN, offers: undefined }, 'o', 'log', 'mars'),
    ).toBe(false);
    expect(
      unlinkResetsDepartmentTeam({ selection: CHOSEN, offers: OFFERS }, 'other', 'log', 'mars'),
    ).toBe(false);
  });
});
