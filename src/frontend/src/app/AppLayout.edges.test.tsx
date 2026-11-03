import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { authValue, LANGUAGES_READ, renderShell } from '../test/renderShell';
import { setViewportWidth } from '../test/viewport';
import { initials } from '../shared/lib/initials';

/**
 * QA additions (mod-015, stage 7, qa.md § Additional scenarios A1, A3, A9,
 * A10, A21): edges of the shell the stories do not spell out.
 */

const reader = [LANGUAGES_READ];

function mainNavigation() {
  return screen.getByRole('navigation', { name: 'Основное меню' });
}

/** Expanded: labels are drawn beside the icons; a rail draws none. */
function isExpanded() {
  return within(mainNavigation()).queryByText('Главная') !== null;
}

afterEach(() => {
  vi.restoreAllMocks();
});

describe('QA A1: the width boundaries of BR-10, with no kept choice', () => {
  it.each([
    [1280, 'expanded'],
    [1279, 'rail'],
    [1024, 'rail'],
    [1023, 'overlay'],
  ] as const)('at %i px the main navigation is %s', (width, expected) => {
    setViewportWidth(width);
    renderShell({ auth: authValue(reader) });

    if (expected === 'overlay') {
      expect(screen.getByRole('button', { name: 'Меню' })).toBeInTheDocument();
      expect(screen.queryByRole('navigation', { name: 'Основное меню' })).not.toBeInTheDocument();
    } else {
      expect(screen.queryByRole('button', { name: 'Меню' })).not.toBeInTheDocument();
      expect(isExpanded()).toBe(expected === 'expanded');
    }
  });
});

describe('QA A3: browser storage that refuses to be read or written', () => {
  it('renders the width default, and the toggle still works for the session', async () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new DOMException('blocked', 'SecurityError');
    });
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new DOMException('blocked', 'SecurityError');
    });
    const user = userEvent.setup();
    renderShell({ auth: authValue(reader) });
    expect(isExpanded()).toBe(true);

    await user.click(screen.getByRole('button', { name: 'Свернуть меню' }));

    expect(isExpanded()).toBe(false);
    expect(screen.getByRole('button', { name: 'Развернуть меню' })).toHaveAttribute(
      'aria-expanded',
      'false',
    );
  });
});

describe('QA A9: initials of names the outline does not list', () => {
  it.each([
    ['Анна-Мария Иванова', 'АИ'],
    ['иван петров', 'ИП'],
    ['\tИван\nПетров', 'ИП'],
  ])('«%s» → «%s»', (name, expected) => {
    expect(initials(name)).toBe(expected);
  });
});

describe('QA A10: choosing the organization that is already current', () => {
  it('opens its «Обзор» with «все» in the rest, as ADR-0032 § 5 words every organization choice', async () => {
    const user = userEvent.setup();
    renderShell({
      auth: authValue(reader),
      contextSource: {
        status: 'ready',
        organizations: [{ id: 'org-1', name: 'Пример организации 1', projects: [], teams: [] }],
        selection: { organizationId: 'org-1', projectId: 'prj-1', teamId: 'team-1' },
        isReal: true,
      },
    });
    const levels = () => within(screen.getByRole('group', { name: 'Контекст работы' }));

    await user.click(levels().getByRole('combobox', { name: 'Организация: Пример организации 1' }));
    await user.click(await screen.findByRole('option', { name: 'Пример организации 1' }));

    expect(screen.getByTestId('location')).toHaveTextContent('/organizations/org-1/overview');
  });
});

describe('QA A21: an address no page serves, beneath «Языки», below 1024 px (qa.md D-2)', () => {
  it('opens the overlay with the main navigation labelled, no «Меню настроек» and nothing marked', async () => {
    setViewportWidth(900);
    const user = userEvent.setup();
    renderShell({ path: '/dictionaries/languages/ru/extra', auth: authValue(reader) });
    expect(
      screen.getByRole('heading', { level: 1, name: 'Страница не найдена' }),
    ).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Меню' }));

    expect(isExpanded()).toBe(true);
    expect(screen.queryByRole('navigation', { name: 'Меню настроек' })).not.toBeInTheDocument();
    expect(mainNavigation().querySelectorAll('[aria-current]')).toHaveLength(0);
  });
});
