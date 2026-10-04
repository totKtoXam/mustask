import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { authValue, LANGUAGES_READ, renderShell } from '../../test/renderShell';

describe('US-2 Scenario: User menu and sign-out', () => {
  it('shows initials, not the name, in the bar; the menu shows the name and «Выйти», which signs out', async () => {
    const auth = authValue([LANGUAGES_READ], { displayName: 'Иван Петров' });
    renderShell({ auth });

    const banner = screen.getByRole('banner');
    expect(within(banner).queryByText('Иван Петров')).not.toBeInTheDocument();
    const profile = within(banner).getByRole('button', { name: 'Профиль: Иван Петров' });
    expect(within(profile).getByText('ИП')).toBeInTheDocument();
    expect(profile).toHaveAttribute('aria-haspopup', 'menu');
    expect(profile).toHaveAttribute('aria-expanded', 'false');

    await userEvent.click(profile);

    const menu = screen.getByRole('menu');
    expect(profile).toHaveAttribute('aria-expanded', 'true');
    expect(within(menu).getByText('Иван Петров')).toBeInTheDocument();
    expect(
      within(menu)
        .getAllByRole('menuitem')
        .map((item) => item.textContent),
    ).toEqual(['Выйти']);

    await userEvent.click(within(menu).getByRole('menuitem', { name: 'Выйти' }));

    expect(auth.signOut).toHaveBeenCalledTimes(1);
    await waitFor(() => expect(screen.queryByRole('menu')).not.toBeInTheDocument());
  });
});

describe('US-2 Scenario Outline: Initials — on the user menu button', () => {
  it.each([
    ['Иван Петров', 'ИП'],
    ['Иван Петрович Сидоров', 'ИП'],
    ['ivan.petrov', 'I'],
    ['e2e-fixture', 'E'],
  ])('«%s» shows «%s»', (displayName, letters) => {
    renderShell({ auth: authValue([], { displayName }) });

    const profile = screen.getByRole('button', { name: `Профиль: ${displayName}` });
    expect(profile).toHaveTextContent(letters);
  });
});

describe('US-2 Scenario: No display name', () => {
  it('shows a person icon, is named «Профиль», and the menu holds only «Выйти»', async () => {
    renderShell({ auth: authValue([], { displayName: '' }) });

    const profile = screen.getByRole('button', { name: 'Профиль' });
    expect(within(profile).getByTestId('PersonOutlineOutlinedIcon')).toBeInTheDocument();

    await userEvent.click(profile);

    const menu = screen.getByRole('menu');
    expect(menu).toHaveTextContent(/^Выйти$/);
  });
});

describe('US-12 Scenario: Menus by keyboard — the user menu', () => {
  it('opens with Space on «Выйти», and Escape returns focus to the button', async () => {
    const user = userEvent.setup();
    renderShell({ auth: authValue([]) });
    const profile = screen.getByRole('button', { name: /^Профиль/ });
    profile.focus();

    await user.keyboard(' ');

    const exit = await screen.findByRole('menuitem', { name: 'Выйти' });
    await waitFor(() => expect(exit).toHaveFocus());

    await user.keyboard('{Escape}');

    await waitFor(() => expect(screen.queryByRole('menu')).not.toBeInTheDocument());
    expect(profile).toHaveFocus();
  });

  it('opens with ArrowDown too', async () => {
    const user = userEvent.setup();
    renderShell({ auth: authValue([]) });
    screen.getByRole('button', { name: /^Профиль/ }).focus();

    await user.keyboard('{ArrowDown}');

    await waitFor(() => expect(screen.getByRole('menuitem', { name: 'Выйти' })).toHaveFocus());
  });
});
