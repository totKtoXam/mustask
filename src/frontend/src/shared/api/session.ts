// Сессия панели поверх token-mediating backend'а (ADR-0022). Панель не ведёт OIDC-обмен и не хранит refresh-токен
// вовсе — только access-токен, и только в module-level переменной этого
// файла: не localStorage, не sessionStorage, не cookie, которую мог бы
// прочитать скрипт.
import { env } from '../config/env';

/** `POST /api/session/token` ответил 401 — сессии нет (`errorCode` из тела). */
export class SessionLostError extends Error {
  readonly code: 'session.anonymous' | 'session.expired';

  constructor(code: 'session.anonymous' | 'session.expired') {
    super(code);
    this.name = 'SessionLostError';
    this.code = code;
  }
}

/** Сеть/таймаут/иной отказ сессионного бэкенда — не то же самое, что потеря сессии. */
export class AuthUnavailableError extends Error {
  constructor() {
    super('auth_unavailable');
    this.name = 'AuthUnavailableError';
  }
}

interface TokenResponseBody {
  accessToken: string;
  /** Абсолютная метка сервера — по часам `Mustask.Api`; панель её для планирования не читает (ниже). */
  expiresAt: string;
  /** Целые секунды жизни токена, посчитанные на сервере — единственное, от чего планируется продление. */
  expiresIn: number;
}

interface CachedToken {
  token: string;
  /**
   * Локальная метка истечения: `Date.now()` в момент получения ответа плюс
   * `expiresIn * 1000` — считается ЦЕЛИКОМ по часам вкладки, а не по
   * `expiresAt` сервера. Раньше кэш и таймер сравнивали абсолютную метку
   * сервера с локальным `Date.now()`; если часы вкладки уходят вперёд даже
   * на минуты, `expiresAt` сервера начинает казаться уже почти прошедшим, а
   * пересчитанная после каждого срабатывания задержка — уходить в 0: таймер
   * бьёт `/api/session/token` без остановки, пока расхождение не выберется
   * само. Считая срок от момента получения по одному и тому же
   * (пусть и не абсолютно точному) локальному времени, кэш и планировщик
   * всегда сверяются сами с собой, а не с чужими часами — расхождение с
   * сервером на исход не влияет вовсе.
   */
  expiresAt: number;
}

// Порог упреждающего продления — тот же, что у кэша токенов бэкенда: совпадение порогов не требуется для корректности (сервер и
// панель планируют независимо, каждый по своим часам), но одинаковое
// значение не сбивает с толку при чтении обоих мест разом.
const RENEW_LEAD_MS = 60_000;

let cached: CachedToken | null = null;
let pending: Promise<string> | null = null;
let proactiveRenewalTimer: ReturnType<typeof setTimeout> | null = null;

type SessionErrorHandler = (error: unknown) => void;
let sessionErrorHandler: SessionErrorHandler | null = null;

type TokenHandler = (token: string) => void;
let sessionTokenHandler: TokenHandler | null = null;

/** `AuthContext` — единственный подписчик: узнаёт о потере сессии на пути httpClient и на пути упреждающего продления ниже. */
export function setSessionErrorHandler(handler: SessionErrorHandler | null): void {
  sessionErrorHandler = handler;
}

/** Вызывается httpClient'ом, когда продление на 401 не удалось (shared/api/httpClient.ts). */
export function notifySessionError(error: unknown): void {
  sessionErrorHandler?.(error);
}

/**
 * `AuthContext` — узнаёт о каждом успешно полученном токене (загрузка,
 * упреждающее продление, повтор после 401) единым способом, а не отдельным
 * вызовом на каждый из «трёх моментов» (загрузка, таймер, повтор) — вызывающему нет
 * нужды различать, кто именно попросил токен.
 */
export function setSessionTokenHandler(handler: TokenHandler | null): void {
  sessionTokenHandler = handler;
}

/**
 * Access-токен вкладки: свежий кэш отдаётся без похода в сеть, иначе —
 * один запрос на пачку параллельных вызовов (single-flight).
 */
export function getAccessToken(opts: { forceRenew?: boolean } = {}): Promise<string> {
  if (!opts.forceRenew && cached && cached.expiresAt - Date.now() > RENEW_LEAD_MS) {
    return Promise.resolve(cached.token);
  }
  return (pending ??= requestToken().finally(() => {
    pending = null;
  }));
}

