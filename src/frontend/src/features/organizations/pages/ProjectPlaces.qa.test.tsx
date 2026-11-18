import { describe, expect, it } from 'vitest';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { server } from '../../../test/mocks/server';
import { TEAMS_URL, exampleDataset, ids } from '../testFixtures';
import { serveOrganizations } from '../testApi';
import { renderPage } from '../testRender';

// mod-022 frontend QA (doc-016 BR-TD-4, BR-TD-6; US-5): what the developer's tests leave out — the project
// move with no department to go to, the refusal's rows for a caller who may not unlink, and the stale
// refusal after the last unlink.

const GENERAL = `/organizations/${ids.alfa}/projects/${ids.prm}/general`;

async function openMove(data = exampleDataset()) {
  const api = serveOrganizations(data);
  renderPage({ path: GENERAL, role: 'admin' });
  await userEvent.click(await screen.findByRole('button', { name: 'Переместить' }));
  return { api, dialog: await screen.findByRole('dialog', { name: /^Перемещение проекта/ }) };
}

describe('mod-022 QA: «Перемещение проекта»', () => {
  it('with no other department says so and offers no field (BR-TD-6: never the top level)', async () => {
    const data = exampleDataset();
    data.areas = data.areas.filter((node) => node.id !== ids.reportsArea);
    const { dialog } = await openMove(data);

    expect(
      await within(dialog).findByText(
        'Нет другого департамента, в который вы можете переместить проект.',
      ),
    ).toBeVisible();
    expect(within(dialog).queryByRole('combobox')).toBeNull();
    expect(within(dialog).getByRole('button', { name: 'Переместить' })).toBeDisabled();
  });

  it('a linked team the caller may not unlink is named in the refusal with no «Отключить» (the server decides, BR-TD-4)', async () => {
    const { api, dialog } = await openMove();
    server.use(
      http.get(TEAMS_URL, () =>
        HttpResponse.json([
          {
            id: ids.platformTeam,
            name: 'Платформа',
            defaultMark: null,
            isMember: false,
            canUnlink: false,
          },
        ]),
      ),
    );
    await userEvent.click(within(dialog).getByRole('combobox', { name: /Новое расположение/ }));
    await userEvent.click(await screen.findByRole('option', { name: 'Отчёты' }));
    await userEvent.click(within(dialog).getByRole('button', { name: 'Переместить' }));

    expect(await within(dialog).findByText('Платформа')).toBeVisible();
    expect(within(dialog).getByText('Команды:')).toBeVisible();
    expect(within(dialog).queryByRole('button', { name: /^Отключить/ })).toBeNull();
    expect(api.writes().map((write) => write.method)).toEqual(['PUT']);
  });
});
