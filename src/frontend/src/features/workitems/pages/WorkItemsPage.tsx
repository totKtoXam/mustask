import { useState, type MouseEvent, type ReactNode } from 'react';
import { Link as RouterLink, useParams } from 'react-router-dom';
import Box from '@mui/material/Box';
import Link from '@mui/material/Link';
import Paper from '@mui/material/Paper';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableContainer from '@mui/material/TableContainer';
import TableHead from '@mui/material/TableHead';
import TablePagination from '@mui/material/TablePagination';
import TableRow from '@mui/material/TableRow';
import AddIcon from '@mui/icons-material/Add';
import FilterListOffIcon from '@mui/icons-material/FilterListOff';
import InboxOutlinedIcon from '@mui/icons-material/InboxOutlined';
import { useProcessDefinition } from '../../process';
import { useAccessCheck } from '../../../shared/lib/access';
import { formatDateTime } from '../../../shared/lib/format';
import { ErrorBanner } from '../../../shared/ui/ErrorBanner';
import { FilterBar } from '../../../shared/ui/FilterBar';
import { PageHeader, type Command } from '../../../shared/ui/PageHeader';
import { RegistrySkeleton } from '../../../shared/ui/RegistrySkeleton';
import { RequiresOrganizationCode } from '../../../shared/ui/RequiresOrganizationCode';
import { NotFound, StateFrame } from '../../../shared/ui/StateFrame';
import { useNotice } from '../../../shared/ui';
import { WORK_ITEMS_PAGE_SIZE } from '../api/workItemsApi';
import { CreateWorkItemDialog } from '../components/CreateWorkItemDialog';
import { AssigneeName, StateName, TypeName } from '../components/Marks';
import { TeamFilter } from '../components/TeamFilter';
import { NEW_WORK_ITEM_LABEL, TypePickerMenu } from '../components/TypePickerMenu';
import { useCreateWorkItem } from '../hooks/mutations';
import { useWorkItems } from '../hooks/queries';
import { useTeamFilter } from '../hooks/useTeamFilter';
import { notFoundLevel } from '../lib/notFoundLevel';
import { creatableTypes } from '../lib/process';
import { createdNotice, teamTexts } from '../lib/teams';
import { workItemPath } from '../paths';
import { workItemsPermissions } from '../permissions';

/**
 * The table's eight columns (spec § Page «Рабочие элементы»; mod-017 BR-OT-9: «Команда» before
 * «Область»), the skeleton's headers too.
 */
const COLUMNS = [
  'ID',
  'Тип',
  'Заголовок',
  'Исполнитель',
  'Статус',
  'Команда',
  'Область',
  'Изменён',
];

/**
 * «Рабочие элементы» (US-1, US-2; spec § Page «Рабочие элементы»; mod-015
 * § 10.1 without secondary commands and pane): every item of the project, the
 * latest change first, 50 a page, under the band «Фильтры» with the team filter
 * (mod-017 US-5, BR-OT-9; design § 5): the chosen teams live in this page's address
 * (`?teamId=`, mod-020). The list's first read validates the address. Keyed by its project, so another project starts on
 * its own first page.
 */
export function WorkItemsPage() {
  const { organizationId = '', projectId = '' } = useParams<{
    organizationId: string;
    projectId: string;
  }>();
  return (
    <WorkItemsList
      key={`${organizationId}/${projectId}`}
      organizationId={organizationId}
      projectId={projectId}
    />
  );
}

/** A text cell whose long words break instead of widening the table (design § 5.1). */
function WrapCell({ children }: { children: ReactNode }) {
  return <TableCell sx={{ overflowWrap: 'anywhere' }}>{children}</TableCell>;
}

