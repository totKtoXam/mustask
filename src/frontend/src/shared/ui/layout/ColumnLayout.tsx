import type { ReactNode } from 'react';
import Box from '@mui/material/Box';
import type { Theme } from '@mui/material/styles';

export interface ColumnLayoutProps {
  /** The texts. Always drawn; it is the first column, and the only one below `twoMin`. */
  main: ReactNode;
  /** The property sections. */
  side?: ReactNode;
  /** The related work and the kept places. */
  extra?: ReactNode;
  /** The container's content width from which three columns stand. Default `layout.columns.threeMin`. */
  threeMin?: number;
  /** The container's content width from which two columns stand. Default `layout.columns.twoMin`. */
  twoMin?: number;
}

/** The name of the container this layout declares and queries: `@container kit-columns (…)`. */
const COLUMNS_CONTAINER = 'kit-columns';

function present(node: ReactNode): boolean {
  return node !== undefined && node !== null && node !== false;
}

type AreaName = 'main' | 'aside1' | 'aside2';

/**
 * The grid's rules for the areas that are drawn. A column with no content takes no
 * track: with one side area the wide layout is two columns, with none it is `main` alone.
 */
function gridSx(theme: Theme, asides: number, twoMin: number, threeMin: number) {
  const { columns, section } = theme.layout;
  const side = `${columns.sideWidth}px`;
  const stacked = ['"main"', '"aside1"', '"aside2"'].slice(0, asides + 1).join(' ');
  const query = (min: number) => `@container ${COLUMNS_CONTAINER} (min-width: ${min}px)`;
  return {
    display: 'grid',
    // One column: the areas stacked, `section.gap` between them as between sections.
    gridTemplateColumns: 'minmax(0, 1fr)',
    gridTemplateAreas: stacked,
    columnGap: `${columns.gap}px`,
    rowGap: `${section.gap}px`,
    ...(asides === 0
      ? {}
      : {
          // Two columns: `extra` follows `side` directly while `main` spans both.
          [query(twoMin)]: {
            gridTemplateColumns: `minmax(0, 1fr) ${side}`,
            gridTemplateRows: asides === 2 ? 'auto 1fr' : 'auto',
            gridTemplateAreas: asides === 2 ? '"main aside1" "main aside2"' : '"main aside1"',
          },
        }),
    ...(asides === 2
      ? {
          [query(threeMin)]: {
            gridTemplateColumns: `minmax(0, 1fr) ${side} ${side}`,
            gridTemplateRows: 'auto',
            gridTemplateAreas: '"main aside1 aside2"',
          },
        }
      : {}),
  };
}

/** `area` is the host's name for the column; the grid's own name for it is `grid`. */
function Area({
  area,
  grid,
  children,
}: {
  area: 'main' | 'side' | 'extra';
  grid: AreaName;
  children: ReactNode;
}) {
  return (
    <Box
      data-area={area}
      sx={(theme) => ({
        gridArea: grid,
        minWidth: 0,
        // Where two consecutive sections get their 32 px: a section has no margin.
        display: 'flex',
        flexDirection: 'column',
        gap: `${theme.layout.section.gap}px`,
      })}
    >
      {children}
    </Box>
  );
}

/**
 * The body of a card (tech-015 design § 5.8): the texts, the property sections and the
 * related work in three, two or one column — decided by the container's own width, not
 * the window's, so the same body is right in the dialog, on the page and in the showcase.
 *
 * Two elements, because an element cannot query itself: the outer one is the container
 * and carries the padding, its child is the grid and holds the `@container` rules.
 * jsdom evaluates no container query; the tracks are measured in a browser (E2E 28.7).
 */
export function ColumnLayout({ main, side, extra, threeMin, twoMin }: ColumnLayoutProps) {
  const asides = [side, extra].filter(present);
  return (
    <Box
      data-column-layout=""
      sx={(theme) => ({
        boxSizing: 'border-box',
        containerType: 'inline-size',
        containerName: COLUMNS_CONTAINER,
        paddingTop: `${theme.layout.columns.top}px`,
        paddingLeft: `${theme.layout.columns.gutter}px`,
        paddingRight: `${theme.layout.columns.gutter}px`,
        paddingBottom: `${theme.layout.columns.bottom}px`,
      })}
    >
      <Box
        data-column-grid=""
        sx={(theme) =>
          gridSx(
            theme,
            asides.length,
            twoMin ?? theme.layout.columns.twoMin,
            threeMin ?? theme.layout.columns.threeMin,
          )
        }
      >
        <Area area="main" grid="main">
          {main}
        </Area>
        {present(side) && (
          <Area area="side" grid="aside1">
            {side}
          </Area>
        )}
        {present(extra) && (
          <Area area="extra" grid={present(side) ? 'aside2' : 'aside1'}>
            {extra}
          </Area>
        )}
      </Box>
    </Box>
  );
}
