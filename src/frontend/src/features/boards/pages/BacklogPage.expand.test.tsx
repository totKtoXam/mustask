import { afterEach, describe, expect, it } from 'vitest';
import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { focusManager } from '@tanstack/react-query';
import { serveBoards } from '../testApi';
import {
  announcer,
  collapseOneLevel,
  expandControl,
  expandOneLevel,
  findBacklogTable,
  orders,
  outline,
  press,
} from '../testBacklog';
import { fullTreeDataset, mskRecord } from '../testFixtures';
import { chooseLevel } from '../testLevels';
import { backlogAddress, renderBoards } from '../testRender';

// feat-027 US-3: the hierarchy header's «Развернуть на один уровень» and «Свернуть
// на один уровень» on «Эпики» of «Платформа» (BR-T-8). The tree first opens expanded
// (BR-T-7), so "every row collapsed" is reached with «Свернуть на один уровень»
// until it is unavailable.

afterEach(() => {
  focusManager.setFocused(undefined);
});

const idle = (button: HTMLElement) => button.getAttribute('aria-disabled') === 'true';

async function collapseEverything() {
  while (!idle(collapseOneLevel())) {
    press(collapseOneLevel());
  }
}

describe('US-3 Scenario: The first state leaves «Развернуть на один уровень» nothing to do', () => {
  it('every row with a child is expanded, so «Развернуть на один уровень» is marked unavailable and «Свернуть на один уровень» is not', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    await findBacklogTable();

    expect(expandOneLevel()).toHaveAttribute('aria-disabled', 'true');
    expect(collapseOneLevel()).not.toHaveAttribute('aria-disabled');
  });
});

describe('US-3 Scenario: Each click opens one more level', () => {
  it('opens MSK-EPIC-1, then its children’s, then their own — «Показано строк: 5.» after the first — and ends unavailable but focusable', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();
    await collapseEverything();
    expect(outline(table)).toEqual(['MSK-EPIC-1', 'MSK-EPIC-20']);

    press(expandOneLevel());

    expect(outline(table)).toEqual([
      'MSK-EPIC-1',
      '  MSK-FEAT-8',
      '  MSK-FEAT-6',
      '  MSK-US-22',
      'MSK-EPIC-20',
    ]);
    expect(expandControl('MSK-EPIC-1')).toHaveAttribute('aria-expanded', 'true');
    expect(expandControl('MSK-FEAT-6')).toHaveAttribute('aria-expanded', 'false');
    expect(announcer()).toHaveTextContent('Показано строк: 5.');

    press(expandOneLevel());
    expect(outline(table)).toEqual([
      'MSK-EPIC-1',
      '  MSK-FEAT-8',
      '    MSK-FEAT-23',
      '  MSK-FEAT-6',
      '    MSK-US-2',
      '    MSK-BUG-4',
      '  MSK-US-22',
      'MSK-EPIC-20',
    ]);

    await userEvent.click(expandOneLevel());
    expect(outline(table)).toEqual([
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
    ]);
    expect(announcer()).toHaveTextContent('Показано строк: 10.');
    expect(expandOneLevel()).toHaveAttribute('aria-disabled', 'true');
    // Unavailable, but still a tab stop: it keeps focus.
    expect(expandOneLevel()).toHaveFocus();
  });
});

describe('US-3 Scenario: Each click closes the deepest open level', () => {
  it('hides MSK-BUG-5 and MSK-TASK-3 and collapses MSK-US-2, leaving the rest expanded; two more clicks leave the epics', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();

    press(collapseOneLevel());

    expect(outline(table)).toEqual([
      'MSK-EPIC-1',
      '  MSK-FEAT-8',
      '    MSK-FEAT-23',
      '  MSK-FEAT-6',
      '    MSK-US-2',
      '    MSK-BUG-4',
      '  MSK-US-22',
      'MSK-EPIC-20',
    ]);
    for (const open of ['MSK-EPIC-1', 'MSK-FEAT-8', 'MSK-FEAT-6']) {
      expect(expandControl(open)).toHaveAttribute('aria-expanded', 'true');
    }
    expect(expandControl('MSK-US-2')).toHaveAttribute('aria-expanded', 'false');

    press(collapseOneLevel());
    press(collapseOneLevel());

    expect(outline(table)).toEqual(['MSK-EPIC-1', 'MSK-EPIC-20']);
    expect(collapseOneLevel()).toHaveAttribute('aria-disabled', 'true');
    expect(announcer()).toHaveTextContent('Показано строк: 2.');
  });
});

