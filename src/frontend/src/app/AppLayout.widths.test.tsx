import { describe, expect, it } from 'vitest';
import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { authValue, LANGUAGES_READ, renderShell } from '../test/renderShell';
import { setReducedMotion, setViewportWidth } from '../test/viewport';
import { KEPT_CHOICE_KEY } from './navigation/keptChoice';
import { theme } from './theme';

const reader = [LANGUAGES_READ];

/** `#E0E0E0` → `rgb(224, 224, 224)`, the form jsdom's computed style reports. */
function rgb(hex: string): string {
  const digits = hex.replace('#', '');
  const [red, green, blue] = [0, 2, 4].map((offset) =>
    parseInt(digits.slice(offset, offset + 2), 16),
  );
  return `rgb(${red}, ${green}, ${blue})`;
}

function mainNavigation() {
  return screen.getByRole('navigation', { name: 'Основное меню' });
}

/** Expanded: labels are drawn beside the icons; a rail draws none. */
function isExpanded() {
  return within(mainNavigation()).queryByText('Главная') !== null;
}

describe('US-4 Scenario: Collapse to a rail and back (BR-10)', () => {
  it('folds to named, tooltipped icons and back, the toggle reporting its state', async () => {
    const user = userEvent.setup();
    renderShell({ auth: authValue(reader) });
    expect(isExpanded()).toBe(true);
    expect(screen.getByRole('button', { name: 'Свернуть меню' })).toHaveAttribute(
      'aria-expanded',
      'true',
    );

    await user.click(screen.getByRole('button', { name: 'Свернуть меню' }));

    expect(isExpanded()).toBe(false);
    const home = within(mainNavigation()).getByRole('link', { name: 'Главная' });
    expect(within(mainNavigation()).getByRole('link', { name: 'Настройки' })).toBeInTheDocument();
    const toggle = screen.getByRole('button', { name: 'Развернуть меню' });
    expect(toggle).toHaveAttribute('aria-expanded', 'false');
    expect(toggle).toHaveAttribute('aria-controls', mainNavigation().id);

    await user.hover(home);
    expect(await screen.findByRole('tooltip')).toHaveTextContent('Главная');
    await user.unhover(home);

    await user.click(toggle);

    expect(isExpanded()).toBe(true);
    expect(screen.getByRole('button', { name: 'Свернуть меню' })).toHaveAttribute(
      'aria-expanded',
      'true',
    );
  });
});

describe('US-4 Scenario: The choice is kept per browser', () => {
  it('opens collapsed again after a reload or in a new tab', async () => {
    const first = renderShell({ auth: authValue(reader) });
    await userEvent.click(screen.getByRole('button', { name: 'Свернуть меню' }));
    expect(localStorage.getItem(KEPT_CHOICE_KEY)).toBe('rail');
    first.unmount();

    renderShell({ auth: authValue(reader) });

    expect(isExpanded()).toBe(false);
    expect(screen.getByRole('button', { name: 'Развернуть меню' })).toBeInTheDocument();
  });
});

describe('US-4 Scenario: Defaults by width when no choice is kept', () => {
  it('is expanded at 1440 px, a rail at 1200 px and expanded again at 1440 px', () => {
    renderShell({ auth: authValue(reader) });
    expect(isExpanded()).toBe(true);

    act(() => setViewportWidth(1200));
    expect(isExpanded()).toBe(false);

    act(() => setViewportWidth(1440));
    expect(isExpanded()).toBe(true);
  });
});

describe('US-4 Scenario: A kept choice holds across widths at 1024 px and above', () => {
  it('stays expanded at 1200 px once the user expanded it', async () => {
    renderShell({ auth: authValue(reader) });
    await userEvent.click(screen.getByRole('button', { name: 'Свернуть меню' }));
    await userEvent.click(screen.getByRole('button', { name: 'Развернуть меню' }));

    act(() => setViewportWidth(1200));

    expect(isExpanded()).toBe(true);
  });
});

