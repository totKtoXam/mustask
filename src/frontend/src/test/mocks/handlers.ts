import { HttpResponse, http } from 'msw';
import { encodeTestToken } from '../jwt';
import { servedFor } from '../organizationRoles';
import { rolesForPermissions } from '../instanceRoles';

// Базовый URL совпадает с test.env.VITE_API_BASE_URL в vite.config.ts.
const api = 'http://localhost:5000';

/** Права стенда — коды, которые дают роли `resource_access.mustask-api.roles` access-токена (`user` и `dictionaries-admin`). */
export const TEST_PERMISSIONS = ['dictionaries.languages.read', 'dictionaries.languages.write'];

/**
 * Токен, который `tokenClaims.decodeAccessToken` умеет разобрать: панель не
 * проверяет подпись, поэтому третий сегмент — заглушка.
 */
export function fakeAccessToken(
  overrides: {
    name?: string | null;
    preferredUsername?: string | null;
    permissions?: string[];
    /** Имена клиентских ролей напрямую (в обход `permissions`): `[]` — токен без роли `user`. */
    roles?: string[];
    expiresInSeconds?: number;
  } = {},
): string {
  const {
    name = 'Иванов Иван',
    preferredUsername = 'ivanov',
    permissions = TEST_PERMISSIONS,
    roles,
    expiresInSeconds = 900,
  } = overrides;
  const payload: Record<string, unknown> = {
    exp: Math.floor(Date.now() / 1000) + expiresInSeconds,
    resource_access: { 'mustask-api': { roles: roles ?? rolesForPermissions(permissions) } },
  };
  if (name !== null) payload.name = name;
  if (preferredUsername !== null) payload.preferred_username = preferredUsername;
  return encodeTestToken(payload);
}

export const TEST_TOKEN = fakeAccessToken();

export const handlers = [
  http.post(`${api}/api/session/token`, () =>
    HttpResponse.json({
      accessToken: TEST_TOKEN,
      expiresAt: new Date(Date.now() + 900_000).toISOString(),
      expiresIn: 900,
    }),
  ),

  http.post(`${api}/api/session/logout`, () =>
    HttpResponse.json({
      logoutUrl: 'http://keycloak.invalid/realms/mustask/protocol/openid-connect/logout',
    }),
  ),

  // The shell's context source reads the active organizations on every
  // signed-in page (mod-002 D-o). By default an instance has none, so a test
  // that mounts the real routes reads nothing else unless it serves a
  // dataset of its own (mod-002 plan-frontend § 2.4).
  http.get(`${api}/api/organizations`, () => HttpResponse.json([])),

  // The served access of an organization (mod-016 api-contract § 1.1): the
  // shell reads it for the selection and the gate for the address. By
  // default the caller is the organization's administrator, so a test that
  // mounts the shell with a real selection (and `onUnhandledRequest:
  // 'error'`) needs nothing more; a test about another role serves its own
  // (`organizationRoles.ts`, `testMembership.ts`).
  http.get(`${api}/api/organizations/:organizationId/access`, () =>
    HttpResponse.json(servedFor('admin')),
  ),
];
