import { describe, expect, it } from 'vitest';
import { Route, Routes } from 'react-router-dom';
import { HttpResponse, http } from 'msw';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderWithProviders } from '../../../test/renderWithProviders';
import { server } from '../../../test/mocks/server';
import { AuthProvider } from '../../auth/AuthContext';
import { fakeAccessTokenWithPermissions } from '../testFixtures';
import { dictionariesPermissions } from '../permissions';
import { LanguageFormPage } from './LanguageFormPage';

const api = 'http://localhost:5000';

function tokenHandler(permissions: string[]) {
  return http.post(`${api}/api/session/token`, () =>
    HttpResponse.json({
      accessToken: fakeAccessTokenWithPermissions(permissions as (typeof dictionariesPermissions)[keyof typeof dictionariesPermissions][]),
      expiresAt: new Date(Date.now() + 900_000).toISOString(),
      expiresIn: 900,
    }),
  );
}

function renderForm(initialEntry: string) {
  return renderWithProviders(
    <Routes>
      <Route path="/dictionaries/languages" element={<div>LANGUAGES_REGISTRY</div>} />
      <Route path="/dictionaries/languages/new" element={<LanguageFormPage />} />
      <Route path="/dictionaries/languages/:code" element={<LanguageFormPage />} />
    </Routes>,
    { initialEntries: [initialEntry], wrapper: (children) => <AuthProvider>{children}</AuthProvider> },
  );
}

const ru = {
  code: 'ru',
  endonym: 'Русский',
  isDefault: true,
  isActive: true,
  sortOrder: 10,
  version: '1001',
  updatedAt: '2026-09-12T07:12:00Z',
  updatedBy: 'admin',
};

describe('LanguageFormPage — доступ', () => {
  it('без languages.write создание отвечает AccessDenied', async () => {
    server.use(tokenHandler([]));
    renderForm('/dictionaries/languages/new');

    expect(await screen.findByText('Доступ ограничен')).toBeInTheDocument();
    expect(screen.queryByLabelText('Код')).not.toBeInTheDocument();
  });

  it('без languages.write редактирование по прямой ссылке тоже отвечает AccessDenied', async () => {
    server.use(tokenHandler([dictionariesPermissions.languagesRead]));
    renderForm('/dictionaries/languages/ru');

    expect(await screen.findByText('Доступ ограничен')).toBeInTheDocument();
  });

  it('с languages.write форма создания рендерится', async () => {
    server.use(tokenHandler([dictionariesPermissions.languagesWrite]));
    renderForm('/dictionaries/languages/new');

    expect(await screen.findByText('Добавить язык')).toBeInTheDocument();
    expect(screen.getByLabelText('Код')).toBeInTheDocument();
    expect(screen.queryByLabelText('Активен')).not.toBeInTheDocument();
  });
});

