// Форма языка: одна форма на два режима — создание (`/languages/new`,
// POST, без If-Match, без конфликта) и редактирование (`/languages/:code`,
// PUT, с If-Match и с VersionConflictPanel). Режим определяется наличием
// `:code` в маршруте, а не отдельным пропом, — оба пути ведут на один и тот
// же компонент.
import { useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate, useParams } from 'react-router-dom';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import FormControlLabel from '@mui/material/FormControlLabel';
import Stack from '@mui/material/Stack';
import Switch from '@mui/material/Switch';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import { ApiError } from '../../../shared/api/httpClient';
import { apiErrorText } from '../../../shared/lib/apiErrorText';
import { formatDateTime } from '../../../shared/lib/format';
import { ErrorBanner } from '../../../shared/ui/ErrorBanner';
import {
  createLanguage,
  getLanguageByCode,
  replaceLanguage,
  type LanguageResponse,
  type ReplaceLanguageInput,
} from '../api/dictionariesApi';
import { AccessDenied } from '../components/AccessDenied';
import { FormSkeleton } from '../components/FormSkeleton';
import { RequirePermission } from '../components/RequirePermission';
import { VersionConflictPanel } from '../components/VersionConflictPanel';
import { useConflictRecovery } from '../hooks/useConflictRecovery';
import { dictionariesPermissions } from '../permissions';

const LANGUAGES_LIST_PATH = '/dictionaries/languages';

const CODE_PATTERN = /^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8})*$/;
const CODE_HELP = 'от 2 до 8 символов, формат BCP-47 (например, ru, kk-KZ)';
const CODE_INVALID_MESSAGE =
  'Код должен содержать от 2 до 8 символов и соответствовать формату BCP-47 (например, ru, kk-KZ).';
const ENDONYM_MAX = 200;
const ENDONYM_TOO_LONG_MESSAGE = 'Не более 200 символов.';
const DEFAULT_CANNOT_BE_DEACTIVATED_HINT =
  'Сначала назначьте языком по умолчанию другой язык — кнопка «Сделать языком по умолчанию» в реестре языков.';

function isValidCode(value: string): boolean {
  return value.length >= 2 && value.length <= 8 && CODE_PATTERN.test(value);
}

function formatAuditStamp(updatedAt: string, updatedBy: string): string {
  return `${formatDateTime(updatedAt)} — ${updatedBy}`;
}

/**
 * Перечитывание не нашло язык (реестр больше не отдаёт эту строку — гонка с
 * тем, что язык, вообще-то, удалить нельзя, но пустой ответ на
 * `filter=code=<code>` теоретически достижим). Обычная клиентская ошибка,
 * а не код каталога: строка вида `dictionaries.*` здесь выглядела бы как
 * зарегистрированный серверный код, которого нет ни в
 * `contracts/errors/dictionaries.json`, ни в каком-либо реестре (ADR-0005/
 * ADR-0006) — `apiErrorText` всё равно не читает `message` обычного `Error`
 * и покажет тот же баннер общего вида, что и для остальных клиентских сбоев.
 */
class LanguageNotFoundOnRereadError extends Error {
  constructor() {
    super('Language not found on re-read (client-side condition, not a server error code).');
    this.name = 'LanguageNotFoundOnRereadError';
  }
}

/** Route-level гейт ("Доступ ограничен") — оба пути этой формы. */
export function LanguageFormPage() {
  return (
    <RequirePermission any={[dictionariesPermissions.languagesWrite]} fallback={<AccessDenied />}>
      <LanguageFormContent />
    </RequirePermission>
  );
}

