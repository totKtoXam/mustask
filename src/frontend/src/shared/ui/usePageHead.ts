import { createElement, useLayoutEffect, type ReactElement } from 'react';
import { useDocumentTitle } from '../lib/documentTitle';
import { usePageChrome, type TrailSegment } from './pageChrome';
import { PageTrail } from './PageTrail';

export interface PageHeadOptions {
  /** The tab's title. Absent, the tab is left alone — the page is still loading. */
  title?: string;
  /** The page's own trail segments, as `PageHeader`'s prop. */
  ancestors?: readonly TrailSegment[];
  /** When the tab says something other than `title`. */
  documentTitle?: string;
}

const NO_ANCESTORS: readonly TrailSegment[] = [];

/**
 * A page's seam with the shell (tech-015 design § 5.6): the tab's title, the registration
 * of the head with the shell, and the trail's placement. `PageHeader` calls it; a page
 * that has no `PageHeader` — the work item card's — calls it itself and passes the
 * `trail` to `PanelShell`.
 *
 * The registration is keyed by the serialised `ancestors`, so a new array on every
 * render registers once. `trail` is the ready `PageTrail` when the shell asks for the
 * trail above the page (below 1024 px) and it is not empty, otherwise `null`: the top
 * bar draws it. The host places the element. Outside the shell (a test, the auth card)
 * the context is inert and `trail` is `null`.
 *
 * Its own file, so a file that exports a component never exports a hook as well
 * (`react-refresh/only-export-components`).
 */
export function usePageHead({ title, ancestors = NO_ANCESTORS, documentTitle }: PageHeadOptions): {
  trail: ReactElement | null;
} {
  useDocumentTitle(documentTitle ?? title);
  const { registerHeader, trail, trailPlacement } = usePageChrome();

  const ancestorsKey = JSON.stringify(ancestors);
  useLayoutEffect(
    () => registerHeader(JSON.parse(ancestorsKey) as TrailSegment[]),
    [registerHeader, ancestorsKey],
  );

  return {
    trail:
      trailPlacement === 'header' && trail.length > 0
        ? createElement(PageTrail, { segments: trail })
        : null,
  };
}
