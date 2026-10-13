import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import DeleteOutlineOutlinedIcon from '@mui/icons-material/DeleteOutlineOutlined';
import { rgb, theme } from '../../../test/kitTesting';
import { ActionButton } from './ActionButton';
import { MenuButton, type MenuButtonProps, type MenuEntry } from './MenuButton';

// tech-015 design § 5.3 «MenuButton» (mod-015 § 12.4): Enter, Space or ArrowDown
// opens on the current entry, the arrows, Home/End and typing move, Escape closes
// and returns focus, radio and page selection, a group's heading as each entry's
// description, a destructive entry in the error colour.

const ENTRIES: readonly MenuEntry[] = [
  { label: 'Эпики', current: false },
  { label: 'Фичи', current: true },
  { label: 'Истории' },
];

function Demo({
  items = ENTRIES,
  ...rest
}: Partial<Omit<MenuButtonProps, 'trigger'>> & { items?: readonly MenuEntry[] }) {
  return (
    <MemoryRouter>
      <MenuButton
        label="Уровень"
        items={items}
        trigger={(trigger) => <ActionButton label="Фичи" {...trigger} />}
        {...rest}
      />
    </MemoryRouter>
  );
}

// While a menu is open MUI hides everything outside it from assistive technology.
const trigger = () => screen.getByRole('button', { name: 'Фичи', hidden: true });

