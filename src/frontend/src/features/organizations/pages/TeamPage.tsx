import { useState } from 'react';
import { useLocation, useNavigate, useParams } from 'react-router-dom';
import Box from '@mui/material/Box';
import DeleteOutlineOutlinedIcon from '@mui/icons-material/DeleteOutlineOutlined';
import EditOutlinedIcon from '@mui/icons-material/EditOutlined';
import LinkOffOutlinedIcon from '@mui/icons-material/LinkOffOutlined';
import NotesOutlinedIcon from '@mui/icons-material/NotesOutlined';
import { useAccessCheck } from '../../../shared/lib/access';
import { apiErrorText } from '../../../shared/lib/apiErrorText';
import { useWorkContext } from '../../../shared/lib/workContext';
import {
  ActionButton,
  DataTable,
  InlineEmpty,
  MoreActionsMenu,
  Section,
  ThinAddIcon,
  TruncatedText,
  type DataColumn,
} from '../../../shared/ui';
import { ErrorBanner } from '../../../shared/ui/ErrorBanner';
import { FormSkeleton } from '../../../shared/ui/FormSkeleton';
import { PageHeader, type Command } from '../../../shared/ui/PageHeader';
import { RequiresOrganizationCode } from '../../../shared/ui/RequiresOrganizationCode';
import { NotFound } from '../../../shared/ui/StateFrame';
import type { TeamProject } from '../api/organizationsApi';
import { TeamDepartmentsSection } from '../components/TeamDepartmentsSection';
import { FactsPanel } from '../components/FactsPanel';
import { LinkDialog, type LinkOption } from '../components/LinkDialog';
import { useNotify } from '../components/notice';
import { TeamDialogs, type TeamDialog } from '../components/TeamDialogs';
import { TeamMembersSection } from '../components/TeamMembersSection';
import { UnlinkDialog } from '../components/UnlinkDialog';
import { useLinkProjectsToTeam, useUnlinkTeam } from '../hooks/mutations';
import { useSelectorOffers } from '../hooks/navigationQueries';
import { useTeam, useTeamMembers, useTeamProjects } from '../hooks/queries';
import { useObjectLink } from '../hooks/useObjectLink';
import { notFoundLevel } from '../lib/notFoundLevel';
import { linkDialog, teamCommands, teamPage, unlinkDialog } from '../lib/teamTexts';
import { organizationTeamsPath } from '../paths';
import { organizationsPermissions } from '../permissions';

/**
 * A team's page (feat-028 US-1, US-3..US-6; design § 4), beneath «Команды» of the organization at
 * `…/teams/:teamId` (feat-043; the retired `…/settings/teams/:teamId` is gone): the team no longer belongs to a project. Its name and «Описание»; its
 * active projects with the link and the unlink from this end; «Участники команды». The commands that
 * change the team are the holder's of `team.write` (rename, describe) and `team.delete`; the link and the
 * unlink need both link codes, and without them the section has no button and no menu column. The
 * page does not depend on a project: a team linked to none says so and offers «Добавить проект».
 * «Команда не найдена» keeps the settings navigation with «Команды» current.
 */
