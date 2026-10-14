import { describe, expect, it } from 'vitest';
import {
  defaultEntry,
  firstSelection,
  resolveWorkContext,
  selectInWorkContext,
  type WorkContextOrganization,
} from './workContext';

const organizations: WorkContextOrganization[] = [
  {
    id: 'org-1',
    name: 'Пример организации 1',
    projects: [
      {
        id: 'prj-1',
        name: 'Пример проекта 1',
        teams: [
          { id: 'team-1', name: 'Пример команды 1' },
          { id: 'team-2', name: 'Пример команды 2' },
        ],
      },
      {
        id: 'prj-2',
        name: 'Пример проекта 2',
        teams: [{ id: 'team-3', name: 'Пример команды 3' }],
      },
    ],
  },
  {
    id: 'org-2',
    name: 'Пример организации 2',
    projects: [
      {
        id: 'prj-3',
        name: 'Пример проекта 3',
        teams: [{ id: 'team-4', name: 'Пример команды 4' }],
      },
    ],
  },
  { id: 'org-3', name: 'Без проектов', projects: [] },
];

describe('US-5 Scenario: Initial context (BR-11, D-11)', () => {
  it('starts on the first organization, its first project and that project’s first team', () => {
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

describe('US-5 Scenario: Changing a level resets the levels below it', () => {
  const initial = firstSelection(organizations);

  it('an organization brings its first project and team', () => {
    expect(selectInWorkContext(organizations, initial, 'organization', 'org-2')).toEqual({
      organizationId: 'org-2',
      projectId: 'prj-3',
      teamId: 'team-4',
    });
  });

  it('a project brings its first team and keeps the organization', () => {
    expect(selectInWorkContext(organizations, initial, 'project', 'prj-2')).toEqual({
      organizationId: 'org-1',
      projectId: 'prj-2',
      teamId: 'team-3',
    });
  });

  it('a team changes only the team', () => {
    expect(selectInWorkContext(organizations, initial, 'team', 'team-2')).toEqual({
      organizationId: 'org-1',
      projectId: 'prj-1',
      teamId: 'team-2',
    });
  });

  it('an organization without projects leaves the lower levels empty', () => {
    expect(selectInWorkContext(organizations, initial, 'organization', 'org-3')).toEqual({
      organizationId: 'org-3',
      projectId: undefined,
      teamId: undefined,
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
        projects: [
          {
            id: 'prj-1',
            name: 'Проект',
            teams: [
              { id: 't-1', name: 'Первая', isMember: false },
              { id: 't-2', name: 'Вторая', isMember: true },
            ],
          },
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
