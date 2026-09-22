import { useState } from 'react';
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import IconButton from '@mui/material/IconButton';
import MenuItem from '@mui/material/MenuItem';
import Paper from '@mui/material/Paper';
import Snackbar from '@mui/material/Snackbar';
import Stack from '@mui/material/Stack';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableContainer from '@mui/material/TableContainer';
import TableHead from '@mui/material/TableHead';
import TablePagination from '@mui/material/TablePagination';
import TableRow from '@mui/material/TableRow';
import TableSortLabel from '@mui/material/TableSortLabel';
import TextField from '@mui/material/TextField';
import Tooltip from '@mui/material/Tooltip';
import Typography from '@mui/material/Typography';
import AddIcon from '@mui/icons-material/Add';
import EditIcon from '@mui/icons-material/Edit';
import StarIcon from '@mui/icons-material/Star';
import StarBorderIcon from '@mui/icons-material/StarBorder';
import { useAuth } from '../../auth/AuthContext';
import { formatDateTime } from '../../../shared/lib/format';
import { ErrorBanner } from '../../../shared/ui/ErrorBanner';
import { AccessDenied } from '../components/AccessDenied';
import { RegistrySkeleton } from '../components/RegistrySkeleton';
import { RequirePermission } from '../components/RequirePermission';
import { DefaultLanguageDialog, type DefaultLanguageDialogTarget } from '../components/DefaultLanguageDialog';
import { describeDefaultAssignment } from '../lib/describeDefaultAssignment';
import { dictionariesPermissions } from '../permissions';
import { getLanguages, type LanguageResponse, type LanguagesSortField, type OrderBy } from '../api/dictionariesApi';

const PAGE_SIZE = 10;

type TriState = '' | 'true' | 'false';

interface FilterState {
  code: string;
  endonym: string;
  isDefault: TriState;
  isActive: TriState;
  sortOrder: string;
}

const emptyFilters: FilterState = { code: '', endonym: '', isDefault: '', isActive: '', sortOrder: '' };

const COLUMNS: { field: LanguagesSortField | null; label: string }[] = [
  { field: 'code', label: 'Код' },
  { field: 'endonym', label: 'Эндоним' },
  { field: 'isDefault', label: 'По умолчанию' },
  { field: 'isActive', label: 'Активность' },
  { field: 'sortOrder', label: 'Порядок' },
  { field: null, label: 'Изменено' },
];

function formatAuditStamp(entry: { updatedAt: string; updatedBy: string }): string {
  return `${formatDateTime(entry.updatedAt)} — ${entry.updatedBy}`;
}

/**
 * Реестр языков. Реестровый паттерн (белый список сортировки/фильтрации,
 * пять состояний ADR-0007), плюс write-гейтированные действия строки
 * (редактировать, назначить языком по умолчанию через `DefaultLanguageDialog`)
 * и своя пара пустых текстов — "языки можно создавать".
 *
 * Право на запись гейтируется здесь же, через `useAuth().can`.
 *
 * Фильтры не debounce'ятся — при NFR-потолке ≤ 10 языков лишний `GET`
 * на клавишу ничего не стоит; пересмотреть, если этот паттерн скопируют в
 * реестр другого масштаба.
 */
