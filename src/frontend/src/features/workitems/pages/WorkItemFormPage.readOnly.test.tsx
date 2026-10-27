import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { server } from '../../../test/mocks/server';
import {
  READ_CODES,
  servedFor,
  servedWith,
  type ServedAccess,
  type TestRole,
} from '../../../test/organizationRoles';
import { cardTexts } from '../lib/cardTexts';
import { serveProblem, serveWorkItems } from '../testApi';
import { ITEM_OF_TYPE, retype, save, textbox } from '../testFields';
import { API, WORK_ITEM_URL, hierarchyDataset, ids, problem, recordOf } from '../testFixtures';
import { mskForm, renderWorkItems } from '../testRender';

// mod-016 US-4, design § 6.5 as the card draws it (feat-029 BR-C-19): without
// `workitems.workitem.write` the work item's card is read-only — every field a value, no
// Save group, no undo, and the card says why; a member whose role has just turned to reader
// keeps what they typed on screen under the refusal; a member without `workitems.link.write`
// has «Родитель» read-only on a card that is editable otherwise (design § 6.2). The page
// hosts the card here; the dialog over the backlog and the board is tested with those pages.

// Each user action draws the kit’s fields in jsdom; a test of several steps outlasts the default.
vi.setConfig({ testTimeout: 120_000 });

const STORY = mskForm(ITEM_OF_TYPE['Пользовательская история']);
const HEADING = 'Пользовательская история MSK-US-2';
const ACCESS_URL = `${API}/api/organizations/:organizationId/access`;

/** MSK-US-2 with a parent, a child, an assignee and a stored 1.5 — and everything else empty. */
function storyDataset() {
  const data = hierarchyDataset();
  const story = recordOf(data, 2);
  story.parentId = recordOf(data, 6).id;
  story.assigneeId = ids.anna;
  story.fields.storyPoints = 1.5;
  story.fields.acceptanceCriteria = [{ given: 'Ссылка выдана', when: 'Пользователь открывает её', then: 'Он входит без пароля' }];
  story.description = 'Пользователь входит по ссылке.\nБез пароля.';
  recordOf(data, 3).parentId = story.id;
  return data;
}

async function openAs(role: TestRole | ServedAccess, data = storyDataset()) {
  const api = serveWorkItems(data);
  const shell = renderWorkItems(STORY, { role });
  await screen.findByRole('heading', { level: 1, name: HEADING });
  await screen.findByRole('textbox', { name: 'Заголовок' });
  await waitFor(() => expect(screen.queryByRole('status', { name: 'Загрузка данных' })).toBeNull());
  return { api, shell };
}

const title = () => textbox('Заголовок');
const parent = () => screen.getByRole('combobox', { name: 'Родитель' });
const assignee = () => screen.getByRole('combobox', { name: 'Исполнитель' });

describe('mod-016 design § 6.5 Scenario: the reader’s card', () => {
  it('keeps the page, the `h1` and the type; draws no Save group and no undo; keeps «Обновить» and «Ещё действия»', async () => {
    await openAs('viewer');

    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent(HEADING);
    expect(screen.queryByRole('button', { name: 'Сохранить' })).not.toBeInTheDocument();
    expect(screen.queryByRole('group', { name: 'Сохранение' })).not.toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: 'Отменить все изменения' }),
    ).not.toBeInTheDocument();
    // What only reads stays (BR-C-19).
    expect(screen.getByRole('button', { name: 'Обновить' })).toBeEnabled();
    expect(screen.getByRole('button', { name: 'Ещё действия' })).toBeEnabled();
  });

  it('says why in the card’s notice, in the words of feat-029', async () => {
    await openAs('viewer');

    expect(screen.getByText(cardTexts.readOnly)).toBeVisible();
    expect(cardTexts.readOnly).toBe('Рабочий элемент доступен только для чтения.');
  });

  it('draws every field read-only: the title and the texts refuse typing, the lists say `aria-readonly`', async () => {
    await openAs('viewer');

    const main = screen.getByRole('main');
    const boxes = within(main).getAllByRole('textbox');
    expect(boxes.length).toBeGreaterThan(3);
    for (const box of boxes) {
      expect(box, box.getAttribute('aria-label') ?? '').toHaveAttribute('readonly');
    }
    const lists = within(main).getAllByRole('combobox');
    expect(lists.length).toBeGreaterThan(3);
    for (const list of lists) {
      expect(list, list.getAttribute('aria-label') ?? '').toHaveAttribute('aria-readonly', 'true');
    }
    await userEvent.type(title(), 'x');
    expect(title()).toHaveValue('Письмо со ссылкой');
  });

  it('reads the values: the assignee, the state, the texts and the type’s fields', async () => {
    await openAs('viewer');

    expect(assignee()).toHaveValue('Анна Смирнова');
    expect(screen.getByRole('combobox', { name: 'Статус' })).toHaveTextContent('Новая');
    expect(textbox('Описание')).toHaveValue('Пользователь входит по ссылке.\nБез пароля.');
    // A stored 1.5 reads «1,5», as the card of the administrator shows it.
    expect(textbox('Оценка в пунктах')).toHaveValue('1,5');
  });

  it('shows the parent as the linked item with its «Открыть» link, and the children list', async () => {
    await openAs('viewer');

    expect(parent()).toHaveValue('MSK-FEAT-6 Вход по ссылке');
    expect(parent()).toHaveAttribute('aria-readonly', 'true');
    expect(screen.getByRole('link', { name: 'Открыть MSK-FEAT-6' })).toHaveAttribute(
      'href',
      mskForm('MSK-FEAT-6'),
    );
    expect(screen.getByRole('heading', { level: 3, name: 'Дочерние элементы (1)' })).toBeVisible();
  });

  it('says «Нет родителя» for an item with none, and has no link to open', async () => {
    const data = storyDataset();
    recordOf(data, 2).parentId = null;
    await openAs('viewer', data);

    expect(parent()).toHaveValue('Нет родителя');
    expect(screen.queryByRole('link', { name: /^Открыть MSK-/ })).not.toBeInTheDocument();
  });

  it('never searches the assignee or the parent candidates: that search needs the write code', async () => {
    const { api } = await openAs('viewer');
    await userEvent.click(assignee());
    await userEvent.click(parent());

    const searches = api.requests.filter(
      (request) =>
        request.path.includes('assignee-candidates') || request.path.includes('parent-candidates'),
    );
    expect(searches).toEqual([]);
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
  });

  it('reads the stored 1.5 as the card of the administrator does, editable', async () => {
    await openAs('admin');

    expect(textbox('Оценка в пунктах')).toHaveValue('1,5');
    expect(textbox('Оценка в пунктах')).not.toHaveAttribute('readonly');
    expect(screen.queryByText(cardTexts.readOnly)).not.toBeInTheDocument();
  });
});

