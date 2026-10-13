import type { ReactNode } from 'react';
import Box from '@mui/material/Box';
import type { Theme } from '@mui/material/styles';
import { Surface } from '../layout/Surface';
import { narrowCardQuery } from './cardContainer';

export interface PropertyBandProps {
  /** Each block a stack of fields with `labelPlacement="start"`. One block is fine. */
  blocks: [ReactNode, ReactNode?];
  /** Top right: «Изменён: 05.10.2026 14:30». */
  meta?: ReactNode;
  /** Bottom right: a `BoxedTabs`, against the band's lower edge. */
  tabs?: ReactNode;
}

function gridSx(theme: Theme, blockCount: number) {
  const { card } = theme.layout;
  const block = `minmax(${card.bandBlockMin}px, ${card.bandBlockWidth}px)`;
  return {
    boxSizing: 'border-box',
    display: 'grid',
    // The blocks are fixed, the free track takes what is left and is empty, the aside is
    // as wide as its tabs: the room is not shared out to the blocks.
    gridTemplateColumns: `${Array(blockCount).fill(block).join(' ')} minmax(0, 1fr) auto`,
    columnGap: `${card.bandBlockGap}px`,
    minHeight: card.bandMinHeight,
    padding: `${card.bandPaddingTop}px ${card.rightInset}px 0 ${card.headerInset}px`,
    // Narrow: the blocks stack in one column, the aside under them; no minimum height.
    [narrowCardQuery(theme)]: {
      gridTemplateColumns: `minmax(${card.bandBlockMin}px, 1fr)`,
      minHeight: 0,
      rowGap: `${card.bandRowGap}px`,
      '& > [data-band-block], & > [data-band-aside]': { gridColumn: 1 },
    },
  } as const;
}

/**
 * The grey strip under the card's header (tech-015 design § 5.9): label-left properties in
 * two blocks, a stamp at the top right and the section tabs at the bottom right. The
 * blocks are fixed tracks of `layout.card.bandBlockWidth` (the reference's pitch is that
 * and `bandBlockGap`), shrinking toward `bandBlockMin` only where the card is too narrow
 * for both and the aside.
 *
 * It keeps `bandMinHeight` with its rows empty, so a place kept for a property arrives
 * without moving what is below. The label column and the text's x are the fields': a
 * field with `labelPlacement="start"` stands in `layout.card.bandLabelWidth` by default.
 */
export function PropertyBand({ blocks, meta, tabs }: PropertyBandProps) {
  const [first, second] = blocks;
  const blockCount = second === undefined ? 1 : 2;
  const blockSx = (theme: Theme) => ({
    minWidth: 0,
    display: 'flex',
    flexDirection: 'column',
    rowGap: `${theme.layout.card.bandRowGap}px`,
  });
  return (
    <Surface variant="band" data-property-band="">
      <Box sx={(theme) => gridSx(theme, blockCount)}>
        <Box data-band-block="1" sx={[blockSx, { gridColumn: 1 }]}>
          {first}
        </Box>
        {second !== undefined && (
          <Box data-band-block="2" sx={[blockSx, { gridColumn: 2 }]}>
            {second}
          </Box>
        )}
        <Box
          data-band-aside=""
          sx={{
            gridColumn: blockCount + 2,
            minWidth: 0,
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'flex-end',
          }}
        >
          {meta && (
            <Box
              data-band-meta=""
              sx={(theme) => ({
                ...theme.typography.caption,
                maxWidth: '100%',
                color: theme.palette.shell.cardLabel,
                overflow: 'hidden',
                textOverflow: 'ellipsis',
                whiteSpace: 'nowrap',
              })}
            >
              {meta}
            </Box>
          )}
          {tabs && (
            // The strip never shrinks, and it stands against the band's lower edge.
            <Box data-band-tabs="" sx={{ marginTop: 'auto', flexShrink: 0 }}>
              {tabs}
            </Box>
          )}
        </Box>
      </Box>
    </Surface>
  );
}