function LanguagesRegistryContent() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { can } = useAuth();
  const canWrite = can(dictionariesPermissions.languagesWrite);

  const [filters, setFilters] = useState<FilterState>(emptyFilters);
  const [uiPage, setUiPage] = useState(0);
  const [orderBy, setOrderBy] = useState<OrderBy<LanguagesSortField> | undefined>(undefined);
  const [defaultTarget, setDefaultTarget] = useState<DefaultLanguageDialogTarget | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const isFilterActive = Object.values(filters).some((value) => value !== '');

  const apiQuery = {
    page: uiPage + 1,
    pageSize: PAGE_SIZE,
    orderBy,
    code: filters.code || undefined,
    endonym: filters.endonym || undefined,
    isDefault: filters.isDefault || undefined,
    isActive: filters.isActive || undefined,
    sortOrder: filters.sortOrder || undefined,
  };

  const query = useQuery({
    queryKey: ['dictionaries', 'languages', apiQuery],
    queryFn: () => getLanguages(apiQuery),
    placeholderData: keepPreviousData,
  });

  const columns = canWrite ? [...COLUMNS, { field: null, label: '' }] : COLUMNS;

  function setFilter(patch: Partial<FilterState>) {
    setFilters((current) => ({ ...current, ...patch }));
    setUiPage(0);
  }

  function resetFilters() {
    setFilters(emptyFilters);
    setUiPage(0);
  }

  function toggleSort(field: LanguagesSortField) {
    setOrderBy((current) => {
      if (!current || current.field !== field) return { field, desc: false };
      return { field, desc: !current.desc };
    });
    setUiPage(0);
  }

  function handleAssigned(result: LanguageResponse) {
    // "Уже назначен" — не то, что вернул сервер (200 в обоих случаях),
    // а то, что мы уже знали из своего же ещё не инвалидированного кэша: был
    // ли назначаемый язык default'ом до этого запроса ("assigning
    // the current default again is a no-op") — see `describeDefaultAssignment`'s
    // own doc comment for the race this cannot close.
    const previousDefault = query.data?.data.find((entry) => entry.isDefault);
    setNotice(describeDefaultAssignment(result.code, result.endonym, previousDefault?.code));
    setDefaultTarget(null);
    void queryClient.invalidateQueries({ queryKey: ['dictionaries', 'languages'] });
  }

  return (
    <Box>
      <Stack direction="row" sx={{ justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
        <Typography variant="h5" component="h2">
          Языки
        </Typography>
        {canWrite && (
          <Button variant="contained" startIcon={<AddIcon />} onClick={() => navigate('/dictionaries/languages/new')}>
            Добавить язык
          </Button>
        )}
      </Stack>

      <Paper sx={{ p: 2, mb: 2 }}>
        <Stack direction="row" spacing={2} useFlexGap sx={{ flexWrap: 'wrap' }}>
          <TextField
            label="Код"
            size="small"
            value={filters.code}
            onChange={(event) => setFilter({ code: event.target.value })}
          />
          <TextField
            label="Эндоним"
            size="small"
            value={filters.endonym}
            onChange={(event) => setFilter({ endonym: event.target.value })}
          />
          <TextField
            label="По умолчанию"
            size="small"
            select
            sx={{ width: 150 }}
            value={filters.isDefault}
            onChange={(event) => setFilter({ isDefault: event.target.value as TriState })}
          >
            <MenuItem value="">Все</MenuItem>
            <MenuItem value="true">Да</MenuItem>
            <MenuItem value="false">Нет</MenuItem>
          </TextField>
          <TextField
            label="Активность"
            size="small"
            select
            sx={{ width: 150 }}
            value={filters.isActive}
            onChange={(event) => setFilter({ isActive: event.target.value as TriState })}
          >
            <MenuItem value="">Все</MenuItem>
            <MenuItem value="true">Активные</MenuItem>
            <MenuItem value="false">Неактивные</MenuItem>
          </TextField>
          <TextField
            label="Порядок"
            size="small"
            sx={{ width: 100 }}
            value={filters.sortOrder}
            onChange={(event) => setFilter({ sortOrder: event.target.value })}
          />
        </Stack>
      </Paper>

      {query.isPending && <RegistrySkeleton columns={columns.map((column) => column.label)} />}
      {query.isError && <ErrorBanner error={query.error} onRetry={() => query.refetch()} />}

      {query.data && (
        <TableContainer component={Paper}>
          <Table size="small" aria-label="Реестр языков">
            <TableHead>
              <TableRow>
                {columns.map((column) => (
                  <TableCell key={column.label || 'actions'}>
                    {column.field ? (
                      <TableSortLabel
                        active={orderBy?.field === column.field}
                        direction={orderBy && orderBy.field === column.field && orderBy.desc ? 'desc' : 'asc'}
                        onClick={() => toggleSort(column.field as LanguagesSortField)}
                      >
                        {column.label}
                      </TableSortLabel>
                    ) : (
                      column.label
                    )}
                  </TableCell>
                ))}
              </TableRow>
            </TableHead>
            <TableBody>
              {query.data.count === 0 && !isFilterActive && (
                <TableRow>
                  <TableCell colSpan={columns.length} align="center" sx={{ py: 4 }}>
                    <Stack spacing={1} sx={{ alignItems: 'center' }}>
                      <Typography variant="subtitle1">Справочник пуст</Typography>
                      <Typography variant="body2" color="text.secondary">
                        Добавьте первый язык кнопкой «Добавить язык» выше.
                      </Typography>
                      {canWrite && (
                        <Button
                          variant="outlined"
                          startIcon={<AddIcon />}
                          sx={{ mt: 1 }}
                          onClick={() => navigate('/dictionaries/languages/new')}
                        >
                          Добавить язык
                        </Button>
                      )}
                    </Stack>
                  </TableCell>
                </TableRow>
              )}
              {query.data.count === 0 && isFilterActive && (
                <TableRow>
                  <TableCell colSpan={columns.length} align="center" sx={{ py: 4 }}>
                    <Stack spacing={1} sx={{ alignItems: 'center' }}>
                      <Typography variant="subtitle1">Ничего не найдено</Typography>
                      <Typography variant="body2" color="text.secondary">
                        Ни один язык не подходит под заданные условия фильтра.
                      </Typography>
                      <Button variant="outlined" sx={{ mt: 1 }} onClick={resetFilters}>
                        Сбросить фильтр
                      </Button>
                    </Stack>
                  </TableCell>
                </TableRow>
              )}
              {query.data.data.map((language) => (
                <TableRow key={language.code} hover>
                  <TableCell>{language.code}</TableCell>
                  <TableCell>{language.endonym}</TableCell>
                  <TableCell>
                    {language.isDefault ? (
                      <Stack direction="row" spacing={0.5} sx={{ alignItems: 'center' }}>
                        <StarIcon fontSize="small" color="warning" />
                        <span>да</span>
                      </Stack>
                    ) : (
                      'нет'
                    )}
                  </TableCell>
                  <TableCell>
                    <Chip
                      label={language.isActive ? 'Активен' : 'Неактивен'}
                      color={language.isActive ? 'success' : 'default'}
                      size="small"
                    />
                  </TableCell>
                  <TableCell>{language.sortOrder}</TableCell>
                  <TableCell>{formatAuditStamp(language)}</TableCell>
                  {canWrite && (
                    <TableCell align="right">
                      <Tooltip title="Редактировать">
                        <IconButton
                          size="small"
                          aria-label={`Редактировать ${language.code}`}
                          onClick={() => navigate(`/dictionaries/languages/${encodeURIComponent(language.code)}`)}
                        >
                          <EditIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>
                      {language.isActive && !language.isDefault && (
                        <Tooltip title="Сделать языком по умолчанию">
                          <IconButton
                            size="small"
                            aria-label={`Сделать языком по умолчанию ${language.code}`}
                            onClick={() => setDefaultTarget({ code: language.code, endonym: language.endonym })}
                          >
                            <StarBorderIcon fontSize="small" />
                          </IconButton>
                        </Tooltip>
                      )}
                    </TableCell>
                  )}
                </TableRow>
              ))}
            </TableBody>
          </Table>
          <TablePagination
            component="div"
            count={query.data.count}
            page={uiPage}
            rowsPerPage={PAGE_SIZE}
            rowsPerPageOptions={[]}
            onPageChange={(_event, page) => setUiPage(page)}
          />
        </TableContainer>
      )}

      <DefaultLanguageDialog
        key={defaultTarget?.code ?? 'closed'}
        language={defaultTarget}
        onClose={() => setDefaultTarget(null)}
        onAssigned={handleAssigned}
      />

      <Snackbar open={notice !== null} autoHideDuration={6000} onClose={() => setNotice(null)} message={notice} />
    </Box>
  );
}

/**
 * Экспортированный экран: route-level гейт по `languages.read`, тот же
 * самогейтирующийся паттерн, что и у `LanguageFormPage` — так `app/
 * routes.tsx` монтирует страницы одинаково, без дополнительного
 * `RequirePermission` на месте использования.
 */
export function LanguagesRegistryPage() {
  return (
    <RequirePermission any={[dictionariesPermissions.languagesRead]} fallback={<AccessDenied />}>
      <LanguagesRegistryContent />
    </RequirePermission>
  );
}
