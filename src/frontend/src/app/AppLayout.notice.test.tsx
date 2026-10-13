import { describe, expect, it } from 'vitest';
import { Link, Route } from 'react-router-dom';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { LANGUAGES_READ, authValue, renderShell } from '../test/renderShell';
import { PageHeader, useNotice } from '../shared/ui';

// tech-015 design § 5.12, K-9: `NoticeProvider` is mounted once in `AppLayout`, so a page's
// `useNotice().show(...)` is drawn bottom left and survives the change of page that raised it.

function Raising() {
  const { show } = useNotice();
  return (
    <>
      <PageHeader title="Языки" />
      <button type="button" onClick={() => show('Изменения сохранены.')}>
        Сохранить
      </button>
      <Link to="/home">На главную страницу</Link>
    </>
  );
}

describe('tech-015 Scenario: AppLayout hosts the notices (design § 5.12)', () => {
  it('draws a notice a page raises, and keeps it across a route change', async () => {
    const user = userEvent.setup();
    renderShell({
      path: '/dictionaries/languages',
      auth: authValue([LANGUAGES_READ]),
      routes: (
        <>
          <Route path="/dictionaries/languages" element={<Raising />} />
          <Route path="/home" element={<PageHeader title="Главная" />} />
        </>
      ),
    });

    await user.click(await screen.findByRole('button', { name: 'Сохранить' }));
    const notice = screen.getByRole('alert');
    expect(notice).toHaveTextContent('Изменения сохранены.');
    expect(notice.closest('.MuiSnackbar-root')).toHaveClass('MuiSnackbar-anchorOriginBottomLeft');

    await user.click(screen.getByRole('link', { name: 'На главную страницу' }));
    expect(await screen.findByRole('heading', { level: 1, name: 'Главная' })).toBeInTheDocument();
    expect(screen.getByRole('alert')).toHaveTextContent('Изменения сохранены.');
  });
});
