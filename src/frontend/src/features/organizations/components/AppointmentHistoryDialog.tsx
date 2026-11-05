import { useCallback, useEffect } from 'react';
import HistoryOutlinedIcon from '@mui/icons-material/HistoryOutlined';
import { ApiError } from '../../../shared/api/httpClient';
import { organizationErrorCodes as codes } from '../../../shared/api/errorCodes';
import { formatDateTime } from '../../../shared/lib/format';
import {
  ActionButton,
  BodyRow,
  Cell,
  DialogShell,
  HeadCell,
  InlineNotice,
  TableCard,
  TableSkeleton,
} from '../../../shared/ui';
import { ErrorBanner } from '../../../shared/ui/ErrorBanner';
import { StateFrame } from '../../../shared/ui/StateFrame';
import { useAppointments, type AppointmentsHost } from '../hooks/queries';
import { HISTORY_COLUMNS, historyDialog } from '../lib/rescueTexts';
import { UserLine } from './UserLine';

export interface AppointmentHistoryDialogProps {
  organizationId: string;
  /** The organization's name, passed by the host and not read again (design § 11). */
  organizationName: string;
  /** Which route the host reads with: the list's (instance code) or the organization's (administrator). */
  host: AppointmentsHost;
  /** A 403 on the read: the host re-reads its access (mod-016 § 2.2). */
  onForbidden?(): void;
  onClose(): void;
}

// The wide dialog's table is 852 px at most: the date on one line, the two users 190 px each with
// room for «Системный Администратор» and the «неактивен» mark flowing on, and the rest to «Причина».
const COLUMN_WIDTHS = [{ width: 130 }, { width: 190 }, { width: 190 }, {}] as const;
const TABLE_MIN_WIDTH = 720;

/**
 * «История назначений» (US-3, design § 5): a read-only dialog with the organization's appointment
 * record, newest first as served, in one component for two hosts — the component knows no route.
 * Only «Закрыть» is in the footer and it holds the first focus. Loading, empty, error, 403 and 404
 * each have their own frame. The kit's `wide` dialog is used (design § 12 point 2, gate-2): the
 * table scrolls sideways inside its card only when the window is narrower than the table.
 */
export function AppointmentHistoryDialog({
  organizationId,
  organizationName,
  host,
  onForbidden,
  onClose,
}: AppointmentHistoryDialogProps) {
  const query = useAppointments(organizationId, host);
  // A stable callback: React runs it once when the button mounts, not on every render (qa D-7).
  const focusClose = useCallback((button: HTMLButtonElement | null) => button?.focus(), []);
  const error = query.isError ? query.error : null;
  const apiError = error instanceof ApiError ? error : null;
  const forbidden = apiError?.status === 403;
  const notFound = apiError?.status === 404 || apiError?.errorCode === codes.notFound;

  useEffect(() => {
    if (forbidden) {
      onForbidden?.();
    }
  }, [forbidden, onForbidden]);

  return (
    <DialogShell
      open
      size="wide"
      title={historyDialog.title(organizationName)}
      onClose={onClose}
      footer={<ActionButton label={historyDialog.close} ref={focusClose} onClick={onClose} />}
    >
      {query.isPending ? (
        <TableSkeleton headers={HISTORY_COLUMNS} rows={4} />
      ) : forbidden ? (
        <InlineNotice severity="error">{historyDialog.forbidden}</InlineNotice>
      ) : notFound ? (
        <InlineNotice severity="error">{historyDialog.notFound}</InlineNotice>
      ) : query.isError ? (
        <ErrorBanner error={query.error} onRetry={() => void query.refetch()} />
      ) : query.data.length === 0 ? (
        <StateFrame compact icon={HistoryOutlinedIcon} heading={historyDialog.empty} />
      ) : (
        <TableCard
          aria-label={historyDialog.tableLabel}
          columns={COLUMN_WIDTHS}
          minWidth={TABLE_MIN_WIDTH}
          head={HISTORY_COLUMNS.map((label) => (
            <HeadCell key={label} label={label} />
          ))}
        >
          {query.data.map((record) => (
            <BodyRow key={record.id}>
              <Cell>{formatDateTime(record.appointedAt)}</Cell>
              <Cell>
                <UserLine user={record.appointedBy} truncate={false} />
              </Cell>
              <Cell>
                <UserLine user={record.appointee} truncate={false} />
              </Cell>
              <Cell wrap>{record.reason}</Cell>
            </BodyRow>
          ))}
        </TableCard>
      )}
    </DialogShell>
  );
}
