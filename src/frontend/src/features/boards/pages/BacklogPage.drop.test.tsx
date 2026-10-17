import { afterEach, describe, expect, it } from 'vitest';
import { act, screen, waitFor } from '@testing-library/react';
import { focusManager } from '@tanstack/react-query';
import { HttpResponse, http } from 'msw';
import { theme } from '../../../app/theme';
import { server } from '../../../test/mocks/server';
import { WORK_ITEM_PARENT_URL } from '../../workitems/testFixtures';
import { serveBoards } from '../testApi';
import {
  CELL,
  actions,
  addControl,
  addRowOf,
  cellOf,
  collapseOneLevel,
  expandControl,
  expandOneLevel,
  findBacklogTable,
  grip,
  orders,
  outline,
  press,
  rowOf,
  rowsOf,
  titleLink,
} from '../testBacklog';
import { dataTransfer, fireDrag, type TestDataTransfer } from '../testDrag';
import {
  catalogText,
  fullTreeDataset,
  ids,
  mskRecord,
  platformItem,
  type BoardsDataset,
} from '../testFixtures';
import { holdParentWrites, parentWrites } from '../testParentDialog';
import { backlogAddress, renderBoards } from '../testRender';

// feat-027 US-5: a row dragged by any point of it (D-11) and dropped onto another
// row is the parent write (BR-T-11). `fullTreeDataset` on «Эпики» reads, expanded:
// MSK-EPIC-1 → MSK-FEAT-8 → MSK-FEAT-23; MSK-EPIC-1 → MSK-FEAT-6 → MSK-US-2 →
// MSK-BUG-5, MSK-TASK-3; MSK-FEAT-6 → MSK-BUG-4; MSK-EPIC-1 → MSK-US-22; and
// MSK-EPIC-20. The in-memory API answers the parent write as feat-001's contract
// does, so a refusal the page cannot see (a descendant on another team, a parent
// changed meanwhile) comes from it.

const PARENT_CHANGED_TEXT = catalogText('workitems.link.parent_changed');
const FAILED_TEXT = 'Не удалось сохранить родителя. Попробуйте ещё раз.';
const LEVEL_TEXT = 'Родителем может быть только рабочий элемент того же или более высокого уровня.';

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

/** «Фичи» with MSK-FEAT-26 added: the stories and tasks of the features stand beneath them, MSK-US-22 on no row. */
const FEATURES_TREE = [
  'MSK-FEAT-8',
  '  MSK-FEAT-23',
  'MSK-FEAT-6',
  '  MSK-US-2',
  '    MSK-BUG-5',
  '    MSK-TASK-3',
  '  MSK-BUG-4',
  'MSK-FEAT-26',
];

afterEach(() => {
  focusManager.setFocused(undefined);
});

/**
 * A drag of the row `identifier`; the pointer then moves over rows and releases.
 * The browser fires `dragstart` and `dragend` on the draggable element, the row
 * itself, wherever on it the press was (D-11); `from` is where the test says the
 * press was, an element of the row, and the event bubbles up to the row.
 */
class Drag {
  readonly transfer: TestDataTransfer = dataTransfer();
  readonly source: HTMLElement;
  private readonly table: HTMLElement;
  private readonly from: HTMLElement;

  constructor(table: HTMLElement, identifier: string, from?: (row: HTMLElement) => HTMLElement) {
    this.table = table;
    this.source = rowOf(table, identifier);
    this.from = from ? from(this.source) : this.source;
  }

  /** The row's title cell, where the pointer is. */
  private cellAt(target: string) {
    return cellOf(rowOf(this.table, target), CELL.title);
  }

  /** Resolves whether the drag was allowed to start. */
  async start(): Promise<boolean> {
    let accepted = true;
    await act(async () => {
      accepted = fireDrag('dragStart', this.from, this.transfer);
    });
    return accepted;
  }

