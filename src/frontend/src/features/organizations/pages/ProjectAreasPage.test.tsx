import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ids } from '../testFixtures';
import { serveOrganizations } from '../testApi';
import { renderPage } from '../testRender';

const PRM_AREAS = `/organizations/${ids.alfa}/projects/${ids.prm}/settings/areas`;
const ANL_AREAS = `/organizations/${ids.alfa}/projects/${ids.anl}/settings/areas`;
const OLD_AREAS = `/organizations/${ids.alfa}/projects/${ids.old}/settings/areas`;

function row(name: string): HTMLElement {
  const found = screen
    .getAllByRole('row')
    .find(
      (candidate) =>
        candidate.hasAttribute('aria-level') &&
        within(candidate).getAllByRole('gridcell')[0]!.textContent?.startsWith(name),
    );
  if (!found) {
    throw new Error(`No row «${name}»`);
  }
  return found;
}

describe('mod-018 design § 7: the project’s «Области»', () => {
  it('shows the project’s own subtree: its node at the root, its sub-areas below', async () => {
    serveOrganizations();
    renderPage({ path: PRM_AREAS });
    await screen.findByRole('treegrid', { name: 'Области' });

    expect(row('Пример проекта')).toHaveAttribute('aria-level', '1');
    expect(row('API')).toHaveAttribute('aria-level', '2');
    expect(row('Шлюз')).toHaveAttribute('aria-level', '3');
    // Neither the department nor another project’s node is in it.
    expect(screen.queryByText('Платформа')).not.toBeInTheDocument();
    expect(screen.queryByText('Аналитика')).not.toBeInTheDocument();
  });

  it('creates a sub-area under the project’s node from the primary action', async () => {
    const api = serveOrganizations();
    renderPage({ path: PRM_AREAS });
    await screen.findByRole('treegrid', { name: 'Области' });

    await userEvent.click(screen.getByRole('button', { name: 'Новая вложенная область' }));
    const dialog = await screen.findByRole('dialog', { name: 'Новая вложенная область' });
    await userEvent.type(within(dialog).getByRole('textbox', { name: /Название/ }), 'iOS');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    expect(await screen.findByText('Область «iOS» создана.')).toBeInTheDocument();
    expect(api.writes()[0]?.body).toEqual({ name: 'iOS', parentId: ids.prmRoot });
    await waitFor(() => expect(row('iOS')).toHaveAttribute('aria-level', '2'));
  });

  it('says a project with no sub-area has none, and how to create one', async () => {
    serveOrganizations();
    renderPage({ path: ANL_AREAS });

    expect(
      await screen.findByText(
        'У проекта пока нет вложенных областей. Создайте первую кнопкой «Новая вложенная область».',
      ),
    ).toBeInTheDocument();
  });

  it('tells a reader there is none and offers no command', async () => {
    serveOrganizations();
    renderPage({ path: ANL_AREAS, role: 'viewer' });

    expect(await screen.findByText('У проекта нет вложенных областей.')).toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: 'Новая вложенная область' }),
    ).not.toBeInTheDocument();
  });

  it('answers «Проект не найден» for a deactivated project', async () => {
    serveOrganizations();
    renderPage({ path: OLD_AREAS });

    expect(await screen.findByRole('heading', { level: 1, name: /не найден/ })).toBeInTheDocument();
  });
});
