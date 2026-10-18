import { useWorkContext } from '../../../shared/lib/workContext';
import type { Team } from '../api/organizationsApi';
import { useDeleteTeam, useDescribeTeam, useRenameTeam } from '../hooks/mutations';
import { deleteTeamDialog, descriptionDialog, renameTeamDialog } from '../lib/teamTexts';
import { DeleteTeamDialog } from './DeleteTeamDialog';
import { DescriptionDialog } from './DescriptionDialog';
import { NameDialog } from './NameDialog';
import { useNotify } from './notice';

/**
 * A dialog of one team to show: its rename, its description or its deletion (feat-028 design § 5).
 * All three are the organization's: a team's address no longer names a project.
 */
export type TeamDialog = { kind: 'rename' | 'describe' | 'delete'; team: Team } | null;

/**
 * The team dialogs of design § 5, shared by the organization's «Команды» and a team's page. Their
 * notices follow DC-7: deleting the team the switcher holds makes the switcher fall back, and its
 * notice is the only one (a team's page never chooses its team, mod-002 design § 3.4).
 */
export function TeamDialogs({
  organizationId,
  dialog,
  onClose,
  onDeleted,
}: {
  organizationId: string;
  dialog: TeamDialog;
  /** `contextGone`: a not-found answer; the page reads itself again. */
  onClose(contextGone: boolean): void;
  onDeleted?(): void;
}) {
  const rename = useRenameTeam(organizationId);
  const describe = useDescribeTeam(organizationId);
  const deleteTeam = useDeleteTeam(organizationId);
  const notify = useNotify();
  const context = useWorkContext();

  if (!dialog) {
    return null;
  }
  const { team } = dialog;

  switch (dialog.kind) {
    case 'rename':
      return (
        <NameDialog
          title={renameTeamDialog.title}
          initialName={team.name}
          submitLabel={renameTeamDialog.submit}
          submit={(name) => rename.mutateAsync({ teamId: team.id, name })}
          onSucceeded={() => {
            onClose(false);
            notify(renameTeamDialog.renamed);
          }}
          onClose={onClose}
        />
      );
    case 'describe':
      return (
        <DescriptionDialog
          title={descriptionDialog.team.title}
          initial={team.description}
          submit={(description) => describe.mutateAsync({ teamId: team.id, description })}
          onSucceeded={() => {
            onClose(false);
            notify(descriptionDialog.team.saved);
          }}
          onClose={onClose}
        />
      );
    case 'delete':
      return (
        <DeleteTeamDialog
          organizationId={organizationId}
          teamId={team.id}
          teamName={team.name}
          action={async () => {
            const { selection } = context;
            const fallsBack =
              selection.organizationId === organizationId && selection.teamId === team.id;
            await deleteTeam.mutateAsync(team.id);
            return fallsBack;
          }}
          onSucceeded={(fallsBack) => {
            onClose(false);
            onDeleted?.();
            if (!fallsBack) {
              notify(deleteTeamDialog.deleted(team.name));
            }
          }}
          onClose={onClose}
        />
      );
  }
}
