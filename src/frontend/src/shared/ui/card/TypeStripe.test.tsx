import { describe, expect, it } from 'vitest';
import { render } from '@testing-library/react';
import { APPEARANCE_TOKENS } from '../../../app/theme/tokens';
import { px, rgb, theme } from '../../../test/kitTesting';
import { TypeStripe } from './TypeStripe';

// tech-015 design § 5.9 «TypeStripe»: the coloured bar of a work item surface — the card's
// header 10 px, the board's card 4 px — in the type's colour, never the only carrier.

function stripe(container: HTMLElement): HTMLElement {
  return container.querySelector('[data-type-stripe]') as HTMLElement;
}

describe('tech-015 Scenario: TypeStripe (design § 5.9)', () => {
  it('is the card’s bar, layout.card.typeBar wide, by default', () => {
    const { container } = render(<TypeStripe appearance="story" />);
    expect(stripe(container)).toHaveAttribute('data-type-stripe', 'card');
    expect(px(getComputedStyle(stripe(container)).width)).toBe(theme.layout.card.typeBar);
  });

  it('is the board card’s bar, layout.card.typeBarCompact wide, when compact', () => {
    const { container } = render(<TypeStripe appearance="story" size="compact" />);
    expect(px(getComputedStyle(stripe(container)).width)).toBe(theme.layout.card.typeBarCompact);
  });

  it.each(APPEARANCE_TOKENS)('wears the colour of the %s type’s stored token', (token) => {
    const { container } = render(<TypeStripe appearance={token} />);
    expect(getComputedStyle(stripe(container)).backgroundColor).toBe(
      rgb(theme.workTracking.typeAppearance(token).colour),
    );
  });

  it('wears the neutral fallback for a token the theme does not know, and never throws', () => {
    const { container } = render(<TypeStripe appearance="unknown-token" />);
    expect(getComputedStyle(stripe(container)).backgroundColor).toBe(
      rgb(theme.workTracking.typeAppearance('unknown-token').colour),
    );
  });

  it('is decoration: aria-hidden, since the type’s name stands in the type line', () => {
    const { container } = render(<TypeStripe appearance="bug" />);
    expect(stripe(container)).toHaveAttribute('aria-hidden', 'true');
    expect(container).not.toHaveTextContent(/\S/);
  });

  it('stretches to the height of the row it stands in and never shrinks', () => {
    const { container } = render(<TypeStripe appearance="epic" />);
    const style = getComputedStyle(stripe(container));
    expect(style.alignSelf).toBe('stretch');
    expect(style.flexShrink).toBe('0');
  });
});