  /**
   * The pointer over `target`, in the `half` of the row it is in (the middle when none);
   * resolves whether the place takes a drop — the page cancels the event when it does.
   */
  async hover(target: string, half?: 'upper' | 'lower'): Promise<boolean> {
    let takesDrop = false;
    await act(async () => {
      takesDrop = !fireDrag('dragOver', this.cellAt(target), this.transfer, half);
    });
    return takesDrop;
  }

  async release(target: string, half?: 'upper' | 'lower') {
    await act(async () => fireDrag('drop', this.cellAt(target), this.transfer, half));
    await this.end();
  }

  async end() {
    await act(async () => fireDrag('dragEnd', this.source, this.transfer));
  }
}

/** The whole gesture: a press on the row, the pointer over the target, release there. */
async function dropOnto(table: HTMLElement, identifier: string, target: string) {
  const gesture = new Drag(table, identifier);
  await gesture.start();
  await gesture.hover(target);
  await gesture.release(target);
}

async function renderEpics(data: BoardsDataset = fullTreeDataset()) {
  const api = serveBoards(data);
  renderBoards(backlogAddress('epics'));
  return { api, table: await findBacklogTable() };
}

const noFrame = (table: HTMLElement) =>
  !rowsOf(table).some((row) => row.hasAttribute('data-drop-target'));
const noLine = (table: HTMLElement) =>
  !rowsOf(table).some((row) => row.hasAttribute('data-drop-line'));

describe('US-5 Scenario: The whole row drags; the grip at its edge is only the hint, not a column', () => {
  it('every row, top-level and nested, is draggable, its title link is not (the browser would drag the address); the grip is hidden from assistive technology, out of the Tab order and not a drag source of its own; no header names a handle', async () => {
    const { table } = await renderEpics();

    expect(rowsOf(table)).toHaveLength(WHOLE_TREE.length);
    for (const row of rowsOf(table)) {
      const identifier = row.dataset.identifier as string;
      expect(row).toHaveAttribute('draggable', 'true');
      expect(titleLink(row)).toHaveAttribute('draggable', 'false');
      const handle = grip(table, identifier);
      expect(handle).toHaveAttribute('aria-hidden', 'true');
      expect(handle).toHaveAttribute('tabindex', '-1');
      expect(handle).not.toHaveAttribute('draggable');
      expect(handle.getAttribute('aria-label')).toBeNull();
    }
    const headers = Array.from(table.querySelectorAll('thead th')).map(
      (header) => header.getAttribute('aria-label') ?? header.textContent,
    );
    expect(headers).toEqual([
      'Иерархия',
      'Порядок',
      'Тип',
      'Заголовок',
      'Статус',
      'Команда',
      'Трудоёмкость',
      'Бизнес-ценность',
      'Вид ценности',
    ]);
  });
});

