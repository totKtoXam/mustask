import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import { legacyToHtml } from './legacy';
import { normaliseRichText } from './normalise';

/**
 * The shared fixture files of `contracts/rich-text/fixtures/` run against the
 * client's normaliser and legacy conversion (plan-frontend § 4.6). The server
 * suite runs the same files over its reducer; the two implementations of the
 * canonical form meet here (BR-RT-5, BR-RT-8, US-7).
 */

function load<T>(name: string): { cases: T[] } {
  const file = path.resolve(
    path.dirname(fileURLToPath(import.meta.url)),
    '../../../../../../contracts/rich-text/fixtures',
    name,
  );
  return JSON.parse(readFileSync(file, 'utf8')) as { cases: T[] };
}

/** `{U+D83D}` stands for the UTF-16 code unit itself (a lone surrogate is not valid in every JSON reader). */
function withSurrogates(text: string): string {
  return text.replaceAll('{U+D83D}', '\ud83d');
}

interface CanonicalCase {
  id: string;
  input: string;
  canonical: string;
  editorOutput?: string;
}

describe('canonical.json', () => {
  const { cases } = load<CanonicalCase>('canonical.json');

  it('has cases', () => {
    expect(cases.length).toBeGreaterThan(40);
  });

  it.each(cases.map((c) => [c.id, c] as const))('%s: input reduces to canonical', (_id, c) => {
    expect(normaliseRichText(c.input).html).toBe(c.canonical);
  });

  it.each(cases.filter((c) => c.editorOutput !== undefined).map((c) => [c.id, c] as const))(
    '%s: the editor output reduces to canonical',
    (_id, c) => {
      expect(normaliseRichText(c.editorOutput as string).html).toBe(c.canonical);
    },
  );

  it.each(cases.map((c) => [c.id, c] as const))('%s: canonical is a fixed point', (_id, c) => {
    expect(normaliseRichText(c.canonical).html).toBe(c.canonical);
  });
});

interface VisibleCase {
  id: string;
  html: string;
  count: number;
  stored?: string;
}

describe('visible-count.json', () => {
  const { cases } = load<VisibleCase>('visible-count.json');

  it.each(cases.map((c) => [c.id, c] as const))('%s: counts BR-RT-8', (_id, c) => {
    const result = normaliseRichText(withSurrogates(c.html));

    expect(result.visibleLength).toBe(c.count);
  });

  it.each(cases.filter((c) => c.stored !== undefined).map((c) => [c.id, c] as const))(
    '%s: stores what the case says',
    (_id, c) => {
      expect(normaliseRichText(withSurrogates(c.html)).html).toBe(c.stored);
    },
  );
});

interface HostileCase {
  id: string;
  input: string;
  mustNotContain: string[];
}

describe('hostile-corpus.json', () => {
  const { cases } = load<HostileCase>('hostile-corpus.json');

  it.each(cases.map((c) => [c.id, c] as const))(
    '%s: nothing hostile survives and the result is stable',
    (_id, c) => {
      const { html } = normaliseRichText(c.input);
      const lower = html.toLowerCase();

      for (const needle of c.mustNotContain) {
        expect(lower).not.toContain(needle.toLowerCase());
      }
      expect(normaliseRichText(html).html).toBe(html);
    },
  );
});

interface LegacyCase {
  id: string;
  plain?: string;
  html?: string;
  count: number;
  generator?: string;
  markup?: number;
}

function generate(name: string): string {
  switch (name) {
    case 'repeat-amp-100000':
      return '&'.repeat(100_000);
    case 'lf-100000':
      return '\n'.repeat(100_000);
    default:
      throw new Error(`Unknown generator '${name}'.`);
  }
}

describe('legacy.json', () => {
  const { cases } = load<LegacyCase>('legacy.json');

  it.each(cases.filter((c) => c.generator === undefined).map((c) => [c.id, c] as const))(
    '%s: converts to the case html, a fixed point of the normaliser',
    (_id, c) => {
      const html = legacyToHtml(c.plain as string);
      expect(html).toBe(c.html);

      const result = normaliseRichText(html);
      expect(result.html).toBe(html);
      expect(result.visibleLength).toBe(c.count);
    },
  );

  it.each(cases.filter((c) => c.generator !== undefined).map((c) => [c.id, c] as const))(
    '%s: the worst case fits the ceilings',
    (_id, c) => {
      const html = legacyToHtml(generate(c.generator as string));
      const result = normaliseRichText(html);

      expect(result.markupLength).toBe(c.markup);
      expect(result.visibleLength).toBe(c.count);
      // A value of blank paragraphs is blank (BR-RT-6); otherwise the html is its own canonical form.
      if (result.hasContent) expect(result.html).toBe(html);
    },
  );
});
