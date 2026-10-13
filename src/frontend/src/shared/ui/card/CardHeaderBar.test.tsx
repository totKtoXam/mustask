import { describe, expect, it } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import { px, rgb, rulesFor, theme } from '../../../test/kitTesting';
import { IconAction } from '../actions/IconAction';
import { InlineTextField } from '../fields/InlineTextField';
import { ThinRefreshIcon, ThinUndoIcon } from '../icons/ThinIcons';
import { CardHeaderBar, type CardHeaderBarProps } from './CardHeaderBar';

// tech-015 design § 5.9 «CardHeaderBar»: the type's bar, then three rows of 32 — the type
// line (the heading), the identifier with the title, the assignee with the actions — and
// 4 + 32 + 8 + 32 + 8 + 32 + 8 = 124.

function renderBar(props: Partial<CardHeaderBarProps> = {}) {
  return render(
    <CardHeaderBar
      appearance="story"
      typeLabel="Пользовательская история"
      identifier="MSK-US-2"
      headingLevel={2}
      title={
        <InlineTextField
          label="Заголовок"
          labelPlacement="hidden"
          size="title"
          value="Письмо со ссылкой"
          onChange={() => {}}
        />
      }
      meta={<span>Анна Смирнова</span>}
      actions={
        <>
          <IconAction label="Обновить" icon={<ThinRefreshIcon />} />
          <IconAction label="Отменить изменения" icon={<ThinUndoIcon />} />
        </>
      }
      {...props}
    />,
  );
}

function header(container: HTMLElement) {
  return container.querySelector('[data-card-header]') as HTMLElement;
}