describe('US-4 Scenario: Narrow window', () => {
  it('hides the navigation behind «Меню»; Escape and a click outside return focus to «Меню»; an entry moves focus to the main region', async () => {
    const user = userEvent.setup();
    localStorage.setItem(KEPT_CHOICE_KEY, 'rail');
    setViewportWidth(900);
    renderShell({ path: '/no-such-page', auth: authValue(reader) });

    expect(screen.queryByRole('navigation', { name: 'Основное меню' })).not.toBeInTheDocument();
    const menu = within(screen.getByRole('banner')).getByRole('button', { name: 'Меню' });
    expect(menu).toHaveAttribute('aria-expanded', 'false');

    await user.click(menu);
    expect(menu).toHaveAttribute('aria-expanded', 'true');
    // Over the content, the expanded navigation with its labels.
    expect(within(mainNavigation()).getByText('Главная')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /меню$/ })).not.toBeInTheDocument();

    await user.keyboard('{Escape}');
    await waitFor(() =>
      expect(screen.queryByRole('navigation', { name: 'Основное меню' })).not.toBeInTheDocument(),
    );
    expect(menu).toHaveFocus();

    await user.click(menu);
    const backdrop = document.querySelector('.MuiBackdrop-root');
    expect(backdrop).not.toBeNull();
    await user.click(backdrop as Element);
    await waitFor(() =>
      expect(screen.queryByRole('navigation', { name: 'Основное меню' })).not.toBeInTheDocument(),
    );
    expect(menu).toHaveFocus();

    await user.click(menu);
    await user.click(within(mainNavigation()).getByRole('link', { name: 'Главная' }));

    expect(screen.getByTestId('location')).toHaveTextContent('/home');
    await waitFor(() =>
      expect(screen.queryByRole('navigation', { name: 'Основное меню' })).not.toBeInTheDocument(),
    );
    await waitFor(() => expect(screen.getByRole('main')).toHaveFocus());

    act(() => setViewportWidth(1440));

    expect(screen.queryByRole('button', { name: 'Меню' })).not.toBeInTheDocument();
    // The kept rail applies again.
    expect(isExpanded()).toBe(false);
  });

  it('closes the overlay when the window widens while it is open', async () => {
    setViewportWidth(900);
    renderShell({ auth: authValue(reader) });
    await userEvent.click(screen.getByRole('button', { name: 'Меню' }));

    act(() => setViewportWidth(1440));
    act(() => setViewportWidth(900));

    await waitFor(() => expect(document.querySelector('.MuiBackdrop-root')).toBeNull());
    expect(screen.getByRole('button', { name: 'Меню' })).toHaveAttribute('aria-expanded', 'false');
  });

  // qa.md D-3. jsdom has no hit testing, so this pins the stacking order that
  // decides it; e2e 19.7a checks the clicks themselves in a browser.
  it.each([
    ['outside settings', '/home'],
    ['on a settings page', '/dictionaries/languages'],
  ])(
    'stacks the open overlay, backdrop included, above the top bar — %s (design § 4.5)',
    async (_case, path) => {
      setViewportWidth(900);
      renderShell({ path, auth: authValue(reader) });
      // Taken before opening: the open overlay hides the rest of the page.
      const topBar = screen.getByRole('banner');
      await userEvent.click(screen.getByRole('button', { name: 'Меню' }));

      const overlay = mainNavigation().closest('.MuiModal-root') as HTMLElement;
      expect(overlay).not.toBeNull();
      // The backdrop belongs to the overlay's stacking context, so a click on
      // the top bar lands on it and closes the overlay (BR-10).
      expect(overlay.querySelector('.MuiBackdrop-root')).not.toBeNull();
      const stackLevel = (element: Element) => Number(getComputedStyle(element).zIndex);
      expect(Number.isFinite(stackLevel(topBar))).toBe(true);
      expect(stackLevel(overlay)).toBeGreaterThan(stackLevel(topBar));
    },
  );
});

describe('US-4 Scenario: The overlay on a settings page (BR-19)', () => {
  it('shows the rail with «Настройки» current and «Меню настроек» beside it', async () => {
    setViewportWidth(900);
    renderShell({ path: '/dictionaries/languages', auth: authValue(reader) });

    expect(screen.queryByRole('navigation', { name: 'Меню настроек' })).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Меню' }));

    expect(isExpanded()).toBe(false);
    expect(within(mainNavigation()).getByRole('link', { name: 'Настройки' })).toHaveAttribute(
      'aria-current',
      'true',
    );
    expect(
      within(screen.getByRole('navigation', { name: 'Меню настроек' })).getByRole('link', {
        name: 'Языки',
      }),
    ).toHaveAttribute('aria-current', 'page');
  });
});

