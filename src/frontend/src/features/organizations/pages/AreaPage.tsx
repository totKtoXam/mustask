import { useState } from 'react';
import { Link as RouterLink, useNavigate, useParams } from 'react-router-dom';
import Box from '@mui/material/Box';
import Link from '@mui/material/Link';
import FolderOpenOutlinedIcon from '@mui/icons-material/FolderOpenOutlined';
import LinkOffOutlinedIcon from '@mui/icons-material/LinkOffOutlined';
import { useAccessCheck } from '../../../shared/lib/access';
import { apiErrorText } from '../../../shared/lib/apiErrorText';
import {
  ActionButton,
  DataTable,
  InlineEmpty,
  InlineNotice,
  MoreActionsMenu,
  Section,
  Tag,
  ThinAddIcon,
  TruncatedText,
  type DataColumn,
} from '../../../shared/ui';
import { ErrorBanner } from '../../../shared/ui/ErrorBanner';
import { FormSkeleton } from '../../../shared/ui/FormSkeleton';
import { PageHeader, type Command } from '../../../shared/ui/PageHeader';
import { RequiresOrganizationCode } from '../../../shared/ui/RequiresOrganizationCode';
import { NotFound } from '../../../shared/ui/StateFrame';
import type { AreaPage as AreaPageData, LinkedTeam } from '../api/organizationsApi';
import { AreaLinkDialog, AreaUnlinkDialog } from '../components/AreaLinkDialog';
import {
  AreaNodeDialogs,
  type NodeDialog,
  type NodeDialogResult,
} from '../components/AreaNodeDialogs';
import { AreaTypeMark, commandIcon } from '../components/AreaTypeMark';
import { FactsPanel, type Fact } from '../components/FactsPanel';
import { useNotify } from '../components/notice';
import { ProjectNodeTeams } from '../components/ProjectNodeTeams';
import { useLinkTeamsToArea, useUnlinkTeamFromArea } from '../hooks/mutations';
import { useArea, useAreaTeams, useAreaTree, useProject } from '../hooks/queries';
import { commandKinds, commandLabel } from '../lib/areaCommands';
import {
  ORGANIZATION_PLACE,
  areaCommands,
  areasPage,
  areaLinkDialog,
  areaPage,
  areaUnlinkDialog,
} from '../lib/areaTexts';
import { notFoundLevel } from '../lib/notFoundLevel';
import {
  organizationAreaPath,
  organizationAreasPath,
  organizationTeamPath,
  projectGeneralPath,
} from '../paths';
import { organizationsPermissions } from '../permissions';

type PageDialog =
  | { kind: 'node'; dialog: NodeDialog }
  | { kind: 'linkTeams' }
  | { kind: 'unlinkTeam'; team: LinkedTeam }
  | null;

function Muted({ children }: { children: string }) {
  return (
    <Box component="span" sx={{ color: 'text.secondary' }}>
      {children}
    </Box>
  );
}

/** The «Проект» fact: the project's name, a link to its «Общие». */
function ProjectFact({ organizationId, projectId }: { organizationId: string; projectId: string }) {
  const project = useProject(organizationId, projectId);
  return (
    <Link component={RouterLink} to={projectGeneralPath(organizationId, projectId)}>
      {project.data?.name ?? '…'}
    </Link>
  );
}

/** The read-only notice of a deactivated project's node, naming the project. */
function DeactivatedNotice({
  organizationId,
  projectId,
}: {
  organizationId: string;
  projectId: string;
}) {
  const tree = useAreaTree(organizationId);
  const name = tree.data?.find(
    (node) => node.type === 'project' && node.projectId === projectId,
  )?.name;
  return <InlineNotice severity="info">{areaPage.deactivatedNotice(name ?? '')}</InlineNotice>;
}

