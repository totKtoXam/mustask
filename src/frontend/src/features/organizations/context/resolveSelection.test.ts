import { describe, expect, it } from 'vitest';
import type { WorkContextSelection } from '../../../shared/lib/workContext';
import { LOADING_LIST, addressSync, resolveSelection, type LevelList } from './resolveSelection';

function ready(...names: string[]): LevelList {
  return { status: 'ready', items: names.map((name) => ({ id: name, name })) };
}

const FAILED: LevelList = { status: 'failed', items: [] };

/** «Альфа» (Аналитика, Пример проекта), «Бета» (Отчётность); teams per project, the first team first. */
const PROJECTS: Record<string, LevelList> = {
  Альфа: ready('Аналитика', 'Пример проекта'),
  Бета: ready('Отчётность'),
};
const TEAMS: Record<string, LevelList> = {
  Аналитика: ready('Аналитика'),
  'Пример проекта': ready('Пример проекта', 'Платформа'),
  Отчётность: ready('Отчётность'),
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
    teamsOf: (_organization, id) => teams[id] ?? ready(),
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
  });

  it('a remembered selection the lists hold is kept', () => {
    const remembered = {
      organizationId: 'Альфа',
      projectId: 'Пример проекта',
      teamId: 'Платформа',
    };

    expect(resolve(remembered).selection).toEqual(remembered);
    expect(resolve(remembered).replacement).toBeNull();
  });
});

describe('mod-002 US-8 Scenario: The default team is the first team', () => {
  it('a project chosen without a team selects its first team', () => {
    expect(resolve({ organizationId: 'Альфа', projectId: 'Пример проекта' }).selection.teamId).toBe(
      'Пример проекта',
    );
  });
});

describe('mod-002 US-8 Scenario: A remembered selection that is gone falls back', () => {
  it('a deleted team: the project’s first team, announced with every level', () => {
    const resolution = resolve({
      organizationId: 'Альфа',
      projectId: 'Пример проекта',
      teamId: 'Удалённая',
    });

    expect(resolution.replacement).toEqual({
      selection: { organizationId: 'Альфа', projectId: 'Пример проекта', teamId: 'Пример проекта' },
      names: ['Альфа', 'Пример проекта', 'Пример проекта'],
    });
  });

  it('a deactivated project: «Аналитика» and its first team', () => {
    const resolution = resolve({ organizationId: 'Альфа', projectId: 'Старый', teamId: 'Старый' });

    expect(resolution.replacement?.names).toEqual(['Альфа', 'Аналитика', 'Аналитика']);
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
      { organizations: ready('Бета'), projects: { Бета: ready() } },
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
    ).toEqual({ selection: { organizationId: 'Бета' }, names: null });
  });

  it('waits for every level below to settle before announcing', () => {
    const resolution = resolve(
      { organizationId: 'Альфа', projectId: 'Старый' },
      { teams: { Аналитика: LOADING_LIST } },
    );

    expect(resolution.settled).toBe(false);
    expect(resolution.replacement).toBeNull();
    expect(resolution.levelStatus).toEqual({ project: 'ready', team: 'loading' });
  });

  it('a failed level keeps its remembered id and never falls back: a failure is not a deletion', () => {
    const resolution = resolve(
      { organizationId: 'Альфа', projectId: 'Пример проекта', teamId: 'Платформа' },
      { projects: { Альфа: FAILED } },
    );

    expect(resolution.selection).toEqual({ organizationId: 'Альфа' });
    expect(resolution.levelStatus.project).toBe('failed');
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

  it('sets «Альфа» and «Пример проекта» from the settings of «Пример проекта», its first team below', () => {
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
