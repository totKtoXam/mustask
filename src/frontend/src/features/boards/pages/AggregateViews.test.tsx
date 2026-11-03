import { afterEach, describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse } from 'msw';
import {
  ORG,
  header,
  mskRow,
  orgAddress,
  renderAggregate,
  serveAggregate,
  storyColumns,
} from '../testAggregate';

// feat-048 US-10, US-11: the aggregate backlog, board and «Рабочие элементы» at the organization
// and department level (design § 2, § 3, § 5): the switch, its default and memory, the group's own
// read, «Показать ещё», a 429 and the cursor reload.

afterEach(() => localStorage.clear());

const GROUPS = {
  project: [
    header('project', 'p-msk', 'Мастаск', 3, { key: 'MSK' }),
    header('project', 'p-ops', 'Операции', 1, { key: 'OPS' }),
  ],
  team: [header('team', null, null, 2), header('team', 't-mars', 'MARS', 2)],
  department: [header('department', 'd-log', 'Логистика', 4)],
  none: [header('none', null, null, 4)],
};

function switchButton(name: string): HTMLElement {
  return screen.getByRole('button', { name: `Группировка: ${name}` });
}

describe('US-10 Scenario: the «Группировка» switch of the organization backlog', () => {
  it('offers the four groupings, defaults to «По проекту» and opens the first group only', async () => {
    const api = serveAggregate({
      groups: GROUPS,
      rowsOf: (group) => (group === 'p-msk' ? [mskRow(1), mskRow(2)] : []),
    });
    renderAggregate(orgAddress('backlogs'));

    expect(await screen.findByText('Элемент 1')).toBeInTheDocument();
    await userEvent.click(switchButton('По проекту'));
    const labels = screen.getAllByRole('menuitemradio').map((item) => item.textContent);
    expect(labels).toEqual(['Без группировки', 'По департаменту', 'По проекту', 'По команде']);
    // The second group is closed, so nothing was read for it.
    expect(api.of('rows').map((r) => r.params.get('group'))).toEqual(['p-msk']);
    expect(api.of('groups')[0]?.params.get('grouping')).toBe('project');
  });

  it('re-reads by the chosen grouping, remembers it and puts «Без команды» first', async () => {
    const api = serveAggregate({ groups: GROUPS, rowsOf: () => [mskRow(7)] });
    const first = renderAggregate(orgAddress('backlogs'));
    await screen.findByText('Элемент 7');

    await userEvent.click(switchButton('По проекту'));
    await userEvent.click(screen.getByRole('menuitemradio', { name: 'По команде' }));
    const groupButtons = await screen.findAllByRole('button', { name: /(Без команды|MARS)/ });
    expect(groupButtons[0]).toHaveTextContent('Без команды');
    expect(api.of('groups').at(-1)?.params.get('grouping')).toBe('team');
    first.unmount();

    renderAggregate(orgAddress('backlogs'));
    expect(
      await screen.findByRole('button', { name: 'Группировка: По команде' }),
    ).toBeInTheDocument();
  });

  it('falls back silently when the remembered option is no longer offered', async () => {
    localStorage.setItem(`mustask.grouping.v1.user-1.${ORG}.backlogs.organization`, 'bogus');
    serveAggregate({ groups: GROUPS, rowsOf: () => [] });
    renderAggregate(orgAddress('backlogs'));
    expect(
      await screen.findByRole('button', { name: 'Группировка: По проекту' }),
    ).toBeInTheDocument();
  });
});

