import { describe, expect, it } from 'vitest';
import { encodeTestToken as token } from '../../test/jwt';
import { rolesForPermissions } from '../../test/instanceRoles';
import { decodeAccessToken, INSTANCE_ROLE_CODES, permissionsFromRoles } from './tokenClaims';

const READ = 'dictionaries.languages.read';
const WRITE = 'dictionaries.languages.write';
const CREATE = 'organizations.organization.create';

function withRoles(roles: string[]) {
  return token({ resource_access: { 'mustask-api': { roles } } });
}

describe('decodeAccessToken', () => {
  // tech-024 (ADR-0031): the token carries the four client-role names; the codes come from the table mirrored from the backend.
  it.each([
    [['user'], [READ]],
    [['user', 'organization-creator'], [READ, CREATE]],
    [['user', 'dictionaries-admin'], [READ, WRITE]],
    [['user', 'system-admin'], [READ, WRITE, CREATE]],
  ])('роли %j дают права %j', (roles, expected) => {
    expect([...decodeAccessToken(withRoles(roles)).permissions].sort()).toEqual([...expected].sort());
  });

  it('без роли user прав нет вовсе, что бы ещё ни было в токене', () => {
    expect(decodeAccessToken(withRoles(['system-admin', 'dictionaries-admin', 'organization-creator'])).permissions).toEqual([]);
    expect(decodeAccessToken(withRoles([])).permissions).toEqual([]);
  });

  it('неизвестные имена ролей и сырые коды прав игнорируются', () => {
    const claims = decodeAccessToken(withRoles(['user', 'instance-admin', WRITE, CREATE, 'offline_access']));

    expect(claims.permissions).toEqual([READ]);
  });

  it('берёт роли ровно из resource_access.mustask-api.roles', () => {
    const claims = decodeAccessToken(
      token({
        resource_access: {
          'mustask-api': { roles: ['user'] },
          'other-client': { roles: ['system-admin'] },
        },
        realm_access: { roles: ['system-admin'] },
      }),
    );

    expect(claims.permissions).toEqual([READ]);
  });

  it('игнорирует realm_access и чужие клиенты', () => {
    const claims = decodeAccessToken(
      token({ realm_access: { roles: ['user'] }, resource_access: { 'other-client': { roles: ['user'] } } }),
    );

    expect(claims.permissions).toEqual([]);
  });

  it('displayName = name, при отсутствии — preferred_username', () => {
    expect(decodeAccessToken(token({ name: 'Иванов Иван', preferred_username: 'ivanov' })).displayName).toBe(
      'Иванов Иван',
    );
    expect(decodeAccessToken(token({ preferred_username: 'ivanov' })).displayName).toBe('ivanov');
  });

  it('токен без обоих claim-ов не бросает и отдаёт пустую строку', () => {
    expect(() => decodeAccessToken(token({}))).not.toThrow();
    expect(decodeAccessToken(token({})).displayName).toBe('');
    expect(decodeAccessToken(token({})).permissions).toEqual([]);
  });

  it('expiresAt — exp в миллисекундах', () => {
    const exp = Math.floor(Date.now() / 1000) + 900;
    expect(decodeAccessToken(token({ exp })).expiresAt).toBe(exp * 1000);
  });

  it('mod-002 design § 3.4: subject is the `sub` claim, an empty string without one', () => {
    expect(decodeAccessToken(token({ sub: '5f0c-user' })).subject).toBe('5f0c-user');
    expect(decodeAccessToken(token({})).subject).toBe('');
    expect(decodeAccessToken(token({ sub: 42 })).subject).toBe('');
  });
});

describe('the mirrored role table (Mustask.Domain.Identity.InstanceRoles)', () => {
  it('has exactly the four roles', () => {
    expect(Object.keys(INSTANCE_ROLE_CODES)).toEqual(['user', 'organization-creator', 'dictionaries-admin', 'system-admin']);
  });

  it('system-admin holds every code any role holds', () => {
    const all = new Set(Object.values(INSTANCE_ROLE_CODES).flat());

    expect(new Set(INSTANCE_ROLE_CODES['system-admin'])).toEqual(all);
  });

  it('permissionsFromRoles and the test helper rolesForPermissions are inverse on reachable sets', () => {
    for (const codes of [[READ], [READ, CREATE], [READ, WRITE], [READ, WRITE, CREATE]]) {
      expect(permissionsFromRoles(rolesForPermissions(codes)).sort()).toEqual([...codes].sort());
    }
    expect(rolesForPermissions([])).toEqual([]);
    expect(() => rolesForPermissions([WRITE])).toThrow();
    expect(() => rolesForPermissions([CREATE])).toThrow();
  });
});
