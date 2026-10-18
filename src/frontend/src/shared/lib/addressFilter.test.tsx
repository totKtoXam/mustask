import { describe, expect, it } from 'vitest';
import type { ReactNode } from 'react';
import { MemoryRouter, useLocation, useNavigate, type InitialEntry } from 'react-router-dom';
import { act, renderHook } from '@testing-library/react';
import { filterSearchOf, filterValuesOf, useAddressFilter } from './addressFilter';

// The one form every filter dimension takes in a page address (mod-020 founded it for the
// team, feat-040 BR-TS-8 follows it for the project): the parameter repeated, one per value.

describe('the values an address carries for a dimension', () => {
  it('reads every value in the address’s order, each once', () => {
    expect(filterValuesOf('?projectId=b&projectId=a&projectId=b', 'projectId')).toEqual(['b', 'a']);
  });

  it('keeps the dimensions apart', () => {
    const search = '?teamId=t1&projectId=p1&teamId=none';

    expect(filterValuesOf(search, 'teamId')).toEqual(['t1', 'none']);
    expect(filterValuesOf(search, 'projectId')).toEqual(['p1']);
  });

  it('is empty without the parameter, and a blank value is not a value', () => {
    expect(filterValuesOf('', 'projectId')).toEqual([]);
    expect(filterValuesOf('?workitem=BET-US-1', 'projectId')).toEqual([]);
    expect(filterValuesOf('?projectId=&projectId=', 'projectId')).toEqual([]);
  });

  it('keeps a value as written, whether it names anything or not', () => {
    expect(filterValuesOf('?projectId=not-a-project&projectId=%D0%BA', 'projectId')).toEqual([
      'not-a-project',
      'к',
    ]);
  });
});

describe('the filter’s part of an address, for a link to another level', () => {
  it('gives back only the named dimension, nothing else the address holds', () => {
    expect(filterSearchOf('?workitem=BET-US-1&projectId=a&projectId=b&teamId=t', 'projectId')).toBe(
      '?projectId=a&projectId=b',
    );
    expect(filterSearchOf('?workitem=BET-US-1', 'projectId')).toBe('');
    expect(filterSearchOf('', 'projectId')).toBe('');
  });

  it('gives back several dimensions in the order they are named', () => {
    expect(filterSearchOf('?teamId=t&projectId=p', 'projectId', 'teamId')).toBe(
      '?projectId=p&teamId=t',
    );
  });
});

function mount(entries: InitialEntry[]) {
  return renderHook(
    () => ({
      filter: useAddressFilter('projectId'),
      location: useLocation(),
      navigate: useNavigate(),
    }),
    {
      wrapper: ({ children }: { children: ReactNode }) => (
        <MemoryRouter initialEntries={entries}>{children}</MemoryRouter>
      ),
    },
  );
}

function address(location: { pathname: string; search: string; hash: string }): string {
  return `${location.pathname}${location.search}${location.hash}`;
}

describe('useAddressFilter', () => {
  it('reads the chosen values from the address', () => {
    const { result } = mount(['/p?projectId=a&projectId=b']);

    expect(result.current.filter[0]).toEqual(['a', 'b']);
  });

  it('writes a choice, one parameter per value, and clearing removes them; another dimension stays', () => {
    const { result } = mount(['/p?teamId=t']);

    act(() => result.current.filter[1](['a', 'b']));
    expect(address(result.current.location)).toBe('/p?teamId=t&projectId=a&projectId=b');

    act(() => result.current.filter[1]([]));
    expect(address(result.current.location)).toBe('/p?teamId=t');
    expect(result.current.filter[0]).toEqual([]);
  });

  it('keeps every other parameter, the hash and the router state', () => {
    const { result } = mount([
      {
        pathname: '/p',
        search: '?workitem=BET-US-1&projectId=old',
        hash: '#top',
        state: { cardDepth: 1 },
      },
    ]);

    act(() => result.current.filter[1](['new']));

    expect(address(result.current.location)).toBe('/p?workitem=BET-US-1&projectId=new#top');
    expect(result.current.location.state).toEqual({ cardDepth: 1 });
  });

  it('is the same array while its content is, though the rest of the address moves', () => {
    const { result } = mount(['/p?projectId=a']);
    const first = result.current.filter[0];

    act(() => void result.current.navigate('/p?projectId=a&workitem=BET-US-1'));
    expect(result.current.filter[0]).toBe(first);

    act(() => void result.current.navigate('/p?projectId=b'));
    expect(result.current.filter[0]).toEqual(['b']);
  });
});
