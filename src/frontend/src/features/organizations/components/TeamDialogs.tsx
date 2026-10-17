import { useWorkContext } from '../../../shared/lib/workContext';
import type { Team } from '../api/organizationsApi';
import { useDeleteTeam, useRenameTeam, useSetDefaultTeam } from '../hooks/mutations';
import { DefaultTeamDialog } from './DefaultTeamDialog';
import { DeleteTeamDialog } from './DeleteTeamDialog';
import { NameDialog } from './NameDialog';
import { useNotify } from './notice';

/**
 * A team dialog to show: a team's rename or deletion, or the project's default
 * team (mod-017 design § 7.1), which belongs to no one team.
 */
export type TeamDialog = { kind: 'rename' | 'delete'; team: Team } | { kind: 'defaultTeam' } | null;

/**
 * The team dialogs of design § 7, shared by «Команды» and a team's page. Their
 * notices follow DC-7: deleting the team the switcher holds makes the switcher
 * fall back, and its notice is the only one (a team's page never chooses its
 * team, design § 3.4).
 */
export function TeamDialogs({
  organizationId,
  projectId,
  dialog,
  teams,
  onClose,
  onDeleted,
  onTeamsStale,
}: {
  organizationId: string;
  projectId: string;
  dialog: TeamDialog;
  /** The project's teams as the page read them; «Команда по умолчанию» offers them. */
  teams?: readonly Team[];
  /** `contextGone`: a not-found answer; the page reads itself again. */
  onClose(contextGone: boolean): void;
  onDeleted?(): void;
  /** A choice was refused as not found or not in the project: the teams are read again. */
  onTeamsStale?(): void;
}) {
  const rename = useRenameTeam(organizationId, projectId);
  const setDefaultTeam = useSetDefaultTeam(organizationId, projectId);
  const deleteTeam = useDeleteTeam(organizationId, projectId);
  const notify = useNotify();
  const context = useWorkContext();

  if (!dialog) {
    return null;
  }

  switch (dialog.kind) {
    case 'rename':
      return (
        <NameDialog
          title="Переименование команды"
          initialName={dialog.team.name}
          submitLabel="Сохранить"
          submit={(name) => rename.mutateAsync({ teamId: dialog.team.id, name })}
          onSucceeded={() => {
            onClose(false);
            notify('Команда переименована.');
          }}
          onClose={onClose}
        />
      );
    case 'defaultTeam':
      return (
        <DefaultTeamDialog
          teams={teams ?? []}
          submit={(teamId) => setDefaultTeam.mutateAsync(teamId)}
          onSucceeded={() => {
            onClose(false);
            notify('Команда по умолчанию изменена.');
          }}
          onRefused={() => onTeamsStale?.()}
          onClose={onClose}
        />
      );
    case 'delete': {
      const { team } = dialog;
      return (
        <DeleteTeamDialog
          organizationId={organizationId}
          projectId={projectId}
          teamId={team.id}
          teamName={team.name}
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
}
