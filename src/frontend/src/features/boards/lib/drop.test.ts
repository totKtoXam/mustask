import { describe, expect, it } from 'vitest';
import { sampleProcessDefinition } from '../../process/testFixtures';
import type { BacklogTreeItem } from '../api/boardsApi';
import {
  LEVEL_REFUSAL_TEXT,
  cycleRefusalText,
  descendantIds,
  dropVerdict,
  pathDown,
  refusalNotice,
  typeRanks,
} from './drop';

// feat-027 BR-T-11, BR-T-12: what dropping a row onto another does, decided on
// the read the page shows, in the parent write's own order — the type, then the
// descendants — and the process's own level ranks for the type.

const TYPE_IDS = Object.fromEntries(
  sampleProcessDefinition.workItemTypes.map((type) => [type.key, type.id]),
);

/** A row of `typeKey` with `children`, each of which stands beneath it (its `parent` is it). */
function node(
  identifier: string,
  typeKey: string,
  children: BacklogTreeItem[] = [],
): BacklogTreeItem {
  const type = { id: TYPE_IDS[typeKey] as string, key: typeKey, name: typeKey, appearance: 'epic' };
  const state = { id: 's', name: 'Новая', category: { code: 'Proposed', label: 'Новые' } };
  const row: BacklogTreeItem = {
    id: identifier,
    identifier,
    number: 0,
    type,
    title: identifier,
    assignee: null,
    team: null,
    state,
    priority: 2,
    effort: null,
    businessValue: null,
    valueArea: null,
    parent: null,
    children,
  };
  for (const child of children) {
    child.parent = { id: identifier, identifier, type, title: identifier, state };
  }
  return row;
}

/** EPIC-1 → FEAT-8 → FEAT-23 → FEAT-30; EPIC-1 → US-22; US-2 → TASK-3; EPIC-20 alone. */
const feat30 = node('FEAT-30', 'FEAT');
const feat23 = node('FEAT-23', 'FEAT', [feat30]);
const feat8 = node('FEAT-8', 'FEAT', [feat23]);
const us22 = node('US-22', 'US');
const epic1 = node('EPIC-1', 'EPIC', [feat8, us22]);
const task3 = node('TASK-3', 'TASK');
const us2 = node('US-2', 'US', [task3]);
const epic20 = node('EPIC-20', 'EPIC');

const ranks = typeRanks(sampleProcessDefinition);

function verdictOf(dragged: BacklogTreeItem, target: BacklogTreeItem) {
  return dropVerdict(dragged, target, { ranks, descendants: descendantIds(dragged) });
}

describe('typeRanks', () => {
  it('ranks every type by its level: epic over feature over story and bug over task', () => {
    const rank = (key: string) => ranks.get(TYPE_IDS[key] as string);
    expect(rank('EPIC')).toBeGreaterThan(rank('FEAT') as number);
    expect(rank('FEAT')).toBeGreaterThan(rank('US') as number);
    expect(rank('US')).toBe(rank('BUG'));
    expect(rank('US')).toBeGreaterThan(rank('TASK') as number);
  });

  it('knows no rank for a type the process does not list', () => {
    expect(ranks.get('unknown')).toBeUndefined();
  });
});

describe('descendantIds and pathDown', () => {
  it('collects every descendant, however deep, and the path to one runs from the item down', () => {
    expect([...descendantIds(epic1)].sort()).toEqual(['FEAT-23', 'FEAT-30', 'FEAT-8', 'US-22']);
    expect(descendantIds(epic20).size).toBe(0);
    expect(pathDown(epic1, feat30)).toEqual(['EPIC-1', 'FEAT-8', 'FEAT-23', 'FEAT-30']);
    expect(pathDown(feat8, feat23)).toEqual(['FEAT-8', 'FEAT-23']);
    expect(pathDown(feat23, epic1)).toBeNull();
  });

  it('walks a chain of any length without recursing', () => {
    let chain = node('LAST', 'FEAT');
    for (let depth = 0; depth < 20_000; depth += 1) {
      chain = node(`F-${depth}`, 'FEAT', [chain]);
    }
    expect(descendantIds(chain).size).toBe(20_000);
    expect(pathDown(chain, { id: 'LAST' })?.length).toBe(20_001);
  });
});

describe('dropVerdict and refusalNotice', () => {
  it('is no target on the item itself or on its current parent, shown and sent nothing', () => {
    expect(verdictOf(feat8, feat8)).toEqual({ kind: 'none' });
    expect(verdictOf(feat8, epic1)).toEqual({ kind: 'none' });
  });

  it('allows a type of the same or a higher level: a story onto a feature, a feature onto an epic, a story onto a story', () => {
    expect(verdictOf(us22, feat8)).toEqual({ kind: 'allowed' });
    expect(verdictOf(feat23, epic20)).toEqual({ kind: 'allowed' });
    expect(verdictOf(us2, us22)).toEqual({ kind: 'allowed' });
    expect(verdictOf(epic20, epic1)).toEqual({ kind: 'allowed' });
  });

  it.each([
    ['a feature', feat8, us22],
    ['an epic', epic20, feat8],
    ['a story', us22, task3],
  ])('refuses %s dropped onto a type below it, with the level’s notice', (_, dragged, target) => {
    expect(verdictOf(dragged, target)).toEqual({ kind: 'refused', reason: 'level' });
    expect(refusalNotice('level', dragged, target)).toBe(LEVEL_REFUSAL_TEXT);
  });

  it('refuses a descendant, the notice naming the path from the item down to the target', () => {
    expect(verdictOf(feat8, feat23)).toEqual({ kind: 'refused', reason: 'descendant' });
    expect(refusalNotice('descendant', feat8, feat23)).toBe(
      'Нельзя выбрать родителем FEAT-23: он вложен в FEAT-8 (FEAT-8 → FEAT-23).',
    );
    expect(verdictOf(feat8, feat30)).toEqual({ kind: 'refused', reason: 'descendant' });
    expect(refusalNotice('descendant', feat8, feat30)).toBe(
      cycleRefusalText('FEAT-30', 'FEAT-8', ['FEAT-8', 'FEAT-23', 'FEAT-30']),
    );
  });

  it('judges the type before the descendants, as the parent write does: an epic dropped onto its own feature is a level refusal', () => {
    expect(verdictOf(epic1, feat8)).toEqual({ kind: 'refused', reason: 'level' });
  });

  it('leaves the type to the server while the process is not read, and still refuses a descendant', () => {
    const none = { ranks: new Map<string, number>(), descendants: descendantIds(feat8) };
    expect(dropVerdict(epic20, feat8, none)).toEqual({ kind: 'allowed' });
    expect(dropVerdict(feat8, feat23, none)).toEqual({ kind: 'refused', reason: 'descendant' });
  });
});
