import { structureDiagram as texts } from './areaTexts';
import { levelVisible, type LevelChoice } from './diagramLevels';
import {
  ancestorsOf,
  SECTION_KEYS,
  type DiagramModel,
  type DiagramNode,
  type RelatedFilter,
  type SectionKey,
} from './diagramModel';

/**
 * The diagram's layout (feat-049 design §§ 4.2-4.4, 4.6): a pure function of the model, the
 * expansion and the options. It fixes the visible set and each card's nearest visible ancestor,
 * places cards in one of two orientations and routes the relation lines. No DOM.
 */

/** Horizontal layout, in px (design § 4.6). */
export const ROW = 40;
export const CARD_HEIGHT = 32;
export const INDENT = 22;
export const HIERARCHY_WIDTH = 480;
export const GUTTER = 150;
export const RULER = 22;
export const PAD = 16;
export const SECTION_HEIGHT = 28;
/** Vertical layout, in px. */
export const NODE_WIDTH = 156;
export const NODE_HEIGHT = 58;
export const SLOT_GAP = 12;
export const BAND = 88;
export const SLIM_BAND = 30;
export const LABEL_WIDTH = 40;
export const CHIP_SIZE = 24;
/** Lines up to this many are all drawn (OQ-6 A); above, the chosen branch's only. */
export const RELATION_LINE_LIMIT = 150;
/** Above this many visible cards the vertical layout shows its note. */
export const VERTICAL_NOTE_LIMIT = 120;

export type Orientation = 'h' | 'v';

export interface LayoutOptions {
  orientation: Orientation;
  levels: LevelChoice;
  /** Unfolded sections. */
  sections: Readonly<Record<SectionKey, boolean>>;
  /** «Только связанные» while a node is chosen. */
  related: RelatedFilter | null;
}

export interface SectionItem {
  type: 'section';
  id: string;
  section: SectionKey;
  title: string;
  count: number;
  expanded: boolean;
  /** `aria-level`. */
  ariaLevel: 1;
  x: number;
  y: number;
  w: number;
  h: number;
}

export interface CardItem {
  type: 'card';
  id: string;
  node: DiagramNode;
  /** The true nesting level, 1 at the top. */
  level: number;
  ariaLevel: number;
  x: number;
  y: number;
  w: number;
  h: number;
  /** The nearest visible ancestor, drawn as the join's start. */
  parentId: string | null;
  /** The join crosses a hidden level: dotted. */
  gap: boolean;
  section: SectionKey;
  hasChildren: boolean;
  expanded: boolean;
}

export type LayoutItem = SectionItem | CardItem;

export interface LayoutLine {
  kind: 'relation' | 'copy';
  from: string;
  to: string;
  /** Through a hidden node: dotted. */
  hidden: boolean;
  /** Emphasis keys, see `Emphasis.lines`. */
  keys: readonly string[];
}

export interface ChipPlace {
  level: number;
  x: number;
  y: number;
}

export interface DiagramLayout {
  orientation: Orientation;
  items: readonly LayoutItem[];
  positions: ReadonlyMap<string, LayoutItem>;
  lines: readonly LayoutLine[];
  chips: readonly ChipPlace[];
  expansion: ReadonlySet<string>;
  /** Visible children of a card in drawing order. */
  visibleChildren: (id: string) => readonly DiagramNode[];
  width: number;
  height: number;
  rulerY: number;
  /** Where the faint level guides of the horizontal layout end. */
  guideBottom: number;
  visibleCount: number;
}

interface Placed {
  node: DiagramNode;
  level: number;
  parent: Placed | null;
  cx: number;
}

