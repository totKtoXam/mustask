import { describe, expect, it } from 'vitest';
import { LANDING_LEVEL } from './lib/levels';
import {
  BACKLOG_ROUTE,
  BOARD_ROUTE,
  PROJECT_BACKLOG_ROUTE,
  PROJECT_BOARD_ROUTE,
  backlogLevelPaths,
  backlogPath,
  backlogsLandingPath,
  boardLevelPaths,
  boardPath,
  boardsLandingPath,
  projectBacklogLevelOf,
  projectBacklogPath,
  projectBoardLevelOf,
  projectBoardPath,
} from './paths';

// The slice’s addresses: a team’s, at the team alone (feat-040 BR-TS-1), and the project’s own
// (feat-039 BR-PB-1), the second without a team in them.

describe('the project’s addresses (feat-039 BR-PB-1)', () => {
  it('are the project’s prefix, the page and the level, with no team', () => {
    expect(projectBacklogPath('o1', 'p1', 'epics')).toBe(
      '/organizations/o1/projects/p1/backlogs/epics',
    );
    expect(projectBoardPath('o1', 'p1', 'stories')).toBe(
      '/organizations/o1/projects/p1/boards/stories',
    );
    expect(PROJECT_BACKLOG_ROUTE).toBe(
      '/organizations/:organizationId/projects/:projectId/backlogs/:level',
    );
    expect(PROJECT_BOARD_ROUTE).toBe(
      '/organizations/:organizationId/projects/:projectId/boards/:level',
    );
  });

  it('re-encode the ids as received', () => {
    expect(projectBacklogPath('o 1', 'p/1', 'features')).toBe(
      '/organizations/o%201/projects/p%2F1/backlogs/features',
    );
  });
});

describe('a team’s addresses (feat-040 BR-TS-1)', () => {
  it('are the organization, the team, the page and the level, with no project', () => {
    expect(backlogPath('o1', 't1', 'epics')).toBe('/organizations/o1/teams/t1/backlogs/epics');
    expect(boardPath('o1', 't1', 'features')).toBe('/organizations/o1/teams/t1/boards/features');
    expect(BACKLOG_ROUTE).toBe('/organizations/:organizationId/teams/:teamId/backlogs/:level');
    expect(BOARD_ROUTE).toBe('/organizations/:organizationId/teams/:teamId/boards/:level');
  });

  it('re-encode the ids as received', () => {
    expect(backlogPath('o 1', 't/1', 'stories')).toBe(
      '/organizations/o%201/teams/t%2F1/backlogs/stories',
    );
  });

  it('give the entries their landing on «Истории» and their three levels in rank order', () => {
    expect(backlogsLandingPath('o1', 't1')).toBe(
      `/organizations/o1/teams/t1/backlogs/${LANDING_LEVEL}`,
    );
    expect(boardsLandingPath('o1', 't1')).toBe(
      `/organizations/o1/teams/t1/boards/${LANDING_LEVEL}`,
    );
    expect(backlogLevelPaths('o1', 't1')).toEqual([
      '/organizations/o1/teams/t1/backlogs/epics',
      '/organizations/o1/teams/t1/backlogs/features',
      '/organizations/o1/teams/t1/backlogs/stories',
    ]);
    expect(boardLevelPaths('o1', 't1')).toEqual([
      '/organizations/o1/teams/t1/boards/epics',
      '/organizations/o1/teams/t1/boards/features',
      '/organizations/o1/teams/t1/boards/stories',
    ]);
  });
});

describe('projectBacklogLevelOf / projectBoardLevelOf', () => {
  it.each([['epics'], ['features'], ['stories']] as const)(
    'read %s from the project’s own address, trailing slash or not, and the other page’s as none',
    (level) => {
      expect(projectBacklogLevelOf(`/organizations/o1/projects/p1/backlogs/${level}`)).toBe(level);
      expect(projectBacklogLevelOf(`/organizations/o1/projects/p1/backlogs/${level}/`)).toBe(level);
      expect(projectBoardLevelOf(`/organizations/o1/projects/p1/boards/${level}`)).toBe(level);
      expect(projectBoardLevelOf(`/organizations/o1/projects/p1/backlogs/${level}`)).toBeNull();
      expect(projectBacklogLevelOf(`/organizations/o1/projects/p1/boards/${level}`)).toBeNull();
    },
  );

  it.each([
    ['a level the page does not serve', '/organizations/o1/projects/p1/backlogs/tasks'],
    ['a level in another case', '/organizations/o1/projects/p1/backlogs/Stories'],
    ['no level', '/organizations/o1/projects/p1/backlogs'],
    ['a page beneath the level', '/organizations/o1/projects/p1/backlogs/stories/more'],
    ['a team’s address', '/organizations/o1/teams/t1/backlogs/stories'],
    [
      'the retired project-nested team address',
      '/organizations/o1/projects/p1/teams/t1/backlogs/stories',
    ],
    ['the project’s settings', '/organizations/o1/projects/p1/settings/general'],
    ['a work item', '/organizations/o1/projects/p1/workitems/ALP-US-1'],
  ])('read %s as none', (_case, pathname) => {
    expect(projectBacklogLevelOf(pathname)).toBeNull();
    expect(projectBoardLevelOf(pathname)).toBeNull();
  });
});
