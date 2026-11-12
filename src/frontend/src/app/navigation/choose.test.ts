import { describe, expect, it } from 'vitest';
import { organizationsPermissions } from '../../features/organizations';
import { targetAfterChoice } from './choose';
import type { ObjectSelection } from './selectorFit';

// ADR-0032 § 5, design § 3: a choice keeps the current entry when the new menu has it (same
// segment, same level), else opens the first entry the user may open, else the «Обзор».

const ORG = 'org-1';
const NONE: ObjectSelection = { department: null, team: null, project: null };
const TEAM: ObjectSelection = { ...NONE, team: 't-1' };
const PROJECT: ObjectSelection = { ...NONE, project: 'p-1' };
const BASE = `/organizations/${ORG}`;
const everything = () => true;

describe('targetAfterChoice', () => {
  it('keeps «Канбан» and its level from a team to another', () => {
    expect(
      targetAfterChoice({
        organizationId: ORG,
        pathname: `${BASE}/teams/t-1/boards/epics`,
        from: TEAM,
        to: { ...NONE, team: 't-2' },
        canOpen: everything,
      }),
    ).toBe(`${BASE}/teams/t-2/boards/epics`);
  });

  it('keeps the entry from a team to a project, the team riding in the query', () => {
    expect(
      targetAfterChoice({
        organizationId: ORG,
        pathname: `${BASE}/teams/t-1/backlogs/features`,
        from: TEAM,
        to: { ...PROJECT, team: 't-1' },
        canOpen: everything,
      }),
    ).toBe(`${BASE}/projects/p-1/backlogs/features?teamId=t-1`);
  });

  it('opens the first entry of the new menu when the old one is not in it', () => {
    expect(
      targetAfterChoice({
        organizationId: ORG,
        pathname: `${BASE}/teams/t-1/members`,
        from: TEAM,
        to: PROJECT,
        canOpen: everything,
      }),
    ).toBe(`${BASE}/projects/p-1/general`);
  });

  it('skips an entry the user may not open', () => {
    expect(
      targetAfterChoice({
        organizationId: ORG,
        pathname: `${BASE}/teams/t-1/members`,
        from: TEAM,
        to: { ...NONE, department: 'd-1' },
        canOpen: (code) => code !== organizationsPermissions.areaRead,
      }),
    ).toBe(`${BASE}/areas/d-1/projects`);
  });

  it('lands on the organization «Обзор» when nothing in the new menu may be opened', () => {
    expect(
      targetAfterChoice({
        organizationId: ORG,
        pathname: `${BASE}/teams/t-1/overview`,
        from: TEAM,
        to: PROJECT,
        canOpen: () => false,
      }),
    ).toBe(`${BASE}/projects/p-1/overview`);
  });

  it('keeps the entry when the selectors are cleared back to the organization', () => {
    expect(
      targetAfterChoice({
        organizationId: ORG,
        pathname: `${BASE}/teams/t-1/projects`,
        from: TEAM,
        to: NONE,
        canOpen: everything,
      }),
    ).toBe(`${BASE}/projects`);
  });
});
