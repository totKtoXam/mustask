import type { MouseEvent, ReactNode } from 'react';
import { Link as RouterLink } from 'react-router-dom';
import Box from '@mui/material/Box';
import ListItemButton from '@mui/material/ListItemButton';
import Typography from '@mui/material/Typography';
import type { PassThrough } from '../passThrough';

export interface SearchOptionProps extends Omit<PassThrough<HTMLElement>, 'role' | 'tabIndex'> {
  /** Required: the listbox points at the active option by its id (`aria-activedescendant`). */
  id: string;
  /** `result` (default) is a row of the panel; `more` is the plain «Показать ещё» row with no link. */
  variant?: 'result' | 'more';
  /** The row is the combobox's active option: tint, a 3 px accent edge, `aria-selected` and `↵`. */
  active?: boolean;
  /** The row's real address: a link, so middle click and Ctrl-click work. `result` only. */
  to?: string;
  /** The type icon, drawn by the host. */
  icon?: ReactNode;
  /** The first line; may carry a `HighlightedText`. */
  primary: ReactNode;
  /** The grey second line. */
  secondary?: ReactNode;
  /** The whole text for the native hover title of a cut line. */
  title?: string;
  /** A colour stripe at the row's left (department, team, project); absent keeps an empty spacer. */
  stripe?: string;
  onClick?: (event: MouseEvent<HTMLElement>) => void;
  /** The pointer moved onto the row: the host makes it active (one highlight only). */
  onPointerEnter?: () => void;
}

const ENTER = '↵';

/**
 * One option of the search panel's listbox (feat-065 design § 1, § 3.1): `role="option"`,
 * never a Tab stop (`tabIndex` -1; focus stays in the field), an icon, one primary and
 * one secondary line cut with an ellipsis, an optional 4 px stripe and a `↵` hint on the
 * active row. The `result` variant is a router link; `more` is a plain row. It is drawn
 * inside an `li role="presentation"`, so the host's group is a `ul`. It knows no
 * search: the host passes every text.
 */
export function SearchOption({
  variant = 'result',
  active = false,
  to,
  icon,
  primary,
  secondary,
  title,
  stripe,
  onClick,
  onPointerEnter,
  ...passThrough
}: SearchOptionProps) {
  const more = variant === 'more';
  const link = !more && to !== undefined;
  const linkProps = link ? { component: RouterLink, to } : {};
  return (
    <li role="presentation">
      <ListItemButton
        {...linkProps}
        role="option"
        tabIndex={-1}
        aria-selected={active}
        data-active={active ? 'true' : undefined}
        title={title}
        disableRipple
        onClick={onClick}
        onPointerEnter={onPointerEnter}
        sx={(theme) => ({
          gap: 1.5,
          alignItems: 'center',
          minHeight: more ? theme.spacing(4.5) : theme.layout.searchOption.height,
          paddingY: 0.75,
          paddingLeft: 1.5,
          paddingRight: 1.5,
          borderLeft: `${theme.layout.selectedMarkerWidth}px solid transparent`,
          color: more ? theme.palette.primary.dark : theme.palette.text.primary,
          textDecoration: 'none',
          '&:hover': { textDecoration: 'none', backgroundColor: 'transparent' },
          ...(active
            ? {
                backgroundColor: theme.palette.shell.rowSelected,
                borderLeftColor: theme.palette.primary.main,
                '&:hover': { backgroundColor: theme.palette.shell.rowSelected },
              }
            : {}),
        })}
        {...passThrough}
      >
        {more ? null : (
          <Box
            aria-hidden
            data-stripe={stripe ? 'true' : undefined}
            sx={(theme) => ({
              flex: 'none',
              width: theme.spacing(0.5),
              height: theme.layout.searchOption.stripeHeight,
              marginRight: theme.spacing(-0.5),
              borderRadius: theme.spacing(0.25),
              backgroundColor: stripe ?? 'transparent',
            })}
          />
        )}
        {icon ? (
          <Box
            aria-hidden
            sx={(theme) => ({
              display: 'inline-flex',
              flex: 'none',
              color: more ? 'inherit' : theme.palette.text.secondary,
            })}
          >
            {icon}
          </Box>
        ) : null}
        <Box sx={{ flex: 1, minWidth: 0 }}>
          <Typography variant="body1" component="div" noWrap sx={{ color: 'inherit' }}>
            {primary}
          </Typography>
          {secondary ? (
            <Typography
              variant="caption"
              component="div"
              noWrap
              sx={(theme) => ({ color: theme.palette.shell.cardLabel })}
            >
              {secondary}
            </Typography>
          ) : null}
        </Box>
        <Box
          component="kbd"
          aria-hidden
          sx={(theme) => ({
            font: 'inherit',
            fontSize: theme.typography.pxToRem(11),
            color: theme.palette.shell.cardLabel,
            visibility: active ? 'visible' : 'hidden',
          })}
        >
          {ENTER}
        </Box>
      </ListItemButton>
    </li>
  );
}
