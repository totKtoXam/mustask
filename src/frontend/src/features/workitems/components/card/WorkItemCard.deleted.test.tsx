import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { server } from '../../../../test/mocks/server';
import {
  DELETED_AT_TEXT,
  WORK_ITEM_URL,
  deleteRecord,
  ids,
  recordOf,
  stateId,
  type Dataset,
} from '../../testFixtures';
import { typeInto } from './testCard';
import { menuEntries, moreActions, openAs } from './testDeletion';

// mod-005 US-3 and BR-DR-7, design § 4 (the deleted card, the mode that follows the read) and
// § 8 (a save that loses to a deletion, D-5), § 9 (what each role sees). Each user action draws
// the kit's fields in jsdom; a test of several steps outlasts the default.

vi.setConfig({ testTimeout: 120_000 });

const STORY = 'MSK-US-2';
const FIRST_SENTENCE = 'Рабочий элемент удалён и доступен только для чтения.';
const SECOND_SENTENCE = 'Восстановите его, чтобы вносить изменения.';
const title = () => screen.getByRole('textbox', { name: 'Заголовок' });
const restoreButton = () => screen.queryByRole('button', { name: 'Восстановить' });
const saveButton = () => screen.queryByRole('button', { name: 'Сохранить' });

/** MSK-US-2, «Активна» when Ann deleted it, assigned to Анна Кузнецова, MSK-TASK-3 its child. */
function deletedStory(data: Dataset) {
  const story = recordOf(data, 2);
  story.stateId = stateId('US', 'Активна');
  story.assigneeId = ids.annaK;
  recordOf(data, 3).parentId = story.id;
  deleteRecord(story);
}

/** The deleted card's notice: the region the information note stands in. */
function notice() {
  return screen
    .getByText(FIRST_SENTENCE, { exact: false })
    .closest('[role="status"]') as HTMLElement;
}

describe('mod-005 US-3 Scenario: The card of a deleted item is read-only (design § 4)', () => {
  it('draws every field as a value, no Save group and no undo; «Обновить» and «Ещё действия» stay; «Восстановить» stands where «Сохранить» did', async () => {
    await openAs(STORY, { role: 'member', prepare: (data) => deletedStory(data) });

    for (const box of screen.getAllByRole('textbox')) {
      expect(box, box.getAttribute('aria-label') ?? '').toHaveAttribute('readonly');
    }
    for (const list of screen.getAllByRole('combobox')) {
      expect(list, list.getAttribute('aria-label') ?? '').toHaveAttribute('aria-readonly', 'true');
    }
    expect(saveButton()).not.toBeInTheDocument();
    expect(screen.queryByRole('group', { name: 'Сохранение' })).not.toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: 'Отменить все изменения' }),
    ).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Обновить' })).toBeEnabled();
    expect(moreActions()).toBeEnabled();
    expect(restoreButton()).toBeEnabled();
    expect(screen.getByRole('combobox', { name: 'Статус' })).toHaveTextContent('Удалена');
    await userEvent.type(title(), 'x');
    expect(title()).toHaveValue('Письмо со ссылкой');
  });

  it('says why in an information notice, how to get the item back, and who deleted it and when', async () => {
    await openAs(STORY, { role: 'member', prepare: (data) => deletedStory(data) });

    const text = notice().textContent;

    expect(text).toContain(`${FIRST_SENTENCE} ${SECOND_SENTENCE}`);
    expect(text).toContain(`Удалил(а): Анна Смирнова, ${DELETED_AT_TEXT}`);
    // «Изменён» is the deletion's time, as the server returns it.
    expect(screen.getByText(`Изменён: ${DELETED_AT_TEXT}`)).toBeVisible();
  });

  it('keeps «Родитель» and «Дочерние элементы» as stored, the link to the child open', async () => {
    await openAs(STORY, {
      role: 'member',
      prepare: (data) => {
        deletedStory(data);
        recordOf(data, 2).parentId = recordOf(data, 6).id;
      },
    });

    expect(screen.getByRole('combobox', { name: 'Родитель' })).toHaveValue(
      'MSK-FEAT-6 Вход по ссылке',
    );
    expect(screen.getByRole('heading', { level: 3, name: 'Дочерние элементы (1)' })).toBeVisible();
    expect(screen.getByRole('link', { name: 'Открыть MSK-FEAT-6' })).toBeVisible();
  });

  it('a reader sees the first sentence alone, the deletion line, and no «Восстановить»', async () => {
    await openAs(STORY, { role: 'viewer', prepare: (data) => deletedStory(data) });

    const text = notice().textContent;

    expect(text).toContain(FIRST_SENTENCE);
    expect(text).not.toContain(SECOND_SENTENCE);
    expect(text).toContain(`Удалил(а): Анна Смирнова, ${DELETED_AT_TEXT}`);
    expect(restoreButton()).not.toBeInTheDocument();
    expect(saveButton()).not.toBeInTheDocument();
  });

  it('an item with no marks (put in «Удалена» outside Delete) shows no line and keeps «Восстановить»', async () => {
    await openAs(STORY, {
      role: 'member',
      prepare: (data) => deleteRecord(recordOf(data, 2), { marks: false }),
    });

    expect(notice().textContent).toContain(`${FIRST_SENTENCE} ${SECOND_SENTENCE}`);
    expect(notice().textContent).not.toContain('Удалил(а)');
    expect(restoreButton()).toBeEnabled();
  });
});