function factsOf(organizationId: string, area: AreaPageData) {
  const facts: Fact[] = [
    { label: areaPage.facts.name, value: area.name },
    { label: areaPage.facts.type, value: <AreaTypeMark type={area.type} /> },
  ];
  if (area.type === 'project') {
    facts.push({
      label: areaPage.facts.location,
      value: area.parent ? area.parent.path : ORGANIZATION_PLACE,
    });
  }
  if (area.type === 'area' && area.parent) {
    facts.push({
      label: areaPage.facts.parent,
      value: (
        <Link component={RouterLink} to={organizationAreaPath(organizationId, area.parent.id)}>
          {area.parent.path}
        </Link>
      ),
    });
  }
  if (area.type === 'area' && area.projectId) {
    facts.push({
      label: areaPage.facts.project,
      value: <ProjectFact organizationId={organizationId} projectId={area.projectId} />,
    });
  }
  facts.push({
    label: areaPage.facts.description,
    value: area.description ? (
      <Box component="span" sx={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>
        {area.description}
      </Box>
    ) : (
      <Muted>{areaPage.noDescription}</Muted>
    ),
  });
  return facts;
}

/**
 * A node's page (mod-018 design § 4; US-1..US-3), beneath «Области» at `…/settings/areas/:areaId`,
 * one page for the three types: the facts of the type, a deactivated project's read-only notice,
 * and the sections of the type — a department's projects and teams, a project node's and a
 * sub-area's nested areas, a project node's teams (the feat-028 links). Commands are
 * `commandKinds`'; a missing code hides the control. Deleted, foreign or dropped: «Область не
 * найдена» with the settings navigation kept.
 */
export function AreaPage() {
  const { organizationId = '', areaId = '' } = useParams<{
    organizationId: string;
    areaId: string;
  }>();
  const query = useArea(organizationId, areaId);
  const area = query.isSuccess ? query.data : undefined;
  const isDepartment = area?.type === 'department';
  const teams = useAreaTeams(organizationId, areaId, isDepartment);
  const tree = useAreaTree(organizationId, { enabled: isDepartment });
  const linkTeams = useLinkTeamsToArea(organizationId, areaId);
  const unlinkTeam = useUnlinkTeamFromArea(organizationId, areaId);
  const navigate = useNavigate();
  const notify = useNotify();
  const access = useAccessCheck();
  const canWrite = access(organizationsPermissions.areaWrite, 'organization');
  const canCreate = access(organizationsPermissions.areaCreate, 'organization');
  const canLink = access(organizationsPermissions.areaLink, 'organization');
  const canLinkTeams = canLink && access(organizationsPermissions.teamLink, 'organization');
  const [dialog, setDialog] = useState<PageDialog>(null);

  const level = query.isError ? notFoundLevel(query.error) : null;
  if (level) {
    return <NotFound level={level} keepNavigation={level === 'area'} />;
  }

  const loading = query.isPending || (query.isError && query.isFetching);

  function closeDialog(contextGone: boolean) {
    setDialog(null);
    if (contextGone) {
      void query.refetch();
      void teams.refetch();
      void tree.refetch();
    }
  }

  function done(result: NodeDialogResult) {
    setDialog(null);
    if (result.kind === 'deleted') {
      navigate(organizationAreasPath(organizationId));
    }
  }

  const children = area?.children ?? [];
  const commands: Command[] = [];
  if (area) {
    for (const kind of commandKinds(area, { canWrite, canCreate })) {
      if (kind === 'createChild') {
        continue;
      }
      commands.push({
        label: commandLabel(kind, area.type),
        icon: commandIcon(kind),
        onClick: () => {
          if (kind === 'delete') {
            setDialog({
              kind: 'node',
              dialog: { kind, node: area, hasChildren: children.length > 0 },
            });
          } else {
            setDialog({ kind: 'node', dialog: { kind, node: area } });
          }
        },
      });
    }
    if (area.type === 'project' && area.projectId && area.projectActive !== false) {
      commands.push({
        label: areaCommands.openProject,
        icon: <FolderOpenOutlinedIcon />,
        to: projectGeneralPath(organizationId, area.projectId),
      });
    }
  }

  const childColumns: DataColumn<(typeof children)[number]>[] = [
    {
      key: 'area',
      header:
        area?.type === 'department'
          ? areaPage.projects.columns.project
          : areaPage.children.columns.area,
      render: (child) => (
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, minWidth: 0 }}>
          <TruncatedText to={organizationAreaPath(organizationId, child.id)} look="link">
            {child.name}
          </TruncatedText>
          {tree.data?.find((node) => node.id === child.id)?.projectActive === false && (
            <Tag label={areasPage.deactivated} />
          )}
        </Box>
      ),
    },
    {
      key: 'description',
      header: areaPage.children.columns.description,
      render: (child) => <TruncatedText>{child.description ?? ''}</TruncatedText>,
    },
  ];

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
  // An unlink of a department's team is the team's own link code with the area's (api-contract § 5).
  if (canLink && access(organizationsPermissions.teamLink, 'organization')) {
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

  const showCreateChild =
    area !== undefined &&
    area.type !== 'department' &&
    commandKinds(area, { canWrite, canCreate }).includes('createChild');
  const childrenTitle =
    area?.type === 'department' ? areaPage.projects.title : areaPage.children.title;
  const childrenEmpty =
    area?.type === 'department' ? areaPage.projects.empty : areaPage.children.empty;
  const childrenLabel =
    area?.type === 'department' ? areaPage.projects.tableLabel : areaPage.children.tableLabel;

  return (
    <>
      <PageHeader
        title={area ? areaPage.title(area.type, area.name) : areaPage.titleLoading}
        commands={commands}
      />
      {area?.projectActive === false && area.projectId && (
        <Box sx={{ mb: 2 }}>
          <DeactivatedNotice organizationId={organizationId} projectId={area.projectId} />
        </Box>
      )}
      {loading ? (
        <FormSkeleton fields={3} />
      ) : query.isError ? (
        <ErrorBanner error={query.error} onRetry={() => void query.refetch()} />
      ) : (
        area && <FactsPanel facts={factsOf(organizationId, area)} />
      )}
      {area && (
        <>
          <Box sx={{ mt: 4 }}>
            <Section
              title={childrenTitle}
              count={children.length}
              actions={
                showCreateChild ? (
                  <ActionButton
                    label={areaCommands.createChild}
                    icon={<ThinAddIcon />}
                    onClick={() => {
                      if (area.projectId) {
                        setDialog({
                          kind: 'node',
                          dialog: {
                            kind: 'createSub',
                            projectId: area.projectId,
                            parentId: area.id,
                          },
                        });
                      }
                    }}
                  />
                ) : undefined
              }
            >
              {children.length === 0 ? (
                <InlineEmpty>{childrenEmpty}</InlineEmpty>
              ) : (
                <DataTable
                  aria-label={childrenLabel}
                  columns={childColumns}
                  rows={children}
                  rowKey={(child) => child.id}
                />
              )}
            </Section>
          </Box>
          {area.type === 'department' && (
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
                    error={
                      teams.isError && !teams.isFetching ? apiErrorText(teams.error) : undefined
                    }
                    onRetry={() => void teams.refetch()}
                  />
                )}
              </Section>
            </Box>
          )}
          {area.type === 'project' && area.projectId && (
            <ProjectNodeTeams
              organizationId={organizationId}
              projectId={area.projectId}
              active={area.projectActive !== false}
            />
          )}
        </>
      )}
      <AreaNodeDialogs
        organizationId={organizationId}
        dialog={dialog?.kind === 'node' ? dialog.dialog : null}
        canCreate={canCreate}
        canLinkTeams={canLinkTeams}
        onDone={done}
        onClose={closeDialog}
      />
      {area && dialog?.kind === 'linkTeams' && (
        <RequiresOrganizationCode code={organizationsPermissions.areaLink}>
          <AreaLinkDialog
            end="departmentTeams"
            organizationId={organizationId}
            ownName={area.name}
            linkedIds={teamRows.map((team) => team.id)}
            submit={(ids) => linkTeams.mutateAsync(ids)}
            onSucceeded={(chosen) => {
              setDialog(null);
              const [only] = chosen;
              const texts = areaLinkDialog.departmentTeams;
              notify(
                chosen.length === 1 && only
                  ? texts.linkedOne(only.name)
                  : texts.linkedMany(chosen.length),
              );
            }}
            onClose={closeDialog}
          />
        </RequiresOrganizationCode>
      )}
      {area && dialog?.kind === 'unlinkTeam' && (
        <RequiresOrganizationCode code={organizationsPermissions.areaLink}>
          <AreaUnlinkDialog
            kind="departmentTeam"
            name={dialog.team.name}
            ownName={area.name}
            action={() => unlinkTeam.mutateAsync(dialog.team.id)}
            onSucceeded={() => {
              setDialog(null);
              notify(areaUnlinkDialog.departmentTeam.done);
            }}
            onClose={closeDialog}
          />
        </RequiresOrganizationCode>
      )}
    </>
  );
}
