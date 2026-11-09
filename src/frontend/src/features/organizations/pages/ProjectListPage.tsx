import { useRef, useState } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';
import Box from '@mui/material/Box';
import AddIcon from '@mui/icons-material/Add';
import ArchiveOutlinedIcon from '@mui/icons-material/ArchiveOutlined';
import FilterListOffIcon from '@mui/icons-material/FilterListOff';
import InboxOutlinedIcon from '@mui/icons-material/InboxOutlined';
import { useAccessCheck } from '../../../shared/lib/access';
import { apiErrorText } from '../../../shared/lib/apiErrorText';
import {
  DataTable,
  InlineSelectField,
  MoreActionsMenu,
  TruncatedText,
  type DataColumn,
} from '../../../shared/ui';
import { PageHeader } from '../../../shared/ui/PageHeader';
import {
  RequiresCode,
  RequiresOrganizationCode,
} from '../../../shared/ui/RequiresOrganizationCode';
import { NotFound } from '../../../shared/ui/StateFrame';
import type { ListView } from '../api/organizationsApi';
import { ConfirmAction } from '../components/ConfirmAction';
import { CreateProjectDialog } from '../components/CreateProjectDialog';
import { ListFilter } from '../components/ListFilter';
import { useNotify } from '../components/notice';
import { RestoreButton } from '../components/RestoreButton';
import { useCreateProject, useDeactivateProject, useRestoreProject } from '../hooks/mutations';
import { useSelectorOffers } from '../hooks/navigationQueries';
import { RoleFloorAccess } from '../access/RoleFloorAccess';
import { useProjects, useWritableAreaIds } from '../hooks/queries';
import { useObjectLink } from '../hooks/useObjectLink';
import { projectsList } from '../lib/navigationTexts';
import { notFoundLevel } from '../lib/notFoundLevel';
import { useRestore } from '../hooks/useRestore';
import { projectTexts } from '../lib/teamTexts';
import { projectGeneralPath } from '../paths';
import { organizationsPermissions } from '../permissions';

/** One row of the list, whichever read it came from. */
interface ProjectRow {
  id: string;
  name: string;
  key: string;
  /** The department holding the project's node; `null` at the top level. */
  place: string | null;
}

/** «Деактивировать» of one row: the confirmation of mod-002, mounted per opening. */
function DeactivateProject({
  organizationId,
  project,
  onClose,
}: {
  organizationId: string;
  project: ProjectRow;
  onClose: (contextGone: boolean) => void;
}) {
  const deactivate = useDeactivateProject(organizationId, project.id);
  const notify = useNotify();
  return (
    <RequiresOrganizationCode code={organizationsPermissions.projectDeactivate}>
      <ConfirmAction
        title="Деактивировать проект?"
        text={projectTexts.deactivate.text(project.name, project.key)}
        confirmLabel={projectsList.deactivate}
        action={() => deactivate.mutateAsync()}
        onSucceeded={() => {
          onClose(false);
          notify(`Проект «${project.name}» деактивирован.`);
        }}
        onClose={onClose}
      />
    </RequiresOrganizationCode>
  );
}

/**
 * «Проекты» of the organization (feat-043 design § 5.3; US-2, US-4): every active project from
 * the one read the selectors share (`selector-offers`: name, key, place), narrowed by «Найти
 * проект»; a name opens the project (D-3). The administrator holds «Показывать» (`?status=`, D-5):
 * the deactivated view, from its own read, whose names are no links, and «Создать проект»
 * (mod-018 § 7's dialog) which opens the new project's «Общие». «Деактивировать» is a row menu
 * with the confirmation of mod-002; «Восстановить» a button on the deactivated row.
 */
export function ProjectListPage({ view }: { view: ListView }) {
  const { organizationId = '' } = useParams<{ organizationId: string }>();
  // The list's rows carry each project's codes; the link dialog's two ends ask the role's floor (BR-SA-18).
  return (
    <RoleFloorAccess organizationId={organizationId}>
      <ProjectList view={view} organizationId={organizationId} />
    </RoleFloorAccess>
  );
}