function WorkItemsList({
  organizationId,
  projectId,
}: {
  organizationId: string;
  projectId: string;
}) {
  /**
   * The chosen team ids and `none` (BR-OT-8), kept in the address (mod-020, mod-017 OQ-9 B): a link
   * carries them. A change of them, whoever makes it, shows page 1.
   */
  const [teamIds, setTeamIds] = useTeamFilter();
  /**
   * 0-based, as `TablePagination` counts; the route's `page` is 1-based. It belongs to the filter
   * it was chosen under, compared by content: another filter, set here or by the address, reads
   * from the first page, and the same one, however its array was made, keeps the page.
   */
  const filterKey = JSON.stringify(teamIds);
  const [paging, setPaging] = useState({ filterKey, page: 0 });
  const page = paging.filterKey === filterKey ? paging.page : 0;
  const setPage = (next: number) => setPaging({ filterKey, page: next });
  const query = useWorkItems(organizationId, projectId, page + 1, teamIds);
  const process = useProcessDefinition(organizationId);
  const create = useCreateWorkItem(organizationId, projectId);
  const { show } = useNotice();
  const access = useAccessCheck();
  const canWrite = access(workItemsPermissions.workItemWrite, 'organization');
  const [pickerAnchor, setPickerAnchor] = useState<HTMLElement | null>(null);
  const [creatingTypeId, setCreatingTypeId] = useState<string | null>(null);

  const level = query.isError ? notFoundLevel(query.error) : null;
  if (level) {
    return <NotFound level={level} />;
  }

  const openPicker = (event?: MouseEvent<HTMLElement>) => {
    if (event) {
      setPickerAnchor(event.currentTarget);
    }
  };
  // Rendered once the organization's process is loaded: the picker lists its types.
  const createAction: Command | undefined =
    process.isSuccess && canWrite
      ? {
          label: NEW_WORK_ITEM_LABEL,
          icon: <AddIcon />,
          menuOpen: pickerAnchor !== null,
          onClick: openPicker,
        }
      : undefined;
  const loading = query.isPending || (query.isError && query.isFetching);
  const types = creatableTypes(process.data?.workItemTypes ?? [], process.data?.backlogLevels);
  const filtered = teamIds.length > 0;
  const changeFilter = (next: string[]) => setTeamIds(next);

  return (
    <>
      <PageHeader title="Рабочие элементы" primaryAction={createAction} />
      <FilterBar active={filtered} onClear={() => changeFilter([])}>
        <TeamFilter
          organizationId={organizationId}
          projectId={projectId}
          value={teamIds}
          onChange={changeFilter}
        />
      </FilterBar>
      {process.isError && notFoundLevel(process.error) === null && (
        <ErrorBanner error={process.error} onRetry={() => void process.refetch()} />
      )}
      {loading ? (
        <RegistrySkeleton columns={COLUMNS} />
      ) : query.isError ? (
        <ErrorBanner error={query.error} onRetry={() => void query.refetch()} />
      ) : query.data.count === 0 && filtered ? (
        <StateFrame
          icon={FilterListOffIcon}
          heading="Ничего не найдено"
          hint="Ни один рабочий элемент не подходит под заданные условия фильтра."
          action={{
            label: 'Сбросить фильтр',
            icon: <FilterListOffIcon />,
            onClick: () => changeFilter([]),
          }}
        />
      ) : query.data.count === 0 ? (
        <StateFrame
          icon={InboxOutlinedIcon}
          heading="Рабочих элементов пока нет"
          hint={
            canWrite
              ? 'Создайте первый рабочий элемент кнопкой «Новый рабочий элемент» выше.'
              : 'Рабочие элементы создают участники организации с ролью «Участник» или «Администратор».'
          }
          action={
            createAction && {
              label: NEW_WORK_ITEM_LABEL,
              icon: <AddIcon />,
              onClick: openPicker,
            }
          }
        />
      ) : (
        <TableContainer component={Paper} variant="outlined">
          <Table size="small" aria-label="Рабочие элементы" aria-busy={query.isPlaceholderData}>
            <TableHead>
              <TableRow>
                {COLUMNS.map((column) => (
                  <TableCell key={column}>{column}</TableCell>
                ))}
              </TableRow>
            </TableHead>
            <TableBody>
              {query.data.data.map((item) => (
                <TableRow key={item.id} hover>
                  <TableCell sx={{ whiteSpace: 'nowrap' }}>{item.identifier}</TableCell>
                  <TableCell>
                    <TypeName type={item.type} />
                  </TableCell>
                  <WrapCell>
                    <Link
                      component={RouterLink}
                      to={workItemPath(organizationId, projectId, item.identifier)}
                    >
                      {item.title}
                    </Link>
                  </WrapCell>
                  <TableCell>
                    <AssigneeName assignee={item.assignee} />
                  </TableCell>
                  <TableCell>
                    <StateName state={item.state} />
                  </TableCell>
                  <WrapCell>
                    {item.team ? (
                      item.team.name
                    ) : (
                      <Box component="span" sx={{ color: 'text.secondary' }}>
                        {teamTexts.noTeam}
                      </Box>
                    )}
                  </WrapCell>
                  <WrapCell>{item.area.path}</WrapCell>
                  <TableCell sx={{ whiteSpace: 'nowrap' }}>
                    {formatDateTime(item.changedAt)}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
          <TablePagination
            component="div"
            count={query.data.count}
            page={page}
            rowsPerPage={WORK_ITEMS_PAGE_SIZE}
            rowsPerPageOptions={[]}
            onPageChange={(_event, next) => setPage(next)}
          />
        </TableContainer>
      )}
      <TypePickerMenu
        anchor={pickerAnchor}
        allowed={canWrite}
        types={types}
        onChoose={(type) => {
          setPickerAnchor(null);
          setCreatingTypeId(type.id);
        }}
        onClose={() => setPickerAnchor(null)}
      />
      {creatingTypeId !== null && (
        <RequiresOrganizationCode code={workItemsPermissions.workItemWrite}>
          <CreateWorkItemDialog
            organizationId={organizationId}
            projectId={projectId}
            types={types}
            initialTypeId={creatingTypeId}
            submit={(body) => create.mutateAsync(body)}
            onCreated={(item) => {
              setCreatingTypeId(null);
              // OQ-4: the user stays on the list, on its first page, the new row on top.
              setPage(0);
              show(createdNotice(item.identifier, item.team, teamIds));
            }}
            onClose={(contextGone) => {
              setCreatingTypeId(null);
              if (contextGone) {
                void query.refetch();
              }
            }}
          />
        </RequiresOrganizationCode>
      )}
    </>
  );
}
