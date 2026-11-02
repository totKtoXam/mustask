import { describe, expect, it } from 'vitest';
import { screen, within } from '@testing-library/react';
import { OVERVIEW_URL, ids, problem } from '../testFixtures';
import { serveOrganizations, serveProblem } from '../testApi';
import { renderPage } from '../testRender';

const ALFA_OVERVIEW = `/organizations/${ids.alfa}/overview`;

describe('feat-043 design § 5.1: «Обзор» of the organization (US-2)', () => {
  it('loads, then shows the figures, the first rows and «Все …» links to the lists', async () => {
    serveOrganizations();
    renderPage({ path: ALFA_OVERVIEW });

    expect(await screen.findByRole('heading', { level: 1, name: 'Обзор' })).toBeInTheDocument();
    const figures = await screen.findByLabelText('Количество');
    expect(within(figures).getByRole('link', { name: /Проекты/ })).toHaveAttribute(
      'href',
      `/organizations/${ids.alfa}/projects`,
    );
    expect(screen.getByRole('link', { name: /^Все проекты \(\d+\)$/ })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Все организации' })).toBeInTheDocument();
  });

  it('fails with «Повторить» and reads again', async () => {
    serveOrganizations();
    serveProblem('get', OVERVIEW_URL, problem('organizations.validation_failed'), { once: true });
    renderPage({ path: ALFA_OVERVIEW });

    await screen.findByRole('button', { name: 'Повторить' });
  });

  it('answers «Организация не найдена» for an unknown organization', async () => {
    serveOrganizations();
    renderPage({ path: '/organizations/a0000000-0000-4000-8000-0000000000ff/overview' });

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Организация не найдена' }),
    ).toBeInTheDocument();
  });
});
