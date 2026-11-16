import Box from '@mui/material/Box';
import { useTheme, type Theme } from '@mui/material/styles';
import { structureDiagram as texts } from '../lib/areaTexts';
import {
  GUTTER,
  HIERARCHY_WIDTH,
  INDENT,
  PAD,
  RULER,
  type CardItem,
  type DiagramLayout,
  type LayoutLine,
} from '../lib/diagramLayout';
import type { DisplayMode } from '../hooks/useStructureSettings';

/**
 * The line layer of the diagram (feat-049 design § 4.6): one `<svg aria-hidden>` behind the
 * cards, and the legend that draws the same four strokes. The legend sample and the canvas line
 * read the same table, so they cannot drift apart (BR-SD-17); colour comes from theme roles.
 */

type LineKind = 'nesting' | 'relation' | 'copy' | 'hidden';

interface Stroke {
  colour: string;
  width: number;
  dash: string | undefined;
}

function stroke(theme: Theme, kind: LineKind): Stroke {
  switch (kind) {
    case 'nesting':
      return { colour: theme.palette.text.secondary, width: 1.5, dash: undefined };
    case 'relation':
      return { colour: theme.palette.primary.dark, width: 2.5, dash: '7 4' };
    case 'copy':
      return { colour: theme.palette.text.primary, width: 2.5, dash: '9 4 2.5 4' };
    case 'hidden':
      return { colour: theme.palette.primary.dark, width: 2.5, dash: '2.5 4' };
  }
}

const EMPHASISED_WIDTH = 4;
const DOT_RADIUS = 4;
const FADED_OPACITY = 0.15;

export function DiagramLegend() {
  const theme = useTheme();
  const entries: readonly [LineKind, string][] = [
    ['nesting', texts.legend.nesting],
    ['relation', texts.legend.relation],
    ['hidden', texts.legend.hidden],
  ];
  return (
    <Box
      sx={(t) => ({
        display: 'flex',
        flexWrap: 'wrap',
        alignItems: 'center',
        gap: 2,
        fontSize: t.typography.caption.fontSize,
        color: t.palette.text.secondary,
      })}
    >
      {entries.map(([kind, label]) => {
        const s = stroke(theme, kind);
        return (
          <Box
            key={kind}
            component="span"
            sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.5 }}
          >
            <svg width="34" height="12" aria-hidden="true">
              <path
                d="M1 6H33"
                stroke={s.colour}
                strokeWidth={s.width}
                strokeDasharray={s.dash}
                fill="none"
              />
            </svg>
            {label}
          </Box>
        );
      })}
    </Box>
  );
}

function joinPath(layout: DiagramLayout, card: CardItem, parent: CardItem): string {
  if (layout.orientation === 'h') {
    const x = parent.x + 10;
    return `M${x} ${parent.y + parent.h}V${card.y + card.h / 2}H${card.x}`;
  }
  const x1 = parent.x + parent.w / 2;
  const x2 = card.x + card.w / 2;
  const ym = card.y - 14;
  return `M${x1} ${parent.y + parent.h}V${ym}H${x2}V${card.y}`;
}

function relationPath(layout: DiagramLayout, line: LayoutLine, copy: boolean) {
  const a = layout.positions.get(line.from)!;
  const b = layout.positions.get(line.to)!;
  if (layout.orientation === 'h') {
    const port = PAD + HIERARCHY_WIDTH;
    const y1 = a.y + a.h / 2;
    const y2 = b.y + b.h / 2;
    const bulge = copy ? 10 : Math.min(GUTTER - 14, 24 + Math.abs(y2 - y1) / 3);
    return {
      d: `M${port} ${y1}C${port + bulge} ${y1} ${port + bulge} ${y2} ${port} ${y2}`,
      dot: [port, y1] as const,
    };
  }
  const xa = a.x + a.w / 2;
  const xb = b.x + b.w / 2;
  if (Math.abs(a.y - b.y) < 5) {
    const yb = a.y + a.h;
    const dip = copy ? 14 : 22 + Math.min(60, Math.abs(xb - xa) / 6);
    return {
      d: `M${xa} ${yb}C${xa} ${yb + dip} ${xb} ${yb + dip} ${xb} ${yb}`,
      dot: [xa, yb] as const,
    };
  }
  const upper = a.y < b.y ? a : b;
  const lower = a.y < b.y ? b : a;
  const xu = upper.x + upper.w / 2;
  const xl = lower.x + lower.w / 2;
  const yu = upper.y + upper.h;
  const yl = lower.y;
  const dy = (yl - yu) / 2;
  return {
    d: `M${xl} ${yl}C${xl} ${yl - dy} ${xu} ${yu + dy} ${xu} ${yu}`,
    dot: [xu, yu] as const,
  };
}

