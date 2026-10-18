import { describe, expect, it } from 'vitest';
import type { WorkContextSelection } from '../../../shared/lib/workContext';
import {
  LOADING_LIST,
  addressSync,
  organizationTeamLevel,
  resolveSelection,
  type LevelList,
} from './resolveSelection';

function ready(...names: string[]): LevelList {
  return { status: 'ready', items: names.map((name) => ({ id: name, name })) };
}

const FAILED: LevelList = { status: 'failed', items: [] };

/** «Альфа» (Аналитика, Пример проекта), «Бета» (Отчётность); the organization's teams, the first first. */
const PROJECTS: Record<string, LevelList> = {
  Альфа: ready('Аналитика', 'Пример проекта'),
  Бета: ready('Отчётность'),
};
const TEAMS: Record<string, LevelList> = {
  Альфа: ready('Аналитика', 'Платформа', 'Пример проекта'),
  Бета: ready('Отчётность'),
};

function resolve(
  chosen: WorkContextSelection,
  overrides: {
    organizations?: LevelList;
    projects?: Record<string, LevelList>;
    teams?: Record<string, LevelList>;
  } = {},
) {
  const projects = overrides.projects ?? PROJECTS;
  const teams = overrides.teams ?? TEAMS;
  return resolveSelection({
    chosen,
    organizations: overrides.organizations ?? ready('Альфа', 'Бета'),
    projectsOf: (id) => projects[id] ?? ready(),
    teamsOf: (id) => teams[id] ?? ready(),
  });
}

describe('mod-002 US-8: the selection without an address (design § 3.4)', () => {
  it('a first visit takes the first entries and announces nothing', () => {
    const resolution = resolve({});

    expect(resolution.selection).toEqual({
      organizationId: 'Альфа',
      projectId: 'Аналитика',
      teamId: 'Аналитика',
    });
    expect(resolution.settled).toBe(true);
    expect(resolution.replacement).toBeNull();
    expect(resolution.chosenTeamId).toBeUndefined();
  });

  it('a remembered selection the lists hold is kept, its team being a chosen one', () => {
    const remembered = {
      organizationId: 'Альфа',
      projectId: 'Пример проекта',
      teamId: 'Платформа',
    };

    expect(resolve(remembered).selection).toEqual(remembered);
    expect(resolve(remembered).replacement).toBeNull();
    expect(resolve(remembered).chosenTeamId).toBe('Платформа');
  });
});

describe('feat-040 BR-TS-10: the team level is the organization’s, whatever its projects', () => {
  it('a project choice leaves the team level as it is: a chosen team stays', () => {
    const chosen = { organizationId: 'Альфа', projectId: 'Пример проекта', teamId: 'Платформа' };

    expect(resolve(chosen).selection.teamId).toBe('Платформа');
    expect(resolve({ ...chosen, projectId: 'Аналитика' }).selection.teamId).toBe('Платформа');
  });

  it('an organization with no project still has its team level', () => {
    const resolution = resolve(
      { organizationId: 'Бета' },
      { projects: { Бета: ready() }, teams: { Бета: ready('Отчётность') } },
    );

    expect(resolution.selection).toEqual({ organizationId: 'Бета', teamId: 'Отчётность' });
    expect(resolution.levelStatus).toEqual({ project: 'ready', team: 'ready' });
  });

  it('an organization with no team has none, whatever its projects', () => {
    const resolution = resolve({ organizationId: 'Бета' }, { teams: { Бета: ready() } });

    expect(resolution.selection).toEqual({ organizationId: 'Бета', projectId: 'Отчётность' });
    expect(resolution.settled).toBe(true);
    expect(resolution.replacement).toBeNull();
  });

  it('the team level is read apart from the project level: one failing does not hold the other back', () => {
    const projectsFailed = resolve({ organizationId: 'Альфа' }, { projects: { Альфа: FAILED } });
    expect(projectsFailed.selection).toEqual({ organizationId: 'Альфа', teamId: 'Аналитика' });
    expect(projectsFailed.levelStatus).toEqual({ project: 'failed', team: 'ready' });

    const teamsFailed = resolve({ organizationId: 'Альфа' }, { teams: { Альфа: FAILED } });
    expect(teamsFailed.selection).toEqual({ organizationId: 'Альфа', projectId: 'Аналитика' });
    expect(teamsFailed.levelStatus).toEqual({ project: 'ready', team: 'failed' });
  });
});