describe('tech-015 Scenario: CardHeaderBar (design § 5.9)', () => {
  describe('Scenario: the heading is the type line «<Тип> <ID>»', () => {
    it.each([1, 2, 3] as const)('is an h%s named «Пользовательская история MSK-US-2»', (level) => {
      renderBar({ headingLevel: level });
      expect(
        screen.getByRole('heading', { level, name: 'Пользовательская история MSK-US-2' }),
      ).toBeInTheDocument();
      expect(screen.getAllByRole('heading')).toHaveLength(1);
    });

    it('keeps the case in the DOM text and the accessible name, and capitalises by CSS alone', () => {
      renderBar();
      const heading = screen.getByRole('heading', { name: 'Пользовательская история MSK-US-2' });
      expect(heading).toHaveTextContent('Пользовательская история MSK-US-2');
      const eyebrow = within(heading).getByText('Пользовательская история MSK-US-2');
      expect(getComputedStyle(eyebrow.parentElement as HTMLElement).textTransform).toBe(
        'uppercase',
      );
    });

    it('is one text, in primary.dark, with the type’s icon before it', () => {
      renderBar();
      const heading = screen.getByRole('heading');
      const line = heading.firstElementChild as HTMLElement;
      expect(getComputedStyle(line).color).toBe(rgb(theme.palette.primary.dark));
      expect(line.querySelector('svg')).toHaveAttribute('aria-hidden', 'true');
    });

    it('carries the id the dialog points at', () => {
      renderBar({ headingId: 'card-heading' });
      expect(screen.getByRole('heading')).toHaveAttribute('id', 'card-heading');
    });
  });

  describe('Scenario: the type’s bar stands at the left over the header’s height only', () => {
    it('is the first child, layout.card.typeBar wide, in the type’s colour', () => {
      const { container } = renderBar({ appearance: 'bug' });
      const bar = header(container).firstElementChild as HTMLElement;
      expect(bar).toHaveAttribute('data-type-stripe', 'card');
      expect(px(getComputedStyle(bar).width)).toBe(theme.layout.card.typeBar);
      expect(getComputedStyle(bar).backgroundColor).toBe(
        rgb(theme.workTracking.typeAppearance('bug').colour),
      );
    });

    it('starts its content layout.card.headerInset from the left edge, bar included', () => {
      const { container } = renderBar();
      const content = header(container).children[1] as HTMLElement;
      const style = getComputedStyle(content);
      expect(theme.layout.card.typeBar + px(style.paddingLeft)).toBe(theme.layout.card.headerInset);
      expect(px(style.paddingRight)).toBe(theme.layout.card.rightInset);
    });
  });

  describe('Scenario: three rows, 4 + 32 + 8 + 32 + 8 + 32 + 8 = 124', () => {
    it('pads the top by headerPadTop and the foot by headerPadBottom, rows headerRowGap apart', () => {
      const { container } = renderBar();
      const content = header(container).children[1] as HTMLElement;
      const style = getComputedStyle(content);
      expect(px(style.paddingTop)).toBe(theme.layout.card.headerPadTop);
      expect(px(style.paddingBottom)).toBe(theme.layout.card.headerPadBottom);
      expect(px(style.rowGap)).toBe(theme.layout.card.headerRowGap);
      expect(content.children).toHaveLength(3);
    });

    it('gives each row density.controlHeight', () => {
      const { container } = renderBar();
      const content = header(container).children[1] as HTMLElement;
      for (const row of Array.from(content.children)) {
        expect(px(getComputedStyle(row).minHeight)).toBe(theme.density.controlHeight);
      }
      const { headerPadTop, headerRowGap, headerPadBottom } = theme.layout.card;
      expect(
        headerPadTop + 3 * theme.density.controlHeight + 2 * headerRowGap + headerPadBottom,
      ).toBe(124);
    });
  });

  describe('Scenario: a field that stands alone in a row does not make it taller', () => {
    it('takes back the gap a field keeps under its box in the title slot and in the meta slot', () => {
      const { container } = renderBar();
      const identifier = container.querySelector('[data-card-identifier]') as HTMLElement;
      const slots = [
        (identifier.parentElement as HTMLElement).lastElementChild,
        container.querySelector('[data-card-meta]'),
      ];
      for (const slot of slots) {
        const rule = rulesFor(slot as Element, '[data-field]').find(
          (candidate) => candidate.declarations['margin-bottom'],
        );
        expect(rule?.declarations['margin-bottom']).toBe(`-${theme.layout.field.gap}px`);
      }
    });
  });

  describe('Scenario: row 2 is the identifier, a 16 px gap and the title', () => {
    it('shows the identifier in the card title type, not a link, without wrapping', () => {
      const { container } = renderBar();
      const identifier = container.querySelector('[data-card-identifier]') as HTMLElement;
      expect(identifier).toHaveTextContent('MSK-US-2');
      expect(identifier.closest('a')).toBeNull();
      const style = getComputedStyle(identifier);
      expect(px(style.fontSize)).toBe(17);
      expect(style.fontWeight).toBe('400');
      expect(style.whiteSpace).toBe('nowrap');
      expect(style.flexShrink).toBe('0');
    });

    it('stands 16 px before the title the host passes, which fills the rest', () => {
      const { container } = renderBar();
      const identifier = container.querySelector('[data-card-identifier]') as HTMLElement;
      const row = identifier.parentElement as HTMLElement;
      expect(px(getComputedStyle(row).gap)).toBe(16);
      expect(screen.getByRole('textbox', { name: 'Заголовок' })).toHaveValue('Письмо со ссылкой');
      expect(row.lastElementChild).toContainElement(screen.getByRole('textbox'));
    });
  });

  describe('Scenario: row 3 holds the meta at the left and the actions at the right', () => {
    it('draws the meta and the actions in that order', () => {
      const { container } = renderBar();
      const meta = container.querySelector('[data-card-meta]') as HTMLElement;
      const actions = container.querySelector('[data-card-actions]') as HTMLElement;
      expect(meta).toHaveTextContent('Анна Смирнова');
      expect(
        within(actions)
          .getAllByRole('button')
          .map((b) => b.getAttribute('aria-label')),
      ).toEqual(['Обновить', 'Отменить изменения']);
      expect(meta.compareDocumentPosition(actions) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    });

    it('puts layout.card.actionGap between the actions, so the 32 px boxes have a pitch of 43', () => {
      const { container } = renderBar();
      const actions = container.querySelector('[data-card-actions]') as HTMLElement;
      expect(px(getComputedStyle(actions).gap)).toBe(theme.layout.card.actionGap);
      expect(theme.layout.iconAction.bar.box + theme.layout.card.actionGap).toBe(43);
      expect(getComputedStyle(actions).flexShrink).toBe('0');
    });

    it('lets the meta fill and shrink', () => {
      const { container } = renderBar();
      const meta = container.querySelector('[data-card-meta]') as HTMLElement;
      expect(parseFloat(getComputedStyle(meta).minWidth)).toBe(0);
      // jsdom does not expand the `flex` shorthand: the stylesheet's rule is the answer.
      const declared = rulesFor(meta, '').find((rule) => rule.declarations.flex);
      expect(declared?.declarations.flex).toMatch(/^1 1 0/);
    });

    it('draws no actions area at all when there are no actions', () => {
      const { container } = renderBar({ actions: undefined });
      expect(container.querySelector('[data-card-actions]')).toBeNull();
    });

    it('keeps the meta slot, so row 3 holds its place when nothing is assigned yet', () => {
      const { container } = renderBar({ meta: undefined });
      expect(container.querySelector('[data-card-meta]')).toBeInTheDocument();
    });
  });

  describe('Scenario: below layout.card.stackBelow row 3 wraps', () => {
    it('wraps by a rule under the card container’s narrow query', () => {
      const { container } = renderBar();
      const row = container.querySelector('[data-card-header-row="3"]') as HTMLElement;
      const narrow = rulesFor(row, '').find((rule) => rule.media.includes('max-width'));
      expect(narrow?.media).toContain('kit-card');
      expect(narrow?.media).toContain(`${theme.layout.card.stackBelow - 0.02}px`);
      expect(narrow?.declarations['flex-wrap']).toBe('wrap');
    });

    it('does not wrap in the wide layout', () => {
      const { container } = renderBar();
      const row = container.querySelector('[data-card-header-row="3"]') as HTMLElement;
      expect(getComputedStyle(row).flexWrap).not.toBe('wrap');
    });
  });

  describe('Scenario: the dialog’s two icons stand over the type line, not under its text', () => {
    it('leaves room at the heading’s end only when a dialog has set how far its icons reach', () => {
      renderBar();
      const heading = screen.getByRole('heading');
      const padding = rulesFor(heading, '').find(
        (rule) => rule.declarations['padding-right'] !== undefined,
      );
      expect(padding?.declarations['padding-right']).toBe('var(--kit-card-icons, 0px)');
    });
  });

  describe('Scenario: no dialog icon is the bar’s', () => {
    it('draws no «Развернуть на страницу» and no «Закрыть»: they are the shell’s', () => {
      renderBar();
      expect(screen.queryByRole('button', { name: 'Развернуть на страницу' })).toBeNull();
      expect(screen.queryByRole('button', { name: 'Закрыть' })).toBeNull();
    });
  });
});
