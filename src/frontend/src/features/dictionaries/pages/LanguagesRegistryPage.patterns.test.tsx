import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { Route, Routes } from 'react-router-dom';
import { theme } from '../../../app/theme';
import { server } from '../../../test/mocks/server';
import { renderWithProviders } from '../../../test/renderWithProviders';
import { AuthContext, type AuthContextValue } from '../../auth/AuthContext';
import type { LanguageResponse } from '../api/dictionariesApi';
import { dictionariesPermissions } from '../permissions';
import { sampleLanguages } from '../testFixtures';
import { LanguagesRegistryPage } from './LanguagesRegistryPage';

const languagesUrl = 'http://localhost:5000/api/dictionaries/languages';
const reader = [dictionariesPermissions.languagesRead];
const administrator = [
  dictionariesPermissions.languagesRead,
  dictionariesPermissions.languagesWrite,
];

function authValue(permissions: string[]): AuthContextValue {
  return {
    status: 'authenticated',
    reason: null,
    loginFailedCode: null,
    displayName: 'Иван Петров',
    can: (code) => permissions.includes(code),
    signIn: vi.fn(),
    signOut: vi.fn(),
    retryBootstrap: vi.fn(),
    retrySignOut: vi.fn(),
  };
}

function renderRegistry(permissions: string[]) {
  return renderWithProviders(
    <Routes>
      <Route path="/dictionaries/languages" element={<LanguagesRegistryPage />} />
      <Route path="/dictionaries/languages/new" element={<div>NEW_LANGUAGE_FORM</div>} />
    </Routes>,
    {
      initialEntries: ['/dictionaries/languages'],
      wrapper: (children) => (
        <AuthContext.Provider value={authValue(permissions)}>{children}</AuthContext.Provider>
      ),
    },
  );
}

/** Answers every list request with `languages`, and records the request URLs. */
function serveLanguages(languages: LanguageResponse[] = sampleLanguages) {
  const urls: string[] = [];
  server.use(
    http.get(languagesUrl, ({ request }) => {
      urls.push(request.url);
      return HttpResponse.json({ count: languages.length, data: languages });
    }),
  );
  return urls;
}

/** `#F3F3F3` → `rgb(243, 243, 243)`, the form jsdom's computed style reports. */
function rgb(hex: string): string {
  const digits = hex.replace('#', '');
  const [red, green, blue] = [0, 2, 4].map((offset) =>
    parseInt(digits.slice(offset, offset + 2), 16),
  );
  return `rgb(${red}, ${green}, ${blue})`;
}

function isBefore(first: Element, second: Element): boolean {
  return Boolean(first.compareDocumentPosition(second) & Node.DOCUMENT_POSITION_FOLLOWING);
}

function filterBar() {
  return screen.getByRole('group', { name: 'Фильтры' });
}

describe('US-6 Scenario: Title and command bar', () => {
  it('shows «Языки» as the level-1 heading and «Добавить язык» as the filled primary with a leading plus', async () => {
    serveLanguages();
    renderRegistry(administrator);

    expect(screen.getByRole('heading', { level: 1, name: 'Языки' })).toBeInTheDocument();
    const primary = screen.getByRole('button', { name: 'Добавить язык' });
    expect(primary).toHaveClass('MuiButton-contained');
    expect(within(primary).getByTestId('AddIcon')).toBeInTheDocument();
    await screen.findByText('Русский');
  });
});

describe('US-6 Scenario: A reader sees no write command (BR-9)', () => {
  it('holds no «Добавить язык» and keeps the command bar’s height', async () => {
    serveLanguages();
    const { container } = renderRegistry(reader);
    await screen.findByText('Русский');

    expect(screen.queryByRole('button', { name: 'Добавить язык' })).not.toBeInTheDocument();
    const bar = container.querySelector('.PageHeader-commands') as Element;
    expect(getComputedStyle(bar).minHeight).toBe(`${theme.density.controlHeight}px`);
  });
});

describe('US-7 Scenario: Loading is a skeleton of the content (BR-12)', () => {
  it('shows the header, the filter bar and placeholder rows under the real column headers, with no page spinner', async () => {
    serveLanguages();
    renderRegistry(reader);

    const header = screen.getByRole('heading', { level: 1, name: 'Языки' });
    const status = screen.getByRole('status', { name: 'Загрузка данных' });
    expect(isBefore(header, filterBar())).toBe(true);
    expect(isBefore(filterBar(), status)).toBe(true);
    for (const column of ['Код', 'Эндоним', 'По умолчанию', 'Активность', 'Порядок', 'Изменено']) {
      expect(within(status).getByText(column)).toBeInTheDocument();
    }
    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
    await screen.findByText('Русский');
  });
});

