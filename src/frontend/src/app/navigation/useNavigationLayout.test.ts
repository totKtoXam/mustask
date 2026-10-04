import { describe, expect, it } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { setViewportWidth } from '../../test/viewport';
import { KEPT_CHOICE_KEY, readKeptChoice } from './keptChoice';
import { resolveMainLayout, useNavigationLayout } from './useNavigationLayout';

describe('US-4 Navigation collapses and adapts to narrow windows — the layout rules (BR-10, BR-19)', () => {
  it.each([
    ['narrow, any page, any choice', 'narrow', true, 'expanded', 'overlay'],
    ['narrow, no choice', 'narrow', false, null, 'overlay'],
    ['a settings page keeps the rail', 'wide', true, 'expanded', 'rail'],
    ['the kept choice outside settings', 'medium', false, 'expanded', 'expanded'],
    ['the kept rail at a wide window', 'wide', false, 'rail', 'rail'],
    ['no choice, 1280 px and wider', 'wide', false, null, 'expanded'],
    ['no choice, 1024–1279 px', 'medium', false, null, 'rail'],
  ] as const)('%s', (_case, widthClass, settingsShown, kept, expected) => {
    expect(resolveMainLayout(widthClass, settingsShown, kept)).toBe(expected);
  });
});

describe('US-4 Scenario: Defaults by width when no choice is kept', () => {
  it('is expanded at 1440 px, a rail at 1200 px, and expanded again at 1440 px', () => {
    const { result } = renderHook(() => useNavigationLayout(false));
    expect(result.current.main).toBe('expanded');

    act(() => setViewportWidth(1200));
    expect(result.current.main).toBe('rail');

    act(() => setViewportWidth(1440));
    expect(result.current.main).toBe('expanded');
  });
});

describe('US-4 Scenario: The choice is kept per browser', () => {
  it('stores the toggled choice, and a new mount reads it', () => {
    const first = renderHook(() => useNavigationLayout(false));
    act(() => first.result.current.toggle());
    expect(first.result.current.main).toBe('rail');
    expect(localStorage.getItem(KEPT_CHOICE_KEY)).toBe('rail');
    first.unmount();

    const second = renderHook(() => useNavigationLayout(false));
    expect(second.result.current.main).toBe('rail');
  });

  it('returns to the width default once browser storage is cleared', () => {
    localStorage.setItem(KEPT_CHOICE_KEY, 'rail');
    localStorage.clear();

    expect(readKeptChoice()).toBeNull();
    const { result } = renderHook(() => useNavigationLayout(false));
    expect(result.current.main).toBe('expanded');
  });

  it('ignores a value it did not write', () => {
    localStorage.setItem(KEPT_CHOICE_KEY, 'sideways');
    expect(readKeptChoice()).toBeNull();
  });
});

describe('US-4 Scenario: A kept choice holds across widths at 1024 px and above', () => {
  it('stays expanded at 1200 px once the user expanded it', () => {
    localStorage.setItem(KEPT_CHOICE_KEY, 'expanded');
    const { result } = renderHook(() => useNavigationLayout(false));

    act(() => setViewportWidth(1200));

    expect(result.current.main).toBe('expanded');
  });

  it('hides the toggle on settings pages and in the overlay', () => {
    const settings = renderHook(() => useNavigationLayout(true));
    expect(settings.result.current.toggleShown).toBe(false);
    settings.unmount();

    setViewportWidth(900);
    const narrow = renderHook(() => useNavigationLayout(false));
    expect(narrow.result.current.main).toBe('overlay');
    expect(narrow.result.current.toggleShown).toBe(false);
  });
});
