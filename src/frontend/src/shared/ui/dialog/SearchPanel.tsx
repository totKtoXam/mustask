import type { KeyboardEvent, MouseEvent, ReactNode } from 'react';
import Box from '@mui/material/Box';
import ClickAwayListener from '@mui/material/ClickAwayListener';
import Paper from '@mui/material/Paper';
import Popper from '@mui/material/Popper';
import { surfaceSx } from '../layout/surfaceStyles';

export type SearchPanelCloseReason = 'clickAway' | 'escape';

export interface SearchPanelProps {
  /** The field the panel hangs under (the header field; on a narrow screen, the header). */
  anchorEl: HTMLElement | null;
  open: boolean;
  /** Click outside (not on the anchor) or Esc pressed inside the panel; the host closes it. */
  onClose: (reason: SearchPanelCloseReason) => void;
  /** The scope row's left: «Искать в:» and the scope menu. */
  scope?: ReactNode;
  /** The scope row's right: the permanent «Полный поиск» button. */
  fullSearch?: ReactNode;
  /** The body: the listbox, a message or a skeleton. It scrolls. */
  children?: ReactNode;
  /** The footer's key hints; hidden in `compact`. Absent: no footer. */
  keys?: ReactNode;
  /** A newer read is pending: the body is `aria-busy` and dimmed, its rows stay. */
  busy?: boolean;
  /** Narrow screens: the panel takes the viewport's width, no corners, no key hints. */
  compact?: boolean;
}

const DIMMED = 0.55;

/** The panel stays out of the focus's way: a press on a row or the background never takes it from the field. */
function keepFocus(event: MouseEvent<HTMLElement>) {
  const target = event.target;
  if (
    target instanceof Element &&
    target.closest('button, input, select, textarea, a:not([role="option"])')
  ) {
    return;
  }
  event.preventDefault();
}

/**
 * The search dropdown (feat-065 design § 1, § 3.1, D-9): a non-modal MUI `Popper` — not a
 * `Popover` or a `Dialog` — so the focus stays in the header field while the user types.
 * It wears the `Surface` card look, closes on a click outside the panel and its anchor,
 * and has no backdrop and no focus trap. Four slots: the scope row (`scope`, `fullSearch`,
 * always shown), the scrolling body (`children`), the key hints (`keys`). It fetches and
 * decides nothing: the host owns `open`, the rows and the keyboard.
 */
export function SearchPanel({
  anchorEl,
  open,
  onClose,
  scope,
  fullSearch,
  children,
  keys,
  busy = false,
  compact = false,
}: SearchPanelProps) {
  const anchorRect = anchorEl?.getBoundingClientRect();
  const anchorWidth = anchorRect?.width ?? 0;
  const anchorBottom = anchorRect?.bottom ?? 0;
  const handleKeyDown = (event: KeyboardEvent<HTMLElement>) => {
    if (event.key === 'Escape') {
      event.stopPropagation();
      onClose('escape');
    }
  };
  return (
    <Popper
      open={open && anchorEl !== null}
      anchorEl={anchorEl}
      placement="bottom-start"
      modifiers={[
        { name: 'offset', options: { offset: [0, compact ? 0 : 4] } },
        { name: 'flip', enabled: false },
        { name: 'preventOverflow', options: { padding: compact ? 0 : 16 } },
      ]}
      sx={(theme) => ({ zIndex: theme.zIndex.appBar + 1 })}
    >
      <ClickAwayListener
        onClickAway={(event) => {
          if (event.target instanceof Node && anchorEl?.contains(event.target)) return;
          onClose('clickAway');
        }}
      >
        <Paper
          elevation={0}
          square={compact}
          data-compact={compact ? 'true' : undefined}
          onMouseDown={keepFocus}
          onKeyDown={handleKeyDown}
          sx={(theme) => {
            const { width, maxHeight, viewportGap } = theme.layout.searchPanel;
            return {
              ...surfaceSx(theme, 'card'),
              display: 'flex',
              flexDirection: 'column',
              overflow: 'hidden',
              ...(compact
                ? {
                    width: '100vw',
                    maxHeight: `calc(100vh - ${anchorBottom}px)`,
                    borderRadius: 0,
                  }
                : {
                    width: `max(${anchorWidth}px, min(${width}px, 100vw - ${viewportGap}px))`,
                    maxHeight,
                  }),
            };
          }}
        >
          <Box
            sx={(theme) => ({
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              flex: 'none',
              gap: 1,
              paddingX: 1.5,
              paddingY: 0.5,
              borderBottom: `${theme.layout.dividerWidth}px solid ${theme.palette.shell.sectionRule}`,
            })}
          >
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75, minWidth: 0 }}>
              {scope}
            </Box>
            <Box sx={{ flex: 'none' }}>{fullSearch}</Box>
          </Box>
          <Box
            aria-busy={busy ? 'true' : undefined}
            sx={{ flex: '1 1 auto', minHeight: 0, overflowY: 'auto', opacity: busy ? DIMMED : 1 }}
          >
            {children}
          </Box>
          {keys && !compact ? (
            <Box
              sx={(theme) => ({
                flex: 'none',
                display: 'flex',
                flexWrap: 'wrap',
                gap: 1.75,
                paddingX: 1.5,
                paddingY: 0.75,
                fontSize: theme.typography.caption.fontSize,
                color: theme.palette.shell.cardLabel,
                borderTop: `${theme.layout.dividerWidth}px solid ${theme.palette.shell.sectionRule}`,
              })}
            >
              {keys}
            </Box>
          ) : null}
        </Paper>
      </ClickAwayListener>
    </Popper>
  );
}