export function TeamPage() {
  const { organizationId = '', teamId = '' } = useParams<{
    organizationId: string;
    teamId: string;
  }>();
  const query = useTeam(organizationId, teamId);
  const projects = useTeamProjects(organizationId, teamId);
  const link = useLinkProjectsToTeam(organizationId, teamId);
  const unlink = useUnlinkTeam(organizationId);
  const section = useLocation().pathname.split('/').filter(Boolean).pop() ?? 'overview';
  const members = useTeamMembers(organizationId, teamId, { enabled: section === 'overview' });
  const offers = useSelectorOffers(organizationId);
  const objectLink = useObjectLink(organizationId);
  const navigate = useNavigate();
  const notify = useNotify();
  const context = useWorkContext();
  const access = useAccessCheck();
  const canWrite = access(organizationsPermissions.teamWrite, 'organization');
  const canDelete = access(organizationsPermissions.teamDelete, 'organization');
  const canLink =
    access(organizationsPermissions.teamLink, 'organization') &&
    access(organizationsPermissions.projectLink, 'organization');
  const [dialog, setDialog] = useState<TeamDialog>(null);
  const [linking, setLinking] = useState(false);
  const [unlinking, setUnlinking] = useState<TeamProject | null>(null);

  const level = query.isError ? notFoundLevel(query.error) : null;
  if (level) {
    return <NotFound level={level} keepNavigation={level === 'team'} />;
  }

  const loading = query.isPending || (query.isError && query.isFetching);
  const team = query.isSuccess ? query.data : undefined;

  function closeDialogs(contextGone: boolean) {
    setDialog(null);
    setLinking(false);
    setUnlinking(null);
    if (contextGone) {
      void query.refetch();
      void projects.refetch();
    }
  }

  const commands: Command[] = [];
  if (team && section === 'settings') {
    if (canWrite) {
      commands.push(
        {
          label: teamCommands.rename,
          icon: <EditOutlinedIcon />,
          onClick: () => setDialog({ kind: 'rename', team }),
        },
        {
          label: teamCommands.describe,
          icon: <NotesOutlinedIcon />,
          onClick: () => setDialog({ kind: 'describe', team }),
        },
      );
    }
    if (canDelete) {
      commands.push({
        label: teamCommands.delete,
        icon: <DeleteOutlineOutlinedIcon />,
        onClick: () => setDialog({ kind: 'delete', team }),
      });
    }
  }

  const rows = projects.data ?? [];
  const columns: DataColumn<TeamProject>[] = [
    {
      key: 'project',
      header: teamPage.projects.columns.project,
      render: (project) => (
        // D-3: the project becomes the most specific chosen object (design § 7.2).
        <TruncatedText
          to={objectLink('project', project.id).to}
          linkState={objectLink('project', project.id).state}
          look="link"
        >
          {project.name}
        </TruncatedText>
      ),
    },
    {
      key: 'key',
      header: teamPage.projects.columns.key,
      render: (project) => project.key,
    },
    {
      key: 'place',
      header: teamPage.projects.columns.place,
      render: (project) => (
        <TruncatedText>
          {offers.data?.projects.find((offer) => offer.id === project.id)?.departmentName ??
            teamPage.projects.topLevel}
        </TruncatedText>
      ),
    },
  ];
  if (canLink) {
    columns.push({
      key: 'actions',
      header: '',
      align: 'right',
      width: 48,
      render: (project) => (
        <MoreActionsMenu
          label={teamPage.projects.rowMenu(project.name)}
          size="row"
          items={[
            {
              label: teamPage.projects.unlink,
              icon: <LinkOffOutlinedIcon />,
              onSelect: () => setUnlinking(project),
              destructive: true,
            },
          ]}
        />
      ),
    });
  }

  function linkedNotice(chosen: LinkOption[]): string {
    const [only] = chosen;
    return chosen.length === 1 && only
      ? linkDialog.team.linkedOne(only.name)
      : linkDialog.team.linkedMany(chosen.length);
  }

  const allowed = ['overview', 'projects', 'members', 'settings'];
  if (team && !allowed.includes(section)) {
    return <NotFound level="page" />;
  }

  const descriptionFact = {
    label: teamPage.facts.description,
    value: team?.description ? (
      <Box component="span" sx={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>
        {team.description}
      </Box>
    ) : (
      <Box component="span" sx={{ color: 'text.secondary' }}>
        {teamPage.noDescription}
      </Box>
    ),
  };
  const facts =
    section === 'settings'
      ? [{ label: teamPage.facts.name, value: team?.name }, descriptionFact]
      : [
          descriptionFact,
          { label: teamPage.facts.members, value: members.isSuccess ? members.data.length : '…' },
          { label: teamPage.facts.projects, value: projects.isSuccess ? rows.length : '…' },
        ];

  return (
    <>
      <PageHeader
        title={team ? teamPage.title(team.name) : teamPage.titleLoading}
        commands={commands}
      />
      {loading ? (
        <FormSkeleton fields={2} />
      ) : query.isError ? (
        <ErrorBanner error={query.error} onRetry={() => void query.refetch()} />
      ) : (
        team && (section === 'overview' || section === 'settings') && <FactsPanel facts={facts} />
      )}
      {section === 'projects' && (
        <Box sx={{ mt: 4 }}>
          <Section
            title={teamPage.projects.title}
            count={projects.isSuccess ? rows.length : undefined}
            actions={
              canLink && team ? (
                <ActionButton
                  label={teamPage.projects.add}
                  icon={<ThinAddIcon />}
                  onClick={() => setLinking(true)}
                />
              ) : undefined
            }
          >
            {projects.isSuccess && rows.length === 0 ? (
              <InlineEmpty>{teamPage.projects.empty}</InlineEmpty>
            ) : (
              <DataTable
                aria-label={teamPage.projects.tableLabel}
                columns={columns}
                rows={rows}
                rowKey={(project) => project.id}
                loading={projects.isPending || (projects.isError && projects.isFetching)}
                error={
                  projects.isError && !projects.isFetching
                    ? apiErrorText(projects.error)
                    : undefined
                }
                onRetry={() => void projects.refetch()}
              />
            )}
          </Section>
        </Box>
      )}
      {team && section === 'overview' && (
        <TeamDepartmentsSection
          organizationId={organizationId}
          teamId={team.id}
          teamName={team.name}
        />
      )}
      {team && section === 'members' && (
        <TeamMembersSection
          organizationId={organizationId}
          team={team}
          onContextGone={() => void query.refetch()}
        />
      )}
      {dialog && (
        <RequiresOrganizationCode
          code={
            dialog.kind === 'delete'
              ? organizationsPermissions.teamDelete
              : organizationsPermissions.teamWrite
          }
        >
          <TeamDialogs
            organizationId={organizationId}
            dialog={dialog}
            onClose={closeDialogs}
            onDeleted={() => navigate(organizationTeamsPath(organizationId))}
          />
        </RequiresOrganizationCode>
      )}
      {linking && team && (
        <RequiresOrganizationCode code={organizationsPermissions.teamLink}>
          <LinkDialog
            end="team"
            organizationId={organizationId}
            ownName={team.name}
            linkedIds={rows.map((project) => project.id)}
            submit={(ids) => link.mutateAsync(ids)}
            onSucceeded={(chosen) => {
              setLinking(false);
              notify(linkedNotice(chosen));
            }}
            onClose={closeDialogs}
          />
        </RequiresOrganizationCode>
      )}
      {unlinking && team && (
        <RequiresOrganizationCode code={organizationsPermissions.teamLink}>
          <UnlinkDialog
            organizationId={organizationId}
            projectId={unlinking.id}
            projectName={unlinking.name}
            teamId={team.id}
            teamName={team.name}
            submit={async (destinationTeamId) => {
              const { selection } = context;
              const fallsBack =
                selection.organizationId === organizationId &&
                selection.projectId === unlinking.id &&
                selection.teamId === team.id;
              await unlink.mutateAsync({
                projectId: unlinking.id,
                teamId: team.id,
                destinationTeamId,
              });
              return fallsBack;
            }}
            onSucceeded={(fallsBack) => {
              setUnlinking(null);
              if (!fallsBack) {
                notify(unlinkDialog.unlinked);
              }
            }}
            onClose={closeDialogs}
          />
        </RequiresOrganizationCode>
      )}
    </>
  );
}
