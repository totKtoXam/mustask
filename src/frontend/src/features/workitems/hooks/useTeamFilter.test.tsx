import { describe, expect, it } from 'vitest';
import type { ReactNode } from 'react';
import { MemoryRouter, useLocation, useNavigate, type InitialEntry } from 'react-router-dom';
import { act, renderHook } from '@testing-library/react';
import { teamFilterOf, teamFilterSearch, useTeamFilter } from './useTeamFilter';

// mod-020 (the human's answer to mod-017 OQ-9 B): the team filter lives in the page address, as
// the repeated `teamId` of api-contract § 3, so a link carries it.

describe('mod-020 the filter an address carries', () => {
  it('reads every teamId in the address’s order, each once, `none` among them', () => {
    expect(teamFilterOf('?teamId=b&teamId=none&teamId=a&teamId=b')).toEqual(['b', 'none', 'a']);
  });

  it('is empty without the parameter, and a blank value is not a value', () => {
    expect(teamFilterOf('')).toEqual([]);
    expect(teamFilterOf('?workitem=MSK-US-2')).toEqual([]);
    expect(teamFilterOf('?teamId=&teamId=')).toEqual([]);
  });

  it('keeps a value as written, whether it names a team of the project or not (BR-OT-8)', () => {
    expect(teamFilterOf('?teamId=not-a-team&teamId=%D0%BA')).toEqual(['not-a-team', 'к']);
  });

  it('gives back only its own part for a link to another level', () => {
    expect(teamFilterSearch('?workitem=MSK-US-2&teamId=a&teamId=none')).toBe(
      '?teamId=a&teamId=none',
    );
    expect(teamFilterSearch('?workitem=MSK-US-2')).toBe('');
    expect(teamFilterSearch('')).toBe('');
  });
});

/** The hook beside the router's address and its `navigate`, which stand in for the page's links. */
function mount(entries: InitialEntry[], initialIndex = entries.length - 1) {
  return renderHook(
    () => ({ filter: useTeamFilter(), location: useLocation(), navigate: useNavigate() }),
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

describe('mod-020 useTeamFilter', () => {
  it('reads the chosen values from the address', () => {
    const { result } = mount(['/p?teamId=a&teamId=none']);

    expect(result.current.filter[0]).toEqual(['a', 'none']);
  });

  it('writes a choice into the address, one teamId per value, and clearing removes them', () => {
    const { result } = mount(['/p']);

    act(() => result.current.filter[1](['a', 'none']));
    expect(address(result.current.location)).toBe('/p?teamId=a&teamId=none');
    expect(result.current.filter[0]).toEqual(['a', 'none']);

    act(() => result.current.filter[1]([]));
    expect(address(result.current.location)).toBe('/p');
    expect(result.current.filter[0]).toEqual([]);
  });

  it('keeps every other parameter, the hash and the router state', () => {
    const { result } = mount([
      {
        pathname: '/p',
        search: '?workitem=MSK-US-2&teamId=old&sort=title',
        hash: '#top',
        state: { cardDepth: 1 },
      },
    ]);

    act(() => result.current.filter[1](['new']));

    expect(address(result.current.location)).toBe('/p?workitem=MSK-US-2&sort=title&teamId=new#top');
    expect(result.current.location.state).toEqual({ cardDepth: 1 });
  });

  it('replaces the address’s entry, so Back leaves the page instead of stepping through filters', () => {
    const { result } = mount(['/before', '/p']);

    act(() => result.current.filter[1](['a']));
    act(() => result.current.filter[1](['b']));
    expect(address(result.current.location)).toBe('/p?teamId=b');

    act(() => void result.current.navigate(-1));

    expect(address(result.current.location)).toBe('/before');
  });

  it('is the same array while its content is, though the rest of the address moves', () => {
    const { result } = mount(['/p?teamId=a']);
    const first = result.current.filter[0];

    // The card's `?workitem=` comes and goes with the filter untouched.
    act(() => void result.current.navigate('/p?teamId=a&workitem=MSK-US-2'));
    expect(result.current.filter[0]).toBe(first);
    act(() => void result.current.navigate('/p?workitem=MSK-US-2&teamId=a'));
    expect(result.current.filter[0]).toBe(first);

    act(() => void result.current.navigate('/p?teamId=b'));
    expect(result.current.filter[0]).toEqual(['b']);
    expect(result.current.filter[0]).not.toBe(first);
  });
});
