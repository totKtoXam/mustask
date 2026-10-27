import { useState } from 'react';
import { Link as RouterLink, useNavigate, useParams } from 'react-router-dom';
import Box from '@mui/material/Box';
import Link from '@mui/material/Link';
import DeleteOutlineOutlinedIcon from '@mui/icons-material/DeleteOutlineOutlined';
import DriveFileMoveOutlinedIcon from '@mui/icons-material/DriveFileMoveOutlined';
import EditOutlinedIcon from '@mui/icons-material/EditOutlined';
import LinkOffOutlinedIcon from '@mui/icons-material/LinkOffOutlined';
import NotesOutlinedIcon from '@mui/icons-material/NotesOutlined';
import { useAccessCheck } from '../../../shared/lib/access';
import { apiErrorText } from '../../../shared/lib/apiErrorText';
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
import type {
  AreaNode,
  AreaPage as AreaPageData,
  LinkedProject,
  LinkedTeam,
} from '../api/organizationsApi';
import {
  AreaLinkDialog,
  AreaUnlinkDialog,
  type AreaLinkOption,
} from '../components/AreaLinkDialog';
import { CreateAreaDialog, DeleteAreaDialog, MoveAreaDialog } from '../components/AreaDialogs';
import { DescriptionDialog } from '../components/DescriptionDialog';
import { FactsPanel } from '../components/FactsPanel';
import { NameDialog } from '../components/NameDialog';
import { useNotify } from '../components/notice';
import {
  useCreateArea,
  useDeleteArea,
  useDescribeArea,
  useLinkProjectsToArea,
  useLinkTeamsToArea,
  useMoveArea,
  useRenameArea,
  useUnlinkProjectFromArea,
  useUnlinkTeamFromArea,
} from '../hooks/mutations';
import { useArea, useAreaProjects, useAreaTeams, useAreaTree } from '../hooks/queries';
import {
  areaCommands,
  areaEditDialogs,
  areaLinkDialog,
  areaPage,
  areaUnlinkDialog,
  createAreaDialog,
  deleteAreaDialog,
  moveAreaDialog,
} from '../lib/areaTexts';
import { notFoundLevel } from '../lib/notFoundLevel';
import {
  organizationAreaPath,
  organizationAreasPath,
  organizationTeamPath,
  projectGeneralPath,
} from '../paths';
import { organizationsPermissions } from '../permissions';

type Dialog =
  | { kind: 'create' | 'rename' | 'describe' | 'move' | 'delete' | 'linkProjects' | 'linkTeams' }
  | { kind: 'unlinkProject'; project: LinkedProject }
  | { kind: 'unlinkTeam'; team: LinkedTeam }
  | null;

/** The tree node the move dialog needs, from the page's own read. */
function nodeOf(area: AreaPageData): AreaNode {
  return {
    id: area.id,
    parentId: area.parentId,
    name: area.name,
    description: area.description,
    path: area.path,
    depth: 0,
  };
}

/**
 * An area's page (mod-018 design § 4; US-1..US-3), beneath «Области» at `…/settings/areas/:areaId`: its
 * facts (name, parent as a link, description), its direct children, and its own projects and teams
 * with the link and the unlink from this end. Each command is the holder of its code's; a missing code
 * hides the control. Deleted, foreign or dropped by the upgrade: «Область не найдена» with the settings
 * navigation kept. Facts and each section load and fail on their own.
 */