describe('US-5 Scenario: A drag starts from any point of the row (D-11)', () => {
  const POINTS: [string, (row: HTMLElement) => HTMLElement][] = [
    ['the «Заголовок» cell', (row) => cellOf(row, CELL.title)],
    ['the «Тип» cell', (row) => cellOf(row, CELL.type)],
    ['the «Статус» cell', (row) => cellOf(row, CELL.state)],
    ['the title link, which is not draggable itself', (row) => titleLink(row)],
    ['the «Ещё действия» button', (row) => actions(row.dataset.identifier as string)],
    ['the dotted grip', (row) => row.querySelector<HTMLElement>('[data-grip]') as HTMLElement],
  ];

  it.each(POINTS)(
    'a press on %s drags MSK-US-22: the row is selected, the move is the effect, and dropped onto MSK-FEAT-8 it is the parent write',
    async (_name, point) => {
      const { api, table } = await renderEpics();

      const gesture = new Drag(table, 'MSK-US-22', point);
      expect(await gesture.start()).toBe(true);
      expect(gesture.transfer.effectAllowed).toBe('move');
      expect(gesture.transfer.getData('text/plain')).toBe('MSK-US-22');
      expect(rowOf(table, 'MSK-US-22')).toHaveAttribute('data-selected', 'true');
      expect(await gesture.hover('MSK-FEAT-8')).toBe(true);
      expect(rowOf(table, 'MSK-FEAT-8')).toHaveAttribute('data-drop-target', 'allowed');
      await gesture.release('MSK-FEAT-8');

      expect(await screen.findByRole('alert')).toHaveTextContent(
        'У MSK-US-22 теперь родитель MSK-FEAT-8.',
      );
      expect(parentWrites(api).map((request) => request.body)).toEqual([
        { parentId: mskRecord(api.data, 8).id, seenParentId: mskRecord(api.data, 1).id },
      ]);
    },
  );

  it('a press on the row’s «+» and on its chevron drags the row too, and a plain click on either still only adds or folds', async () => {
    const { table } = await renderEpics();

    const plus = await addControl('MSK-EPIC-1');
    const chevron = expandControl('MSK-EPIC-1');
    for (const button of [plus, chevron]) {
      const gesture = new Drag(table, 'MSK-EPIC-1', () => button);
      expect(await gesture.start()).toBe(true);
      expect(gesture.transfer.getData('text/plain')).toBe('MSK-EPIC-1');
      await gesture.end();
    }
    // No drag was dropped: nothing was written, and the click path is untouched.
    expect(outline(table)).toEqual(WHOLE_TREE);
    press(chevron);
    expect(outline(table)).toEqual(['MSK-EPIC-1', 'MSK-EPIC-20']);
  });

  it('the inline add row and its field are never a drag source', async () => {
    const { table } = await renderEpics();

    press(await addControl('MSK-EPIC-1'));
    const add = addRowOf(table);
    expect(add).not.toBeNull();
    expect(add).not.toHaveAttribute('draggable');
    const field = (add as HTMLElement).querySelector('input') as HTMLElement;
    expect(field).not.toBeNull();
    expect(field.closest('[draggable="true"]')).toBeNull();
    // A drag begun in the add row starts nothing: its row handles no dragstart.
    const transfer = dataTransfer();
    await act(async () => fireDrag('dragStart', field, transfer));
    expect(transfer.getData('text/plain')).toBe('');
    expect(noFrame(table)).toBe(true);
    expect(rowsOf(table).some((row) => row.hasAttribute('data-dragging'))).toBe(false);
  });
});

describe('US-5 Scenario: A story dropped onto a feature becomes its child', () => {
  it('MSK-US-22 onto MSK-FEAT-8: selected and dimmed, the target framed as allowed; released, the parent write names MSK-FEAT-8 and MSK-EPIC-1 as seen, the tree is read again, the notice and focus follow', async () => {
    const { api, table } = await renderEpics();
    expect(outline(table)).toEqual(WHOLE_TREE);

    const gesture = new Drag(table, 'MSK-US-22');
    expect(await gesture.start()).toBe(true);
    expect(gesture.transfer.effectAllowed).toBe('move');
    expect(gesture.transfer.getData('text/plain')).toBe('MSK-US-22');
    expect(rowOf(table, 'MSK-US-22')).toHaveAttribute('data-selected', 'true');

    // The row is dimmed from the first move over the table, the target framed.
    expect(await gesture.hover('MSK-FEAT-8')).toBe(true);
    expect(rowOf(table, 'MSK-US-22')).toHaveAttribute('data-dragging', 'true');
    expect(rowOf(table, 'MSK-FEAT-8')).toHaveAttribute('data-drop-target', 'allowed');
    expect(getComputedStyle(rowOf(table, 'MSK-FEAT-8')).outline).toBe(
      `2px solid ${theme.palette.primary.main}`,
    );
    expect(getComputedStyle(cellOf(rowOf(table, 'MSK-US-22'), CELL.title)).opacity).toBe('0.6');
    expect(getComputedStyle(cellOf(rowOf(table, 'MSK-FEAT-8'), CELL.title)).opacity).toBe('');
    expect(rowsOf(table).filter((row) => row.hasAttribute('data-drop-target'))).toHaveLength(1);
    expect(noLine(table)).toBe(true);
    expect(gesture.transfer.dropEffect).toBe('move');

    const reads = api.reads('/backlogs/epics').length;
    await gesture.release('MSK-FEAT-8');

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'У MSK-US-22 теперь родитель MSK-FEAT-8.',
    );
    expect(screen.getByRole('alert')).toHaveClass('MuiAlert-colorSuccess');
    expect(parentWrites(api).map((request) => request.body)).toEqual([
      { parentId: mskRecord(api.data, 8).id, seenParentId: mskRecord(api.data, 1).id },
    ]);
    expect(mskRecord(api.data, 22).parentId).toBe(mskRecord(api.data, 8).id);
    expect(api.reads('/backlogs/epics').length).toBeGreaterThan(reads);
    // The features come before the stories (BR-T-6): MSK-FEAT-23, then MSK-US-22, beneath MSK-FEAT-8.
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
    ]);
    expect(rowOf(table, 'MSK-US-22')).toHaveAttribute('data-selected', 'true');
    expect(noFrame(table)).toBe(true);
    expect(rowsOf(table).some((row) => row.hasAttribute('data-dragging'))).toBe(false);
    await waitFor(() => expect(actions('MSK-US-22')).toHaveFocus());
  });

  it('the new parent is expanded even when the user had collapsed it', async () => {
    const { table } = await renderEpics();
    // MSK-FEAT-8 collapsed: its child MSK-FEAT-23 is hidden, the row itself is a target.
    press(expandControl('MSK-FEAT-8'));
    expect(outline(table)).not.toContain('    MSK-FEAT-23');

    await dropOnto(table, 'MSK-US-22', 'MSK-FEAT-8');

    await screen.findByRole('alert');
    expect(outline(table)).toContain('    MSK-FEAT-23');
    expect(outline(table)).toContain('    MSK-US-22');
  });
});