function LanguageFormContent() {
  const { code: codeParam } = useParams<{ code?: string }>();
  const isEdit = codeParam !== undefined;
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  // ---- Режим создания (`/languages/new`) ------------------------------
  const [code, setCode] = useState('');

  // ---- Общий черновик формы (эндоним/порядок/активность) --------------
  const [endonym, setEndonym] = useState('');
  const [sortOrder, setSortOrder] = useState('0');
  const [isActive, setIsActive] = useState(false);
  const [savedIsActive, setSavedIsActive] = useState(false);
  const [version, setVersion] = useState<string | null>(null);
  const [updatedAt, setUpdatedAt] = useState<string | null>(null);
  const [updatedBy, setUpdatedBy] = useState<string | null>(null);
  const [seeded, setSeeded] = useState(false);

  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [topError, setTopError] = useState<string | null>(null);

  // ---- Режим редактирования (`/languages/:code`) — начальная загрузка --
  // Нет `GET /languages/{code}`: точечное перечитывание тем
  // же `filter=code=<code>&pageSize=1`, что и при конфликте версий.
  const query = useQuery<LanguageResponse>({
    queryKey: ['dictionaries', 'language', codeParam],
    queryFn: async () => {
      const language = await getLanguageByCode(codeParam as string);
      if (!language) {
        // Нет отдельного 404 от списочного маршрута — пустой результат
        // трактуется как обычная ошибка загрузки (ErrorBanner + повтор),
        // а не выдумывается свой текст (ближайший специфицированный путь —
        // тот же ErrorBanner).
        throw new LanguageNotFoundOnRereadError();
      }
      return language;
    },
    enabled: isEdit,
  });

  // Сидируем черновик из первого успешного ответа прямо в теле рендера (React
  // "adjusting state when a prop changes" — не эффект: без этого фоновый
  // refetch после invalidateQueries переписал бы то, что уже набрал
  // администратор). Условие `!seeded` не даёт сделать это дважды, поэтому
  // повторный рендер, вызванный этим же setState, сразу застаёт `seeded`
  // истинным и тело больше не выполняется.
  if (isEdit && query.data && !seeded) {
    setSeeded(true);
    setEndonym(query.data.endonym);
    setSortOrder(String(query.data.sortOrder));
    setIsActive(query.data.isActive);
    setSavedIsActive(query.data.isActive);
    setVersion(query.data.version);
    setUpdatedAt(query.data.updatedAt);
    setUpdatedBy(query.data.updatedBy);
  }

  function applyServerResult(result: LanguageResponse) {
    setEndonym(result.endonym);
    setSortOrder(String(result.sortOrder));
    setIsActive(result.isActive);
    setSavedIsActive(result.isActive);
    setVersion(result.version);
    setUpdatedAt(result.updatedAt);
    setUpdatedBy(result.updatedBy);
    setFieldErrors({});
    setTopError(null);
    void queryClient.invalidateQueries({ queryKey: ['dictionaries', 'languages'] });
  }

  const conflictRecovery = useConflictRecovery<ReplaceLanguageInput, LanguageResponse, LanguageResponse>({
    save: (input, ifMatch) => replaceLanguage(codeParam ?? '', input, ifMatch),
    reread: async () => {
      const language = await getLanguageByCode(codeParam ?? '');
      if (!language) {
        throw new LanguageNotFoundOnRereadError();
      }
      return language;
    },
    toFields: (mine, server) => [
      { label: 'Эндоним', mine: mine.endonym, server: server.endonym },
      { label: 'Порядок', mine: String(mine.sortOrder), server: String(server.sortOrder) },
      {
        label: 'Активность',
        mine: mine.isActive ? 'Активен' : 'Неактивен',
        server: server.isActive ? 'Активен' : 'Неактивен',
      },
    ],
    getVersion: (server) => server.version,
  });

  const busy = conflictRecovery.status === 'saving' || conflictRecovery.status === 'checking';
  const showConflictPanel = conflictRecovery.status === 'checking' || conflictRecovery.status === 'comparing';

  function handleSaveError(error: unknown) {
    if (error instanceof ApiError && error.status === 400) {
      const errors = error.body?.errors ?? {};
      const next: Record<string, string> = {};
      const endonymDetail = errors.endonym?.[0]?.detail;
      if (endonymDetail) {
        next.endonym = endonymDetail;
        // "Переключатель возвращается к последнему сохранённому значению,
        // так как ничего не было сохранено" (активация без
        // эндонима / очистка эндонима активного языка).
        setIsActive(savedIsActive);
      }
      const sortOrderDetail = errors.sortOrder?.[0]?.detail;
      if (sortOrderDetail) {
        next.sortOrder = sortOrderDetail;
      }
      if (Object.keys(next).length > 0) {
        setFieldErrors(next);
        return;
      }
    }
    if (error instanceof ApiError && error.status === 409 && error.errorCode === 'dictionaries.languages.default_cannot_be_deactivated') {
      setTopError(`${error.body?.detail ?? ''} ${DEFAULT_CANNOT_BE_DEACTIVATED_HINT}`.trim());
      setIsActive(true);
      return;
    }
    setTopError(apiErrorText(error));
  }

  async function handleSubmitEdit(event: FormEvent) {
    event.preventDefault();
    if (version === null) {
      return;
    }
    if (endonym.length > ENDONYM_MAX) {
      setFieldErrors({ endonym: ENDONYM_TOO_LONG_MESSAGE });
      return;
    }
    setFieldErrors({});
    setTopError(null);
    const input: ReplaceLanguageInput = { endonym, sortOrder: Number(sortOrder), isActive };
    try {
      const result = await conflictRecovery.submit(input, version);
      if (result) {
        applyServerResult(result);
      }
    } catch (error) {
      handleSaveError(error);
    }
  }

  async function handleKeepMine() {
    const input: ReplaceLanguageInput = { endonym, sortOrder: Number(sortOrder), isActive };
    try {
      const result = await conflictRecovery.keepMine(input);
      if (result) {
        applyServerResult(result);
      }
    } catch (error) {
      handleSaveError(error);
    }
  }

  function handleAcceptServer() {
    const server = conflictRecovery.acceptServer();
    if (server) {
      applyServerResult(server);
    }
  }

  // ---- Режим создания ---------------------------------------------------
  const createMutation = useMutation({
    mutationFn: createLanguage,
    onSuccess: (result) => {
      void queryClient.invalidateQueries({ queryKey: ['dictionaries', 'languages'] });
      // "S4new -> создан -> S4edit": открываем форму
      // редактирования только что созданного языка, не реестр.
      navigate(`/dictionaries/languages/${encodeURIComponent(result.code)}`, { replace: true });
    },
    onError: (error: unknown) => {
      if (error instanceof ApiError && error.status === 400) {
        const errors = error.body?.errors ?? {};
        const next: Record<string, string> = {};
        const codeDetail = errors.code?.[0]?.detail;
        if (codeDetail) next.code = codeDetail;
        const endonymDetail = errors.endonym?.[0]?.detail;
        if (endonymDetail) next.endonym = endonymDetail;
        if (Object.keys(next).length > 0) {
          setFieldErrors(next);
          return;
        }
      }
      if (error instanceof ApiError && error.status === 409 && error.errorCode === 'dictionaries.languages.already_exists') {
        setFieldErrors({ code: error.body?.detail ?? '' });
        return;
      }
      setTopError(apiErrorText(error));
    },
  });

  function handleSubmitCreate(event: FormEvent) {
    event.preventDefault();
    const errors: Record<string, string> = {};
    if (!isValidCode(code)) {
      errors.code = CODE_INVALID_MESSAGE;
    }
    if (endonym.length > ENDONYM_MAX) {
      errors.endonym = ENDONYM_TOO_LONG_MESSAGE;
    }
    if (Object.keys(errors).length > 0) {
      setFieldErrors(errors);
      return;
    }
    setFieldErrors({});
    setTopError(null);
    createMutation.mutate({
      code,
      endonym: endonym.trim() === '' ? undefined : endonym,
      sortOrder: sortOrder === '' ? undefined : Number(sortOrder),
    });
  }

  function handleCancel() {
    navigate(LANGUAGES_LIST_PATH);
  }

  return (
    <Box>
      <Stack direction="row" spacing={2} sx={{ alignItems: 'center', mb: 2 }}>
        <Button startIcon={<ArrowBackIcon />} onClick={handleCancel}>
          К списку
        </Button>
        <Typography variant="h5" component="h2">
          {isEdit ? `Язык: ${codeParam}` : 'Добавить язык'}
        </Typography>
      </Stack>

      {isEdit && query.isPending && <FormSkeleton fields={3} />}
      {isEdit && query.isError && <ErrorBanner error={query.error} onRetry={() => void query.refetch()} />}

      {isEdit && seeded && (
        <Box component="form" onSubmit={(event) => void handleSubmitEdit(event)}>
          <Stack spacing={2} sx={{ maxWidth: 480 }}>
            {topError && <Alert severity="error">{topError}</Alert>}
            <Typography variant="body2">Код: {codeParam} (нередактируемо)</Typography>
            <TextField
              label="Эндоним"
              value={endonym}
              onChange={(event) => setEndonym(event.target.value)}
              error={Boolean(fieldErrors.endonym)}
              helperText={fieldErrors.endonym}
              disabled={busy}
            />
            <TextField
              label="Порядок"
              type="number"
              value={sortOrder}
              onChange={(event) => setSortOrder(event.target.value)}
              error={Boolean(fieldErrors.sortOrder)}
              helperText={fieldErrors.sortOrder}
              disabled={busy}
            />
            <FormControlLabel
              control={
                <Switch checked={isActive} onChange={(event) => setIsActive(event.target.checked)} disabled={busy} />
              }
              label="Активен"
            />
            {updatedAt && updatedBy && (
              <Typography variant="caption" color="text.secondary">
                Изменено: {formatAuditStamp(updatedAt, updatedBy)}
              </Typography>
            )}

            {showConflictPanel ? (
              <VersionConflictPanel
                status={conflictRecovery.status === 'checking' ? 'checking' : 'comparing'}
                fields={conflictRecovery.fields}
                mine={{ updatedAt: null, updatedBy: null }}
                server={{
                  updatedAt: conflictRecovery.server?.updatedAt ?? '',
                  updatedBy: conflictRecovery.server?.updatedBy ?? '',
                }}
                onKeepMine={() => void handleKeepMine()}
                onAcceptServer={handleAcceptServer}
              />
            ) : (
              <Stack direction="row" spacing={2} sx={{ justifyContent: 'flex-end' }}>
                <Button onClick={handleCancel} disabled={busy}>
                  Отмена
                </Button>
                <Button type="submit" variant="contained" disabled={busy}>
                  Сохранить
                </Button>
              </Stack>
            )}
          </Stack>
        </Box>
      )}

      {!isEdit && (
        <Box component="form" onSubmit={handleSubmitCreate}>
          <Stack spacing={2} sx={{ maxWidth: 480 }}>
            {topError && <Alert severity="error">{topError}</Alert>}
            <TextField
              label="Код"
              value={code}
              onChange={(event) => setCode(event.target.value)}
              error={Boolean(fieldErrors.code)}
              helperText={fieldErrors.code ?? CODE_HELP}
              autoFocus
              disabled={createMutation.isPending}
            />
            <TextField
              label="Эндоним"
              value={endonym}
              onChange={(event) => setEndonym(event.target.value)}
              error={Boolean(fieldErrors.endonym)}
              helperText={fieldErrors.endonym ?? '(необязательно)'}
              disabled={createMutation.isPending}
            />
            <TextField
              label="Порядок"
              type="number"
              value={sortOrder}
              onChange={(event) => setSortOrder(event.target.value)}
              helperText="(необязательно, по умолчанию 0)"
              disabled={createMutation.isPending}
            />
            <Stack direction="row" spacing={2} sx={{ justifyContent: 'flex-end' }}>
              <Button onClick={handleCancel} disabled={createMutation.isPending}>
                Отмена
              </Button>
              <Button type="submit" variant="contained" disabled={createMutation.isPending}>
                Добавить
              </Button>
            </Stack>
          </Stack>
        </Box>
      )}
    </Box>
  );
}
