import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { Route, Routes } from 'react-router-dom';
import { server } from '../../../test/mocks/server';
import { renderWithProviders } from '../../../test/renderWithProviders';
import { AuthContext, type AuthContextValue } from '../../auth/AuthContext';
import { dictionariesPermissions } from '../permissions';
import { sampleLanguages } from '../testFixtures';
import { LanguagesRegistryPage } from './LanguagesRegistryPage';

const api = 'http://localhost:5000';
const languagesUrl = `${api}/api/dictionaries/languages`;
const defaultUrl = `${api}/api/dictionaries/languages/default`;

// Ручной стаб `can` (а не реальный токен через `AuthProvider`, как в
// `LanguageFormPage.test.tsx`) — этот файл исторически ставит свой
// собственный `AuthContext.Provider`; менять его ради единообразия —
// отдельная правка.
function authValue(permissions: string[]): AuthContextValue {
  return {
    status: 'authenticated',
    reason: null,
    loginFailedCode: null,
    displayName: 'Иванов Иван',
    can: (code) => permissions.includes(code),
    signIn: vi.fn(),
    signOut: vi.fn(),
    retryBootstrap: vi.fn(),
    retrySignOut: vi.fn(),
  };
}

function pageWithRoutes() {
  return (
    <Routes>
      <Route path="/dictionaries/languages" element={<LanguagesRegistryPage />} />
      <Route path="/dictionaries/languages/new" element={<div>NEW_LANGUAGE_FORM</div>} />
      <Route path="/dictionaries/languages/:code" element={<div>EDIT_LANGUAGE_FORM</div>} />
    </Routes>
  );
}

function renderPage(permissions: string[]) {
  return renderWithProviders(pageWithRoutes(), {
    initialEntries: ['/dictionaries/languages'],
    wrapper: (children) => <AuthContext.Provider value={authValue(permissions)}>{children}</AuthContext.Provider>,
  });
}

function mockList(languages = sampleLanguages) {
  server.use(
    http.get(languagesUrl, () => HttpResponse.json({ count: languages.length, data: languages })),
  );
}

const readOnly = [dictionariesPermissions.languagesRead];
const withWrite = [dictionariesPermissions.languagesRead, dictionariesPermissions.languagesWrite];

