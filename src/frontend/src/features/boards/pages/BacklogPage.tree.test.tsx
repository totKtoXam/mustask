import { afterEach, describe, expect, it } from 'vitest';
import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { focusManager } from '@tanstack/react-query';
import { HttpResponse, http } from 'msw';
import { server } from '../../../test/mocks/server';
import { serveBoards } from '../testApi';
import {
  CELL,
  cellOf,
  collapseOneLevel,
  expandControl,
  findBacklogTable,
  identifiers,
  openActions,
  press,
  orders,
  outline,
  rowOf,
  rowsOf,
  titleLink,
} from '../testBacklog';
import {
  BACKLOG_URL,
  fullTreeDataset,
  ids,
  mskRecord,
  stateId,
  treeDataset,
} from '../testFixtures';
import { chooseLevel } from '../testLevels';
import { backlogAddress, renderBoards } from '../testRender';

// feat-027 US-2: the whole tree under a row, from the one answer. US-1's «Data»
// (`fullTreeDataset`): «Платформа»'s «Эпики» read MSK-EPIC-1 and MSK-EPIC-20;
// MSK-EPIC-1 has MSK-FEAT-8, MSK-FEAT-6 and MSK-US-22, MSK-FEAT-8 has
// MSK-FEAT-23, MSK-FEAT-6 has MSK-US-2 and MSK-BUG-4, MSK-US-2 has MSK-BUG-5 and
// MSK-TASK-3; MSK-FEAT-21 and MSK-TASK-7, in «Мастаск»'s area, stand beneath no
// row of «Платформа». The first state is expanded as deep as 2 000 drawn rows
// allow (BR-T-7), so a scenario that starts «collapsed» reaches it with «Свернуть
// на один уровень» until that is unavailable.

afterEach(() => {
  focusManager.setFocused(undefined);
});

/** Lets the tab lose and regain focus, as the browser reports it. */
async function refocusTab() {
  await act(async () => {
    focusManager.setFocused(false);
  });
  await act(async () => {
    focusManager.setFocused(true);
  });
}

/** «Свернуть на один уровень» until it is unavailable: every row collapsed. */
async function collapseEverything() {
  while (collapseOneLevel().getAttribute('aria-disabled') !== 'true') {
    press(collapseOneLevel());
  }
}

const WHOLE_TREE = [
  'MSK-EPIC-1',
  '  MSK-FEAT-8',
  '    MSK-FEAT-23',
  '  MSK-FEAT-6',
  '    MSK-US-2',
  '      MSK-BUG-5',
  '      MSK-TASK-3',
  '    MSK-BUG-4',
  '  MSK-US-22',
  'MSK-EPIC-20',
];

describe('US-2 Scenario: The first state is expanded as far as the budget allows (BR-T-7)', () => {
  it('opens «Эпики» of «Платформа» with every row that has a child expanded: the ten rows, MSK-FEAT-21 and MSK-TASK-7 not among them', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));

    const table = await findBacklogTable();

    expect(outline(table)).toEqual(WHOLE_TREE);
    expect(identifiers(table)).not.toContain('MSK-FEAT-21');
    expect(identifiers(table)).not.toContain('MSK-TASK-7');
    for (const parent of ['MSK-EPIC-1', 'MSK-FEAT-8', 'MSK-FEAT-6', 'MSK-US-2']) {
      expect(expandControl(parent)).toHaveAttribute('aria-expanded', 'true');
    }
  });
});

