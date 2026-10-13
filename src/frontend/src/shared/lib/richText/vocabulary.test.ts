import { readdirSync, readFileSync, statSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import {
  CODE_LANGUAGES,
  CODE_LANGUAGE_PLAIN,
  FONT_SIZES,
  HIGHLIGHTS,
  MARK_ORDER,
  TEXT_COLOURS,
  firstToken,
  tokenByName,
} from './vocabulary';

describe('vocabulary (contracts/rich-text/vocabularies.json)', () => {
  it('holds the closed palettes of BR-RT-14 and the 12 languages of BR-RT-16', () => {
    expect(TEXT_COLOURS).toHaveLength(8);
    expect(HIGHLIGHTS).toHaveLength(8);
    expect(FONT_SIZES.map((token) => token.name)).toEqual(['xs', 'sm', 'lg', 'xl']);
    expect(CODE_LANGUAGES).toHaveLength(12);
    expect(CODE_LANGUAGE_PLAIN).toBe('plain');
  });

  it('keeps the canonical mark order outermost first (BR-RT-15)', () => {
    expect(MARK_ORDER).toEqual([
      'link',
      'bold',
      'italic',
      'underline',
      'strike',
      'highlight',
      'textColor',
      'fontSize',
    ]);
  });

  it('reads the first valid token in attribute order, on ASCII whitespace only', () => {
    const [red, blue] = [
      TEXT_COLOURS.find((t) => t.name === 'red'),
      TEXT_COLOURS.find((t) => t.name === 'blue'),
    ];
    expect(red && blue).toBeTruthy();

    expect(firstToken(TEXT_COLOURS, `evil ${red?.class} ${blue?.class}`)).toBe(red);
    expect(firstToken(TEXT_COLOURS, `${blue?.class}\t${red?.class}`)).toBe(blue);
    expect(firstToken(TEXT_COLOURS, `  ${red?.class}\n`)).toBe(red);
    expect(firstToken(TEXT_COLOURS, (red?.class as string).toUpperCase())).toBeNull();
    expect(firstToken(TEXT_COLOURS, `${red?.class}\u00a0x`)).toBeNull();
    expect(firstToken(TEXT_COLOURS, `${red?.class}-dark`)).toBeNull();
    expect(firstToken(TEXT_COLOURS, null)).toBeNull();
    expect(firstToken(TEXT_COLOURS, '')).toBeNull();
  });

  it('finds a token by name', () => {
    expect(tokenByName(HIGHLIGHTS, 'yellow')?.name).toBe('yellow');
    expect(tokenByName(HIGHLIGHTS, 'chartreuse')).toBeNull();
    expect(tokenByName(HIGHLIGHTS, null)).toBeNull();
  });
});

/**
 * ADR-0027 decision 6: neither side hand-writes a token. No class literal of
 * the vocabulary appears in product code outside `vocabulary.ts` and the theme
 * table (`app/theme`), which is keyed by the imported class names.
 */
describe('no token literal outside the vocabulary', () => {
  const SRC = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..');
  const PATTERN = /rt-color-|rt-hl-|rt-size-|language-/;

  function sources(directory: string): string[] {
    const result: string[] = [];
    for (const entry of readdirSync(directory)) {
      const full = path.join(directory, entry);
      if (statSync(full).isDirectory()) {
        result.push(...sources(full));
      } else if (/\.(ts|tsx)$/.test(entry) && !/\.test\.(ts|tsx)$/.test(entry)) {
        result.push(full);
      }
    }
    return result;
  }

  it('is true of the product sources', () => {
    const offenders = sources(SRC)
      .filter((file) => {
        const relative = path.relative(SRC, file).replaceAll('\\', '/');
        return (
          relative !== 'shared/lib/richText/vocabulary.ts' &&
          !relative.startsWith('app/theme/') &&
          !relative.startsWith('test/')
        );
      })
      .filter((file) => PATTERN.test(readFileSync(file, 'utf8')))
      .map((file) => path.relative(SRC, file).replaceAll('\\', '/'));

    expect(offenders).toEqual([]);
  });
});
