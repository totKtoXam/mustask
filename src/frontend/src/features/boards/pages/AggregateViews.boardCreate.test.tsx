import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { theme } from '../../../app/theme';
import { server } from '../../../test/mocks/server';
import { rgb } from '../../../test/kitTesting';
import type { WorkContextSource } from '../../../shared/lib/workContext';
import { API } from '../../workitems/testFixtures';
import { ids } from '../testFixtures';
import { teamContext } from '../testRender';
import {
  ORG,
  header,
  mskCard,
  orgAddress,
  renderAggregate,
  serveAggregate,
  storyColumns,
} from '../testAggregate';

// feat-060 (D-1): the aggregate board's status-category stripe and dot per column, the type stripe
// per card, and «Новый рабочий элемент» with a required «Проект» among the projects of the view's
// scope. The dialog's fields are the kit's, which are slow in jsdom.

vi.setConfig({ testTimeout: 60_000 });

afterEach(() => localStorage.clear());

const ORG_API = `${API}/api/organizations/${ORG}`;
const BOARD = [header('project', 'p-msk', 'Мастаск', 1, { key: 'MSK', columns: storyColumns() })];

/** Мастаск sits in the department «Логистика»; Операции at the top level. */
function contextWithOffers(): WorkContextSource {
  return {
    ...teamContext({ projectId: undefined, teamId: undefined }),
    offers: {
      departments: [{ id: 'd-log', name: 'Логистика' }],
      projects: [
        { id: ids.msk, name: 'Мастаск', key: 'MSK', departmentId: 'd-log', teamIds: [] },
        { id: ids.ops, name: 'Операции', key: 'OPS', departmentId: null, teamIds: [] },
      ],
      teams: [],
    },
  };
}

function serveCreation() {
  const posts: { path: string; body: unknown }[] = [];
  server.use(
    http.get(`${ORG_API}/projects/:projectId/teams`, () =>
      HttpResponse.json([
        { id: 't-plat', name: 'Платформа', defaultMark: 'named', isMember: true },
      ]),
    ),
    http.get(`${ORG_API}/projects/:projectId/areas`, ({ params }) =>
      HttpResponse.json([
        {
          id: 'area-1',
          parentId: null,
          type: 'project',
          projectId: params.projectId,
          projectActive: true,
          name: 'Корень',
          description: null,
          path: 'Корень',
          depth: 1,
        },
      ]),
    ),
    http.post(`${ORG_API}/projects/:projectId/workitems`, async ({ request }) => {
      posts.push({ path: new URL(request.url).pathname, body: await request.json() });
      return HttpResponse.json({ id: 'row-9' }, { status: 201 });
    }),
  );
  return posts;
}

const NEW_ITEM = 'Новый рабочий элемент';

async function openDialog() {
  await userEvent.click(await screen.findByRole('button', { name: NEW_ITEM }));
  return screen.findByRole('dialog', { name: NEW_ITEM });
}

async function optionsOf(box: HTMLElement): Promise<string[]> {
  await userEvent.click(box);
  const names = within(await screen.findByRole('listbox'))
    .getAllByRole('option')
    .map((option) => option.textContent ?? '');
  await userEvent.keyboard('{Escape}');
  await waitFor(() => expect(screen.queryByRole('listbox')).toBeNull());
  return names;
}

describe('feat-060 Scenario: the colours of the aggregate board', () => {
  it('draws the category stripe and dot on each column and the type stripe on each card', async () => {
    serveAggregate({
      groups: { project: BOARD },
      cardsOf: (_group, column) => (column === 'Proposed-1' ? [mskCard(1)] : []),
    });
    renderAggregate(orgAddress('boards'));
    await screen.findByText('Элемент 1');

    const proposed = screen.getByRole('group', { name: 'Новая' });
    const active = screen.getByRole('group', { name: 'Активна' });
    expect(getComputedStyle(proposed).borderTopStyle).toBe('solid');
    expect(getComputedStyle(proposed).borderTopWidth).not.toBe('');
    // Two categories, two colours.
    expect(getComputedStyle(proposed).borderTopColor).not.toBe(
      getComputedStyle(active).borderTopColor,
    );
    expect(proposed.querySelector('.WorkTrackingCategoryMarker')).not.toBeNull();
    expect(active.querySelector('.WorkTrackingCategoryMarker')).not.toBeNull();

    const card = document.querySelector('[data-drag-card="MSK-US-1"]') as HTMLElement;
    expect(getComputedStyle(card).borderLeftStyle).toBe('solid');
    expect(getComputedStyle(card).borderLeftColor).toBe(
      rgb(theme.workTracking.typeAppearance('story').colour),
    );
  });
});