describe('US-2 Scenario: The «Эпики» tree reaches the tasks', () => {
  it('collapsed to the epics, expanding MSK-EPIC-1, MSK-FEAT-8, MSK-FEAT-6 and MSK-US-2 reads the ten rows, indented by depth', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();
    await collapseEverything();
    expect(outline(table)).toEqual(['MSK-EPIC-1', 'MSK-EPIC-20']);

    for (const parent of ['MSK-EPIC-1', 'MSK-FEAT-8', 'MSK-FEAT-6', 'MSK-US-2']) {
      press(expandControl(parent));
    }

    expect(outline(table)).toEqual(WHOLE_TREE);
    const indent = (identifier: string) =>
      getComputedStyle(cellOf(rowOf(table, identifier), CELL.title).firstElementChild as Element)
        .paddingLeft;
    expect(indent('MSK-EPIC-1')).toBe('0px');
    expect(indent('MSK-FEAT-8')).toBe('16px');
    expect(indent('MSK-FEAT-23')).toBe('32px');
    expect(indent('MSK-BUG-5')).toBe('48px');
  });
});

describe('US-2 Scenario: Children stand by level, then in their backlog’s order (BR-T-6)', () => {
  it('beneath MSK-EPIC-1 the features come first, in the «Фичи» order, then MSK-US-22; beneath MSK-US-2 its bug MSK-BUG-5 comes before its task MSK-TASK-3', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));

    const table = await findBacklogTable();

    const tiers = (tier: number) =>
      rowsOf(table)
        .filter((row) => row.dataset.tier === String(tier))
        .map((row) => row.dataset.identifier);
    expect(tiers(1)).toEqual(['MSK-FEAT-8', 'MSK-FEAT-6', 'MSK-US-22']);
    expect(tiers(3)).toEqual(['MSK-BUG-5', 'MSK-TASK-3']);
  });
});

describe('US-2 Scenario: Another team’s children stand beneath no row (BR-T-4)', () => {
  it('MSK-FEAT-21 and MSK-TASK-7, in «Мастаск»’s area, are not shown; MSK-FEAT-21 is a top-level row of «Мастаск»’s «Фичи»', async () => {
    serveBoards(fullTreeDataset());
    const view = renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();
    expect(identifiers(table)).not.toContain('MSK-FEAT-21');
    expect(identifiers(table)).not.toContain('MSK-TASK-7');
    view.unmount();

    renderBoards(backlogAddress('features', ids.mskTeam));

    const other = await findBacklogTable();
    expect(outline(other)).toEqual(['MSK-FEAT-21']);
    expect(rowOf(other, 'MSK-FEAT-21')).toHaveAttribute('data-tier', '0');
    expect(orders(other)).toEqual(['1']);
  });
});

describe('US-2 Scenario: A same-level child stands beneath its parent only (BR-T-5)', () => {
  it('«Фичи»: the top-level rows are MSK-FEAT-8 «1» and MSK-FEAT-6 «2», and MSK-FEAT-23 stands beneath MSK-FEAT-8 with no «Порядок»', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('features'));

    const table = await findBacklogTable();

    expect(outline(table)).toEqual([
      'MSK-FEAT-8',
      '  MSK-FEAT-23',
      'MSK-FEAT-6',
      '  MSK-US-2',
      '    MSK-BUG-5',
      '    MSK-TASK-3',
      '  MSK-BUG-4',
    ]);
    expect(orders(table)).toEqual(['1', '', '2', '', '', '', '']);
    expect(rowOf(table, 'MSK-FEAT-23')).toHaveAttribute('data-tier', '1');
  });
});

