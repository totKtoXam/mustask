import { describe, expect, it } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AREA_URL, ORG_TEAM_URL, PROJECT_URL, exampleDataset, ids, problem } from '../testFixtures';
import { serveOrganizations, serveProblem } from '../testApi';
import { renderPage } from '../testRender';

// feat-041 frontend QA: a hidden object answers 404 with the code of a missing one, so every
// not-found frame appears and «Недостаточно прав» never does (US-6 «A hidden object does not exist»);
// turning the switch off reports a refusal as an error notice.

describe('feat-041 QA: hidden objects show the not-found frame of their kind', () => {
  it('a hidden team page', async () => {
    serveOrganizations();
    serveProblem('get', ORG_TEAM_URL, problem('organizations.team.not_found'));
    renderPage({ path: `/organizations/${ids.alfa}/teams/${ids.platformTeam}/settings` });

    expect(await screen.findByRole('heading', { name: 'Команда не найдена' })).toBeInTheDocument();
    expect(screen.queryByText('Недостаточно прав')).not.toBeInTheDocument();
  });

  it('a hidden department or area', async () => {
    serveOrganizations();
    serveProblem('get', AREA_URL, problem('organizations.area.not_found'));
    renderPage({ path: `/organizations/${ids.alfa}/areas/${ids.platformArea}/overview` });

    expect(await screen.findByRole('heading', { name: 'Область не найдена' })).toBeInTheDocument();
    expect(screen.queryByText('Недостаточно прав')).not.toBeInTheDocument();
  });
});

describe('feat-041 QA: turning the switch off is refused', () => {
  it('shows the refusal as an error notice and the switch stays on', async () => {
    const data = exampleDataset();
    data.projects.find((project) => project.id === ids.prm)!.visibleToOrganization = true;
    serveOrganizations(data);
    serveProblem('put', `${PROJECT_URL}/visibility`, problem('organizations.access.forbidden'));
    renderPage({ path: `/organizations/${ids.alfa}/projects/${ids.prm}/settings` });

    const toggle = await screen.findByRole('switch', { name: 'Видим всей организации' });
    await userEvent.click(toggle);

    expect(await screen.findByText('Недостаточно прав')).toBeInTheDocument();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(screen.getByRole('switch', { name: 'Видим всей организации' })).toBeChecked();
    expect(
      screen.queryByText('Проект «Пример проекта» виден только участвующим.'),
    ).not.toBeInTheDocument();
  });
});
