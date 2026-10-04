import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { Route, Routes } from 'react-router-dom';
import { server } from '../../../test/mocks/server';
import { renderWithProviders } from '../../../test/renderWithProviders';
import { AuthContext, type AuthContextValue } from '../../auth/AuthContext';
import { LanguagesRegistryPage } from '../pages/LanguagesRegistryPage';
import { dictionariesPermissions } from '../permissions';
import { sampleLanguages } from '../testFixtures';

const languagesUrl = 'http://localhost:5000/api/dictionaries/languages';
const defaultUrl = `${languagesUrl}/default`;

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

/** The registry, so the dialog opens from its real trigger, the row's star. */
async function openFromRegistry(user: ReturnType<typeof userEvent.setup>) {
  server.use(
    http.get(languagesUrl, () =>
      HttpResponse.json({ count: sampleLanguages.length, data: sampleLanguages }),
    ),
  );
  renderWithProviders(
    <Routes>
      <Route path="/dictionaries/languages" element={<LanguagesRegistryPage />} />
    </Routes>,
    {
      initialEntries: ['/dictionaries/languages'],
      wrapper: (children) => (
        <AuthContext.Provider value={administrator}>{children}</AuthContext.Provider>
      ),
    },
  );
  await screen.findByText('Русский');
  const opener = screen.getByRole('button', { name: 'Сделать языком по умолчанию kk' });
  await user.click(opener);
  return { opener, dialog: await screen.findByRole('dialog') };
}

describe('US-9 Scenario: Dialog anatomy — «Назначить языком по умолчанию»', () => {
  it('has its title, «Закрыть» in the header, its text and «Отмена» left of «Назначить», the only filled button', async () => {
    const user = userEvent.setup();
    const { dialog } = await openFromRegistry(user);

    expect(dialog).toHaveAccessibleName('Назначить языком по умолчанию');
    expect(
      within(dialog).getByRole('heading', { level: 2, name: 'Назначить языком по умолчанию' }),
    ).toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: 'Закрыть' })).toBeInTheDocument();
    expect(
      within(dialog).getByText('Сделать «Қазақша» (kk) языком по умолчанию?'),
    ).toBeInTheDocument();
    const buttons = within(dialog).getAllByRole('button');
    const cancel = within(dialog).getByRole('button', { name: 'Отмена' });
    const assign = within(dialog).getByRole('button', { name: 'Назначить' });
    expect(buttons.indexOf(cancel)).toBeLessThan(buttons.indexOf(assign));
    expect(buttons.filter((button) => button.classList.contains('MuiButton-contained'))).toEqual([
      assign,
    ]);
  });
});

describe('US-9 Scenario: Backing out of a dialog — «Назначить языком по умолчанию»', () => {
  it.each([
    ['Escape', (user: ReturnType<typeof userEvent.setup>) => user.keyboard('{Escape}')],
    [
      '«Закрыть»',
      (user: ReturnType<typeof userEvent.setup>) =>
        user.click(screen.getByRole('button', { name: 'Закрыть' })),
    ],
    [
      '«Отмена»',
      (user: ReturnType<typeof userEvent.setup>) =>
        user.click(screen.getByRole('button', { name: 'Отмена' })),
    ],
  ])(
    '%s closes it with nothing changed and returns focus to the star that opened it',
    async (_way, backOut) => {
      const user = userEvent.setup();
      const assign = vi.fn();
      server.use(
        http.put(defaultUrl, () => {
          assign();
          return HttpResponse.json(sampleLanguages[1]);
        }),
      );
      const { opener } = await openFromRegistry(user);

      await backOut(user);

      await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
      expect(assign).not.toHaveBeenCalled();
      await waitFor(() => expect(opener).toHaveFocus());
    },
  );
});

describe('US-9 Scenario: A busy dialog cannot be dismissed — «Назначить языком по умолчанию»', () => {
  it('disables «Отмена», «Закрыть» and «Назначить», shows progress, and ignores Escape and a click outside', async () => {
    const user = userEvent.setup();
    let answer: () => void = () => {};
    server.use(
      http.put(
        defaultUrl,
        () =>
          new Promise<Response>((resolve) => {
            answer = () => resolve(HttpResponse.json({ ...sampleLanguages[1], isDefault: true }));
          }),
      ),
    );
    const { dialog } = await openFromRegistry(user);

    await user.click(within(dialog).getByRole('button', { name: 'Назначить' }));

    await waitFor(() =>
      expect(within(dialog).getByRole('button', { name: 'Назначить' })).toBeDisabled(),
    );
    expect(within(dialog).getByRole('button', { name: 'Отмена' })).toBeDisabled();
    expect(within(dialog).getByRole('button', { name: 'Закрыть' })).toBeDisabled();
    expect(within(dialog).getByRole('progressbar')).toBeInTheDocument();

    await user.keyboard('{Escape}');
    await user.click(document.querySelector('.MuiBackdrop-root') as Element);
    expect(screen.getByRole('dialog')).toBeInTheDocument();

    answer();
    expect(
      await screen.findByText('Язык по умолчанию изменён на «Қазақша» (kk).'),
    ).toBeInTheDocument();
  });
});