export function layoutDiagram(
  model: DiagramModel,
  expansion: ReadonlySet<string>,
  options: LayoutOptions,
): DiagramLayout {
  const { orientation, levels, sections, related } = options;
  const vertical = orientation === 'v';
  let expanded: ReadonlySet<string> = expansion;
  if (related) {
    // «Только связанные»: the ancestors of the shown cards are forced open for the choice.
    const forced = new Set(expansion);
    // A kept card opens too, so the kept copies of related teams under a department show.
    for (const id of related.keep) {
      forced.add(id);
      ancestorsOf(model, id).forEach((a) => forced.add(a));
    }
    expanded = forced;
  }

  const keepNode = (node: DiagramNode): boolean => {
    if (!related) return true;
    if (node.kind === 'team') {
      return related.teams.has(node.baseId) && (!node.parentId || related.keep.has(node.parentId));
    }
    return related.keep.has(node.id);
  };
  const kidsOf = (id: string): readonly DiagramNode[] =>
    (model.childrenOf.get(id) ?? []).filter(keepNode);

  let width = vertical ? 0 : PAD + HIERARCHY_WIDTH + GUTTER + PAD;
  let y = PAD;
  const rulerY = vertical ? 0 : y;
  if (!vertical) y += RULER;
  const items: LayoutItem[] = [];
  const positions = new Map<string, LayoutItem>();
  const chips: ChipPlace[] = [];
  const left = PAD + (vertical ? LABEL_WIDTH : 0);
  const sectionItems: SectionItem[] = [];

  for (const section of SECTION_KEYS) {
    const roots = model.roots[section].filter(keepNode);
    if (roots.length === 0) continue;
    const sequence: Placed[] = [];
    const walk = (list: readonly DiagramNode[], level: number, parent: Placed | null) => {
      for (const node of list) {
        const placed: Placed = { node, level, parent, cx: 0 };
        sequence.push(placed);
        const kids = kidsOf(node.id);
        if (kids.length > 0 && expanded.has(node.id)) walk(kids, level + 1, placed);
      }
    };
    walk(roots, 1, null);
    let total = 0;
    const count = (list: readonly DiagramNode[]) => {
      for (const node of list) {
        total += 1;
        count(model.childrenOf.get(node.id) ?? []);
      }
    };
    count(model.roots[section]);

    const open = sections[section];
    const head: SectionItem = {
      type: 'section',
      id: `section:${section}`,
      section,
      title: texts.sections[section],
      count: total,
      expanded: open,
      ariaLevel: 1,
      x: PAD,
      y,
      w: vertical ? 0 : width - 2 * PAD,
      h: SECTION_HEIGHT,
    };
    sectionItems.push(head);
    items.push(head);
    positions.set(head.id, head);
    y += SECTION_HEIGHT;
    if (!open) {
      y += 10;
      continue;
    }

    // Hidden levels drop; a card attaches to its nearest visible ancestor.
    const visible = sequence.filter((p) => levelVisible(levels, p.level));
    const visibleSet = new Set(visible);
    const attach = new Map<Placed, Placed | null>();
    for (const p of visible) {
      let ancestor = p.parent;
      while (ancestor && !visibleSet.has(ancestor)) ancestor = ancestor.parent;
      attach.set(p, ancestor);
    }
    const cardOf = (p: Placed, x: number, top: number, w: number, h: number): CardItem => {
      const ancestor = attach.get(p) ?? null;
      return {
        type: 'card',
        id: p.node.id,
        node: p.node,
        level: p.level,
        ariaLevel: p.level + 1,
        x,
        y: top,
        w,
        h,
        parentId: ancestor ? ancestor.node.id : null,
        gap: ancestor ? p.level - ancestor.level > 1 : false,
        section,
        hasChildren: kidsOf(p.node.id).length > 0,
        expanded: expanded.has(p.node.id),
      };
    };

    const cards: CardItem[] = [];
    if (!vertical) {
      visible.forEach((p, index) => {
        cards.push(
          cardOf(
            p,
            PAD + (p.level - 1) * INDENT,
            y + index * ROW,
            HIERARCHY_WIDTH - (p.level - 1) * INDENT,
            CARD_HEIGHT,
          ),
        );
      });
      y += visible.length * ROW;
    } else {
      const kids = new Map<Placed, Placed[]>();
      for (const p of visible) {
        const ancestor = attach.get(p);
        if (ancestor) {
          const list = kids.get(ancestor);
          if (list) list.push(p);
          else kids.set(ancestor, [p]);
        }
      }
      let slot = 0;
      const place = (p: Placed) => {
        const children = kids.get(p) ?? [];
        if (children.length === 0) {
          p.cx = slot * (NODE_WIDTH + SLOT_GAP) + NODE_WIDTH / 2;
          slot += 1;
        } else {
          children.forEach(place);
          p.cx = (children[0]!.cx + children[children.length - 1]!.cx) / 2;
        }
      };
      visible.filter((p) => !attach.get(p)).forEach(place);
      width = Math.max(width, left + slot * (NODE_WIDTH + SLOT_GAP) + PAD);
      const present = new Set(visible.map((p) => p.level));
      const bands: { top: number; h: number }[] = [];
      let bandTop = y;
      for (let level = 1; level <= model.sectionDepth[section]; level += 1) {
        const h = present.has(level) ? BAND : SLIM_BAND;
        bands[level] = { top: bandTop, h };
        chips.push({ level, x: PAD, y: bandTop + Math.max(2, (h - CHIP_SIZE) / 2) });
        bandTop += h;
      }
      for (const p of visible) {
        cards.push(
          cardOf(p, left + p.cx - NODE_WIDTH / 2, bands[p.level]!.top, NODE_WIDTH, NODE_HEIGHT),
        );
      }
      y = bandTop;
    }
    for (const card of cards) {
      items.push(card);
      positions.set(card.id, card);
    }
    y += 14;
  }
  if (vertical) for (const head of sectionItems) head.w = width - 2 * PAD;
  if (!vertical) {
    for (let level = 1; level <= model.maxLevel; level += 1) {
      chips.push({ level, x: PAD + (level - 1) * INDENT, y: rulerY });
    }
  }

  // Relation lines run inside the tree; the end of a hidden or unseen card goes to its nearest
  // visible ancestor, and a line with no visible end is not drawn.
  const resolve = (id: string): string | null => {
    let current: DiagramNode | undefined = model.nodes.get(id);
    while (current) {
      const place = positions.get(current.id);
      if (place && place.type === 'card') return current.id;
      current = current.parentId ? model.nodes.get(current.parentId) : undefined;
    }
    return null;
  };
  const lines: LayoutLine[] = [];
  const seen = new Map<string, LayoutLine & { keys: string[] }>();
  for (const pair of model.pairs) {
    const project = model.nodes.get(pair.project)!;
    const projectEnd = resolve(pair.project);
    let best: { id: string; hidden: boolean } | null = null;
    let score = 9;
    for (const copy of model.copies.get(pair.team) ?? []) {
      const end = resolve(copy.id);
      if (!end) continue;
      const rank = (end === copy.id ? 0 : 2) + (copy.parentId === project.parentId ? 0 : 1);
      if (rank < score) {
        score = rank;
        best = { id: end, hidden: end !== copy.id };
      }
    }
    if (!projectEnd || !best || projectEnd === best.id) continue;
    const key = `${projectEnd}>${best.id}`;
    let line = seen.get(key);
    if (!line) {
      line = {
        kind: 'relation',
        from: projectEnd,
        to: best.id,
        hidden: projectEnd !== pair.project || best.hidden,
        keys: [],
      };
      seen.set(key, line);
      lines.push(line);
    }
    line.keys.push(`${pair.project}>${pair.team}`);
  }
  for (const [teamId, copies] of model.copies) {
    const shown = copies
      .filter((c) => positions.get(c.id)?.type === 'card')
      .map((c) => positions.get(c.id)!)
      .sort((a, b) => a.y - b.y || a.x - b.x);
    for (let i = 1; i < shown.length; i += 1) {
      lines.push({
        kind: 'copy',
        from: shown[i - 1]!.id,
        to: shown[i]!.id,
        hidden: false,
        keys: [`copy:${teamId}`],
      });
    }
  }

  return {
    orientation,
    items,
    positions,
    lines,
    chips,
    expansion: expanded,
    visibleChildren: kidsOf,
    width: Math.max(width, PAD * 2 + 300),
    height: y + PAD,
    rulerY,
    guideBottom: y - 14,
    visibleCount: items.filter((item) => item.type === 'card').length,
  };
}
