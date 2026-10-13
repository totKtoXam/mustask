import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { px, rgb, rulesFor, theme } from '../../../test/kitTesting';
import { PropertyBand } from './PropertyBand';

// tech-015 design § 5.9 «PropertyBand»: the grey strip under the header — two fixed blocks of
// 253 (the reference's pitch is 253 + 32), a free track, the aside with the stamp and the
// tabs; it keeps 76 px with its rows empty. jsdom evaluates no container query, so the
// narrow layout is read as the rule the stylesheet holds.

function band(container: HTMLElement) {
  return container.querySelector('[data-property-band]') as HTMLElement;
}

function grid(container: HTMLElement) {
  return band(container).firstElementChild as HTMLElement;
}

describe('tech-015 Scenario: PropertyBand (design § 5.9)', () => {
  describe('Scenario: a grey strip, never coloured', () => {
    it('is the application background, flat, with no shadow', () => {
      const { container } = render(<PropertyBand blocks={[<p key="a">Статус</p>]} />);
      const style = getComputedStyle(band(container));
      expect(style.backgroundColor).toBe(rgb(theme.palette.background.default));
      expect(style.boxShadow).toBe('none');
    });
  });

  describe('Scenario: four grid tracks, the blocks fixed', () => {
    it('is minmax(bandBlockMin, bandBlockWidth) twice, a free track and an auto aside', () => {
      const { container } = render(
        <PropertyBand blocks={[<p key="a">Статус</p>, <p key="b">Область</p>]} />,
      );
      const { bandBlockMin, bandBlockWidth, bandBlockGap } = theme.layout.card;
      const block = `minmax(${bandBlockMin}px, ${bandBlockWidth}px)`;
      const style = getComputedStyle(grid(container));
      expect(style.display).toBe('grid');
      expect(style.gridTemplateColumns).toBe(`${block} ${block} minmax(0, 1fr) auto`);
      expect(px(style.columnGap)).toBe(bandBlockGap);
    });

    it('never gives the room left to the blocks: no 1fr block track', () => {
      const { container } = render(
        <PropertyBand blocks={[<p key="a">Статус</p>, <p key="b">Область</p>]} />,
      );
      const tracks = getComputedStyle(grid(container)).gridTemplateColumns;
      expect(tracks.match(/1fr/g)).toHaveLength(1);
      expect(tracks).toContain('minmax(0, 1fr)');
    });

    it('draws no second block track when it is given one block', () => {
      const { container } = render(<PropertyBand blocks={[<p key="a">Статус</p>]} />);
      const { bandBlockMin, bandBlockWidth } = theme.layout.card;
      expect(getComputedStyle(grid(container)).gridTemplateColumns).toBe(
        `minmax(${bandBlockMin}px, ${bandBlockWidth}px) minmax(0, 1fr) auto`,
      );
      expect(container.querySelector('[data-band-block="2"]')).toBeNull();
    });

    it('puts the pitch of the blocks at 285 and block B’s label 303 from the card’s edge', () => {
      const { bandBlockWidth, bandBlockGap, headerInset } = theme.layout.card;
      expect(bandBlockWidth + bandBlockGap).toBe(285);
      expect(headerInset + bandBlockWidth + bandBlockGap).toBe(303);
    });

    it('places the blocks in tracks 1 and 2 and the aside after the free track', () => {
      const { container } = render(
        <PropertyBand blocks={[<p key="a">Статус</p>, <p key="b">Область</p>]} />,
      );
      expect(
        getComputedStyle(container.querySelector('[data-band-block="1"]') as Element).gridColumn,
      ).toBe('1');
      expect(
        getComputedStyle(container.querySelector('[data-band-block="2"]') as Element).gridColumn,
      ).toBe('2');
      expect(
        getComputedStyle(container.querySelector('[data-band-aside]') as Element).gridColumn,
      ).toBe('4');
    });
  });

  describe('Scenario: it keeps its height with its rows empty', () => {
    it('has the least height of layout.card.bandMinHeight and the reference’s padding', () => {
      const { container } = render(<PropertyBand blocks={[<span key="a" />]} />);
      const style = getComputedStyle(grid(container));
      const { bandMinHeight, bandPaddingTop, rightInset, headerInset } = theme.layout.card;
      expect(px(style.minHeight)).toBe(bandMinHeight);
      expect(style.boxSizing).toBe('border-box');
      expect(px(style.paddingTop)).toBe(bandPaddingTop);
      expect(px(style.paddingRight)).toBe(rightInset);
      expect(style.paddingBottom).toBe('0px');
      expect(px(style.paddingLeft)).toBe(headerInset);
    });

    it('is tall enough for two rows of 32 and their gap (74 of 76)', () => {
      const { bandMinHeight, bandPaddingTop, bandRow, bandRowGap, tabStripHeight } =
        theme.layout.card;
      expect(bandMinHeight).toBeGreaterThanOrEqual(bandPaddingTop + 2 * bandRow + bandRowGap);
      expect(tabStripHeight).toBeLessThanOrEqual(bandMinHeight);
    });

    it('stacks the fields of a block bandRowGap apart', () => {
      const { container } = render(<PropertyBand blocks={[<p key="a">Статус</p>]} />);
      const block = container.querySelector('[data-band-block="1"]') as HTMLElement;
      const style = getComputedStyle(block);
      expect(style.flexDirection).toBe('column');
      expect(px(style.rowGap)).toBe(theme.layout.card.bandRowGap);
      expect(parseFloat(style.minWidth)).toBe(0);
    });
  });

  describe('Scenario: the aside holds the stamp on top and the tabs at the bottom', () => {
    it('renders the meta and the tabs, in the aside, right-aligned', () => {
      const { container } = render(
        <PropertyBand
          blocks={[<p key="a">Статус</p>]}
          meta="Изменён: 05.10.2026 14:30"
          tabs={<div role="tablist" aria-label="Разделы" />}
        />,
      );
      const aside = container.querySelector('[data-band-aside]') as HTMLElement;
      expect(aside).toHaveTextContent('Изменён: 05.10.2026 14:30');
      expect(screen.getByRole('tablist', { name: 'Разделы' })).toBeInTheDocument();
      expect(aside).toContainElement(screen.getByRole('tablist'));
      expect(getComputedStyle(aside).alignItems).toBe('flex-end');
    });

    it('draws the stamp in the caption type and the card’s label grey, cut by an ellipsis', () => {
      const { container } = render(
        <PropertyBand blocks={[<p key="a">Статус</p>]} meta="Изменён: 05.10.2026 14:30" />,
      );
      const meta = container.querySelector('[data-band-meta]') as HTMLElement;
      const style = getComputedStyle(meta);
      expect(style.color).toBe(rgb(theme.palette.shell.cardLabel));
      expect(px(style.fontSize)).toBe(12);
      expect(style.textOverflow).toBe('ellipsis');
      expect(style.whiteSpace).toBe('nowrap');
    });

    it('keeps the tab strip against the band’s lower edge and never shrinks it', () => {
      const { container } = render(
        <PropertyBand blocks={[<p key="a">Статус</p>]} tabs={<span>Детали</span>} />,
      );
      const tabs = container.querySelector('[data-band-tabs]') as HTMLElement;
      const style = getComputedStyle(tabs);
      expect(style.marginTop).toBe('auto');
      expect(style.flexShrink).toBe('0');
    });

    it('draws neither area when it has neither', () => {
      const { container } = render(<PropertyBand blocks={[<p key="a">Статус</p>]} />);
      expect(container.querySelector('[data-band-meta]')).toBeNull();
      expect(container.querySelector('[data-band-tabs]')).toBeNull();
    });
  });

  describe('Scenario: below layout.card.stackBelow the blocks stack and the minimum height goes', () => {
    it('is one column of at least bandBlockMin by a rule under the card container’s narrow query', () => {
      const { container } = render(
        <PropertyBand blocks={[<p key="a">Статус</p>, <p key="b">Область</p>]} />,
      );
      const narrow = rulesFor(grid(container), '').find((rule) => rule.media.includes('max-width'));
      expect(narrow?.media).toContain('kit-card');
      expect(narrow?.declarations['grid-template-columns']).toBe(
        `minmax(${theme.layout.card.bandBlockMin}px, 1fr)`,
      );
      expect(narrow?.declarations['min-height']).toBe('0');
    });
  });
});
