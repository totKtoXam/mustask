import { describe, expect, it } from 'vitest';
import { normaliseRichText } from './normalise';

const ID = '0b9f6a2e-5c3d-4a7e-8f1b-2d4c6e8a0b1c';

/** feat-078 US-3: the image element in the client's normaliser (the shared fixtures are in fixtures.test.ts). */
describe('image element (US-3)', () => {
  it('writes a CR in alt as LF', () => {
    const { html } = normaliseRichText(`<img data-image-id="${ID}" alt="a&#13;b">`);
    expect(html).toBe(`<img data-image-id="${ID}" alt="a\nb">`);
    expect(normaliseRichText(html).html).toBe(html);
  });

  it('counts an image as content and adds no visible character', () => {
    const only = normaliseRichText(`<img data-image-id="${ID}" alt="">`);
    expect(only.hasContent).toBe(true);
    expect(only.visibleLength).toBe(0);

    const between = normaliseRichText(`<p>a</p><img data-image-id="${ID}" alt=""><p>b</p>`);
    expect(between.visibleLength).toBe(3);
  });

  it('cuts alt to 255 code points without splitting a pair', () => {
    const alt = '😀'.repeat(300);
    const { html } = normaliseRichText(`<img data-image-id="${ID}" alt="${alt}">`);
    expect(html).toBe(`<img data-image-id="${ID}" alt="${'😀'.repeat(255)}">`);
  });
});
