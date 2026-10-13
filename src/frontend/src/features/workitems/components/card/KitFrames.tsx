import { useLayoutEffect, useSyncExternalStore, type ReactNode } from 'react';
import { DialogShell, PanelShell, type DialogCloseReason } from '../../../../shared/ui';
import { closeGuardTexts } from '../../lib/cardTexts';
import type { WorkItemEdits } from '../../lib/edits';
import type { CardFrameProps } from './cardFrame';
import type { FrameStore } from './frameStore';
import { useCardParts } from './useCardParts';

/**
 * The card on a page (feat-029 design § 5): the shell's trail above, then one white
 * panel on the application background. The page scrolls, not the card, and the type
 * line is the page's `h1`. No «Развернуть на страницу» and no «Закрыть»: the trail
 * leads back.
 */
export function PageFrame({ frame, trail }: { frame: CardFrameProps; trail?: ReactNode }) {
  const parts = useCardParts(frame, 1);
  return (
    <PanelShell trail={trail} header={parts.header} band={parts.band} alert={parts.alert}>
      {parts.body}
    </PanelShell>
  );
}

/**
 * The card's `renderFrame` in a dialog: publishes the frame to the dialog's shell
 * (`DialogFrame`) before the browser paints, and takes it back when the card goes — the
 * item is gone, or the dialog is — so the shell falls back to the host's own frame.
 */
export function PublishFrame({ store, frame }: { store: FrameStore; frame: CardFrameProps }) {
  useLayoutEffect(() => {
    store.set(frame);
  });
  useLayoutEffect(() => () => store.set(null), [store]);
  return null;
}

/**
 * The card as a dialog over a page (design § 4.1): 90 % of the window, no dimming, the
 * header and the band fixed and the body scrolling, named by the type line (`h2`). Its
 * «Закрыть», Esc and a click outside ask «Закрыть без сохранения?» while edits stand
 * (D-4), and none of them closes it while a save runs. «Развернуть на страницу» is a
 * plain callback: the host opens the page and carries the edits.
 *
 * It is the one shell of the dialog in every state (design § 6): until the card has
 * published a frame to `store` it is named `label` and holds `fallback` — the skeleton,
 * the failed read's banner, the not-found frame — and keeps its two icons; when the
 * card is there, the shell takes the card's header, band, notice and body, and is named
 * by the type line. Nothing is mounted or unmounted between the two.
 */
export function DialogFrame({
  store,
  identifier,
  label,
  fallback,
  open = true,
  onClose,
  onExpand,
  onExited,
}: {
  store: FrameStore;
  /** The item's identifier: the question's text names it before the card is there too. */
  identifier: string;
  /** The dialog's name until the card names it by its type line. */
  label: string;
  /** What the panel holds while the card has published no frame. */
  fallback: ReactNode;
  open?: boolean;
  /** The user chose to close — after the question, if edits stood. */
  onClose: (reason: DialogCloseReason) => void;
  /** «Развернуть на страницу»; the edits are the card's, `undefined` before it is there. */
  onExpand: (edits?: WorkItemEdits) => void;
  /** After the dialog has closed: the host restores focus if the opener is gone. */
  onExited?: () => void;
}) {
  const frame = useSyncExternalStore(store.subscribe, store.get);
  const parts = useCardParts(frame, 2);
  const guard = closeGuardTexts(identifier);
  return (
    <DialogShell
      open={open}
      onClose={onClose}
      size="card"
      header={parts.header}
      band={parts.band}
      alert={parts.alert}
      labelledBy={frame?.headingId}
      label={frame ? undefined : label}
      busy={frame?.busy}
      onExpand={() => onExpand(frame?.edits)}
      guard={{
        dirty: frame?.dirty ?? false,
        title: guard.title,
        text: guard.text,
        discardLabel: guard.confirmLabel,
        cancelLabel: guard.cancelLabel,
      }}
      onExited={onExited}
    >
      {frame ? parts.body : fallback}
    </DialogShell>
  );
}
