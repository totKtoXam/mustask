import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import { ApiError } from '../../../shared/api/httpClient';
import { apiErrorText } from '../../../shared/lib/apiErrorText';
import { ConfirmDialog } from '../../../shared/ui/ConfirmDialog';
import { InlineNotice } from '../../../shared/ui/feedback/InlineNotice';
import { FieldsSkeleton } from '../../../shared/ui/feedback/FieldsSkeleton';
import { DataTable, type DataColumn } from '../../../shared/ui/table/DataTable';
import type { AreaPage } from '../api/organizationsApi';
import type { SwitchImpact } from '../api/grantsApi';
import { useSwitchImpact } from '../hooks/queries';
import { GRANT_ROLE_LABELS, switchOffDialog } from '../lib/grantTexts';
import { UserLine } from './UserLine';

/** Design D-12: the given-access list shows this many rows and scrolls (the server sends up to 50). */
const GIVEN_VISIBLE_ROWS = 6;

type GivenGrant = SwitchImpact['givenGrants'][number];

const GIVEN_COLUMNS: DataColumn<GivenGrant>[] = [
  {
    key: 'user',
    header: switchOffDialog.givenColumns.user,
    render: (row) => <UserLine user={row.user} />,
  },
  {
    key: 'role',
    header: switchOffDialog.givenColumns.role,
    render: (row) => GRANT_ROLE_LABELS[row.role],
  },
  {
    key: 'givenBy',
    header: switchOffDialog.givenColumns.givenBy,
    render: (row) => <UserLine user={row.givenBy} />,
  },
];

export interface SwitchOffDialogProps {
  organizationId: string;
  area: AreaPage;
  /** The department above a project node, the parent of a sub-area: the name the lead sentence uses. */
  sourceName: string;
  busy: boolean;
  /** A refusal of the write itself, shown in the dialog. */
  error: unknown;
  onConfirm(): void;
  onClose(): void;
}

/**
 * «Отключить наследование доступа?» (design § 5, OQ-6, BR-SA-7, BR-SA-19, R-27): what turning the switch
 * off does now — the count of users who lose it, the line about the caller, the access in the project
 * those users gave — read once when the dialog opens and not re-judged at confirm. «Отключить» is
 * unavailable until the count is known; a failed read says so with «Повторить».
 */
export function SwitchOffDialog({
  organizationId,
  area,
  sourceName,
  busy,
  error,
  onConfirm,
  onClose,
}: SwitchOffDialogProps) {
  const impact = useSwitchImpact(organizationId, area.id);
  const isNode = area.type === 'project';
  const data = impact.data;
  const forbidden = impact.error instanceof ApiError && impact.error.status === 403;

  return (
    <ConfirmDialog
      open
      destructive
      cancelAutoFocus
      title={switchOffDialog.title}
      confirmLabel={switchOffDialog.confirm}
      busy={busy}
      confirmDisabled={data === undefined}
      error={error ? apiErrorText(error) : undefined}
      onConfirm={onConfirm}
      onClose={onClose}
    >
      <Stack spacing={2}>
        <Typography variant="body2">
          {isNode
            ? switchOffDialog.leadNode(sourceName, area.name)
            : switchOffDialog.leadSubArea(sourceName, area.name)}
        </Typography>
        {impact.isPending && <FieldsSkeleton count={2} />}
        {impact.isError && (
          <InlineNotice
            severity="error"
            action={
              forbidden
                ? undefined
                : { label: switchOffDialog.retry, onClick: () => void impact.refetch() }
            }
          >
            {forbidden ? switchOffDialog.forbidden : switchOffDialog.loadFailed}
          </InlineNotice>
        )}
        {data && (
          <>
            <Typography variant="body2">
              {data.holderCount === 0
                ? switchOffDialog.nobody
                : isNode
                  ? switchOffDialog.countNode(data.holderCount)
                  : switchOffDialog.countSubArea(data.holderCount)}
            </Typography>
            {data.holderCount > 0 && (
              <Typography variant="caption" color="text.secondary">
                {switchOffDialog.countNote}
              </Typography>
            )}
            {data.callerIncluded && (
              <InlineNotice severity="warning">
                {isNode ? switchOffDialog.selfNode : switchOffDialog.selfSubArea}
              </InlineNotice>
            )}
            {data.givenGrantsTotal > 0 && (
              <Stack spacing={1}>
                <Typography variant="subtitle2" component="p">
                  {switchOffDialog.givenHeading(data.givenGrantsTotal)}
                </Typography>
                <DataTable
                  aria-label={switchOffDialog.givenTable}
                  columns={GIVEN_COLUMNS}
                  rows={data.givenGrants}
                  maxRows={GIVEN_VISIBLE_ROWS}
                  rowKey={(row) => row.user.id}
                />
                <Typography variant="caption" color="text.secondary">
                  {switchOffDialog.givenNote}
                </Typography>
              </Stack>
            )}
          </>
        )}
      </Stack>
    </ConfirmDialog>
  );
}