describe('US-10 Scenario: a group’s own read', () => {
  it('shows «Показать ещё» and appends the next page by the cursor', async () => {
    const api = serveAggregate({
      groups: GROUPS,
      rowsOf: (_group, offset) => (offset === 0 ? [mskRow(1), mskRow(2)] : [mskRow(3)]),
    });
    renderAggregate(orgAddress('backlogs'));
    await screen.findByText('Элемент 2');

    await userEvent.click(screen.getByRole('button', { name: 'Показать ещё' }));
    expect(await screen.findByText('Элемент 3')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Показать ещё' })).not.toBeInTheDocument();
    expect(api.of('rows').map((r) => r.params.get('cursor'))).toEqual([null, '2']);
  });

  it('retries a 429 by Retry-After by itself, then reads', async () => {
    serveAggregate({
      groups: GROUPS,
      rowsOf: () => [mskRow(1)],
      fail: (request, attempt) =>
        request.kind === 'rows' && attempt === 1
          ? new HttpResponse(null, { status: 429, headers: { 'Retry-After': '0' } })
          : undefined,
    });
    renderAggregate(orgAddress('backlogs'));
    expect(await screen.findByText('Элемент 1')).toBeInTheDocument();
  });

  it('after the retries shows the refusal with «Повторить», which reads again', async () => {
    let refuse = true;
    serveAggregate({
      groups: GROUPS,
      rowsOf: () => [mskRow(1)],
      fail: (request) =>
        request.kind === 'rows' && refuse
          ? new HttpResponse(null, { status: 429, headers: { 'Retry-After': '0' } })
          : undefined,
    });
    renderAggregate(orgAddress('backlogs'));
    expect(
      await screen.findByText(
        'Не удалось загрузить элементы группы. Сервис занят, попробуйте ещё раз.',
        {},
        { timeout: 8000 },
      ),
    ).toBeInTheDocument();
    refuse = false;
    await userEvent.click(screen.getByRole('button', { name: 'Повторить' }));
    expect(await screen.findByText('Элемент 1')).toBeInTheDocument();
  }, 15000);

  it('reloads the headers silently when a cursor is refused (400 on the cursor)', async () => {
    const api = serveAggregate({
      groups: GROUPS,
      rowsOf: () => [mskRow(1), mskRow(2)],
      fail: (request, attempt) =>
        request.kind === 'rows' && request.params.get('cursor') === '2' && attempt === 1
          ? HttpResponse.json(
              { status: 400, errors: { cursor: ['stale'] } },
              { status: 400, headers: { 'Content-Type': 'application/problem+json' } },
            )
          : undefined,
    });
    renderAggregate(orgAddress('backlogs'));
    await screen.findByText('Элемент 2');
    await userEvent.click(screen.getByRole('button', { name: 'Показать ещё' }));
    await waitFor(() => expect(api.of('groups').length).toBeGreaterThan(1));
    expect(screen.queryByText('Не удалось загрузить элементы группы.')).not.toBeInTheDocument();
  });

  it('shows the error with «Повторить» when the headers cannot be read', async () => {
    serveAggregate({
      groups: GROUPS,
      fail: (request) =>
        request.kind === 'groups' ? new HttpResponse(null, { status: 500 }) : undefined,
    });
    renderAggregate(orgAddress('backlogs'));
    expect(await screen.findByText('Не удалось загрузить элементы.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Повторить' })).toBeInTheDocument();
  });

  it('says so when nothing is there', async () => {
    serveAggregate({ groups: { project: [] } });
    renderAggregate(orgAddress('backlogs'));
    expect(await screen.findByText('В бэклоге пока нет элементов')).toBeInTheDocument();
  });
});

describe('US-11 Scenario: «Рабочие элементы»', () => {
  it('is flat by default, with the list read, and groups on request', async () => {
    const api = serveAggregate({
      groups: GROUPS,
      rowsOf: () => [mskRow(5)],
      list: { count: 0, data: [] },
    });
    renderAggregate(orgAddress('workitems'));
    await screen.findByRole('button', { name: 'Группировка: Без группировки' });
    await waitFor(() => expect(api.of('list').length).toBe(1));
    expect(api.of('groups')).toHaveLength(0);

    await userEvent.click(switchButton('Без группировки'));
    await userEvent.click(screen.getByRole('menuitemradio', { name: 'По проекту' }));
    expect(await screen.findByText('Элемент 5')).toBeInTheDocument();
    expect(api.of('groups').at(-1)?.params.get('grouping')).toBe('project');
    expect(
      within(screen.getByRole('table', { name: 'Мастаск' })).getAllByRole('row').length,
    ).toBeGreaterThan(1);
  });

  it('offers no «По команде» at the team level (a team fixes it)', async () => {
    serveAggregate({ groups: GROUPS, list: { count: 0, data: [] } });
    renderAggregate(`/organizations/${ORG}/teams/t-1/workitems`);
    await userEvent.click(
      await screen.findByRole('button', { name: 'Группировка: Без группировки' }),
    );
    const labels = screen.getAllByRole('menuitemradio').map((item) => item.textContent);
    expect(labels).toEqual(['Без группировки', 'По департаменту', 'По проекту']);
  });
});

describe('US-10 Scenario: the board', () => {
  it('reads each column of an open lane as its own cell', async () => {
    const api = serveAggregate({
      groups: {
        project: [
          header('project', 'p-msk', 'Мастаск', 1, {
            key: 'MSK',
            columns: storyColumns(),
          }),
        ],
      },
      cardsOf: () => [],
    });
    renderAggregate(orgAddress('boards'));
    await waitFor(() => expect(api.of('cells').length).toBeGreaterThan(0));
    expect(api.of('cells')[0]?.params.get('group')).toBe('p-msk');
    expect(api.of('cells')[0]?.params.get('column')).toMatch(/^[A-Za-z]+-\d+$/);
  });
});