export function AreaPage() {
  const { organizationId = '', areaId = '' } = useParams<{
    organizationId: string;
    areaId: string;
  }>();
  const query = useArea(organizationId, areaId);
  const projects = useAreaProjects(organizationId, areaId);
  const teams = useAreaTeams(organizationId, areaId);
  const tree = useAreaTree(organizationId, { enabled: false });
  const createArea = useCreateArea(organizationId);
  const renameArea = useRenameArea(organizationId);
  const describeArea = useDescribeArea(organizationId);
  const moveArea = useMoveArea(organizationId);
  const deleteArea = useDeleteArea(organizationId);
  const linkProjects = useLinkProjectsToArea(organizationId, areaId);
  const linkTeams = useLinkTeamsToArea(organizationId, areaId);
  const unlinkProject = useUnlinkProjectFromArea(organizationId, areaId);
  const unlinkTeam = useUnlinkTeamFromArea(organizationId, areaId);
  const navigate = useNavigate();
  const notify = useNotify();
  const access = useAccessCheck();
  const canWrite = access(organizationsPermissions.areaWrite, 'organization');
  const canCreate = access(organizationsPermissions.areaCreate, 'organization');
  const canLink = access(organizationsPermissions.areaLink, 'organization');
  const canLinkProjects = canLink && access(organizationsPermissions.projectLink, 'organization');
  const canLinkTeams = canLink && access(organizationsPermissions.teamLink, 'organization');
  const [dialog, setDialog] = useState<Dialog>(null);

  const level = query.isError ? notFoundLevel(query.error) : null;
  if (level) {
    return <NotFound level={level} keepNavigation={level === 'area'} />;
  }

  const loading = query.isPending || (query.isError && query.isFetching);
  const area = query.isSuccess ? query.data : undefined;

  function closeDialog(contextGone: boolean) {
    setDialog(null);
    if (contextGone) {
      void query.refetch();
      void projects.refetch();
      void teams.refetch();
      void tree.refetch();
    }
  }

  const commands: Command[] = [];
  if (area && canWrite) {
    commands.push(
      {
        label: areaCommands.rename,
        icon: <EditOutlinedIcon />,
        onClick: () => setDialog({ kind: 'rename' }),
      },
      {
        label: areaCommands.describe,
        icon: <NotesOutlinedIcon />,
        onClick: () => setDialog({ kind: 'describe' }),
      },
      {
        label: areaCommands.move,
        icon: <DriveFileMoveOutlinedIcon />,
        onClick: () => setDialog({ kind: 'move' }),
      },
      {
        label: areaCommands.delete,
        icon: <DeleteOutlineOutlinedIcon />,
        onClick: () => setDialog({ kind: 'delete' }),
      },
    );
  }

  const children = area?.children ?? [];
  const childColumns: DataColumn<(typeof children)[number]>[] = [
    {
      key: 'area',
      header: areaPage.children.columns.area,
      render: (child) => (
        <TruncatedText to={organizationAreaPath(organizationId, child.id)} look="link">
          {child.name}
        </TruncatedText>
      ),
    },
    {
      key: 'description',
      header: areaPage.children.columns.description,
      render: (child) => <TruncatedText>{child.description ?? ''}</TruncatedText>,
    },
  ];

  const projectRows = projects.data ?? [];
  const projectColumns: DataColumn<LinkedProject>[] = [
    {
      key: 'project',
      header: areaPage.projects.columns.project,
      render: (project) => (
        // D-4: the project's «Общие», the other end of the link, where «Области» lists the area back.
        <TruncatedText to={projectGeneralPath(organizationId, project.id)} look="link">
          {project.name}
        </TruncatedText>
      ),
    },
    { key: 'key', header: areaPage.projects.columns.key, render: (project) => project.key },
  ];
  if (canLink) {
    projectColumns.push({
      key: 'actions',
      header: '',
      align: 'right',
      width: 48,
      render: (project) => (
        <MoreActionsMenu
          label={areaPage.rowMenu(project.name)}
          size="row"
          items={[
            {
              label: areaPage.projects.unlink,
              icon: <LinkOffOutlinedIcon />,
              onSelect: () => setDialog({ kind: 'unlinkProject', project }),
              destructive: true,
            },
          ]}
        />
      ),
    });
  }

  const teamRows = teams.data ?? [];
  const teamColumns: DataColumn<LinkedTeam>[] = [
    {
      key: 'team',
      header: areaPage.teams.columns.team,
      render: (team) => (
        <TruncatedText to={organizationTeamPath(organizationId, team.id)} look="link">
          {team.name}
        </TruncatedText>
      ),
    },
  ];
  if (canLink) {
    teamColumns.push({
      key: 'actions',
      header: '',
      align: 'right',
      width: 48,
      render: (team) => (
        <MoreActionsMenu
          label={areaPage.rowMenu(team.name)}
          size="row"
          items={[
            {
              label: areaPage.teams.unlink,
              icon: <LinkOffOutlinedIcon />,
              onSelect: () => setDialog({ kind: 'unlinkTeam', team }),
              destructive: true,
            },
          ]}
        />
      ),
    });
  }

  function linkedNotice(chosen: AreaLinkOption[], end: 'areaProjects' | 'areaTeams'): string {
    const [only] = chosen;
    const texts = areaLinkDialog[end];
    return chosen.length === 1 && only
      ? texts.linkedOne(only.name)
      : texts.linkedMany(chosen.length);
  }

  return (
    <>
      <PageHeader
        title={area ? areaPage.title(area.name) : areaPage.titleLoading}
        commands={commands}
      />
      {loading ? (
        <FormSkeleton fields={3} />
      ) : query.isError ? (
        <ErrorBanner error={query.error} onRetry={() => void query.refetch()} />
      ) : (
        area && (
          <FactsPanel
            facts={[
              { label: areaPage.facts.name, value: area.name },
              {
                label: areaPage.facts.parent,
                value: area.parent ? (
                  <Link
                    component={RouterLink}
                    to={organizationAreaPath(organizationId, area.parent.id)}
                  >
                    {area.parent.path}
                  </Link>
                ) : (
                  <Box component="span" sx={{ color: 'text.secondary' }}>
                    {areaPage.topLevel}
                  </Box>
                ),
              },
              {
                label: areaPage.facts.description,
                value: area.description ? (
                  <Box component="span" sx={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>
                    {area.description}
                  </Box>
                ) : (
                  <Box component="span" sx={{ color: 'text.secondary' }}>
                    {areaPage.noDescription}
                  </Box>
                ),
              },
            ]}
          />
        )
      )}
      {area && (
        <>
          <Box sx={{ mt: 4 }}>
            <Section
              title={areaPage.children.title}
              count={children.length}
              actions={
                canWrite ? (
                  <ActionButton
                    label={areaCommands.createChild}
                    icon={<ThinAddIcon />}
                    onClick={() => setDialog({ kind: 'create' })}
                  />
                ) : undefined
              }
            >
              {children.length === 0 ? (
                <InlineEmpty>{areaPage.children.empty}</InlineEmpty>
              ) : (
                <DataTable
                  aria-label={areaPage.children.tableLabel}
                  columns={childColumns}
                  rows={children}
                  rowKey={(child) => child.id}
                />
              )}
            </Section>
          </Box>
          <Box sx={{ mt: 4 }}>
            <Section
              title={areaPage.projects.title}
              count={projects.isSuccess ? projectRows.length : undefined}
              actions={
                canLinkProjects ? (
                  <ActionButton
                    label={areaPage.projects.add}
                    icon={<ThinAddIcon />}
                    onClick={() => setDialog({ kind: 'linkProjects' })}
                  />
                ) : undefined
              }
            >
              {projects.isSuccess && projectRows.length === 0 ? (
                <InlineEmpty>{areaPage.projects.empty}</InlineEmpty>
              ) : (
                <DataTable
                  aria-label={areaPage.projects.tableLabel}
                  columns={projectColumns}
                  rows={projectRows}
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
          <Box sx={{ mt: 4 }}>
            <Section
              title={areaPage.teams.title}
              count={teams.isSuccess ? teamRows.length : undefined}
              actions={
                canLinkTeams ? (
                  <ActionButton
                    label={areaPage.teams.add}
                    icon={<ThinAddIcon />}
                    onClick={() => setDialog({ kind: 'linkTeams' })}
                  />
                ) : undefined
              }
            >
              {teams.isSuccess && teamRows.length === 0 ? (
                <InlineEmpty>{areaPage.teams.empty}</InlineEmpty>
              ) : (
                <DataTable
                  aria-label={areaPage.teams.tableLabel}
                  columns={teamColumns}
                  rows={teamRows}
                  rowKey={(team) => team.id}
                  loading={teams.isPending || (teams.isError && teams.isFetching)}
                  error={teams.isError && !teams.isFetching ? apiErrorText(teams.error) : undefined}
                  onRetry={() => void teams.refetch()}
                />
              )}
            </Section>
          </Box>
        </>
      )}
      {area && dialog && (
        <RequiresOrganizationCode
          code={
            dialog.kind === 'linkProjects' ||
            dialog.kind === 'linkTeams' ||
            dialog.kind === 'unlinkProject' ||
            dialog.kind === 'unlinkTeam'
              ? organizationsPermissions.areaLink
              : organizationsPermissions.areaWrite
          }
        >
          {dialog.kind === 'create' && (
            <CreateAreaDialog
              organizationId={organizationId}
              initialParentId={area.id}
              canTopLevel={canCreate}
              canLinkProjects={canLinkProjects}
              canLinkTeams={canLinkTeams}
              submit={(body) => createArea.mutateAsync(body)}
              onSucceeded={(created) => {
                setDialog(null);
                notify(createAreaDialog.created(created.name));
              }}
              onClose={closeDialog}
            />
          )}
          {dialog.kind === 'rename' && (
            <NameDialog
              title={areaEditDialogs.rename.title}
              helper={areaEditDialogs.rename.helper}
              initialName={area.name}
              submitLabel={areaEditDialogs.rename.submit}
              submit={(name) => renameArea.mutateAsync({ areaId: area.id, name })}
              onSucceeded={() => {
                setDialog(null);
                notify(areaEditDialogs.rename.renamed);
              }}
              onClose={closeDialog}
            />
          )}
          {dialog.kind === 'describe' && (
            <DescriptionDialog
              title={areaEditDialogs.description.title}
              initial={area.description}
              submit={(description) => describeArea.mutateAsync({ areaId: area.id, description })}
              onSucceeded={() => {
                setDialog(null);
                notify(areaEditDialogs.description.saved);
              }}
              onClose={closeDialog}
            />
          )}
          {dialog.kind === 'move' && (
            <MoveAreaDialog
              organizationId={organizationId}
              area={nodeOf(area)}
              canTopLevel={canCreate}
              submit={(parentId) => moveArea.mutateAsync({ areaId: area.id, parentId })}
              onSucceeded={() => {
                setDialog(null);
                notify(moveAreaDialog.moved(area.name));
              }}
              onClose={closeDialog}
            />
          )}
          {dialog.kind === 'delete' && (
            <DeleteAreaDialog
              organizationId={organizationId}
              area={area}
              hasChildren={area.children.length > 0}
              action={() => deleteArea.mutateAsync(area.id)}
              onSucceeded={() => {
                setDialog(null);
                navigate(organizationAreasPath(organizationId));
                notify(deleteAreaDialog.deleted(area.name));
              }}
              onClose={closeDialog}
            />
          )}
          {dialog.kind === 'linkProjects' && (
            <AreaLinkDialog
              end="areaProjects"
              organizationId={organizationId}
              ownName={area.name}
              linkedIds={projectRows.map((project) => project.id)}
              submit={(ids) => linkProjects.mutateAsync(ids)}
              onSucceeded={(chosen) => {
                setDialog(null);
                notify(linkedNotice(chosen, 'areaProjects'));
              }}
              onClose={closeDialog}
            />
          )}
          {dialog.kind === 'linkTeams' && (
            <AreaLinkDialog
              end="areaTeams"
              organizationId={organizationId}
              ownName={area.name}
              linkedIds={teamRows.map((team) => team.id)}
              submit={(ids) => linkTeams.mutateAsync(ids)}
              onSucceeded={(chosen) => {
                setDialog(null);
                notify(linkedNotice(chosen, 'areaTeams'));
              }}
              onClose={closeDialog}
            />
          )}
          {dialog.kind === 'unlinkProject' && (
            <AreaUnlinkDialog
              kind="areaProject"
              name={dialog.project.name}
              ownName={area.name}
              action={() => unlinkProject.mutateAsync(dialog.project.id)}
              onSucceeded={() => {
                setDialog(null);
                notify(areaUnlinkDialog.areaProject.done);
              }}
              onClose={closeDialog}
            />
          )}
          {dialog.kind === 'unlinkTeam' && (
            <AreaUnlinkDialog
              kind="areaTeam"
              name={dialog.team.name}
              ownName={area.name}
              action={() => unlinkTeam.mutateAsync(dialog.team.id)}
              onSucceeded={() => {
                setDialog(null);
                notify(areaUnlinkDialog.areaTeam.done);
              }}
              onClose={closeDialog}
            />
          )}
        </RequiresOrganizationCode>
      )}
    </>
  );
}
