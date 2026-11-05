// Права и имя вошедшего пользователя панель достаёт сама из payload
// access-токена, а не из отдельного ответа сервера (ни один эндпоинт /api/session/* не отдаёт объект пользователя).
// Подпись токена панель не проверяет — она и раньше не проверяла id-токен на
// клиенте, а токен всё равно уходит на Mustask.Api, который его верифицирует сам.

// Код клиента-аудитории, по которому ищутся роли в `resource_access`.
const AUDIENCE = 'mustask-api';

/**
 * tech-024 (ADR-0033): токен несёт имена четырёх клиентских ролей `mustask-api`, а не коды прав. Таблица «роль → инстанс-коды»
 * — рукописное зеркало `Mustask.Domain.Identity.InstanceRoles` (одна таблица в коде бэкенда, там же единственный источник
 * правды): меняется вместе с ней, одним изменением. Роль, которой нет в таблице, игнорируется; без роли `user` прав нет вовсе —
 * как и на сервере. Коды организации в токене не лежат (ADR-0027).
 */
export const INSTANCE_ROLE_CODES: Readonly<Record<string, readonly string[]>> = {
  user: ['dictionaries.languages.read'],
  'organization-creator': ['organizations.organization.create'],
  'dictionaries-admin': ['dictionaries.languages.read', 'dictionaries.languages.write'],
  'system-admin': [
    'dictionaries.languages.read',
    'dictionaries.languages.write',
    'organizations.organization.create',
    'organizations.organization.rescue',
  ],
};

/** Роль, без которой нет ни сессии, ни прав. */
export const USER_ROLE = 'user';

/** Инстанс-коды, которые дают роли токена: объединение по известным ролям; без `user` — пусто. */
export function permissionsFromRoles(roles: readonly string[]): string[] {
  if (!roles.includes(USER_ROLE)) {
    return [];
  }
  const codes = new Set<string>();
  for (const [role, held] of Object.entries(INSTANCE_ROLE_CODES)) {
    if (roles.includes(role)) {
      held.forEach((code) => codes.add(code));
    }
  }
  return [...codes];
}

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
  /** Инстанс-коды по ролям из `resource_access.mustask-api.roles` (`permissionsFromRoles`) — `realm_access` и другие клиенты не читаются. */
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
    permissions: permissionsFromRoles(payload.resource_access?.[AUDIENCE]?.roles ?? []),
    expiresAt: typeof payload.exp === 'number' ? payload.exp * 1000 : NaN,
  };
}
