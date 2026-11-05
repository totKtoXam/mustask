import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { px, rgb, theme } from '../../../test/kitTesting';
import { Section, SectionHead } from './Section';

// tech-015 design § 5.8 «Section and SectionHead»: a region named by its heading, a rule
// in `shell.sectionRule`, 41 px from the head to the first field, and **no margin**.

const SIDES = ['marginTop', 'marginRight', 'marginBottom', 'marginLeft'] as const;

describe('tech-015 Scenario: Section (design § 5.8)', () => {
  describe('Scenario: a region named by its own heading', () => {
    it('is a section landmark named by its h2', () => {
      render(
        <Section title="Описание">
          <p>Текст</p>
        </Section>,
      );
      const region = screen.getByRole('region', { name: 'Описание' });
      expect(region.tagName).toBe('SECTION');
      const heading = screen.getByRole('heading', { level: 2, name: 'Описание' });
      expect(region).toHaveAttribute('aria-labelledby', heading.id);
      expect(region).toContainElement(screen.getByText('Текст'));
    });

    it('takes the level 3 on request and draws it in the h3 type', () => {
      render(
        <Section title="Планирование" headingLevel={3}>
          x
        </Section>,
      );
      const heading = screen.getByRole('heading', { level: 3, name: 'Планирование' });
      expect(heading.tagName).toBe('H3');
      expect(px(getComputedStyle(heading).fontSize)).toBe(14);
    });

    it('draws the level 2 in the section head’s 16 / 24 / 600', () => {
      render(<Section title="Описание">x</Section>);
      const style = getComputedStyle(screen.getByRole('heading', { name: 'Описание' }));
      expect(px(style.fontSize)).toBe(16);
      expect(style.fontWeight).toBe('600');
    });

    it('names two sections apart: each region points at its own heading', () => {
      render(
        <>
          <Section title="Один">a</Section>
          <Section title="Два">b</Section>
        </>,
      );
      const [one, two] = screen.getAllByRole('region');
      expect(one?.getAttribute('aria-labelledby')).not.toBe(two?.getAttribute('aria-labelledby'));
    });

    it('puts the count after the title, inside the heading, so the name carries it', () => {
      render(
        <Section title="Дочерние элементы" count={3}>
          x
        </Section>,
      );
      expect(screen.getByRole('heading', { name: 'Дочерние элементы (3)' })).toBeInTheDocument();
      expect(screen.getByRole('region', { name: 'Дочерние элементы (3)' })).toBeInTheDocument();
    });

    it('draws its actions to the right of the title', () => {
      render(
        <Section title="Связанная работа" actions={<button type="button">Добавить связь</button>}>
          x
        </Section>,
      );
      const action = screen.getByRole('button', { name: 'Добавить связь' });
      const heading = screen.getByRole('heading', { name: 'Связанная работа' });
      expect(
        heading.compareDocumentPosition(action) & Node.DOCUMENT_POSITION_FOLLOWING,
      ).toBeTruthy();
    });
  });

  describe('Scenario: it has no margin of its own', () => {
    it('has a computed margin of 0 on every side, so the area’s gap is the only 32', () => {
      render(
        <Section title="Описание">
          <p>Текст</p>
        </Section>,
      );
      const region = screen.getByRole('region', { name: 'Описание' });
      for (const side of SIDES) {
        expect(parseFloat(getComputedStyle(region)[side])).toBe(0);
      }
      const heading = screen.getByRole('heading', { name: 'Описание' });
      for (const side of SIDES) {
        expect(parseFloat(getComputedStyle(heading)[side])).toBe(0);
      }
    });
  });

  describe('Scenario: the head, the rule and the body', () => {
    it('draws the rule in shell.sectionRule, not in divider', () => {
      render(
        <Section title="Описание">
          <p>Текст</p>
        </Section>,
      );
      const head = screen.getByRole('heading', { name: 'Описание' }).parentElement as HTMLElement;
      const style = getComputedStyle(head);
      expect(style.borderBottomColor).toBe(rgb(theme.palette.shell.sectionRule));
      expect(style.borderBottomColor).not.toBe(rgb(theme.palette.divider));
      expect(parseFloat(style.borderBottomWidth)).toBe(theme.layout.dividerWidth);
      expect(parseFloat(style.paddingBottom)).toBe(theme.layout.section.headGap);
    });

    it('is 41 px from the head’s top to the first field: 24 + 12 + the rule + 4', () => {
      render(
        <Section title="Описание">
          <p data-testid="field">Текст</p>
        </Section>,
      );
      const heading = screen.getByRole('heading', { name: 'Описание' });
      const body = screen.getByTestId('field').parentElement as HTMLElement;
      const lineHeight = Math.round(
        parseFloat(theme.typography.h2.fontSize as string) *
          theme.typography.htmlFontSize *
          Number(theme.typography.h2.lineHeight),
      );
      expect(lineHeight).toBe(24);
      expect(
        lineHeight +
          theme.layout.section.headGap +
          parseFloat(getComputedStyle(heading.parentElement as HTMLElement).borderBottomWidth) +
          parseFloat(getComputedStyle(body).paddingTop),
      ).toBe(41);
    });

    it('insets the content by layout.section.inset at both sides', () => {
      render(
        <Section title="Описание">
          <p data-testid="field">Текст</p>
        </Section>,
      );
      const body = screen.getByTestId('field').parentElement as HTMLElement;
      const style = getComputedStyle(body);
      expect(parseFloat(style.paddingLeft)).toBe(theme.layout.section.inset);
      expect(parseFloat(style.paddingRight)).toBe(theme.layout.section.inset);
    });

    it('draws no rule when told not to', () => {
      render(
        <Section title="Описание" divider={false}>
          x
        </Section>,
      );
      const head = screen.getByRole('heading', { name: 'Описание' }).parentElement as HTMLElement;
      expect(getComputedStyle(head).borderBottomStyle).not.toBe('solid');
    });
  });
});

describe('tech-015 Scenario: SectionHead alone (design § 5.8)', () => {
  it('is the heading, its count, its actions and the rule — and no region', () => {
    render(
      <SectionHead
        title="Бэклог"
        headingLevel={3}
        count={2}
        actions={<button type="button">Ещё</button>}
        id="head-1"
      />,
    );
    expect(screen.getByRole('heading', { level: 3, name: 'Бэклог (2)' })).toHaveAttribute(
      'id',
      'head-1',
    );
    expect(screen.queryByRole('region')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Ещё' })).toBeInTheDocument();
  });
});
