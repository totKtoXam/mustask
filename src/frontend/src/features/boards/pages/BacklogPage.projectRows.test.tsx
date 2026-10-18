import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { focusManager } from '@tanstack/react-query';
import { theme } from '../../../app/theme';
import { serveBoards } from '../testApi';
import {
  CELL,
  addControl,
  cellOf,
  findBacklogTable,
  identifiers,
  outline,
  rowOf,
} from '../testBacklog';
import { dataTransfer, fireDrag } from '../testDrag';
import { acrossProjectsDataset, boardsDataset, createdAt, ids, item } from '../testFixtures';
import { listedParents, openParentDialog, parentField, parentWrites } from '../testParentDialog';
import { backlogAddress, renderBoards } from '../testRender';

// feat-040 US-4 (BR-TS-5) on a team's backlog of several projects: every write of a row goes to the
// row's own project, and a parent is always of that project. «Платформа» works on «Мастаск» and
// «Операции»; «Фичи» holds MSK-FEAT-6, MSK-FEAT-8 and OPS-FEAT-7, «Эпики» the two epics too.

vi.setConfig({ testTimeout: 60_000 });

afterEach(() => {
  focusManager.setFocused(undefined);
});

const PROJECT_REFUSAL = 'Родителем может быть только рабочий элемент того же проекта.';

function tree() {
  const data = boardsDataset();
  const platform = data.teams.find((team) => team.id === ids.platformTeam);
  if (!platform) throw new Error('No team «Платформа».');
  platform.moreProjectIds = [ids.ops];
  const owned = (minute: number) => ({ teamId: ids.platformTeam, createdAt: createdAt(minute) });
  const epic = item(ids.msk, 1, 'EPIC', 'Единый вход', owned(1));
  data.items.push(
    epic,
    item(ids.ops, 2, 'EPIC', 'Складской учёт', owned(2)),
    item(ids.msk, 6, 'FEAT', 'Вход по ссылке', { ...owned(3), parentId: epic.id }),
    item(ids.msk, 8, 'FEAT', 'Вход через SSO', owned(4)),
    item(ids.ops, 7, 'FEAT', 'Приёмка', owned(5)),
  );
  return data;
}

/** A drag of a row over a row, the browser's own drag and drop as a test drives it. */
async function dragOnto(table: HTMLElement, identifier: string, target: string) {
  const transfer = dataTransfer();
  const source = rowOf(table, identifier);
  const over = cellOf(rowOf(table, target), CELL.title);
  await act(async () => fireDrag('dragStart', source, transfer));
  let takesDrop = false;
  await act(async () => {
    takesDrop = !fireDrag('dragOver', over, transfer);
  });
  const state = rowOf(table, target).getAttribute('data-drop-target');
  await act(async () => fireDrag('drop', over, transfer));
  await act(async () => fireDrag('dragEnd', source, transfer));
  return { takesDrop, state };
}

describe('feat-040 BR-TS-5: a row dropped onto a row of another project is a refused target', () => {
  it('frames the row refused while hovered, sends nothing on release, and says why in the error notice', async () => {
    const api = serveBoards(tree());
    renderBoards(backlogAddress('features'));
    const table = await findBacklogTable();
    expect(identifiers(table)).toEqual(['MSK-FEAT-6', 'MSK-FEAT-8', 'OPS-FEAT-7']);

    const { takesDrop, state } = await dragOnto(table, 'MSK-FEAT-6', 'OPS-FEAT-7');

    // A refused row still takes the drop, so that the release can say why.
    expect(takesDrop).toBe(true);
    expect(state).toBe('refused');
    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent(PROJECT_REFUSAL);
    expect(alert).toHaveClass('MuiAlert-colorError');
    expect(api.writes()).toEqual([]);
    expect(outline(table)).toEqual(['MSK-FEAT-6', 'MSK-FEAT-8', 'OPS-FEAT-7']);
  });

  it('draws the refused frame in the error colour, as the type refusal does', async () => {
    serveBoards(tree());
    renderBoards(backlogAddress('features'));
    const table = await findBacklogTable();
    const transfer = dataTransfer();
    await act(async () => fireDrag('dragStart', rowOf(table, 'MSK-FEAT-6'), transfer));
    await act(async () =>
      fireDrag('dragOver', cellOf(rowOf(table, 'OPS-FEAT-7'), CELL.title), transfer),
    );

    expect(getComputedStyle(rowOf(table, 'OPS-FEAT-7')).outline).toBe(
      `2px dashed ${theme.palette.error.main}`,
    );
  });

  it('is judged before the type: a row of another project is refused though its level would do', async () => {
    serveBoards(tree());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();

    const { state } = await dragOnto(table, 'MSK-FEAT-6', 'OPS-EPIC-2');

    expect(state).toBe('refused');
    expect(await screen.findByRole('alert')).toHaveTextContent(PROJECT_REFUSAL);
  });

  it('leaves a row of the same project allowed: the parent write goes to the item’s own project', async () => {
    const api = serveBoards(tree());
    renderBoards(backlogAddress('features'));
    const table = await findBacklogTable();

    const { takesDrop, state } = await dragOnto(table, 'MSK-FEAT-6', 'MSK-FEAT-8');

    expect(takesDrop).toBe(true);
    expect(state).toBe('allowed');
    await waitFor(() => expect(parentWrites(api)).toHaveLength(1));
    const [write] = parentWrites(api);
    expect(write?.path).toBe(
      `/api/organizations/${ids.example}/projects/${ids.msk}/workitems/MSK-FEAT-6/parent`,
    );
    expect(write?.body).toEqual({
      parentId: api.item(ids.msk, 8).id,
      seenParentId: api.item(ids.msk, 1).id,
    });
  });

  it('an item of the other project writes its parent in that project', async () => {
    const data = tree();
    data.items.push(item(ids.ops, 9, 'FEAT', 'Отгрузка', { teamId: ids.platformTeam }));
    const api = serveBoards(data);
    renderBoards(backlogAddress('features'));
    const table = await findBacklogTable();

    await dragOnto(table, 'OPS-FEAT-9', 'OPS-FEAT-7');

    await waitFor(() => expect(parentWrites(api)).toHaveLength(1));
    expect(parentWrites(api)[0]?.path).toBe(
      `/api/organizations/${ids.example}/projects/${ids.ops}/workitems/OPS-FEAT-9/parent`,
    );
  });
});

