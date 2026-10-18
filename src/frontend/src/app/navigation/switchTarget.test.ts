import { describe, expect, it } from 'vitest';
import HomeOutlinedIcon from '@mui/icons-material/HomeOutlined';
import type { AccessCheck } from '../../shared/lib/access';
import type { WorkContextSelection } from '../../shared/lib/workContext';
import { navigationModel } from './model';
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
          id: 'organization-teams',
          label: 'Команды',
          scope: 'organization',
          landing: (s) =>
            s.organizationId ? `/organizations/${s.organizationId}/settings/teams` : null,
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

// feat-028 BR-OL-12 (doc-012 P-1): the retired tests «a project choice opens «Команды» of the new
// project» and «an organization choice opens «Команды» of its first project, or «Главная» without
// one» described the project-nested team page `…/projects/:p/settings/teams/:t`, which is no page any
// more; the team's page is the organization's, and these are its twins.
describe('feat-028 Scenario: A page addressing one team lands on its entry’s page — the resolver', () => {
  const teamPage = '/organizations/o1/settings/teams/t9';

  it('the organization’s team page is beneath «Команды» of the organization, whatever project is chosen', () => {
    expect(target(teamPage, { organizationId: 'o1' }, alfaAnl)).toBe(
      '/organizations/o1/settings/teams',
    );
  });

  it('an organization choice opens «Команды» of that organization, with a project or without one', () => {
    const carried = { organizationId: 'o1' };
    expect(target(teamPage, carried, betaRep)).toBe('/organizations/o2/settings/teams');
    expect(target(teamPage, carried, betaNone)).toBe('/organizations/o2/settings/teams');
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

describe('mod-006 US-1 Scenario: Switching the team keeps the entry, not the level — the resolver', () => {
  const backlogs = navigationModel.work.find((entry) => entry.id === 'backlogs');
  const withBacklogs: NavigationModel = { ...model, work: backlogs ? [backlogs] : [] };
  const alfaPrmOtherTeam = { organizationId: 'o1', projectId: 'p1', teamId: 't9' };

  it.each([['epics'], ['features'], ['stories']])(
    'a team chosen on the «%s» backlog opens the new team’s «Истории» backlog',
    (level) => {
      expect(
        switchTarget(withBacklogs, signedIn, {
          pathname: `/organizations/o1/projects/p1/teams/t1/backlogs/${level}`,
          carried: alfaPrm,
          next: alfaPrmOtherTeam,
        }),
      ).toBe('/organizations/o1/projects/p1/teams/t9/backlogs/stories');
    },
  );
});

describe('mod-015 BR-18 on a team-level page: a project or organization choice keeps the entry — the resolver', () => {
  // What the source's `select` hands the resolver on a team-level address
  // (`useOrganizationsContextSource.teamLevel.test.tsx` pins it): the choice
  // with the first entries of the levels below it, read first — a project's
  // first team, an organization's first project and its first team (mod-002
  // design § 2.2, § 3.5). A level with no entry stays unset.
  const backlogs = navigationModel.work.find((entry) => entry.id === 'backlogs');
  const boards = navigationModel.work.find((entry) => entry.id === 'boards');
  const withTeamEntries: NavigationModel = {
    ...model,
    work: [backlogs, boards].filter((entry) => entry !== undefined),
  };
  const anlChosen = { organizationId: 'o1', projectId: 'p2', teamId: 't2' };
  const betaChosen = { organizationId: 'o2', projectId: 'p3', teamId: 't3' };

  function from(pathname: string, next: WorkContextSelection) {
    return switchTarget(withTeamEntries, signedIn, { pathname, carried: alfaPrm, next });
  }

  it.each([['backlogs'], ['boards']])(
    'on «%s», a project chosen, with its first team read, opens that team’s «Истории» landing',
    (surface) => {
      expect(from(`/organizations/o1/projects/p1/teams/t1/${surface}/features`, anlChosen)).toBe(
        `/organizations/o1/projects/p2/teams/t2/${surface}/stories`,
      );
    },
  );

  it.each([['backlogs'], ['boards']])(
    'on «%s», an organization chosen, with its first project and team read, opens that team’s «Истории» landing',
    (surface) => {
      expect(from(`/organizations/o1/projects/p1/teams/t1/${surface}/epics`, betaChosen)).toBe(
        `/organizations/o2/projects/p3/teams/t3/${surface}/stories`,
      );
    },
  );

  it('opens «Главная» when the choice names no team: the entry is not visible there', () => {
    const backlog = '/organizations/o1/projects/p1/teams/t1/backlogs/features';
    // A project whose teams list came back empty, and a bare project choice
    // with no team read — why the source reads the first team first (review F1).
    expect(from(backlog, { organizationId: 'o1', projectId: 'p2', teamId: undefined })).toBe(
      '/home',
    );
    expect(from(backlog, { organizationId: 'o1', projectId: 'p2' })).toBe('/home');
    // An organization with no project.
    expect(from(backlog, { organizationId: 'o2', projectId: undefined })).toBe('/home');
  });
});

describe('mod-016 design § 2.5, DC-8: a switch does not wait for the codes of the organization it opens', () => {
  const members = {
    id: 'members',
    label: 'Участники',
    scope: 'organization' as const,
    permission: 'organizations.membership.read',
    landing: (s: WorkContextSelection) =>
      s.organizationId ? `/organizations/${s.organizationId}/settings/members` : null,
  };
  const withMembers: NavigationModel = {
    ...model,
    settings: [
      { id: 'organization', label: 'Организация', entries: [members] },
      ...model.settings.slice(1),
    ],
  };
  const fromMembers = '/organizations/o1/settings/members';
  const noOrganizationCodes: AccessCheck = (_code, scope) => scope === 'instance';

  function open(access: AccessCheck, isCodesRead: (organizationId: string | undefined) => boolean) {
    return switchTarget(
      withMembers,
      access,
      { pathname: fromMembers, carried: { organizationId: 'o1' }, next: betaRep },
      isCodesRead,
    );
  }

  it('opens «Участники» of B from «Участники» of A while B’s codes are unread', () => {
    expect(open(noOrganizationCodes, () => false)).toBe('/organizations/o2/settings/members');
  });

  it('asks the code once the codes are read: a read B without it opens «Главная»', () => {
    expect(open(noOrganizationCodes, () => true)).toBe('/home');
  });

  it('opens the entry of a read B that holds the code', () => {
    expect(
      open(
        () => true,
        () => true,
      ),
    ).toBe('/organizations/o2/settings/members');
  });

  it('asks about the organization it switches to, not the one it leaves', () => {
    const askedAbout: (string | undefined)[] = [];
    const access: AccessCheck = (_code, _scope, organizationId) => {
      askedAbout.push(organizationId);
      return organizationId === 'o2';
    };

    expect(open(access, (organizationId) => organizationId === 'o2')).toBe(
      '/organizations/o2/settings/members',
    );
    expect(askedAbout).toEqual(['o2']);
  });

  it('takes every organization as read when it is given no way to know', () => {
    expect(
      switchTarget(withMembers, noOrganizationCodes, {
        pathname: fromMembers,
        carried: { organizationId: 'o1' },
        next: betaRep,
      }),
    ).toBe('/home');
  });
});
