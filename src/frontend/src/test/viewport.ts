/**
 * A controllable `window.matchMedia` for component tests (mod-015 plan
 * § 2.5, D-9).
 *
 * jsdom has no `matchMedia`, so MUI's `useMediaQuery` answers `false` to
 * every query and every shell test would land in the narrow layout. This
 * stub evaluates `(min-width: …)`, `(max-width: …)`, their `and`
 * combinations and `(prefers-reduced-motion: reduce)` against a width and a
 * motion preference the test sets. Changing either notifies the live
 * `MediaQueryList`s whose answer changed, so mounted components re-render the
 * way they would on a real resize — wrap the call in `act()`.
 *
 * Defaults: 1440 px wide, motion allowed. `src/test/setup.ts` resets them
 * before every test.
 */

export const DEFAULT_VIEWPORT_WIDTH = 1440;

let currentWidth = DEFAULT_VIEWPORT_WIDTH;
let reducedMotion = false;
const liveLists = new Set<StubMediaQueryList>();

function evaluateFeature(part: string): boolean {
  const trimmed = part.trim().toLowerCase();
  if (trimmed === 'all' || trimmed === 'screen' || trimmed === 'only screen') {
    return true;
  }
  const feature = /^\(\s*([a-z-]+)\s*(?::\s*([^)]+?))?\s*\)$/.exec(trimmed);
  if (!feature) {
    return false;
  }
  const [, name, value = ''] = feature;
  switch (name) {
    case 'min-width':
      return currentWidth >= parseFloat(value);
    case 'max-width':
      return currentWidth <= parseFloat(value);
    case 'prefers-reduced-motion':
      return value === 'reduce' ? reducedMotion : !reducedMotion;
    default:
      return false;
  }
}

/** Evaluates a media query against the stub's current width and motion preference. */
export function evaluateMediaQuery(query: string): boolean {
  return query
    .replace(/^@media\s*/i, '')
    .split(/\s+and\s+/i)
    .every(evaluateFeature);
}

class StubMediaQueryList extends EventTarget {
  readonly media: string;
  onchange: ((event: Event) => void) | null = null;
  private lastMatches: boolean;

  constructor(media: string) {
    super();
    this.media = media;
    this.lastMatches = evaluateMediaQuery(media);
  }

  get matches(): boolean {
    return evaluateMediaQuery(this.media);
  }

  addListener(listener: (event: Event) => void): void {
    this.addEventListener('change', listener);
  }

  removeListener(listener: (event: Event) => void): void {
    this.removeEventListener('change', listener);
  }

  notifyIfChanged(): void {
    const matches = this.matches;
    if (matches === this.lastMatches) {
      return;
    }
    this.lastMatches = matches;
    const event = Object.assign(new Event('change'), { matches, media: this.media });
    this.dispatchEvent(event);
    this.onchange?.(event);
  }
}

function notifyAll(): void {
  liveLists.forEach((list) => list.notifyIfChanged());
}

/** Installs the stub on `window`; a no-op outside a DOM environment. */
export function installMatchMedia(): void {
  if (typeof window === 'undefined') {
    return;
  }
  Object.defineProperty(window, 'matchMedia', {
    configurable: true,
    writable: true,
    value: (query: string) => {
      const list = new StubMediaQueryList(query);
      liveLists.add(list);
      return list as unknown as MediaQueryList;
    },
  });
}

/** Sets the viewport width the stub answers for, in CSS px. */
export function setViewportWidth(width: number): void {
  currentWidth = width;
  notifyAll();
}

/** Sets whether the stub answers `(prefers-reduced-motion: reduce)` as matching. */
export function setReducedMotion(reduce: boolean): void {
  reducedMotion = reduce;
  notifyAll();
}

/** Back to 1440 px with motion allowed; forgets every list from earlier tests. */
export function resetViewport(): void {
  currentWidth = DEFAULT_VIEWPORT_WIDTH;
  reducedMotion = false;
  liveLists.clear();
}