describe('US-2 Scenario: «Истории» rows open to their bugs and tasks (BR-T-4, supersedes OQ-5)', () => {
  it('the top-level rows are MSK-US-2, MSK-BUG-4, MSK-US-22, numbered 1..3; only MSK-US-2 has a chevron, and it opens to MSK-BUG-5 and MSK-TASK-3', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('stories'));
    const table = await findBacklogTable();

    expect(
      rowsOf(table)
        .filter((row) => row.dataset.tier === '0')
        .map((row) => row.dataset.identifier),
    ).toEqual(['MSK-US-2', 'MSK-BUG-4', 'MSK-US-22']);
    expect(orders(table)).toEqual(['1', '', '', '2', '3']);
    expect(
      within(table)
        .getAllByRole('button', { name: /^(Развернуть|Свернуть) MSK-/ })
        .map((control) => control.getAttribute('aria-label')),
    ).toEqual(['Свернуть MSK-US-2']);

    await collapseEverything();
    expect(outline(table)).toEqual(['MSK-US-2', 'MSK-BUG-4', 'MSK-US-22']);
    expect(expandControl('MSK-US-2')).toHaveAccessibleName('Развернуть MSK-US-2');
    press(expandControl('MSK-US-2'));
    expect(outline(table)).toEqual([
      'MSK-US-2',
      '  MSK-BUG-5',
      '  MSK-TASK-3',
      'MSK-BUG-4',
      'MSK-US-22',
    ]);
  });
});

describe('US-2 Scenario: The chevron stands left of the icon and works by keyboard', () => {
  it('stands between the indent and the type icon, and a row with no child keeps an empty box of the same width, so the icons of one depth line up', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();

    const slot = (identifier: string) =>
      cellOf(rowOf(table, identifier), CELL.title).firstElementChild?.children[0] as HTMLElement;
    expect(slot('MSK-EPIC-1')).toBe(expandControl('MSK-EPIC-1'));
    expect(getComputedStyle(slot('MSK-EPIC-1')).width).toBe('24px');
    expect(getComputedStyle(slot('MSK-EPIC-1')).height).toBe('24px');
    // The empty box of a row with nothing beneath it is hidden from assistive technology.
    expect(slot('MSK-BUG-5')).toHaveAttribute('aria-hidden', 'true');
    expect(getComputedStyle(slot('MSK-BUG-5')).width).toBe('24px');
    // After the chevron's slot comes the icon, then the title.
    const row = rowOf(table, 'MSK-EPIC-1');
    const title = titleLink(row);
    const icon = cellOf(row, CELL.title).querySelector('svg[data-testid="CrownIcon"]');
    expect(icon).not.toBeNull();
    expect(
      (icon as Element).compareDocumentPosition(title) & Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();
    expect(
      (slot('MSK-EPIC-1').compareDocumentPosition(icon as Element) &
        Node.DOCUMENT_POSITION_FOLLOWING) as number,
    ).toBeTruthy();
  });

  it('Enter on «Свернуть MSK-EPIC-1» collapses the row, the control reads «Развернуть MSK-EPIC-1» with `aria-expanded="false"`, and focus stays on it; Space expands it again', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();

    expandControl('MSK-EPIC-1').focus();
    await userEvent.keyboard('{Enter}');

    expect(outline(table)).toEqual(['MSK-EPIC-1', 'MSK-EPIC-20']);
    expect(expandControl('MSK-EPIC-1')).toHaveAccessibleName('Развернуть MSK-EPIC-1');
    expect(expandControl('MSK-EPIC-1')).toHaveAttribute('aria-expanded', 'false');
    expect(
      expandControl('MSK-EPIC-1').querySelector('[data-testid="ThinChevronRightIcon"]'),
    ).not.toBeNull();
    expect(expandControl('MSK-EPIC-1')).toHaveFocus();

    await userEvent.keyboard(' ');
    expect(outline(table)).toEqual(WHOLE_TREE);
    expect(
      expandControl('MSK-EPIC-1').querySelector('[data-testid="ThinChevronDownIcon"]'),
    ).not.toBeNull();
    expect(expandControl('MSK-EPIC-1')).toHaveFocus();
  });

  it('the controls are tab stops in row order: «+», the chevron, the title, then «⋮»', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    await findBacklogTable();

    (
      await screen.findByRole('button', { name: 'Добавить: Фича (MSK-EPIC-1)', hidden: true })
    ).focus();
    await userEvent.tab();
    expect(expandControl('MSK-EPIC-1')).toHaveFocus();
    await userEvent.tab();
    expect(screen.getByRole('link', { name: 'Вход и регистрация' })).toHaveFocus();
    await userEvent.tab();
    expect(screen.getByRole('button', { name: 'Ещё действия: MSK-EPIC-1' })).toHaveFocus();
  });
});

