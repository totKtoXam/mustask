import { describe, expect, it } from 'vitest';
import { render } from '@testing-library/react';
import { BacklogIcon } from './BacklogIcon';

// feat-027 design § 3.2, moved to the kit by tech-015 (design § 5.1): the team head's glyph —
// three right-aligned bars, filled, with no numerals. The whole set of glyphs is in
// ThinIcons.test.tsx; this file is the glyph's own.

describe('tech-015 Scenario: BacklogIcon (design § 5.1)', () => {
  it('is an aria-hidden 24-unit svg named BacklogIcon', () => {
    const { container } = render(<BacklogIcon />);
    const svg = container.querySelector('svg[data-testid="BacklogIcon"]');
    expect(svg).not.toBeNull();
    expect(svg).toHaveAttribute('aria-hidden', 'true');
    expect(svg).toHaveAttribute('viewBox', '0 0 24 24');
  });

  it('draws three bars, 13, 18 and 12 units long, right-aligned, and no numerals', () => {
    const { container } = render(<BacklogIcon />);
    const path = container.querySelector('path');
    expect(path).toHaveAttribute('d', 'M8 6h13v2H8zM3 11h18v2H3zM9 16h12v2H9z');
    expect(container.querySelector('text')).toBeNull();
  });

  it('is a filled shape and takes the size its button gives it', () => {
    const { container } = render(<BacklogIcon fontSize="inherit" />);
    const path = container.querySelector('path');
    expect(path).not.toHaveAttribute('stroke');
    expect(container.querySelector('svg')?.getAttribute('class')).toContain(
      'MuiSvgIcon-fontSizeInherit',
    );
  });
});