/**
 * Момент 2 («по таймеру expiresAt − 60 с») живёт здесь, а не
 * в React: только этот модуль знает точный срок последнего выданного
 * токена, и планирование должно пережить любую перерисовку `AuthContext`.
 * Сбой здесь не пользовательский запрос — сеть могла на секунду моргнуть, и
 * это не повод показывать AnonymousScreen самому по себе (* «посреди работы экран не меняется»); настоящая потеря сессии всё равно
 * долетит через `notifySessionError`, как и с любого другого пути.
 */
function clearProactiveRenewalTimer(): void {
  if (proactiveRenewalTimer !== null) {
    clearTimeout(proactiveRenewalTimer);
    proactiveRenewalTimer = null;
  }
}

function scheduleProactiveRenewal(expiresAt: number): void {
  clearProactiveRenewalTimer();
  const delay = Math.max(expiresAt - Date.now() - RENEW_LEAD_MS, 0);
  proactiveRenewalTimer = setTimeout(() => {
    getAccessToken().catch((error: unknown) => {
      if (error instanceof SessionLostError) {
        notifySessionError(error);
      }
    });
  }, delay);
}

async function requestToken(): Promise<string> {
  let response: Response;
  try {
    response = await fetch(`${env.apiBaseUrl}/api/session/token`, {
      method: 'POST',
      credentials: 'include',
    });
  } catch {
    throw new AuthUnavailableError();
  }

  if (response.status === 401) {
    cached = null;
    const body = (await response.json().catch(() => null)) as { errorCode?: string } | null;
    throw new SessionLostError(
      body?.errorCode === 'session.expired' ? 'session.expired' : 'session.anonymous',
    );
  }
  if (!response.ok) {
    throw new AuthUnavailableError();
  }

  // «Момент, когда ответ пришёл» — здесь, а не раньше (сеть могла
  // занять сколько угодно) и не после `response.json()` (парсинг не должен
  // сдвигать точку отсчёта, хоть обычно это доли миллисекунды).
  const receivedAt = Date.now();
  const body = (await response.json()) as TokenResponseBody;
  cached = { token: body.accessToken, expiresAt: receivedAt + body.expiresIn * 1000 };
  scheduleProactiveRenewal(cached.expiresAt);
  sessionTokenHandler?.(body.accessToken);
  return body.accessToken;
}

/** Тест-сид навигации верхнего окна: jsdom не умеет `location.assign`. */
let navigateImpl: (url: string) => void = (url) => {
  window.location.assign(url);
};

export function setNavigateImpl(fn: (url: string) => void): void {
  navigateImpl = fn;
}

/** Вход — навигация, не fetch: дальше вкладкой управляют бэкенд и Keycloak. */
export function signIn(returnTo: string = '/'): void {
  const target = returnTo.startsWith('/') && !returnTo.startsWith('//') ? returnTo : '/';
  navigateImpl(`${env.apiBaseUrl}/api/session/login?returnUrl=${encodeURIComponent(target)}`);
}

/**
 * Выход: сначала сетевой вызов, и только на его успехе — очистка
 * локального состояния и навигация на `logoutUrl`, в этом порядке.
 * `onBeforeNavigate` — место для `queryClient.clear()`:
 * этот файл не знает о React Query, вызывающий (`AuthContext`) — знает.
 */
export async function signOut(onBeforeNavigate?: () => void): Promise<void> {
  // Таймер снимается ДО сетевого вызова, а не только на успехе:
  // на 503/сетевом сбое `signOut` бросает ниже, и если бы таймер оставался
  // взведён, ближайшее упреждающее продление («момент 2»)
  // при поднявшемся Keycloak тихо вернуло бы панель из «Выход не завершён»
  // в рабочий экран без явного действия человека — а это запрещено именно
  // как побочный эффект. Кэш здесь не трогаем: до успешного ответа
  // сессия остаётся живой, а «Повторить выход» ходит по cookie, не
  // по токену.
  clearProactiveRenewalTimer();

  let response: Response;
  try {
    response = await fetch(`${env.apiBaseUrl}/api/session/logout`, {
      method: 'POST',
      credentials: 'include',
    });
  } catch {
    throw new AuthUnavailableError();
  }
  if (!response.ok) {
    throw new AuthUnavailableError();
  }

  const { logoutUrl } = (await response.json()) as { logoutUrl: string };
  cached = null;
  onBeforeNavigate?.();
  navigateImpl(logoutUrl);
}

/** Сброс модульного состояния между тестами — сессии не должны протекать между ними. */
export function resetSessionModule(): void {
  cached = null;
  pending = null;
  sessionErrorHandler = null;
  sessionTokenHandler = null;
  clearProactiveRenewalTimer();
  navigateImpl = (url) => window.location.assign(url);
}