describe('LanguagesRegistryPage', () => {
  it('показывает скелетон, пока реестр грузится', async () => {
    mockList();
    renderPage(withWrite);

    expect(screen.getByRole('status', { name: 'Загрузка данных' })).toBeInTheDocument();

    // Дожидаемся, чтобы запрос этого теста не «утёк» в следующий: MSW
    // сопоставляет обработчик асинхронно, и незавершённый на конец теста
    // запрос может быть обслужен уже подменённым в следующем тесте
    // обработчиком (наблюдалось на практике при отладке этого файла).
    await screen.findByText('Русский');
  });

  it('ошибка загрузки — баннер с повтором, повтор восстанавливает список', async () => {
    let attempt = 0;
    server.use(
      http.get(languagesUrl, () => {
        attempt += 1;
        if (attempt === 1) {
          return new HttpResponse(null, { status: 500 });
        }
        return HttpResponse.json({ count: sampleLanguages.length, data: sampleLanguages });
      }),
    );
    renderPage(withWrite);

    expect(await screen.findByText(/Ошибка API/)).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Повторить' }));

    expect(await screen.findByText('Русский')).toBeInTheDocument();
  });

  it('пустой справочник без фильтра — "Справочник пуст" с кнопкой добавления для администратора', async () => {
    mockList([]);
    renderPage(withWrite);

    expect(await screen.findByText('Справочник пуст')).toBeInTheDocument();
    expect(screen.getByText('Добавьте первый язык кнопкой «Добавить язык» выше.')).toBeInTheDocument();
    // Кнопка есть и в шапке, и в самом пустом состоянии.
    expect(screen.getAllByRole('button', { name: 'Добавить язык' })).toHaveLength(2);
  });

  it('пустой справочник без фильтра — читателю кнопка добавления не предлагается', async () => {
    mockList([]);
    renderPage(readOnly);

    expect(await screen.findByText('Справочник пуст')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Добавить язык' })).not.toBeInTheDocument();
  });

  it('фильтр без совпадений — "Ничего не найдено", сброс возвращает список', async () => {
    mockList();
    renderPage(withWrite);
    expect(await screen.findByText('Русский')).toBeInTheDocument();

    server.use(http.get(languagesUrl, () => HttpResponse.json({ count: 0, data: [] })));
    await userEvent.type(screen.getByLabelText('Код'), 'zz');

    expect(await screen.findByText('Ничего не найдено')).toBeInTheDocument();
    expect(screen.getByText('Ни один язык не подходит под заданные условия фильтра.')).toBeInTheDocument();

    mockList();
    await userEvent.click(screen.getByRole('button', { name: 'Сбросить фильтр' }));

    expect(await screen.findByText('Русский')).toBeInTheDocument();
    expect(screen.getByLabelText('Код')).toHaveValue('');
  });

  it('клик по заголовку "Эндоним" запрашивает сортировку по белому списку', async () => {
    const urls: string[] = [];
    server.use(
      http.get(languagesUrl, ({ request }) => {
        urls.push(request.url);
        return HttpResponse.json({ count: sampleLanguages.length, data: sampleLanguages });
      }),
    );
    renderPage(withWrite);
    expect(await screen.findByText('Русский')).toBeInTheDocument();

    const header = screen.getByRole('columnheader', { name: 'Эндоним' });
    await userEvent.click(within(header).getByText('Эндоним'));

    await waitFor(() => expect(urls.some((url) => url.includes('orderBy=endonym'))).toBe(true));
    // Пагинация и размер страницы всегда конкретные значения.
    expect(urls[0]).toMatch(/page=1&pageSize=10/);
  });

  it('заполнение фильтра "Код" отправляет filter=code=*значение, не пустой параметр', async () => {
    const urls: string[] = [];
    server.use(
      http.get(languagesUrl, ({ request }) => {
        urls.push(request.url);
        return HttpResponse.json({ count: sampleLanguages.length, data: sampleLanguages });
      }),
    );
    renderPage(withWrite);
    expect(await screen.findByText('Русский')).toBeInTheDocument();

    await userEvent.type(screen.getByLabelText('Код'), 'kk');

    // `=*` уже сама по себе оператор Contains —
    // без замыкающей `*`, иначе Gridify ищет литеральную звёздочку.
    await waitFor(() => expect(urls.some((url) => url.includes('filter=code%3D*kk'))).toBe(true));
    expect(urls.some((url) => url.includes('filter=code%3D*kk*'))).toBe(false);
  });

  it('читатель видит все строки, но без колонки действий', async () => {
    mockList();
    renderPage(readOnly);

    expect(await screen.findByText('Русский')).toBeInTheDocument();
    expect(screen.getByText('Қазақша')).toBeInTheDocument();
    expect(screen.getByText('Türkçe')).toBeInTheDocument();
    expect(screen.queryByLabelText(/Редактировать/)).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/Сделать языком по умолчанию/)).not.toBeInTheDocument();
  });

  it('администратор видит "Редактировать" на каждой строке и звезду только на активной не-default строке', async () => {
    mockList();
    renderPage(withWrite);
    expect(await screen.findByText('Русский')).toBeInTheDocument();

    expect(screen.getByLabelText('Редактировать ru')).toBeInTheDocument();
    expect(screen.getByLabelText('Редактировать kk')).toBeInTheDocument();
    expect(screen.getByLabelText('Редактировать tr')).toBeInTheDocument();

    // ru — уже default, tr — неактивен: звезды не показываем ни там, ни там.
    expect(screen.queryByLabelText('Сделать языком по умолчанию ru')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Сделать языком по умолчанию tr')).not.toBeInTheDocument();
    expect(screen.getByLabelText('Сделать языком по умолчанию kk')).toBeInTheDocument();
  });

  it('"Редактировать" ведёт на форму языка по коду', async () => {
    mockList();
    renderPage(withWrite);
    expect(await screen.findByText('Русский')).toBeInTheDocument();

    await userEvent.click(screen.getByLabelText('Редактировать kk'));

    expect(await screen.findByText('EDIT_LANGUAGE_FORM')).toBeInTheDocument();
  });

  it('"Добавить язык" в шапке ведёт на форму создания', async () => {
    mockList();
    renderPage(withWrite);
    expect(await screen.findByText('Русский')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Добавить язык' }));

    expect(await screen.findByText('NEW_LANGUAGE_FORM')).toBeInTheDocument();
  });

  it('звезда открывает диалог назначения языка по умолчанию с кодом и эндонимом строки', async () => {
    mockList();
    renderPage(withWrite);
    expect(await screen.findByText('Русский')).toBeInTheDocument();

    await userEvent.click(screen.getByLabelText('Сделать языком по умолчанию kk'));

    expect(screen.getByRole('heading', { name: 'Назначить языком по умолчанию' })).toBeInTheDocument();
    expect(screen.getByText('Сделать «Қазақша» (kk) языком по умолчанию?')).toBeInTheDocument();
  });

  it('успешное назначение закрывает диалог, показывает snackbar и обновляет реестр', async () => {
    mockList();
    server.use(
      http.put(defaultUrl, () =>
        HttpResponse.json({ ...sampleLanguages[1], isDefault: true }),
      ),
    );
    renderPage(withWrite);
    expect(await screen.findByText('Русский')).toBeInTheDocument();

    await userEvent.click(screen.getByLabelText('Сделать языком по умолчанию kk'));
    await userEvent.click(screen.getByRole('button', { name: 'Назначить' }));

    expect(
      await screen.findByText('Язык по умолчанию изменён на «Қазақша» (kk).'),
    ).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Назначить языком по умолчанию' })).not.toBeInTheDocument();
  });

  it('отказ сервера (язык неактивен) остаётся в диалоге и показывает текст сервера', async () => {
    mockList();
    server.use(
      http.put(defaultUrl, () =>
        HttpResponse.json(
          {
            errorCode: 'dictionaries.languages.default_must_be_active',
            detail:
              'Языком по умолчанию может стать только активный язык (код «kk»).',
          },
          { status: 409 },
        ),
      ),
    );
    renderPage(withWrite);
    expect(await screen.findByText('Русский')).toBeInTheDocument();

    await userEvent.click(screen.getByLabelText('Сделать языком по умолчанию kk'));
    await userEvent.click(screen.getByRole('button', { name: 'Назначить' }));

    expect(
      await screen.findByText(
        'Языком по умолчанию может стать только активный язык (код «kk»).',
      ),
    ).toBeInTheDocument();
    // Диалог не закрылся — администратор может прочитать список кодов перед отменой.
    expect(screen.getByRole('heading', { name: 'Назначить языком по умолчанию' })).toBeInTheDocument();
  });

  it('"Отмена" закрывает диалог без запроса', async () => {
    mockList();
    const putSpy = vi.fn();
    server.use(
      http.put(defaultUrl, () => {
        putSpy();
        return HttpResponse.json(sampleLanguages[1]);
      }),
    );
    renderPage(withWrite);
    expect(await screen.findByText('Русский')).toBeInTheDocument();

    await userEvent.click(screen.getByLabelText('Сделать языком по умолчанию kk'));
    await userEvent.click(screen.getByRole('button', { name: 'Отмена' }));

    expect(screen.queryByRole('heading', { name: 'Назначить языком по умолчанию' })).not.toBeInTheDocument();
    expect(putSpy).not.toHaveBeenCalled();
  });
});

describe('LanguagesRegistryPage — самостоятельный гейт, без внешнего RequirePermission', () => {
  // An earlier version of this file had a second
  // describe block that wrapped the exported page in its own extra
  // `RequirePermission` — that only proved the wrapper denies access, not
  // that `LanguagesRegistryPage` denies it on its own, and was removed as a
  // vestigial double gate. This test renders the page
  // exactly as `app/routes.tsx` mounts it (via `renderPage`,
  // `pageWithRoutes()`, no extra wrap) to prove the page's own internal gate
  // is what blocks a session with no `languages.read`.
  it('без languages.read реестр не монтируется и не запрашивает список', async () => {
    let requested = false;
    server.use(
      http.get(languagesUrl, () => {
        requested = true;
        return HttpResponse.json({ count: sampleLanguages.length, data: sampleLanguages });
      }),
    );

    renderPage([]);

    expect(await screen.findByText('Доступ ограничен')).toBeInTheDocument();
    expect(screen.queryByText('Языки')).not.toBeInTheDocument();
    expect(requested).toBe(false);
  });
});

