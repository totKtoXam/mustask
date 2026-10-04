import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { authValue, LANGUAGES_READ, LANGUAGES_WRITE, renderShell } from '../test/renderShell';
import { setViewportWidth } from '../test/viewport';
import { KEPT_CHOICE_KEY } from './navigation/keptChoice';

const reader = [LANGUAGES_READ];
const administrator = [LANGUAGES_READ, LANGUAGES_WRITE];

afterEach(() => {
  vi.restoreAllMocks();
});

/** The accessible name of whatever has focus now. */
function focusedName(): string {
  const element = document.activeElement as HTMLElement | null;
  if (!element) {
    return '';
  }
  return element.getAttribute('aria-label') ?? element.textContent ?? '';
}

async function tabSequence(user: ReturnType<typeof userEvent.setup>, count: number) {
  const names: string[] = [];
  for (let step = 0; step < count; step += 1) {
    await user.tab();
    names.push(focusedName());
  }
  return names;
}

describe('US-12 Scenario: Skip to the content', () => {
  it('shows «Перейти к содержимому» on the first Tab, and Enter moves focus into the main region', async () => {
    const user = userEvent.setup();
    renderShell({ auth: authValue(reader) });

    await user.tab();

    const skip = screen.getByRole('link', { name: 'Перейти к содержимому' });
    expect(skip).toHaveFocus();

    await user.keyboard('{Enter}');

    expect(screen.getByRole('main')).toHaveFocus();
    expect(screen.getByTestId('location')).toHaveTextContent('/home');
  });
});

describe('US-12 Scenario: Landmarks outside settings', () => {
  it('has one banner, one main region and one navigation region, «Основное меню», on «Главная»', () => {
    renderShell({ auth: authValue(reader) });

    expect(screen.getAllByRole('banner')).toHaveLength(1);
    expect(screen.getAllByRole('main')).toHaveLength(1);
    const navigations = screen.getAllByRole('navigation');
    expect(navigations).toHaveLength(1);
    expect(navigations[0]).toHaveAccessibleName('Основное меню');
  });
});

describe('US-12 Scenario: Landmarks on a settings page (BR-19)', () => {
  it('has one banner, one main region and three navigation regions on «Языки»', () => {
    renderShell({ path: '/dictionaries/languages', auth: authValue(reader) });

    expect(screen.getAllByRole('banner')).toHaveLength(1);
    expect(screen.getAllByRole('main')).toHaveLength(1);
    expect(
      screen
        .getAllByRole('navigation')
        .map((navigation) => navigation.getAttribute('aria-label'))
        .sort(),
    ).toEqual(['Меню настроек', 'Основное меню', 'Путь к странице']);
  });
});

describe('US-12 Scenario: Tab order follows the layout', () => {
  it('on «Главная» at 1440 px: mark, three levels, user menu, «Главная», «Настройки», the toggle, the page', async () => {
    const user = userEvent.setup();
    renderShell({ auth: authValue(administrator) });

    expect(await tabSequence(user, 10)).toEqual([
      'Перейти к содержимому',
      'Mustask',
      'Организация: Пример организации 1',
      'Проект: Пример проекта 1',
      'Команда: Пример команды 1',
      'Профиль: Иван Петров',
      'Главная',
      'Настройки',
      'Свернуть меню',
      'Действие страницы',
    ]);
  });

  it('on «Языки»: mark, three levels, user menu, the rail’s «Главная» and «Настройки», «Языки» in «Меню настроек», the page', async () => {
    const user = userEvent.setup();
    renderShell({ path: '/dictionaries/languages', auth: authValue(administrator) });

    const sequence = await tabSequence(user, 10);

    expect(sequence).toEqual([
      'Перейти к содержимому',
      'Mustask',
      'Организация: Пример организации 1',
      'Проект: Пример проекта 1',
      'Команда: Пример команды 1',
      'Профиль: Иван Петров',
      'Главная',
      'Настройки',
      'Языки',
      'Действие страницы',
    ]);
    // The second «Языки» stop is the settings entry, not a trail segment.
    expect(document.activeElement).toHaveTextContent('Действие страницы');
  });

  it('below 1024 px «Меню» comes first after the skip link', async () => {
    const user = userEvent.setup();
    setViewportWidth(900);
    renderShell({ auth: authValue(administrator) });

    expect(await tabSequence(user, 3)).toEqual(['Перейти к содержимому', 'Меню', 'Mustask']);
  });
});

describe('US-12 Scenario: Menus by keyboard', () => {
  it('opens a switcher level with Enter on its current entry; arrows move, Enter chooses', async () => {
    const user = userEvent.setup();
    renderShell({ auth: authValue(reader) });
    const level = screen.getByRole('button', { name: 'Организация: Пример организации 1' });
    level.focus();

    await user.keyboard('{Enter}');

    const current = await screen.findByRole('menuitemradio', { name: 'Пример организации 1' });
    await waitFor(() => expect(current).toHaveFocus());

    await user.keyboard('{ArrowDown}');
    expect(screen.getByRole('menuitemradio', { name: 'Пример организации 2' })).toHaveFocus();
    await user.keyboard('{ArrowUp}');
    expect(current).toHaveFocus();
    await user.keyboard('{ArrowDown}{Enter}');

    await waitFor(() => expect(screen.queryByRole('menu')).not.toBeInTheDocument());
    expect(
      screen.getByRole('button', { name: 'Организация: Пример организации 2' }),
    ).toBeInTheDocument();
  });

  it.each([['{ArrowDown}'], [' ']])(
    'opens with %s too, and Escape closes it and returns focus to the level',
    async (key) => {
      const user = userEvent.setup();
      renderShell({ auth: authValue(reader) });
      const level = screen.getByRole('button', { name: 'Проект: Пример проекта 1' });
      level.focus();

      await user.keyboard(key);

      await waitFor(() =>
        expect(screen.getByRole('menuitemradio', { name: 'Пример проекта 1' })).toHaveFocus(),
      );
      expect(level).toHaveAttribute('aria-expanded', 'true');

      await user.keyboard('{Escape}');

      await waitFor(() => expect(screen.queryByRole('menu')).not.toBeInTheDocument());
      expect(level).toHaveFocus();
      expect(level).toHaveAttribute('aria-expanded', 'false');
    },
  );
});

describe('US-12 Scenario: Collapsed rail stays usable', () => {
  it('names each rail icon, shows its label as a tooltip on keyboard focus, and Enter opens its page', async () => {
    const user = userEvent.setup();
    // jsdom throws on `:focus-visible`, which MUI's Tooltip asks before it
    // opens on focus. A browser matches it on keyboard focus, which is all
    // this test does; here the focused element stands in for it.
    const matches = Element.prototype.matches;
    vi.spyOn(Element.prototype, 'matches').mockImplementation(function (
      this: Element,
      selector: string,
    ) {
      return selector === ':focus-visible'
        ? this === document.activeElement
        : matches.call(this, selector);
    });
    localStorage.setItem(KEPT_CHOICE_KEY, 'rail');
    renderShell({ path: '/no-such-page', auth: authValue(reader) });

    const navigation = screen.getByRole('navigation', { name: 'Основное меню' });
    const home = within(navigation).getByRole('link', { name: 'Главная' });
    expect(within(navigation).queryByText('Главная')).not.toBeInTheDocument();

    // Tab onto the rail's first entry, as a keyboard user does.
    await tabSequence(user, 7);
    expect(home).toHaveFocus();
    expect(await screen.findByRole('tooltip')).toHaveTextContent('Главная');

    await user.keyboard('{Enter}');

    expect(screen.getByTestId('location')).toHaveTextContent('/home');
  });
});