/** A team list in the organization's order, the named teams being the user's own (api-contract § 4.1). */
function teamsOf(names: string[], own: string[], defaultTeam?: string): LevelList {
  return {
    status: 'ready',
    items: organizationTeamLevel(
      names.map((name) => ({ id: name, name, isMember: own.includes(name) })),
      defaultTeam,
    ),
  };
}

describe('feat-040 BR-TS-10: the level’s order and the fallback (mod-020, mod-016 D-5)', () => {
  const organization = { organizationId: 'Альфа', projectId: 'Пример проекта' } as const;
  const resolveTeam = (chosen: WorkContextSelection, teams: LevelList) =>
    resolve(chosen, { teams: { Альфа: teams } });

  it('lists the user’s own teams first, each group in the server’s order', () => {
    const level = organizationTeamLevel(
      [
        { id: 'a', name: 'JUPITER', isMember: false },
        { id: 'b', name: 'MARS', isMember: false },
        { id: 'c', name: 'VENUS', isMember: true },
        { id: 'd', name: 'URANUS', isMember: true },
      ],
      undefined,
    );

    expect(level.map((team) => team.name)).toEqual(['VENUS', 'URANUS', 'JUPITER', 'MARS']);
  });

  it('marks the project’s default team only', () => {
    const level = organizationTeamLevel(
      [
        { id: 'a', name: 'JUPITER', isMember: false },
        { id: 'b', name: 'MARS', isMember: false },
      ],
      'b',
    );

    expect(level.map((team) => team.isDefault)).toEqual([false, true]);
  });

  it('a user in one team gets it, not the level’s first', () => {
    expect(
      resolveTeam(organization, teamsOf(['Мобильная', 'Платформа', 'Тест'], ['Платформа']))
        .selection.teamId,
    ).toBe('Платформа');
  });

  it('a user in several gets the first of them', () => {
    expect(resolveTeam(organization, teamsOf(['А', 'Б', 'В'], ['Б', 'В'])).selection.teamId).toBe(
      'Б',
    );
  });

  it('a user in none gets the level’s first team', () => {
    expect(resolveTeam(organization, teamsOf(['Б', 'А'], [])).selection.teamId).toBe('Б');
  });

  it('the project’s default team comes before the user’s own, and is a fallback, not a choice', () => {
    const resolution = resolveTeam(organization, teamsOf(['А', 'Б', 'В'], ['В'], 'Б'));

    expect(resolution.selection.teamId).toBe('Б');
    expect(resolution.chosenTeamId).toBeUndefined();
  });

  it('a chosen team beats the default and stays chosen', () => {
    const resolution = resolveTeam(
      { ...organization, teamId: 'В' },
      teamsOf(['А', 'Б', 'В'], [], 'Б'),
    );

    expect(resolution.selection.teamId).toBe('В');
    expect(resolution.chosenTeamId).toBe('В');
    expect(resolution.replacement).toBeNull();
  });

  it('a chosen team that is gone falls back and is named in the notice; the fallback is not remembered', () => {
    const teams = teamsOf(['Пример проекта', 'Платформа'], ['Платформа']);
    const resolution = resolveTeam({ ...organization, teamId: 'Удалённая' }, teams);

    expect(resolution.replacement).toEqual({
      selection: { ...organization, teamId: undefined },
      names: ['Альфа', 'Пример проекта', 'Платформа'],
    });
  });

  it('the levels above carry no membership and keep their first entries', () => {
    expect(resolve({}).selection).toMatchObject({
      organizationId: 'Альфа',
      projectId: 'Аналитика',
    });
  });
});