describe('feat-040 BR-TS-5: «Изменить родителя…» offers the parents of the row’s own project', () => {
  it('lists the items of «Операции» for an item of «Операции», and none of «Мастаск»', async () => {
    const api = serveBoards(tree());
    renderBoards(backlogAddress('features'));
    await findBacklogTable();

    const dialog = await openParentDialog('OPS-FEAT-7');
    await userEvent.click(parentField(dialog));
    await screen.findByRole('listbox');

    const lines = listedParents();
    expect(lines.some((line) => line.startsWith('OPS-EPIC-2'))).toBe(true);
    expect(lines.filter((line) => line.startsWith('MSK-'))).toEqual([]);
    expect(api.reads('/parent-candidates').map((request) => request.path.split('?')[0])).toEqual([
      `/api/organizations/${ids.example}/projects/${ids.ops}/workitems/OPS-FEAT-7/parent-candidates`,
    ]);
  });
});

describe('feat-040 BR-TS-5: a row’s «+» creates beneath it, in the parent’s project', () => {
  async function addEpicChild(parent: string, type: string, title: string) {
    await userEvent.click(await addControl(parent));
    const input = await screen.findByRole('textbox', { name: `Заголовок: ${type} (${parent})` });
    await waitFor(() => expect(input).toHaveFocus());
    await userEvent.click(input);
    await userEvent.paste(title);
    await userEvent.keyboard('{Enter}');
  }

  it('on an epic of «Операции»: the create goes to «Операции» with the parent and takes the next number there', async () => {
    const api = serveBoards(tree());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();

    await addEpicChild('OPS-EPIC-2', 'Фича', 'Отгрузка со склада');

    expect(await screen.findByText('Рабочий элемент OPS-FEAT-8 создан.')).toBeInTheDocument();
    const [post] = api.writes().filter((request) => request.path.endsWith('/workitems'));
    expect(post?.path).toBe(`/api/organizations/${ids.example}/projects/${ids.ops}/workitems`);
    expect(post?.body).toMatchObject({ parentId: api.item(ids.ops, 2).id });
    await waitFor(() =>
      expect(within(rowOf(table, 'OPS-FEAT-8')).getByText('Операции')).toBeInTheDocument(),
    );
  });

  it('on an epic of «Мастаск»: the create goes to «Мастаск»', async () => {
    const api = serveBoards(tree());
    renderBoards(backlogAddress('epics'));
    await findBacklogTable();

    await addEpicChild('MSK-EPIC-1', 'Фича', 'Вход по коду');

    expect(await screen.findByText('Рабочий элемент MSK-FEAT-9 создан.')).toBeInTheDocument();
    const [post] = api.writes().filter((request) => request.path.endsWith('/workitems'));
    expect(post?.path).toBe(`/api/organizations/${ids.example}/projects/${ids.msk}/workitems`);
  });
});

describe('feat-040 BR-TS-3: the same row and card shape on the project’s own views', () => {
  it('draws «Проект» on a project backlog row too, after «Команда»', async () => {
    serveBoards(acrossProjectsDataset());
    renderBoards(`/organizations/${ids.example}/projects/${ids.ops}/backlogs/stories`);
    const table = await findBacklogTable();

    expect(identifiers(table)).toEqual(['OPS-US-1', 'OPS-US-2', 'OPS-US-3', 'OPS-US-4']);
    expect(within(cellOf(rowOf(table, 'OPS-US-3'), CELL.team)).getByText('Операции')).toBeVisible();
    expect(
      within(cellOf(rowOf(table, 'OPS-US-3'), CELL.project)).getByText('Операции'),
    ).toBeVisible();
    expect(
      within(cellOf(rowOf(table, 'OPS-US-4'), CELL.team)).getByText('Без команды'),
    ).toBeVisible();
  });
});
