// Тестовый помощник (tech-024, ADR-0033): токен несёт имена клиентских ролей, а тесты панели мыслят кодами прав. Этот помощник
// подбирает набор ролей, который даёт ровно перечисленные инстанс-коды — обратное к `permissionsFromRoles`. Недостижимый набор
// (например, запись справочника без чтения: каждая роль с правами требует `user`, а он читает) — ошибка теста, а не молчаливое
// округление: настоящий Keycloak такой токен не выдаст.
import { INSTANCE_ROLE_CODES, permissionsFromRoles, USER_ROLE } from '../shared/api/tokenClaims';

export function rolesForPermissions(codes: readonly string[]): string[] {
  const wanted = new Set(codes.filter((code) => Object.values(INSTANCE_ROLE_CODES).some((held) => held.includes(code))));
  if (wanted.size === 0) {
    return [];
  }

  const roles = Object.entries(INSTANCE_ROLE_CODES)
    .filter(([role, held]) => role === USER_ROLE || held.every((code) => wanted.has(code)))
    .map(([role]) => role);
  const given = new Set(permissionsFromRoles(roles));

  if (given.size !== wanted.size || [...wanted].some((code) => !given.has(code))) {
    throw new Error(
      `No set of roles holds exactly [${[...wanted].sort().join(', ')}]: every role with rights needs "user", which reads the dictionaries.`,
    );
  }
  return roles;
}
