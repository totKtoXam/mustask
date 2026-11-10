import { useEffect, useEffectEvent, useMemo, useState } from 'react';
import Typography from '@mui/material/Typography';
import InboxOutlinedIcon from '@mui/icons-material/InboxOutlined';
import { useAuth } from '../../auth/AuthContext';
import { formatDateTime } from '../../../shared/lib/format';
import { ActionButton } from '../../../shared/ui/actions/ActionButton';
import { MoreActionsMenu } from '../../../shared/ui/actions/MoreActionsMenu';
import { InlineNotice } from '../../../shared/ui/feedback/InlineNotice';
import { InlineTextField } from '../../../shared/ui/fields/InlineTextField';
import { FilterBar } from '../../../shared/ui/FilterBar';
import { Section } from '../../../shared/ui/layout/Section';
import { Tag } from '../../../shared/ui/marks/Tag';
import { DataTable, type DataColumn } from '../../../shared/ui/table/DataTable';
import type { GrantRow, GrantScope } from '../api/grantsApi';
import { useGrants } from '../hooks/queries';
import { GRANT_ROLE_LABELS, changeGrantDialog, grantsSection } from '../lib/grantTexts';
import { notFoundLevel } from '../lib/notFoundLevel';
import { plainName } from '../lib/userName';
import { ChangeGrantRoleDialog, GiveGrantDialog, RevokeGrantDialog } from './GrantDialogs';
import { useNotify } from './notice';
import { UserLine } from './UserLine';

export interface GrantsSectionProps {
  organizationId: string;
  scope: GrantScope;
  objectId: string;
  objectName: string;
  /** The object's own `…grant` code, from its served codes (BR-SA-18). */
  canGrant: boolean;
  title: string;
  headingLevel?: 2 | 3;
  /** The object is gone: the page reads itself again and shows its frame. */
  onContextGone(): void;
}

type Dialog =
  { kind: 'give' } | { kind: 'role'; row: GrantRow } | { kind: 'revoke'; row: GrantRow };

const EMPTY_TEXT = {
  department: grantsSection.empty.department,
  project: grantsSection.empty.project,
  team: grantsSection.empty.team,
} as const;

function matches(row: GrantRow, text: string): boolean {
  const needle = text.trim().toLowerCase();
  if (needle === '') return true;
  return (
    plainName(row.user).toLowerCase().includes(needle) ||
    (row.user.email ?? '').toLowerCase().includes(needle)
  );
}

/**
 * The access of one object — a department, a project or a team (mod-019 design §§ 2-3): who holds a grant,
 * in which role, from where. «Выдать доступ» and the row menu are the object's `…grant` holder's; everyone
 * who reads the object reads the list. A project's list also carries the department's rows, muted and
 * without a menu: they are changed on the department.
 */