describe('mod-002 US-8 Scenario: A remembered selection that is gone falls back', () => {
  it('a deleted team: the level’s default, announced with every level; it is no choice', () => {
    const resolution = resolve({
      organizationId: 'Альфа',
      projectId: 'Пример проекта',
      teamId: 'Удалённая',
    });

    expect(resolution.replacement).toEqual({
      selection: { organizationId: 'Альфа', projectId: 'Пример проекта', teamId: undefined },
      names: ['Альфа', 'Пример проекта', 'Аналитика'],
    });
  });

  it('a deactivated project: «Аналитика», the team a chosen one stays', () => {
    const resolution = resolve({
      organizationId: 'Альфа',
      projectId: 'Старый',
      teamId: 'Платформа',
    });

    expect(resolution.replacement?.names).toEqual(['Альфа', 'Аналитика', 'Платформа']);
    expect(resolution.replacement?.selection).toEqual({
      organizationId: 'Альфа',
      projectId: 'Аналитика',
      teamId: 'Платформа',
    });
  });

  it('a deactivated organization: «Бета», its first project and its first team', () => {
    const resolution = resolve(
      { organizationId: 'Альфа', projectId: 'Пример проекта', teamId: 'Платформа' },
      { organizations: ready('Бета') },
    );

    expect(resolution.replacement?.names).toEqual(['Бета', 'Отчётность', 'Отчётность']);
  });

  it('leaves out a level with nothing in it: «Выбрано: Бета.»', () => {
    const resolution = resolve(
      { organizationId: 'Альфа', projectId: 'Пример проекта' },
      { organizations: ready('Бета'), projects: { Бета: ready() }, teams: { Бета: ready() } },
    );

    expect(resolution.replacement?.names).toEqual(['Бета']);
    expect(resolution.selection).toEqual({ organizationId: 'Бета' });
  });

  it('raises no notice when the gone entry’s level is left empty', () => {
    expect(resolve({ organizationId: 'Альфа' }, { organizations: ready() }).replacement).toEqual({
      selection: {},
      names: null,
    });
    expect(
      resolve({ organizationId: 'Бета', projectId: 'Отчётность' }, { projects: { Бета: ready() } })
        .replacement,
    ).toEqual({
      selection: { organizationId: 'Бета', projectId: undefined, teamId: undefined },
      names: null,
    });
  });

  it('waits for every level to settle before announcing', () => {
    const resolution = resolve(
      { organizationId: 'Альфа', projectId: 'Старый' },
      { teams: { Альфа: LOADING_LIST } },
    );

    expect(resolution.settled).toBe(false);
    expect(resolution.replacement).toBeNull();
    expect(resolution.levelStatus).toEqual({ project: 'ready', team: 'loading' });
  });

  it('a failed level keeps its remembered id and never falls back: a failure is not a deletion', () => {
    const resolution = resolve(
      { organizationId: 'Альфа', projectId: 'Пример проекта', teamId: 'Платформа' },
      { projects: { Альфа: FAILED }, teams: { Альфа: FAILED } },
    );

    expect(resolution.selection).toEqual({ organizationId: 'Альфа' });
    expect(resolution.levelStatus).toEqual({ project: 'failed', team: 'failed' });
    expect(resolution.replacement).toBeNull();
  });

  it('an organization list that fails sets nothing and keeps everything', () => {
    const resolution = resolve({ organizationId: 'Альфа' }, { organizations: FAILED });

    expect(resolution.status).toBe('failed');
    expect(resolution.selection).toEqual({});
    expect(resolution.replacement).toBeNull();
  });
});

