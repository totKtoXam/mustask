import { useWorkContext } from '../../../shared/lib/workContext';
import type { Team } from '../api/organizationsApi';
import { useDeleteTeam, useRenameTeam, useSetTeamDefaultArea } from '../hooks/mutations';
import { DefaultAreaDialog } from './DefaultAreaDialog';
import { DeleteTeamDialog } from './DeleteTeamDialog';
import { NameDialog } from './NameDialog';
import { useNotify } from './notice';

/** A team dialog to show: the team's rename, default area or deletion. */
export type TeamDialog = { kind: 'rename' | 'defaultArea' | 'delete'; team: Team } | null;

/**
 * The team dialogs of design § 9.5–§ 9.7, shared by «Команды» and a team's
 * page. Their notices follow DC-7: deleting the team the switcher holds makes
 * the switcher fall back, and its notice is the only one (a team's page never
 * chooses its team, design § 3.4).
 */
export function TeamDialogs({
  organizationId,
  projectId,
  dialog,
  firstTeamName,
  onClose,
  onDeleted,
}: {
  organizationId: string;
  projectId: string;
  dialog: TeamDialog;
  firstTeamName: string;
  /** `contextGone`: a not-found answer; the page reads itself again. */
  onClose(contextGone: boolean): void;
  onDeleted?(): void;
}) {
  const rename = useRenameTeam(organizationId, projectId);
  const setDefaultArea = useSetTeamDefaultArea(organizationId, projectId);
  const deleteTeam = useDeleteTeam(organizationId, projectId);
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
          title="Переименование команды"
          helper="Названия областей команды не изменятся."
          initialName={team.name}
          submitLabel="Сохранить"
          submit={(name) => rename.mutateAsync({ teamId: team.id, name })}
          onSucceeded={() => {
            onClose(false);
            notify('Команда переименована.');
          }}
          onClose={onClose}
        />
      );
    case 'defaultArea':
      return (
        <DefaultAreaDialog
          organizationId={organizationId}
          projectId={projectId}
          team={team}
          submit={(areaId) => setDefaultArea.mutateAsync({ teamId: team.id, areaId })}
          onSucceeded={() => {
            onClose(false);
            notify('Область по умолчанию изменена.');
          }}
          onClose={onClose}
        />
      );
    case 'delete':
      return (
        <DeleteTeamDialog
          teamName={team.name}
          firstTeamName={firstTeamName}
          action={async () => {
            const { selection } = context;
            const fallsBack =
              selection.organizationId === organizationId &&
              selection.projectId === projectId &&
              selection.teamId === team.id;
            await deleteTeam.mutateAsync(team.id);
            return fallsBack;
          }}
          onSucceeded={(fallsBack) => {
            onClose(false);
            onDeleted?.();
            if (!fallsBack) {
              notify(`Команда «${team.name}» удалена.`);
            }
          }}
          onClose={onClose}
        />
      );
  }
}
