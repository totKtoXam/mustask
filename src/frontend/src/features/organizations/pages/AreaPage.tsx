import { useState } from 'react';
import { Link as RouterLink, useLocation, useNavigate, useParams } from 'react-router-dom';
import Box from '@mui/material/Box';
import Link from '@mui/material/Link';
import DriveFileMoveOutlinedIcon from '@mui/icons-material/DriveFileMoveOutlined';
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
import { ConfirmAction } from '../components/ConfirmAction';
import { FactsPanel, type Fact } from '../components/FactsPanel';
import { useNotify } from '../components/notice';
import { PlaceProjectDialog } from '../components/PlaceProjectDialog';
import { useLinkTeamsToArea, useMoveArea, useUnlinkTeamFromArea } from '../hooks/mutations';
import { useArea, useAreaTeams, useAreaTree } from '../hooks/queries';
import { useObjectLink } from '../hooks/useObjectLink';
import { commandKinds, commandLabel } from '../lib/areaCommands';
import {
  areaCommands,
  areasPage,
  areaLinkDialog,
  areaPage,
  areaUnlinkDialog,
  liftProjectDialog,
  moveAreaDialog,
  placeProjectDialog,
} from '../lib/areaTexts';
import { notFoundLevel } from '../lib/notFoundLevel';
import {
  organizationAreaPath,
  areaProjectsPath,
  areaTeamsPath,
  organizationAreasPath,
} from '../paths';
import { organizationsPermissions } from '../permissions';

type PageDialog =
  | { kind: 'node'; dialog: NodeDialog }
  | { kind: 'place' }
  | { kind: 'lift'; project: { id: string; name: string } }
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

/** The «Проект» fact: the project's name, a link to its «Общие» that makes it the chosen project. */
function ProjectFact({ organizationId, projectId }: { organizationId: string; projectId: string }) {
  // From the tree node: the project read answers 404 when the project is deactivated (TC-4).
  const tree = useAreaTree(organizationId);
  const objectLink = useObjectLink(organizationId);
  const link = objectLink('project', projectId);
  const name = tree.data?.find(
    (node) => node.type === 'project' && node.projectId === projectId,
  )?.name;
  return (
    <Link component={RouterLink} to={link.to} state={link.state}>
      {name ?? '…'}
    </Link>
  );
}

