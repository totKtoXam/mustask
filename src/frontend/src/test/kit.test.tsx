import { describe, expect, it } from 'vitest';
import { screen } from '@testing-library/react';
import { ids } from '../features/organizations/testFixtures';
import { apiUrl, hold, problem, problemText, recordRequests, serveProblem } from './api';
import { datasetOf, fixtureId, organizationOf, projectOf, teamOf } from './fixtures';
import { renderPage } from './renderPage';

// tech-035: the frontend kit has logic of its own, so it is tested: the helpers must fail a test when they
// should, or a green suite proves nothing. The API helpers are exercised through `fetch`, with no screen.

describe('apiUrl', () => {
  it('joins the API base and a route, with or without the leading slash', () => {
    expect(apiUrl('/api/organizations')).toBe('http://localhost:5000/api/organizations');
    expect(apiUrl('api/organizations')).toBe('http://localhost:5000/api/organizations');
  });
});

describe('problem', () => {
  it('takes the status and the text from the catalog and fills the parameters in', () => {
    const body = problem('dictionaries.languages.default_must_be_active', { code: 'kk' });

    expect(body.status).toBe(409);
    expect(body.errorCode).toBe('dictionaries.languages.default_must_be_active');
    expect(body.detail).toBe('Языком по умолчанию может стать только активный язык (код «kk»).');
    expect(body.params).toEqual({ code: { value: 'kk', type: 'string' } });
  });

  it('carries no params when there are none, and lets the status be overridden', () => {
    const body = problem('organizations.organization.not_found', {}, 410);

    expect(body.params).toBeUndefined();
    expect(body.status).toBe(410);
    expect(body.detail).toBe(problemText('organizations.organization.not_found'));
  });

  it('refuses a code the catalog does not have, so a retired code fails the test that names it', () => {
    expect(() => problem('organizations.no_such_code')).toThrow(
      /No code organizations\.no_such_code/,
    );
    expect(() => problemText('nope')).toThrow();
  });

  it('refuses a text whose parameter the test did not fill, so no placeholder is expected as wording', () => {
    expect(() => problemText('organizations.organization.name_taken')).toThrow(/no value for {name}/);
  });
});

describe('serveProblem', () => {
  it('answers every request with the problem body and the problem content type', async () => {
    const body = serveProblem('get', '/api/kit/thing', 'organizations.organization.not_found');

    const response = await fetch(apiUrl('/api/kit/thing'));

    expect(response.status).toBe(404);
    expect(response.headers.get('Content-Type')).toContain('application/problem+json');
    expect(await response.json()).toEqual(body);
    expect((await fetch(apiUrl('/api/kit/thing'))).status).toBe(404);
  });

  it('answers once only when asked: the next request falls through to the next handler', async () => {
    recordRequests('post', '/api/kit/once');
    serveProblem(
      'post',
      '/api/kit/once',
      'organizations.organization.not_found',
      {},
      { once: true },
    );

    expect((await fetch(apiUrl('/api/kit/once'), { method: 'POST' })).status).toBe(404);
    expect((await fetch(apiUrl('/api/kit/once'), { method: 'POST' })).status).toBe(204);
  });
});

describe('recordRequests', () => {
  it('keeps method, path, query and parsed body of what was sent, in order', async () => {
    const { requests } = recordRequests('put', '/api/kit/:id/name', () => ({ ok: true }));

    const response = await fetch(apiUrl('/api/kit/42/name?x=1'), {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ name: 'Новое' }),
    });

    expect(await response.json()).toEqual({ ok: true });
    expect(requests).toEqual([
      { method: 'PUT', path: '/api/kit/42/name', search: '?x=1', body: { name: 'Новое' } },
    ]);
  });

  it('stays empty when a different route was called, which is how a test says no request was sent', async () => {
    const { requests } = recordRequests('delete', '/api/kit/never');
    recordRequests('get', '/api/kit/other');

    await fetch(apiUrl('/api/kit/other'));

    expect(requests).toEqual([]);
  });

  it('answers 204 by default and a given Response as it is', async () => {
    recordRequests('get', '/api/kit/plain');
    recordRequests('get', '/api/kit/custom', () => new Response('x', { status: 418 }));

    expect((await fetch(apiUrl('/api/kit/plain'))).status).toBe(204);
    expect((await fetch(apiUrl('/api/kit/custom'))).status).toBe(418);
  });
});

describe('hold', () => {
  it('keeps the answer pending until released, and says when the request arrived', async () => {
    const held = hold('get', '/api/kit/slow', { done: true });
    let settled = false;

    const pending = fetch(apiUrl('/api/kit/slow')).then(async (response) => {
      settled = true;
      return response.json();
    });
    await held.reached;
    await new Promise((resolve) => setTimeout(resolve, 20));
    expect(settled).toBe(false);

    held.release();

    expect(await pending).toEqual({ done: true });
    expect(settled).toBe(true);
  });
});

describe('fixtures', () => {
  it('builds ids by one scheme, the one the organizations dataset already uses', () => {
    expect(fixtureId('organization', 1)).toBe(ids.alfa);
    expect(fixtureId('project', 2)).toBe(ids.anl);
    expect(fixtureId('team', 1)).toBe(ids.prmTeam);
    expect(fixtureId('area', 2)).toBe(ids.platformArea);
    expect(fixtureId('user', 12)).toBe('e0000000-0000-4000-8000-000000000012');
  });

  it('refuses a number that is not a positive integer', () => {
    expect(() => fixtureId('team', 0)).toThrow(/numbered from 1/);
    expect(() => fixtureId('team', 1.5)).toThrow();
  });

  it('ties a project and a team to their organization, by record or by id, and lets a field be overridden', () => {
    const organization = organizationOf(3, { name: 'Омега' });

    expect(organization).toEqual({ id: fixtureId('organization', 3), name: 'Омега', active: true });
    expect(projectOf(1, organization).organizationId).toBe(organization.id);
    expect(teamOf(1, organization.id, { deleted: true })).toMatchObject({
      organizationId: organization.id,
      deleted: true,
    });
  });

  it('gives a dataset only what the test names', () => {
    const organization = organizationOf(1);

    expect(datasetOf({ organizations: [organization] })).toEqual({
      organizations: [organization],
      projects: [],
      teams: [],
      links: [],
      areas: [],
    });
  });
});

describe('renderPage', () => {
  it('opens the real route with the data it was given, as the administrator by default', async () => {
    const organization = organizationOf(1);
    renderPage(`/organizations/${organization.id}/projects`, {
      data: datasetOf({
        organizations: [organization],
        projects: [projectOf(1, organization, { name: 'Гамма-проект' })],
      }),
    });

    expect(await screen.findByText('Гамма-проект')).toBeInTheDocument();
    expect(screen.getByTestId('location')).toHaveTextContent(
      `/organizations/${organization.id}/projects`,
    );
  });

  it('serves the access of the role it was given, so a page the role lacks the code for is closed', async () => {
    const organization = organizationOf(1);
    renderPage(`/organizations/${organization.id}/projects?status=deactivated`, {
      role: 'viewer',
      data: datasetOf({ organizations: [organization] }),
    });

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Доступ ограничен' }),
    ).toBeInTheDocument();
  });
});
