import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import { ActionButton, TruncatedText } from '../../../shared/ui';
import { RequiresCode } from '../../../shared/ui/RequiresOrganizationCode';
import type { ProjectTeam } from '../api/organizationsApi';
import { useUnlinkTeam } from '../hooks/mutations';
import { moveAreaDialog } from '../lib/areaTexts';
import { UnlinkDialog } from './UnlinkDialog';

/**
 * The refusal of a project's move while a team is linked (mod-022 BR-TD-4, design § 5.2): the server's
 * text, then the project's teams, each with «Отключить». Shared by the move dialog and «Добавить проект».
 */
export function ProjectTeamsRefusal({
  text,
  teams,
  onUnlink,
}: {
  text: string;
  teams: readonly ProjectTeam[];
  onUnlink(team: ProjectTeam): void;
}) {
  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
      <Typography variant="body2">{text}</Typography>
      {teams.length > 0 && <Typography variant="body2">{moveAreaDialog.project.teams}</Typography>}
      {teams.map((team) => (
        <Box key={team.id} sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <TruncatedText>{team.name}</TruncatedText>
          {(team.canUnlink ?? true) && (
            <ActionButton
              label={moveAreaDialog.project.unlink}
              tone="subtle"
              size="compact"
              aria-label={moveAreaDialog.project.unlinkRow(team.name)}
              onClick={() => onUnlink(team)}
            />
          )}
        </Box>
      ))}
    </Box>
  );
}

/** The unlink dialog that stands in the host dialog's place while a team of the refusal is unlinked. */
export function UnlinkProjectTeamDialog({
  organizationId,
  projectId,
  projectName,
  team,
  onDone,
  onClose,
}: {
  organizationId: string;
  projectId: string;
  projectName: string;
  team: ProjectTeam;
  onDone(): void;
  /** `contextGone`: the project or the team answered not found. */
  onClose(contextGone: boolean): void;
}) {
  const unlink = useUnlinkTeam(organizationId);
  return (
    <RequiresCode holds={team.canUnlink ?? true}>
      <UnlinkDialog
        organizationId={organizationId}
        projectId={projectId}
        projectName={projectName}
        teamId={team.id}
        teamName={team.name}
        submit={(destinationTeamId) =>
          unlink.mutateAsync({ projectId, teamId: team.id, destinationTeamId })
        }
        onSucceeded={onDone}
        onClose={onClose}
      />
    </RequiresCode>
  );
}