describe('LanguageFormPage — создание', () => {
  it('успешное создание переходит на форму редактирования только что созданного языка', async () => {
    const user = userEvent.setup();
    server.use(
      tokenHandler([dictionariesPermissions.languagesWrite]),
      http.post(`${api}/api/dictionaries/languages`, async ({ request }) => {
        const body = (await request.json()) as { code: string; endonym?: string; sortOrder?: number };
        return HttpResponse.json(
          {
            code: body.code,
            endonym: body.endonym ?? '',
            isDefault: false,
            isActive: false,
            sortOrder: body.sortOrder ?? 0,
            version: '2001',
            updatedAt: '2026-09-12T08:00:00Z',
            updatedBy: 'admin',
          },
          { status: 201 },
        );
      }),
      http.get(`${api}/api/dictionaries/languages`, () =>
        HttpResponse.json({
          count: 1,
          data: [
            {
              code: 'tr',
              endonym: 'Türkçe',
              isDefault: false,
              isActive: false,
              sortOrder: 40,
              version: '2001',
              updatedAt: '2026-09-12T08:00:00Z',
              updatedBy: 'admin',
            },
          ],
        }),
      ),
    );

    renderForm('/dictionaries/languages/new');

    await user.type(await screen.findByLabelText('Код'), 'tr');
    await user.click(screen.getByRole('button', { name: 'Добавить' }));

    expect(await screen.findByText('Язык: tr')).toBeInTheDocument();
    expect(await screen.findByDisplayValue('Türkçe')).toBeInTheDocument();
  });

  it('дублирующийся код отклоняется на поле «Код», без перехода на другой экран', async () => {
    const user = userEvent.setup();
    server.use(
      tokenHandler([dictionariesPermissions.languagesWrite]),
      http.post(`${api}/api/dictionaries/languages`, () =>
        HttpResponse.json(
          { errorCode: 'dictionaries.languages.already_exists', detail: 'Язык с кодом «ru» уже существует.' },
          { status: 409 },
        ),
      ),
    );

    renderForm('/dictionaries/languages/new');

    await user.type(await screen.findByLabelText('Код'), 'ru');
    await user.click(screen.getByRole('button', { name: 'Добавить' }));

    expect(await screen.findByText('Язык с кодом «ru» уже существует.')).toBeInTheDocument();
    expect(screen.getByText('Добавить язык')).toBeInTheDocument();
  });

  it('невалидный код блокируется на клиенте, запрос не отправляется', async () => {
    const user = userEvent.setup();
    let postCalled = false;
    server.use(
      tokenHandler([dictionariesPermissions.languagesWrite]),
      http.post(`${api}/api/dictionaries/languages`, () => {
        postCalled = true;
        return HttpResponse.json({}, { status: 201 });
      }),
    );

    renderForm('/dictionaries/languages/new');

    await user.type(await screen.findByLabelText('Код'), 'р');
    await user.click(screen.getByRole('button', { name: 'Добавить' }));

    expect(
      await screen.findByText(
        'Код должен содержать от 2 до 8 символов и соответствовать формату BCP-47 (например, ru, kk-KZ).',
      ),
    ).toBeInTheDocument();
    expect(postCalled).toBe(false);
  });
});

