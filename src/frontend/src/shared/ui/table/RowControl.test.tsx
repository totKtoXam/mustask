import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { px, rulesFor, theme } from '../../../test/kitTesting';
import { MenuButton } from '../actions/MenuButton';
import { ThinAddIcon, ThinMoreVerticalIcon } from '../icons/ThinIcons';
import { BodyRow } from './BodyRow';
import { Cell } from './Cell';
import { RowControl, type RowControlProps } from './RowControl';
import { TableCard } from './TableCard';

// tech-015 design § 5.11 «RowControl»: an `IconAction` of the row size that its row reveals — in
// the Tab order by default (`focus="tab"`), or left to a treegrid host (`focus="roving"`).

function renderControl(props: Partial<RowControlProps> = {}) {
  return render(
    <TableCard aria-label="Таблица" head={null}>
      <BodyRow data-testid="row">
        <Cell>
          <RowControl
            label="Добавить: Фича (MSK-EPIC-1)"
            icon={<ThinAddIcon />}
            tone="add"
            {...props}
          />
        </Cell>
      </BodyRow>
    </TableCard>,
  );
}

function control() {
  return screen.getByRole('button', { name: 'Добавить: Фича (MSK-EPIC-1)' });
}

describe('tech-015 Scenario: RowControl (design § 5.11)', () => {
  describe('Scenario: the row size', () => {
    it('is a 24 px box with a 16 px glyph, named for its row', () => {
      renderControl();
      const style = getComputedStyle(control());
      expect(px(style.width)).toBe(theme.layout.iconAction.row.box);
      expect(px(style.height)).toBe(theme.layout.iconAction.row.box);
      expect(px(style.fontSize)).toBe(theme.layout.iconAction.row.glyph);
      expect([theme.layout.iconAction.row.box, theme.layout.iconAction.row.glyph]).toEqual([
        24, 16,
      ]);
    });

    it('shows its name as a tooltip, like any icon action', async () => {
      const user = userEvent.setup();
      renderControl();
      await user.hover(control());
      expect(await screen.findByRole('tooltip')).toHaveTextContent('Добавить: Фича (MSK-EPIC-1)');
    });

    it('carries the attribute the row reveals it by', () => {
      renderControl();
      expect(control()).toHaveAttribute('data-row-control');
      const row = screen.getByTestId('row');
      const reveal = rulesFor(row, '[data-row-control]').find(
        (rule) => rule.declarations.opacity === '0',
      );
      expect(reveal).toBeDefined();
    });
  });

  describe('Scenario: focus="tab" — in the Tab order', () => {
    it('leaves tabIndex alone: the control is a stop of the Tab order even while it is transparent', async () => {
      const user = userEvent.setup();
      renderControl();
      expect(control()).not.toHaveAttribute('tabindex', '-1');
      await user.tab();
      expect(control()).toHaveFocus();
    });

    it('is `focus="tab"` by default', () => {
      renderControl({ focus: 'tab' });
      expect(control()).not.toHaveAttribute('tabindex', '-1');
    });
  });

  describe('Scenario: focus="roving" — the host owns the focus', () => {
    it('has tabIndex -1 unless the host passes one', () => {
      renderControl({ focus: 'roving' });
      expect(control()).toHaveAttribute('tabindex', '-1');
    });

    it('honours a tabIndex the host passes — the current row’s control gets 0', () => {
      renderControl({ focus: 'roving', tabIndex: 0 });
      expect(control()).toHaveAttribute('tabindex', '0');
    });

    it('does nothing about focus itself: it is focusable by the host’s own call', () => {
      renderControl({ focus: 'roving' });
      control().focus();
      expect(control()).toHaveFocus();
    });
  });

  describe('Scenario: the IconAction it is', () => {
    it('keeps `inactive` for a write in flight: aria-disabled, still focusable, the click ignored', async () => {
      const user = userEvent.setup();
      const onClick = vi.fn();
      renderControl({ inactive: true, onClick });
      expect(control()).toHaveAttribute('aria-disabled', 'true');
      await user.click(control());
      expect(onClick).not.toHaveBeenCalled();
      control().focus();
      expect(control()).toHaveFocus();
    });

    it('calls onClick', async () => {
      const user = userEvent.setup();
      const onClick = vi.fn();
      renderControl({ onClick });
      await user.click(control());
      expect(onClick).toHaveBeenCalledOnce();
    });

    it('is the trigger of a row’s menu: aria-expanded keeps it shown while the menu is open', async () => {
      const user = userEvent.setup();
      render(
        <TableCard aria-label="Таблица" head={null}>
          <BodyRow>
            <Cell>
              <MenuButton
                label="Ещё действия: MSK-EPIC-1"
                items={[{ label: 'Изменить родителя…' }]}
                trigger={(trigger) => (
                  <RowControl
                    label="Ещё действия: MSK-EPIC-1"
                    icon={<ThinMoreVerticalIcon />}
                    {...trigger}
                  />
                )}
              />
            </Cell>
          </BodyRow>
        </TableCard>,
      );
      const more = screen.getByRole('button', { name: 'Ещё действия: MSK-EPIC-1' });
      expect(more).toHaveAttribute('data-row-control');
      expect(more).toHaveAttribute('aria-expanded', 'false');
      await user.click(more);
      expect(more).toHaveAttribute('aria-expanded', 'true');
    });
  });
});