export function GrantsSection({
  organizationId,
  scope,
  objectId,
  objectName,
  canGrant,
  title,
  headingLevel = 2,
  onContextGone,
}: GrantsSectionProps) {
  const query = useGrants(organizationId, scope, objectId);
  const { subject } = useAuth();
  const notify = useNotify();
  const [dialog, setDialog] = useState<Dialog | null>(null);
  const [filter, setFilter] = useState('');
  const [changedNotice, setChangedNotice] = useState(false);

  const gone = query.isError && notFoundLevel(query.error) !== null;
  const reportGone = useEffectEvent(onContextGone);
  useEffect(() => {
    if (gone) reportGone();
  }, [gone]);

  const all = useMemo(() => query.data?.items ?? [], [query.data]);
  const rows = useMemo(() => all.filter((row) => matches(row, filter)), [all, filter]);
  const inheritance = query.data?.inheritance ?? null;
  const showOrigin = scope === 'project';

  const target = { organizationId, scope, objectId, objectName };
  const close = () => setDialog(null);
  const outcome = {
    onDone: (message: string) => {
      close();
      setChangedNotice(false);
      notify(message);
    },
    onChanged: () => {
      close();
      setChangedNotice(true);
      void query.refetch();
    },
    onContextGone,
    onClose: close,
  };

  const columns: DataColumn<GrantRow>[] = [
    {
      key: 'user',
      header: grantsSection.columns.user,
      render: (row) => (
        <Typography
          component="span"
          variant="body2"
          sx={{ color: row.origin === 'department' ? 'text.secondary' : 'text.primary' }}
        >
          <UserLine user={row.user} truncate={false} />
          {row.user.id === subject && row.origin === 'held' && (
            <>
              {' '}
              <Tag label={grantsSection.you} />
            </>
          )}
        </Typography>
      ),
    },
    {
      key: 'role',
      header: grantsSection.columns.role,
      render: (row) => GRANT_ROLE_LABELS[row.role],
    },
    ...(showOrigin
      ? [
          {
            key: 'origin',
            header: grantsSection.columns.origin,
            render: (row: GrantRow) =>
              row.origin === 'department'
                ? grantsSection.fromDepartment(row.department?.name ?? '')
                : grantsSection.held,
          },
        ]
      : []),
    {
      key: 'givenBy',
      header: grantsSection.columns.givenBy,
      render: (row) => (
        <>
          <UserLine user={row.givenBy} truncate={false} />
          {`, ${formatDateTime(row.givenAt)}`}
        </>
      ),
    },
    ...(canGrant
      ? [
          {
            key: 'menu',
            header: '',
            width: 48,
            align: 'right' as const,
            render: (row: GrantRow) => {
              if (row.origin === 'department') return null;
              const isSelf = row.user.id === subject;
              return (
                <MoreActionsMenu
                  size="row"
                  label={grantsSection.rowMenu(plainName(row.user))}
                  items={[
                    ...(isSelf
                      ? []
                      : [
                          {
                            label: grantsSection.changeRole,
                            onSelect: () => setDialog({ kind: 'role', row }),
                          },
                        ]),
                    {
                      label: grantsSection.revoke,
                      destructive: true,
                      onSelect: () => setDialog({ kind: 'revoke', row }),
                    },
                  ]}
                />
              );
            },
          },
        ]
      : []),
  ];

  const giveButton = canGrant ? (
    <ActionButton
      label={grantsSection.give}
      tone="primary"
      onClick={() => setDialog({ kind: 'give' })}
    />
  ) : undefined;

  const filtered = all.length > 0 && rows.length === 0;
  const empty = filtered
    ? {
        icon: InboxOutlinedIcon,
        heading: grantsSection.nothingFound,
        headingLevel: 3 as const,
        action: { label: grantsSection.resetFilter, onClick: () => setFilter('') },
      }
    : {
        icon: InboxOutlinedIcon,
        heading: EMPTY_TEXT[scope],
        headingLevel: 3 as const,
        hint: canGrant ? grantsSection.empty.managerLine : undefined,
        action: canGrant
          ? { label: grantsSection.give, onClick: () => setDialog({ kind: 'give' }) }
          : undefined,
      };

  return (
    <Section title={title} headingLevel={headingLevel} actions={giveButton}>
      <InlineNotice severity="info">{grantsSection.noticeOnlyGranted}</InlineNotice>
      {inheritance !== null && !inheritance.inheritsAccess && (
        <InlineNotice severity="info">
          {grantsSection.noticeSwitchedOff(inheritance.departmentName)}
        </InlineNotice>
      )}
      {changedNotice && <InlineNotice severity="info">{changeGrantDialog.gone}</InlineNotice>}
      {all.length > 0 && (
        <FilterBar active={filter !== ''} onClear={() => setFilter('')}>
          <InlineTextField
            label={grantsSection.filter}
            frame="always"
            value={filter}
            onChange={setFilter}
          />
        </FilterBar>
      )}
      <DataTable
        aria-label={grantsSection.tableLabel}
        columns={columns}
        rows={rows}
        rowKey={(row) => `${row.origin}:${row.user.id}`}
        loading={query.isPending}
        error={query.isError && !gone ? grantsSection.loadFailed : undefined}
        onRetry={() => void query.refetch()}
        empty={empty}
      />
      {dialog?.kind === 'give' && <GiveGrantDialog target={target} {...outcome} />}
      {dialog?.kind === 'role' && (
        <ChangeGrantRoleDialog target={target} row={dialog.row} {...outcome} />
      )}
      {dialog?.kind === 'revoke' && (
        <RevokeGrantDialog
          target={target}
          row={dialog.row}
          isSelf={dialog.row.user.id === subject}
          {...outcome}
        />
      )}
    </Section>
  );
}
