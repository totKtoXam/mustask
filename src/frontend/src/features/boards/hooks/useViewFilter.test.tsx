import { describe, expect, it } from 'vitest';
import type { ReactNode } from 'react';
import { MemoryRouter, useLocation, type InitialEntry } from 'react-router-dom';
import { act, renderHook } from '@testing-library/react';
import { projectScope, teamScope } from '../lib/scope';
import {
  PROJECT_FILTER_PARAM,
  projectFilterOf,
  projectFilterSearch,
  useProjectFilter,
  useViewFilter,
} from './useViewFilter';

// feat-040 BR-TS-8, OQ-2 A: the project filter of a team's views lives in the page address, as the
// repeated `projectId` of api-contract § 3.1 — the form mod-020 founded for the team filter.

describe('feat-040 BR-TS-8 the project filter an address carries', () => {
  it('is the repeated `projectId`, as the API names the read’s parameter', () => {
    expect(PROJECT_FILTER_PARAM).toBe('projectId');
  });

  it('reads every project in the address’s order, each once', () => {
    expect(projectFilterOf('?projectId=b&projectId=a&projectId=b')).toEqual(['b', 'a']);
  });

  it('is empty without the parameter, and a blank value is not a value', () => {
    expect(projectFilterOf('')).toEqual([]);
    expect(projectFilterOf('?workitem=BET-US-1&teamId=t')).toEqual([]);
    expect(projectFilterOf('?projectId=&projectId=')).toEqual([]);
  });

  it('keeps a value as written, whether it names a project of the team or not (BR-TS-8)', () => {
    expect(projectFilterOf('?projectId=not-a-project')).toEqual(['not-a-project']);
  });

  it('gives back only its own part for a link to another level or to the other page', () => {
    expect(projectFilterSearch('?workitem=BET-US-1&projectId=a&teamId=t&projectId=b')).toBe(
      '?projectId=a&projectId=b',
    );
    expect(projectFilterSearch('?workitem=BET-US-1')).toBe('');
  });
});

function mount(entries: InitialEntry[]) {
  return renderHook(
    () => ({
      project: useProjectFilter(),
      team: useViewFilter(teamScope('t1')),
      onProject: useViewFilter(projectScope('p1')),
      location: useLocation(),
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

describe('feat-040 useProjectFilter', () => {
  it('reads the chosen projects from the address and writes a choice back, replacing the entry', () => {
    const { result } = mount(['/organizations/o/teams/t1/backlogs/stories?projectId=a']);

    expect(result.current.project[0]).toEqual(['a']);
    act(() => result.current.project[1](['a', 'b']));
    expect(address(result.current.location)).toBe(
      '/organizations/o/teams/t1/backlogs/stories?projectId=a&projectId=b',
    );
    act(() => result.current.project[1]([]));
    expect(address(result.current.location)).toBe('/organizations/o/teams/t1/backlogs/stories');
  });

  it('keeps the card’s `?workitem=` and the team dimension while a choice is made', () => {
    const { result } = mount(['/p?workitem=BET-US-1&teamId=t']);

    act(() => result.current.project[1](['a']));

    expect(address(result.current.location)).toBe('/p?workitem=BET-US-1&teamId=t&projectId=a');
  });
});

describe('feat-040 useViewFilter: the one filter a view has', () => {
  it('is the project filter on a team’s views and the team filter on the project’s', () => {
    const { result } = mount(['/p?projectId=a&teamId=t&teamId=none']);

    expect(result.current.team[0]).toEqual(['a']);
    expect(result.current.onProject[0]).toEqual(['t', 'none']);
  });

  it('ignores the other dimension an address carries: it narrows nothing here', () => {
    const { result } = mount(['/p?teamId=t']);

    expect(result.current.team[0]).toEqual([]);
  });

  it('writes its own dimension and leaves the other where it is', () => {
    const { result } = mount(['/p?projectId=a&teamId=t']);

    act(() => result.current.team[1](['b']));
    expect(address(result.current.location)).toBe('/p?teamId=t&projectId=b');

    act(() => result.current.onProject[1](['none']));
    expect(address(result.current.location)).toBe('/p?projectId=b&teamId=none');
  });
});
