import { createContext, useContext } from 'react';

/** One ancestor in the page trail: a link when it has a page, plain text otherwise (BR-6). */
export interface TrailSegment {
  label: string;
  to?: string;
}

/**
 * Where the shell shows the trail: in the top bar at 1024 px and wider, above
 * the page title below that (design § 3.6).
 */
export type TrailPlacement = 'topBar' | 'header';

/**
 * The seam between the shell and a page's header (mod-015 plan § 4.5, D-7).
 *
 * A page that renders `PageHeader` registers it here, with any ancestors of
 * its own. The shell derives the rest of the trail from its navigation model
 * — «Настройки / Справочники» for a settings page, «Языки» for a page beneath
 * the registry — so no slice copies the model's labels. A page that renders a
 * full-page frame instead (refused, not found) registers no header and so has
 * no trail.
 */
export interface PageChrome {
  /** Registers a mounted page header; returns the function that unregisters it. */
  registerHeader(ancestors: readonly TrailSegment[]): () => void;
  /**
   * Registers a mounted not-found frame: no page serves the address (BR-13),
   * so the shell marks no navigation entry current and shows no settings
   * navigation, whatever the address starts with. Returns the function that
   * unregisters it.
   */
  registerNotFound(): () => void;
  /** The trail the shell derived for the registered header; empty without one. */
  trail: readonly TrailSegment[];
  trailPlacement: TrailPlacement;
}

const noRegistration = () => () => {};

/** Outside the shell (a page test, the auth card) there is no trail. */
export const PageChromeContext = createContext<PageChrome>({
  registerHeader: noRegistration,
  registerNotFound: noRegistration,
  trail: [],
  trailPlacement: 'topBar',
});

export function usePageChrome(): PageChrome {
  return useContext(PageChromeContext);
}
