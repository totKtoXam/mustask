import { describe, expect, it } from 'vitest';
import {
  ORGANIZATIONS_ROUTES,
  areasPath,
  deactivatedProjectsPath,
  organizationGeneralPath,
  projectGeneralPath,
  projectsPath,
  teamPath,
  teamsPath,
} from './paths';
import { organizationsPermissions } from './permissions';

describe('mod-002 design § 1: the slice’s routes', () => {
  it('are exactly the nine of design § 1, under mod-015 D-17’s prefixes', () => {
    expect(ORGANIZATIONS_ROUTES).toEqual([
      '/organizations',
      '/organizations/deactivated',
      '/organizations/:organizationId/settings/general',
      '/organizations/:organizationId/settings/projects',
      '/organizations/:organizationId/settings/projects/deactivated',
      '/organizations/:organizationId/projects/:projectId/settings/general',
      '/organizations/:organizationId/projects/:projectId/settings/teams',
      '/organizations/:organizationId/projects/:projectId/settings/teams/:teamId',
      '/organizations/:organizationId/projects/:projectId/settings/areas',
    ]);
  });

  it('builds each address with every id re-encoded as received', () => {
    expect(organizationGeneralPath('o-1')).toBe('/organizations/o-1/settings/general');
    expect(projectsPath('a/b')).toBe('/organizations/a%2Fb/settings/projects');
    expect(deactivatedProjectsPath('o')).toBe('/organizations/o/settings/projects/deactivated');
    expect(projectGeneralPath('o', 'p q')).toBe('/organizations/o/projects/p%20q/settings/general');
    expect(teamsPath('o', 'p')).toBe('/organizations/o/projects/p/settings/teams');
    expect(teamPath('o', 'p', 't?')).toBe('/organizations/o/projects/p/settings/teams/t%3F');
    expect(areasPath('o', 'p')).toBe('/organizations/o/projects/p/settings/areas');
  });
});

describe('mod-002 US-1 Scenario: No screen grants the right', () => {
  it('has no route and no code about rights, roles or groups beyond the create right it reads', () => {
    for (const route of ORGANIZATIONS_ROUTES) {
      expect(route).not.toMatch(/right|role|group|member|permission|access/i);
    }
    expect(Object.values(organizationsPermissions)).toEqual([
      'organizations.organization.read',
      'organizations.project.read',
      'organizations.team.read',
      'organizations.area.read',
      'organizations.organization.create',
    ]);
  });
});
