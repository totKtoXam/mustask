import { afterEach, describe, expect, it, vi } from 'vitest';
import { readRemembered, storageKey, writeRemembered } from './storage';

afterEach(() => {
  vi.restoreAllMocks();
});

describe('mod-002 US-8 Scenario: Remembered across a reload — the storage', () => {
  it('keeps the selection under mustask.context.<sub> as {"organizationId","projectId","teamId"}', () => {
    writeRemembered('user-1', { organizationId: 'o', projectId: 'p', teamId: 't' });

    expect(storageKey('user-1')).toBe('mustask.context.user-1');
    expect(JSON.parse(localStorage.getItem('mustask.context.user-1') ?? '')).toEqual({
      organizationId: 'o',
      projectId: 'p',
      teamId: 't',
    });
    expect(readRemembered('user-1')).toEqual({ organizationId: 'o', projectId: 'p', teamId: 't' });
  });

  it('keeps one user’s selection apart from another’s', () => {
    writeRemembered('user-1', { organizationId: 'o1' });
    writeRemembered('user-2', { organizationId: 'o2' });

    expect(readRemembered('user-1').organizationId).toBe('o1');
    expect(readRemembered('user-2').organizationId).toBe('o2');
  });

  it.each([
    ['not json', '{'],
    ['an array', '[]'],
    ['a number', '42'],
    ['no organization', '{"projectId":"p"}'],
    ['ids of the wrong type', '{"organizationId":1}'],
  ])('remembers nothing from %s', (_case, raw) => {
    localStorage.setItem('mustask.context.user-1', raw);

    expect(readRemembered('user-1')).toEqual({});
  });

  it('remembers nothing without a subject, and writes nothing', () => {
    writeRemembered(undefined, { organizationId: 'o' });
    writeRemembered('', { organizationId: 'o' });

    expect(readRemembered(undefined)).toEqual({});
    expect(localStorage.length).toBe(0);
  });

  it('remembers nothing and fails nothing when storage throws', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new DOMException('blocked', 'SecurityError');
    });
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new DOMException('blocked', 'SecurityError');
    });

    expect(() => writeRemembered('user-1', { organizationId: 'o' })).not.toThrow();
    expect(readRemembered('user-1')).toEqual({});
  });
});
