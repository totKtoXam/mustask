import { useState, type ReactNode } from 'react';
import Box from '@mui/material/Box';
import { CardSkeleton, ErrorBanner, NotFound, useNotice } from '../../../shared/ui';
import { useWorkItem } from '../hooks/queries';
import { useWorkItemAccess } from '../hooks/useWorkItemAccess';
import { useWorkItemDialog } from '../hooks/useWorkItemDialog';
import { cardTexts, loadingName } from '../lib/cardTexts';
import { notFoundLevel, type WorkItemsNotFoundLevel } from '../lib/notFoundLevel';
import { createFrameStore } from './card/frameStore';
import { DialogFrame, PublishFrame } from './card/KitFrames';
import { WorkItemCard } from './card/WorkItemCard';

export interface WorkItemCardDialogProps {
  organizationId: string;
  projectId: string;
  /**
   * The card changed the item — a save, a refresh — so what the host shows behind it is
   * stale: the host reads its own queries again (BR-C-21). The workitems queries are the
   * card's; the backlog's and the board's keys are the host's.
   */
  onChanged?: () => void;
}

/**
 * The work item card as a dialog over a page (feat-029 US-1, US-6; BR-C-1..BR-C-5): the
 * page — the backlog, the board — mounts it once, and it opens whenever the address has
 * `?workitem=<identifier>` (`useWorkItemDialog`). It reads the item, keys the card by the
 * identifier — a switch to a related item starts afresh — and shows the state the read
 * is in: the kit's skeleton while it loads, the error banner when the read failed,
 * «Рабочий элемент не найден» when it is not there, the card when it is — all in the one
 * shell the dialog draws from the first frame of the click (`DialogFrame`), which the card
 * fills when it arrives. Everything the card shows and does is `WorkItemCard`'s, the same
 * card the page hosts.
 *
 * Closing keeps the dialog drawn until it has faded out, then puts focus back on the row
 * or card that opened it if the browser has not (`restoreFocus`).
 */
export function WorkItemCardDialog({
  organizationId,
  projectId,
  onChanged,
}: WorkItemCardDialogProps) {
  const { identifier } = useWorkItemDialog();
  // The identifier last asked for stays drawn until the dialog has faded out.
  const [shown, setShown] = useState<string | null>(identifier);
  // The identifier the dialog was opened on: the row or card to give focus back to, however
  // many related items the dialog has switched through since.
  const [opener, setOpener] = useState<string | null>(identifier);
  if (identifier !== null && identifier !== shown) {
    if (shown === null) {
      setOpener(identifier);
    }
    setShown(identifier);
  }
  // Whether the dialog fills the window outlives a switch to a related item, which
  // draws a dialog of its own.
  const [expanded, setExpanded] = useState(false);
  const target = identifier ?? shown;
  if (target === null) {
    return null;
  }
  return (
    <CardDialog
      key={target}
      organizationId={organizationId}
      projectId={projectId}
      identifier={target}
      open={identifier !== null}
      expanded={expanded}
      onToggleExpanded={() => setExpanded((current) => !current)}
      onExited={() => {
        restoreFocus(opener ?? target);
        setShown((current) => (current === target ? null : current));
      }}
      onChanged={onChanged}
    />
  );
}

/** The dialog of one identifier: its read, its states and its card. */
function CardDialog({
  organizationId,
  projectId,
  identifier,
  open,
  expanded,
  onToggleExpanded,
  onExited,
  onChanged,
}: {
  organizationId: string;
  projectId: string;
  identifier: string;
  open: boolean;
  expanded: boolean;
  onToggleExpanded: () => void;
  onExited: () => void;
  onChanged?: () => void;
}) {
  const dialog = useWorkItemDialog();
  const query = useWorkItem(organizationId, projectId, identifier);
  const { show } = useNotice();
  // The role views (mod-016 design § 6.5), as the page's: the host asks, the card shows.
  const { readOnly, parentReadOnly, canDelete, canPurge } = useWorkItemAccess();
  // The level a save found gone, or the read's own answer.
  const [gone, setGone] = useState<WorkItemsNotFoundLevel | null>(null);
  // The card hands its frame to the shell below; the host draws no shell of its own.
  const [frames] = useState(createFrameStore);
  const { close } = dialog;

  // What the shell's panel holds while the card has published no frame: the loading
  // skeleton (also for the instant between the read and the card's first frame), the
  // failed read's banner, the not-found frame.
  let fallback: ReactNode = <CardSkeleton />;
  let card: ReactNode = null;
  const level = gone ?? (query.isError ? notFoundLevel(query.error) : null);
  if (level) {
    fallback = (
      <NotFound
        level={level}
        compact
        hint={cardTexts.frame.notFoundHint}
        action={{ label: 'Закрыть', onClick: () => close() }}
      />
    );
  } else if (query.isPending || (query.isError && query.isFetching)) {
    // The skeleton: a read, or the retry of a failed one, is running.
  } else if (query.isError) {
    fallback = (
      <Box sx={{ paddingX: 3 }}>
        <ErrorBanner
          error={query.error}
          title={cardTexts.frame.errorTitle}
          onRetry={() => void query.refetch()}
        />
      </Box>
    );
  } else {
    card = (
      <WorkItemCard
        organizationId={organizationId}
        projectId={projectId}
        item={query.data}
        mode="dialog"
        readOnly={readOnly}
        parentReadOnly={parentReadOnly}
        canDelete={canDelete}
        canPurge={canPurge}
        notify={show}
        onGone={setGone}
        onClose={close}
        onOpenItem={dialog.switchTo}
        onSaved={onChanged}
        onRefreshed={onChanged}
        // A Delete, a Restore and a Purge change what the page behind shows (design § 7); a
        // Purge also leaves nothing to look at, so the card closes (design § 6).
        onChanged={onChanged}
        onPurged={close}
        renderFrame={(content) => <PublishFrame store={frames} frame={content} />}
      />
    );
  }

  // One shell for every state, mounted once, so the dialog never fades out and in between
  // the loading and the card, and focus stays where the first open put it (design § 6).
  return (
    <>
      <DialogFrame
        store={frames}
        identifier={identifier}
        label={loadingName(identifier)}
        fallback={fallback}
        open={open}
        onClose={() => close()}
        expanded={expanded}
        onToggleExpanded={onToggleExpanded}
        onExited={onExited}
      />
      {card}
    </>
  );
}

/** A CSS attribute selector's string, escaped: the identifier is read as received. */
function quoted(value: string): string {
  return `"${value.replace(/["\\]/g, '\\$&')}"`;
}

/**
 * After the dialog has closed: if the browser has not put focus back on the title that
 * opened it — the opener was redrawn, or the address opened the dialog and nothing did —
 * focus goes to the title of the row or card with this identifier (the host marks both
 * with `data-identifier`), else to the page's main region (design § 4.1).
 */
function restoreFocus(identifier: string): void {
  const active = document.activeElement;
  // The shell is still in the page when it reports having exited, so focus left on its
  // «Закрыть» (a dialog the address opened has no opener to give it back to) is as lost
  // as focus on the body.
  if (active !== null && active !== document.body && active.closest('[role="dialog"]') === null) {
    return;
  }
  const opener = document.querySelector<HTMLElement>(
    `[data-identifier=${quoted(identifier)}] a[href]`,
  );
  (opener ?? document.querySelector<HTMLElement>('main'))?.focus();
}
