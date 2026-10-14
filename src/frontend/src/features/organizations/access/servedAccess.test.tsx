import type { ReactNode } from 'react';
import { describe, expect, it } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { organizationsKeys } from '../api/queryKeys';
import type { AccessAnswer } from './accessAnswer';
import { useServedAccessCheck } from './servedAccess';

const ORG = 'a0000000-0000-4000-8000-000000000001';
const OTHER = 'a0000000-0000-4000-8000-000000000002';
const READ = 'organizations.project.read';
const WRITE = 'organizations.project.write';

function member(...permissions: string[]): AccessAnswer {
  return { kind: 'member', role: 'member', permissions };
}

function setup(can: (code: string) => boolean = () => false) {
  const client = new QueryClient();
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
  const rendered = renderHook(() => useServedAccessCheck(can), { wrapper });
  const hold = (organizationId: string, value: AccessAnswer) =>
    act(() => {
      client.setQueryData(organizationsKeys.access(organizationId), value);
    });
  return { client, rendered, hold };
}

describe('the shell’s check from served access (mod-016 design § 2.6, plan P-5)', () => {
  it('answers an instance code from the token and ignores the organization', () => {
    const { rendered } = setup((code) => code === 'dictionaries.languages.write');

    const { access } = rendered.result.current;

    expect(access('dictionaries.languages.write', 'instance')).toBe(true);
    expect(access('dictionaries.languages.write', 'instance', ORG)).toBe(true);
    expect(access('dictionaries.languages.read', 'instance')).toBe(false);
  });

  it('answers an organization code from the served codes of the organization asked about', () => {
    const { rendered, hold } = setup();
    hold(ORG, member(READ));
    hold(OTHER, member(READ, WRITE));

    const { access } = rendered.result.current;

    expect(access(READ, 'organization', ORG)).toBe(true);
    expect(access(WRITE, 'organization', ORG)).toBe(false);
    expect(access(WRITE, 'organization', OTHER)).toBe(true);
    // The three levels answer from the same organization's codes.
    expect(access(READ, 'project', ORG)).toBe(true);
    expect(access(READ, 'team', ORG)).toBe(true);
  });

  it('answers "no" for no organization, one that is unread, and one answered 404', () => {
    const { rendered, hold } = setup();
    hold(ORG, { kind: 'not-member' });

    const { access } = rendered.result.current;

    expect(access(READ, 'organization')).toBe(false);
    expect(access(READ, 'organization', '')).toBe(false);
    expect(access(READ, 'organization', OTHER)).toBe(false);
    expect(access(READ, 'organization', ORG)).toBe(false);
  });

  it('takes a new identity exactly when a served answer changes, so its consumers ask again', () => {
    const { rendered, hold } = setup();
    const first = rendered.result.current.access;

    hold(ORG, member(READ));
    const afterFirstRead = rendered.result.current.access;
    expect(afterFirstRead).not.toBe(first);
    expect(afterFirstRead(READ, 'organization', ORG)).toBe(true);

    // A re-read that answers the same thing changes nothing.
    hold(ORG, member(READ));
    expect(rendered.result.current.access).toBe(afterFirstRead);

    hold(ORG, member(READ, WRITE));
    expect(rendered.result.current.access).not.toBe(afterFirstRead);
    expect(rendered.result.current.access(WRITE, 'organization', ORG)).toBe(true);

    hold(ORG, { kind: 'not-member' });
    expect(rendered.result.current.access(WRITE, 'organization', ORG)).toBe(false);
  });

  it('forgets an organization whose entry leaves the cache', () => {
    const { client, rendered, hold } = setup();
    hold(ORG, member(READ));

    act(() => {
      client.removeQueries({ queryKey: organizationsKeys.access(ORG) });
    });

    expect(rendered.result.current.access(READ, 'organization', ORG)).toBe(false);
  });

  it('says an organization’s codes are read once an answer is held, the 404 included', () => {
    const { rendered, hold } = setup();
    expect(rendered.result.current.isCodesRead(ORG)).toBe(false);
    expect(rendered.result.current.isCodesRead(undefined)).toBe(false);

    hold(ORG, member(READ));
    hold(OTHER, { kind: 'not-member' });

    expect(rendered.result.current.isCodesRead(ORG)).toBe(true);
    expect(rendered.result.current.isCodesRead(OTHER)).toBe(true);
  });
});