describe('US-3 Scenario: Expanding completes a level the user opened in part', () => {
  it('with MSK-EPIC-1 and only MSK-FEAT-6 beneath it open, «Свернуть на один уровень» closes MSK-FEAT-6 and keeps MSK-EPIC-1; after reopening it «Развернуть на один уровень» opens MSK-FEAT-8 too', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();
    await collapseEverything();
    press(expandControl('MSK-EPIC-1'));
    press(expandControl('MSK-FEAT-6'));

    press(collapseOneLevel());

    expect(expandControl('MSK-FEAT-6')).toHaveAttribute('aria-expanded', 'false');
    expect(expandControl('MSK-EPIC-1')).toHaveAttribute('aria-expanded', 'true');
    press(expandControl('MSK-FEAT-6'));
    press(expandOneLevel());

    expect(expandControl('MSK-FEAT-8')).toHaveAttribute('aria-expanded', 'true');
    expect(expandControl('MSK-US-2')).toHaveAttribute('aria-expanded', 'false');
    expect(outline(table)).toEqual([
      'MSK-EPIC-1',
      '  MSK-FEAT-8',
      '    MSK-FEAT-23',
      '  MSK-FEAT-6',
      '    MSK-US-2',
      '    MSK-BUG-4',
      '  MSK-US-22',
      'MSK-EPIC-20',
    ]);
  });
});

describe('US-3 Scenario: Nothing to expand or collapse', () => {
  it('«Фичи» of «Мастаск», whose one row MSK-FEAT-21 has no child: both buttons are unavailable and activating either changes nothing and announces nothing', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('features', 'c2000000-0000-4000-8000-000000000001'));
    const table = await findBacklogTable();

    expect(outline(table)).toEqual(['MSK-FEAT-21']);
    expect(expandOneLevel()).toHaveAttribute('aria-disabled', 'true');
    expect(collapseOneLevel()).toHaveAttribute('aria-disabled', 'true');
    press(expandOneLevel());
    press(collapseOneLevel());

    expect(outline(table)).toEqual(['MSK-FEAT-21']);
    expect(announcer().textContent).toBe('');
  });
});

describe('US-3 Scenario: The buttons change only what is shown', () => {
  it('sends no request and changes no order, link or «Изменён»; «Порядок» still numbers MSK-EPIC-1 «1» and MSK-EPIC-20 «2»', async () => {
    const api = serveBoards(fullTreeDataset());
    const stored = JSON.stringify(api.data);
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();
    const requests = api.requests.length;

    press(collapseOneLevel());
    press(collapseOneLevel());
    press(expandOneLevel());

    expect(api.requests).toHaveLength(requests);
    expect(api.writes()).toEqual([]);
    expect(JSON.stringify(api.data)).toBe(stored);
    expect(orders(table).filter((order) => order !== '')).toEqual(['1', '2']);
  });
});

describe('US-3 Scenario: The buttons work by keyboard', () => {
  it('Enter, then Space on «Развернуть на один уровень» open two levels, focus staying on the button', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();
    await collapseEverything();

    expandOneLevel().focus();
    await userEvent.keyboard('{Enter}');
    await userEvent.keyboard(' ');

    expect(outline(table)).toEqual([
      'MSK-EPIC-1',
      '  MSK-FEAT-8',
      '    MSK-FEAT-23',
      '  MSK-FEAT-6',
      '    MSK-US-2',
      '    MSK-BUG-4',
      '  MSK-US-22',
      'MSK-EPIC-20',
    ]);
    expect(expandOneLevel()).toHaveFocus();
  });
});

