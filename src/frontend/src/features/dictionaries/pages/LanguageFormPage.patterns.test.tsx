import { describe, expect, it, vi } from 'vitest';
import { Route, Routes } from 'react-router-dom';
import { HttpResponse, http } from 'msw';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { theme } from '../../../app/theme';
import { server } from '../../../test/mocks/server';
import { renderWithProviders } from '../../../test/renderWithProviders';
import { AuthContext, type AuthContextValue } from '../../auth/AuthContext';
import { dictionariesPermissions } from '../permissions';
import { sampleLanguages } from '../testFixtures';
import { LanguageFormPage } from './LanguageFormPage';

const languagesUrl = 'http://localhost:5000/api/dictionaries/languages';
const ru = sampleLanguages[0]!;

const administrator: AuthContextValue = {
  status: 'authenticated',
  reason: null,
  loginFailedCode: null,
  displayName: 'Иван Петров',
  can: (code) =>
    code === dictionariesPermissions.languagesRead ||
    code === dictionariesPermissions.languagesWrite,
  signIn: vi.fn(),
  signOut: vi.fn(),
  retryBootstrap: vi.fn(),
  retrySignOut: vi.fn(),
};

function renderForm(path: string) {
  return renderWithProviders(
    <Routes>
      <Route path="/dictionaries/languages" element={<div>LANGUAGES_REGISTRY</div>} />
      <Route path="/dictionaries/languages/new" element={<LanguageFormPage />} />
      <Route path="/dictionaries/languages/:code" element={<LanguageFormPage />} />
    </Routes>,
    {
      initialEntries: [path],
      wrapper: (children) => (
        <AuthContext.Provider value={administrator}>{children}</AuthContext.Provider>
      ),
    },
  );
}

function isBefore(first: Element, second: Element): boolean {
  return Boolean(first.compareDocumentPosition(second) & Node.DOCUMENT_POSITION_FOLLOWING);
}

describe('US-9 Scenario: Form page layout', () => {
  it('has the title and no «К списку»; the fields in one column of limited width; «Отмена» and the filled «Добавить» below; «Код» focused', async () => {
    renderForm('/dictionaries/languages/new');

    expect(screen.getByRole('heading', { level: 1, name: 'Добавить язык' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'К списку' })).not.toBeInTheDocument();

    const code = screen.getByLabelText('Код');
    const endonym = screen.getByLabelText('Эндоним');
    const order = screen.getByLabelText('Порядок');
    const form = code.closest('form') as HTMLFormElement;
    expect(form).toContainElement(endonym);
    expect(form).toContainElement(order);
    expect(getComputedStyle(form).maxWidth).toBe(`${theme.layout.formColumnWidth}px`);
    expect(
      screen.getByText('от 2 до 8 символов, формат BCP-47 (например, ru, kk-KZ)'),
    ).toBeInTheDocument();
    expect(screen.getByText('(необязательно)')).toBeInTheDocument();
    expect(screen.getByText('(необязательно, по умолчанию 0)')).toBeInTheDocument();

    const cancel = screen.getByRole('button', { name: 'Отмена' });
    const add = screen.getByRole('button', { name: 'Добавить' });
    expect(isBefore(order, cancel)).toBe(true);
    expect(isBefore(cancel, add)).toBe(true);
    expect(add).toHaveClass('MuiButton-contained');
    expect(cancel).toHaveClass('MuiButton-text');
    expect(code).toHaveFocus();

    await userEvent.click(cancel);

    expect(await screen.findByText('LANGUAGES_REGISTRY')).toBeInTheDocument();
  });
});

