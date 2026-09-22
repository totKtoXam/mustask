// Восстановление после версионного конфликта для любой записи с `If-Match`
// (сегодня — редактирование языка). Вынесено в хук, чтобы одна и та же
// последовательность восстановления не переписывалась заново для каждой
// новой записи с `If-Match`.
import { useCallback, useState } from 'react';
import { ApiError } from '../../../shared/api/httpClient';

/** Видимое поведение: сохраняем → проверяем версию → сравниваем. */
export type ConflictStatus = 'idle' | 'saving' | 'checking' | 'comparing';

export interface ConflictField {
  label: string;
  mine: string;
  server: string;
}

interface UseConflictRecoveryOptions<TInput, TResult, TServerData> {
  /** Сама запись (`PUT /languages/{code}` и т. п.), несёт `If-Match`. */
  save: (input: TInput, ifMatch: string) => Promise<TResult>;
  /** Перечитывание ровно того ресурса, что конфликтовал. */
  reread: () => Promise<TServerData>;
  /** Строит пары "моё/серверное" для показа в `VersionConflictPanel`. */
  toFields: (mine: TInput, server: TServerData) => ConflictField[];
  /** Версия для следующего `If-Match` — своя у каждого ресурса. */
  getVersion: (server: TServerData) => string;
}

export interface UseConflictRecoveryResult<TInput, TResult, TServerData> {
  status: ConflictStatus;
  /** Заполнено только при `status === 'comparing'`, иначе пустой массив. */
  fields: ConflictField[];
  /** Последний перечитанный ответ сервера — `null` вне восстановления. */
  server: TServerData | null;
  /** Первая попытка сохранить `input` с текущим (возможно, устаревшим) `ifMatch`. */
  submit: (input: TInput, ifMatch: string) => Promise<TResult | undefined>;
  /** "Сохранить свой вариант": тот же `input`, `If-Match` — версия из перечитывания. */
  keepMine: (input: TInput) => Promise<TResult | undefined>;
  /** "Принять серверный вариант": не шлёт запрос, отдаёт вызывающему перечитанные данные и сбрасывает панель. */
  acceptServer: () => TServerData | null;
  /** Уход с блока/экрана посреди панели — черновик просто не был отправлен, отправлять нечего. */
  reset: () => void;
}

/**
 * И 409 `dictionaries.version_conflict`, и 400 `dictionaries.validation_failed`
 * с `errors.ifMatch` по причине `dictionaries.version_required` (два
 * администратора одновременно создают одну и ту же отсутствующую запись)
 * ведут в один и тот же `'comparing'` — `ifMatch` при этом
 * никогда не показывается администратору как имя поля.
 */
function isVersionConflictError(error: unknown): boolean {
  if (!(error instanceof ApiError)) {
    return false;
  }
  if (error.status === 409 && error.errorCode === 'dictionaries.version_conflict') {
    return true;
  }
  if (error.status === 400) {
    const ifMatchErrors = error.body?.errors?.ifMatch;
    return Boolean(ifMatchErrors?.some((entry) => entry.code === 'dictionaries.version_required'));
  }
  return false;
}

export function useConflictRecovery<TInput, TResult, TServerData>({
  save,
  reread,
  toFields,
  getVersion,
}: UseConflictRecoveryOptions<TInput, TResult, TServerData>): UseConflictRecoveryResult<
  TInput,
  TResult,
  TServerData
> {
  const [status, setStatus] = useState<ConflictStatus>('idle');
  const [server, setServer] = useState<TServerData | null>(null);
  const [fields, setFields] = useState<ConflictField[]>([]);

  const attempt = useCallback(
    async (input: TInput, ifMatch: string): Promise<TResult | undefined> => {
      setStatus('saving');
      try {
        const result = await save(input, ifMatch);
        setStatus('idle');
        setServer(null);
        setFields([]);
        return result;
      } catch (error) {
        if (!isVersionConflictError(error)) {
          setStatus('idle');
          throw error;
        }
        setStatus('checking');
        try {
          const fresh = await reread();
          setServer(fresh);
          setFields(toFields(input, fresh));
          setStatus('comparing');
          return undefined;
        } catch (rereadError) {
          // Перечитать саму запись не удалось — не остаёмся в подвешенном
          // "checking"; экран получает исходную ошибку тем же путём, что и
          // любая другая (свой ErrorBanner), а не тонет в спиннере навечно.
          setStatus('idle');
          throw rereadError;
        }
      }
    },
    [save, reread, toFields],
  );

  const keepMine = useCallback(
    (input: TInput) => {
      // Нет перечитанной версии — панель не открыта, посылать нечего.
      if (!server) {
        return Promise.resolve<TResult | undefined>(undefined);
      }
      // Второй конфликт (ещё один гонщик) повторяет цикл с шага 1 — `attempt`
      // сам снова уйдёт в 'checking' → 'comparing', а не сдастся молча.
      return attempt(input, getVersion(server));
    },
    [attempt, server, getVersion],
  );

  const acceptServer = useCallback((): TServerData | null => {
    const current = server;
    setStatus('idle');
    setServer(null);
    setFields([]);
    return current;
  }, [server]);

  const reset = useCallback(() => {
    setStatus('idle');
    setServer(null);
    setFields([]);
  }, []);

  return { status, fields, server, submit: attempt, keepMine, acceptServer, reset };
}
