import { describe, expect, it } from 'vitest';
import { HttpResponse, http } from 'msw';
import { screen, within } from '@testing-library/react';
import { server } from '../../../test/mocks/server';
import { OVERVIEW_URL, ORGANIZATIONS_URL, ids, overviewFixture } from '../testFixtures';
import { serveOrganizations } from '../testApi';
import { renderPage } from '../testRender';

// feat-043 QA, US-2 Scenarios «The team block follows her teams», «Empty states by what exists»,
// «Обзор lists are bounded» (BR-NV-9, BR-NV-10): the page renders what the read returns.

const ALFA_OVERVIEW = `/organizations/${ids.alfa}/overview`;

function serveOverview(overview: unknown) {
  server.use(http.get(OVERVIEW_URL, () => HttpResponse.json(overview)));
}

describe('feat-043 QA, US-2: «Обзор» by what the read returns', () => {
  it('shows no «Мои команды» to a user in no team, and both teams first to a member of two', async () => {
    serveOrganizations();
    serveOverview({ ...overviewFixture, myTeams: [] });
    const first = renderPage({ path: ALFA_OVERVIEW });
    await screen.findByLabelText('Количество');
    expect(screen.queryByRole('heading', { name: 'Мои команды' })).not.toBeInTheDocument();
    first.unmount();

    serveOverview({
      ...overviewFixture,
      myTeams: [
        { id: 'a', name: 'MARS', isMember: true },
        { id: 'b', name: 'VENUS', isMember: true },
      ],
    });
    renderPage({ path: ALFA_OVERVIEW });
    const heading = await screen.findByRole('heading', { name: 'Мои команды' });
    const section = heading.closest('section') ?? heading.parentElement!.parentElement!;
    const names = within(section)
      .getAllByRole('link')
      .map((link) => link.textContent);
    expect(names).toEqual(['MARS', 'VENUS']);
  });

  it('an empty organization says so in each list and offers no «Все …» link', async () => {
    serveOrganizations();
    serveOverview({
      departmentCount: 0,
      projectCount: 0,
      teamCount: 0,
      departments: [],
      projects: [],
      teams: [],
      myTeams: [],
    });
    renderPage({ path: ALFA_OVERVIEW });

    expect(await screen.findByText('Департаментов пока нет')).toBeInTheDocument();
    expect(screen.getByText('Проектов пока нет')).toBeInTheDocument();
    expect(screen.getByText('Команд пока нет')).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /^Все департаменты/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /^Все проекты/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /^Все команды/ })).not.toBeInTheDocument();
  });

  it('shows the first rows only and the full count in the link; each figure opens its list', async () => {
    serveOrganizations();
    const rows = (prefix: string) =>
      Array.from({ length: 5 }, (_, index) => ({
        id: `${prefix}${index}`,
        name: `${prefix}${index}`,
        key: `K${index}`,
        isMember: false,
      }));
    serveOverview({
      departmentCount: 1000,
      projectCount: 200,
      teamCount: 200,
      departments: rows('Д'),
      projects: rows('П'),
      teams: rows('К'),
      myTeams: [],
    });
    renderPage({ path: ALFA_OVERVIEW });

    expect(await screen.findByRole('link', { name: 'Все проекты (200)' })).toHaveAttribute(
      'href',
      `/organizations/${ids.alfa}/projects`,
    );
    expect(screen.getByRole('link', { name: 'Все департаменты (1000)' })).toHaveAttribute(
      'href',
      `/organizations/${ids.alfa}/areas`,
    );
    expect(screen.getByRole('link', { name: 'Все команды (200)' })).toHaveAttribute(
      'href',
      `/organizations/${ids.alfa}/teams`,
    );
    expect(screen.queryByText('П5')).not.toBeInTheDocument();
    const figures = screen.getByLabelText('Количество');
    expect(within(figures).getByRole('link', { name: /1000/ })).toHaveAttribute(
      'href',
      `/organizations/${ids.alfa}/areas`,
    );
  });

  it('lists the first five organizations beside it, the current one marked, ending with «Все организации»', async () => {
    serveOrganizations();
    server.use(
      http.get(ORGANIZATIONS_URL, () =>
        HttpResponse.json(
          Array.from({ length: 7 }, (_, index) => ({
            id: index === 0 ? ids.alfa : `a000000${index}-0000-4000-8000-000000000001`,
            name: `Орг ${index}`,
          })),
        ),
      ),
    );
    renderPage({ path: ALFA_OVERVIEW });

    const heading = await screen.findByRole('heading', { name: 'Организации' });
    const section = heading.parentElement!.parentElement!;
    await within(section).findByText('Орг 0');
    expect(within(section).queryByText('Орг 5')).not.toBeInTheDocument();
    expect(within(section).getByText('текущая')).toBeInTheDocument();
    expect(within(section).getByRole('link', { name: 'Все организации' })).toBeInTheDocument();
  });
});
