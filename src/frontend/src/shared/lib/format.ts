import dayjs from 'dayjs';

/**
 * ISO-строка сервера (`updatedAt` и подобные аудит-штампы) → локальная
 * дата-время для карточек/панелей. Один форматтер на всё приложение: когда
 * он был продублирован по срезу, одна из копий забыла вызвать хоть какой-
 * нибудь форматтер. Композицию с автором ("… — {updatedBy}") каждый
 * вызывающий по-прежнему строит сам — это не часть форматирования.
 *
 * Пустой/некорректный вход → `'—'`, а не `Invalid Date`. Сегодня
 * недостижимо (`useConflictRecovery` выставляет `server` раньше, чем
 * `status: 'comparing'`), но место вызова (`LanguageFormPage.tsx`)
 * по-прежнему подставляет `?? ''`, если сервер когда-нибудь не даст штамп.
 */
export function formatDateTime(iso: string | null | undefined): string {
  if (!iso) {
    return '—';
  }
  const parsed = dayjs(iso);
  return parsed.isValid() ? parsed.format('DD.MM.YYYY HH:mm') : '—';
}
