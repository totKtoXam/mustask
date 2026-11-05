import { beforeEach, describe, expect, it } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import type { ReactNode } from 'react';
import { AuthContext, type AuthContextValue } from '../../auth/AuthContext';
import { structureStorageKey, useStructureSettings } from './useStructureSettings';

function wrapper(subject: string) {
  const value = { status: 'authenticated', subject } as unknown as AuthContextValue;
  return ({ children }: { children: ReactNode }) => (
    <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
  );
}

beforeEach(() => window.localStorage.clear());

describe('feat-049 BR-SD-9: the remembered view settings', () => {
  it('defaults to tree / dim / horizontal and writes nothing', () => {
    const { result } = renderHook(() => useStructureSettings('o1'), { wrapper: wrapper('u') });
    expect([result.current.view, result.current.mode, result.current.layout]).toEqual([
      'tree',
      'dim',
      'h',
    ]);
    expect(window.localStorage.length).toBe(0);
  });

  it('writes each choice under its own key, per user and organization', () => {
    const { result } = renderHook(() => useStructureSettings('o1'), { wrapper: wrapper('u') });
    act(() => result.current.setMode('hl'));
    act(() => result.current.setLayout('v'));
    expect(window.localStorage.getItem(structureStorageKey('mode', 'u', 'o1'))).toBe('hl');
    expect(window.localStorage.getItem(structureStorageKey('layout', 'u', 'o1'))).toBe('v');
    expect(structureStorageKey('view', 'u', 'o1')).toBe('mustask.structure.view.v1.u.o1');
  });

  it('ignores a value outside the allowed set and reads another organization on its own', () => {
    window.localStorage.setItem(structureStorageKey('mode', 'u', 'o1'), 'wild');
    window.localStorage.setItem(structureStorageKey('mode', 'u', 'o2'), 'only');
    const { result, rerender } = renderHook(({ org }) => useStructureSettings(org), {
      wrapper: wrapper('u'),
      initialProps: { org: 'o1' },
    });
    expect(result.current.mode).toBe('dim');
    rerender({ org: 'o2' });
    expect(result.current.mode).toBe('only');
  });
});
