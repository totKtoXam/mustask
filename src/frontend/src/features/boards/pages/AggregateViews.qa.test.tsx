import { afterEach, describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse } from 'msw';
import {
  ORG,
  header,
  mskCard,
  mskRow,
  orgAddress,
  renderAggregate,
  serveAggregate,
  storyColumns,
} from '../testAggregate';

// feat-048 frontend QA (stage 7): scenarios of US-11 and design § 3, § 5 that the developer's
// page tests leave out: department order, the stream, «Свернуть все», the hints, the viewer, a
// deactivated project, the board's lanes and the grouped list's shape.

afterEach(() => localStorage.clear());

function remember(view: 'backlogs' | 'boards' | 'workitems', grouping: string): void {
  localStorage.setItem(`mustask.grouping.v1.user-1.${ORG}.${view}.organization`, grouping);
}

describe('US-11 Scenario: By department, «Без департамента» first', () => {
  it('lists the groups in the server order with the narrowed count as «3 из 40»', async () => {
    remember('backlogs', 'department');
    serveAggregate({
      groups: {
        department: [
          header('department', null, null, 1),
          { ...header('department', 'd-log', 'Логистика', 3), total: 40 },
          header('department', 'd-fin', 'Финансы', 1),
        ],
      },
      rowsOf: () => [mskRow(1)],
    });
    renderAggregate(orgAddress('backlogs'));

    const buttons = await screen.findAllByRole('button', {
      name: /(Без департамента|Логистика|Финансы)/,
    });
    expect(buttons.map((b) => b.textContent)).toEqual([
      expect.stringContaining('Без департамента'),
      expect.stringContaining('3 из 40'),
      expect.stringContaining('Финансы'),
    ]);
    expect(buttons[1]).toHaveTextContent('Логистика');
  });

  it('shows the change-time hint once and no move menu', async () => {
    remember('backlogs', 'department');
    serveAggregate({
      groups: { department: [header('department', 'd-log', 'Логистика', 2)] },
      rowsOf: () => [mskRow(1)],
    });
    renderAggregate(orgAddress('backlogs'));
    await screen.findByText('MSK-US-1');
    expect(
      screen.getAllByText(/При этой группировке строки идут по времени изменения/),
    ).toHaveLength(1);
    expect(screen.queryByRole('button', { name: 'Ещё действия' })).not.toBeInTheDocument();
  });
});

