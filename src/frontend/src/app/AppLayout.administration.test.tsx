import { describe, expect, it } from 'vitest';
import { Route } from 'react-router-dom';
import { screen, within } from '@testing-library/react';
import { ADMIN_ORGANIZATIONS_PATH, organizationsPermissions } from '../features/organizations';
import { PageHeader } from '../shared/ui/PageHeader';
import { authValue, LANGUAGES_READ, renderShell } from '../test/renderShell';

// feat-033 DC-2: «Администрирование» → «Все организации» in the settings navigation, shown by the
// instance code in the token and by nothing else.

const RESCUE = organizationsPermissions.organizationRescue;
const routes = (
  <Route path={ADMIN_ORGANIZATIONS_PATH} element={<PageHeader title="Все организации" />} />
);

describe('feat-033 DC-2: the settings entry «Все организации»', () => {
  it('is listed under «Администрирование» for the holder of the rescue code, with the trail and the marker', async () => {
    renderShell({
      path: ADMIN_ORGANIZATIONS_PATH,
      auth: authValue([LANGUAGES_READ, RESCUE]),
      routes,
    });

    const menu = await screen.findByRole('navigation', { name: 'Меню настроек' });
    expect(within(menu).getByText('Администрирование')).toBeInTheDocument();
    expect(within(menu).getByRole('link', { name: 'Все организации' })).toHaveAttribute(
      'aria-current',
      'page',
    );
    expect(await screen.findByRole('navigation', { name: 'Путь к странице' })).toHaveTextContent(
      'Настройки/Администрирование',
    );
  });

  it('sits between «Справочники» and «Интерфейс», so «Настройки» still lands on the first entry', async () => {
    renderShell({
      path: ADMIN_ORGANIZATIONS_PATH,
      auth: authValue([LANGUAGES_READ, RESCUE]),
      routes,
    });

    const menu = await screen.findByRole('navigation', { name: 'Меню настроек' });
    const labels = within(menu)
      .getAllByRole('link')
      .map((link) => link.textContent);
    expect(labels).toEqual(['Языки', 'Все организации']);
  });

  it('is not listed without the code: no section, no entry', () => {
    renderShell({ path: '/dictionaries/languages', auth: authValue([LANGUAGES_READ]) });

    const menu = screen.getByRole('navigation', { name: 'Меню настроек' });
    expect(within(menu).queryByText('Администрирование')).not.toBeInTheDocument();
    expect(within(menu).queryByRole('link', { name: 'Все организации' })).not.toBeInTheDocument();
  });
});
