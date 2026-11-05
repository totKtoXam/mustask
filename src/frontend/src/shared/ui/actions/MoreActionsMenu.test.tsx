import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { px, rulesFor, theme } from '../../../test/kitTesting';
import { MoreActionsMenu } from './MoreActionsMenu';

// tech-015 design § 5.3 «MoreActionsMenu»: a MenuButton whose trigger is the
// vertical thin «⋮»; the button and the menu both read the label.

describe('tech-015 Scenario: MoreActionsMenu (design § 5.3)', () => {
  it('is named «Ещё действия» by default — the button and the menu', async () => {
    const user = userEvent.setup();
    render(<MoreActionsMenu items={[{ label: 'Копировать ссылку' }]} />);
    const button = screen.getByRole('button', { name: 'Ещё действия' });
    expect(button).toHaveAttribute('aria-haspopup', 'menu');
    expect(button).toHaveAttribute('aria-expanded', 'false');
    await user.click(button);
    expect(await screen.findByRole('menu', { name: 'Ещё действия' })).toBeInTheDocument();
    expect(button).toHaveAttribute('aria-expanded', 'true');
  });

  it('names its row when it is given a label: «Ещё действия: MSK-EPIC-1»', async () => {
    const user = userEvent.setup();
    render(
      <MoreActionsMenu
        label="Ещё действия: MSK-EPIC-1"
        size="row"
        items={[{ label: 'Изменить' }]}
      />,
    );
    await user.click(screen.getByRole('button', { name: 'Ещё действия: MSK-EPIC-1' }));
    expect(
      await screen.findByRole('menu', { name: 'Ещё действия: MSK-EPIC-1' }),
    ).toBeInTheDocument();
  });

  it('draws the vertical thin dots — never a horizontal «⋯»', () => {
    render(<MoreActionsMenu items={[]} />);
    const button = screen.getByRole('button', { name: 'Ещё действия' });
    expect(button.querySelector('[data-testid="ThinMoreVerticalIcon"]')).not.toBeNull();
    expect(button.querySelector('[data-testid="MoreHorizIcon"]')).toBeNull();
  });

  it.each([
    ['bar', theme.layout.iconAction.bar],
    ['row', theme.layout.iconAction.row],
  ] as const)('is the %s size of IconAction', (size, { box, glyph }) => {
    render(<MoreActionsMenu size={size} items={[]} />);
    const style = getComputedStyle(screen.getByRole('button', { name: 'Ещё действия' }));
    expect(px(style.width)).toBe(box);
    expect(px(style.fontSize)).toBe(glyph);
  });

  it('is a bar-size control by default', () => {
    render(<MoreActionsMenu items={[]} />);
    expect(px(getComputedStyle(screen.getByRole('button')).width)).toBe(32);
  });

  it('opens with the keyboard, chooses an entry and gives focus back', async () => {
    const user = userEvent.setup();
    const onSelect = vi.fn();
    render(
      <MemoryRouter>
        <MoreActionsMenu items={[{ label: 'Копировать ссылку', onSelect }]} />
      </MemoryRouter>,
    );
    await user.tab();
    await user.keyboard('{ArrowDown}');
    await waitFor(() =>
      expect(screen.getByRole('menuitem', { name: 'Копировать ссылку' })).toHaveFocus(),
    );
    await user.keyboard('{Enter}');
    expect(onSelect).toHaveBeenCalledTimes(1);
    await waitFor(() => expect(screen.queryByRole('menu')).toBeNull());
    expect(screen.getByRole('button', { name: 'Ещё действия' })).toHaveFocus();
  });

  describe('Scenario: a table row’s control is revealed', () => {
    it('is always drawn unless it is told it is not revealed', () => {
      const { container, rerender } = render(<MoreActionsMenu items={[]} />);
      expect(container.querySelector('[data-revealed]')).toBeNull();
      rerender(<MoreActionsMenu items={[]} revealed />);
      expect(container.querySelector('[data-revealed]')).toBeNull();
    });

    it('is transparent at rest when not revealed, but stays in the Tab order', async () => {
      const user = userEvent.setup();
      const { container } = render(<MoreActionsMenu items={[]} revealed={false} />);
      const wrapper = container.querySelector('[data-revealed="false"]') as HTMLElement;
      expect(getComputedStyle(wrapper).opacity).toBe('0');
      await user.tab();
      expect(screen.getByRole('button', { name: 'Ещё действия' })).toHaveFocus();
    });

    it('stays drawn while its menu is open, by an attribute it sets itself, not by :has (review R1-2)', async () => {
      const user = userEvent.setup();
      const { container } = render(
        <MoreActionsMenu items={[{ label: 'Изменить' }]} revealed={false} />,
      );
      const wrapper = container.querySelector('[data-revealed="false"]') as HTMLElement;
      expect(wrapper).toHaveAttribute('data-expanded', 'false');
      expect(getComputedStyle(wrapper).opacity).toBe('0');

      await user.click(screen.getByRole('button', { name: 'Ещё действия' }));
      await screen.findByRole('menu');
      // The menu took the focus away, so only the open state keeps the control drawn.
      expect(wrapper).not.toContainElement(document.activeElement as HTMLElement);
      expect(wrapper).toHaveAttribute('data-expanded', 'true');
      expect(getComputedStyle(wrapper).opacity).toBe('1');

      await user.keyboard('{Escape}');
      await waitFor(() => expect(screen.queryByRole('menu')).toBeNull());
      expect(wrapper).toHaveAttribute('data-expanded', 'false');
    });

    it('has the open rule as a rule on its own attribute', () => {
      const { container } = render(<MoreActionsMenu items={[]} revealed={false} />);
      const wrapper = container.querySelector('[data-revealed="false"]') as HTMLElement;
      const open = rulesFor(wrapper, '[data-expanded="true"]');
      expect(open.some((rule) => rule.declarations.opacity === '1')).toBe(true);
    });
  });
});
