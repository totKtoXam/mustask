import type { ReactNode } from 'react';
import Box from '@mui/material/Box';
import { ColumnLayout } from '../layout/ColumnLayout';
import { VisuallyHidden } from '../text/VisuallyHidden';
import { Circle, FieldShape, LoadingRegion, Shape } from './skeletonParts';

export interface CardSkeletonProps {
  /**
   * A visually hidden heading of the given level, so the page has its `h1` while the card
   * loads: the page passes `{ text: 'Рабочий элемент MSK-US-2', level: 1 }`. The dialog
   * passes none and is named by `DialogShell`'s `label`.
   */
  heading?: { text: string; level: 1 | 2 };
}

/** A section's head as a shape: 24 high, the section head's own line. */
const HEAD_HEIGHT = 24;
const LINE_HEIGHT = 16;

function SectionShape({ children }: { children: ReactNode }) {
  return (
    <Box>
      <Box sx={{ mb: 1.5 }}>
        <Shape width="40%" height={HEAD_HEIGHT} />
      </Box>
      {children}
    </Box>
  );
}

/**
 * The card while it loads (tech-015 design § 5.12): its header, its band and its body in
 * three generic columns — the type is not known yet, so the bar is a neutral one and the
 * columns are the ones every card has. Sizes come from the card's own tokens, so the
 * loaded card lands where the skeleton was.
 *
 * One `role="status"` «Загрузка данных» with every shape hidden; `heading` adds the page's
 * visually hidden heading beside it, outside the live region.
 */
export function CardSkeleton({ heading }: CardSkeletonProps) {
  return (
    <>
      {heading && <VisuallyHidden component={`h${heading.level}`}>{heading.text}</VisuallyHidden>}
      <LoadingRegion>
        <Box data-card-skeleton-header="" sx={{ display: 'flex', alignItems: 'stretch' }}>
          <Box
            sx={(theme) => ({
              width: theme.layout.card.typeBar,
              flexShrink: 0,
              backgroundColor: theme.palette.action.hover,
            })}
          />
          <Box
            sx={(theme) => ({
              flex: 1,
              minWidth: 0,
              display: 'flex',
              flexDirection: 'column',
              rowGap: `${theme.layout.card.headerRowGap}px`,
              paddingTop: `${theme.layout.card.headerPadTop}px`,
              paddingBottom: `${theme.layout.card.headerPadBottom}px`,
              paddingLeft: `${theme.layout.card.headerInset - theme.layout.card.typeBar}px`,
              paddingRight: `${theme.layout.card.rightInset}px`,
            })}
          >
            <Box
              sx={(theme) => ({
                height: theme.density.controlHeight,
                display: 'flex',
                alignItems: 'center',
              })}
            >
              <Shape width={220} height={LINE_HEIGHT} />
            </Box>
            <Box
              sx={(theme) => ({
                height: theme.density.controlHeight,
                display: 'flex',
                alignItems: 'center',
              })}
            >
              <Shape width="60%" height={24} />
            </Box>
            <Box
              sx={(theme) => ({
                height: theme.density.controlHeight,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                columnGap: 2,
              })}
            >
              <Box sx={{ display: 'flex', alignItems: 'center', columnGap: 1, minWidth: 0 }}>
                <Circle />
                <Shape width={160} height={LINE_HEIGHT} />
              </Box>
              <Shape width={112} height={32} />
            </Box>
          </Box>
        </Box>

        <Box
          data-card-skeleton-band=""
          sx={(theme) => ({
            boxSizing: 'border-box',
            display: 'flex',
            justifyContent: 'space-between',
            columnGap: 2,
            minHeight: theme.layout.card.bandMinHeight,
            backgroundColor: theme.palette.background.default,
            padding: `${theme.layout.card.bandPaddingTop}px ${theme.layout.card.rightInset}px 0 ${theme.layout.card.headerInset}px`,
          })}
        >
          <Box
            sx={(theme) => ({
              width: theme.layout.card.bandBlockWidth,
              maxWidth: '100%',
              display: 'flex',
              flexDirection: 'column',
              rowGap: `${theme.layout.card.bandRowGap}px`,
            })}
          >
            <FieldShape placement="start" />
            <FieldShape placement="start" />
          </Box>
          <Shape width={160} height={LINE_HEIGHT} />
        </Box>

        <ColumnLayout
          main={
            <SectionShape>
              <Box sx={{ display: 'flex', flexDirection: 'column', rowGap: 1.5 }}>
                {Array.from({ length: 6 }, (_unused, index) => (
                  <Shape key={index} width={index === 5 ? '60%' : '100%'} height={LINE_HEIGHT} />
                ))}
              </Box>
            </SectionShape>
          }
          side={
            <SectionShape>
              <FieldShape placement="top" />
              <FieldShape placement="top" />
              <FieldShape placement="top" />
            </SectionShape>
          }
          extra={
            <SectionShape>
              <FieldShape placement="top" />
              <FieldShape placement="top" />
            </SectionShape>
          }
        />
      </LoadingRegion>
    </>
  );
}
