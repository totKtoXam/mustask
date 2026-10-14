import { useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
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
import { StateFrame } from '../../../shared/ui/StateFrame';
import type { ListView } from '../api/organizationsApi';
import { NameDialog } from '../components/NameDialog';
import { useNotify } from '../components/notice';
import { RestoreButton } from '../components/RestoreButton';
import { TruncatedName } from '../components/TruncatedName';
import { useCreateOrganization, useRestoreOrganization } from '../hooks/mutations';
import { useOrganizations } from '../hooks/queries';
import { useRestore } from '../hooks/useRestore';
import { DEACTIVATED_ORGANIZATIONS_ROUTE, ORGANIZATIONS_PATH, projectsPath } from '../paths';
import { organizationsPermissions } from '../permissions';

const VIEWS = [
  { label: 'Активные', to: ORGANIZATIONS_PATH },
  { label: 'Деактивированные', to: DEACTIVATED_ORGANIZATIONS_ROUTE },
];

/**
 * «Организации» (US-1, US-2, US-8; design § 5): the user's own organizations
 * in two tabs that are addresses — the active ones the user belongs to, and in
 * the deactivated tab those the user administers (mod-016 BR-M1-35; the server
 * narrows both). The switcher's «Все организации» and «Нет организаций» are
 * its only ways in (D-d). The page needs no organization role and stands
 * outside the gate: «Создать организацию» needs the create right in the token,
 * the one check it makes. No filter and no paging: tens of organizations (NFR
 * Volumes).
 */
export function OrganizationListPage({ view }: { view: ListView }) {
  const access = useAccessCheck();
  const canCreate = access(organizationsPermissions.organizationCreate, 'instance');
  const query = useOrganizations(view);
  const navigate = useNavigate();
  const notify = useNotify();
  const createOrganization = useCreateOrganization();
  const restoreOrganization = useRestoreOrganization();
  const [creating, setCreating] = useState(false);
  const pageRef = useRef<HTMLDivElement>(null);
  const restore = useRestore({
    rows: view === 'deactivated' ? query.data : undefined,
    restore: (id) => restoreOrganization.mutateAsync(id),
    notice: (name) => `Организация «${name}» восстановлена.`,
    container: pageRef,
  });

  const openCreate = () => setCreating(true);
  const createAction = { label: 'Создать организацию', icon: <AddIcon />, onClick: openCreate };
  const loading = query.isPending || (query.isError && query.isFetching);

  return (
    <Box ref={pageRef}>
      <PageHeader
        title="Организации"
        primaryAction={canCreate ? createAction : undefined}
        views={VIEWS}
        viewsLabel="Организации по статусу"
      />
      {loading ? (
        <RegistrySkeleton columns={view === 'active' ? ['Название'] : ['Название', '']} />
      ) : query.isError ? (
        <ErrorBanner error={query.error} onRetry={() => void query.refetch()} />
      ) : query.data.length === 0 ? (
        view === 'active' ? (
          <StateFrame
            icon={InboxOutlinedIcon}
            heading="Вы пока не состоите ни в одной организации"
            hint={
              canCreate
                ? 'Создайте организацию кнопкой «Создать организацию» выше или попросите администратора организации добавить вас.'
                : 'Попросите администратора организации добавить вас.'
            }
            action={canCreate ? createAction : undefined}
          />
        ) : (
          <StateFrame
            icon={InboxOutlinedIcon}
            heading="Деактивированных организаций нет"
            hint="Здесь показываются деактивированные организации, в которых вы администратор. Их можно восстановить."
          />
        )
      ) : (
        <TableContainer component={Paper} variant="outlined">
          <Table
            size="small"
            aria-label={view === 'active' ? 'Активные организации' : 'Деактивированные организации'}
          >
            <TableHead>
              <TableRow>
                <TableCell>Название</TableCell>
                {view === 'deactivated' && <TableCell />}
              </TableRow>
            </TableHead>
            <TableBody>
              {query.data.map((organization) => (
                <TableRow key={organization.id} hover>
                  <TableCell sx={{ maxWidth: 0, width: '100%' }}>
                    <TruncatedName
                      name={organization.name}
                      to={view === 'active' ? projectsPath(organization.id) : undefined}
                    />
                  </TableCell>
                  {view === 'deactivated' && (
                    <TableCell align="right" sx={{ whiteSpace: 'nowrap' }}>
                      <RestoreButton
                        name={organization.name}
                        busy={restore.isBusy(organization.id)}
                        buttonRef={restore.buttonRef(organization.id)}
                        onClick={() => restore.restore(organization.id, organization.name)}
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
        <NameDialog
          title="Новая организация"
          helper="От 1 до 100 символов"
          submitLabel="Создать"
          submit={(name) => createOrganization.mutateAsync(name)}
          onSucceeded={(organization) => {
            setCreating(false);
            // DC-6: a new organization's «Проекты» opens, and its address
            // puts it in the switcher.
            navigate(projectsPath(organization.id));
            notify(`Организация «${organization.name}» создана.`);
          }}
          onClose={() => setCreating(false)}
        />
      )}
    </Box>
  );
}