describe('US-5 Scenario: A drop changes no place and no other field', () => {
  it('MSK-US-22 dropped onto MSK-FEAT-8 keeps its place on «Истории», its Статус, Исполнитель, Область and planning values', async () => {
    const data = fullTreeDataset();
    const us22 = mskRecord(data, 22);
    us22.assigneeId = ids.anna;
    const api = serveBoards(data);
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();
    const stored = () => {
      const { stateId, assigneeId, areaId, fields, typeId } = mskRecord(api.data, 22);
      return { stateId, assigneeId, areaId, fields: { ...fields }, typeId };
    };
    const before = stored();
    const order = api.order(ids.platformTeam, 'stories');
    const placesBefore = [...api.data.places];

    await dropOnto(table, 'MSK-US-22', 'MSK-FEAT-8');
    await screen.findByRole('alert');

    expect(stored()).toEqual(before);
    expect(api.order(ids.platformTeam, 'stories')).toEqual(order);
    expect(api.data.places).toEqual(placesBefore);
    expect(api.writes().map((request) => request.method)).toEqual(['PUT']);
  });
});

describe('US-5 Scenario: A bug dropped onto a story leaves the top level', () => {
  it('on «Истории» MSK-US-2 reads «1», MSK-BUG-4 «2», MSK-US-22 «3»; MSK-BUG-4 dropped onto MSK-US-22 leaves MSK-US-2 «1» and MSK-US-22 «2», the bug beneath it with no «Порядок»', async () => {
    const api = serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('stories'));
    const table = await findBacklogTable();
    expect(outline(table)).toEqual([
      'MSK-US-2',
      '  MSK-BUG-5',
      '  MSK-TASK-3',
      'MSK-BUG-4',
      'MSK-US-22',
    ]);
    expect(orders(table)).toEqual(['1', '', '', '2', '3']);

    await dropOnto(table, 'MSK-BUG-4', 'MSK-US-22');

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'У MSK-BUG-4 теперь родитель MSK-US-22.',
    );
    expect(parentWrites(api).map((request) => request.body)).toEqual([
      { parentId: mskRecord(api.data, 22).id, seenParentId: mskRecord(api.data, 6).id },
    ]);
    expect(outline(table)).toEqual([
      'MSK-US-2',
      '  MSK-BUG-5',
      '  MSK-TASK-3',
      'MSK-US-22',
      '  MSK-BUG-4',
    ]);
    expect(orders(table)).toEqual(['1', '', '', '2', '']);
  });
});

