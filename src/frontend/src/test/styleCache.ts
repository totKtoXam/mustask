import createCache, { type EmotionCache } from '@emotion/cache';

/**
 * The emotion cache every test render shares (tech-028).
 *
 * `speedy: true` makes emotion write rules through `CSSStyleSheet.insertRule` into one
 * sheet instead of appending a `<style>` text node per rule, which is what a component
 * test pays for in jsdom. The rules stay readable through the CSSOM
 * (`document.styleSheets`, `kitTesting.ts` → `rulesFor`); a test must not read a `<style>`
 * tag's text, it is empty in this mode. The key stays `css`: the `css-<hash>` class names
 * the kit's tests look for do not change.
 *
 * One cache per test, dropped in `setup.ts` after the test: jsdom cascades by the order
 * rules were inserted, so a cache that outlived its test would hand the next one a
 * different order than a fresh document did.
 */
let current: EmotionCache | null = null;

export function styleCache(): EmotionCache {
  current ??= createCache({ key: 'css', speedy: true });
  return current;
}

/** Removes the test's rules from the document and forgets the cache. */
export function resetStyleCache(): void {
  current?.sheet.flush();
  current = null;
}
