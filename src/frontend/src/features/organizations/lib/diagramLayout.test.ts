import { describe, expect, it } from 'vitest';
import { diagramAreas, diagramOffers } from './diagramFixture';
import {
  CARD_HEIGHT,
  HIERARCHY_WIDTH,
  INDENT,
  NODE_HEIGHT,
  NODE_WIDTH,
  PAD,
  ROW,
  layoutDiagram,
  type CardItem,
  type LayoutOptions,
} from './diagramLayout';
import { NO_LEVELS } from './diagramLevels';
import { buildModel, emphasisOf, relatedFilter } from './diagramModel';

const model = buildModel(diagramAreas, diagramOffers);
const all = new Set(
  [...model.childrenOf.entries()].filter(([, kids]) => kids.length > 0).map(([id]) => id),
);
const base: LayoutOptions = {
  orientation: 'h',
  levels: NO_LEVELS,
  sections: { outside: true, departments: true },
  related: null,
};

function cards(layout: ReturnType<typeof layoutDiagram>): CardItem[] {
  return layout.items.filter((i): i is CardItem => i.type === 'card');
}

describe('feat-049 horizontal layout (design § 4.2)', () => {
  const layout = layoutDiagram(model, all, base);

  it('puts the outside section on top and lists cards in pre-order', () => {
    expect(layout.items.map((i) => i.id)).toEqual([
      'section:outside',
      'nF',
      'team:ATLAS@-',
      'section:departments',
      'd1',
      'nA',
      'a1',
      'a2',
      'team:JUPITER@d1',
      'team:MARS@d1',
      'team:VENUS@d1',
      'd2',
      'nB',
      'nC',
      'team:JUPITER@d2',
      'team:SATURN@d2',
    ]);
  });

  it('indents a card by its true level and ends all at one right edge', () => {
    const byId = (id: string) => layout.positions.get(id) as CardItem;
    expect(byId('d1').x).toBe(PAD);
    expect(byId('a2').x).toBe(PAD + 3 * INDENT);
    expect(byId('team:MARS@d1').level).toBe(2);
    expect(byId('team:MARS@d1').ariaLevel).toBe(3);
    expect(byId('team:ATLAS@-').level).toBe(1);
    for (const card of cards(layout)) expect(card.x + card.w).toBe(PAD + HIERARCHY_WIDTH);
    expect(byId('nA').y - byId('d1').y).toBe(ROW);
    expect(byId('d1').h).toBe(CARD_HEIGHT);
  });

  it('draws project-team lines with the copy under the project’s own department', () => {
    const line = layout.lines.find((l) => l.keys.includes('nA>MARS'))!;
    expect(line).toMatchObject({ kind: 'relation', from: 'nA', to: 'team:MARS@d1', hidden: false });
    const jupiter = layout.lines.find((l) => l.keys.includes('nF>JUPITER'))!;
    expect(jupiter.to).toBe('team:JUPITER@d1');
    expect(layout.lines.filter((l) => l.kind === 'copy')).toHaveLength(1);
  });

  it('drops the cards of a folded section and of a collapsed branch', () => {
    const folded = layoutDiagram(model, all, {
      ...base,
      sections: { outside: true, departments: false },
    });
    expect(cards(folded).map((c) => c.id)).toEqual(['nF', 'team:ATLAS@-']);
    const collapsed = layoutDiagram(model, new Set(), base);
    expect(cards(collapsed).map((c) => c.id)).toEqual([
      'nF',
      'team:ATLAS@-',
      'd1',
      'd2',
    ]);
  });
});

describe('feat-049 levels (BR-SD-14)', () => {
  it('shows exactly the pressed levels and joins across the hidden one by a dotted join', () => {
    const layout = layoutDiagram(model, all, {
      ...base,
      levels: { selected: new Set([1, 3]), mode: 'only' },
    });
    const shown = cards(layout);
    expect(shown.map((c) => c.id)).toEqual(['nF', 'team:ATLAS@-', 'd1', 'a1', 'd2']);
    const a1 = shown.find((c) => c.id === 'a1')!;
    expect(a1.parentId).toBe('d1');
    expect(a1.gap).toBe(true);
  });

  it('shows the topmost pressed level and every deeper one in the mode «from»', () => {
    const layout = layoutDiagram(model, all, {
      ...base,
      levels: { selected: new Set([3]), mode: 'from' },
    });
    expect(cards(layout).map((c) => c.id)).toEqual(['a1', 'a2']);
  });

  it('draws a relation through a hidden card dotted, and none without a visible end', () => {
    const layout = layoutDiagram(model, all, {
      ...base,
      levels: { selected: new Set([2]), mode: 'only' },
    });
    // Level 2 holds the projects of departments, the teams under them and a1's parent chain.
    const line = layout.lines.find((l) => l.keys.includes('nF>JUPITER'));
    expect(line).toBeUndefined();
    const ok = layout.lines.find((l) => l.keys.includes('nA>MARS'))!;
    expect(ok.hidden).toBe(false);
  });
});

describe('feat-049 «Только связанные» (design § 4.7)', () => {
  it('keeps the chosen branch, its relations and their ancestors', () => {
    const related = relatedFilter(model, emphasisOf(model, 'nF'));
    const layout = layoutDiagram(model, new Set(), { ...base, related });
    expect(cards(layout).map((c) => c.id)).toEqual(['nF', 'd1', 'team:JUPITER@d1', 'd2', 'team:JUPITER@d2']);
    // The user's expansion is not changed by the forced one.
    expect(layout.expansion.has('d1')).toBe(true);
  });
});

describe('feat-049 vertical layout (design § 4.3)', () => {
  const layout = layoutDiagram(model, all, { ...base, orientation: 'v' });

  it('puts levels on rows and a parent over the middle of its children', () => {
    const byId = (id: string) => layout.positions.get(id) as CardItem;
    expect(byId('d1').y).toBeLessThan(byId('nA').y);
    expect(byId('nA').y).toBe(byId('team:MARS@d1').y);
    const children = ['nA', 'team:JUPITER@d1', 'team:MARS@d1', 'team:VENUS@d1'].map(byId);
    const middle = (children[0]!.x + children[3]!.x) / 2;
    expect(byId('d1').x).toBeCloseTo(middle);
    expect(byId('d1').w).toBe(NODE_WIDTH);
    expect(byId('d1').h).toBe(NODE_HEIGHT);
  });

  it('shows the same cards in the same order as the horizontal one', () => {
    const horizontal = layoutDiagram(model, all, base);
    expect(layout.items.map((i) => i.id)).toEqual(horizontal.items.map((i) => i.id));
    expect(layout.lines).toEqual(horizontal.lines);
  });

  it('repeats the level chips in each section band', () => {
    const levels = layout.chips.map((c) => c.level);
    // outside: levels 1; departments: levels 1..4.
    expect(levels).toEqual([1, 1, 2, 3, 4]);
  });
});
