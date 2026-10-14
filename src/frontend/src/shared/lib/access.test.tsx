import type { ReactNode } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { renderHook } from '@testing-library/react';
import { AccessCheckContext, bindAccessCheck, useAccessCheck, type AccessCheck } from './access';

describe('bindAccessCheck (mod-016 design § 2.6)', () => {
  it('asks the base check about the bound organization when the call names none', () => {
    const base = vi.fn<AccessCheck>(() => true);
    const bound = bindAccessCheck(base, 'org-a');

    bound('organizations.project.write', 'organization');

    expect(base).toHaveBeenCalledWith('organizations.project.write', 'organization', 'org-a');
  });

  it('lets a call name another organization, which wins over the bound one', () => {
    const base = vi.fn<AccessCheck>(() => false);
    const bound = bindAccessCheck(base, 'org-a');

    expect(bound('organizations.project.write', 'organization', 'org-b')).toBe(false);

    expect(base).toHaveBeenCalledWith('organizations.project.write', 'organization', 'org-b');
  });

  it('answers what the base check answers, instance codes included', () => {
    const base: AccessCheck = (code, scope, organizationId) =>
      scope === 'instance' ? code === 'dictionaries.languages.read' : organizationId === 'org-a';
    const bound = bindAccessCheck(base, 'org-a');

    expect(bound('dictionaries.languages.read', 'instance')).toBe(true);
    expect(bound('organizations.team.read', 'team')).toBe(true);
    expect(bindAccessCheck(base, 'org-b')('organizations.team.read', 'team')).toBe(false);
  });
});

describe('useAccessCheck outside the shell', () => {
  it('fails closed: every code is "no"', () => {
    const { result } = renderHook(() => useAccessCheck());

    expect(result.current('dictionaries.languages.read', 'instance')).toBe(false);
    expect(result.current('organizations.project.read', 'organization', 'org-a')).toBe(false);
  });

  it('returns the check a provider supplies', () => {
    const check: AccessCheck = () => true;
    const wrapper = ({ children }: { children: ReactNode }) => (
      <AccessCheckContext.Provider value={check}>{children}</AccessCheckContext.Provider>
    );

    const { result } = renderHook(() => useAccessCheck(), { wrapper });

    expect(result.current).toBe(check);
  });
});