describe('mod-002 US-8 Scenario: The address sets the switcher — the decision', () => {
  const organizations = ready('Альфа', 'Бета');
  const chosen = { organizationId: 'Бета', projectId: 'Отчётность', teamId: 'Отчётность' };

  it('an address with no context sets nothing', () => {
    expect(
      addressSync({ address: {}, chosen, organizations, addressProjects: LOADING_LIST }),
    ).toEqual({ kind: 'none' });
  });

  it('waits while the lists it needs are read', () => {
    expect(
      addressSync({
        address: { organizationId: 'Альфа' },
        chosen,
        organizations: LOADING_LIST,
        addressProjects: LOADING_LIST,
      }),
    ).toEqual({ kind: 'wait' });
    expect(
      addressSync({
        address: { organizationId: 'Альфа', projectId: 'Пример проекта' },
        chosen,
        organizations,
        addressProjects: LOADING_LIST,
      }),
    ).toEqual({ kind: 'wait' });
  });

  it('sets «Альфа» and «Пример проекта» from the settings of «Пример проекта»; another organization’s chosen team does not travel', () => {
    expect(
      addressSync({
        address: { organizationId: 'Альфа', projectId: 'Пример проекта' },
        chosen,
        organizations,
        addressProjects: PROJECTS.Альфа!,
      }),
    ).toEqual({
      kind: 'set',
      selection: { organizationId: 'Альфа', projectId: 'Пример проекта', teamId: undefined },
    });
  });

  it('a project address keeps a team chosen in the same organization (feat-040 BR-TS-10)', () => {
    expect(
      addressSync({
        address: { organizationId: 'Бета', projectId: 'Отчётность' },
        chosen,
        organizations,
        addressProjects: PROJECTS.Бета!,
      }),
    ).toEqual({
      kind: 'set',
      selection: { organizationId: 'Бета', projectId: 'Отчётность', teamId: 'Отчётность' },
    });
  });

  it('keeps the lower levels when they belong to the address’s level', () => {
    expect(
      addressSync({
        address: { organizationId: 'Бета' },
        chosen,
        organizations,
        addressProjects: LOADING_LIST,
      }),
    ).toEqual({ kind: 'set', selection: chosen });
  });

  it('sets nothing for an organization or project that is not found (deactivated, unknown, another organization’s)', () => {
    expect(
      addressSync({
        address: { organizationId: 'Гамма' },
        chosen,
        organizations,
        addressProjects: LOADING_LIST,
      }),
    ).toEqual({ kind: 'none' });
    expect(
      addressSync({
        address: { organizationId: 'Альфа', projectId: 'Отчётность' },
        chosen,
        organizations,
        addressProjects: PROJECTS.Альфа!,
      }),
    ).toEqual({ kind: 'none' });
  });
});

describe('feat-040 BR-TS-1, BR-TS-10: a team address sets the organization and the team — the decision', () => {
  const organizations = ready('Альфа', 'Бета');
  const chosen = { organizationId: 'Бета', projectId: 'Отчётность', teamId: 'Отчётность' };
  const address = { organizationId: 'Альфа', teamId: 'Платформа' };

  it('waits for the organization’s teams, then sets the organization and the team', () => {
    expect(
      addressSync({
        address,
        chosen,
        organizations,
        addressProjects: LOADING_LIST,
        addressTeams: LOADING_LIST,
      }),
    ).toEqual({ kind: 'wait' });
    expect(
      addressSync({
        address,
        chosen,
        organizations,
        addressProjects: LOADING_LIST,
        addressTeams: TEAMS.Альфа!,
      }),
    ).toEqual({
      kind: 'set',
      selection: { organizationId: 'Альфа', projectId: undefined, teamId: 'Платформа' },
    });
  });

  it('leaves the project level at its selection within the same organization', () => {
    expect(
      addressSync({
        address: { organizationId: 'Бета', teamId: 'Отчётность' },
        chosen: { organizationId: 'Бета', projectId: 'Отчётность' },
        organizations,
        addressProjects: LOADING_LIST,
        addressTeams: TEAMS.Бета!,
      }),
    ).toEqual({
      kind: 'set',
      selection: { organizationId: 'Бета', projectId: 'Отчётность', teamId: 'Отчётность' },
    });
  });

  it('sets nothing for a team the organization’s active teams do not hold (deleted, another organization’s)', () => {
    expect(
      addressSync({
        address: { ...address, teamId: 'Отчётность' },
        chosen,
        organizations,
        addressProjects: LOADING_LIST,
        addressTeams: TEAMS.Альфа!,
      }),
    ).toEqual({ kind: 'none' });
  });
});