function ProjectList({ view, organizationId }: { view: ListView; organizationId: string }) {
  const offers = useSelectorOffers(organizationId, { enabled: view === 'active' });
  const deactivated = useProjects(organizationId, 'deactivated', {
    enabled: view === 'deactivated',
  });
  const query = view === 'active' ? offers : deactivated;
  const createProject = useCreateProject(organizationId);
  const restoreProject = useRestoreProject(organizationId);
  const objectLink = useObjectLink(organizationId);
  const notify = useNotify();
  const navigate = useNavigate();
  const [, setParams] = useSearchParams();
  const access = useAccessCheck();
  const [creating, setCreating] = useState(false);
  // mod-019 BR-SA-12, BR-SA-17: a project is created where the caller may write — the top level with
  // `area.create`, a department with `area.write` on it; with no place the command is absent.
  const canTop = access(organizationsPermissions.areaCreate, 'organization');
  const writable = useWritableAreaIds(organizationId);
  const writableIds = new Set(writable.data ?? []);
  const hasPlace = canTop || writableIds.size > 0;
  const [deactivating, setDeactivating] = useState<ProjectRow | null>(null);
  const [text, setText] = useState('');
  const pageRef = useRef<HTMLDivElement>(null);

  const rows: ProjectRow[] | undefined =
    view === 'active'
      ? offers.data?.projects.map(({ id, name, key, departmentName }) => ({
          id,
          name,
          key,
          place: departmentName,
        }))
      : deactivated.data?.map(({ id, name, key }) => ({ id, name, key, place: null }));

  const restore = useRestore({
    rows: view === 'deactivated' ? rows : undefined,
    restore: (id) => restoreProject.mutateAsync(id),
    notice: (name) => `Проект «${name}» восстановлен.`,
    container: pageRef,
    // The gate turns the page into «Организация не найдена»; no notice over it (design § 9).
    answeredByPage: (error) => notFoundLevel(error) === 'organization',
  });

  const level = query.isError ? notFoundLevel(query.error) : null;
  if (level) {
    return <NotFound level={level} />;
  }

  // mod-016 design § 6.2: «Создать проект» needs `organizations.project.write`, the other view
  // and «Деактивировать» `organizations.project.deactivate`.
  const canCreate = hasPlace;
  const canDeactivate = access(organizationsPermissions.projectDeactivate, 'organization');
  const needle = text.trim().toLowerCase();
  const shown = (rows ?? []).filter(
    (row) => needle === '' || `${row.name} ${row.key}`.toLowerCase().includes(needle),
  );
  const createAction = {
    label: projectsList.create,
    icon: <AddIcon />,
    onClick: () => setCreating(true),
  };

  const columns: DataColumn<ProjectRow>[] = [
    {
      key: 'name',
      header: projectsList.columns.name,
      render: (project) => {
        if (view === 'deactivated') {
          return <TruncatedText>{project.name}</TruncatedText>;
        }
        const link = objectLink('project', project.id);
        return (
          <TruncatedText to={link.to} linkState={link.state} look="link">
            {project.name}
          </TruncatedText>
        );
      },
    },
    { key: 'key', header: projectsList.columns.key, render: (project) => project.key },
  ];
  if (view === 'active') {
    columns.push({
      key: 'place',
      header: projectsList.columns.place,
      render: (project) => <TruncatedText>{project.place ?? projectsList.topLevel}</TruncatedText>,
    });
  }
  if (canDeactivate) {
    columns.push({
      key: 'actions',
      header: '',
      align: 'right',
      width: 48,
      render: (project) =>
        view === 'deactivated' ? (
          <RestoreButton
            name={project.name}
            busy={restore.isBusy(project.id)}
            buttonRef={restore.buttonRef(project.id)}
            onClick={() => restore.restore(project.id, project.name)}
          />
        ) : (
          <MoreActionsMenu
            label={projectsList.rowMenu(project.name)}
            size="row"
            items={[
              {
                label: projectsList.deactivate,
                icon: <ArchiveOutlinedIcon />,
                onSelect: () => setDeactivating(project),
                destructive: true,
              },
            ]}
          />
        ),
    });
  }

  const filtered = needle !== '' && (rows?.length ?? 0) > 0 && shown.length === 0;
  const empty =
    view === 'deactivated'
      ? { icon: InboxOutlinedIcon, heading: projectsList.emptyDeactivated }
      : filtered
        ? {
            icon: FilterListOffIcon,
            heading: projectsList.nothingFound,
            action: { label: projectsList.resetFilter, onClick: () => setText('') },
          }
        : {
            icon: InboxOutlinedIcon,
            heading: projectsList.empty.heading,
            hint: canCreate ? projectsList.empty.adminHint : projectsList.empty.otherHint,
            action: canCreate ? createAction : undefined,
          };

  return (
    <Box ref={pageRef}>
      <PageHeader
        title={projectsList.title}
        primaryAction={query.isSuccess && canCreate ? createAction : undefined}
      />
      <ListFilter label={projectsList.search} text={text} onText={setText}>
        {canDeactivate && (
          <InlineSelectField
            label={projectsList.show}
            frame="always"
            value={view}
            options={[
              { value: 'active', label: projectsList.active },
              { value: 'deactivated', label: projectsList.deactivated },
            ]}
            onChange={(next) => {
              setText('');
              setParams(next === 'deactivated' ? { status: 'deactivated' } : {});
            }}
          />
        )}
      </ListFilter>
      <DataTable
        aria-label={projectsList.tableLabel}
        columns={columns}
        rows={shown}
        rowKey={(project) => project.id}
        loading={query.isPending || (query.isError && query.isFetching)}
        error={query.isError && !query.isFetching ? apiErrorText(query.error) : undefined}
        onRetry={() => void query.refetch()}
        empty={empty}
      />
      {deactivating && (
        <DeactivateProject
          organizationId={organizationId}
          project={deactivating}
          onClose={(contextGone) => {
            setDeactivating(null);
            if (contextGone) {
              void query.refetch();
            }
          }}
        />
      )}
      {creating && (
        <RequiresCode holds={hasPlace}>
          <CreateProjectDialog
            organizationId={organizationId}
            canLink={
              access(organizationsPermissions.teamLink, 'organization') &&
              access(organizationsPermissions.projectLink, 'organization')
            }
            canPlaceInDepartment={writableIds.size > 0}
            canPlaceAtTop={canTop}
            writableIds={writableIds}
            onForbidden={() => void writable.refetch()}
            submit={(body) => createProject.mutateAsync(body)}
            onSucceeded={(project) => {
              setCreating(false);
              notify(`Проект «${project.name}» создан.`);
              navigate(projectGeneralPath(organizationId, project.id));
            }}
            onClose={(contextGone) => {
              setCreating(false);
              if (contextGone) {
                void query.refetch();
              }
            }}
          />
        </RequiresCode>
      )}
    </Box>
  );
}
