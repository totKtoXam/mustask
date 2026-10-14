// Единственная точка HTTP-доступа к бизнес-маршрутам Mustask.Api: базовый URL из
// env, Bearer-токен из shared/api/session, один повтор на 401 и разбор
// ProblemDetails.
import { env } from '../config/env';
import { SessionLostError, getAccessToken, notifySessionError } from './session';

/**
 * Тело ошибки Mustask.Api — RFC 7807 ProblemDetails с расширениями MUSTASK
 * (ADR-0004/0006): `errorCode` — машинный код из закрытого реестра,
 * `detail` — готовый русский текст из каталога, `params` — сырые значения,
 * `errors` — причины по полям для 400.
 */
export interface ProblemDetails {
  title?: string;
  status?: number;
  detail?: string;
  errorCode?: string;
  params?: Record<string, { value: unknown; type: string }>;
  errors?: Record<string, { code: string; detail: string }[]>;
}

export class ApiError extends Error {
  readonly status: number;
  readonly body: ProblemDetails | null;
  /**
   * The request path (with its query) that got the answer, set by `send()` for
   * a non-OK answer; absent on an error built by hand. The access watcher
   * reads it to tell an organization-scoped 403/404 from any other
   * (mod-016 plan-frontend P-4): absent means "not scoped".
   */
  readonly path?: string;

  constructor(status: number, body: ProblemDetails | null, path?: string) {
    super(body?.errorCode ?? body?.title ?? `HTTP ${status}`);
    this.name = 'ApiError';
    this.status = status;
    this.body = body;
    this.path = path;
  }

  get errorCode(): string | undefined {
    return this.body?.errorCode;
  }
}

async function parseProblem(response: Response): Promise<ProblemDetails | null> {
  try {
    return (await response.json()) as ProblemDetails;
  } catch {
    return null;
  }
}

interface SendOptions extends Omit<RequestInit, 'body'> {
  body?: unknown;
}

async function attempt(
  path: string,
  token: string | null,
  options: SendOptions,
): Promise<Response> {
  const { body, headers, ...init } = options;
  const requestHeaders = new Headers(headers as HeadersInit | undefined);
  if (token) {
    requestHeaders.set('Authorization', `Bearer ${token}`);
  }
  if (body !== undefined) {
    requestHeaders.set('Content-Type', 'application/json');
  }
  return fetch(`${env.apiBaseUrl}${path}`, {
    ...init,
    // Бизнес-маршруты Mustask.Api читают только Bearer, не cookie сессии
    // — credentials 'omit', иначе браузер шлёт пустой
    // заголовок Cookie без всякой пользы.
    credentials: 'omit',
    headers: requestHeaders,
    body: body !== undefined ? JSON.stringify(body) : undefined,
  });
}

async function send(path: string, options: SendOptions = {}): Promise<Response> {
  let token: string | null;
  try {
    token = await getAccessToken();
  } catch (error) {
    // Только настоящая потеря сессии переводит панель на AnonymousScreen:
    // `AuthUnavailableError` — Keycloak недоступен, сеть моргнула,
    // таймаут — пробрасывается как есть, чтобы вызывающий (страница,
    // мутация) показал свою ошибку баннером и не потерял состояние экрана.
    if (error instanceof SessionLostError) {
      notifySessionError(error);
      throw new ApiError(401, null);
    }
    throw error;
  }

  let response = await attempt(path, token, options);

  // Ровно одна попытка продления на запрос: если и после свежего
  // токена 401, дело не в сроке жизни токена — это ApiError, а не повод
  // ещё раз идти на /api/session/token.
  if (response.status === 401) {
    try {
      const renewed = await getAccessToken({ forceRenew: true });
      response = await attempt(path, renewed, options);
    } catch (error) {
      // Тот же выбор, что и выше: продление посреди работы, упёршееся в
      // недоступный Keycloak (503/сеть/таймаут), не должно молча стать
      // «сессия потеряна» — экран и введённые пользователем данные остаются.
      if (error instanceof SessionLostError) {
        notifySessionError(error);
        throw new ApiError(401, await parseProblem(response));
      }
      throw error;
    }
  }

  if (!response.ok) {
    throw new ApiError(response.status, await parseProblem(response), path);
  }

  return response;
}

/** JSON-запрос; для ответов без тела (204) возвращает undefined. */
export async function apiFetch<T>(path: string, options: SendOptions = {}): Promise<T> {
  const response = await send(path, options);
  if (response.status === 204) {
    return undefined as T;
  }
  return (await response.json()) as T;
}
