import { describe, expect, it } from 'vitest';
import type { ReactNode } from 'react';
import { MemoryRouter, useLocation, useNavigate, type InitialEntry } from 'react-router-dom';
import { act, renderHook } from '@testing-library/react';
import { useWorkItemFilter } from './useWorkItemFilter';

// feat-003 BR-WS-11, design § 3: the four filters live in the page address, in mod-020's form, and
// one writer sets them in one replace.

/** The hook beside the router's address and its `navigate`, which stand in for the page's links. */
function mount(entries: InitialEntry[], initialIndex = entries.length - 1) {
  return renderHook(
    () => ({ filter: useWorkItemFilter(), location: useLocation(), navigate: useNavigate() }),
    {
      wrapper: ({ children }: { children: ReactNode }) => (
        <MemoryRouter initialEntries={entries} initialIndex={initialIndex}>
          {children}
        </MemoryRouter>
      ),
    },
  );
}

function address(location: { pathname: string; search: string; hash: string }): string {
  return `${location.pathname}${location.search}${location.hash}`;
}

describe('feat-003 useWorkItemFilter reads the address', () => {
  it('reads the four dimensions', () => {
    const { result } = mount(['/p?state=a&assigneeId=u1&assigneeId=none&search=x&teamId=t']);

    expect(result.current.filter[0]).toEqual({
      state: ['a'],
      assigneeId: ['u1', 'none'],
      search: 'x',
      teamId: ['t'],
    });
  });

  it('is the same object while its content is, though the rest of the address moves', () => {
    const { result } = mount(['/p?state=a']);
    const first = result.current.filter[0];

    act(() => void result.current.navigate('/p?state=a&workitem=MSK-US-2'));
    expect(result.current.filter[0]).toBe(first);

    act(() => void result.current.navigate('/p?state=b'));
    expect(result.current.filter[0]).toEqual(expect.objectContaining({ state: ['b'] }));
    expect(result.current.filter[0]).not.toBe(first);
  });
});

describe('feat-003 useWorkItemFilter writes the address', () => {
  it('writes a change of one dimension and keeps the others', () => {
    const { result } = mount(['/p?state=a&teamId=t']);

    act(() => result.current.filter[1]({ search: 'x' }));

    expect(address(result.current.location)).toBe('/p?state=a&search=x&teamId=t');
  });

  it('writes all four in one replace: a clear is one entry and removes every dimension', () => {
    const { result } = mount(['/before', '/p?state=a&assigneeId=u1&search=x&teamId=t&sort=title']);

    act(() => result.current.filter[1]({ state: [], assigneeId: [], search: '', teamId: [] }));
    expect(address(result.current.location)).toBe('/p?sort=title');
    expect(result.current.filter[0]).toEqual({ state: [], assigneeId: [], search: '', teamId: [] });

    act(() => void result.current.navigate(-1));
    expect(address(result.current.location)).toBe('/before');
  });

  it('keeps every other parameter, the hash and the router state, and replaces the entry', () => {
    const { result } = mount([
      '/before',
      { pathname: '/p', search: '?workitem=MSK-US-2', hash: '#top', state: { cardDepth: 1 } },
    ]);

    act(() => result.current.filter[1]({ state: ['Новая'] }));
    act(() => result.current.filter[1]({ assigneeId: ['none'] }));

    expect(address(result.current.location)).toBe(
      `/p?workitem=MSK-US-2&state=${encodeURIComponent('Новая')}&assigneeId=none#top`,
    );
    expect(result.current.location.state).toEqual({ cardDepth: 1 });
    act(() => void result.current.navigate(-1));
    expect(address(result.current.location)).toBe('/before');
  });

  it('leaves blank values and repeats out', () => {
    const { result } = mount(['/p']);

    act(() => result.current.filter[1]({ state: ['a', 'a', ' '], assigneeId: [''], search: '  ' }));

    expect(address(result.current.location)).toBe('/p?state=a');
  });

  it('two changes made before the router has rendered lose neither dimension to the other’s write', () => {
    const { result } = mount(['/p']);

    act(() => {
      result.current.filter[1]({ state: ['a'] });
      result.current.filter[1]({ assigneeId: ['u1'] });
      result.current.filter[1]({ search: 'x' });
    });

    expect(address(result.current.location)).toBe('/p?state=a&assigneeId=u1&search=x');
  });

  it('does not write when the address already says it', () => {
    const { result } = mount(['/p?state=a']);
    const before = result.current.location.key;

    act(() => result.current.filter[1]({ state: ['a'] }));

    expect(result.current.location.key).toBe(before);
  });

  it('builds on the address Back or a link left, not on what it wrote before', () => {
    const { result } = mount(['/p?state=a']);
    act(() => result.current.filter[1]({ search: 'x' }));

    act(() => void result.current.navigate('/p?teamId=t'));
    act(() => result.current.filter[1]({ search: 'y' }));

    expect(address(result.current.location)).toBe('/p?search=y&teamId=t');
  });
});
