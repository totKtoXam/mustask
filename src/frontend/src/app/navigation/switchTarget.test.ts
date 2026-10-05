import { describe, expect, it } from 'vitest';
import HomeOutlinedIcon from '@mui/icons-material/HomeOutlined';
import type { AccessCheck } from '../../shared/lib/access';
import type { WorkContextSelection } from '../../shared/lib/workContext';
import { switchTarget } from './switchTarget';
import type { NavigationModel } from './types';

/** The settings entries of mod-002 design § 2.1 and mod-001's «Процесс», as the model holds them. */
const model: NavigationModel = {
  top: [
    {
      id: 'home',
      label: 'Главная',
      icon: HomeOutlinedIcon,
      scope: 'instance',
      landing: () => '/home',
    },
  ],
  work: [],
  settings: [
    {
      id: 'organization',
      label: 'Организация',
      entries: [
        {
          id: 'organization-general',
          label: 'Общие',
          scope: 'organization',
          landing: (s) =>
            s.organizationId ? `/organizations/${s.organizationId}/settings/general` : null,
        },
        {
          id: 'projects',
          label: 'Проекты',
          scope: 'organization',
          landing: (s) =>
            s.organizationId ? `/organizations/${s.organizationId}/settings/projects` : null,
        },
        {
          id: 'process',
          label: 'Процесс',
          scope: 'organization',
          landing: (s) =>
            s.organizationId ? `/organizations/${s.organizationId}/settings/process` : null,
        },
      ],
    },
    {
      id: 'project',
      label: 'Проект',
      entries: ['general', 'teams', 'areas'].map((page) => ({
        id: `project-${page}`,
        label: page,
        scope: 'project' as const,
        landing: (s: WorkContextSelection) =>
          s.organizationId && s.projectId
            ? `/organizations/${s.organizationId}/projects/${s.projectId}/settings/${page}`
            : null,
      })),
    },
    {
      id: 'dictionaries',
      label: 'Справочники',
      entries: [
        {
          id: 'languages',
          label: 'Языки',
          scope: 'instance',
          landing: () => '/dictionaries/languages',
        },
      ],
    },
  ],
};

const signedIn: AccessCheck = () => true;

/** «Альфа» (o1) with «Пример проекта» (p1) and «Аналитика» (p2); «Бета» (o2) with «Отчётность» (p3). */
const alfaPrm = { organizationId: 'o1', projectId: 'p1', teamId: 't1' };
const alfaAnl = { organizationId: 'o1', projectId: 'p2', teamId: 't2' };
const betaRep = { organizationId: 'o2', projectId: 'p3', teamId: 't3' };
const betaNone = { organizationId: 'o2' };

function target(pathname: string, carried: WorkContextSelection, next: WorkContextSelection) {
  return switchTarget(model, signedIn, { pathname, carried, next });
}

describe('mod-002 US-8 Scenario: On an organization-level page the lower levels do not move it — the resolver', () => {
  it.each([
    ['/organizations/o1/settings/general', '/organizations/o2/settings/general'],
    ['/organizations/o1/settings/projects', '/organizations/o2/settings/projects'],
    ['/organizations/o1/settings/projects/deactivated', '/organizations/o2/settings/projects'],
    ['/organizations/o1/settings/process', '/organizations/o2/settings/process'],
  ])('an organization chosen on %s opens %s', (pathname, expected) => {
    expect(target(pathname, { organizationId: 'o1' }, betaRep)).toBe(expected);
  });
});

describe('mod-002 US-8 Scenario: A project switch on a project-level page — the resolver', () => {
  it.each([['general'], ['teams'], ['areas']])(
    'on project «%s», a project choice opens the same entry for the new project',
    (page) => {
      const pathname = `/organizations/o1/projects/p1/settings/${page}`;
      expect(target(pathname, alfaPrm, alfaAnl)).toBe(
        `/organizations/o1/projects/p2/settings/${page}`,
      );
    },
  );

  it.each([['general'], ['teams'], ['areas']])(
    'on project «%s», an organization choice opens the same entry for its first project, or «Главная» without one',
    (page) => {
      const pathname = `/organizations/o1/projects/p1/settings/${page}`;
      expect(target(pathname, alfaPrm, betaRep)).toBe(
        `/organizations/o2/projects/p3/settings/${page}`,
      );
      expect(target(pathname, alfaPrm, betaNone)).toBe('/home');
    },
  );
});

describe('mod-002 US-8 Scenario: A page addressing one team lands on its entry’s page — the resolver', () => {
  const teamPage = '/organizations/o1/projects/p1/settings/teams/t9';

  it('a project choice opens «Команды» of the new project', () => {
    expect(target(teamPage, { organizationId: 'o1', projectId: 'p1' }, alfaAnl)).toBe(
      '/organizations/o1/projects/p2/settings/teams',
    );
  });

  it('an organization choice opens «Команды» of its first project, or «Главная» without one', () => {
    const carried = { organizationId: 'o1', projectId: 'p1' };
    expect(target(teamPage, carried, betaRep)).toBe('/organizations/o2/projects/p3/settings/teams');
    expect(target(teamPage, carried, betaNone)).toBe('/home');
  });
});

describe('mod-002 design § 2.2: a not-found frame switches as the page whose address it answers', () => {
  it('matches the entry by the address’s own ids, not the switcher’s selection', () => {
    // «Проект не найден» on a project of another organization: the switcher
    // still holds «Бета», the address carries «Альфа» and an unknown project.
    const pathname = '/organizations/o1/projects/gone/settings/areas';

    expect(target(pathname, { organizationId: 'o1', projectId: 'gone' }, alfaAnl)).toBe(
      '/organizations/o1/projects/p2/settings/areas',
    );
  });

  it('opens «Главная» for an address beneath no entry, and when the entry is not visible', () => {
    expect(target('/organizations/o1/projects/p1/unknown', alfaPrm, alfaAnl)).toBe('/home');
    const refused: AccessCheck = (_code, scope) => scope === 'instance';
    expect(
      switchTarget(
        {
          ...model,
          settings: [
            {
              id: 'organization',
              label: 'Организация',
              entries: [{ ...model.settings[0]!.entries[0]!, permission: 'x.read' }],
            },
          ],
        },
        refused,
        {
          pathname: '/organizations/o1/settings/general',
          carried: { organizationId: 'o1' },
          next: betaRep,
        },
      ),
    ).toBe('/home');
  });
});
