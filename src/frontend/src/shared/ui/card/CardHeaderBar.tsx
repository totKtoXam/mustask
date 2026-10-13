import type { ReactNode } from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import type { Theme } from '@mui/material/styles';
import { TypeLabel } from '../marks/TypeLabel';
import { CARD_ICONS_INSET, narrowCardQuery } from './cardContainer';
import { TypeStripe } from './TypeStripe';

/**
 * A field in a header row stands alone. The gap every field keeps under its box, to space
 * a stack of them, would make the row `layout.field.gap` taller than `density.controlHeight`
 * and put the box off the row's centre; the slot takes that gap back, so a 32 px title and
 * a 28 px assignee both stand centred in their rows and the header is 124.
 */
const takeBackFieldGap = (theme: Theme) => ({
  '& [data-field]': { marginBottom: `-${theme.layout.field.gap}px` },
});

export interface CardHeaderBarProps {
  /** The type's stored appearance token: the bar's colour and the type line's icon. */
  appearance: string;
  /** The type's name, e.g. «Пользовательская история». */
  typeLabel: string;
  /** The item's identifier, e.g. «MSK-US-2». */
  identifier: string;
  /** 1 on the page, 2 in the dialog, 3 inside a showcase section. */
  headingLevel: 1 | 2 | 3;
  /** The heading's id: what the dialog's `aria-labelledby` points at. */
  headingId?: string;
  /** An `InlineTextField size="title"` the host passes, with its label «Заголовок» hidden. */
  title: ReactNode;
  /** Row 3, left: the assignee (a `PersonField`, label hidden), the places kept. */
  meta?: ReactNode;
  /** Row 3, right, `layout.card.actionGap` apart: `SplitButton`, `IconAction`s, `MoreActionsMenu`. */
  actions?: ReactNode;
}

/**
 * The head of a work item card (tech-015 design § 5.9): the type's bar at the left over
 * the header's height only, then three rows of `density.controlHeight` — the type line
 * («<Тип> <ID>», which is the heading), the identifier and the title, and the assignee
 * with the actions — `headerRowGap` apart: 4 + 32 + 8 + 32 + 8 + 32 + 8 = 124.
 *
 * The dialog's «Развернуть на страницу» and «Закрыть» are not this bar's: `DialogShell`
 * draws them over the right end of row 1. Below `layout.card.stackBelow` — the shell's
 * width, read as a container query — row 3 wraps: the meta on its own line, the actions
 * under it at the right.
 */
export function CardHeaderBar({
  appearance,
  typeLabel,
  identifier,
  headingLevel,
  headingId,
  title,
  meta,
  actions,
}: CardHeaderBarProps) {
  return (
    <Box data-card-header="" sx={{ display: 'flex', alignItems: 'stretch' }}>
      <TypeStripe appearance={appearance} />
      <Box
        sx={(theme) => ({
          flex: 1,
          minWidth: 0,
          display: 'flex',
          flexDirection: 'column',
          rowGap: `${theme.layout.card.headerRowGap}px`,
          paddingTop: `${theme.layout.card.headerPadTop}px`,
          paddingBottom: `${theme.layout.card.headerPadBottom}px`,
          // The content starts `headerInset` from the card's edge: the bar is part of it.
          paddingLeft: `${theme.layout.card.headerInset - theme.layout.card.typeBar}px`,
          paddingRight: `${theme.layout.card.rightInset}px`,
        })}
      >
        <Typography
          id={headingId}
          variant="caption"
          component={`h${headingLevel}`}
          sx={(theme) => ({
            display: 'flex',
            alignItems: 'center',
            minHeight: theme.density.controlHeight,
            minWidth: 0,
            margin: 0,
            // Stops short of the dialog's two icons, which stand over this row's end.
            paddingRight: `var(${CARD_ICONS_INSET}, 0px)`,
          })}
        >
          <TypeLabel eyebrow appearance={appearance} name={`${typeLabel} ${identifier}`} />
        </Typography>
        <Box
          sx={(theme) => ({
            display: 'flex',
            alignItems: 'center',
            gap: 2,
            minHeight: theme.density.controlHeight,
            minWidth: 0,
          })}
        >
          <Typography
            variant="cardTitle"
            component="span"
            data-card-identifier=""
            sx={{ flexShrink: 0, whiteSpace: 'nowrap' }}
          >
            {identifier}
          </Typography>
          <Box sx={(theme) => ({ flex: 1, minWidth: 0, ...takeBackFieldGap(theme) })}>{title}</Box>
        </Box>
        <Box
          data-card-header-row="3"
          sx={(theme) => ({
            display: 'flex',
            alignItems: 'center',
            minHeight: theme.density.controlHeight,
            minWidth: 0,
            columnGap: 2,
            rowGap: `${theme.layout.card.headerRowGap}px`,
            [narrowCardQuery(theme)]: {
              flexWrap: 'wrap',
              '& > [data-card-meta]': { flexBasis: '100%' },
              '& > [data-card-actions]': { marginLeft: 'auto' },
            },
          })}
        >
          <Box
            data-card-meta=""
            sx={(theme) => ({ flex: '1 1 0', minWidth: 0, ...takeBackFieldGap(theme) })}
          >
            {meta}
          </Box>
          {actions && (
            <Box
              data-card-actions=""
              sx={(theme) => ({
                display: 'flex',
                alignItems: 'center',
                flexShrink: 0,
                gap: `${theme.layout.card.actionGap}px`,
              })}
            >
              {actions}
            </Box>
          )}
        </Box>
      </Box>
    </Box>
  );
}
