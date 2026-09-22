import { ApiError } from '../api/httpClient';
import { AuthUnavailableError } from '../api/session';

/**
 * Человекочитаемый текст ошибки для баннеров и снекбаров.
 *
 * Основной источник — `detail` из ProblemDetails: сервер уже отрендерил
 * русский текст из каталога ошибок по коду и параметрам (ADR-0006), и
 * дублировать этот каталог на клиенте означало бы завести второй источник
 * истины, который разойдётся с первым. Здесь остаются только случаи, когда
 * текста от сервера нет вовсе.
 */
export function apiErrorText(error: unknown): string {
  if (error instanceof AuthUnavailableError) {
    // /api/session/token не ответил в разумное время посреди работы (не при
    // загрузке — тогда это отдельный экран AnonymousScreen).
    return 'Сервис аутентификации недоступен, повторите позже.';
  }
  if (error instanceof ApiError) {
    if (error.body?.detail) {
      return error.body.detail;
    }
    if (error.status === 401) {
      // Единственный 401, который сюда доходит, — продление уже прошло
      // успешно, а исходный запрос всё равно отвергнут:
      // настоящая потеря сессии AuthContext уводит на AnonymousScreen раньше,
      // чем эта ветка вообще увидит ошибку.
      return 'Не удалось подтвердить сессию на сервере. Повторите попытку позже.';
    }
    if (error.status === 403) {
      return 'Недостаточно прав для этой операции.';
    }
    if (error.status === 503) {
      return 'Сервис временно недоступен, повторите позже.';
    }
    return `Ошибка API (HTTP ${error.status}).`;
  }
  if (error instanceof Error && error.name === 'TypeError') {
    return 'Не удалось соединиться с API. Проверьте, что Mustask.Api запущен.';
  }
  return 'Непредвиденная ошибка. Повторите попытку.';
}
