import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { Route } from 'react-router-dom';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { LanguagesRegistryPage } from '../features/dictionaries';
import { sampleLanguages } from '../features/dictionaries/testFixtures';
import { server } from '../test/mocks/server';
import { authValue, LANGUAGES_READ, renderShell } from '../test/renderShell';

const languagesUrl = 'http://localhost:5000/api/dictionaries/languages';

describe('US-5 Scenario: Screens of this task ignore the context (BR-18)', () => {
  const requests: string[] = [];
  const onRequest = ({ request }: { request: Request }) => {
    requests.push(request.url);
  };
  beforeEach(() => {
    requests.length = 0;
    server.use(
      http.get(languagesUrl, () =>
        HttpResponse.json({ count: sampleLanguages.length, data: sampleLanguages }),
      ),
    );
    server.events.on('request:start', onRequest);
  });
  afterEach(() => {
    server.events.removeListener('request:start', onRequest);
  });

  it('keeps the address, the rows and the navigation of «Языки», and sends no new request', async () => {
    renderShell({
      path: '/dictionaries/languages',
      auth: authValue([LANGUAGES_READ]),
      routes: <Route path="/dictionaries/languages" element={<LanguagesRegistryPage />} />,
    });
    expect(await screen.findByText('Русский')).toBeInTheDocument();
    const rows = screen.getAllByRole('row').map((row) => row.textContent);
    const navigation = screen.getByRole('navigation', { name: 'Меню настроек' }).innerHTML;
    const requestsBefore = requests.length;
    expect(requestsBefore).toBeGreaterThan(0);

    await userEvent.click(screen.getByRole('button', { name: /^Организация:/ }));
    await userEvent.click(
      within(screen.getByRole('menu')).getByRole('menuitemradio', { name: 'Пример организации 2' }),
    );

    expect(
      screen.getByRole('button', { name: 'Организация: Пример организации 2' }),
    ).toBeInTheDocument();
    expect(screen.getByTestId('location')).toHaveTextContent('/dictionaries/languages');
    expect(screen.getAllByRole('row').map((row) => row.textContent)).toEqual(rows);
    expect(screen.getByRole('navigation', { name: 'Меню настроек' }).innerHTML).toBe(navigation);
    expect(requests).toHaveLength(requestsBefore);
  });
});