describe('feat-060 Scenario: «Новый рабочий элемент» on the aggregate board', () => {
  it('is in the header of a user who may write, and not on the aggregate backlog', async () => {
    serveAggregate({ groups: { project: BOARD }, cardsOf: () => [] });
    const board = renderAggregate(orgAddress('boards'), { context: contextWithOffers() });
    expect(await screen.findByRole('button', { name: NEW_ITEM })).toBeVisible();
    board.unmount();

    serveAggregate({ groups: { project: BOARD }, rowsOf: () => [] });
    renderAggregate(orgAddress('backlogs'), { context: contextWithOffers() });
    await screen.findByRole('button', { name: /Группировка/ });
    expect(screen.queryByRole('button', { name: NEW_ITEM })).toBeNull();
  });

  it('is absent on the aggregate «Рабочие элементы»', async () => {
    serveAggregate({
      groups: { none: [header('none', null, null, 0)] },
      list: { count: 0, data: [] },
    });
    renderAggregate(orgAddress('workitems'), { context: contextWithOffers() });
    await screen.findByRole('heading', { name: 'Рабочие элементы' });
    expect(screen.queryByRole('button', { name: NEW_ITEM })).toBeNull();
  });

  it('offers only the projects the user may write in', async () => {
    serveAggregate({ groups: { project: BOARD }, cardsOf: () => [] });
    serveCreation();
    // Write on Мастаск only: Операции carries no code.
    server.use(
      http.get(`${ORG_API}/projects`, () =>
        HttpResponse.json([
          { id: ids.msk, name: 'Мастаск', key: 'MSK', codes: ['workitems.workitem.write'] },
          { id: 'p-msk', name: 'Мастаск', key: 'MSK', codes: ['workitems.workitem.write'] },
          { id: ids.ops, name: 'Операции', key: 'OPS', codes: [] },
        ]),
      ),
    );
    renderAggregate(orgAddress('boards'), { context: contextWithOffers() });

    const found = await openDialog();
    const project = await within(found).findByRole('combobox', { name: 'Проект' });
    expect(await optionsOf(project)).toEqual(['Мастаск']);
  });

  it('is absent for a viewer, who holds no write code on any project', async () => {
    serveAggregate({ groups: { project: BOARD }, cardsOf: () => [] });
    renderAggregate(orgAddress('boards'), { role: 'viewer', context: contextWithOffers() });
    await screen.findByRole('button', { name: /Группировка/ });
    expect(screen.queryByRole('button', { name: NEW_ITEM })).toBeNull();
  });

  it('offers every active project of the organization, required, none chosen; nothing is sent without one', async () => {
    serveAggregate({ groups: { project: BOARD }, cardsOf: () => [] });
    const posts = serveCreation();
    renderAggregate(orgAddress('boards'), { context: contextWithOffers() });

    const found = await openDialog();
    const project = await within(found).findByRole('combobox', { name: 'Проект' });
    expect(project).toHaveValue('');
    expect(project).toHaveAttribute('aria-required', 'true');
    expect(await optionsOf(project)).toEqual(['Мастаск', 'Операции']);

    await userEvent.type(within(found).getByRole('textbox', { name: /Заголовок/ }), 'Новая');
    await userEvent.click(within(found).getByRole('button', { name: 'Создать' }));
    expect(await within(found).findByText('Выберите проект')).toBeVisible();
    expect(posts).toEqual([]);
  });

  it('offers only the department’s projects on a department board', async () => {
    serveAggregate({ groups: { project: BOARD }, cardsOf: () => [] });
    serveCreation();
    renderAggregate(`${orgAddress('boards')}?departmentId=d-log`, {
      context: contextWithOffers(),
    });

    const found = await openDialog();
    const project = await within(found).findByRole('combobox', { name: 'Проект' });
    expect(await optionsOf(project)).toEqual(['Мастаск']);
  });

  it('creates in the chosen project, with its default team, and the item shows on the board', async () => {
    let created = false;
    const api = serveAggregate({
      groups: { project: BOARD },
      cardsOf: (_group, column) => (created && column === 'Proposed-1' ? [mskCard(9)] : []),
    });
    const posts = serveCreation();
    renderAggregate(orgAddress('boards'), { context: contextWithOffers() });

    const found = await openDialog();
    const project = await within(found).findByRole('combobox', { name: 'Проект' });
    await userEvent.click(project);
    await userEvent.click(await screen.findByRole('option', { name: 'Мастаск' }));
    await waitFor(() =>
      expect(within(found).getByRole('combobox', { name: 'Команда' })).toHaveValue('Платформа'),
    );
    await userEvent.type(within(found).getByRole('textbox', { name: /Заголовок/ }), 'Новая');
    created = true;
    await userEvent.click(within(found).getByRole('button', { name: 'Создать' }));

    await waitFor(() => expect(posts).toHaveLength(1));
    expect(posts[0]?.path).toBe(`/api/organizations/${ORG}/projects/${ids.msk}/workitems`);
    expect(posts[0]?.body).toMatchObject({ title: 'Новая', team: { teamId: 't-plat' } });
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    expect(await screen.findByText('Элемент 9')).toBeInTheDocument();
    expect(api.of('cells').length).toBeGreaterThan(2);
  });
});