describe('US-2 Scenario: A child in «Удалена» frees its parent’s place', () => {
  it('MSK-BUG-5 placed in the Removed category directly: «Истории» shows it nowhere, and MSK-US-2 expands to MSK-TASK-3 only', async () => {
    const data = fullTreeDataset();
    mskRecord(data, 5).stateId = stateId('BUG', 'Удалена');
    serveBoards(data);
    renderBoards(backlogAddress('stories'));

    const table = await findBacklogTable();

    expect(outline(table)).toEqual(['MSK-US-2', '  MSK-TASK-3', 'MSK-BUG-4', 'MSK-US-22']);
  });

  it('a parent in «Удалена»: its children stand beneath no row of it — MSK-FEAT-6 and MSK-FEAT-8 are top-level rows of «Фичи», «Эпики» has MSK-EPIC-20 alone', async () => {
    const data = treeDataset();
    mskRecord(data, 1).stateId = stateId('EPIC', 'Удалена');
    serveBoards(data);
    renderBoards(backlogAddress('features'));
    const table = await findBacklogTable();

    expect(
      rowsOf(table)
        .filter((row) => row.dataset.tier === '0')
        .map((row) => row.dataset.identifier),
    ).toEqual(['MSK-FEAT-8', 'MSK-FEAT-6']);

    await chooseLevel('Эпики');
    await waitFor(async () => expect(outline(await findBacklogTable())).toEqual(['MSK-EPIC-20']));
    expect(screen.queryByRole('button', { name: /^(Развернуть|Свернуть) MSK-/ })).toBeNull();
  });
});

describe('US-2 Scenario: Expanded rows survive the page’s re-reads, not a reload (BR-T-7)', () => {
  it('MSK-EPIC-1 and MSK-FEAT-6 stay expanded, and the rest collapsed, after a reorder and a refocus', async () => {
    const api = serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();
    await collapseEverything();
    press(expandControl('MSK-EPIC-1'));
    press(expandControl('MSK-FEAT-6'));

    const menu = await openActions('MSK-EPIC-20');
    await userEvent.click(within(menu).getByRole('menuitem', { name: 'В начало' }));
    const reordered = [
      'MSK-EPIC-20',
      'MSK-EPIC-1',
      '  MSK-FEAT-8',
      '  MSK-FEAT-6',
      '    MSK-US-2',
      '    MSK-BUG-4',
      '  MSK-US-22',
    ];
    await waitFor(() => expect(outline(table)).toEqual(reordered));
    const reads = api.reads('/backlogs/epics').length;
    await refocusTab();
    await waitFor(() => expect(api.reads('/backlogs/epics').length).toBeGreaterThan(reads));

    expect(outline(table)).toEqual(reordered);
    expect(expandControl('MSK-EPIC-1')).toHaveAttribute('aria-expanded', 'true');
    expect(expandControl('MSK-FEAT-6')).toHaveAttribute('aria-expanded', 'true');
  });

  it('another level and back starts at the first state', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();
    await collapseEverything();
    press(expandControl('MSK-EPIC-1'));
    expect(outline(table)).toEqual([
      'MSK-EPIC-1',
      '  MSK-FEAT-8',
      '  MSK-FEAT-6',
      '  MSK-US-22',
      'MSK-EPIC-20',
    ]);

    await chooseLevel('Фичи');
    await waitFor(async () =>
      expect(identifiers(await findBacklogTable())).toContain('MSK-FEAT-23'),
    );
    await chooseLevel('Эпики');

    await waitFor(async () => expect(outline(await findBacklogTable())).toEqual(WHOLE_TREE));
  });

  it('a reload starts at the first state too', async () => {
    serveBoards(fullTreeDataset());
    const view = renderBoards(backlogAddress('epics'));
    await findBacklogTable();
    await collapseEverything();
    view.unmount();

    renderBoards(backlogAddress('epics'));

    expect(outline(await findBacklogTable())).toEqual(WHOLE_TREE);
  });
});