function descriptionFact(area: AreaPageData): Fact {
  return {
    label: areaPage.facts.description,
    value: area.description ? (
      <Box component="span" sx={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>
        {area.description}
      </Box>
    ) : (
      <Muted>{areaPage.noDescription}</Muted>
    ),
  };
}

/**
 * The facts of «Обзор» (design § 6): a department's description, place and the two stat links; a
 * project's node its type, place and description; a sub-area its parent, project and description.
 */
function overviewFacts(
  organizationId: string,
  area: AreaPageData,
  counts: { projects: number; teams: number | undefined },
): Fact[] {
  if (area.type === 'department') {
    const facts: Fact[] = [
      descriptionFact(area),
      { label: areaPage.facts.location, value: areaPage.topLevel },
      {
        label: areaPage.facts.projects,
        value: (
          <Link component={RouterLink} to={areaProjectsPath(organizationId, area.id)}>
            {counts.projects}
          </Link>
        ),
      },
    ];
    if (counts.teams !== undefined) {
      facts.push({
        label: areaPage.facts.teams,
        value: (
          <Link component={RouterLink} to={areaTeamsPath(organizationId, area.id)}>
            {counts.teams}
          </Link>
        ),
      });
    }
    return facts;
  }
  if (area.type === 'project') {
    return [
      { label: areaPage.facts.type, value: <AreaTypeMark type={area.type} /> },
      {
        label: areaPage.facts.location,
        value: area.parent ? area.parent.path : areaPage.topLevel,
      },
      descriptionFact(area),
    ];
  }
  const facts: Fact[] = [];
  if (area.parent) {
    facts.push({
      label: areaPage.facts.parent,
      value: (
        <Link component={RouterLink} to={organizationAreaPath(organizationId, area.parent.id)}>
          {area.parent.path}
        </Link>
      ),
    });
  }
  if (area.projectId) {
    facts.push({
      label: areaPage.facts.project,
      value: <ProjectFact organizationId={organizationId} projectId={area.projectId} />,
    });
  }
  facts.push(descriptionFact(area));
  return facts;
}

/** The facts of «Параметры»: the name and the description the commands change. */
function settingsFacts(area: AreaPageData): Fact[] {
  return [{ label: areaPage.facts.name, value: area.name }, descriptionFact(area)];
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
  const tree = useAreaTree(organizationId);
  const objectLink = useObjectLink(organizationId);
  const area = query.isSuccess ? query.data : undefined;
  const isDepartment = area?.type === 'department';
  const teams = useAreaTeams(organizationId, areaId, isDepartment);
  const linkTeams = useLinkTeamsToArea(organizationId, areaId);
  const unlinkTeam = useUnlinkTeamFromArea(organizationId, areaId);
  const moveArea = useMoveArea(organizationId);
  const navigate = useNavigate();
  const notify = useNotify();
  const access = useAccessCheck();
  const canWrite = access(organizationsPermissions.areaWrite, 'organization');
  const canCreate = access(organizationsPermissions.areaCreate, 'organization');
  const canLink = access(organizationsPermissions.areaLink, 'organization');
  const canLinkTeams = canLink && access(organizationsPermissions.teamLink, 'organization');
  const [dialog, setDialog] = useState<PageDialog>(null);
  const section = useLocation().pathname.split('/').filter(Boolean).pop() ?? 'overview';

  const level = query.isError ? notFoundLevel(query.error) : null;
  if (level) {
    return <NotFound level={level} keepNavigation={level === 'area'} />;
  }
  // A deactivated project's node and sub-areas open nothing (BR-NV-13; OQ-3 A).
  if (area?.projectActive === false) {
    return <NotFound level="project" keepNavigation />;
  }

  const loading = query.isPending || (query.isError && query.isFetching);

  function closeDialog(contextGone: boolean) {
    setDialog(null);
    if (contextGone) {
      void query.refetch();
      void teams.refetch();
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
    for (const kind of section === 'settings' ? commandKinds(area, { canWrite, canCreate }) : []) {
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
    if (section === 'overview' && area.type === 'project' && area.projectId) {
      commands.push({
        label: areaCommands.openProject,
        icon: <FolderOpenOutlinedIcon />,
        to: objectLink('project', area.projectId).to,
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
      render: (child) => {
        // A project of a department opens the project's «Общие» and becomes the chosen project
        // (BR-NV-19); a sub-area opens its own page. A deactivated project is plain text (OQ-3 A).
        const projectId =
          child.type === 'project'
            ? (tree.data?.find((node) => node.id === child.id)?.projectId ?? null)
            : null;
        const deactivated = child.projectActive === false;
        const link = projectId !== null ? objectLink('project', projectId) : null;
        const to =
          child.type === 'project'
            ? deactivated || link === null
              ? undefined
              : link.to
            : organizationAreaPath(organizationId, child.id);
        return (
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, minWidth: 0 }}>
            <TruncatedText
              to={to}
              linkState={to === link?.to ? link?.state : undefined}
              look="link"
            >
              {child.name}
            </TruncatedText>
            {deactivated && <Tag label={areasPage.deactivated} />}
          </Box>
        );
      },
    },
    {
      key: 'description',
      header: areaPage.children.columns.description,
      render: (child) => <TruncatedText>{child.description ?? ''}</TruncatedText>,
    },
  ];
  // A project moves to the top level for the holder of both codes (design § 8.3; api-contract § 1).
  const canLift = canWrite && canCreate;
  if (area?.type === 'department' && canLift) {
    childColumns.push({
      key: 'actions',
      header: '',
      align: 'right',
      width: 48,
      render: (child) =>
        child.projectActive === false ? null : (
          <MoreActionsMenu
            label={areaPage.rowMenu(child.name)}
            size="row"
            items={[
              {
                label: areaPage.projects.liftToTop,
                icon: <DriveFileMoveOutlinedIcon />,
                onSelect: () => setDialog({ kind: 'lift', project: child }),
              },
            ]}
          />
        ),
    });
  }
  if (area?.type === 'department') {
    childColumns.splice(1, 0, {
      key: 'key',
      header: areaPage.projects.columns.key,
      render: (child) => <TruncatedText>{child.projectKey ?? ''}</TruncatedText>,
    });
  }

  const teamRows = teams.data ?? [];
  const teamColumns: DataColumn<LinkedTeam>[] = [
    {
      key: 'team',
      header: areaPage.teams.columns.team,
      render: (team) => {
        // The team becomes the most specific chosen object; the department stays when it fits.
        const link = objectLink('team', team.id);
        return (
          <TruncatedText to={link.to} linkState={link.state} look="link">
            {team.name}
          </TruncatedText>
        );
      },
    },
  ];
  // The department-end unlink is `area.link` alone (api-contract § 5, DELETE /areas/{a}/teams/{t}).
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

  // The menu of a project's node or a sub-area has no «Проекты» and no «Команды» (design § 6).
  const allowed = isDepartment
    ? ['overview', 'projects', 'teams', 'settings']
    : ['overview', 'settings'];
  if (area && !allowed.includes(section)) {
    return <NotFound level="page" />;
  }

  const deactivatedCount = children.filter((child) => child.projectActive === false).length;
  const childrenSection = (
    <Section
      title={childrenTitle}
      count={children.length}
      actions={
        isDepartment ? (
          <>
            {deactivatedCount > 0 && (
              <Tag label={areaPage.projects.deactivatedCount(deactivatedCount)} />
            )}
            {canWrite && (
              <ActionButton
                label={areaPage.projects.add}
                icon={<ThinAddIcon />}
                onClick={() => setDialog({ kind: 'place' })}
              />
            )}
          </>
        ) : showCreateChild ? (
          <ActionButton
            label={areaCommands.createChild}
            icon={<ThinAddIcon />}
            onClick={() => {
              if (area?.projectId) {
                setDialog({
                  kind: 'node',
                  dialog: { kind: 'createSub', projectId: area.projectId, parentId: area.id },
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
  );

  return (
    <>
      <PageHeader
        title={area ? areaPage.title(area.type, area.name) : areaPage.titleLoading}
        commands={commands}
      />
      {loading ? (
        <FormSkeleton fields={3} />
      ) : query.isError ? (
        <ErrorBanner error={query.error} onRetry={() => void query.refetch()} />
      ) : (
        area &&
        (section === 'overview' || section === 'settings') && (
          <FactsPanel
            facts={
              section === 'settings'
                ? settingsFacts(area)
                : overviewFacts(organizationId, area, {
                    projects: children.length,
                    teams: teams.isSuccess ? teamRows.length : undefined,
                  })
            }
          />
        )
      )}
      {area && section === 'settings' && area.type === 'project' && (
        <Box sx={{ mt: 2 }}>
          <InlineNotice severity="info">{areaPage.nameFollowsProject}</InlineNotice>
        </Box>
      )}
      {area && ((section === 'overview' && !isDepartment) || section === 'projects') && (
        <Box sx={{ mt: 4 }}>{childrenSection}</Box>
      )}
      {area && section === 'overview' && area.type === 'project' && (
        <Box sx={{ mt: 2 }}>
          <InlineNotice severity="info">{areaPage.projectTeamsHint}</InlineNotice>
        </Box>
      )}
      {area && section === 'teams' && isDepartment && (
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
      )}
      <AreaNodeDialogs
        organizationId={organizationId}
        dialog={dialog?.kind === 'node' ? dialog.dialog : null}
        canCreate={canCreate}
        canLinkTeams={canLinkTeams}
        onDone={done}
        onClose={closeDialog}
      />
      {area && dialog?.kind === 'place' && (
        <RequiresOrganizationCode code={organizationsPermissions.areaWrite}>
          <PlaceProjectDialog
            organizationId={organizationId}
            department={{ id: area.id, name: area.name }}
            canFromTop={canCreate}
            submit={(nodeId) => moveArea.mutateAsync({ areaId: nodeId, parentId: area.id })}
            onSucceeded={(project) => {
              setDialog(null);
              notify(placeProjectDialog.moved(project.name));
            }}
            onClose={closeDialog}
          />
        </RequiresOrganizationCode>
      )}
      {dialog?.kind === 'lift' && (
        <RequiresOrganizationCode code={organizationsPermissions.areaCreate}>
          <ConfirmAction
            title={liftProjectDialog.title}
            text={liftProjectDialog.text(dialog.project.name)}
            confirmLabel={liftProjectDialog.confirm}
            action={() => moveArea.mutateAsync({ areaId: dialog.project.id, parentId: null })}
            onSucceeded={() => {
              setDialog(null);
              notify(moveAreaDialog.project.moved(dialog.project.name));
            }}
            onClose={closeDialog}
          />
        </RequiresOrganizationCode>
      )}
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