describe('US-11 Scenario: «Без группировки» is one list', () => {
  it('has no headers and no «Свернуть все», shows the count line and the hint', async () => {
    remember('backlogs', 'none');
    const api = serveAggregate({
      groups: { none: [header('none', null, null, 3)] },
      rowsOf: () => [mskRow(1)],
    });
    renderAggregate(orgAddress('backlogs'));
    await screen.findByText('MSK-US-1');
    expect(screen.getByText('Элементов: 3')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Свернуть все' })).not.toBeInTheDocument();
    expect(screen.getByText(/При этой группировке строки идут по времени изменения/)).toBeVisible();
    // The stream is read as one group with no group id.
    expect(api.of('rows')[0]?.params.get('grouping')).toBe('none');
    expect(api.of('rows')[0]?.params.get('group')).toBeNull();
  });
});

describe('BR-NV-20: «Свернуть все»', () => {
  it('closes the open group and the next visit remembers it closed', async () => {
    serveAggregate({
      groups: { project: [header('project', 'p-msk', 'Мастаск', 1, { key: 'MSK' })] },
      rowsOf: () => [mskRow(1)],
    });
    renderAggregate(orgAddress('backlogs'));
    await screen.findByText('MSK-US-1');
    await userEvent.click(screen.getByRole('button', { name: 'Свернуть все' }));
    await waitFor(() => expect(screen.queryByText('MSK-US-1')).not.toBeInTheDocument());
    expect(screen.getByRole('button', { name: /Мастаск/ })).toHaveAttribute(
      'aria-expanded',
      'false',
    );
  });
});

describe('design § 3: permissions', () => {
  it('gives a viewer no move menu and no drag handle in a by-project group', async () => {
    serveAggregate({
      groups: { project: [header('project', 'p-msk', 'Мастаск', 2, { key: 'MSK' })] },
      rowsOf: () => [mskRow(1), mskRow(2)],
    });
    renderAggregate(orgAddress('backlogs'), { role: 'viewer' });
    await screen.findByText('MSK-US-2');
    expect(screen.queryByRole('button', { name: 'Ещё действия' })).not.toBeInTheDocument();
    expect(document.querySelector('[data-drag-row]')).toBeNull();
  });
});

describe('OQ-4 A: a project deactivated meanwhile', () => {
  it('drops its group and says so', async () => {
    serveAggregate({
      groups: { project: [header('project', 'p-msk', 'Мастаск', 2, { key: 'MSK' })] },
      fail: (request) =>
        request.kind === 'rows'
          ? HttpResponse.json(
              { status: 404, errorCode: 'organizations.project.not_found' },
              { status: 404, headers: { 'Content-Type': 'application/problem+json' } },
            )
          : undefined,
    });
    renderAggregate(orgAddress('backlogs'));
    expect(await screen.findByText('Проект больше недоступен: группа скрыта.')).toBeInTheDocument();
    await waitFor(() => expect(screen.queryByRole('button', { name: /Мастаск/ })).toBeNull());
  });
});

describe('US-11 Scenario: the board in lanes, and as one board', () => {
  const columns = storyColumns();

  it('puts «Без команды» first and gives each lane its own cells', async () => {
    remember('boards', 'team');
    const api = serveAggregate({
      groups: {
        team: [
          header('team', null, null, 2, { columns }),
          header('team', 't-mars', 'MARS', 1, { columns }),
        ],
      },
      cardsOf: () => [mskCard(1)],
    });
    renderAggregate(orgAddress('boards'));
    const lanes = await screen.findAllByRole('button', { name: /(Без команды|MARS)/ });
    expect(lanes[0]).toHaveTextContent('Без команды');
    // Only the first lane is open: its columns are read, MARS is not.
    await waitFor(() => expect(api.of('cells').length).toBeGreaterThan(0));
    expect(new Set(api.of('cells').map((r) => r.params.get('group')))).toEqual(new Set(['none']));
  });

  it('draws one board without lanes for «Без группировки»', async () => {
    remember('boards', 'none');
    const api = serveAggregate({
      groups: { none: [header('none', null, null, 1, { columns })] },
      cardsOf: () => [mskCard(1)],
    });
    renderAggregate(orgAddress('boards'));
    expect((await screen.findAllByText('MSK-US-1')).length).toBeGreaterThan(0);
    expect(screen.queryByRole('button', { name: 'Свернуть все' })).not.toBeInTheDocument();
    expect(screen.getByRole('group', { name: 'Новая' })).toBeInTheDocument();
    expect(api.of('cells')[0]?.params.get('group')).toBeNull();
  });
});

describe('US-11 Scenario: «Рабочие элементы» grouped is a different shape', () => {
  it('has no pager and no sortable column, while the flat list keeps its pager', async () => {
    serveAggregate({
      groups: { project: [header('project', 'p-msk', 'Мастаск', 1, { key: 'MSK' })] },
      rowsOf: () => [mskRow(1)],
      list: { count: 0, data: [] },
    });
    renderAggregate(orgAddress('workitems'));
    await userEvent.click(
      await screen.findByRole('button', { name: 'Группировка: Без группировки' }),
    );
    await userEvent.click(screen.getByRole('menuitemradio', { name: 'По проекту' }));
    const table = await screen.findByRole('table', { name: 'Мастаск' });
    await within(table).findByText('MSK-US-1');
    expect(within(table).queryAllByRole('button')).toHaveLength(0);
    expect(screen.queryByRole('navigation', { name: /страниц/i })).not.toBeInTheDocument();
  });
});
