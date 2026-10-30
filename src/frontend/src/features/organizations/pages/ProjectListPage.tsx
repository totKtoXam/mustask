import { useRef, useState } from 'react';
import { useParams } from 'react-router-dom';
import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableContainer from '@mui/material/TableContainer';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';
import AddIcon from '@mui/icons-material/Add';
import InboxOutlinedIcon from '@mui/icons-material/InboxOutlined';
import { useAccessCheck } from '../../../shared/lib/access';
import { ErrorBanner } from '../../../shared/ui/ErrorBanner';
import { PageHeader } from '../../../shared/ui/PageHeader';
import { RegistrySkeleton } from '../../../shared/ui/RegistrySkeleton';
import { RequiresOrganizationCode } from '../../../shared/ui/RequiresOrganizationCode';
import { NotFound, StateFrame } from '../../../shared/ui/StateFrame';
import type { ListView } from '../api/organizationsApi';
import { CreateProjectDialog } from '../components/CreateProjectDialog';
import { useNotify } from '../components/notice';
import { RestoreButton } from '../components/RestoreButton';
import { TruncatedName } from '../components/TruncatedName';
import { useCreateProject, useRestoreProject } from '../hooks/mutations';
import { useProjects } from '../hooks/queries';
import { useRestore } from '../hooks/useRestore';
import { notFoundLevel } from '../lib/notFoundLevel';
import { deactivatedProjectsPath, projectGeneralPath, projectsPath } from '../paths';
import { organizationsPermissions } from '../permissions';

/**
 * «Проекты» (US-1, US-3; design § 7): an organization's projects in two tabs
 * that are addresses, created from here (the page stays, DC-6) and restored
 * from the deactivated tab. The tab's first read validates the address.
 */
export function ProjectListPage({ view }: { view: ListView }) {
  const { organizationId = '' } = useParams<{ organizationId: string }>();
  const query = useProjects(organizationId, view);
  const createProject = useCreateProject(organizationId);
  const restoreProject = useRestoreProject(organizationId);
  const notify = useNotify();
  const access = useAccessCheck();
  const [creating, setCreating] = useState(false);
  const pageRef = useRef<HTMLDivElement>(null);
  const restore = useRestore({
    rows: view === 'deactivated' ? query.data : undefined,
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

  // mod-016 design § 6.2: «Создать проект» needs `organizations.project.write`
  // and the tab «Деактивированные» `organizations.project.deactivate`; without
  // the second the strip has one view and is not drawn.
  const canCreate = access(organizationsPermissions.projectWrite, 'organization');
  const canSeeDeactivated = access(organizationsPermissions.projectDeactivate, 'organization');
  const views = [
    { label: 'Активные', to: projectsPath(organizationId) },
    ...(canSeeDeactivated
      ? [{ label: 'Деактивированные', to: deactivatedProjectsPath(organizationId) }]
      : []),
  ];
  const createAction = {
    label: 'Создать проект',
    icon: <AddIcon />,
    onClick: () => setCreating(true),
  };
  const loading = query.isPending || (query.isError && query.isFetching);
  const columns = view === 'active' ? ['Название', 'Ключ'] : ['Название', 'Ключ', ''];

  return (
    <Box ref={pageRef}>
      <PageHeader
        title="Проекты"
        primaryAction={query.isSuccess && canCreate ? createAction : undefined}
        views={views}
        viewsLabel="Проекты по статусу"
      />
      {loading ? (
        <RegistrySkeleton columns={columns} />
      ) : query.isError ? (
        <ErrorBanner error={query.error} onRetry={() => void query.refetch()} />
      ) : query.data.length === 0 ? (
        view === 'active' ? (
          <StateFrame
            icon={InboxOutlinedIcon}
            heading="Пока нет проектов"
            hint={
              canCreate
                ? 'Создайте первый проект кнопкой «Создать проект» выше.'
                : 'Проекты создаёт администратор организации.'
            }
            action={canCreate ? createAction : undefined}
          />
        ) : (
          <StateFrame
            icon={InboxOutlinedIcon}
            heading="Деактивированных проектов нет"
            hint="Сюда попадают проекты, деактивированные в их настройках. Их можно восстановить."
          />
        )
      ) : (
        <TableContainer component={Paper} variant="outlined">
          <Table
            size="small"
            aria-label={view === 'active' ? 'Активные проекты' : 'Деактивированные проекты'}
          >
            <TableHead>
              <TableRow>
                {columns.map((column, index) => (
                  <TableCell key={index}>{column}</TableCell>
                ))}
              </TableRow>
            </TableHead>
            <TableBody>
              {query.data.map((project) => (
                <TableRow key={project.id} hover>
                  <TableCell sx={{ maxWidth: 0, width: '70%' }}>
                    <TruncatedName
                      name={project.name}
                      to={
                        view === 'active'
                          ? projectGeneralPath(organizationId, project.id)
                          : undefined
                      }
                    />
                  </TableCell>
                  <TableCell>{project.key}</TableCell>
                  {view === 'deactivated' && (
                    <TableCell align="right" sx={{ whiteSpace: 'nowrap' }}>
                      <RestoreButton
                        name={project.name}
                        busy={restore.isBusy(project.id)}
                        buttonRef={restore.buttonRef(project.id)}
                        onClick={() => restore.restore(project.id, project.name)}
                      />
                    </TableCell>
                  )}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}
      {creating && (
        <RequiresOrganizationCode code={organizationsPermissions.projectWrite}>
          <CreateProjectDialog
            organizationId={organizationId}
            canLink={
              access(organizationsPermissions.teamLink, 'organization') &&
              access(organizationsPermissions.projectLink, 'organization')
            }
            canPlaceInDepartment={access(organizationsPermissions.areaWrite, 'organization')}
            canPlaceAtTop={access(organizationsPermissions.areaCreate, 'organization')}
            submit={(body) => createProject.mutateAsync(body)}
            onSucceeded={(project) => {
              setCreating(false);
              notify(`Проект «${project.name}» создан.`);
            }}
            onClose={(contextGone) => {
              setCreating(false);
              if (contextGone) {
                void query.refetch();
              }
            }}
          />
        </RequiresOrganizationCode>
      )}
    </Box>
  );
}
