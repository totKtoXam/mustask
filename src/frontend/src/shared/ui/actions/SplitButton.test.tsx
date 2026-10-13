import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ThinSaveIcon } from '../icons/ThinIcons';
import { rgb, theme } from '../../../test/kitTesting';
import { SplitButton, type SplitButtonProps } from './SplitButton';

// tech-015 design § 5.2 «SplitButton»: group and arrow names, the thin chevron, the
// reference's clean disabled look, a busy button that keeps the tone's fill.

const ITEMS = [{ label: 'Сохранить и закрыть' }, { label: 'Сохранить и создать новую' }];

function Demo(props: Partial<SplitButtonProps>) {
  return (
    <SplitButton
      label="Сохранить"
      icon={<ThinSaveIcon />}
      groupLabel="Сохранение"
      menuLabel="Ещё варианты"
      items={ITEMS}
      {...props}
    />
  );
}

const main = () => screen.getByRole('button', { name: 'Сохранить', hidden: true });
const arrow = () => screen.getByRole('button', { name: 'Ещё варианты', hidden: true });

describe('tech-015 Scenario: SplitButton (design § 5.2)', () => {
  describe('Scenario: names', () => {
    it('is a group named by groupLabel, with the main button and the arrow', () => {
      render(<Demo />);
      const group = screen.getByRole('group', { name: 'Сохранение' });
      expect(group).toContainElement(main());
      expect(group).toContainElement(arrow());
    });

    it('names the arrow so that it does not contain the main label’s word', () => {
      render(<Demo />);
      // A query for the main button's name finds that button alone.
      expect(screen.getAllByRole('button', { name: /Сохранить/ })).toEqual([main()]);
      expect(arrow().getAttribute('aria-label')).not.toMatch(/Сохран/);
    });

    it('opens a menu of menuitems from the arrow, named by menuLabel', async () => {
      const user = userEvent.setup();
      render(<Demo />);
      expect(arrow()).toHaveAttribute('aria-haspopup', 'menu');
      expect(arrow()).toHaveAttribute('aria-expanded', 'false');
      await user.click(arrow());
      expect(await screen.findByRole('menu', { name: 'Ещё варианты' })).toBeInTheDocument();
      expect(screen.getAllByRole('menuitem').map((item) => item.textContent)).toEqual([
        'Сохранить и закрыть',
        'Сохранить и создать новую',
      ]);
      expect(arrow()).toHaveAttribute('aria-expanded', 'true');
    });

    it('opens the menu from the keyboard (ArrowDown on the arrow) and returns focus', async () => {
      const user = userEvent.setup();
      render(<Demo />);
      arrow().focus();
      await user.keyboard('{ArrowDown}');
      await waitFor(() =>
        expect(screen.getByRole('menuitem', { name: 'Сохранить и закрыть' })).toHaveFocus(),
      );
      await user.keyboard('{Escape}');
      await waitFor(() => expect(screen.queryByRole('menu')).toBeNull());
      expect(arrow()).toHaveFocus();
    });
  });

  describe('Scenario: the arrow’s glyph is the thin chevron', () => {
    it('draws ThinChevronDownIcon at the bar’s glyph size, never Material’s drop-down triangle', () => {
      render(<Demo />);
      expect(arrow().querySelector('[data-testid="ThinChevronDownIcon"]')).not.toBeNull();
      expect(arrow().querySelector('[data-testid="ArrowDropDownIcon"]')).toBeNull();
      const glyph = arrow().querySelector('svg') as SVGElement;
      expect(getComputedStyle(glyph).fontSize).toBe(`${theme.layout.iconAction.bar.glyph}px`);
    });
  });

  describe('Scenario: the main button', () => {
    it('is primary by default and draws its icon; the tone `neutral` is grey', () => {
      const { rerender } = render(<Demo />);
      expect(main()).toHaveClass('MuiButton-colorPrimary');
      expect(main().querySelector('[data-testid="ThinSaveIcon"]')).not.toBeNull();
      rerender(<Demo tone="neutral" />);
      expect(main()).toHaveClass('MuiButton-colorInherit');
    });

    it('calls onClick and submits by type', async () => {
      const user = userEvent.setup();
      const onClick = vi.fn();
      const onSubmit = vi.fn((event: { preventDefault: () => void }) => event.preventDefault());
      render(
        <form onSubmit={onSubmit}>
          <Demo onClick={onClick} type="submit" />
        </form>,
      );
      await user.click(main());
      expect(onClick).toHaveBeenCalledTimes(1);
      expect(onSubmit).toHaveBeenCalledTimes(1);
      expect(arrow()).toHaveAttribute('type', 'button');
    });
  });

  describe('Scenario: pristine — the reference’s clean disabled look', () => {
    it.each(['primary', 'neutral'] as const)(
      'tone %s: both parts are native disabled in splitDisabledFill and splitDisabledText',
      (tone) => {
        render(<Demo tone={tone} disabled />);
        for (const part of [main(), arrow()]) {
          expect(part).toBeDisabled();
          const style = getComputedStyle(part);
          expect(style.backgroundColor).toBe(rgb(theme.palette.shell.splitDisabledFill));
          expect(style.color).toBe(rgb(theme.palette.shell.splitDisabledText));
          // Not MUI's disabled grey, which is a darker block with a fainter label.
          expect(style.backgroundColor).not.toBe(rgb(theme.palette.action.disabledBackground));
          expect(style.color).not.toBe(rgb(theme.palette.action.disabled));
        }
      },
    );

    it('leaves the Tab order and does nothing', async () => {
      const user = userEvent.setup({ pointerEventsCheck: 0 });
      const onClick = vi.fn();
      render(<Demo disabled onClick={onClick} />);
      await user.tab();
      expect(main()).not.toHaveFocus();
      expect(arrow()).not.toHaveFocus();
      await user.click(main());
      await user.click(arrow());
      expect(onClick).not.toHaveBeenCalled();
      expect(screen.queryByRole('menu')).toBeNull();
    });

    it('is not drawn clean when it is enabled', () => {
      render(<Demo />);
      expect(main()).not.toBeDisabled();
      expect(getComputedStyle(main()).backgroundColor).not.toBe(
        rgb(theme.palette.shell.splitDisabledFill),
      );
    });
  });

  describe('Scenario: busy — the work is running', () => {
    it('swaps the icon for a spinner, keeps the tone’s fill and is not natively disabled', () => {
      render(<Demo busy />);
      expect(main()).toHaveAttribute('aria-busy', 'true');
      expect(main()).toHaveAttribute('aria-disabled', 'true');
      expect(arrow()).toHaveAttribute('aria-disabled', 'true');
      expect(main()).not.toBeDisabled();
      expect(arrow()).not.toBeDisabled();
      expect(main().querySelector('[data-testid="ThinSaveIcon"]')).toBeNull();
      expect(screen.getByRole('progressbar')).toBeInTheDocument();
      for (const part of [main(), arrow()]) {
        expect(part).not.toHaveClass('Mui-disabled');
        expect(getComputedStyle(part).backgroundColor).not.toBe(
          rgb(theme.palette.shell.splitDisabledFill),
        );
      }
      expect(getComputedStyle(main()).getPropertyValue('--variant-containedBg')).toBe(
        theme.palette.primary.main,
      );
    });

    it('keeps focus on both parts, and neither does anything', async () => {
      const user = userEvent.setup();
      const onClick = vi.fn();
      render(<Demo busy onClick={onClick} />);
      await user.tab();
      expect(main()).toHaveFocus();
      await user.tab();
      expect(arrow()).toHaveFocus();
      await user.click(main());
      await user.click(arrow());
      await user.keyboard('{ArrowDown}');
      expect(onClick).not.toHaveBeenCalled();
      expect(screen.queryByRole('menu')).toBeNull();
      expect(arrow()).toHaveFocus();
    });
  });
});
