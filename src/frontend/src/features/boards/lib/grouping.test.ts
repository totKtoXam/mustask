import { describe, expect, it } from 'vitest';
import {
  backlogCanMove,
  backlogHint,
  defaultGrouping,
  groupingsOffered,
  groupingStorageKey,
  groupingTexts,
  rememberGrouping,
  resolveGrouping,
  viewLevelOf,
  type KeyValueStore,
} from './grouping';
import { initialOpen, MAX_OPEN_GROUPS, withOpened } from './openGroups';
import type { GroupHeader } from '../api/aggregateApi';

function memory(): KeyValueStore & { data: Map<string, string> } {
  const data = new Map<string, string>();
  return { data, getItem: (k) => data.get(k) ?? null, setItem: (k, v) => void data.set(k, v) };
}

describe('groupingsOffered (BR-AV-2)', () => {
  it.each([
    ['organization', {}, ['none', 'department', 'project', 'team']],
    ['department', { departmentId: 'd' }, ['none', 'project', 'team']],
    ['team', { teamId: 't' }, ['none', 'project']],
    ['team with a department', { teamId: 't', departmentId: 'd' }, ['none', 'project']],
    ['project', { projectId: 'p' }, ['none', 'team']],
    ['project and team', { projectId: 'p', teamId: 't' }, ['none']],
  ])('%s', (_name, selectors, expected) => {
    expect(groupingsOffered(selectors)).toEqual(expected);
  });
});

describe('defaultGrouping (BR-AV-3, OQ-7 A)', () => {
  it('is by project for backlog and board at organization and department level', () => {
    expect(defaultGrouping('backlogs', {})).toBe('project');
    expect(defaultGrouping('boards', { departmentId: 'd' })).toBe('project');
  });
  it('is none at team level without a department, by project with one, none at project level', () => {
    expect(defaultGrouping('backlogs', { teamId: 't' })).toBe('none');
    expect(defaultGrouping('boards', { teamId: 't', departmentId: 'd' })).toBe('project');
    expect(defaultGrouping('backlogs', { projectId: 'p' })).toBe('none');
  });
  it('is none for «Рабочие элементы» everywhere', () => {
    expect(defaultGrouping('workitems', {})).toBe('none');
    expect(defaultGrouping('workitems', { departmentId: 'd' })).toBe('none');
  });
});

describe('the browser memory', () => {
  it('is per user, organization, view and level, and wins over the default', () => {
    const store = memory();
    rememberGrouping(store, 'u', 'o', 'backlogs', {}, 'team');
    expect(resolveGrouping(store, 'u', 'o', 'backlogs', {})).toBe('team');
    expect(resolveGrouping(store, 'u', 'o', 'boards', {})).toBe('project');
    expect(resolveGrouping(store, 'u', 'o', 'backlogs', { departmentId: 'd' })).toBe('project');
    expect(resolveGrouping(store, 'other', 'o', 'backlogs', {})).toBe('project');
    expect(store.data.has(groupingStorageKey('u', 'o', 'backlogs', viewLevelOf({})))).toBe(true);
  });

  it('falls back silently to the default when the level no longer offers the remembered one', () => {
    const store = memory();
    store.setItem(groupingStorageKey('u', 'o', 'backlogs', 'department'), 'department');
    expect(resolveGrouping(store, 'u', 'o', 'backlogs', { departmentId: 'd' })).toBe('project');
  });

  it('ignores a remembered «По департаменту» at team level (the backend refuses it)', () => {
    const store = memory();
    store.setItem(groupingStorageKey('u', 'o', 'backlogs', 'team'), 'department');
    expect(resolveGrouping(store, 'u', 'o', 'backlogs', { teamId: 't' })).toBe('none');
    expect(resolveGrouping(store, 'u', 'o', 'backlogs', { teamId: 't', departmentId: 'd' })).toBe(
      'project',
    );
  });

  it('survives a blocked store', () => {
    const blocked: KeyValueStore = {
      getItem: () => {
        throw new Error('blocked');
      },
      setItem: () => {
        throw new Error('blocked');
      },
    };
    expect(resolveGrouping(blocked, 'u', 'o', 'backlogs', {})).toBe('project');
    expect(() => rememberGrouping(blocked, 'u', 'o', 'backlogs', {}, 'none')).not.toThrow();
  });
});

describe('hints and moves (design § 5)', () => {
  it('shows the narrowing hint only by project while narrowed', () => {
    expect(backlogHint('project', { teamId: 't' }, false)).toBe(groupingTexts.narrowingHint);
    expect(backlogHint('project', {}, false)).toBeNull();
  });
  it('shows the change-time hint for the other groupings and the stream at organization level', () => {
    expect(backlogHint('team', {}, false)).toBe(groupingTexts.changeTimeHint);
    expect(backlogHint('none', {}, false)).toBe(groupingTexts.changeTimeHint);
    expect(backlogHint('team', { projectId: 'p' }, false)).toBe(groupingTexts.changeTimeHint);
    expect(backlogHint('none', { projectId: 'p' }, false)).toBeNull();
  });
  it('moves rows only by project with nothing narrowing', () => {
    expect(backlogCanMove('project', {}, false)).toBe(true);
    expect(backlogCanMove('project', { teamId: 't' }, false)).toBe(false);
    expect(backlogCanMove('project', {}, true)).toBe(false);
    expect(backlogCanMove('team', {}, false)).toBe(false);
    expect(backlogCanMove('none', {}, false)).toBe(false);
  });
});

describe('open groups (BR-NV-20)', () => {
  const header = (id: string | null): GroupHeader => ({
    kind: 'team',
    id,
    name: id,
    count: 1,
    total: 1,
  });
  it('opens the first group on a first visit and drops entries of groups that are gone', () => {
    const headers = [header(null), header('a'), header('b')];
    expect(initialOpen(null, headers)).toEqual(['none']);
    expect(initialOpen(['b', 'gone'], headers)).toEqual(['b']);
  });
  it('keeps ten at most, dropping the oldest', () => {
    const open = Array.from({ length: MAX_OPEN_GROUPS }, (_u, i) => String(i));
    expect(withOpened(open, 'new')).toEqual([...open.slice(1), 'new']);
    expect(withOpened(open, '3')).toEqual(open);
  });
});
