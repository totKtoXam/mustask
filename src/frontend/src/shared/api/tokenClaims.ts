// Права и имя вошедшего пользователя панель достаёт сама из payload
// access-токена, а не из отдельного ответа сервера (ни один эндпоинт /api/session/* не отдаёт объект пользователя).
// Подпись токена панель не проверяет — она и раньше не проверяла id-токен на
// клиенте, а токен всё равно уходит на Mustask.Api, который его верифицирует сам.

// Код клиента-аудитории, по которому ищутся права в `resource_access`
// (Mustask.Domain.Identity.PermissionCatalog — код прав как
// строки этот файл не перечисляет, они остаются точечными литералами в
// страницах, как и до этой задачи).
const AUDIENCE = 'mustask-api';

interface AccessTokenPayload {
  sub?: string;
  name?: string;
  preferred_username?: string;
  exp?: number;
  resource_access?: Record<string, { roles?: string[] } | undefined>;
}

export interface AccessTokenClaims {
  /**
   * The user's subject (`sub`), '' when absent: the key of what the panel
   * remembers per user, such as the work context (mod-002 design § 3.4).
   */
  subject: string;
  /** `name` → `preferred_username` → пустая строка. */
  displayName: string;
  /** Только `resource_access.mustask-api.roles` — `realm_access` и другие клиенты не читаются. */
  permissions: string[];
  /** `exp` в миллисекундах; `NaN`, если claim'а нет — вызывающий должен это учитывать. */
  expiresAt: number;
}

function decodePayload(token: string): AccessTokenPayload {
  const segment = token.split('.')[1];
  if (!segment) {
    return {};
  }
  try {
    const base64 = segment.replace(/-/g, '+').replace(/_/g, '/');
    const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '=');
    // `atob` отдаёт «бинарную» строку (один код-юнит на байт) — имя
    // вошедшего может быть кириллицей, и без TextDecoder такая строка
    // разобралась бы в мусор вместо UTF-8.
    const binary = atob(padded);
    const bytes = Uint8Array.from(binary, (char) => char.charCodeAt(0));
    const json = new TextDecoder().decode(bytes);
    return JSON.parse(json) as AccessTokenPayload;
  } catch {
    return {};
  }
}

/** Разбор access-токена на имя и права — без проверки подписи (см. заголовок файла). */
export function decodeAccessToken(token: string): AccessTokenClaims {
  const payload = decodePayload(token);
  return {
    subject: typeof payload.sub === 'string' ? payload.sub : '',
    displayName: payload.name ?? payload.preferred_username ?? '',
    permissions: payload.resource_access?.[AUDIENCE]?.roles ?? [],
    expiresAt: typeof payload.exp === 'number' ? payload.exp * 1000 : NaN,
  };
}
