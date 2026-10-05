import { describe, expect, it } from 'vitest';
import { encodeTestToken as token } from '../../test/jwt';
import { decodeAccessToken } from './tokenClaims';

describe('decodeAccessToken', () => {
  it('берёт права ровно из resource_access.mustask-api.roles', () => {
    const claims = decodeAccessToken(
      token({
        resource_access: {
          'mustask-api': { roles: ['dictionaries.languages.write'] },
          'other-client': { roles: ['other.role'] },
        },
        realm_access: { roles: ['admin'] },
      }),
    );

    expect(claims.permissions).toEqual(['dictionaries.languages.write']);
  });

  it('игнорирует realm_access и чужие клиенты', () => {
    const claims = decodeAccessToken(
      token({ realm_access: { roles: ['admin'] }, resource_access: { 'other-client': { roles: ['x'] } } }),
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