// Code review F-6: the rule's width is the theme's `layout.dividerWidth`,
// not a literal at the call site (BR-1), in both placements of the column.
describe('Design § 4.4: «Меню настроек» stands behind a divider rule from the theme', () => {
  function expectDividerRule(column: Element | null) {
    expect(column).not.toBeNull();
    const style = getComputedStyle(column as Element);
    expect(style.borderLeftWidth).toBe(`${theme.layout.dividerWidth}px`);
    expect(style.borderLeftStyle).toBe('solid');
    expect(style.borderLeftColor).toBe(rgb(theme.palette.divider));
  }

  it('beside the permanent rail at 1440 px', () => {
    renderShell({ path: '/dictionaries/languages', auth: authValue(reader) });

    const settings = screen.getByRole('navigation', { name: 'Меню настроек' });
    expectDividerRule(settings.closest('.MuiDrawer-paper'));
  });

  it('beside the rail in the overlay at 900 px', async () => {
    setViewportWidth(900);
    renderShell({ path: '/dictionaries/languages', auth: authValue(reader) });
    await userEvent.click(screen.getByRole('button', { name: 'Меню' }));

    const settings = screen.getByRole('navigation', { name: 'Меню настроек' });
    expectDividerRule(settings.parentElement);
  });
});

describe('US-4 Scenario: Settings pages keep the rail (BR-19)', () => {
  it('shows a rail with no toggle on «Языки», and the kept expanded navigation back on «Главная»', async () => {
    localStorage.setItem(KEPT_CHOICE_KEY, 'expanded');
    renderShell({ path: '/dictionaries/languages', auth: authValue(reader) });

    expect(isExpanded()).toBe(false);
    expect(screen.queryByRole('button', { name: /меню$/ })).not.toBeInTheDocument();

    await userEvent.click(within(mainNavigation()).getByRole('link', { name: 'Главная' }));

    expect(isExpanded()).toBe(true);
    expect(screen.getByRole('button', { name: 'Свернуть меню' })).toBeInTheDocument();
  });
});

describe('US-4 Scenario: Reduced motion', () => {
  it('opens the overlay without an animated transition under prefers-reduced-motion', async () => {
    setReducedMotion(true);
    setViewportWidth(900);
    renderShell({ auth: authValue(reader) });

    await userEvent.click(screen.getByRole('button', { name: 'Меню' }));

    const paper = document.querySelector('.MuiDrawer-paper') as HTMLElement;
    expect(paper.style.transition).toMatch(/^transform 0ms /);
  });

  it('slides the overlay in when motion is allowed', async () => {
    setViewportWidth(900);
    renderShell({ auth: authValue(reader) });

    await userEvent.click(screen.getByRole('button', { name: 'Меню' }));

    const paper = document.querySelector('.MuiDrawer-paper') as HTMLElement;
    expect(paper.style.transition).toMatch(/^transform [1-9]\d*ms /);
  });
});

describe('US-12 Scenario: Reflow at 200 % zoom (D-8) — what yields below 1024 px (design § 3.6)', () => {
  it('moves the trail out of the top bar into the page header, with the same name and links', () => {
    setViewportWidth(640);
    renderShell({ path: '/dictionaries/languages/new', auth: authValue(reader) });

    const banner = screen.getByRole('banner');
    expect(
      within(banner).queryByRole('navigation', { name: 'Путь к странице' }),
    ).not.toBeInTheDocument();
    const trail = within(screen.getByRole('main')).getByRole('navigation', {
      name: 'Путь к странице',
    });
    expect(within(trail).getByRole('link', { name: 'Языки' })).toHaveAttribute(
      'href',
      '/dictionaries/languages',
    );
    // Every shell control stays, fully named.
    for (const name of [
      'Меню',
      'Организация: Пример организации 1',
      'Проект: Пример проекта 1',
      'Команда: Пример команды 1',
      'Профиль: Иван Петров',
    ]) {
      expect(within(banner).getByRole('button', { name })).toBeInTheDocument();
    }
  });
});