describe('mod-016 design § 2.3 Scenario: a member turned reader after a refused save', () => {
  it('keeps what was typed on screen under the refusal, read-only: the typed title and number stay, the Save group is gone', async () => {
    const data = storyDataset();
    await openAs('member', data);
    expect(title()).toHaveValue('Письмо со ссылкой');
    serveProblem('patch', WORK_ITEM_URL, problem('organizations.access.forbidden'), {
      once: true,
    });

    await userEvent.clear(title());
    await userEvent.type(title(), 'Письмо со ссылкой для входа');
    await retype('Оценка в пунктах', '2.5');
    // The codes the 403 makes the shell read again are the reader’s.
    server.use(http.get(ACCESS_URL, () => HttpResponse.json(servedFor('viewer'))));
    await userEvent.click(save());

    // The role-change notice stays 6 s: it is looked for first, while it is on screen.
    expect(
      await screen.findByText(
        'Ваша роль в организации «Пример организации» изменилась: «Читатель».',
      ),
    ).toBeVisible();
    // The re-read shows the reader’s codes: the card is read-only, from the draft.
    await waitFor(() => expect(title()).toHaveAttribute('readonly'));
    expect(screen.getByText('Недостаточно прав')).toBeVisible();
    expect(title()).toHaveValue('Письмо со ссылкой для входа');
    expect(textbox('Оценка в пунктах')).toHaveValue('2.5');
    expect(screen.queryByRole('button', { name: 'Сохранить' })).not.toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: 'Отменить все изменения' }),
    ).not.toBeInTheDocument();
    // The stored item is untouched: the save was refused.
    expect(recordOf(data, 2).title).toBe('Письмо со ссылкой');
  });

  it('with the role unchanged after the 403 the card stays editable with its alert and typed values', async () => {
    await openAs('member');
    serveProblem('patch', WORK_ITEM_URL, problem('organizations.access.forbidden'), {
      once: true,
    });

    await userEvent.clear(title());
    await userEvent.type(title(), 'Другое название');
    await userEvent.click(save());

    expect(await screen.findByText('Недостаточно прав')).toBeVisible();
    expect(title()).toHaveValue('Другое название');
    expect(title()).not.toHaveAttribute('readonly');
    expect(screen.getByRole('button', { name: 'Сохранить' })).toBeEnabled();
    expect(screen.queryByText(/Ваша роль в организации/)).not.toBeInTheDocument();
  });
});

describe('mod-016 design § 6.2 Scenario: a member without the link right', () => {
  it('edits the item and has «Родитель» read-only — the value and its link, not the picker', async () => {
    await openAs(servedWith('member', [...READ_CODES, 'workitems.workitem.write']));

    expect(title()).not.toHaveAttribute('readonly');
    expect(screen.getByRole('button', { name: 'Сохранить' })).toBeVisible();
    expect(parent()).toHaveAttribute('aria-readonly', 'true');
    expect(parent()).toHaveValue('MSK-FEAT-6 Вход по ссылке');
    expect(screen.getByRole('link', { name: 'Открыть MSK-FEAT-6' })).toBeVisible();
    // «Дочерние элементы» is read-only already and stays.
    expect(screen.getByRole('heading', { level: 3, name: 'Дочерние элементы (1)' })).toBeVisible();
    // The card is editable, so it does not say it is read-only.
    expect(screen.queryByText(cardTexts.readOnly)).not.toBeInTheDocument();
  });

  it('saves another field of the item without the parent', async () => {
    const { api } = await openAs(servedWith('member', [...READ_CODES, 'workitems.workitem.write']));

    await userEvent.type(title(), '!');
    await userEvent.click(save());

    expect(await screen.findByText('Изменения сохранены.')).toBeVisible();
    const patches = api.writes().filter((request) => request.method === 'PATCH');
    expect(patches.map((request) => request.body)).toEqual([{ title: 'Письмо со ссылкой!' }]);
  });

  it('a member with the link right gets the picker', async () => {
    await openAs('member');

    expect(parent()).not.toHaveAttribute('aria-readonly', 'true');
  });
});