describe('US-5 Scenario: A nested row can be dragged onto another row', () => {
  it('MSK-BUG-4, nested beneath MSK-FEAT-6, dropped onto MSK-EPIC-20 stands beneath it and no longer beneath MSK-FEAT-6', async () => {
    const { api, table } = await renderEpics();

    await dropOnto(table, 'MSK-BUG-4', 'MSK-EPIC-20');

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'У MSK-BUG-4 теперь родитель MSK-EPIC-20.',
    );
    expect(parentWrites(api).map((request) => request.body)).toEqual([
      { parentId: mskRecord(api.data, 20).id, seenParentId: mskRecord(api.data, 6).id },
    ]);
    expect(outline(table)).toEqual([
      'MSK-EPIC-1',
      '  MSK-FEAT-8',
      '    MSK-FEAT-23',
      '  MSK-FEAT-6',
      '    MSK-US-2',
      '      MSK-BUG-5',
      '      MSK-TASK-3',
      '  MSK-US-22',
      'MSK-EPIC-20',
      '  MSK-BUG-4',
    ]);
    await waitFor(() => expect(actions('MSK-BUG-4')).toHaveFocus());
  });
});

describe('US-5 Scenario Outline: A refused target says why and sends nothing', () => {
  it.each([
    ['MSK-FEAT-6', 'MSK-US-22', LEVEL_TEXT],
    ['MSK-EPIC-20', 'MSK-FEAT-6', LEVEL_TEXT],
    ['MSK-US-22', 'MSK-TASK-3', LEVEL_TEXT],
    [
      'MSK-FEAT-8',
      'MSK-FEAT-23',
      'Нельзя выбрать родителем MSK-FEAT-23: он вложен в MSK-FEAT-8 (MSK-FEAT-8 → MSK-FEAT-23).',
    ],
  ])(
    '%s dragged over %s: the target is framed refused; released, no request is sent and the error notice reads the text',
    async (item, target, text) => {
      const { api, table } = await renderEpics();

      const gesture = new Drag(table, item);
      await gesture.start();
      // A refused row still takes the drop, so that the release can say why.
      expect(await gesture.hover(target)).toBe(true);
      expect(rowOf(table, target)).toHaveAttribute('data-drop-target', 'refused');
      expect(getComputedStyle(rowOf(table, target)).outline).toBe(
        `2px dashed ${theme.palette.error.main}`,
      );
      await gesture.release(target);

      const alert = await screen.findByRole('alert');
      expect(alert).toHaveTextContent(text);
      expect(alert).toHaveClass('MuiAlert-colorError');
      expect(api.writes()).toEqual([]);
      expect(outline(table)).toEqual(WHOLE_TREE);
      expect(noFrame(table)).toBe(true);
    },
  );

  it('an epic dropped onto a feature beneath it is refused for the type — the level is judged before the descendants, as the parent write does', async () => {
    const { api, table } = await renderEpics();

    await dropOnto(table, 'MSK-EPIC-1', 'MSK-FEAT-23');

    expect(await screen.findByRole('alert')).toHaveTextContent(LEVEL_TEXT);
    expect(api.writes()).toEqual([]);
  });
});

describe('US-5 Scenario: The item itself and its current parent are no targets', () => {
  it('MSK-FEAT-6 over its own row, then over MSK-EPIC-1: neither is framed or takes a drop, and releasing there sends nothing and shows nothing', async () => {
    const { api, table } = await renderEpics();

    const gesture = new Drag(table, 'MSK-FEAT-6');
    await gesture.start();
    expect(await gesture.hover('MSK-FEAT-6')).toBe(false);
    expect(noFrame(table)).toBe(true);
    expect(await gesture.hover('MSK-EPIC-1')).toBe(false);
    expect(noFrame(table)).toBe(true);
    await gesture.release('MSK-EPIC-1');

    expect(screen.queryByRole('alert')).toBeNull();
    expect(api.writes()).toEqual([]);
    expect(outline(table)).toEqual(WHOLE_TREE);
  });
});