export function DiagramLines({
  layout,
  maxLevel,
  emphasised,
  mode,
  drawAll,
}: {
  layout: DiagramLayout;
  maxLevel: number;
  /** Keys of the emphasised lines while a node is chosen, else `null`. */
  emphasised: ReadonlySet<string> | null;
  mode: DisplayMode;
  /** Up to the line limit all lines are drawn; above it only the chosen branch's (OQ-6 A). */
  drawAll: boolean;
}) {
  const theme = useTheme();
  const horizontal = layout.orientation === 'h';
  const nesting = stroke(theme, 'nesting');
  const guides: string[] = [];
  if (horizontal) {
    for (let level = 1; level <= maxLevel; level += 1) {
      const x = PAD + (level - 1) * INDENT + 0.5;
      guides.push(`M${x} ${layout.rulerY + RULER}V${layout.guideBottom}`);
    }
  }
  const joins = layout.items.flatMap((item) => {
    if (item.type !== 'card' || !item.parentId) return [];
    const parent = layout.positions.get(item.parentId);
    if (!parent || parent.type !== 'card') return [];
    return [{ id: item.id, d: joinPath(layout, item, parent), gap: item.gap }];
  });
  const hidden = stroke(theme, 'hidden');

  return (
    <svg
      aria-hidden="true"
      width={layout.width}
      height={layout.height}
      style={{ position: 'absolute', left: 0, top: 0, pointerEvents: 'none' }}
    >
      {guides.map((d) => (
        <path
          key={d}
          d={d}
          stroke={theme.palette.divider}
          strokeWidth="1"
          strokeDasharray="1 3"
          fill="none"
        />
      ))}
      {joins.map((join) => (
        <path
          key={join.id}
          d={join.d}
          fill="none"
          stroke={nesting.colour}
          strokeWidth={nesting.width}
          strokeDasharray={join.gap ? hidden.dash : undefined}
        />
      ))}
      {layout.lines.map((line) => {
        const copy = line.kind === 'copy';
        const kind: LineKind = copy ? 'copy' : line.hidden ? 'hidden' : 'relation';
        const base = stroke(theme, kind);
        const isEmphasised = emphasised !== null && line.keys.some((key) => emphasised.has(key));
        if (!drawAll && !isEmphasised) return null;
        const greyed = emphasised !== null && !isEmphasised && mode === 'hl';
        const faded = emphasised !== null && !isEmphasised && mode === 'dim';
        const { d, dot } = relationPath(layout, line, copy);
        const colour = greyed ? theme.palette.shell.addAction : base.colour;
        return (
          <g
            key={`${line.kind}:${line.from}>${line.to}`}
            data-line={line.keys.join(' ')}
            opacity={faded ? FADED_OPACITY : 1}
          >
            <path
              d={d}
              fill="none"
              stroke={colour}
              strokeWidth={isEmphasised ? EMPHASISED_WIDTH : base.width}
              strokeDasharray={base.dash}
            />
            {!copy && <circle cx={dot[0]} cy={dot[1]} r={DOT_RADIUS} fill={colour} />}
          </g>
        );
      })}
    </svg>
  );
}
