import { useState } from 'react';
import { Link as RouterLink, useParams } from 'react-router-dom';
import Box from '@mui/material/Box';
import Link from '@mui/material/Link';
import Paper from '@mui/material/Paper';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableContainer from '@mui/material/TableContainer';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';
import Typography from '@mui/material/Typography';
import AddIcon from '@mui/icons-material/Add';
import FormatListNumberedIcon from '@mui/icons-material/FormatListNumbered';
import InboxOutlinedIcon from '@mui/icons-material/InboxOutlined';
import LinkOffOutlinedIcon from '@mui/icons-material/LinkOffOutlined';
import StarBorderIcon from '@mui/icons-material/StarBorder';
import ViewWeekOutlinedIcon from '@mui/icons-material/ViewWeekOutlined';
import { useAccessCheck } from '../../../shared/lib/access';
import { useWorkContext } from '../../../shared/lib/workContext';
import { Tag } from '../../../shared/ui';
import { ErrorBanner } from '../../../shared/ui/ErrorBanner';
import { PageHeader, type Command } from '../../../shared/ui/PageHeader';
import { RegistrySkeleton } from '../../../shared/ui/RegistrySkeleton';
import { RequiresOrganizationCode } from '../../../shared/ui/RequiresOrganizationCode';
import { NotFound, StateFrame } from '../../../shared/ui/StateFrame';
import { projectBacklogPath, projectBoardPath } from '../../boards';
import type { ProjectTeam } from '../api/organizationsApi';
import { DefaultTeamDialog } from '../components/DefaultTeamDialog';
import { LinkDialog, type LinkOption } from '../components/LinkDialog';
import { useNotify } from '../components/notice';
import { RowActionsMenu } from '../components/RowActionsMenu';
import { TruncatedName } from '../components/TruncatedName';
import { UnlinkDialog } from '../components/UnlinkDialog';
import { useLinkTeamsToProject, useSetDefaultTeam, useUnlinkTeam } from '../hooks/mutations';
import { useProject, useTeams } from '../hooks/queries';
import { DEFAULT_MARK_LABEL } from '../lib/defaultMark';
import { notFoundLevel } from '../lib/notFoundLevel';
import { linkDialog, projectTeamsPage, unlinkDialog } from '../lib/teamTexts';
import { organizationTeamPath, organizationTeamsPath } from '../paths';
import { organizationsPermissions } from '../permissions';

/** The project views «Бэклог проекта» and «Доска проекта» open on the level «Истории» (design § 7). */
const PROJECT_VIEWS_LEVEL = 'stories';

/** What the page asks of its user in a dialog. */
type Dialog =
  { kind: 'defaultTeam' } | { kind: 'link' } | { kind: 'unlink'; team: ProjectTeam } | null;

/**
 * The project's «Команды» (feat-028 US-2, US-3, US-4, US-6, US-7; design § 7): the teams of the
 * organization linked to the project, by name, as the server orders them — no team comes first. The
 * named default carries «По умолчанию», the only linked team «По умолчанию: единственная команда». A
 * team's name opens its page in the organization's settings; the row menu holds «Отключить от проекта»
 * alone — renaming and deleting a team are not done through a project (BR-OL-5). «Добавить команду» links
 * existing teams and needs both link codes; «Команда по умолчанию» is `project.write`'s and shown while a team
 * is linked. **A project with no team is the normal first state** (every project is in it right after the
 * upgrade, US-7), not a fault: the page says so. «Бэклог проекта» and «Доска проекта» (feat-028 OQ-2 A,
 * extended by feat-040 OQ-1 A, retired by feat-043) are offered to every role, always: the team pages
 * left the project's address, so this is the way into the project's own views.
 */
