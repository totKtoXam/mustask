import { describe, expect, it } from 'vitest';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { authValue, LANGUAGES_READ, LANGUAGES_WRITE, renderShell } from '../test/renderShell';

const reader = [LANGUAGES_READ];
const administrator = [LANGUAGES_READ, LANGUAGES_WRITE];

// The five tests of the former top bar, rewritten for the new frame
// (mod-015 US-11, change table C-1..C-4).
describe('AppLayout', () => {
  it('C-1: the name from the token is in the user menu, not as text in the bar', async () => {
    renderShell({ auth: authValue(administrator, { displayName: 'Иванов Иван' }) });

    const banner = screen.getByRole('banner');
    expect(within(banner).queryByText('Иванов Иван')).not.toBeInTheDocument();
    const profile = within(banner).getByRole('button', { name: 'Профиль: Иванов Иван' });

    await userEvent.click(profile);

    expect(within(screen.getByRole('menu')).getByText('Иванов Иван')).toBeInTheDocument();
  });

  it('C-2: «Выйти» in the user menu calls signOut', async () => {
    const auth = authValue(administrator);
    renderShell({ auth });

    await userEvent.click(screen.getByRole('button', { name: /^Профиль/ }));
    await userEvent.click(screen.getByRole('menuitem', { name: 'Выйти' }));

    expect(auth.signOut).toHaveBeenCalledTimes(1);
  });

  it('C-3: «Mustask» in the bar is a link to «Главная», not a heading', () => {
    renderShell({ path: '/dictionaries/languages', auth: authValue(reader) });

    const banner = screen.getByRole('banner');
    expect(within(banner).getByRole('link', { name: 'Mustask' })).toHaveAttribute('href', '/home');
    expect(screen.queryByRole('heading', { name: 'Mustask' })).not.toBeInTheDocument();
  });

  it('C-4: with languagesRead the main navigation shows «Настройки», which leads to «Языки»', () => {
    renderShell({ auth: authValue(reader) });

    const navigation = screen.getByRole('navigation', { name: 'Основное меню' });
    expect(within(navigation).getByRole('link', { name: 'Настройки' })).toHaveAttribute(
      'href',
      '/dictionaries/languages',
    );
    expect(within(navigation).queryByRole('link', { name: 'Языки' })).not.toBeInTheDocument();
  });

  it('C-4: without languagesRead «Настройки» is hidden and ungated entries remain', () => {
    renderShell({ auth: authValue([]) });

    const navigation = screen.getByRole('navigation', { name: 'Основное меню' });
    expect(within(navigation).queryByRole('link', { name: 'Настройки' })).not.toBeInTheDocument();
    expect(within(navigation).getByRole('link', { name: 'Главная' })).toBeInTheDocument();
  });
});

describe('US-2 Scenario: The frame’s regions (BR-6)', () => {
  it('has one banner holding, left to right, the mark, the switcher, the trail and the user menu; the main navigation; one main region holding the page', () => {
    renderShell({ path: '/dictionaries/languages', auth: authValue(reader) });

    // `getByRole` throws on a second match: one banner, one main region.
    const banner = screen.getByRole('banner');
    const parts = [
      within(banner).getByRole('link', { name: 'Mustask' }),
      within(banner).getByRole('group', { name: 'Контекст работы' }),
      within(banner).getByRole('navigation', { name: 'Путь к странице' }),
      within(banner).getByRole('button', { name: 'Профиль: Иван Петров' }),
    ];
    parts.slice(1).forEach((part, index) => {
      const previous = parts[index] as HTMLElement;
      expect(
        previous.compareDocumentPosition(part) & Node.DOCUMENT_POSITION_FOLLOWING,
      ).toBeTruthy();
    });

    expect(screen.getByRole('navigation', { name: 'Основное меню' })).toBeInTheDocument();
    const main = screen.getByRole('main');
    expect(within(main).getByRole('heading', { level: 1, name: 'Языки' })).toBeInTheDocument();
  });
});

describe('US-2 Scenario: The product mark leads home', () => {
  it('opens «Главная» inside the panel, without leaving the router', async () => {
    renderShell({ path: '/dictionaries/languages', auth: authValue(reader) });

    await userEvent.click(screen.getByRole('link', { name: 'Mustask' }));

    expect(screen.getByTestId('location')).toHaveTextContent('/home');
    expect(screen.getByRole('heading', { level: 1, name: 'Главная' })).toBeInTheDocument();
  });
});

describe('US-2 Scenario: Page trail (BR-6)', () => {
  it('reads «Настройки / Справочники» on «Языки», neither segment a link', () => {
    renderShell({ path: '/dictionaries/languages', auth: authValue(administrator) });

    const trail = screen.getByRole('navigation', { name: 'Путь к странице' });
    expect(trail).toHaveTextContent('Настройки/Справочники');
    expect(within(trail).queryByRole('link')).not.toBeInTheDocument();
  });

  it('reads «Настройки / Справочники / Языки» on the new-language form, «Языки» a link and the page only as the heading', () => {
    renderShell({ path: '/dictionaries/languages/new', auth: authValue(administrator) });

    const trail = screen.getByRole('navigation', { name: 'Путь к странице' });
    expect(trail).toHaveTextContent('Настройки/Справочники/Языки');
    expect(within(trail).getByRole('link', { name: 'Языки' })).toHaveAttribute(
      'href',
      '/dictionaries/languages',
    );
    expect(within(trail).queryByText('Добавить язык')).not.toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1, name: 'Добавить язык' })).toBeInTheDocument();
  });

  it('has no trail on «Главная» and none on a full-page frame', () => {
    const { unmount } = renderShell({ path: '/home', auth: authValue(administrator) });
    expect(screen.queryByRole('navigation', { name: 'Путь к странице' })).not.toBeInTheDocument();
    unmount();

    renderShell({ path: '/no-such-page', auth: authValue(administrator) });
    expect(
      screen.getByRole('heading', { level: 1, name: 'Страница не найдена' }),
    ).toBeInTheDocument();
    expect(screen.queryByRole('navigation', { name: 'Путь к странице' })).not.toBeInTheDocument();
  });
});
