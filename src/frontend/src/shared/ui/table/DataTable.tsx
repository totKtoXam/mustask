import type { ReactNode } from 'react';
import Stack from '@mui/material/Stack';
import { InlineNotice } from '../feedback/InlineNotice';
import { StateFrame, type StateFrameProps } from '../StateFrame';
import { BodyRow } from './BodyRow';
import { Cell } from './Cell';
import { HeadCell, type HeadCellSort } from './HeadCell';
import { TablePager, type TablePagerProps } from './TablePager';
import { TableCard } from './TableCard';
import { TableSkeleton } from './TableSkeleton';

export interface DataColumn<R> {
  key: string;
  /** The column's name, the header's label. */
  header: string;
  width?: number | string;
  minWidth?: number | string;
  align?: 'left' | 'right';
  separator?: boolean;
  clip?: boolean;
  sort?: HeadCellSort;
  render: (row: R) => ReactNode;
}

export interface DataTableProps<R> {
  /** The table's name — «Реестр языков». */
  'aria-label': string;
  columns: readonly DataColumn<R>[];
  rows: readonly R[];
  rowKey: (row: R) => string;
  selectedKey?: string | null;
  onSelect?: (key: string) => void;
  /** As `TableCard`: the card scrolls inside itself and its header is sticky. */
  maxHeightOffset?: number;
  minHeight?: number;
  /** No rows yet: a `TableSkeleton` with the real headers. */
  loading?: boolean;
  /** A read error, already in words — the server's own text. */
  error?: string;
  /** The error's «Повторить»; without it the notice has no action. */
  onRetry?: () => void;
  /** The frame that replaces the card when there are no rows. */
  empty?: StateFrameProps;
  pager?: TablePagerProps;
}

/** The label of a retry; the host's own words are in `error`. */
const RETRY_LABEL = 'Повторить';

/**
 * The plain registry, built from the table parts (tech-015 design § 5.11), owning its
 * loading, empty and error states:
 *
 * - loading, no rows: a `TableSkeleton` with the real column labels;
 * - empty: `empty`'s `StateFrame` replaces the card (the page's filter bar and head stay);
 * - a read error, no rows: the error replaces the card, with «Повторить»;
 * - a failed re-read with rows shown: the table stays — rows, selection, scroll — and the
 *   error stands above it; a successful retry removes it.
 *
 * A page decides permissions before it renders the table. A plain table has Tab stops, no
 * arrow-key navigation and no `aria-selected`.
 */
export function DataTable<R>({
  'aria-label': ariaLabel,
  columns,
  rows,
  rowKey,
  selectedKey = null,
  onSelect,
  maxHeightOffset,
  minHeight,
  loading = false,
  error,
  onRetry,
  empty,
  pager,
}: DataTableProps<R>) {
  const notice =
    error === undefined ? null : (
      <InlineNotice
        severity="error"
        action={onRetry ? { label: RETRY_LABEL, onClick: onRetry } : undefined}
      >
        {error}
      </InlineNotice>
    );

  if (rows.length === 0) {
    if (loading) return <TableSkeleton headers={columns.map((column) => column.header)} />;
    if (error !== undefined) return notice;
    if (empty) return <StateFrame {...empty} />;
  }

  const widths = columns.some((column) => column.width !== undefined)
    ? columns.map((column) => ({ width: column.width }))
    : undefined;

  return (
    <Stack spacing={2}>
      {notice}
      <TableCard
        aria-label={ariaLabel}
        columns={widths}
        maxHeightOffset={maxHeightOffset}
        minHeight={minHeight}
        foot={pager && <TablePager {...pager} />}
        head={columns.map((column) => (
          <HeadCell
            key={column.key}
            label={column.header}
            width={column.width}
            minWidth={column.minWidth}
            align={column.align}
            separator={column.separator}
            clip={column.clip}
            sort={column.sort}
          />
        ))}
      >
        {rows.map((row) => {
          const key = rowKey(row);
          return (
            <BodyRow
              key={key}
              selected={selectedKey === key}
              onSelect={onSelect ? () => onSelect(key) : undefined}
            >
              {columns.map((column) => (
                <Cell key={column.key} align={column.align} clip={column.clip}>
                  {column.render(row)}
                </Cell>
              ))}
            </BodyRow>
          );
        })}
      </TableCard>
    </Stack>
  );
}
