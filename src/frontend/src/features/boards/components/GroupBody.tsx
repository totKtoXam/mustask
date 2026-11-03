import { useEffect, useRef, type ReactNode } from 'react';
import Box from '@mui/material/Box';
import { ActionButton, InlineEmpty, InlineNotice, TableSkeleton } from '../../../shared/ui';
import type { UseGroupRead } from '../hooks/useGroupRead';
import { aggregateTexts, loadedMore } from '../lib/aggregateTexts';

/**
 * What a group, a lane cell or the stream shows below its header (design § 3): the states of one
 * `useGroupRead`, and the rows or cards the caller draws once there are some, then
 * «Показать ещё». The header keeps its name and count in every state, so it is not here.
 */
export function GroupBody<T extends { id: string }>({
  read,
  stream,
  skeleton = 'rows',
  announce,
  children,
}: {
  read: UseGroupRead<T>;
  /** «Без группировки»: the neutral copy of the stream column. */
  stream: boolean;
  skeleton?: 'rows' | 'cards';
  announce?(text: string): void;
  children(items: readonly T[]): ReactNode;
}) {
  const { state } = read;
  const before = useRef({ count: 0, more: state.more });
  useEffect(() => {
    const was = before.current;
    if (was.more !== 'idle' && state.more === 'idle' && state.items.length > was.count) {
      announce?.(loadedMore(state.items.length - was.count));
    }
    before.current = { count: state.items.length, more: state.more };
  }, [state.more, state.items.length, announce]);

  const loadingText = stream ? aggregateTexts.loadingStream : aggregateTexts.loadingGroup;

  switch (state.phase) {
    case 'idle':
    case 'gone':
      return null;
    case 'waiting':
    case 'loading':
    case 'retrying':
      return (
        <Box role="status" aria-label={loadingText} sx={{ display: 'grid', gap: 1 }}>
          <Box aria-hidden="true">
            <TableSkeleton headers={[loadingText]} rows={skeleton === 'rows' ? 5 : 3} />
          </Box>
          {state.phase === 'retrying' && <Box aria-live="polite">{aggregateTexts.retrying}</Box>}
        </Box>
      );
    case 'refused':
      return (
        <InlineNotice
          severity="error"
          action={{ label: aggregateTexts.retry, onClick: read.retry }}
        >
          {stream ? aggregateTexts.refusedStream : aggregateTexts.refusedGroup}
        </InlineNotice>
      );
    case 'failed':
      return (
        <InlineNotice
          severity="error"
          action={{ label: aggregateTexts.retry, onClick: read.retry }}
        >
          {stream ? aggregateTexts.errorStream : aggregateTexts.errorGroup}
        </InlineNotice>
      );
    case 'ready':
      break;
  }

  if (state.items.length === 0 && state.nextCursor === null) {
    return (
      <InlineEmpty>{stream ? aggregateTexts.emptyStream : aggregateTexts.emptyGroup}</InlineEmpty>
    );
  }
  const moreBusy = state.more === 'loading' || state.more === 'retrying';
  return (
    <Box sx={{ display: 'grid', gap: 1 }}>
      {children(state.items)}
      {state.more === 'refused' && (
        <InlineNotice severity="error">
          {stream ? aggregateTexts.refusedStream : aggregateTexts.refusedGroup}
        </InlineNotice>
      )}
      {state.more === 'failed' && (
        <InlineNotice severity="error">
          {stream ? aggregateTexts.errorStream : aggregateTexts.errorGroup}
        </InlineNotice>
      )}
      {state.more === 'retrying' && <Box aria-live="polite">{aggregateTexts.retrying}</Box>}
      {state.nextCursor !== null && (
        <Box>
          <ActionButton
            label={aggregateTexts.showMore}
            busy={moreBusy}
            onClick={moreBusy ? undefined : read.loadMore}
          />
        </Box>
      )}
    </Box>
  );
}

/** The headers read pending (design § 3): three header bars in one status region. */
export function HeadersSkeleton() {
  return (
    <Box role="status" aria-label={aggregateTexts.loadingHeaders}>
      <Box aria-hidden="true">
        <TableSkeleton headers={[aggregateTexts.loadingHeaders]} rows={3} />
      </Box>
    </Box>
  );
}