describe('US-3 Scenario: The state the buttons leave is kept like any expansion', () => {
  it('two levels opened stay after the tab loses and regains focus', async () => {
    const api = serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();
    await collapseEverything();
    press(expandOneLevel());
    press(expandOneLevel());
    const twoLevels = outline(table);

    const reads = api.reads('/backlogs/epics').length;
    await act(async () => {
      focusManager.setFocused(false);
    });
    await act(async () => {
      focusManager.setFocused(true);
    });
    await waitFor(() => expect(api.reads('/backlogs/epics').length).toBeGreaterThan(reads));

    expect(outline(table)).toEqual(twoLevels);
  });

  it('another level and back start at the first state', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    await findBacklogTable();
    await collapseEverything();
    press(expandOneLevel());

    await chooseLevel('Фичи');
    await waitFor(async () =>
      expect(within(await findBacklogTable()).queryByText('Вход по коду')).not.toBeNull(),
    );
    await chooseLevel('Эпики');

    await waitFor(async () =>
      expect(outline(await findBacklogTable())).toContain('      MSK-TASK-3'),
    );
    expect(screen.getByRole('button', { name: 'Развернуть на один уровень' })).toHaveAttribute(
      'aria-disabled',
      'true',
    );
  });

  it('a level visited before opens at the first state of the answer read now, not of the cached one (F-2): a row that gained a child meanwhile is expanded', async () => {
    const api = serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    await findBacklogTable();
    // MSK-FEAT-23 has no child on «Эпики».
    expect(
      screen.queryByRole('button', { name: /^(Развернуть|Свернуть) MSK-FEAT-23$/ }),
    ).toBeNull();

    await chooseLevel('Фичи');
    await waitFor(async () =>
      expect(within(await findBacklogTable()).queryByText('Вход по коду')).not.toBeNull(),
    );
    // Meanwhile, on the page of «Фичи» or from another tab, MSK-US-22 is given to MSK-FEAT-23.
    mskRecord(api.data, 22).parentId = mskRecord(api.data, 23).id;
    await chooseLevel('Эпики');

    // The table first shows the answer cached by the first visit, then the one read now.
    await waitFor(async () =>
      expect(outline(await findBacklogTable())).toContain('      MSK-US-22'),
    );
    expect(expandControl('MSK-FEAT-23')).toHaveAttribute('aria-expanded', 'true');
    expect(expandOneLevel()).toHaveAttribute('aria-disabled', 'true');
  });

  it('an untouched table follows the answers it is given, a touched one keeps what the user set (F-2)', async () => {
    const api = serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();
    expect(outline(table)).toContain('    MSK-FEAT-23');

    // Untouched: a child that appears is shown expanded by the first state.
    mskRecord(api.data, 22).parentId = mskRecord(api.data, 23).id;
    await act(async () => {
      focusManager.setFocused(false);
    });
    await act(async () => {
      focusManager.setFocused(true);
    });
    await waitFor(() => expect(outline(table)).toContain('      MSK-US-22'));

    // Touched: the user folds MSK-FEAT-23; a later read does not open it again.
    press(expandControl('MSK-FEAT-23'));
    mskRecord(api.data, 20).parentId = mskRecord(api.data, 1).id;
    await act(async () => {
      focusManager.setFocused(false);
    });
    await act(async () => {
      focusManager.setFocused(true);
    });
    await waitFor(() => expect(outline(table)).toContain('  MSK-EPIC-20'));
    expect(expandControl('MSK-FEAT-23')).toHaveAttribute('aria-expanded', 'false');
    expect(outline(table)).not.toContain('      MSK-US-22');
  });
});

describe('US-3 Scenario: The status region speaks every press (F-11)', () => {
  it('a button whose result repeats the last announcement still changes the region, so it is spoken again', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    await findBacklogTable();

    press(collapseOneLevel());
    expect(announcer()).toHaveTextContent('Показано строк: 8.');
    // A chevron announces nothing; the count is the same again after the next press.
    press(expandControl('MSK-US-2'));
    expect(announcer()).toHaveTextContent('Показано строк: 8.');
    const watched = announcer();
    const observer = new MutationObserver(() => {});
    observer.observe(watched, { childList: true, subtree: true, characterData: true });

    press(collapseOneLevel());
    const changes = observer.takeRecords();
    observer.disconnect();

    expect(watched).toHaveTextContent('Показано строк: 8.');
    expect(changes.length).toBeGreaterThan(0);
  });
});
