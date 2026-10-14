import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import DeleteOutlineOutlinedIcon from '@mui/icons-material/DeleteOutlineOutlined';
import EditOutlinedIcon from '@mui/icons-material/EditOutlined';
import { useAccessCheck } from '../../../shared/lib/access';
import { ErrorBanner } from '../../../shared/ui/ErrorBanner';
import { FormSkeleton } from '../../../shared/ui/FormSkeleton';
import { PageHeader, type Command } from '../../../shared/ui/PageHeader';
import { RequiresOrganizationCode } from '../../../shared/ui/RequiresOrganizationCode';
import { NotFound } from '../../../shared/ui/StateFrame';
import { FactsPanel } from '../components/FactsPanel';
import { TeamDialogs, type TeamDialog } from '../components/TeamDialogs';
import { TeamMembersSection } from '../components/TeamMembersSection';
import { useTeam, useTeams } from '../hooks/queries';
import { notFoundLevel } from '../lib/notFoundLevel';
import { teamsPath } from '../paths';
import { organizationsPermissions } from '../permissions';
import { FIRST_TEAM_CHIP } from './TeamsPage';

/**
 * A team's page (US-4, US-7, US-8; design § 10), beneath «Команды» at
 * `…/settings/teams/:teamId` (DC-4): its name and default area, renamed,
 * re-defaulted and deleted through the dialogs «Команды» uses, and — mod-016
 * — «Участники команды» under the facts. Its team is the page's entity, never
 * the switcher's (design § 3.4). «Команда не найдена» keeps the settings
 * navigation with «Команды» current (design § 12, P-3 (a)); an outer level not
 * found answers with its own frame. The commands that change the team exist
 * for the holder of `organizations.team.write` only (mod-016 design § 6.2);
 * the facts and the members are read-only for everyone else.
 */
export function TeamPage() {
  const {
    organizationId = '',
    projectId = '',
    teamId = '',
  } = useParams<{ organizationId: string; projectId: string; teamId: string }>();
  const query = useTeam(organizationId, projectId, teamId);
  const teams = useTeams(organizationId, projectId, { enabled: query.isSuccess });
  const navigate = useNavigate();
  const access = useAccessCheck();
  const canWrite = access(organizationsPermissions.teamWrite, 'organization');
  const [dialog, setDialog] = useState<TeamDialog>(null);

  const level = query.isError ? notFoundLevel(query.error) : null;
  if (level) {
    return <NotFound level={level} keepNavigation={level === 'team'} />;
  }

  const loading = query.isPending || (query.isError && query.isFetching);
  const team = query.isSuccess ? query.data : undefined;
  const firstTeamName = teams.data?.find((candidate) => candidate.isFirstTeam)?.name ?? '';

  const commands: Command[] = [];
  if (team && canWrite) {
    commands.push({
      label: 'Переименовать',
      icon: <EditOutlinedIcon />,
      onClick: () => setDialog({ kind: 'rename', team }),
    });
    if (!team.isFirstTeam) {
      commands.push({
        label: 'Удалить команду',
        icon: <DeleteOutlineOutlinedIcon />,
        onClick: () => setDialog({ kind: 'delete', team }),
      });
    }
  }

  return (
    <>
      <PageHeader title={team ? `Команда: ${team.name}` : 'Команда'} commands={commands} />
      {loading ? (
        <FormSkeleton fields={2} />
      ) : query.isError ? (
        <ErrorBanner error={query.error} onRetry={() => void query.refetch()} />
      ) : (
        team && (
          <>
            <FactsPanel
              facts={[
                {
                  label: 'Название',
                  value: (
                    <Box
                      component="span"
                      sx={{
                        display: 'inline-flex',
                        alignItems: 'center',
                        gap: 1,
                        flexWrap: 'wrap',
                      }}
                    >
                      {team.name}
                      {team.isFirstTeam && <Chip size="small" label={FIRST_TEAM_CHIP} />}
                    </Box>
                  ),
                },
                {
                  label: 'Область по умолчанию',
                  value: (
                    <Box
                      component="span"
                      sx={{
                        display: 'inline-flex',
                        alignItems: 'center',
                        gap: 1,
                        flexWrap: 'wrap',
                      }}
                    >
                      {team.defaultArea.path}
                      {!team.isFirstTeam && canWrite && (
                        <Button
                          variant="text"
                          size="small"
                          aria-label="Изменить область по умолчанию"
                          onClick={() => setDialog({ kind: 'defaultArea', team })}
                        >
                          Изменить
                        </Button>
                      )}
                    </Box>
                  ),
                  note: team.isFirstTeam
                    ? 'Корневая область проекта. У первой команды область по умолчанию не меняется.'
                    : undefined,
                },
              ]}
            />
            <TeamMembersSection
              organizationId={organizationId}
              projectId={projectId}
              team={team}
              onContextGone={() => void query.refetch()}
            />
          </>
        )
      )}
      <RequiresOrganizationCode code={organizationsPermissions.teamWrite}>
        <TeamDialogs
          organizationId={organizationId}
          projectId={projectId}
          dialog={dialog}
          firstTeamName={firstTeamName}
          onClose={(contextGone) => {
            setDialog(null);
            if (contextGone) {
              void query.refetch();
            }
          }}
          onDeleted={() => navigate(teamsPath(organizationId, projectId))}
        />
      </RequiresOrganizationCode>
    </>
  );
}
