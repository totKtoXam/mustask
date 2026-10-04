import { useCallback, useState } from 'react';
import type { WidthClass } from '../theme/tokens';
import { readKeptChoice, writeKeptChoice, type NavigationChoice } from './keptChoice';
import { useWidthClass } from './useWidthClass';

/** How the main navigation stands: expanded, an icon rail, or an overlay from «Меню». */
export type MainNavigationLayout = 'expanded' | 'rail' | 'overlay';

/**
 * The layout rules of BR-10 and BR-19 (plan § 4.4), first match wins:
 * below 1024 px the overlay, whatever is kept; on a settings page the rail;
 * otherwise the kept choice; with none, expanded at 1280 px and wider and a
 * rail at 1024–1279 px.
 */
export function resolveMainLayout(
  widthClass: WidthClass,
  settingsShown: boolean,
  kept: NavigationChoice | null,
): MainNavigationLayout {
  if (widthClass === 'narrow') {
    return 'overlay';
  }
  if (settingsShown) {
    return 'rail';
  }
  if (kept) {
    return kept;
  }
  return widthClass === 'wide' ? 'expanded' : 'rail';
}

export interface NavigationLayout {
  widthClass: WidthClass;
  main: MainNavigationLayout;
  /** The collapse toggle: only outside settings and outside the overlay (BR-10, BR-19). */
  toggleShown: boolean;
  /** Collapses or expands, and keeps the choice per browser. */
  toggle(): void;
}

export function useNavigationLayout(settingsShown: boolean): NavigationLayout {
  const widthClass = useWidthClass();
  const [kept, setKept] = useState<NavigationChoice | null>(readKeptChoice);
  const main = resolveMainLayout(widthClass, settingsShown, kept);

  const toggle = useCallback(() => {
    const next: NavigationChoice = main === 'expanded' ? 'rail' : 'expanded';
    writeKeptChoice(next);
    setKept(next);
  }, [main]);

  return { widthClass, main, toggleShown: main !== 'overlay' && !settingsShown, toggle };
}
