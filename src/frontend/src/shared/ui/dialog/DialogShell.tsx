import { useEffect, useId, useState, type ReactNode } from 'react';
import Box from '@mui/material/Box';
import Dialog from '@mui/material/Dialog';
import DialogActions from '@mui/material/DialogActions';
import DialogContent from '@mui/material/DialogContent';
import DialogTitle from '@mui/material/DialogTitle';
import useMediaQuery from '@mui/material/useMediaQuery';
import { useTheme, type Theme } from '@mui/material/styles';
import { IconAction } from '../actions/IconAction';
import { CARD_ICONS_INSET, cardContainerSx } from '../card/cardContainer';
import { ThinCloseIcon, ThinExpandIcon } from '../icons/ThinIcons';
import { ConfirmDialog } from '../ConfirmDialog';

/** Why a dialog is being closed: Escape, a click outside it, or its «Закрыть». */
export type DialogCloseReason = 'escape' | 'backdrop' | 'close';

/** The unsaved-changes confirmation a dismissal of a dirty dialog opens first. */
export interface DialogGuard {
  dirty: boolean;
  /** The question's title, the host's. */
  title: string;
  /** What will be lost; the item's identifier belongs here. */
  text: ReactNode;
  /** The destructive primary: «Закрыть без сохранения». */
  discardLabel: string;
  /** Default «Продолжить редактирование»; it holds the initial focus, so Enter never discards. */
  cancelLabel?: string;
}

export interface DialogShellProps {
  open: boolean;
  onClose: (reason: DialogCloseReason) => void;
  /** `confirm` is MUI's xs, `form` sm, `card` 90 % × 90 % of the window (`layout.card.dialogShare`). */
  size: 'confirm' | 'form' | 'card';
  /** A plain title: the dialog's `h2` with its «Закрыть». */
  title?: string;
  /** The fixed area on top: a `CardHeaderBar`. */
  header?: ReactNode;
  /** The fixed area under it: a `PropertyBand`. */
  band?: ReactNode;
  /** The fixed area between the band and the panel: an `InlineNotice`. */
  alert?: ReactNode;
  /** A paragraph under the title (`confirm`, `form`). */
  lead?: ReactNode;
  /** The panel; it scrolls. */
  children: ReactNode;
  /** The actions (`confirm`, `form`); the card has none. */
  footer?: ReactNode;
  /** The heading's id when there is no `title` — the `CardHeaderBar`'s `headingId`. */
  labelledBy?: string;
  /** The dialog's name when there is no heading to point at — a card still loading or failed. */
  label?: string;
  /** A request is running: nothing closes the dialog and its two icons are inert. */
  busy?: boolean;
  /** Draws «Развернуть на страницу»; a plain callback, the host navigates. Never guarded. */
  onExpand?: () => void;
  /** Opt-in: a dismissal of a dirty dialog asks first. */
  guard?: DialogGuard;
  /** The width below which the dialog is full screen; `false` never. Default `md` (1024 px) for the card. */
  fullScreenBelow?: 'md' | false;
  /** After it has closed: the host restores focus if the opener is gone. */
  onExited?: () => void;
}

const EXPAND_LABEL = 'Развернуть на страницу';
const CLOSE_LABEL = 'Закрыть';
const GUARD_CANCEL_LABEL = 'Продолжить редактирование';

const MAX_WIDTH = { confirm: 'xs', form: 'sm', card: false } as const;

/** The card's Paper: 90 % × 90 %, no margin, no height cap, a column, and the card container. */
function cardPaperSx(theme: Theme, fullScreen: boolean) {
  const share = `${theme.layout.card.dialogShare * 100}%`;
  return {
    display: 'flex',
    flexDirection: 'column',
    position: 'relative',
    // MUI's Paper caps its height at `100% - 64px` and keeps a 32 px margin, which
    // would break the 90 %.
    margin: 0,
    maxHeight: 'none',
    maxWidth: 'none',
    width: fullScreen ? '100%' : share,
    height: fullScreen ? '100%' : share,
    borderRadius: fullScreen ? 0 : `${theme.shape.borderRadius}px`,
    // The header and the band read this container; the confirm and form sizes never
    // declare it — a width fixed by `inline-size` containment cannot follow content.
    ...cardContainerSx,
  } as const;
}

/**
 * Any modal window over a page (tech-015 design § 5.10): the work item card
 * (`size="card"`), a form in a dialog, a confirmation. MUI's `Dialog`, `role="dialog"`
 * for every size, named by its heading (`title`, or `labelledBy`) or, with no heading to
 * point at, by `label`.
 *
 * The card size draws **no dimming**: its backdrop is `invisible` — still rendered, so a
 * click outside still reaches `onClose('backdrop')` and the guard — and MUI's own
 * elevation-24 shadow separates it from the page. A `ConfirmDialog` opened over it
 * dims; a question does. Its two icons, «Развернуть на страницу» and «Закрыть», are the
 * first controls in the DOM, `layout.card.dialogIconGap` apart and the ✕
 * `dialogIconInset` from the edge.
 *
 * With a `guard` whose `dirty` is true, Escape, the outside click and «Закрыть» first
 * open a confirmation, and `onClose` is called only when the user chooses to discard.
 * While `busy` none of them closes. Leaving the page by navigation is not guarded.
 */