describe('tech-015 Scenario: MenuButton (design § 5.3)', () => {
  describe('Scenario: it opens by the keyboard on the current entry', () => {
    it.each([
      ['Enter', '{Enter}'],
      ['Space', ' '],
      ['ArrowDown', '{ArrowDown}'],
    ])('%s opens the menu with focus on the current entry', async (_name, keys) => {
      const user = userEvent.setup();
      render(<Demo selection="page" />);
      await user.tab();
      expect(trigger()).toHaveFocus();
      await user.keyboard(keys);
      expect(await screen.findByRole('menu', { name: 'Уровень' })).toBeInTheDocument();
      await waitFor(() => expect(screen.getByRole('menuitem', { name: 'Фичи' })).toHaveFocus());
    });

    it('opens on the first entry when none is current', async () => {
      const user = userEvent.setup();
      render(<Demo items={[{ label: 'Один' }, { label: 'Два' }]} />);
      await user.tab();
      await user.keyboard('{ArrowDown}');
      await waitFor(() => expect(screen.getByRole('menuitem', { name: 'Один' })).toHaveFocus());
    });

    it('skips a disabled first entry', async () => {
      const user = userEvent.setup();
      render(<Demo items={[{ label: 'Один', disabled: true }, { label: 'Два' }]} />);
      await user.tab();
      await user.keyboard('{ArrowDown}');
      await waitFor(() => expect(screen.getByRole('menuitem', { name: 'Два' })).toHaveFocus());
    });
  });

  describe('Scenario: the trigger says what it does', () => {
    it('announces a menu, is collapsed at rest and expanded with its menu', async () => {
      const user = userEvent.setup();
      render(<Demo />);
      expect(trigger()).toHaveAttribute('aria-haspopup', 'menu');
      expect(trigger()).toHaveAttribute('aria-expanded', 'false');
      expect(trigger()).not.toHaveAttribute('aria-controls');
      await user.click(trigger());
      const menu = await screen.findByRole('menu', { name: 'Уровень' });
      expect(trigger()).toHaveAttribute('aria-expanded', 'true');
      expect(trigger()).toHaveAttribute('aria-controls', menu.closest('[id]')?.id ?? menu.id);
    });

    it('names the menu by its label', async () => {
      render(<Demo label="Ещё действия: MSK-EPIC-1" />);
      await userEvent.click(trigger());
      expect(
        await screen.findByRole('menu', { name: 'Ещё действия: MSK-EPIC-1' }),
      ).toBeInTheDocument();
    });
  });

  describe('Scenario: moving and choosing', () => {
    it('walks with the arrows, jumps with Home and End, and jumps to a typed letter', async () => {
      const user = userEvent.setup();
      render(<Demo selection="page" />);
      await user.tab();
      await user.keyboard('{ArrowDown}');
      await waitFor(() => expect(screen.getByRole('menuitem', { name: 'Фичи' })).toHaveFocus());
      await user.keyboard('{ArrowDown}');
      expect(screen.getByRole('menuitem', { name: 'Истории' })).toHaveFocus();
      await user.keyboard('{ArrowUp}');
      expect(screen.getByRole('menuitem', { name: 'Фичи' })).toHaveFocus();
      await user.keyboard('{Home}');
      expect(screen.getByRole('menuitem', { name: 'Эпики' })).toHaveFocus();
      await user.keyboard('{End}');
      expect(screen.getByRole('menuitem', { name: 'Истории' })).toHaveFocus();
      await user.keyboard('ф');
      expect(screen.getByRole('menuitem', { name: 'Фичи' })).toHaveFocus();
    });

    it('chooses with Enter: the menu closes, the entry runs and focus returns to the trigger', async () => {
      const user = userEvent.setup();
      const onSelect = vi.fn();
      render(<Demo items={[{ label: 'Один', onSelect }, { label: 'Два' }]} />);
      await user.tab();
      await user.keyboard('{ArrowDown}');
      await waitFor(() => expect(screen.getByRole('menuitem', { name: 'Один' })).toHaveFocus());
      await user.keyboard('{Enter}');
      expect(onSelect).toHaveBeenCalledTimes(1);
      await waitFor(() => expect(screen.queryByRole('menu')).toBeNull());
      expect(trigger()).toHaveFocus();
    });

    it('chooses with a click, and a disabled entry does nothing', async () => {
      const user = userEvent.setup({ pointerEventsCheck: 0 });
      const onSelect = vi.fn();
      const never = vi.fn();
      render(
        <Demo
          items={[
            { label: 'Один', onSelect },
            { label: 'Два', disabled: true, onSelect: never },
          ]}
        />,
      );
      await user.click(trigger());
      await user.click(await screen.findByRole('menuitem', { name: 'Два' }));
      expect(never).not.toHaveBeenCalled();
      await user.click(screen.getByRole('menuitem', { name: 'Один' }));
      expect(onSelect).toHaveBeenCalledTimes(1);
    });

    it('links an entry that has an address', async () => {
      const user = userEvent.setup();
      render(<Demo items={[{ label: 'Эпики', to: '/boards/epics' }]} />);
      await user.click(trigger());
      expect(await screen.findByRole('menuitem', { name: 'Эпики' })).toHaveAttribute(
        'href',
        '/boards/epics',
      );
    });
  });

  describe('Scenario: Escape closes and returns focus', () => {
    it('closes on Escape and gives focus back to the trigger', async () => {
      const user = userEvent.setup();
      render(<Demo />);
      await user.tab();
      await user.keyboard('{Enter}');
      await screen.findByRole('menu');
      await user.keyboard('{Escape}');
      await waitFor(() => expect(screen.queryByRole('menu')).toBeNull());
      expect(trigger()).toHaveFocus();
      expect(trigger()).toHaveAttribute('aria-expanded', 'false');
    });

    it('leaves the focus to the host when disableRestoreFocus is set', async () => {
      const user = userEvent.setup();
      render(<Demo disableRestoreFocus />);
      await user.tab();
      await user.keyboard('{Enter}');
      await screen.findByRole('menu');
      await user.keyboard('{Escape}');
      await waitFor(() => expect(screen.queryByRole('menu')).toBeNull());
      expect(trigger()).not.toHaveFocus();
    });
  });

  describe('Scenario: radio and page selection', () => {
    it('radio: menuitemradio, checked on the current one, with a check mark', async () => {
      const user = userEvent.setup();
      render(<Demo selection="radio" />);
      await user.click(trigger());
      const current = await screen.findByRole('menuitemradio', { name: 'Фичи' });
      expect(current).toHaveAttribute('aria-checked', 'true');
      expect(screen.getByRole('menuitemradio', { name: 'Эпики' })).toHaveAttribute(
        'aria-checked',
        'false',
      );
      expect(current.querySelector('[data-testid="CheckOutlinedIcon"]')).not.toBeNull();
      expect(screen.queryByRole('menuitem')).toBeNull();
    });

    it('page: the current entry is selected and aria-current="page"', async () => {
      const user = userEvent.setup();
      render(<Demo selection="page" />);
      await user.click(trigger());
      const current = await screen.findByRole('menuitem', { name: 'Фичи' });
      expect(current).toHaveAttribute('aria-current', 'page');
      expect(current).toHaveClass('Mui-selected');
      const other = screen.getByRole('menuitem', { name: 'Эпики' });
      expect(other).not.toHaveAttribute('aria-current');
      expect(other).not.toHaveClass('Mui-selected');
    });

    it('none: plain menuitems, and no entry is marked even if one says it is current', async () => {
      const user = userEvent.setup();
      render(<Demo selection="none" />);
      await user.click(trigger());
      const items = await screen.findAllByRole('menuitem');
      expect(items).toHaveLength(3);
      for (const item of items) {
        expect(item).not.toHaveAttribute('aria-current');
        expect(item).not.toHaveAttribute('aria-checked');
        expect(item).not.toHaveClass('Mui-selected');
      }
    });
  });

  describe('Scenario: groups and rules', () => {
    const grouped: readonly MenuEntry[] = [
      { group: 'Переместить', items: [{ label: 'В начало' }, { label: 'В конец' }] },
      'divider',
      { label: 'Изменить родителя…' },
    ];

    it('names the heading as the description of each entry under it, and of no other', async () => {
      const user = userEvent.setup();
      render(<Demo items={grouped} />);
      await user.click(trigger());
      const heading = await screen.findByText('Переместить');
      expect(heading).toHaveAttribute('role', 'presentation');
      for (const name of ['В начало', 'В конец']) {
        const item = screen.getByRole('menuitem', { name });
        expect(item).toHaveAttribute('aria-describedby', heading.id);
        expect(item).toHaveAccessibleDescription('Переместить');
      }
      expect(screen.getByRole('menuitem', { name: 'Изменить родителя…' })).not.toHaveAttribute(
        'aria-describedby',
      );
    });

    it('draws a rule as a separator', async () => {
      const user = userEvent.setup();
      render(<Demo items={grouped} />);
      await user.click(trigger());
      expect(await screen.findByRole('separator')).toBeInTheDocument();
    });
  });

  describe('Scenario: look of an entry', () => {
    it('draws a destructive entry’s text and icon in the error colour, and the others neutral', async () => {
      const user = userEvent.setup();
      render(
        <Demo
          items={[
            { label: 'Копировать ссылку', icon: <DeleteOutlineOutlinedIcon /> },
            {
              label: 'Удалить рабочий элемент',
              icon: <DeleteOutlineOutlinedIcon />,
              destructive: true,
            },
          ]}
        />,
      );
      await user.click(trigger());
      const destructive = await screen.findByRole('menuitem', { name: 'Удалить рабочий элемент' });
      expect(getComputedStyle(destructive).color).toBe(rgb(theme.palette.error.main));
      const icon = destructive.querySelector('.MuiListItemIcon-root') as HTMLElement;
      expect(getComputedStyle(icon).color).toBe(rgb(theme.palette.error.main));
      const plain = screen.getByRole('menuitem', { name: 'Копировать ссылку' });
      const plainIcon = plain.querySelector('.MuiListItemIcon-root') as HTMLElement;
      // A neutral icon: not the accent, not the error colour.
      expect(getComputedStyle(plainIcon).color).toBe(rgb(theme.palette.text.secondary));
    });
  });
});
