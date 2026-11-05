import { useEffect, useRef, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import Box from '@mui/material/Box';
import CorporateFareOutlinedIcon from '@mui/icons-material/CorporateFareOutlined';
import HistoryOutlinedIcon from '@mui/icons-material/HistoryOutlined';
import PersonAddAltOutlinedIcon from '@mui/icons-material/PersonAddAltOutlined';
import { ApiError } from '../../../shared/api/httpClient';
import { formatDate } from '../../../shared/lib/format';
import {
  BodyRow,
  Cell,
  HeadCell,
  LiveRegion,
  TableCard,
  TablePager,
  TableSkeleton,
  Tag,
  VisuallyHidden,
} from '../../../shared/ui';
import { ErrorBanner } from '../../../shared/ui/ErrorBanner';
import { PageHeader } from '../../../shared/ui/PageHeader';
import { AccessDenied, StateFrame } from '../../../shared/ui/StateFrame';
import { useAuth } from '../../auth/AuthContext';
import { organizationsKeys } from '../api/queryKeys';
import { RESCUE_PAGE_SIZE, type RescueOrganization } from '../api/rescueApi';
import { AppointAdminDialog } from '../components/AppointAdminDialog';
import { AppointmentHistoryDialog } from '../components/AppointmentHistoryDialog';
import { ListFilter } from '../components/ListFilter';
import { useNotify } from '../components/notice';
import { RowActionsMenu } from '../components/RowActionsMenu';
import { TruncatedName } from '../components/TruncatedName';
import { useAppointAdministrator } from '../hooks/mutations';
import { useRescueOrganizations } from '../hooks/queries';
import { useDebouncedValue } from '../hooks/useDebouncedValue';
import { RESCUE_COLUMNS, appointDialog, rescuePage } from '../lib/rescueTexts';
import { plainName } from '../lib/userName';
import { organizationsPermissions } from '../permissions';

const SKELETON_ROWS = 8;
const COLUMN_WIDTHS = [{}, { width: 160 }, { width: 220 }, { width: 64 }] as const;
const TABLE_MIN_WIDTH = 640;

type RescueDialog =
  | { kind: 'appoint'; organization: RescueOrganization }
  | { kind: 'history'; organization: RescueOrganization }
  | null;

/**
 * «Все организации» (US-1, design § 3): every organization of the instance, active and
 * deactivated, for the holder of `organizations.organization.rescue`. Paged 25 by the server, a
 * name filter (settled after a pause, sent trimmed), a row menu with the two actions. The name is
 * plain text: nothing of an organization opens from here (DC-3). Filter and page are component
 * state (DC-5). A 403 on the list or on a dialog's call turns the page into «Доступ ограничен»
 * and drops its queries (DC-8): the list is unmounted first and its queries removed after, so
 * nothing re-creates the read.
 */
export function AllOrganizationsPage() {
  const { can } = useAuth();
  // The gate is the code, not a role (ADR-0035): without it nothing below is mounted.
  if (!can(organizationsPermissions.organizationRescue)) {
    return <AccessDenied />;
  }
  return <RescueGate />;
}

function RescueGate() {
  const client = useQueryClient();
  const [revoked, setRevoked] = useState(false);

  // After the commit that unmounted the list: no observer is left to read the query again (qa D-8).
  useEffect(() => {
    if (revoked) {
      client.removeQueries({ queryKey: organizationsKeys.rescueLists });
    }
  }, [revoked, client]);

  return revoked ? <AccessDenied /> : <AllOrganizations onRevoked={() => setRevoked(true)} />;
}

function AllOrganizations({ onRevoked }: { onRevoked(): void }) {
  const client = useQueryClient();
  const notify = useNotify();
  const [text, setText] = useState('');
  const [page, setPage] = useState(1);
  const [dialog, setDialog] = useState<RescueDialog>(null);
  const settled = useDebouncedValue(text.trim());
  const query = useRescueOrganizations(page, settled);
  // The page the rows on screen belong to: the pager's range moves with them, not before (qa D-9).
  const [shownPage, setShownPage] = useState(1);
  const [shownFilter, setShownFilter] = useState(settled);
  // The filter the rows on screen answer: the empty frame keeps its wording while a refined or
  // cleared filter loads over it (code review CR-1).
  const [dataFilter, setDataFilter] = useState(settled);
  const buttons = useRef(new Map<string, HTMLButtonElement | null>());
  const returnTo = useRef<string | null>(null);

  const forbidden = query.error instanceof ApiError && query.error.status === 403;
  const count = query.data?.count ?? 0;
  const lastPage = Math.max(1, Math.ceil(count / RESCUE_PAGE_SIZE));

  // A settled filter is a new list: back to page 1 before anything is asked, so no request goes
  // out with the old filter (qa D-10). State adjusted while rendering, as below.
  if (shownFilter !== settled) {
    setShownFilter(settled);
    setPage(1);
  }
  if (query.data && !query.isPlaceholderData && shownPage !== page) {
    setShownPage(page);
  }
  if (query.data && !query.isPlaceholderData && dataFilter !== settled) {
    setDataFilter(settled);
  }

  // Rows deleted meanwhile: the request is repeated for the last page that exists. State adjusted
  // while rendering, the pattern React names for a value derived from a fresh answer.
  if (query.data && !query.isPlaceholderData && query.data.data.length === 0 && page > lastPage) {
    setPage(lastPage);
  }

  // Focus goes back to the row's «Действия» once its dialog has closed.
  useEffect(() => {
    if (dialog === null && returnTo.current !== null) {
      buttons.current.get(returnTo.current)?.focus();
      returnTo.current = null;
    }
  }, [dialog]);

  if (forbidden) {
    return <AccessDenied />;
  }

  const close = () => setDialog(null);
  const revoke = onRevoked;
  const open = (next: NonNullable<RescueDialog>) => {
    returnTo.current = next.organization.id;
    setDialog(next);
  };
  const changeText = (next: string) => setText(next);
  const filtered = settled !== '';
  const firstLoad = query.isPending;
  const refreshing = query.isFetching && !firstLoad;

  return (
    <Box>
      <PageHeader title={rescuePage.title} />
      <ListFilter label={rescuePage.filterLabel} text={text} onText={changeText} />
      <LiveRegion
        text={
          filtered && query.isSuccess && !query.isPlaceholderData ? rescuePage.found(count) : ''
        }
      />
      {firstLoad ? (
        <TableSkeleton headers={RESCUE_COLUMNS} rows={SKELETON_ROWS} />
      ) : query.isError ? (
        <ErrorBanner error={query.error} onRetry={() => void query.refetch()} />
      ) : count === 0 ? (
        dataFilter !== '' ? (
          <StateFrame
            compact
            icon={CorporateFareOutlinedIcon}
            heading={rescuePage.noMatch}
            action={{ label: rescuePage.resetFilter, onClick: () => changeText('') }}
          />
        ) : (
          <StateFrame compact icon={CorporateFareOutlinedIcon} heading={rescuePage.empty} />
        )
      ) : (
        <Box aria-busy={refreshing} sx={{ opacity: refreshing ? 0.6 : 1 }}>
          <TableCard
            aria-label={rescuePage.title}
            columns={COLUMN_WIDTHS}
            minWidth={TABLE_MIN_WIDTH}
            head={RESCUE_COLUMNS.map((label, index) => (
              <HeadCell key={index} label={label} />
            ))}
            foot={
              <TablePager
                page={shownPage - 1}
                disabled={refreshing}
                rowsPerPage={RESCUE_PAGE_SIZE}
                count={count}
                onPageChange={(next) => setPage(next + 1)}
              />
            }
          >
            {query.data?.data.map((organization) => (
              <BodyRow key={organization.id}>
                <Cell>
                  <TruncatedName name={organization.name} />
                </Cell>
                <Cell>
                  {organization.status === 'active' ? (
                    rescuePage.statusActive
                  ) : (
                    <Tag label={rescuePage.statusDeactivated} variant="outlined" />
                  )}
                </Cell>
                <Cell>
                  {organization.lastAppointedAt === null ? (
                    <>
                      <span aria-hidden="true">—</span>
                      <VisuallyHidden>{rescuePage.neverAppointed}</VisuallyHidden>
                    </>
                  ) : (
                    formatDate(organization.lastAppointedAt)
                  )}
                </Cell>
                <Cell align="right">
                  <RowActionsMenu
                    name={organization.name}
                    buttonRef={(button) => {
                      buttons.current.set(organization.id, button);
                    }}
                    actions={[
                      {
                        label: rescuePage.appoint,
                        icon: <PersonAddAltOutlinedIcon fontSize="small" />,
                        onClick: () => open({ kind: 'appoint', organization }),
                      },
                      {
                        label: rescuePage.history,
                        icon: <HistoryOutlinedIcon fontSize="small" />,
                        onClick: () => open({ kind: 'history', organization }),
                      },
                    ]}
                  />
                </Cell>
              </BodyRow>
            ))}
          </TableCard>
        </Box>
      )}
      {dialog?.kind === 'appoint' && (
        <AppointDialogHost
          organization={dialog.organization}
          onSucceeded={(name) => {
            close();
            notify(appointDialog.appointed(name));
          }}
          onForbidden={revoke}
          onReread={() =>
            void client.invalidateQueries({ queryKey: organizationsKeys.rescueLists })
          }
          onClose={close}
        />
      )}
      {dialog?.kind === 'history' && (
        <AppointmentHistoryDialog
          organizationId={dialog.organization.id}
          organizationName={dialog.organization.name}
          host="rescue"
          onForbidden={revoke}
          onClose={close}
        />
      )}
    </Box>
  );
}

function AppointDialogHost({
  organization,
  onSucceeded,
  onForbidden,
  onReread,
  onClose,
}: {
  organization: RescueOrganization;
  onSucceeded(appointeeName: string): void;
  onForbidden(): void;
  onReread(): void;
  onClose(): void;
}) {
  const appoint = useAppointAdministrator(organization.id);
  return (
    <AppointAdminDialog
      organizationName={organization.name}
      submit={(body) => appoint.mutateAsync(body)}
      onSucceeded={(appointment) => onSucceeded(plainName(appointment.appointee))}
      onForbidden={onForbidden}
      onReread={onReread}
      onClose={onClose}
    />
  );
}