export function DialogShell({
  open,
  onClose,
  size,
  title,
  header,
  band,
  alert,
  lead,
  children,
  footer,
  labelledBy,
  label,
  busy = false,
  onExpand,
  guard,
  fullScreenBelow,
  onExited,
}: DialogShellProps) {
  const theme = useTheme();
  const titleId = useId();
  const [confirming, setConfirming] = useState<DialogCloseReason | null>(null);
  const card = size === 'card';
  const below = fullScreenBelow ?? (card ? 'md' : false);
  const narrow = useMediaQuery(theme.breakpoints.down('md'));
  const fullScreen = below === 'md' && narrow;

  // Exactly one name: a heading of its own, a heading elsewhere, or a label.
  const names = [title, labelledBy, label].filter((name) => name !== undefined).length;
  useEffect(() => {
    if (import.meta.env.DEV && names !== 1) {
      console.warn(
        'DialogShell needs exactly one of `title`, `labelledBy` and `label` to name the dialog.',
      );
    }
  }, [names]);

  function dismiss(reason: DialogCloseReason) {
    if (busy) return;
    if (guard?.dirty) {
      setConfirming(reason);
      return;
    }
    onClose(reason);
  }

  const labelledByHeading = title === undefined ? labelledBy : titleId;
  const iconsReach =
    theme.layout.card.dialogIconInset +
    theme.layout.iconAction.bar.box +
    (onExpand ? theme.layout.card.dialogIconGap + theme.layout.iconAction.bar.box : 0);

  return (
    <>
      <Dialog
        open={open}
        onClose={(_event, reason) => dismiss(reason === 'escapeKeyDown' ? 'escape' : 'backdrop')}
        maxWidth={MAX_WIDTH[size]}
        fullWidth={!card}
        fullScreen={fullScreen}
        aria-labelledby={labelledByHeading}
        slotProps={{
          // `invisible`: no fill, but the element — and the outside click — stay.
          backdrop: card ? { invisible: true } : undefined,
          paper: {
            // MUI names a dialog by `aria-labelledby` unconditionally, and a generated
            // id would point at nothing when `label` names it instead.
            ...(label === undefined ? {} : { 'aria-labelledby': undefined, 'aria-label': label }),
            sx: card ? cardPaperSx(theme, fullScreen) : undefined,
          },
          transition: { onExited },
        }}
      >
        {card && (
          <Box
            data-dialog-icons=""
            sx={(current) => ({
              position: 'absolute',
              zIndex: 1,
              top: current.layout.card.headerPadTop,
              right: current.layout.card.dialogIconInset,
              display: 'flex',
              gap: `${current.layout.card.dialogIconGap}px`,
            })}
          >
            {onExpand && (
              <IconAction
                label={EXPAND_LABEL}
                icon={<ThinExpandIcon />}
                inactive={busy}
                onClick={onExpand}
              />
            )}
            <IconAction
              label={CLOSE_LABEL}
              icon={<ThinCloseIcon />}
              inactive={busy}
              onClick={() => dismiss('close')}
            />
          </Box>
        )}
        {title !== undefined && (
          <DialogTitle id={titleId} sx={{ paddingRight: (current) => current.spacing(7) }}>
            {title}
          </DialogTitle>
        )}
        {title !== undefined && !card && (
          <Box
            sx={(current) => ({
              position: 'absolute',
              top: current.spacing(1),
              right: current.spacing(1),
            })}
          >
            <IconAction
              label={CLOSE_LABEL}
              icon={<ThinCloseIcon />}
              inactive={busy}
              onClick={() => dismiss('close')}
            />
          </Box>
        )}
        {header && (
          <Box
            data-dialog-area="header"
            sx={{
              [CARD_ICONS_INSET]: `${Math.max(0, iconsReach - theme.layout.card.rightInset)}px`,
            }}
          >
            {header}
          </Box>
        )}
        {band && <Box data-dialog-area="band">{band}</Box>}
        {alert && <Box data-dialog-area="alert">{alert}</Box>}
        {lead && (
          <Box data-dialog-area="lead" sx={{ paddingInline: 3, paddingBottom: 1 }}>
            {lead}
          </Box>
        )}
        {card ? (
          <Box
            data-dialog-area="panel"
            sx={{ flex: 1, minHeight: 0, overflowY: 'auto', scrollbarGutter: 'stable' }}
          >
            {children}
          </Box>
        ) : (
          <DialogContent>{children}</DialogContent>
        )}
        {footer && <DialogActions>{footer}</DialogActions>}
      </Dialog>
      {guard && (
        <ConfirmDialog
          open={confirming !== null}
          title={guard.title}
          confirmLabel={guard.discardLabel}
          cancelLabel={guard.cancelLabel ?? GUARD_CANCEL_LABEL}
          destructive
          cancelAutoFocus
          onConfirm={() => {
            const reason = confirming;
            setConfirming(null);
            if (reason) onClose(reason);
          }}
          onClose={() => setConfirming(null)}
        >
          {guard.text}
        </ConfirmDialog>
      )}
    </>
  );
}
