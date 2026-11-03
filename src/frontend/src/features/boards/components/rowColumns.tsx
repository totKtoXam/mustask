import type { ReactNode } from 'react';
import { Link as RouterLink } from 'react-router-dom';
import Link from '@mui/material/Link';
import { formatDateTime } from '../../../shared/lib/format';
import type { DataColumn } from '../../../shared/ui';
import { AssigneeName, StateName, TypeName, teamTexts, workItemPath } from '../../workitems';
import type { AggregateRow } from '../api/aggregateApi';

/**
 * The rows of one group, flat and without indent outside by project (design § 3, D-6 A): the
 * shipped columns plus «Проект» as a link and «Команда» as plain text or «Без команды» (D-4 A).
 * Columns are never sortable here: a group reads by its cursor's order (OQ-9 A).
 */
export type RowLike = Pick<
  AggregateRow,
  'id' | 'identifier' | 'type' | 'title' | 'assignee' | 'state' | 'team' | 'changedAt'
> & { project: { id: string; name: string } };

export function rowColumns<R extends RowLike>({
  organizationId,
  hideProject = false,
  hideTeam = false,
  withChanged = false,
  actions,
  rows = [],
}: {
  organizationId: string;
  rows?: readonly R[];
  /** By project the group is the project: the column would repeat the header. */
  hideProject?: boolean;
  /** The project's own level has no «Команда» column in the aggregate form. */
  hideTeam?: boolean;
  withChanged?: boolean;
  /** The «Ещё действия» cell of a row, when the user may move it. */
  actions?: (row: R, index: number) => ReactNode;
}) {
  const columns: DataColumn<R>[] = [
    { key: 'identifier', header: 'ID', render: (row) => row.identifier },
    { key: 'type', header: 'Тип', render: (row) => <TypeName type={row.type} /> },
    {
      key: 'title',
      header: 'Заголовок',
      render: (row) => (
        <Link
          component={RouterLink}
          to={workItemPath(organizationId, row.project.id, row.identifier)}
        >
          {row.title}
        </Link>
      ),
    },
    {
      key: 'assignee',
      header: 'Исполнитель',
      render: (row) => <AssigneeName assignee={row.assignee} />,
    },
    { key: 'state', header: 'Статус', render: (row) => <StateName state={row.state} /> },
  ];
  if (!hideProject) {
    columns.push({
      key: 'project',
      header: 'Проект',
      render: (row) => (
        <Link
          component={RouterLink}
          to={`/organizations/${organizationId}/projects/${row.project.id}`}
        >
          {row.project.name}
        </Link>
      ),
    });
  }
  if (!hideTeam) {
    columns.push({
      key: 'team',
      header: 'Команда',
      render: (row) => row.team?.name ?? teamTexts.noTeam,
    });
  }
  if (withChanged) {
    columns.push({
      key: 'changedAt',
      header: 'Изменён',
      render: (row) => formatDateTime(row.changedAt),
    });
  }
  if (actions) {
    columns.push({
      key: 'actions',
      header: 'Действия',
      align: 'right',
      render: (row) => actions(row, rows.indexOf(row)),
    });
  }
  return columns;
}
