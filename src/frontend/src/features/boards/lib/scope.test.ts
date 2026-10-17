import { describe, expect, it } from 'vitest';
import { backlogKey, boardKey, boardsKeys } from '../api/queryKeys';
import {
  PROJECT_SCOPE,
  creationSurface,
  readName,
  scopeTeamId,
  teamScope,
  type SurfaceRead,
} from './scope';

// feat-039: what a backlog or a board is of, and the keys and reads that follow from it.

const level = { code: 'stories', name: 'Истории', types: [] };
const team = { id: 't1', name: 'Платформа', defaultArea: { id: 'a1', path: 'Платформа' } };
const teamBacklog: SurfaceRead = { team, level, items: [] };
const projectBacklog: SurfaceRead = { project: { id: 'p1', name: 'Альфа' }, level, items: [] };
const teamBoard: SurfaceRead = { team, level, columns: [] };
const projectBoard: SurfaceRead = { project: { id: 'p1', name: 'Альфа' }, level, columns: [] };

describe('the scope', () => {
  it('is a team’s, which names its id, or the whole project’s, which names none', () => {
    expect(scopeTeamId(teamScope('t1'))).toBe('t1');
    expect(scopeTeamId(PROJECT_SCOPE)).toBeNull();
    expect(PROJECT_SCOPE).toEqual({ kind: 'project' });
  });
});

describe('the query keys under a scope', () => {
  it('are a team’s own keys for a team and the project’s own for the project', () => {
    expect(backlogKey('o1', 'p1', teamScope('t1'), 'epics')).toEqual(
      boardsKeys.backlog('o1', 'p1', 't1', 'epics'),
    );
    expect(boardKey('o1', 'p1', teamScope('t1'), 'epics')).toEqual(
      boardsKeys.board('o1', 'p1', 't1', 'epics'),
    );
    expect(backlogKey('o1', 'p1', PROJECT_SCOPE, 'epics')).toEqual(
      boardsKeys.projectBacklog('o1', 'p1', 'epics'),
    );
    expect(boardKey('o1', 'p1', PROJECT_SCOPE, 'epics')).toEqual(
      boardsKeys.projectBoard('o1', 'p1', 'epics'),
    );
  });

  it('never fall under the other scope’s prefix: a read-again of one reaches none of the other’s', () => {
    const startsWith = (key: readonly unknown[], prefix: readonly unknown[]) =>
      prefix.every((part, index) => key[index] === part);
    const team = [
      backlogKey('o1', 'p1', teamScope('t1'), 'stories'),
      boardKey('o1', 'p1', teamScope('t1'), 'stories'),
    ];
    const project = [
      backlogKey('o1', 'p1', PROJECT_SCOPE, 'stories'),
      boardKey('o1', 'p1', PROJECT_SCOPE, 'stories'),
    ];
    const teamPrefixes = [
      boardsKeys.backlogs('o1', 'p1', 't1'),
      boardsKeys.boards('o1', 'p1', 't1'),
    ];
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

describe('creationSurface', () => {
  it('is the team’s answer, and none for the project’s: a creation needs a team (BR-PB-6)', () => {
    expect(creationSurface(teamBacklog)).toBe(teamBacklog);
    expect(creationSurface(teamBoard)).toBe(teamBoard);
    expect(creationSurface(projectBacklog)).toBeUndefined();
    expect(creationSurface(projectBoard)).toBeUndefined();
    expect(creationSurface(undefined)).toBeUndefined();
  });
});
