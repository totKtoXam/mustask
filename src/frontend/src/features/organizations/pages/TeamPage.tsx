import { useState } from 'react';
import { useLocation, useNavigate, useParams } from 'react-router-dom';
import Box from '@mui/material/Box';
import DeleteOutlineOutlinedIcon from '@mui/icons-material/DeleteOutlineOutlined';
import DriveFileMoveOutlinedIcon from '@mui/icons-material/DriveFileMoveOutlined';
import EditOutlinedIcon from '@mui/icons-material/EditOutlined';
import LinkOffOutlinedIcon from '@mui/icons-material/LinkOffOutlined';
import NotesOutlinedIcon from '@mui/icons-material/NotesOutlined';
import { ObjectAccess } from '../../../shared/lib/objectAccessProvider';
import { useObjectAccess } from '../../../shared/lib/objectAccess';
import { apiErrorText } from '../../../shared/lib/apiErrorText';
import { useWorkContext } from '../../../shared/lib/workContext';
import {
  ActionButton,
  ColorLabel,
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
import { AccessDenied, NotFound } from '../../../shared/ui/StateFrame';
import type { TeamProject } from '../api/organizationsApi';
import { MoveTeamDialog } from '../components/MoveTeamDialog';
import { FactsPanel } from '../components/FactsPanel';
import { LinkDialog, type LinkOption } from '../components/LinkDialog';
import { useNotify } from '../components/notice';
import { TeamDialogs, type TeamDialog } from '../components/TeamDialogs';
import { useObjectCodesNotice } from '../hooks/useObjectCodesNotice';
import { codesChangedNotice } from '../lib/grantTexts';
import { GrantsSection } from '../components/GrantsSection';
import { grantsSection } from '../lib/grantTexts';
import { TeamMembersSection } from '../components/TeamMembersSection';
import { UnlinkDialog } from '../components/UnlinkDialog';
import { useLinkProjectsToTeam, useMoveTeam, useUnlinkTeam } from '../hooks/mutations';
import { useSelectorOffers } from '../hooks/navigationQueries';
import { useAreaTree, useTeam, useTeamMembers, useTeamProjects } from '../hooks/queries';
import { useObjectLink } from '../hooks/useObjectLink';
import { notFoundLevel } from '../lib/notFoundLevel';
import { linkDialog, moveTeamDialog, teamCommands, teamPage, unlinkDialog } from '../lib/teamTexts';
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
  const move = useMoveTeam(organizationId);
  const tree = useAreaTree(organizationId);
  const section = useLocation().pathname.split('/').filter(Boolean).pop() ?? 'overview';
  const members = useTeamMembers(organizationId, teamId, { enabled: section === 'overview' });
  const offers = useSelectorOffers(organizationId);
  const objectLink = useObjectLink(organizationId);
  const navigate = useNavigate();
  const notify = useNotify();
  const context = useWorkContext();
  const access = useObjectAccess(query.data?.codes);
  useObjectCodesNotice(teamId, query.data?.name, query.data?.codes, codesChangedNotice.team);
  const canWrite = access(organizationsPermissions.teamWrite, 'organization');
  const canDelete = access(organizationsPermissions.teamDelete, 'organization');
  // The team's end of a link; the other end's code is the picker's filter (`linkableFor`, api-contract § 4).
  const canLink = access(organizationsPermissions.teamLink, 'organization');
  const [dialog, setDialog] = useState<TeamDialog>(null);
  const [linking, setLinking] = useState(false);
  const [unlinking, setUnlinking] = useState<TeamProject | null>(null);
  // The move dialog (doc-016 § 4.2): while a project is being unlinked from its refusal, the unlink
  // dialog stands in its place and this keeps what the dialog held.
  const [moving, setMoving] = useState<{
    departmentId: string | null;
    refusal: string | null;
  } | null>(null);
  // The administrator's code, as the move route declares it (api-contract § 2).
  const canMove = access(organizationsPermissions.teamCreate, 'organization');

  const level = query.isError ? notFoundLevel(query.error) : null;
  if (level) {
    return <NotFound level={level} keepNavigation={level === 'team'} />;
  }
  if (section === 'settings' && query.isSuccess && !canWrite && !canDelete) {
    return <AccessDenied />;
  }

  const loading = query.isPending || (query.isError && query.isFetching);
  const team = query.isSuccess ? query.data : undefined;

  function closeDialogs(contextGone: boolean) {
    setDialog(null);
    setLinking(false);
    setUnlinking(null);
    setMoving(null);
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

  if (team && section === 'overview' && canMove) {
    commands.push({
      label: teamCommands.move,
      icon: <DriveFileMoveOutlinedIcon />,
      onClick: () => setMoving({ departmentId: null, refusal: null }),
    });
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
  if (rows.some((project) => project.canUnlink ?? canLink)) {
    columns.push({
      key: 'actions',
      header: '',
      align: 'right',
      width: 48,
      render: (project) =>
        (project.canUnlink ?? canLink) ? (
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
        ) : null,
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
          {
            label: teamPage.facts.department,
            value: team ? (
              <ColorLabel
                size="department"
                colour={tree.data?.find((node) => node.id === team.departmentId)?.color}
                name={team.departmentName ?? ''}
                to={objectLink('department', team.departmentId).to}
                linkState={objectLink('department', team.departmentId).state}
              />
            ) : undefined,
          },
          descriptionFact,
          { label: teamPage.facts.members, value: members.isSuccess ? members.data.length : '…' },
          { label: teamPage.facts.projects, value: projects.isSuccess ? rows.length : '…' },
        ];

  return (
    <ObjectAccess codes={query.data?.codes}>
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
      {team && section === 'members' && (
        <>
          <TeamMembersSection
            organizationId={organizationId}
            team={team}
            onContextGone={() => void query.refetch()}
          />
          <Box sx={{ mt: 4 }}>
            <GrantsSection
              organizationId={organizationId}
              scope="team"
              objectId={team.id}
              objectName={team.name}
              canGrant={access(organizationsPermissions.teamGrant, 'organization')}
              title={grantsSection.teamSectionAccess}
              onContextGone={() => void query.refetch()}
            />
          </Box>
        </>
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
      {moving && team && !unlinking && (
        <RequiresOrganizationCode code={organizationsPermissions.teamCreate}>
          <MoveTeamDialog
            organizationId={organizationId}
            team={team}
            projects={rows}
            initialDepartmentId={moving.departmentId}
            initialRefusal={moving.refusal}
            submit={(departmentId) => move.mutateAsync({ teamId: team.id, departmentId })}
            onUnlink={(project, departmentId, refusal) => {
              setMoving({ departmentId, refusal });
              setUnlinking(project);
            }}
            onSucceeded={(place) => {
              setMoving(null);
              notify(moveTeamDialog.moved(place.name, place.departmentName));
            }}
            onClose={closeDialogs}
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
              if (!fallsBack && !moving) {
                notify(unlinkDialog.unlinked);
              }
            }}
            onClose={(contextGone) => {
              if (moving && !contextGone) {
                setUnlinking(null);
                return;
              }
              closeDialogs(contextGone);
            }}
          />
        </RequiresOrganizationCode>
      )}
    </ObjectAccess>
  );
}
