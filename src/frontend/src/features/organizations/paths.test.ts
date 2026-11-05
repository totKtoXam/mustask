import { describe, expect, it } from 'vitest';
import {
  MEMBERS_ROUTE,
  ORGANIZATIONS_ROUTES,
  organizationAreaPath,
  organizationAreasPath,
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

const RETIRED_SHAPE =
  /\/settings\/(teams|areas|projects|members)|\/projects\/:projectId\/settings\/(general|teams|areas)/;

describe('mod-002 design § 1, mod-016 design § 1 and feat-028 design § 1: the slice’s routes', () => {
  it('are exactly the nineteen of feat-043, one prefix per kind (ADR-0032 § 1)', () => {
    expect(ORGANIZATIONS_ROUTES).toEqual([
      '/organizations',
      '/organizations/deactivated',
      '/organizations/:organizationId/settings/general',
      '/organizations/:organizationId/overview',
      '/organizations/:organizationId/areas',
      '/organizations/:organizationId/projects',
      '/organizations/:organizationId/teams',
      '/organizations/:organizationId/members',
      '/organizations/:organizationId/areas/:areaId/overview',
      '/organizations/:organizationId/areas/:areaId/projects',
      '/organizations/:organizationId/areas/:areaId/teams',
      '/organizations/:organizationId/areas/:areaId/settings',
      '/organizations/:organizationId/teams/:teamId/overview',
      '/organizations/:organizationId/teams/:teamId/projects',
      '/organizations/:organizationId/teams/:teamId/members',
      '/organizations/:organizationId/teams/:teamId/settings',
      '/organizations/:organizationId/projects/:projectId/general',
      '/organizations/:organizationId/projects/:projectId/teams',
      '/organizations/:organizationId/projects/:projectId/settings',
    ]);
  });

  it('keeps no retired address among them (BR-NV-6)', () => {
    expect(ORGANIZATIONS_ROUTES.filter((route) => RETIRED_SHAPE.test(route))).toEqual([]);
  });

  it('builds each address with every id re-encoded as received', () => {
    expect(organizationGeneralPath('o-1')).toBe('/organizations/o-1/settings/general');
    expect(projectsPath('a/b')).toBe('/organizations/a%2Fb/projects');
    expect(deactivatedProjectsPath('o')).toBe('/organizations/o/projects?status=deactivated');
    expect(membersPath('o #1')).toBe('/organizations/o%20%231/members');
    expect(projectGeneralPath('o', 'p q')).toBe('/organizations/o/projects/p%20q/general');
    expect(teamsPath('o', 'p')).toBe('/organizations/o/projects/p/teams');
    expect(organizationTeamsPath('o 1')).toBe('/organizations/o%201/teams');
    expect(organizationTeamPath('o', 't?')).toBe('/organizations/o/teams/t%3F/overview');
    expect(organizationAreasPath('o')).toBe('/organizations/o/areas');
    expect(organizationAreaPath('o', 'a b')).toBe('/organizations/o/areas/a%20b/overview');
  });
});

describe('mod-002 US-1 Scenario: No screen grants the right to create organizations', () => {
  it('has no route about rights, roles or groups, and reads the create right from the token', () => {
    // «Участники» (mod-016) is the one route that names the people of an
    // organization; it is a list of memberships, not a screen of rights.
    for (const route of ORGANIZATIONS_ROUTES.filter(
      (candidate) => candidate !== MEMBERS_ROUTE && !candidate.endsWith('/teams/:teamId/members'),
    )) {
      expect(route).not.toMatch(/right|role|group|member|permission|access/i);
    }
    expect(MEMBERS_ROUTE).not.toMatch(/right|role|group|permission|access/i);
    // mod-016: the twelve organization codes of api-contract § 0.2 are served per
    // organization (`GET …/access`), never in the token, and feat-028 adds four (its § 0.1);
    // the create right and feat-033's rescue right (ADR-0035) are the slice's instance codes.
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
      'organizations.area.create',
      'organizations.area.link',
      'organizations.organization.create',
      'organizations.organization.rescue',
    ]);
  });
});