describe('mod-005 BR-DR-7: the deleter is named as «Исполнитель» is, with the marks of BR-13', () => {
  const line = () => notice().textContent ?? '';

  it('an inactive account carries «(неактивен)»', async () => {
    await openAs(STORY, {
      prepare: (data) => {
        deletedStory(data);
        const ann = data.users.find((user) => user.id === ids.anna);
        if (ann) ann.active = false;
      },
    });

    expect(line()).toContain(`Удалил(а): Анна Смирнова (неактивен), ${DELETED_AT_TEXT}`);
  });

  it('a user who left the organization carries «(не в организации)», which prevails', async () => {
    await openAs(STORY, {
      prepare: (data) => {
        deletedStory(data);
        const ann = data.users.find((user) => user.id === ids.anna);
        if (ann) {
          ann.active = false;
          ann.member = false;
        }
      },
    });

    expect(line()).toContain(`Удалил(а): Анна Смирнова (не в организации), ${DELETED_AT_TEXT}`);
  });

  it('a user the directory does not know is «Неизвестный пользователь»', async () => {
    await openAs(STORY, {
      prepare: (data) => {
        deletedStory(data);
        const marks = recordOf(data, 2).deletion;
        if (marks) marks.byId = ids.ghost;
      },
    });

    expect(line()).toContain(
      `Удалил(а): Неизвестный пользователь (не в организации), ${DELETED_AT_TEXT}`,
    );
  });
});

describe('mod-005 design § 2, § 9: the «⋮» menu of a deleted card, per role', () => {
  it('the administrator is offered «Копировать ссылку», a rule and «Удалить навсегда»', async () => {
    await openAs(STORY, { role: 'admin', prepare: (data) => deletedStory(data) });

    const { names, rules } = await menuEntries();

    expect(names).toEqual(['Копировать ссылку', 'Удалить навсегда']);
    expect(rules).toBe(1);
  });

  it('«Удалить навсегда» is the entry in the error colour', async () => {
    await openAs(STORY, { role: 'admin', prepare: (data) => deletedStory(data) });
    await userEvent.click(moreActions());

    const copy = await screen.findByRole('menuitem', { name: 'Копировать ссылку' });
    const purge = screen.getByRole('menuitem', { name: 'Удалить навсегда' });

    expect(getComputedStyle(purge).color).not.toBe(getComputedStyle(copy).color);
  });

  it.each(['member', 'viewer'] as const)(
    'a %s is offered «Копировать ссылку» alone',
    async (role) => {
      await openAs(STORY, { role, prepare: (data) => deletedStory(data) });

      const { names, rules } = await menuEntries();

      expect(names).toEqual(['Копировать ссылку']);
      expect(rules).toBe(0);
    },
  );
});

describe('mod-005 design § 4: the mode follows the read', () => {
  it('a live card with a draft that another user deleted turns read-only on «Обновить»: the draft is dropped, the notice says only that the data is updated', async () => {
    const { api, notify } = await openAs(STORY, { role: 'member' });
    typeInto(title(), '!');
    deleteRecord(recordOf(api.data, 2));

    await userEvent.click(screen.getByRole('button', { name: 'Обновить' }));

    await waitFor(() => expect(restoreButton()).toBeEnabled());
    expect(title()).toHaveValue('Письмо со ссылкой');
    expect(title()).toHaveAttribute('readonly');
    expect(saveButton()).not.toBeInTheDocument();
    expect(notify).toHaveBeenCalledWith('Данные обновлены.');
    expect(notify).not.toHaveBeenCalledWith(expect.stringContaining('остались на месте'));
  });

  it('a deleted card that another user restored turns editable on «Обновить», with an empty draft and no note', async () => {
    const { api, notify } = await openAs(STORY, {
      role: 'member',
      prepare: (data) => deletedStory(data),
    });
    const record = recordOf(api.data, 2);
    record.stateId = stateId('US', 'Активна');
    record.deletion = null;

    await userEvent.click(screen.getByRole('button', { name: 'Обновить' }));

    await waitFor(() => expect(saveButton()).toBeDisabled());
    expect(title()).not.toHaveAttribute('readonly');
    expect(restoreButton()).not.toBeInTheDocument();
    expect(screen.queryByText(FIRST_SENTENCE, { exact: false })).not.toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'Статус' })).toHaveTextContent('Активна');
    expect(notify).toHaveBeenCalledWith('Данные обновлены.');
  });
});