describe('US-7 Scenario: Error with retry', () => {
  it('shows the shared error component with «Повторить» in place of the table, header and filter bar kept', async () => {
    let failing = true;
    server.use(
      http.get(languagesUrl, () =>
        failing
          ? new HttpResponse(null, { status: 500 })
          : HttpResponse.json({ count: sampleLanguages.length, data: sampleLanguages }),
      ),
    );
    renderRegistry(reader);

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent(/Ошибка API/);
    expect(screen.getByRole('heading', { level: 1, name: 'Языки' })).toBeInTheDocument();
    expect(filterBar()).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();

    failing = false;
    await userEvent.click(within(alert).getByRole('button', { name: 'Повторить' }));

    expect(await screen.findByText('Русский')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
});

describe('US-7 Scenario: Empty, for an administrator', () => {
  it('replaces the table, its header row and paging with the frame, which offers «Добавить язык»', async () => {
    serveLanguages([]);
    renderRegistry(administrator);

    const heading = await screen.findByRole('heading', { level: 2, name: 'Справочник пуст' });
    expect(
      screen.getByText('Добавьте первый язык кнопкой «Добавить язык» выше.'),
    ).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1, name: 'Языки' })).toBeInTheDocument();
    expect(isBefore(filterBar(), heading)).toBe(true);
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
    expect(screen.queryByRole('columnheader')).not.toBeInTheDocument();
    expect(screen.queryByText(/из \d+/)).not.toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();

    const [, frameAction] = screen.getAllByRole('button', { name: 'Добавить язык' });
    expect(frameAction).toHaveClass('MuiButton-outlined');
    await userEvent.click(frameAction as HTMLElement);

    expect(await screen.findByText('NEW_LANGUAGE_FORM')).toBeInTheDocument();
  });
});

describe('US-7 Scenario: The empty state offers only what the user may do (BR-9)', () => {
  it('tells a reader «Языки ещё не добавлены.» and offers no button', async () => {
    serveLanguages([]);
    renderRegistry(reader);

    expect(
      await screen.findByRole('heading', { level: 2, name: 'Справочник пуст' }),
    ).toBeInTheDocument();
    expect(screen.getByText('Языки ещё не добавлены.')).toBeInTheDocument();
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });
});

describe('US-7 Scenario: Empty after a filter', () => {
  it('keeps the header and the filter with its value, shows the frame, and «Сбросить фильтр» clears every filter to page 1', async () => {
    const urls = serveLanguages();
    renderRegistry(administrator);
    await screen.findByText('Русский');

    server.use(http.get(languagesUrl, () => HttpResponse.json({ count: 0, data: [] })));
    await userEvent.type(screen.getByLabelText('Код'), 'zz');

    expect(
      await screen.findByRole('heading', { level: 2, name: 'Ничего не найдено' }),
    ).toBeInTheDocument();
    expect(
      screen.getByText('Ни один язык не подходит под заданные условия фильтра.'),
    ).toBeInTheDocument();
    expect(screen.getByLabelText('Код')).toHaveValue('zz');
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: 'Сбросить фильтр' })).toHaveLength(1);

    const answered = serveLanguages();
    await userEvent.click(screen.getByRole('button', { name: 'Сбросить фильтр' }));

    expect(await screen.findByText('Русский')).toBeInTheDocument();
    expect(screen.getByLabelText('Код')).toHaveValue('');
    expect(answered.at(-1)).toMatch(/page=1&pageSize=10/);
    expect(answered.at(-1)).not.toMatch(/filter=/);
    expect(urls.length).toBeGreaterThan(0);
  });
});

describe('US-8 Scenario: Filter bar above the table', () => {
  it('sits between the header and the table with the five fields, and no clear control while no filter is set', async () => {
    serveLanguages();
    renderRegistry(reader);
    const table = await screen.findByRole('table', { name: 'Реестр языков' });

    const bar = filterBar();
    expect(isBefore(screen.getByRole('heading', { level: 1, name: 'Языки' }), bar)).toBe(true);
    expect(isBefore(bar, table)).toBe(true);
    for (const label of ['Код', 'Эндоним', 'По умолчанию', 'Активность', 'Порядок']) {
      expect(within(bar).getByLabelText(label)).toBeInTheDocument();
    }
    expect(screen.queryByRole('button', { name: 'Очистить фильтры' })).not.toBeInTheDocument();
  });
});

describe('US-8 Scenario: Clearing all filters from the bar', () => {
  it('offers «Очистить фильтры» while filters are set; it empties every filter, shows page 1 and goes', async () => {
    const urls = serveLanguages();
    renderRegistry(reader);
    await screen.findByText('Русский');

    await userEvent.click(screen.getByLabelText('Активность'));
    await userEvent.click(await screen.findByRole('option', { name: 'Неактивные' }));
    await userEvent.type(screen.getByLabelText('Код'), 'k');

    const clear = await screen.findByRole('button', { name: 'Очистить фильтры' });
    expect(clear).toHaveClass('MuiButton-text');
    urls.length = 0;

    await userEvent.click(clear);

    await waitFor(() => expect(urls.at(-1)).not.toMatch(/filter=/));
    expect(urls.at(-1)).toMatch(/page=1&pageSize=10/);
    expect(screen.getByLabelText('Код')).toHaveValue('');
    expect(within(filterBar()).getByRole('combobox', { name: 'Активность' })).not.toHaveTextContent(
      'Неактивные',
    );
    expect(screen.queryByRole('button', { name: 'Очистить фильтры' })).not.toBeInTheDocument();
  });
});

