/**
 * WCAG 2.1 contrast ratio (BR-5), used by the theme's contrast test.
 * https://www.w3.org/TR/WCAG21/#dfn-contrast-ratio
 */

function channels(hex: string): [number, number, number] {
  const digits = hex.replace(/^#/, '');
  const full =
    digits.length === 3
      ? digits
          .split('')
          .map((digit) => digit + digit)
          .join('')
      : digits;
  if (!/^[0-9a-fA-F]{6}$/.test(full)) {
    throw new Error(`Not a #RGB or #RRGGBB colour: ${hex}`);
  }
  return [0, 2, 4].map((offset) => parseInt(full.slice(offset, offset + 2), 16) / 255) as [
    number,
    number,
    number,
  ];
}

function linear(channel: number): number {
  return channel <= 0.03928 ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4;
}

/** Relative luminance of an sRGB colour given as `#RGB` or `#RRGGBB`. */
export function relativeLuminance(hex: string): number {
  const [red, green, blue] = channels(hex).map(linear) as [number, number, number];
  return 0.2126 * red + 0.7152 * green + 0.0722 * blue;
}

/** The contrast ratio of two colours, from 1 to 21; symmetric in its arguments. */
export function contrastRatio(foreground: string, background: string): number {
  const first = relativeLuminance(foreground);
  const second = relativeLuminance(background);
  const lighter = Math.max(first, second);
  const darker = Math.min(first, second);
  return (lighter + 0.05) / (darker + 0.05);
}