describe('mod-005 design § 8, D-5: a save that loses to a deletion', () => {
  it('tells the server’s text as an error notice, reads the item, turns read-only and drops the draft', async () => {
    const { api, notify } = await openAs(STORY, { role: 'member' });
    typeInto(title(), '!');
    deleteRecord(recordOf(api.data, 2));

    await userEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    await waitFor(() => expect(restoreButton()).toBeEnabled());
    expect(notify).toHaveBeenCalledWith(FIRST_SENTENCE, 'error');
    expect(title()).toHaveValue('Письмо со ссылкой');
    expect(saveButton()).not.toBeInTheDocument();
    // The card's error area is not used: the deleted note says why, the notice told the server's text.
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(recordOf(api.data, 2).title).toBe('Письмо со ссылкой');
  });

  it('when the read after the refusal fails the text stays in the card’s error area, and «Обновить» tries again', async () => {
    const { api } = await openAs(STORY, { role: 'member' });
    typeInto(title(), '!');
    deleteRecord(recordOf(api.data, 2));
    server.use(
      http.get(
        WORK_ITEM_URL,
        () => HttpResponse.json({ detail: 'Сбой чтения.' }, { status: 500 }),
        {
          once: true,
        },
      ),
    );

    await userEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(FIRST_SENTENCE);
    await userEvent.click(screen.getByRole('button', { name: 'Обновить' }));

    await waitFor(() => expect(restoreButton()).toBeEnabled());
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(title()).toHaveValue('Письмо со ссылкой');
  });
});

describe('mod-005 BR-DR-9, BR-DR-10 x feat-028 Scenario: a deleted card shows its owning team as stored', () => {
  const team = () => screen.getByRole('combobox', { name: 'Команда' });

  it('shows the team the item holds now (an unlink’s destination) as a read-only value, with no read of the project’s teams', async () => {
    const { api } = await openAs(STORY, {
      role: 'member',
      prepare: (data) => {
        recordOf(data, 2).teamId = ids.marsTeam;
        deletedStory(data);
        // MARS was unlinked from the project: the deleted item went to VENUS and stayed deleted.
        recordOf(data, 2).teamId = ids.venusTeam;
      },
    });

    expect(team()).toHaveValue('VENUS');
    expect(team()).toHaveAttribute('aria-readonly', 'true');
    expect(screen.getByRole('combobox', { name: 'Статус' })).toHaveTextContent('Удалена');
    expect(api.reads('/teams')).toHaveLength(0);
  });

  it('shows «Без команды» for one whose team was deleted (the deletion released it), still deleted', async () => {
    await openAs(STORY, {
      role: 'member',
      prepare: (data) => {
        recordOf(data, 2).teamId = ids.marsTeam;
        deletedStory(data);
        recordOf(data, 2).teamId = null;
      },
    });

    expect(team()).toHaveValue('Без команды');
    expect(team()).toHaveAttribute('aria-readonly', 'true');
    expect(restoreButton()).toBeEnabled();
  });
});

describe('mod-005 design § 2: a deleted card still copies its link', () => {
  it('puts the page’s address on the clipboard for a reader too', async () => {
    const user = userEvent.setup();
    const { notify } = await openAs(STORY, {
      role: 'viewer',
      prepare: (data) => deletedStory(data),
    });

    await user.click(moreActions());
    await user.click(await screen.findByRole('menuitem', { name: 'Копировать ссылку' }));

    const address = `${window.location.origin}/organizations/${ids.example}/projects/${ids.msk}/workitems/MSK-US-2`;
    expect(await navigator.clipboard.readText()).toBe(address);
    expect(notify).toHaveBeenCalledWith('Ссылка скопирована.');
  });
});