describe('US-9 Scenario: Edit form layout', () => {
  it('stands the code, «Эндоним», «Порядок», «Активен» and the stamp in one column with «Отмена» and «Сохранить» below', async () => {
    server.use(http.get(languagesUrl, () => HttpResponse.json({ count: 1, data: [ru] })));
    renderForm('/dictionaries/languages/ru');

    expect(screen.getByRole('heading', { level: 1, name: 'Язык: ru' })).toBeInTheDocument();
    const code = await screen.findByText('Код: ru (нередактируемо)');
    const form = code.closest('form') as HTMLFormElement;
    const stamp = screen.getByText(/Изменено: /);
    for (const element of [
      screen.getByLabelText('Эндоним'),
      screen.getByLabelText('Порядок'),
      screen.getByRole('switch', { name: 'Активен' }),
      stamp,
    ]) {
      expect(form).toContainElement(element);
    }
    const cancel = screen.getByRole('button', { name: 'Отмена' });
    expect(isBefore(stamp, cancel)).toBe(true);
    expect(isBefore(cancel, screen.getByRole('button', { name: 'Сохранить' }))).toBe(true);
  });

  it('puts the comparison panel in place of «Отмена» and «Сохранить» during a version conflict', async () => {
    const user = userEvent.setup();
    server.use(
      http.get(languagesUrl, () => HttpResponse.json({ count: 1, data: [ru] })),
      http.put(`${languagesUrl}/ru`, () =>
        HttpResponse.json({ errorCode: 'dictionaries.version_conflict' }, { status: 409 }),
      ),
    );
    renderForm('/dictionaries/languages/ru');
    await screen.findByDisplayValue('Русский');

    await user.click(screen.getByRole('button', { name: 'Сохранить' }));

    expect(
      await screen.findByText(
        'Запись уже изменена другим пользователем. Сравните значения и выберите, что сохранить.',
      ),
    ).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Отмена' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Сохранить' })).not.toBeInTheDocument();
  });
});

describe('US-9 Scenario: Field and form errors', () => {
  it('shows a field error under «Код» in the error colour and marks the field invalid', async () => {
    const user = userEvent.setup();
    server.use(
      http.post(languagesUrl, () =>
        HttpResponse.json(
          {
            errorCode: 'dictionaries.validation_failed',
            errors: {
              code: [{ code: 'dictionaries.languages.code_invalid', detail: 'Код не подходит.' }],
            },
          },
          { status: 400 },
        ),
      ),
    );
    renderForm('/dictionaries/languages/new');

    await user.type(screen.getByLabelText('Код'), 'xx');
    await user.click(screen.getByRole('button', { name: 'Добавить' }));

    const message = await screen.findByText('Код не подходит.');
    expect(message).toHaveClass('Mui-error');
    expect(screen.getByLabelText('Код')).toHaveAttribute('aria-invalid', 'true');
  });

  it('shows an error not tied to a field in a banner above the first field', async () => {
    const user = userEvent.setup();
    server.use(http.post(languagesUrl, () => new HttpResponse(null, { status: 500 })));
    renderForm('/dictionaries/languages/new');

    await user.type(screen.getByLabelText('Код'), 'xx');
    await user.click(screen.getByRole('button', { name: 'Добавить' }));

    const banner = await screen.findByRole('alert');
    expect(banner).toHaveTextContent(/Ошибка API/);
    expect(isBefore(banner, screen.getByLabelText('Код'))).toBe(true);
  });
});

describe('US-9 Scenario: A busy form cannot be submitted twice', () => {
  it('disables the fields, «Отмена» and «Добавить» while the request runs', async () => {
    const user = userEvent.setup();
    let answer: () => void = () => {};
    server.use(
      http.post(
        languagesUrl,
        () =>
          new Promise<Response>((resolve) => {
            answer = () => resolve(HttpResponse.json({ ...ru, code: 'xx' }, { status: 201 }));
          }),
      ),
      http.get(languagesUrl, () => HttpResponse.json({ count: 1, data: [{ ...ru, code: 'xx' }] })),
    );
    renderForm('/dictionaries/languages/new');

    await user.type(screen.getByLabelText('Код'), 'xx');
    await user.click(screen.getByRole('button', { name: 'Добавить' }));

    await waitFor(() => expect(screen.getByRole('button', { name: 'Добавить' })).toBeDisabled());
    expect(screen.getByRole('button', { name: 'Отмена' })).toBeDisabled();
    expect(screen.getByLabelText('Код')).toBeDisabled();
    expect(screen.getByLabelText('Эндоним')).toBeDisabled();
    expect(screen.getByLabelText('Порядок')).toBeDisabled();

    answer();
    expect(await screen.findByRole('heading', { level: 1, name: 'Язык: xx' })).toBeInTheDocument();
  });
});
