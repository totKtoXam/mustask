import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { px, rgb, rulesFor, theme } from '../../../test/kitTesting';
import { PanelShell } from './PanelShell';

// tech-015 design § 5.10 «PanelShell»: the same card on a page — the shell's trail above, one
// white panel on the application background, the `kit-card` container on the panel, and a
// page that scrolls, not the card.

function panel(container: HTMLElement) {
  return container.querySelector('[data-panel-shell]') as HTMLElement;
}

describe('tech-015 Scenario: PanelShell (design § 5.10)', () => {
  describe('Scenario: the slots of the dialog, in a surface', () => {
    it('stacks the header, the band, the alert and the body in that order', () => {
      const { container } = render(
        <PanelShell
          header={<div>Шапка</div>}
          band={<div>Полоса</div>}
          alert={<div role="status">Конфликт версий</div>}
        >
          <p>Тело</p>
        </PanelShell>,
      );
      expect(panel(container).textContent).toBe('ШапкаПолосаКонфликт версийТело');
    });

    it('draws only the slots it is given', () => {
      const { container } = render(
        <PanelShell>
          <p>Тело</p>
        </PanelShell>,
      );
      expect(panel(container).textContent).toBe('Тело');
    });
  });

  describe('Scenario: one white panel with the soft glow', () => {
    it('is the card surface: paper colour, theme.cardShadow, the theme’s corner', () => {
      const { container } = render(<PanelShell>Тело</PanelShell>);
      const style = getComputedStyle(panel(container));
      expect(style.backgroundColor).toBe(rgb(theme.palette.background.paper));
      expect(style.boxShadow.replace(/\s+/g, '')).toBe(theme.cardShadow.replace(/\s+/g, ''));
      expect(style.borderRadius).toBe(`${theme.shape.borderRadius}px`);
      expect(panel(container)).toHaveClass('MuiPaper-elevation0');
    });

    it('declares the kit-card container its header and band read', () => {
      const { container } = render(<PanelShell>Тело</PanelShell>);
      expect(getComputedStyle(panel(container)).containerType).toBe('inline-size');
      const declared = rulesFor(panel(container), '').find(
        (rule) => rule.declarations['container-name'],
      );
      expect(declared?.declarations['container-name']).toBe('kit-card');
    });
  });

  describe('Scenario: the page scrolls, not the card', () => {
    it('fixes nothing and scrolls nowhere: no inner scrollbar, no sticky header', () => {
      const { container } = render(
        <PanelShell header={<div>Шапка</div>} band={<div>Полоса</div>}>
          Тело
        </PanelShell>,
      );
      for (const element of [panel(container), ...Array.from(panel(container).children)]) {
        const style = getComputedStyle(element);
        expect(style.overflowY).not.toBe('auto');
        expect(style.overflowY).not.toBe('scroll');
        expect(style.position).not.toBe('sticky');
        expect(style.position).not.toBe('fixed');
      }
      expect(getComputedStyle(panel(container)).maxHeight).not.toMatch(/\d/);
    });

    it('has no «Развернуть на страницу» and no «Закрыть»: the trail leads back', () => {
      render(<PanelShell>Тело</PanelShell>);
      expect(screen.queryByRole('button', { name: 'Развернуть на страницу' })).toBeNull();
      expect(screen.queryByRole('button', { name: 'Закрыть' })).toBeNull();
    });
  });

  describe('Scenario: the space above is the shell’s', () => {
    it('leaves layout.pageHead.panelTop above the panel', () => {
      const { container } = render(<PanelShell>Тело</PanelShell>);
      const outer = panel(container).parentElement as HTMLElement;
      expect(px(getComputedStyle(outer).paddingTop)).toBe(theme.layout.pageHead.panelTop);
      expect(theme.layout.pageHead.panelTop).toBe(16);
    });

    it('draws the trail above the panel with layout.pageHead.trailGap under it', () => {
      const { container } = render(
        <PanelShell trail={<nav aria-label="Путь к странице">Бэклоги / MSK-US-2</nav>}>
          Тело
        </PanelShell>,
      );
      const trail = screen.getByRole('navigation', { name: 'Путь к странице' });
      expect(
        trail.compareDocumentPosition(panel(container)) & Node.DOCUMENT_POSITION_FOLLOWING,
      ).toBeTruthy();
      const slot = trail.parentElement as HTMLElement;
      expect(slot).toHaveAttribute('data-panel-trail');
      expect(px(getComputedStyle(slot).marginBottom)).toBe(theme.layout.pageHead.trailGap);
      expect(panel(container)).not.toContainElement(trail);
    });

    it('draws no trail slot when the shell wants the trail in the top bar', () => {
      const { container } = render(<PanelShell trail={null}>Тело</PanelShell>);
      expect(container.querySelector('[data-panel-trail]')).toBeNull();
    });
  });
});