describe('US-5 Scenario: A descendant on another team is refused by the server', () => {
  it('MSK-FEAT-8 → MSK-FEAT-21 («Мастаск») → MSK-FEAT-26: on «Фичи» MSK-FEAT-26 is a top-level row, outlined as allowed; the drop of MSK-FEAT-8 is refused with the path, the backlog read again, no link changed', async () => {
    const data = fullTreeDataset();
    const feat21 = mskRecord(data, 21);
    feat21.parentId = mskRecord(data, 8).id;
    data.items.push(platformItem(26, 'FEAT', 'Отчёт о входах', { parentId: feat21.id }));
    const api = serveBoards(data);
    renderBoards(backlogAddress('features'));
    const table = await findBacklogTable();
    expect(outline(table)).toEqual(FEATURES_TREE);
    expect(rowsOf(table).map((row) => row.dataset.identifier)).not.toContain('MSK-FEAT-21');

    const gesture = new Drag(table, 'MSK-FEAT-8');
    await gesture.start();
    await gesture.hover('MSK-FEAT-26');
    expect(rowOf(table, 'MSK-FEAT-26')).toHaveAttribute('data-drop-target', 'allowed');
    const reads = api.reads('/backlogs/features').length;
    await gesture.release('MSK-FEAT-26');

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Нельзя выбрать родителем MSK-FEAT-26: он вложен в MSK-FEAT-8 (MSK-FEAT-8 → MSK-FEAT-21 → MSK-FEAT-26).',
    );
    expect(screen.getByRole('alert')).toHaveClass('MuiAlert-colorError');
    expect(parentWrites(api).map((request) => request.body)).toEqual([
      {
        parentId: mskRecord(api.data, 26).id,
        seenParentId: mskRecord(api.data, 1).id,
      },
    ]);
    await waitFor(() => expect(api.reads('/backlogs/features').length).toBeGreaterThan(reads));
    expect(mskRecord(api.data, 8).parentId).toBe(mskRecord(api.data, 1).id);
    expect(outline(table)).toEqual(FEATURES_TREE);
  });
});

describe('US-5 Scenario: A parent changed meanwhile refuses the drop', () => {
  it('Борис’s page shows MSK-US-22 beneath MSK-EPIC-1 after Анна gave it MSK-FEAT-6: his drop onto MSK-FEAT-8 reads the backlog again, then shows the code’s text; the parent is MSK-FEAT-6', async () => {
    const { api, table } = await renderEpics();
    // Анна's write, taken as the server would.
    mskRecord(api.data, 22).parentId = mskRecord(api.data, 6).id;
    const reads = api.reads('/backlogs/epics').length;

    await dropOnto(table, 'MSK-US-22', 'MSK-FEAT-8');

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent(PARENT_CHANGED_TEXT);
    expect(alert).toHaveClass('MuiAlert-colorError');
    expect(parentWrites(api).map((request) => request.body)).toEqual([
      { parentId: mskRecord(api.data, 8).id, seenParentId: mskRecord(api.data, 1).id },
    ]);
    expect(api.reads('/backlogs/epics').length).toBeGreaterThan(reads);
    expect(mskRecord(api.data, 22).parentId).toBe(mskRecord(api.data, 6).id);
    // The read came first: MSK-US-22 already stands beneath MSK-FEAT-6.
    expect(outline(table)).toEqual([
      'MSK-EPIC-1',
      '  MSK-FEAT-8',
      '    MSK-FEAT-23',
      '  MSK-FEAT-6',
      '    MSK-US-2',
      '      MSK-BUG-5',
      '      MSK-TASK-3',
      '    MSK-BUG-4',
      '    MSK-US-22',
      'MSK-EPIC-20',
    ]);
  });
});

