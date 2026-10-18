import { describe, expect, it } from 'vitest';
import {
  MEMBERS_ROUTE,
  ORGANIZATIONS_ROUTES,
  areasPath,
  deactivatedProjectsPath,
  membersPath,
  organizationGeneralPath,
  organizationTeamPath,
  organizationTeamsPath,
  projectGeneralPath,
  projectsPath,
  teamsPath,
} from './paths';
import { organizationsPermissions } from './permissions';

describe('mod-002 design § 1, mod-016 design § 1 and feat-028 design § 1: the slice’s routes', () => {
  it('are exactly the eleven, under mod-015 D-17’s prefixes', () => {
    expect(ORGANIZATIONS_ROUTES).toEqual([
      '/organizations',
      '/organizations/deactivated',
      '/organizations/:organizationId/settings/general',
      '/organizations/:organizationId/settings/projects',
      '/organizations/:organizationId/settings/projects/deactivated',
      '/organizations/:organizationId/settings/members',
      '/organizations/:organizationId/settings/teams',
      '/organizations/:organizationId/settings/teams/:teamId',
      '/organizations/:organizationId/projects/:projectId/settings/general',
      '/organizations/:organizationId/projects/:projectId/settings/teams',
      '/organizations/:organizationId/projects/:projectId/settings/areas',
    ]);
  });

  it('has no team page beneath a project any more (BR-OL-12, design § 9)', () => {
    expect(
      ORGANIZATIONS_ROUTES.some((route) => route.includes('/projects/:projectId/settings/teams/')),
    ).toBe(false);
  });

  it('builds each address with every id re-encoded as received', () => {
    expect(organizationGeneralPath('o-1')).toBe('/organizations/o-1/settings/general');
    expect(projectsPath('a/b')).toBe('/organizations/a%2Fb/settings/projects');
    expect(deactivatedProjectsPath('o')).toBe('/organizations/o/settings/projects/deactivated');
    expect(membersPath('o #1')).toBe('/organizations/o%20%231/settings/members');
    expect(projectGeneralPath('o', 'p q')).toBe('/organizations/o/projects/p%20q/settings/general');
    expect(teamsPath('o', 'p')).toBe('/organizations/o/projects/p/settings/teams');
    expect(organizationTeamsPath('o 1')).toBe('/organizations/o%201/settings/teams');
    expect(organizationTeamPath('o', 't?')).toBe('/organizations/o/settings/teams/t%3F');
    expect(areasPath('o', 'p')).toBe('/organizations/o/projects/p/settings/areas');
  });
});

describe('mod-002 US-1 Scenario: No screen grants the right to create organizations', () => {
  it('has no route about rights, roles or groups, and reads the create right from the token', () => {
    // «Участники» (mod-016) is the one route that names the people of an
    // organization; it is a list of memberships, not a screen of rights.
    for (const route of ORGANIZATIONS_ROUTES.filter((candidate) => candidate !== MEMBERS_ROUTE)) {
      expect(route).not.toMatch(/right|role|group|member|permission|access/i);
    }
    expect(MEMBERS_ROUTE).not.toMatch(/right|role|group|permission|access/i);
    // mod-016: the twelve organization codes of api-contract § 0.2 are served per
    // organization (`GET …/access`), never in the token, and feat-028 adds four (its § 0.1);
    // the create right is the slice's one instance code.
    expect(Object.values(organizationsPermissions)).toEqual([
      'organizations.organization.read',
      'organizations.organization.write',
      'organizations.organization.deactivate',
      'organizations.membership.read',
      'organizations.membership.write',
      'organizations.project.read',
      'organizations.project.write',
      'organizations.project.deactivate',
      'organizations.team.read',
      'organizations.team.write',
      'organizations.team.create',
      'organizations.team.delete',
      'organizations.team.link',
      'organizations.project.link',
      'organizations.area.read',
      'organizations.area.write',
      'organizations.organization.create',
    ]);
  });
});
