import { useState, type MouseEvent } from 'react';
import { Link as RouterLink, useParams } from 'react-router-dom';
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
import InboxOutlinedIcon from '@mui/icons-material/InboxOutlined';
import { useProcessDefinition } from '../../process';
import { formatDateTime } from '../../../shared/lib/format';
import { ErrorBanner } from '../../../shared/ui/ErrorBanner';
import { PageHeader, type Command } from '../../../shared/ui/PageHeader';
import { RegistrySkeleton } from '../../../shared/ui/RegistrySkeleton';
import { NotFound, StateFrame } from '../../../shared/ui/StateFrame';
import { useNotice } from '../../../shared/ui';
import { WORK_ITEMS_PAGE_SIZE } from '../api/workItemsApi';
import { CreateWorkItemDialog } from '../components/CreateWorkItemDialog';
import { AssigneeName, StateName, TypeName } from '../components/Marks';
import { NEW_WORK_ITEM_LABEL, TypePickerMenu } from '../components/TypePickerMenu';
import { useCreateWorkItem } from '../hooks/mutations';
import { useWorkItems } from '../hooks/queries';
import { notFoundLevel } from '../lib/notFoundLevel';
import { creatableTypes } from '../lib/process';
import { workItemPath } from '../paths';

/** The table's seven columns (spec § Page «Рабочие элементы»), the skeleton's headers too. */
const COLUMNS = ['ID', 'Тип', 'Заголовок', 'Исполнитель', 'Статус', 'Область', 'Изменён'];

/**
 * «Рабочие элементы» (US-1, US-2; spec § Page «Рабочие элементы»; mod-015
 * § 10.1 without its filter bar, secondary commands and pane): every item of
 * the project, the latest change first, 50 a page. The list's first read
 * validates the address. Keyed by its project, so another project starts on
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

function WorkItemsList({
  organizationId,
  projectId,
}: {
  organizationId: string;
  projectId: string;
}) {
  /** 0-based, as `TablePagination` counts; the route's `page` is 1-based. */
  const [page, setPage] = useState(0);
  const query = useWorkItems(organizationId, projectId, page + 1);
  const process = useProcessDefinition(organizationId);
  const create = useCreateWorkItem(organizationId, projectId);
  const { show } = useNotice();
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
  const createAction: Command | undefined = process.isSuccess
    ? {
        label: NEW_WORK_ITEM_LABEL,
        icon: <AddIcon />,
        menuOpen: pickerAnchor !== null,
        onClick: openPicker,
      }
    : undefined;
  const loading = query.isPending || (query.isError && query.isFetching);
  const types = creatableTypes(process.data?.workItemTypes ?? []);

  return (
    <>
      <PageHeader title="Рабочие элементы" primaryAction={createAction} />
      {process.isError && notFoundLevel(process.error) === null && (
        <ErrorBanner error={process.error} onRetry={() => void process.refetch()} />
      )}
      {loading ? (
        <RegistrySkeleton columns={COLUMNS} />
      ) : query.isError ? (
        <ErrorBanner error={query.error} onRetry={() => void query.refetch()} />
      ) : query.data.count === 0 ? (
        <StateFrame
          icon={InboxOutlinedIcon}
          heading="Рабочих элементов пока нет"
          hint="Создайте первый рабочий элемент кнопкой «Новый рабочий элемент» выше."
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
          <Table size="small" aria-label="Рабочие элементы">
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
                  <TableCell sx={{ overflowWrap: 'anywhere' }}>
                    <Link
                      component={RouterLink}
                      to={workItemPath(organizationId, projectId, item.identifier)}
                    >
                      {item.title}
                    </Link>
                  </TableCell>
                  <TableCell>
                    <AssigneeName assignee={item.assignee} />
                  </TableCell>
                  <TableCell>
                    <StateName state={item.state} />
                  </TableCell>
                  <TableCell>{item.area.path}</TableCell>
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
        types={types}
        onChoose={(type) => {
          setPickerAnchor(null);
          setCreatingTypeId(type.id);
        }}
        onClose={() => setPickerAnchor(null)}
      />
      {creatingTypeId !== null && (
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
            show(`Рабочий элемент ${item.identifier} создан.`);
          }}
          onClose={(contextGone) => {
            setCreatingTypeId(null);
            if (contextGone) {
              void query.refetch();
            }
          }}
        />
      )}
    </>
  );
}
