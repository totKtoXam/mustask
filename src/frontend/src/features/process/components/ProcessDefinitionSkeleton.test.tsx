import { describe, expect, it } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import { TYPE_NAMES_IN_ORDER } from '../testFixtures';
import { ProcessDefinitionSkeleton } from './ProcessDefinitionSkeleton';

/** The `@media (prefers-reduced-motion: reduce)` rules of the document's style sheets. */
function reducedMotionRules(): CSSStyleRule[] {
  const rules: CSSStyleRule[] = [];
  for (const sheet of Array.from(document.styleSheets)) {
    for (const rule of Array.from(sheet.cssRules)) {
      if (
        rule instanceof CSSMediaRule &&
        /prefers-reduced-motion:\s*reduce/.test(rule.conditionText)
      ) {
        rules.push(...Array.from(rule.cssRules).filter((inner) => inner instanceof CSSStyleRule));
      }
    }
  }
  return rules;
}

describe('ProcessDefinitionSkeleton — loading in the canon (design § States, mod-015 BR-12)', () => {
  it('is one status «Загрузка данных» of five cards, four placeholder rows each under the real headers', () => {
    const { container } = render(<ProcessDefinitionSkeleton />);

    const status = screen.getByRole('status', { name: 'Загрузка данных' });
    expect(screen.getAllByRole('status')).toEqual([status]);
    expect(container.querySelectorAll('.MuiPaper-root')).toHaveLength(5);
    const tables = within(status).getAllByRole('table');
    expect(tables).toHaveLength(5);
    for (const table of tables) {
      expect(
        within(table)
          .getAllByRole('columnheader')
          .map((cell) => cell.textContent),
      ).toEqual(['Порядок', 'Статус', 'Категория статуса']);
      expect(within(table).getAllByRole('row')).toHaveLength(1 + 4);
    }
    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
  });

  it('shows no part of a process', () => {
    render(<ProcessDefinitionSkeleton />);

    for (const name of TYPE_NAMES_IN_ORDER) {
      expect(screen.queryByText(name)).not.toBeInTheDocument();
    }
    expect(screen.queryByRole('heading')).not.toBeInTheDocument();
  });

  it('stops the pulse under prefers-reduced-motion: reduce, through the theme (design § Motion)', () => {
    const { container } = render(<ProcessDefinitionSkeleton />);
    const skeleton = container.querySelector('.MuiSkeleton-root') as HTMLElement;
    const emotionClasses = Array.from(skeleton.classList).filter((name) => name.startsWith('css-'));

    const stopping = reducedMotionRules().filter(
      (rule) =>
        emotionClasses.some((name) => rule.selectorText.includes(name)) &&
        rule.style.animation === 'none',
    );

    expect(emotionClasses.length).toBeGreaterThan(0);
    expect(stopping.length).toBeGreaterThan(0);
  });
});