describe('US-2 Scenario: A link made elsewhere shows at the next read', () => {
  it('MSK-US-22’s parent set to MSK-FEAT-8 in another tab: on refocus it is no longer a child of MSK-EPIC-1 and shows beneath MSK-FEAT-8', async () => {
    const api = serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();

    mskRecord(api.data, 22).parentId = mskRecord(api.data, 8).id;
    await refocusTab();

    await waitFor(() =>
      expect(outline(table)).toEqual([
        'MSK-EPIC-1',
        '  MSK-FEAT-8',
        '    MSK-FEAT-23',
        '    MSK-US-22',
        '  MSK-FEAT-6',
        '    MSK-US-2',
        '      MSK-BUG-5',
        '      MSK-TASK-3',
        '    MSK-BUG-4',
        'MSK-EPIC-20',
      ]),
    );
  });
});

describe('US-2 Scenario: Expanding asks the server nothing', () => {
  it('expanding and collapsing any row sends no request and changes no order, link or «Изменён»', async () => {
    const api = serveBoards(fullTreeDataset());
    const stored = JSON.stringify(api.data);
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();
    const requests = api.requests.length;

    press(expandControl('MSK-FEAT-6'));
    press(expandControl('MSK-EPIC-1'));
    press(expandControl('MSK-EPIC-1'));

    // MSK-FEAT-6 stays collapsed; MSK-EPIC-1 was collapsed and expanded again.
    expect(outline(table)).toEqual([
      'MSK-EPIC-1',
      '  MSK-FEAT-8',
      '    MSK-FEAT-23',
      '  MSK-FEAT-6',
      '  MSK-US-22',
      'MSK-EPIC-20',
    ]);
    expect(api.requests).toHaveLength(requests);
    expect(api.writes()).toEqual([]);
    expect(JSON.stringify(api.data)).toBe(stored);
  });
});

describe('US-2 Scenario: A failed re-read keeps the table (tech-012 m2, folded in by D-6)', () => {
  it('leaves the rows, what is expanded and the selection as they were with the error above the table; a retry that succeeds takes it away', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();
    await collapseEverything();
    press(expandControl('MSK-EPIC-1'));
    await userEvent.click(cellOf(rowOf(table, 'MSK-FEAT-8'), CELL.type));
    const shown = ['MSK-EPIC-1', '  MSK-FEAT-8', '  MSK-FEAT-6', '  MSK-US-22', 'MSK-EPIC-20'];
    expect(outline(table)).toEqual(shown);

    server.use(
      http.get(
        BACKLOG_URL,
        () =>
          HttpResponse.json(
            { status: 500, title: 'Ошибка', detail: 'Сервис не ответил.' },
            { status: 500 },
          ),
        { once: true },
      ),
    );
    await refocusTab();

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Сервис не ответил.');
    // The table is the same node, with its rows, its expansion and its selection.
    expect(await findBacklogTable()).toBe(table);
    expect(outline(table)).toEqual(shown);
    expect(rowOf(table, 'MSK-FEAT-8')).toHaveAttribute('data-selected', 'true');
    expect(screen.queryByRole('status', { name: 'Загрузка данных' })).toBeNull();

    await userEvent.click(within(alert).getByRole('button', { name: 'Повторить' }));

    await waitFor(() => expect(screen.queryByRole('alert')).toBeNull());
    expect(await findBacklogTable()).toBe(table);
    expect(outline(table)).toEqual(shown);
    expect(rowOf(table, 'MSK-FEAT-8')).toHaveAttribute('data-selected', 'true');
  });
});
