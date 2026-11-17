import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ADMIN_CODES } from '../../../test/organizationRoles';
import { PROJECT_URL, exampleDataset, ids, problem } from '../testFixtures';
import { serveOrganizations, serveProblem } from '../testApi';
import { renderPage } from '../testRender';

// feat-041 (doc-016 design § 6.1, BR-TD-12 (c)): «Видим всей организации» in the project's «Параметры».

const SETTINGS = `/organizations/${ids.alfa}/projects/${ids.prm}/settings`;
const VISIBILITY = `/api/organizations/${ids.alfa}/projects/${ids.prm}/visibility`;

async function ready() {
  await screen.findByText('Пример проекта');
}

describe('feat-041 US-6 Scenario: «Видим всей организации»', () => {
  it('shows the switch off with its helper, then asks before turning it on and sends the value', async () => {
    const api = serveOrganizations();
    renderPage({ path: SETTINGS });
    await ready();

    expect(await screen.findByText('Видимость')).toBeInTheDocument();
    expect(
      screen.getByText('Проект и его команды увидят все участники организации.'),
    ).toBeInTheDocument();
    const toggle = screen.getByRole('switch', { name: 'Видим всей организации' });
    expect(toggle).not.toBeChecked();

    await userEvent.click(toggle);
    const dialog = await screen.findByRole('dialog', {
      name: 'Сделать проект «Пример проекта» видимым всей организации?',
    });
    expect(dialog).toHaveTextContent(
      'Проект, его команды и департамент увидят все участники организации, но не другие проекты департамента.',
    );
    expect(api.writes()).toEqual([]);
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сделать видимым' }));

    expect(
      await screen.findByText('Проект «Пример проекта» виден всей организации.'),
    ).toBeVisible();
    expect(api.writes()[0]).toEqual({
      method: 'PUT',
      path: VISIBILITY,
      body: { visibleToOrganization: true },
    });
    await waitFor(() =>
      expect(screen.getByRole('switch', { name: 'Видим всей организации' })).toBeChecked(),
    );
  });

  it('turns it off without a question and notices «виден только участвующим»', async () => {
    const data = exampleDataset();
    data.projects.find((project) => project.id === ids.prm)!.visibleToOrganization = true;
    const api = serveOrganizations(data);
    renderPage({ path: SETTINGS });
    await ready();

    const toggle = await screen.findByRole('switch', { name: 'Видим всей организации' });
    await waitFor(() => expect(toggle).toBeChecked());
    await userEvent.click(toggle);

    expect(
      await screen.findByText('Проект «Пример проекта» виден только участвующим.'),
    ).toBeVisible();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(api.writes()[0]).toEqual({
      method: 'PUT',
      path: VISIBILITY,
      body: { visibleToOrganization: false },
    });
  });

  it('keeps the dialog open with the server’s refusal and leaves the switch off', async () => {
    serveOrganizations();
    serveProblem('put', `${PROJECT_URL}/visibility`, problem('organizations.access.forbidden'));
    renderPage({ path: SETTINGS });
    await ready();

    await userEvent.click(await screen.findByRole('switch', { name: 'Видим всей организации' }));
    const dialog = await screen.findByRole('dialog');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сделать видимым' }));

    expect(await within(dialog).findByText('Недостаточно прав')).toBeInTheDocument();
    expect(
      screen.getByRole('switch', { name: 'Видим всей организации', hidden: true }),
    ).not.toBeChecked();
  });

  it('is absent without organizations.visibility.write, whatever else the role holds', async () => {
    serveOrganizations();
    renderPage({
      path: SETTINGS,
      orgCodes: ADMIN_CODES.filter((code) => code !== 'organizations.visibility.write'),
    });
    await ready();

    expect(screen.getByRole('button', { name: 'Переименовать' })).toBeInTheDocument();
    expect(screen.queryByText('Видимость')).not.toBeInTheDocument();
    expect(screen.queryByRole('switch', { name: 'Видим всей организации' })).toBeNull();
  });
});

describe('feat-041 US-6 Scenario: a hidden project answers as a missing one', () => {
  it('shows the not-found frame, not «Недостаточно прав», when the read answers 404 project.not_found', async () => {
    serveOrganizations();
    serveProblem('get', PROJECT_URL, problem('organizations.project.not_found'));
    renderPage({ path: SETTINGS });

    expect(await screen.findByRole('heading', { name: 'Проект не найден' })).toBeInTheDocument();
    expect(screen.queryByText('Недостаточно прав')).not.toBeInTheDocument();
  });
});