describe('US-8 Scenario: Two ways out of an empty filter, never two controls of one name', () => {
  it('offers «Очистить фильтры» in the bar and «Сбросить фильтр» in the frame, and either clears', async () => {
    serveLanguages();
    renderRegistry(reader);
    await screen.findByText('Русский');
    server.use(http.get(languagesUrl, () => HttpResponse.json({ count: 0, data: [] })));
    await userEvent.type(screen.getByLabelText('Код'), 'zz');
    await screen.findByRole('heading', { level: 2, name: 'Ничего не найдено' });

    expect(screen.getAllByRole('button', { name: 'Очистить фильтры' })).toHaveLength(1);
    expect(screen.getAllByRole('button', { name: 'Сбросить фильтр' })).toHaveLength(1);

    serveLanguages();
    await userEvent.click(screen.getByRole('button', { name: 'Очистить фильтры' }));

    expect(await screen.findByText('Русский')).toBeInTheDocument();
    expect(screen.getByLabelText('Код')).toHaveValue('');
  });
});

describe('US-8 Scenario: Dense table', () => {
  it('puts the headers on a distinct header row, highlights the row under the pointer and keeps rows dense', async () => {
    serveLanguages();
    renderRegistry(reader);
    const table = await screen.findByRole('table', { name: 'Реестр языков' });

    const headerCell = within(table).getByRole('columnheader', { name: 'Код' });
    expect(getComputedStyle(headerCell).backgroundColor).toBe(rgb(theme.palette.shell.tableHeader));
    const row = within(table).getByText('Русский').closest('tr') as HTMLElement;
    expect(row).toHaveClass('MuiTableRow-hover');
    expect(parseFloat(getComputedStyle(row).height)).toBeLessThanOrEqual(40);
  });
});

describe('US-8 Scenario: Sorting is visible', () => {
  it('asks for code ascending, then descending, and the header announces each through aria-sort', async () => {
    const urls = serveLanguages();
    renderRegistry(reader);
    const table = await screen.findByRole('table', { name: 'Реестр языков' });
    expect(within(table).getByRole('columnheader', { name: 'Код' })).not.toHaveAttribute(
      'aria-sort',
    );

    await userEvent.click(within(table).getByRole('button', { name: 'Код' }));

    await waitFor(() => expect(urls.at(-1)).toMatch(/orderBy=code(&|$)/));
    expect(within(table).getByRole('columnheader', { name: 'Код' })).toHaveAttribute(
      'aria-sort',
      'ascending',
    );

    await userEvent.click(within(table).getByRole('button', { name: 'Код' }));

    await waitFor(() => expect(urls.at(-1)).toMatch(/orderBy=code(\+|%20)desc/));
    expect(within(table).getByRole('columnheader', { name: 'Код' })).toHaveAttribute(
      'aria-sort',
      'descending',
    );
  });
});

describe('US-8 Scenario: Row actions are reachable without a pointer', () => {
  it('names the icon buttons by code and reaches them by Tab', async () => {
    const user = userEvent.setup();
    serveLanguages();
    renderRegistry(administrator);
    await screen.findByText('Русский');

    const edit = screen.getByRole('button', { name: 'Редактировать ru' });
    expect(screen.getByRole('button', { name: 'Сделать языком по умолчанию kk' })).toBeVisible();

    let reached = false;
    for (let step = 0; step < 30 && !reached; step += 1) {
      await user.tab();
      reached = document.activeElement === edit;
    }
    expect(reached).toBe(true);
  });
});

describe('US-8 Scenario: Paging in Russian', () => {
  it('reads «1–10 из 25» and «11–20 из 25» on the next page', async () => {
    const all: LanguageResponse[] = Array.from({ length: 25 }, (_, index) => ({
      ...sampleLanguages[1]!,
      code: `l${String(index).padStart(2, '0')}`,
      endonym: `Язык ${index}`,
      isDefault: false,
    }));
    server.use(
      http.get(languagesUrl, ({ request }) => {
        const page = Number(new URL(request.url).searchParams.get('page'));
        return HttpResponse.json({
          count: all.length,
          data: all.slice((page - 1) * 10, page * 10),
        });
      }),
    );
    renderRegistry(reader);

    expect(await screen.findByText('1–10 из 25')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: /следующ/i }));

    expect(await screen.findByText('11–20 из 25')).toBeInTheDocument();
  });
});
