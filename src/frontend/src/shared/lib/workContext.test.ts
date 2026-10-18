import { describe, expect, it } from 'vitest';
import {
  defaultEntry,
  firstSelection,
  resolveWorkContext,
  selectInWorkContext,
  type WorkContextOrganization,
} from './workContext';

// feat-040 BR-TS-10: the team level is the organization's, whatever its projects.
const organizations: WorkContextOrganization[] = [
  {
    id: 'org-1',
    name: 'Пример организации 1',
    projects: [
      { id: 'prj-1', name: 'Пример проекта 1' },
      { id: 'prj-2', name: 'Пример проекта 2' },
    ],
    teams: [
      { id: 'team-1', name: 'Пример команды 1' },
      { id: 'team-2', name: 'Пример команды 2' },
      { id: 'team-3', name: 'Пример команды 3' },
    ],
  },
  {
    id: 'org-2',
    name: 'Пример организации 2',
    projects: [{ id: 'prj-3', name: 'Пример проекта 3' }],
    teams: [{ id: 'team-4', name: 'Пример команды 4' }],
  },
  { id: 'org-3', name: 'Без проектов', projects: [], teams: [] },
  {
    id: 'org-4',
    name: 'Только команды',
    projects: [],
    teams: [{ id: 'team-5', name: 'Пример команды 5' }],
  },
];

describe('US-5 Scenario: Initial context (BR-11, D-11)', () => {
  it('starts on the first organization, its first project and its first team', () => {
    expect(firstSelection(organizations)).toEqual({
      organizationId: 'org-1',
      projectId: 'prj-1',
      teamId: 'team-1',
    });
  });

  it('has nothing to select without organizations', () => {
    expect(firstSelection([])).toEqual({
      organizationId: undefined,
      projectId: undefined,
      teamId: undefined,
    });
  });
});

describe('US-5 Scenario: Changing a level resets the levels below it — and feat-040 BR-TS-10', () => {
  const initial = firstSelection(organizations);

  it('an organization brings its first project and its default team', () => {
    expect(selectInWorkContext(organizations, initial, 'organization', 'org-2')).toEqual({
      organizationId: 'org-2',
      projectId: 'prj-3',
      teamId: 'team-4',
    });
  });

  it('a project keeps a chosen team and the organization: the team level is the organization’s', () => {
    const chosen = { ...initial, teamId: 'team-3' };
    expect(selectInWorkContext(organizations, chosen, 'project', 'prj-2')).toEqual({
      organizationId: 'org-1',
      projectId: 'prj-2',
      teamId: 'team-3',
    });
  });

  it('a project change with no team chosen takes the level’s default entry', () => {
    expect(
      selectInWorkContext(organizations, { organizationId: 'org-1' }, 'project', 'prj-2'),
    ).toEqual({ organizationId: 'org-1', projectId: 'prj-2', teamId: 'team-1' });
  });

  it('a team changes only the team', () => {
    expect(selectInWorkContext(organizations, initial, 'team', 'team-2')).toEqual({
      organizationId: 'org-1',
      projectId: 'prj-1',
      teamId: 'team-2',
    });
  });

  it('an organization without projects or teams leaves the lower levels empty', () => {
    expect(selectInWorkContext(organizations, initial, 'organization', 'org-3')).toEqual({
      organizationId: 'org-3',
      projectId: undefined,
      teamId: undefined,
    });
  });

  it('an organization without projects still holds its teams (BR-TS-10)', () => {
    expect(selectInWorkContext(organizations, initial, 'organization', 'org-4')).toEqual({
      organizationId: 'org-4',
      projectId: undefined,
      teamId: 'team-5',
    });
  });
});

describe('resolveWorkContext', () => {
  it('falls back to a level’s first entry for an id the lists do not hold', () => {
    const context = resolveWorkContext(organizations, { organizationId: 'gone' });
    expect(context.organization?.id).toBe('org-1');
    expect(context.project?.id).toBe('prj-1');
    expect(context.team?.id).toBe('team-1');
  });
});

describe('defaultEntry (mod-016 D-5, US-7)', () => {
  const teams = [
    { id: 't-1', isMember: false },
    { id: 't-2', isMember: true },
    { id: 't-3', isMember: true },
  ];

  it('is the first entry the user is a member of, in the list’s order', () => {
    expect(defaultEntry(teams)?.id).toBe('t-2');
  });

  it('is the first entry when the user is in none of them or the list does not say', () => {
    expect(defaultEntry([{ id: 'a', isMember: false }, { id: 'b' }])?.id).toBe('a');
    expect(defaultEntry([{ id: 'a' }, { id: 'b' }])?.id).toBe('a');
  });

  it('is nothing for an empty list', () => {
    expect(defaultEntry([])).toBeUndefined();
  });

  it('chooses the team a context opens on, below a chosen project or organization', () => {
    const withMembership: WorkContextOrganization[] = [
      {
        id: 'org-1',
        name: 'Организация',
        projects: [{ id: 'prj-1', name: 'Проект' }],
        teams: [
          { id: 't-1', name: 'Первая', isMember: false },
          { id: 't-2', name: 'Вторая', isMember: true },
        ],
      },
    ];

    expect(firstSelection(withMembership).teamId).toBe('t-2');
    expect(selectInWorkContext(withMembership, {}, 'organization', 'org-1').teamId).toBe('t-2');
    expect(
      selectInWorkContext(withMembership, { organizationId: 'org-1' }, 'project', 'prj-1').teamId,
    ).toBe('t-2');
    // A chosen team and a resolved id that is found are never replaced.
    expect(
      resolveWorkContext(withMembership, {
        organizationId: 'org-1',
        projectId: 'prj-1',
        teamId: 't-1',
      }).team?.id,
    ).toBe('t-1');
  });
});

describe('defaultEntry (mod-020: the project’s default team first)', () => {
  it('is the default entry, before the one the user is a member of', () => {
    const teams = [
      { id: 't-1', isMember: true, isDefault: false },
      { id: 't-2', isMember: false, isDefault: true },
      { id: 't-3', isMember: true, isDefault: false },
    ];

    expect(defaultEntry(teams)?.id).toBe('t-2');
  });

  it('is the user’s own first, then the first, when no entry is the default or the list does not say', () => {
    expect(
      defaultEntry([
        { id: 'a', isMember: false, isDefault: false },
        { id: 'b', isMember: true, isDefault: false },
      ])?.id,
    ).toBe('b');
    expect(defaultEntry([{ id: 'a', isDefault: false }, { id: 'b' }])?.id).toBe('a');
  });

  it('chooses the team a context opens on, below a chosen project or organization', () => {
    const withDefault: WorkContextOrganization[] = [
      {
        id: 'org-1',
        name: 'Организация',
        projects: [{ id: 'prj-1', name: 'Проект' }],
        teams: [
          { id: 't-1', name: 'Первая', isMember: true },
          { id: 't-2', name: 'Вторая', isMember: false, isDefault: true },
        ],
      },
    ];

    expect(firstSelection(withDefault).teamId).toBe('t-2');
    expect(selectInWorkContext(withDefault, {}, 'organization', 'org-1').teamId).toBe('t-2');
    expect(
      selectInWorkContext(withDefault, { organizationId: 'org-1' }, 'project', 'prj-1').teamId,
    ).toBe('t-2');
    // A chosen team that is found is never replaced by the default.
    expect(
      resolveWorkContext(withDefault, {
        organizationId: 'org-1',
        projectId: 'prj-1',
        teamId: 't-1',
      }).team?.id,
    ).toBe('t-1');
  });
});