describe('LanguageFormPage — редактирование', () => {
  it('во время начальной загрузки показывает скелетон формы', async () => {
    server.use(
      tokenHandler([dictionariesPermissions.languagesWrite]),
      http.get(`${api}/api/dictionaries/languages`, async () => {
        await new Promise((resolve) => setTimeout(resolve, 50));
        return HttpResponse.json({ count: 1, data: [ru] });
      }),
    );

    renderForm('/dictionaries/languages/ru');

    expect(await screen.findByRole('status', { name: 'Загрузка данных' })).toBeInTheDocument();
  });

  it('ошибку начальной загрузки показывает ErrorBanner с повтором', async () => {
    const user = userEvent.setup();
    // The stub fails until the test itself lifts the failure, rather than
    // failing only the first request: counting attempts made this flaky,
    // because any second fetch before the assertion turned the error into a
    // success and the retry button never appeared.
    let failing = true;
    server.use(
      tokenHandler([dictionariesPermissions.languagesWrite]),
      http.get(`${api}/api/dictionaries/languages`, () =>
        failing
          ? HttpResponse.json({ errorCode: 'dictionaries.unexpected' }, { status: 500 })
          : HttpResponse.json({ count: 1, data: [ru] }),
      ),
    );

    renderForm('/dictionaries/languages/ru');

    const retry = await screen.findByRole('button', { name: 'Повторить' });
    failing = false;
    await user.click(retry);

    expect(await screen.findByDisplayValue('Русский')).toBeInTheDocument();
  });

  it('загружает язык и показывает эндоним/порядок/активность/дату изменения', async () => {
    server.use(tokenHandler([dictionariesPermissions.languagesWrite]), http.get(`${api}/api/dictionaries/languages`, () => HttpResponse.json({ count: 1, data: [ru] })));

    renderForm('/dictionaries/languages/ru');

    expect(await screen.findByText('Код: ru (нередактируемо)')).toBeInTheDocument();
    expect(screen.getByDisplayValue('Русский')).toBeInTheDocument();
    expect(screen.getByDisplayValue('10')).toBeInTheDocument();
    expect(screen.getByRole('switch', { name: 'Активен' })).toBeChecked();
    expect(screen.getByText(/Изменено: 12\.09\.2026/)).toBeInTheDocument();
  });

  it('успешное сохранение обновляет черновик из ответа сервера', async () => {
    const user = userEvent.setup();
    server.use(
      tokenHandler([dictionariesPermissions.languagesWrite]),
      http.get(`${api}/api/dictionaries/languages`, () => HttpResponse.json({ count: 1, data: [ru] })),
      http.put(`${api}/api/dictionaries/languages/ru`, async ({ request }) => {
        const body = (await request.json()) as { endonym: string; sortOrder: number; isActive: boolean };
        return HttpResponse.json({ ...ru, ...body, version: '1002', updatedBy: 'admin2' });
      }),
    );

    renderForm('/dictionaries/languages/ru');
    const endonymInput = await screen.findByDisplayValue('Русский');
    await user.clear(endonymInput);
    await user.type(endonymInput, 'Русский язык');
    await user.click(screen.getByRole('button', { name: 'Сохранить' }));

    expect(await screen.findByDisplayValue('Русский язык')).toBeInTheDocument();
    expect(await screen.findByText(/admin2/)).toBeInTheDocument();
  });

  it('активация без эндонима: ошибка на поле «Эндоним», переключатель возвращается назад', async () => {
    const user = userEvent.setup();
    const inactive = { ...ru, isActive: false, endonym: '' };
    server.use(
      tokenHandler([dictionariesPermissions.languagesWrite]),
      http.get(`${api}/api/dictionaries/languages`, () => HttpResponse.json({ count: 1, data: [inactive] })),
      http.put(`${api}/api/dictionaries/languages/ru`, () =>
        HttpResponse.json(
          {
            errorCode: 'dictionaries.validation_failed',
            errors: {
              endonym: [
                {
                  code: 'dictionaries.languages.endonym_required',
                  detail: 'У активного языка должно быть указано название на этом языке.',
                },
              ],
            },
          },
          { status: 400 },
        ),
      ),
    );

    renderForm('/dictionaries/languages/ru');
    const toggle = await screen.findByRole('switch', { name: 'Активен' });
    await user.click(toggle);
    await user.click(screen.getByRole('button', { name: 'Сохранить' }));

    expect(
      await screen.findByText('У активного языка должно быть указано название на этом языке.'),
    ).toBeInTheDocument();
    expect(toggle).not.toBeChecked();
  });

  it('деактивация языка по умолчанию: Alert сверху с добавленной подсказкой, переключатель возвращается на «включено»', async () => {
    const user = userEvent.setup();
    server.use(
      tokenHandler([dictionariesPermissions.languagesWrite]),
      http.get(`${api}/api/dictionaries/languages`, () => HttpResponse.json({ count: 1, data: [ru] })),
      http.put(`${api}/api/dictionaries/languages/ru`, () =>
        HttpResponse.json(
          {
            errorCode: 'dictionaries.languages.default_cannot_be_deactivated',
            detail: 'Нельзя деактивировать язык по умолчанию.',
          },
          { status: 409 },
        ),
      ),
    );

    renderForm('/dictionaries/languages/ru');
    const toggle = await screen.findByRole('switch', { name: 'Активен' });
    await user.click(toggle);
    await user.click(screen.getByRole('button', { name: 'Сохранить' }));

    expect(
      await screen.findByText(
        'Нельзя деактивировать язык по умолчанию. Сначала назначьте языком по умолчанию другой язык — кнопка «Сделать языком по умолчанию» в реестре языков.',
      ),
    ).toBeInTheDocument();
    expect(toggle).toBeChecked();
  });

  it('конфликт версии показывает VersionConflictPanel с полями Эндоним/Порядок/Активность после перечитывания', async () => {
    const user = userEvent.setup();
    let getCalls = 0;
    server.use(
      tokenHandler([dictionariesPermissions.languagesWrite]),
      http.get(`${api}/api/dictionaries/languages`, () => {
        getCalls += 1;
        if (getCalls === 1) {
          return HttpResponse.json({ count: 1, data: [ru] });
        }
        return HttpResponse.json({
          count: 1,
          data: [{ ...ru, endonym: 'Русский (сервер)', version: '9999', updatedBy: 'admin2' }],
        });
      }),
      http.put(`${api}/api/dictionaries/languages/ru`, () =>
        HttpResponse.json({ errorCode: 'dictionaries.version_conflict' }, { status: 409 }),
      ),
    );

    renderForm('/dictionaries/languages/ru');
    const endonymInput = await screen.findByDisplayValue('Русский');
    await user.clear(endonymInput);
    await user.type(endonymInput, 'Русский (моё)');
    await user.click(screen.getByRole('button', { name: 'Сохранить' }));

    expect(
      await screen.findByText('Запись уже изменена другим пользователем. Сравните значения и выберите, что сохранить.'),
    ).toBeInTheDocument();
    expect(screen.getByText('Русский (моё)')).toBeInTheDocument();
    expect(screen.getByText('Русский (сервер)')).toBeInTheDocument();
    await waitFor(() => expect(getCalls).toBe(2));
  });

  // "own fields stay visible and editable" уже доказано
  // выше по этому же сценарию, но ни один тест не утверждал, что правка,
  // набранная именно во время сравнения, а не замороженное до-конфликтное
  // значение, действительно уходит в переотправленный запрос — это и есть
  // "слияние вручную", а не просто видимое, но бессмысленное поле.
  it('версионный конфликт: правка, набранная во время сравнения, уходит в "Сохранить свой вариант" со свежей версией (слияние вручную)', async () => {
    const user = userEvent.setup();
    let putCalls = 0;
    let secondBody: { endonym: string; sortOrder: number; isActive: boolean } | null = null;
    let secondIfMatch: string | null = null;
    server.use(
      tokenHandler([dictionariesPermissions.languagesWrite]),
      http.get(`${api}/api/dictionaries/languages`, () => HttpResponse.json({ count: 1, data: [ru] })),
      http.put(`${api}/api/dictionaries/languages/ru`, async ({ request }) => {
        putCalls += 1;
        if (putCalls === 1) {
          return HttpResponse.json({ errorCode: 'dictionaries.version_conflict' }, { status: 409 });
        }
        secondBody = (await request.json()) as { endonym: string; sortOrder: number; isActive: boolean };
        secondIfMatch = request.headers.get('If-Match');
        return HttpResponse.json({ ...ru, ...secondBody, version: '10000', updatedBy: 'admin3' });
      }),
    );

    renderForm('/dictionaries/languages/ru');
    const endonymInput = await screen.findByDisplayValue('Русский');
    await user.clear(endonymInput);
    await user.type(endonymInput, 'Русский (до конфликта)');
    await user.click(screen.getByRole('button', { name: 'Сохранить' }));

    expect(
      await screen.findByText('Запись уже изменена другим пользователем. Сравните значения и выберите, что сохранить.'),
    ).toBeInTheDocument();

    const endonymDuringConflict = screen.getByDisplayValue('Русский (до конфликта)');
    await user.clear(endonymDuringConflict);
    await user.type(endonymDuringConflict, 'Русский (правка во время сравнения)');
    await user.click(screen.getByRole('button', { name: 'Сохранить свой вариант' }));

    await waitFor(() => expect(putCalls).toBe(2));
    expect(secondBody).toEqual({
      endonym: 'Русский (правка во время сравнения)',
      sortOrder: 10,
      isActive: true,
    });
    expect(secondIfMatch).toBe('1001');
  });
});
