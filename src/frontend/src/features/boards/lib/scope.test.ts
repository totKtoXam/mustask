import { describe, expect, it } from 'vitest';
import { backlogKey, boardKey, boardsKeys } from '../api/queryKeys';
import {
  creationSurface,
  filterIdsOf,
  projectScope,
  readName,
  readProjects,
  scopeOwnerId,
  scopeProjectId,
  scopeTeamId,
  startTeamOf,
  teamScope,
  type SurfaceRead,
} from './scope';

// feat-039: what a backlog or a board is of, and the keys and reads that follow from it;
// feat-040: a team's scope names the team alone, and its views carry the project filter.

const level = { code: 'stories', name: 'Истории', types: [] };
const team = { id: 't1', name: 'Платформа' };
const alpha = { id: 'p1', name: 'Альфа' };
const teamBacklog: SurfaceRead = { team, projects: [alpha], level, items: [] };
const projectBacklog: SurfaceRead = { project: alpha, level, items: [] };
const teamBoard: SurfaceRead = { team, projects: [alpha], level, columns: [] };
const projectBoard: SurfaceRead = { project: alpha, level, columns: [] };

describe('the scope', () => {
  it('is a team’s, which names its id and no project, or the project’s, which names its id and no team', () => {
    expect(scopeTeamId(teamScope('t1'))).toBe('t1');
    expect(scopeProjectId(teamScope('t1'))).toBeNull();
    expect(scopeTeamId(projectScope('p1'))).toBeNull();
    expect(scopeProjectId(projectScope('p1'))).toBe('p1');
    expect(scopeOwnerId(teamScope('t1'))).toBe('t1');
    expect(scopeOwnerId(projectScope('p1'))).toBe('p1');
    expect(projectScope('p1')).toEqual({ kind: 'project', projectId: 'p1' });
  });
});

describe('the filter a scope carries (feat-040 BR-TS-8)', () => {
  const address = { teamIds: ['t9', 'none'], projectIds: ['p9'] };

  it('is the project filter on a team’s views and the team filter on the project’s', () => {
    expect(filterIdsOf(teamScope('t1'), address)).toEqual(['p9']);
    expect(filterIdsOf(projectScope('p1'), address)).toEqual(['t9', 'none']);
  });

  it('is empty when the address carries only the other dimension', () => {
    expect(filterIdsOf(teamScope('t1'), { teamIds: ['t9'], projectIds: [] })).toEqual([]);
    expect(filterIdsOf(projectScope('p1'), { teamIds: [], projectIds: ['p9'] })).toEqual([]);
  });
});

describe('the query keys under a scope', () => {
  it('are a team’s own keys for a team, with no project in them, and the project’s own for the project', () => {
    expect(backlogKey('o1', teamScope('t1'), 'epics')).toEqual(
      boardsKeys.backlog('o1', 't1', 'epics'),
    );
    expect(boardKey('o1', teamScope('t1'), 'epics')).toEqual(boardsKeys.board('o1', 't1', 'epics'));
    expect(backlogKey('o1', projectScope('p1'), 'epics')).toEqual(
      boardsKeys.projectBacklog('o1', 'p1', 'epics'),
    );
    expect(boardKey('o1', projectScope('p1'), 'epics')).toEqual(
      boardsKeys.projectBoard('o1', 'p1', 'epics'),
    );
  });

  it('carry the sorted filter, so a filter in another order is the same read', () => {
    expect(backlogKey('o1', teamScope('t1'), 'stories', ['b', 'a'])).toEqual(
      backlogKey('o1', teamScope('t1'), 'stories', ['a', 'b']),
    );
    expect(backlogKey('o1', teamScope('t1'), 'stories', ['a'])).not.toEqual(
      backlogKey('o1', teamScope('t1'), 'stories'),
    );
  });

  it('keep the level at one place for both scopes, so one filter’s rows stay while another is read', () => {
    const team = backlogKey('o1', teamScope('t1'), 'features', ['a']);
    const project = backlogKey('o1', projectScope('p1'), 'features', ['a']);

    expect(team[4]).toBe('features');
    expect(project[4]).toBe('features');
  });

  it('never fall under the other scope’s prefix: a read-again of one reaches none of the other’s', () => {
    const startsWith = (key: readonly unknown[], prefix: readonly unknown[]) =>
      prefix.every((part, index) => key[index] === part);
    const team = [
      backlogKey('o1', teamScope('t1'), 'stories'),
      boardKey('o1', teamScope('t1'), 'stories'),
    ];
    const project = [
      backlogKey('o1', projectScope('p1'), 'stories'),
      boardKey('o1', projectScope('p1'), 'stories'),
    ];
    const teamPrefixes = [boardsKeys.backlogs('o1', 't1'), boardsKeys.boards('o1', 't1')];
    const projectPrefixes = [
      boardsKeys.projectBacklogs('o1', 'p1'),
      boardsKeys.projectBoards('o1', 'p1'),
    ];

    for (const key of project) {
      expect(teamPrefixes.some((prefix) => startsWith(key, prefix))).toBe(false);
    }
    for (const key of team) {
      expect(projectPrefixes.some((prefix) => startsWith(key, prefix))).toBe(false);
    }
    expect(
      projectPrefixes.some((prefix) => startsWith(project[0] as readonly unknown[], prefix)),
    ).toBe(true);
    expect(teamPrefixes.some((prefix) => startsWith(team[1] as readonly unknown[], prefix))).toBe(
      true,
    );
  });
});

describe('readName', () => {
  it('is the team’s name or the project’s, whichever the answer names, and none before the read', () => {
    expect(readName(teamBacklog)).toBe('Платформа');
    expect(readName(teamBoard)).toBe('Платформа');
    expect(readName(projectBacklog)).toBe('Альфа');
    expect(readName(projectBoard)).toBe('Альфа');
    expect(readName(undefined)).toBeUndefined();
  });
});

describe('readProjects', () => {
  it('is the team’s linked projects of a team’s answer, and none for the project’s', () => {
    expect(readProjects(teamBacklog)).toEqual([alpha]);
    expect(readProjects(teamBoard)).toEqual([alpha]);
    expect(readProjects(projectBacklog)).toBeUndefined();
    expect(readProjects(undefined)).toBeUndefined();
  });
});

// mod-017 lifts feat-039 BR-PB-6: every answer, a team's or the project's, offers a creation;
// feat-040 BR-TS-9: a team's names the projects it may create in.
describe('creationSurface', () => {
  it('is the level of any answer, with the team’s projects on a team’s, and none before the read', () => {
    expect(creationSurface(teamBacklog)).toEqual({ level, projects: [alpha] });
    expect(creationSurface(teamBoard)).toEqual({ level, projects: [alpha] });
    expect(creationSurface(projectBacklog)).toEqual({ level });
    expect(creationSurface(projectBoard)).toEqual({ level });
    expect(creationSurface(undefined)).toBeUndefined();
  });
});

describe('startTeamOf', () => {
  it('is the team of a team’s scope, and none for the project’s: the dialog takes its default team (BR-OT-3)', () => {
    expect(startTeamOf(teamScope('t1'))).toBe('t1');
    expect(startTeamOf(projectScope('p1'))).toBeUndefined();
  });
});
