import type { ReactNode } from 'react';
import { DataTable } from '../../../shared/ui';
import type { AggregateRow } from '../api/aggregateApi';
import { rowColumns, type RowLike } from './rowColumns';

export function AggregateRows<R extends RowLike = AggregateRow>({
  label,
  rows,
  ...options
}: {
  organizationId: string;
  label: string;
  rows: readonly R[];
  hideProject?: boolean;
  hideTeam?: boolean;
  withChanged?: boolean;
  actions?: (row: R, index: number) => ReactNode;
}) {
  return (
    <DataTable
      aria-label={label}
      columns={rowColumns({ ...options, rows })}
      rows={rows}
      rowKey={(row) => row.id}
    />
  );
}
