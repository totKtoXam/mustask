import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { px, rulesFor, theme } from '../../../test/kitTesting';
import { ColumnLayout } from './ColumnLayout';

// tech-015 design § 5.8 «ColumnLayout»: the body of a card in three, two or one column by
// the container's own width. jsdom evaluates no container query, so the tracks are read as
// the rules the stylesheet holds; the measured columns and the 32 px between sections are
// the showcase E2E's (28.7).

function container() {
  return screen.getByTestId('columns').querySelector('[data-column-layout]') as HTMLElement;
}

function grid() {
  return container().querySelector('[data-column-grid]') as HTMLElement;
}

function areas() {
  return Array.from(grid().children).map((child) => (child as HTMLElement).dataset.area);
}

/** The declarations the grid gets at a container query whose condition has `condition` in it. */
function underQuery(condition: string) {
  const rule = rulesFor(grid(), '').find((candidate) => candidate.media.includes(condition));
  return rule?.declarations;
}

function renderColumns(props: Partial<Parameters<typeof ColumnLayout>[0]> = {}) {
  return render(
    <div data-testid="columns">
      <ColumnLayout
        main={<p>Тексты</p>}
        side={<p>Свойства</p>}
        extra={<p>Связанная работа</p>}
        {...props}
      />
    </div>,
  );
}

describe('tech-015 Scenario: ColumnLayout (design § 5.8)', () => {
  describe('Scenario: only the columns that are given are drawn, in the DOM order main, side, extra', () => {
    it('draws three areas in that order', () => {
      renderColumns();
      expect(areas()).toEqual(['main', 'side', 'extra']);
      expect(grid()).toHaveTextContent('ТекстыСвойстваСвязанная работа');
    });

    it('draws no area, and no track, for a column with no content', () => {
      renderColumns({ extra: undefined });
      expect(areas()).toEqual(['main', 'side']);
      renderColumns({ side: null, extra: false });
    });

    it('draws main alone when side and extra are absent', () => {
      renderColumns({ side: undefined, extra: undefined });
      expect(areas()).toEqual(['main']);
      expect(underQuery('min-width')).toBeUndefined();
    });

    it('draws extra in the second track when side is absent', () => {
      renderColumns({ side: undefined });
      expect(areas()).toEqual(['main', 'extra']);
      const wide = underQuery(`min-width: ${theme.layout.columns.twoMin}px`);
      expect(wide?.['grid-template-areas']).toBe('"main aside1"');
    });
  });

  describe('Scenario: an outer padded container and a grid that is its child', () => {
    it('declares the container on the outer element and never on the grid', () => {
      renderColumns();
      const outer = getComputedStyle(container());
      expect(outer.containerType).toBe('inline-size');
      expect(getComputedStyle(grid()).containerType).toBe('');
      expect(grid().parentElement).toBe(container());
      expect(getComputedStyle(grid()).display).toBe('grid');
      expect(getComputedStyle(container()).display).not.toBe('grid');
    });

    it('carries columns.top above, gutter at the sides and bottom below', () => {
      renderColumns();
      const style = getComputedStyle(container());
      expect(px(style.paddingTop)).toBe(theme.layout.columns.top);
      expect(px(style.paddingLeft)).toBe(theme.layout.columns.gutter);
      expect(px(style.paddingRight)).toBe(theme.layout.columns.gutter);
      expect(px(style.paddingBottom)).toBe(theme.layout.columns.bottom);
    });

    it('names its container, so the rules query this one and no other', () => {
      renderColumns();
      const declared = rulesFor(container(), '').find(
        (rule) => rule.declarations['container-name'],
      );
      expect(declared?.declarations['container-name']).toBe('kit-columns');
    });
  });

  describe('Scenario: each area is a flex column that spaces its sections by layout.section.gap', () => {
    it.each(['main', 'side', 'extra'] as const)('the %s area', (name) => {
      renderColumns();
      const area = grid().querySelector(`[data-area="${name}"]`) as HTMLElement;
      const style = getComputedStyle(area);
      expect(style.display).toBe('flex');
      expect(style.flexDirection).toBe('column');
      expect(px(style.gap)).toBe(theme.layout.section.gap);
      expect(parseFloat(style.minWidth)).toBe(0);
    });
  });

  describe('Scenario: one column below, two from twoMin, three from threeMin', () => {
    it('stacks the three areas in one column with section.gap between them', () => {
      renderColumns();
      const style = getComputedStyle(grid());
      expect(style.gridTemplateColumns).toBe('minmax(0, 1fr)');
      expect(style.gridTemplateAreas).toBe('"main" "aside1" "aside2"');
      expect(px(style.rowGap)).toBe(theme.layout.section.gap);
      expect(px(style.columnGap)).toBe(theme.layout.columns.gap);
    });

    it('is two columns from the container’s twoMin: main spans both rows, extra follows side', () => {
      renderColumns();
      const wide = underQuery(`min-width: ${theme.layout.columns.twoMin}px`);
      expect(wide).toMatchObject({
        'grid-template-columns': `minmax(0, 1fr) ${theme.layout.columns.sideWidth}px`,
        'grid-template-rows': 'auto 1fr',
        'grid-template-areas': '"main aside1" "main aside2"',
      });
    });

    it('is three columns from the container’s threeMin: minmax(0,1fr) 320 320', () => {
      renderColumns();
      const wide = underQuery(`min-width: ${theme.layout.columns.threeMin}px`);
      const side = `${theme.layout.columns.sideWidth}px`;
      expect(wide).toMatchObject({
        'grid-template-columns': `minmax(0, 1fr) ${side} ${side}`,
        'grid-template-areas': '"main aside1 aside2"',
      });
    });

    it('reads the thresholds from the host when it overrides them', () => {
      renderColumns({ twoMin: 500, threeMin: 900 });
      expect(underQuery('min-width: 500px')).toBeDefined();
      expect(underQuery('min-width: 900px')).toBeDefined();
      expect(underQuery(`min-width: ${theme.layout.columns.twoMin}px`)).toBeUndefined();
    });

    it('has only two columns, never three, when it has one side area', () => {
      renderColumns({ extra: undefined });
      expect(underQuery(`min-width: ${theme.layout.columns.threeMin}px`)).toBeUndefined();
      expect(underQuery(`min-width: ${theme.layout.columns.twoMin}px`)).toMatchObject({
        'grid-template-areas': '"main aside1"',
      });
    });
  });

  describe('Scenario: the thresholds leave the first column room (design § 3.3)', () => {
    it('keeps at least 368 px for main at three columns, inside the gutters', () => {
      const { threeMin, sideWidth, gap } = theme.layout.columns;
      expect(threeMin - 2 * sideWidth - 2 * gap).toBeGreaterThanOrEqual(360);
    });
  });
});