export function TeamsPage() {
  const { organizationId = '', projectId = '' } = useParams<{
    organizationId: string;
    projectId: string;
  }>();
  const query = useTeams(organizationId, projectId);
  const project = useProject(organizationId, projectId);
  const link = useLinkTeamsToProject(organizationId, projectId);
  const unlink = useUnlinkTeam(organizationId);
  const setDefaultTeam = useSetDefaultTeam(organizationId, projectId);
  const notify = useNotify();
  const context = useWorkContext();
  const access = useAccessCheck();
  const canLink =
    access(organizationsPermissions.teamLink, 'organization') &&
    access(organizationsPermissions.projectLink, 'organization');
  const canNameDefault = access(organizationsPermissions.projectWrite, 'organization');
  const [dialog, setDialog] = useState<Dialog>(null);

  const level = query.isError ? notFoundLevel(query.error) : null;
  if (level) {
    return <NotFound level={level} />;
  }

  function closeDialog(contextGone: boolean) {
    setDialog(null);
    if (contextGone) {
      void query.refetch();
    }
  }

  const teams = query.data ?? [];
  const loading = query.isPending || (query.isError && query.isFetching);
  const noTeam = query.isSuccess && teams.length === 0;
  const showActions = canLink;

  // feat-040 BR-TS-12 (OQ-1 A, design § 9): the interim way into the project's own views, always, to
  // every role and from the first paint — they need no read. Team pages left the project's address,
  // so no menu leads here any more (feat-043 retires it).
  const commands: Command[] = [
    {
      label: projectTeamsPage.projectBacklog,
      icon: <FormatListNumberedIcon />,
      to: projectBacklogPath(organizationId, projectId, PROJECT_VIEWS_LEVEL),
    },
    {
      label: projectTeamsPage.projectBoard,
      icon: <ViewWeekOutlinedIcon />,
      to: projectBoardPath(organizationId, projectId, PROJECT_VIEWS_LEVEL),
    },
  ];
  if (canNameDefault && query.isSuccess && teams.length > 0) {
    commands.push({
      label: projectTeamsPage.defaultTeam,
      icon: <StarBorderIcon />,
      onClick: () => setDialog({ kind: 'defaultTeam' }),
    });
  }

  function linkedNotice(chosen: LinkOption[]): string {
    const [only] = chosen;
    return chosen.length === 1 && only
      ? linkDialog.project.linkedOne(only.name)
      : linkDialog.project.linkedMany(chosen.length);
  }

  const openLink = () => setDialog({ kind: 'link' });

  return (
    <>
      <PageHeader
        title={projectTeamsPage.title}
        primaryAction={
          canLink
            ? { label: projectTeamsPage.add, icon: <AddIcon />, onClick: openLink }
            : undefined
        }
        commands={commands}
      />
      {query.isSuccess && (
        <Typography variant="body2" sx={{ color: 'text.secondary', mb: 2 }}>
          {projectTeamsPage.intro}
          <Link component={RouterLink} to={organizationTeamsPath(organizationId)}>
            {projectTeamsPage.introSection}
          </Link>
          {'.'}
        </Typography>
      )}
      {loading ? (
        <RegistrySkeleton columns={showActions ? ['Команда', ''] : ['Команда']} />
      ) : query.isError ? (
        <ErrorBanner error={query.error} onRetry={() => void query.refetch()} />
      ) : noTeam ? (
        // A normal state, not a fault: no warning colour (design § 7).
        <StateFrame
          icon={InboxOutlinedIcon}
          heading={projectTeamsPage.empty.heading}
          hint={canLink ? projectTeamsPage.empty.adminHint : projectTeamsPage.empty.otherHint}
          action={
            canLink
              ? { label: projectTeamsPage.add, icon: <AddIcon />, onClick: openLink }
              : undefined
          }
        />
      ) : (
        <TableContainer component={Paper} variant="outlined">
          <Table size="small" aria-label="Команды">
            <TableHead>
              <TableRow>
                <TableCell>Команда</TableCell>
                {showActions && <TableCell />}
              </TableRow>
            </TableHead>
            <TableBody>
              {teams.map((team) => (
                <TableRow key={team.id} hover>
                  <TableCell sx={{ maxWidth: 0, width: '100%' }}>
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, minWidth: 0 }}>
                      <Box sx={{ minWidth: 0 }}>
                        <TruncatedName
                          name={team.name}
                          to={organizationTeamPath(organizationId, team.id)}
                        />
                      </Box>
                      {team.defaultMark && (
                        <Box sx={{ flexShrink: 0 }}>
                          <Tag label={DEFAULT_MARK_LABEL[team.defaultMark]} />
                        </Box>
                      )}
                    </Box>
                  </TableCell>
                  {showActions && (
                    <TableCell align="right">
                      <RowActionsMenu
                        name={team.name}
                        actions={[
                          {
                            label: projectTeamsPage.unlink,
                            icon: <LinkOffOutlinedIcon fontSize="small" />,
                            onClick: () => setDialog({ kind: 'unlink', team }),
                            destructive: true,
                          },
                        ]}
                      />
                    </TableCell>
                  )}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}
      {dialog?.kind === 'defaultTeam' && (
        <RequiresOrganizationCode code={organizationsPermissions.projectWrite}>
          <DefaultTeamDialog
            teams={teams}
            submit={(teamId) => setDefaultTeam.mutateAsync(teamId)}
            onSucceeded={() => {
              setDialog(null);
              notify('Команда по умолчанию изменена.');
            }}
            onRefused={() => void query.refetch()}
            onClose={closeDialog}
          />
        </RequiresOrganizationCode>
      )}
      {dialog?.kind === 'link' && (
        <RequiresOrganizationCode code={organizationsPermissions.projectLink}>
          <LinkDialog
            end="project"
            organizationId={organizationId}
            ownName={project.data?.name ?? ''}
            linkedIds={teams.map((team) => team.id)}
            submit={(ids) => link.mutateAsync(ids)}
            onSucceeded={(chosen) => {
              setDialog(null);
              notify(linkedNotice(chosen));
            }}
            onClose={closeDialog}
          />
        </RequiresOrganizationCode>
      )}
      {dialog?.kind === 'unlink' && (
        <RequiresOrganizationCode code={organizationsPermissions.projectLink}>
          <UnlinkDialog
            organizationId={organizationId}
            projectId={projectId}
            projectName={project.data?.name ?? ''}
            teamId={dialog.team.id}
            teamName={dialog.team.name}
            submit={async (destinationTeamId) => {
              const { team } = dialog;
              const { selection } = context;
              const fallsBack =
                selection.organizationId === organizationId &&
                selection.projectId === projectId &&
                selection.teamId === team.id;
              await unlink.mutateAsync({ projectId, teamId: team.id, destinationTeamId });
              return fallsBack;
            }}
            onSucceeded={(fallsBack) => {
              setDialog(null);
              if (!fallsBack) {
                notify(unlinkDialog.unlinked);
              }
            }}
            onClose={closeDialog}
          />
        </RequiresOrganizationCode>
      )}
    </>
  );
}
