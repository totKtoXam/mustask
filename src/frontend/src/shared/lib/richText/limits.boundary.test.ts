import { describe, expect, it } from 'vitest';
import { checkDescription, normaliseRichText } from '.';

// feat-018 QA (frontend): US-1 "Over the visible limit", BR-RT-8 code-point counting at the boundary.
describe('visible limit counted in code points (US-1, BR-RT-8)', () => {
  it('100 000 emoji are within the limit, 100 001 are over it', () => {
    const at = normaliseRichText('<p>' + '😀'.repeat(100_000) + '</p>');
    const over = normaliseRichText('<p>' + '😀'.repeat(100_001) + '</p>');

    expect(at.visibleLength).toBe(100_000);
    expect(checkDescription(at)).toBe('ok');
    expect(over.visibleLength).toBe(100_001);
    expect(checkDescription(over)).toBe('too-long');
  });

  it('a ZWJ sequence counts each of its code points, no grapheme clustering', () => {
    // man + ZWJ + woman + ZWJ + girl = 5 code points
    expect(normaliseRichText('<p>👨‍👩‍👧</p>').visibleLength).toBe(5);
  });

  it('a lone surrogate counts 1 and is replaced by U+FFFD in the markup', () => {
    const result = normaliseRichText('<p>a\uD83Db</p>');

    expect(result.visibleLength).toBe(3);
    expect(result.html).toBe('<p>a�b</p>');
  });

  it('leaf blocks add one line feed between them, none around (a list item is a container)', () => {
    expect(normaliseRichText('<ul><li><p>a</p></li><li><p>b</p></li></ul>').visibleLength).toBe(3);
  });

  it('hostile class and style values never reach the output (BR-RT-14)', () => {
    const out = normaliseRichText(
      '<p><span class="RT-COLOR-RED rt-color-red evil" style="color:red">x</span></p>',
    ).html;

    expect(out).not.toMatch(/style=|evil|RT-COLOR/);
  });
});
