import { useState } from 'react';
import Box from '@mui/material/Box';
import List from '@mui/material/List';
import ListItem from '@mui/material/ListItem';
import Typography from '@mui/material/Typography';
import { apiErrorText } from '../../../shared/lib/apiErrorText';
import {
  ConfirmDialog,
  FieldsSkeleton,
  InlineNotice,
  Tag,
  TruncatedText,
} from '../../../shared/ui';
import { useDeletionCounts } from '../hooks/queries';
import { placeRefusal } from '../lib/refusals';
import { deleteTeamDialog, linkDialog } from '../lib/teamTexts';

/**
 * «Удаление команды» (feat-028 US-5, BR-OL-9; design § 5.3): destructive, with no restore, focus on
 * «Отмена». What the team owns, in any state, is counted **per linked project** — deactivated ones
 * and zeros included — when the dialog opens: while it is read a skeleton stands for the text and
 * «Удалить команду» is unavailable; a failed read says so with «Повторить» and the deletion stays
 * unavailable. A team linked to no project is asked about without a list. An item given to the team
 * while the dialog is open is not in the counts and is reassigned all the same. There is no 409: a
 * project may have no team. A refusal (the team is gone, or the code is) shows in the dialog's alert,
 * and the page reads itself again when the dialog closes.
 */
export function DeleteTeamDialog<R>({
  organizationId,
  teamId,
  teamName,
  action,
  onSucceeded,
  onClose,
}: {
  organizationId: string;
  teamId: string;
  teamName: string;
  action(): Promise<R>;
  onSucceeded(result: R): void;
  /** `contextGone`: a refusal that leaves the page stale; the page reads itself again. */
  onClose(contextGone: boolean): void;
}) {
  const counts = useDeletionCounts(organizationId, teamId);
  const [busy, setBusy] = useState(false);
  const [alert, setAlert] = useState<string | null>(null);
  const [stale, setStale] = useState(false);

  async function confirm() {
    setBusy(true);
    setAlert(null);
    try {
      onSucceeded(await action());
    } catch (error) {
      const placed = placeRefusal(error, []);
      setAlert(placed.alert);
      // The team is gone or the code is: whatever the page shows is no longer true.
      setStale(true);
      setBusy(false);
    }
  }

  const counting = counts.isPending || (counts.isError && counts.isFetching);
  const projects = counts.data?.projects ?? [];

  return (
    <ConfirmDialog
      open
      title={deleteTeamDialog.title}
      confirmLabel={deleteTeamDialog.confirm}
      destructive
      cancelAutoFocus
      busy={busy}
      error={alert}
      confirmDisabled={!counts.isSuccess}
      onConfirm={() => void confirm()}
      onClose={() => onClose(stale)}
    >
      {counting ? (
        <FieldsSkeleton count={2} />
      ) : counts.isError ? (
        <InlineNotice
          severity="error"
          action={{ label: linkDialog.retry, onClick: () => void counts.refetch() }}
        >
          {apiErrorText(counts.error)}
        </InlineNotice>
      ) : projects.length > 0 ? (
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
          <Typography variant="body1">{deleteTeamDialog.withProjects(teamName)}</Typography>
          <List
            dense
            disablePadding
            aria-label={deleteTeamDialog.listLabel}
            tabIndex={0}
            sx={(theme) => ({ maxHeight: theme.spacing(32), overflowY: 'auto' })}
          >
            {projects.map((project) => (
              <ListItem
                key={project.projectId}
                disableGutters
                sx={{ display: 'flex', alignItems: 'center', gap: 1 }}
              >
                <Box sx={{ minWidth: 0, display: 'flex', alignItems: 'center', gap: 1 }}>
                  <TruncatedText>{project.name}</TruncatedText>
                  {!project.active && <Tag label={deleteTeamDialog.deactivated} />}
                </Box>
                <Box
                  component="span"
                  sx={{ marginLeft: 'auto', flexShrink: 0, whiteSpace: 'nowrap' }}
                >
                  {project.count}
                </Box>
              </ListItem>
            ))}
          </List>
          <Typography variant="body1">{deleteTeamDialog.irreversible}</Typography>
        </Box>
      ) : (
        <Typography variant="body1">{deleteTeamDialog.withoutProjects(teamName)}</Typography>
      )}
    </ConfirmDialog>
  );
}