describe('US-5 Scenario: No answer', () => {
  it('«Не удалось сохранить родителя. Попробуйте ещё раз.» is shown, the backlog is read again and the link is unchanged', async () => {
    const { api, table } = await renderEpics();
    server.use(http.put(WORK_ITEM_PARENT_URL, () => HttpResponse.error()));
    const reads = api.reads('/backlogs/epics').length;

    await dropOnto(table, 'MSK-US-22', 'MSK-FEAT-8');

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent(FAILED_TEXT);
    expect(alert).toHaveClass('MuiAlert-colorError');
    await waitFor(() => expect(api.reads('/backlogs/epics').length).toBeGreaterThan(reads));
    expect(mskRecord(api.data, 22).parentId).toBe(mskRecord(api.data, 1).id);
    expect(outline(table)).toEqual(WHOLE_TREE);
    await waitFor(() => expect(actions('MSK-US-22')).not.toHaveAttribute('aria-disabled'));
  });
});

describe('US-5 Scenario: A chain that would pass 64 items is refused by the server (D-9)', () => {
  it('`workitems.link.depth_exceeded` is the notice, with the finished text the server sends; the backlog is read again and the link is unchanged', async () => {
    const { api, table } = await renderEpics();
    const text =
      'Слишком глубокая вложенность: в цепочке родителей может быть не больше 64 элементов.';
    server.use(
      http.put(WORK_ITEM_PARENT_URL, () =>
        HttpResponse.json(
          {
            status: 409,
            title: 'workitems.link.depth_exceeded',
            detail: text,
            errorCode: 'workitems.link.depth_exceeded',
            params: { limit: { value: 64, type: 'number' } },
          },
          { status: 409, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    );
    const reads = api.reads('/backlogs/epics').length;

    await dropOnto(table, 'MSK-US-22', 'MSK-FEAT-8');

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent(text);
    expect(alert).toHaveClass('MuiAlert-colorError');
    await waitFor(() => expect(api.reads('/backlogs/epics').length).toBeGreaterThan(reads));
    expect(mskRecord(api.data, 22).parentId).toBe(mskRecord(api.data, 1).id);
    expect(outline(table)).toEqual(WHOLE_TREE);
  });
});

describe('US-5 Scenario: Between rows is still a reorder, for top-level rows only', () => {
  it('MSK-EPIC-20 over the upper quarter of MSK-EPIC-1 shows mod-006’s line, not a frame; releasing reorders it to «1» and changes no link', async () => {
    const { api, table } = await renderEpics();

    const gesture = new Drag(table, 'MSK-EPIC-20');
    await gesture.start();
    expect(await gesture.hover('MSK-EPIC-1', 'upper')).toBe(true);
    expect(rowOf(table, 'MSK-EPIC-1')).toHaveAttribute('data-drop-line', 'top');
    expect(noFrame(table)).toBe(true);
    await gesture.release('MSK-EPIC-1', 'upper');

    await waitFor(() => expect(orders(table)[0]).toBe('1'));
    expect(rowsOf(table)[0]).toHaveAttribute('data-identifier', 'MSK-EPIC-20');
    expect(api.writes().map((request) => request.path.split('/').slice(-1)[0])).toEqual([
      'reorder',
    ]);
    expect(parentWrites(api)).toEqual([]);
  });

  it('with three blocks, the lower quarter of the last row of the first is the line below it; the upper quarter of a nested row, and the middle of any row, is a row target', async () => {
    const data = fullTreeDataset();
    data.items.push(platformItem(30, 'EPIC', 'Третий эпик'));
    const { api, table } = await renderEpics(data);

    const gesture = new Drag(table, 'MSK-EPIC-30');
    await gesture.start();
    // MSK-US-22 is the last row of MSK-EPIC-1's block: the line stands above the next block.
    expect(await gesture.hover('MSK-US-22', 'lower')).toBe(true);
    expect(rowOf(table, 'MSK-EPIC-20')).toHaveAttribute('data-drop-line', 'top');
    expect(noFrame(table)).toBe(true);
    // The lower quarter of a row in the middle of a block is no line: the row is a target.
    await gesture.hover('MSK-FEAT-6', 'lower');
    expect(noLine(table)).toBe(true);
    expect(rowOf(table, 'MSK-FEAT-6')).toHaveAttribute('data-drop-target', 'refused');
    // The middle of a block's first row is the row itself.
    await gesture.hover('MSK-EPIC-1');
    expect(rowOf(table, 'MSK-EPIC-1')).toHaveAttribute('data-drop-target', 'allowed');
    expect(noLine(table)).toBe(true);
    await gesture.end();

    // Below the last block the line is the table's bottom.
    const first = new Drag(table, 'MSK-EPIC-1');
    await first.start();
    await first.hover('MSK-EPIC-30', 'lower');
    expect(rowOf(table, 'MSK-EPIC-30')).toHaveAttribute('data-drop-line', 'bottom');
    await first.end();

    expect(api.writes()).toEqual([]);
  });

  it('a nested row dragged between MSK-FEAT-8 and MSK-FEAT-23 shows no line: only row targets, framed allowed or refused', async () => {
    const { api, table } = await renderEpics();

    const gesture = new Drag(table, 'MSK-FEAT-6');
    await gesture.start();
    await gesture.hover('MSK-FEAT-23', 'upper');
    expect(noLine(table)).toBe(true);
    expect(rowOf(table, 'MSK-FEAT-23')).toHaveAttribute('data-drop-target', 'allowed');
    await gesture.hover('MSK-FEAT-8', 'lower');
    expect(noLine(table)).toBe(true);
    await gesture.end();

    expect(api.writes()).toEqual([]);
  });
});

describe('US-5 Scenario: One write at a time', () => {
  it('while a drop is saved every row is undraggable and every «+», «⋮» and both header buttons is off; the drag of another row does not start; they are on once the backlog is read again', async () => {
    const { api, table } = await renderEpics();
    await addControl('MSK-EPIC-1');
    const { release } = holdParentWrites();

    await dropOnto(table, 'MSK-US-22', 'MSK-FEAT-8');

    await waitFor(() => expect(actions('MSK-FEAT-8')).toHaveAttribute('aria-disabled', 'true'));
    for (const row of rowsOf(table)) {
      const identifier = row.dataset.identifier as string;
      expect(rowOf(table, identifier)).toHaveAttribute('draggable', 'false');
      expect(actions(identifier)).toHaveAttribute('aria-disabled', 'true');
    }
    expect(await addControl('MSK-EPIC-1')).toHaveAttribute('aria-disabled', 'true');
    expect(expandOneLevel()).toHaveAttribute('aria-disabled', 'true');
    expect(collapseOneLevel()).toHaveAttribute('aria-disabled', 'true');
    expect(await new Drag(table, 'MSK-EPIC-20').start()).toBe(false);
    expect(screen.queryByRole('alert')).toBeNull();

    release();
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'У MSK-US-22 теперь родитель MSK-FEAT-8.',
    );
    await waitFor(() => expect(actions('MSK-US-22')).not.toHaveAttribute('aria-disabled'));
    for (const row of rowsOf(table)) {
      expect(row).toHaveAttribute('draggable', 'true');
    }
    expect(await addControl('MSK-EPIC-1')).not.toHaveAttribute('aria-disabled');
    expect(parentWrites(api)).toHaveLength(1);
  });

  it('Escape during a drag — the drag ends with no drop — cancels it with nothing sent and no frame left', async () => {
    const { api, table } = await renderEpics();

    const gesture = new Drag(table, 'MSK-US-22');
    await gesture.start();
    await gesture.hover('MSK-FEAT-8');
    expect(rowOf(table, 'MSK-FEAT-8')).toHaveAttribute('data-drop-target', 'allowed');
    await gesture.end();

    expect(noFrame(table)).toBe(true);
    expect(rowsOf(table).some((row) => row.hasAttribute('data-dragging'))).toBe(false);
    expect(api.writes()).toEqual([]);
    expect(screen.queryByRole('alert')).toBeNull();
    expect(outline(table)).toEqual(WHOLE_TREE);
  });
});
