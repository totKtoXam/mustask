import { useRef, useState, type MouseEvent, type ReactNode } from 'react';
import { Link as RouterLink, useLocation, useParams } from 'react-router-dom';
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
import { LiveRegion, useNotice } from '../../../shared/ui';
import { WORK_ITEMS_PAGE_SIZE } from '../api/workItemsApi';
import { AssigneeFilter } from '../components/AssigneeFilter';
import { CreateWorkItemDialog } from '../components/CreateWorkItemDialog';
import { KeywordFilter } from '../components/KeywordFilter';
import { MarkedAssigneeName, StateName, TypeName } from '../components/Marks';
import { StateFilter } from '../components/StateFilter';
import { TeamFilter } from '../components/TeamFilter';
import { NEW_WORK_ITEM_LABEL, TypePickerMenu } from '../components/TypePickerMenu';
import { useCreateWorkItem } from '../hooks/mutations';
import { useWorkItems } from '../hooks/queries';
import { useWorkItemFilter } from '../hooks/useWorkItemFilter';
import { listOriginState } from '../lib/listOrigin';
import { notFoundLevel } from '../lib/notFoundLevel';
import { creatableTypes } from '../lib/process';
import { teamTexts } from '../lib/teams';
import { createdInListNotice, filterContent, filterTexts, isFiltered } from '../lib/workItemFilter';
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
 * § 10.1 without secondary commands and pane): every live item of the project, the
 * latest change first, 50 a page, under the band «Фильтры» with «Поиск», «Статус»,
 * «Исполнитель» (feat-003 BR-WS-1; «Удалена» is the way into the recycle bin) and the team filter
 * (mod-017 US-5, BR-OT-9; design § 5). All four live in this page's address (`?search=`, `?state=`,
 * `?assigneeId=`, `?teamId=`; BR-WS-11, mod-020). The list's first read validates the address.
 * Keyed by its project, so another project starts on its own first page.
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

/**
 * One field of the band (design § 2): it stands at the top of the band, so a taller «Исполнитель»
 * box (tags in rows) does not move its neighbours, and is never wider than the band's row.
 */
function BandField({ children }: { children: ReactNode }) {
  return <Box sx={{ alignSelf: 'flex-start', maxWidth: '100%' }}>{children}</Box>;
}

function WorkItemsList({
  organizationId,
  projectId,
}: {
  organizationId: string;
  projectId: string;
}) {
  /**
   * The four filters — keyword, states, assignees, teams — kept in the address (feat-003 BR-WS-11,
   * mod-020, mod-017 OQ-9 B): a link carries them, one writer sets them in one replace. A change of
   * their content, whoever makes it, shows page 1.
   */
  const [filter, setFilter] = useWorkItemFilter();
  /**
   * 0-based, as `TablePagination` counts; the route's `page` is 1-based. It belongs to the filter
   * it was chosen under, compared by content: another filter, set here or by the address, reads
   * from the first page, and the same one, however its arrays were made, keeps the page. A change
   * of content replaces the pair at once (page 0), so coming back to an earlier content — the
   * filter just set is cleared — does not bring that content's old page back (BR-WS-11, qa D-F1).
   */
  const content = filterContent(filter);
  const [paging, setPaging] = useState({ content, page: 0 });
  if (paging.content !== content) {
    setPaging({ content, page: 0 });
  }
  const page = paging.content === content ? paging.page : 0;
  const setPage = (next: number) => setPaging({ content, page: next });
  const query = useWorkItems(organizationId, projectId, page + 1, filter);
  const process = useProcessDefinition(organizationId);
  const create = useCreateWorkItem(organizationId, projectId);
  const { show } = useNotice();
  const access = useAccessCheck();
  const canWrite = access(workItemsPermissions.workItemWrite, 'organization');
  const [pickerAnchor, setPickerAnchor] = useState<HTMLElement | null>(null);
  const [creatingTypeId, setCreatingTypeId] = useState<string | null>(null);
  const keywordRef = useRef<HTMLInputElement>(null);
  const { pathname, search } = useLocation();
  // The title links carry the list's address, so a purge on the card returns here (BR-WS-5).
  const origin = listOriginState(pathname, search);

  // The result of a change of a filter is announced once it has settled (design § 9), not on the
  // first read: the content a result was last announced for starts as the page's own.
  const [announcedFor, setAnnouncedFor] = useState(content);
  const [announcement, setAnnouncement] = useState({ text: '', serial: 0 });
  if (
    query.isSuccess &&
    !query.isFetching &&
    !query.isPlaceholderData &&
    announcedFor !== content
  ) {
    setAnnouncedFor(content);
    setAnnouncement(({ serial }) => ({
      text: query.data.count === 0 ? filterTexts.noMatch : filterTexts.found(query.data.count),
      serial: serial + 1,
    }));
  }

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
  // A value no field can name is a filter that is on (design § 5): it empties the list and
  // «Очистить фильтры» removes it.
  const filtered = isFiltered(filter);
  // Both clears remove all four filters, the team's included (BR-WS-13), and send focus to «Поиск»:
  // the button that was pressed is gone once the filters are.
  const clearFilters = () => {
    setFilter({ state: [], assigneeId: [], search: '', teamId: [] });
    keywordRef.current?.focus();
  };

  return (
    <>
      <PageHeader title="Рабочие элементы" primaryAction={createAction} />
      <LiveRegion text={announcement.text} serial={announcement.serial} />
      <FilterBar active={filtered} onClear={clearFilters}>
        <BandField>
          <KeywordFilter
            ref={keywordRef}
            value={filter.search}
            onChange={(keyword) => setFilter({ search: keyword })}
          />
        </BandField>
        <BandField>
          <StateFilter
            organizationId={organizationId}
            value={filter.state}
            onChange={(state) => setFilter({ state })}
          />
        </BandField>
        <BandField>
          <AssigneeFilter
            organizationId={organizationId}
            projectId={projectId}
            value={filter.assigneeId}
            states={filter.state}
            onChange={(assigneeId) => setFilter({ assigneeId })}
          />
        </BandField>
        <BandField>
          <TeamFilter
            organizationId={organizationId}
            projectId={projectId}
            value={filter.teamId}
            onChange={(teamId) => setFilter({ teamId })}
          />
        </BandField>
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
            onClick: clearFilters,
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
                      state={origin}
                    >
                      {item.title}
                    </Link>
                  </WrapCell>
                  <TableCell>
                    <MarkedAssigneeName assignee={item.assignee} />
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
            place={{ kind: 'project', projectId }}
            types={types}
            initialTypeId={creatingTypeId}
            submit={(_projectId, body) => create.mutateAsync(body)}
            onCreated={(item) => {
              setCreatingTypeId(null);
              // OQ-4: the user stays on the list, on its first page, the new row on top.
              setPage(0);
              show(createdInListNotice(item, filter));
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
