import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { server } from '../../../test/mocks/server';
import { serveWorkItems, type WorkItemsTestApi } from '../testApi';
import {
  PARENT_CANDIDATES_URL,
  WORK_ITEM_URL,
  hierarchyDataset,
  ids,
  recordOf,
  stateId,
  type Dataset,
} from '../testFixtures';
import { held } from '../components/card/testCard';
import { mskForm, renderWorkItems } from '../testRender';

// Each user action draws the kit’s fields in jsdom; a test of several steps outlasts the default.
vi.setConfig({ testTimeout: 120_000 });

// feat-001 frontend pass A: the form's section «Связанная работа» — the
// «Родитель» field (US-1), the refusals it shows (US-2) and the children list
// (US-3), on the in-memory API with the stories' data (`hierarchyDataset`).

async function openForm(identifier: string, data: Dataset = hierarchyDataset()) {
  const api = serveWorkItems(data);
  const view = renderWorkItems(mskForm(identifier));
  await screen.findByRole('textbox', { name: 'Заголовок' });
  return { api, view, data };
}

function patches(api: WorkItemsTestApi) {
  return api.writes().filter((request) => request.method === 'PATCH');
}

const parentField = () => screen.getByRole('combobox', { name: 'Родитель' });
const save = held(() => screen.getByRole('button', { name: 'Сохранить' }));
const related = () => screen.getByRole('region', { name: 'Связанная работа' });

/** The open list's lines: an item by «<ID> <Заголовок>», without its state. */
function lines(): string[] {
  return within(screen.getByRole('listbox'))
    .getAllByRole('option')
    .map((option) => {
      const primary = option.querySelector('.MuiListItemText-primary');
      return (primary ?? option).textContent ?? '';
    });
}

/** Opens «Родитель» and types `text` over whatever it shows. */
async function search(text: string) {
  await userEvent.click(parentField());
  await userEvent.clear(parentField());
  if (text) {
    await userEvent.paste(text);
  }
}

async function pick(name: string | RegExp) {
  await userEvent.click(await screen.findByRole('option', { name }));
}

/** The children list's rows as their text, or none when the list is empty. */
function childRows(count: number): string[] {
  const list = within(related()).queryByRole('list', { name: `Дочерние элементы (${count})` });
  return list
    ? within(list)
        .getAllByRole('listitem')
        .map((row) => row.textContent ?? '')
    : [];
}

/** The dataset with `child` hung under `parent` (both «Мастаск» numbers). */
function linked(pairs: readonly (readonly [number, number])[], data = hierarchyDataset()): Dataset {
  for (const [child, parent] of pairs) {
    recordOf(data, child).parentId = recordOf(data, parent).id;
  }
  return data;
}

describe('feat-001 US-1 Scenario: A parent is set and saved', () => {
  it('offers «Нет родителя» first, then the allowed items most recently changed first; «ссылк» → MSK-FEAT-6 is saved and stays after a reload', async () => {
    const { api, view, data } = await openForm('MSK-US-2');
    expect(within(related()).getByRole('heading', { level: 2 })).toHaveTextContent(
      'Связанная работа',
    );
    expect(parentField()).toHaveValue('Нет родителя');
    await waitFor(() =>
      expect(parentField()).toHaveAccessibleDescription(
        'Родителем может быть: Эпик, Фича, Пользовательская история, Баг.',
      ),
    );
    expect(api.reads('parent-candidates')).toEqual([]);

    await userEvent.click(parentField());

    await screen.findByRole('option', { name: /MSK-FEAT-8 Вход через SSO/ });
    expect(lines()).toEqual([
      'Нет родителя',
      'MSK-FEAT-8 Вход через SSO',
      'MSK-FEAT-6 Вход по ссылке',
      'MSK-BUG-4 Ссылка истекает раньше срока',
      'MSK-EPIC-1 Вход и регистрация',
    ]);
    expect(api.reads('parent-candidates')[0]?.path).toBe(
      `/api/organizations/${ids.example}/projects/${ids.msk}/workitems/MSK-US-2/parent-candidates?text=`,
    );

    await search('ссылк');
    await waitFor(() =>
      expect(lines()).toEqual([
        'Нет родителя',
        'MSK-FEAT-6 Вход по ссылке',
        'MSK-BUG-4 Ссылка истекает раньше срока',
      ]),
    );
    await pick(/MSK-FEAT-6 Вход по ссылке/);
    expect(parentField()).toHaveValue('MSK-FEAT-6 Вход по ссылке');
    await userEvent.click(save());

    expect(await screen.findByText('Изменения сохранены.')).toBeInTheDocument();
    const feat6 = recordOf(data, 6);
    expect(patches(api).map((request) => request.body)).toEqual([
      { parent: { parentId: feat6.id, seenParentId: null } },
    ]);
    expect(parentField()).toHaveValue('MSK-FEAT-6 Вход по ссылке');
    expect(within(related()).getByRole('link', { name: 'Открыть MSK-FEAT-6' })).toBeInTheDocument();
    expect(save()).toBeDisabled();

    view.unmount();
    renderWorkItems(mskForm('MSK-US-2'));
    await screen.findByRole('textbox', { name: 'Заголовок' });
    expect(parentField()).toHaveValue('MSK-FEAT-6 Вход по ссылке');
    expect(within(related()).getByRole('link', { name: 'Открыть MSK-FEAT-6' })).toBeInTheDocument();
  });
});

describe('feat-001 US-1 Scenario: The picker finds an item by its identifier', () => {
  it('«feat-8» offers «MSK-FEAT-8 Вход через SSO» with its state «Новая» as secondary text', async () => {
    await openForm('MSK-US-2');

    await search('feat-8');

    await waitFor(() => expect(lines()).toEqual(['Нет родителя', 'MSK-FEAT-8 Вход через SSO']));
    const option = screen.getByRole('option', { name: /MSK-FEAT-8/ });
    expect(option.querySelector('.MuiListItemText-secondary')).toHaveTextContent('Новая');
  });
});

describe('feat-001 US-1 Scenario: Each type is offered the parents its level allows', () => {
  it.each([
    ['MSK-EPIC-1', 'Эпик', ['Нет родителя', 'Ничего не найдено']],
    ['MSK-FEAT-6', 'Эпик, Фича', ['Нет родителя', 'MSK-FEAT-8', 'MSK-EPIC-1']],
    [
      'MSK-BUG-4',
      'Эпик, Фича, Пользовательская история, Баг',
      ['Нет родителя', 'MSK-FEAT-8', 'MSK-FEAT-6', 'MSK-US-2', 'MSK-EPIC-1'],
    ],
    [
      'MSK-TASK-3',
      'Эпик, Фича, Пользовательская история, Баг, Задача',
      [
        'Нет родителя',
        'MSK-FEAT-8',
        'MSK-FEAT-6',
        'MSK-BUG-4',
        'MSK-US-2',
        'MSK-EPIC-1',
        'MSK-TASK-12',
        'MSK-TASK-11',
        'MSK-TASK-10',
      ],
    ],
  ])(
    '%s: «Родителем может быть: %s.», and only those types offered',
    async (identifier, types, expected) => {
      await openForm(identifier);
      await waitFor(() =>
        expect(parentField()).toHaveAccessibleDescription(`Родителем может быть: ${types}.`),
      );

      await userEvent.click(parentField());

      // An item's line by its identifier alone; «Нет родителя» and hints as they read.
      const short = (line: string) =>
        /^[A-Z]+-[A-Z]+-\d+ /.test(line) ? line.split(' ')[0] : line;
      await waitFor(() => expect(lines().map(short)).toEqual(expected));
      // Never the item itself, never an item of another project.
      expect(lines().some((line) => line.startsWith(`${identifier} `))).toBe(false);
      expect(lines().some((line) => line.startsWith('OPS-'))).toBe(false);
    },
  );
});

describe('feat-001 US-1 Scenario: Choosing another parent replaces the stored one in one save', () => {
  it('sends MSK-FEAT-8 over the seen MSK-FEAT-6; the three items’ «Изменён» move together; MSK-FEAT-8 lists the child', async () => {
    const { api, data } = await openForm('MSK-US-2', linked([[2, 6]]));
    expect(parentField()).toHaveValue('MSK-FEAT-6 Вход по ссылке');
    const before = recordOf(data, 2).changedAt;

    await search('SSO');
    await pick(/MSK-FEAT-8 Вход через SSO/);
    await userEvent.click(save());

    expect(await screen.findByText('Изменения сохранены.')).toBeInTheDocument();
    expect(patches(api).map((request) => request.body)).toEqual([
      { parent: { parentId: recordOf(data, 8).id, seenParentId: recordOf(data, 6).id } },
    ]);
    expect(recordOf(data, 2).parentId).toBe(recordOf(data, 8).id);
    const moved = recordOf(data, 2).changedAt;
    expect(Date.parse(moved)).toBeGreaterThan(Date.parse(before));
    expect(recordOf(data, 6).changedAt).toBe(moved);
    expect(recordOf(data, 8).changedAt).toBe(moved);
    expect(parentField()).toHaveValue('MSK-FEAT-8 Вход через SSO');

    await userEvent.click(within(related()).getByRole('link', { name: 'Открыть MSK-FEAT-8' }));

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Фича MSK-FEAT-8' }),
    ).toBeInTheDocument();
    expect(childRows(1)).toEqual([expect.stringContaining('MSK-US-2Письмо со ссылкой')]);
    expect(api.answer(recordOf(data, 6)).children).toEqual([]);
  });
});

describe('feat-001 US-1 Scenario: «Нет родителя» clears the parent', () => {
  it('sends `parentId: null` with the seen parent; the field reads «Нет родителя» and «Открыть …» is gone', async () => {
    const { api, data } = await openForm('MSK-US-2', linked([[2, 6]]));
    expect(within(related()).getByRole('link', { name: 'Открыть MSK-FEAT-6' })).toBeInTheDocument();

    await userEvent.click(parentField());
    await pick('Нет родителя');
    await userEvent.click(save());

    expect(await screen.findByText('Изменения сохранены.')).toBeInTheDocument();
    expect(patches(api).map((request) => request.body)).toEqual([
      { parent: { parentId: null, seenParentId: recordOf(data, 6).id } },
    ]);
    expect(parentField()).toHaveValue('Нет родителя');
    expect(within(related()).queryByRole('link', { name: /^Открыть/ })).not.toBeInTheDocument();
    expect(recordOf(data, 2).parentId).toBeNull();
    expect(api.answer(recordOf(data, 6)).children).toEqual([]);
  });
});

describe('feat-001 US-1 Scenario: The parent is saved with the other fields, all or none', () => {
  it('stores «Заголовок» and «Родитель» by one save', async () => {
    const { api, data } = await openForm('MSK-US-2');
    await userEvent.type(screen.getByRole('textbox', { name: 'Заголовок' }), '!');
    await search('ссылк');
    await pick(/MSK-FEAT-6/);

    await userEvent.click(save());

    expect(await screen.findByText('Изменения сохранены.')).toBeInTheDocument();
    expect(patches(api).map((request) => request.body)).toEqual([
      {
        title: 'Письмо со ссылкой!',
        parent: { parentId: recordOf(data, 6).id, seenParentId: null },
      },
    ]);
    expect(recordOf(data, 2).title).toBe('Письмо со ссылкой!');
    expect(recordOf(data, 2).parentId).toBe(recordOf(data, 6).id);
  });

  it('when the server refuses the parent, neither is stored, the refusal shows under «Родитель», both inputs stay', async () => {
    const { data } = await openForm('MSK-US-2');
    await userEvent.type(screen.getByRole('textbox', { name: 'Заголовок' }), '!');
    await search('ссылк');
    await pick(/MSK-FEAT-6/);
    // MSK-FEAT-6 reaches «Удалена» before the save lands.
    recordOf(data, 6).stateId = stateId('FEAT', 'Удалена');

    await userEvent.click(save());

    expect(
      await screen.findByText('Удалённый рабочий элемент нельзя связывать.'),
    ).toBeInTheDocument();
    expect(parentField()).toHaveAttribute('aria-invalid', 'true');
    expect(parentField()).toHaveAccessibleDescription(
      'Удалённый рабочий элемент нельзя связывать.',
    );
    expect(parentField()).toHaveValue('MSK-FEAT-6 Вход по ссылке');
    expect(screen.getByRole('textbox', { name: 'Заголовок' })).toHaveValue('Письмо со ссылкой!');
    expect(recordOf(data, 2).title).toBe('Письмо со ссылкой');
    expect(recordOf(data, 2).parentId ?? null).toBeNull();
    expect(save()).toBeEnabled();
    expect(screen.queryByText('Изменения сохранены.')).not.toBeInTheDocument();
  });
});

describe('feat-001 US-1 Scenario: «Сохранить» follows the field', () => {
  it('is enabled after MSK-FEAT-6 is picked and disabled once «Нет родителя» is picked again', async () => {
    await openForm('MSK-US-2');
    expect(save()).toBeDisabled();

    await search('ссылк');
    await pick(/MSK-FEAT-6/);
    expect(save()).toBeEnabled();

    await userEvent.click(parentField());
    await pick('Нет родителя');
    expect(save()).toBeDisabled();
  });
});

describe('feat-001 US-1 Scenario: A save of other fields leaves the parent alone', () => {
  it('Анна’s save of «Исполнитель» sends no `parent`, and MSK-US-2 keeps Борис’s MSK-FEAT-6', async () => {
    const { api, data } = await openForm('MSK-US-2');
    expect(parentField()).toHaveValue('Нет родителя');
    // Борис gives it MSK-FEAT-6 meanwhile.
    recordOf(data, 2).parentId = recordOf(data, 6).id;

    const assignee = screen.getByRole('combobox', { name: 'Исполнитель' });
    await userEvent.click(assignee);
    await userEvent.clear(assignee);
    await userEvent.paste('Смирнова');
    await pick(/Анна Смирнова/);
    await userEvent.click(save());

    expect(await screen.findByText('Изменения сохранены.')).toBeInTheDocument();
    expect(patches(api).map((request) => request.body)).toEqual([
      { assignee: { userId: ids.anna } },
    ]);
    expect(recordOf(data, 2).parentId).toBe(recordOf(data, 6).id);
    expect(parentField()).toHaveValue('MSK-FEAT-6 Вход по ссылке');
  });
});

describe('feat-001 US-1 Scenario: A link changes no other field and no backlog place', () => {
  it('a save of the parent alone keeps «Статус», «Исполнитель» and «Область»', async () => {
    const { data } = await openForm('MSK-US-2');
    const before = { ...recordOf(data, 2) };

    await search('ссылк');
    await pick(/MSK-FEAT-6/);
    await userEvent.click(save());
    await screen.findByText('Изменения сохранены.');

    const after = recordOf(data, 2);
    expect([after.stateId, after.assigneeId, after.areaId, after.title]).toEqual([
      before.stateId,
      before.assigneeId,
      before.areaId,
      before.title,
    ]);
  });
});

describe('feat-001 US-1 Scenario: The field is read-only while the form is saved', () => {
  it('«Родитель» is busy, a value that cannot be edited, with the other fields until the answer', async () => {
    await openForm('MSK-US-2');
    let release: () => void = () => {};
    const held = new Promise<void>((resolve) => {
      release = resolve;
    });
    server.use(
      http.patch(
        WORK_ITEM_URL,
        async () => {
          await held;
          return HttpResponse.json(
            { status: 503, title: 'unavailable', detail: 'Сервис временно недоступен.' },
            { status: 503 },
          );
        },
        { once: true },
      ),
    );
    await search('ссылк');
    await pick(/MSK-FEAT-6/);

    await userEvent.click(save());

    await waitFor(() => expect(parentField()).toHaveAttribute('aria-busy', 'true'));
    expect(parentField()).toHaveAttribute('readonly');
    expect(screen.getByRole('textbox', { name: 'Заголовок' })).toHaveAttribute('readonly');
    release();
    await waitFor(() => expect(parentField()).not.toHaveAttribute('aria-busy'));
    expect(parentField()).not.toHaveAttribute('readonly');
  });
});

describe('feat-001 US-1 Scenario: The parent opens from the field', () => {
  it('«Открыть MSK-FEAT-6» opens the form of MSK-FEAT-6', async () => {
    await openForm('MSK-US-2', linked([[2, 6]]));

    await userEvent.click(within(related()).getByRole('link', { name: 'Открыть MSK-FEAT-6' }));

    expect(screen.getByTestId('location')).toHaveTextContent(mskForm('MSK-FEAT-6'));
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Фича MSK-FEAT-6' }),
    ).toBeInTheDocument();
  });
});

describe('feat-001 US-1 Scenario: A failed search can be retried', () => {
  it('shows «Поиск…», then «Не удалось выполнить поиск. Попробуйте ещё раз.», and typing again searches again', async () => {
    await openForm('MSK-US-2');
    let release: () => void = () => {};
    const held = new Promise<void>((resolve) => {
      release = resolve;
    });
    server.use(
      http.get(
        PARENT_CANDIDATES_URL,
        async () => {
          await held;
          return HttpResponse.error();
        },
        { once: true },
      ),
    );

    await userEvent.click(parentField());

    expect(await screen.findByRole('option', { name: 'Поиск…' })).toHaveAttribute(
      'aria-disabled',
      'true',
    );
    release();
    expect(
      await screen.findByRole('option', {
        name: 'Не удалось выполнить поиск. Попробуйте ещё раз.',
      }),
    ).toHaveAttribute('aria-disabled', 'true');
    expect(lines()[0]).toBe('Нет родителя');

    await userEvent.clear(parentField());
    await userEvent.paste('SSO');

    expect(
      await screen.findByRole('option', { name: /MSK-FEAT-8 Вход через SSO/ }),
    ).toBeInTheDocument();
  });

  it('shows «Ничего не найдено» for no match, and «Показаны первые 20. Уточните запрос.» last past 20', async () => {
    const data = hierarchyDataset();
    for (let number = 20; number < 45; number += 1) {
      data.items.push({
        ...recordOf(data, 8),
        id: `b2000000-0000-4000-8000-${String(number).padStart(12, '0')}`,
        number,
        title: `Фича ${number}`,
      });
    }
    await openForm('MSK-US-2', data);

    await search('Ёжик');
    expect(await screen.findByRole('option', { name: 'Ничего не найдено' })).toHaveAttribute(
      'aria-disabled',
      'true',
    );

    await userEvent.clear(parentField());
    await userEvent.paste('Фича');
    expect(
      await screen.findByRole('option', { name: 'Показаны первые 20. Уточните запрос.' }),
    ).toHaveAttribute('aria-disabled', 'true');
    expect(lines()).toHaveLength(22);
    expect(lines().at(-1)).toBe('Показаны первые 20. Уточните запрос.');
  });
});

describe('feat-001 US-2 Scenario: A parent on a lower level is refused (and A refused replace keeps the old parent)', () => {
  it('the picker never offers MSK-TASK-3; a stale list’s MSK-TASK-3 is refused under «Родитель» and MSK-FEAT-6 stays', async () => {
    const { api, data } = await openForm('MSK-US-2', linked([[2, 6]]));
    await search('Шаблон');
    expect(await screen.findByRole('option', { name: 'Ничего не найдено' })).toBeInTheDocument();
    // A list that offers it anyway: the server still decides.
    server.use(
      http.get(PARENT_CANDIDATES_URL, () =>
        HttpResponse.json({ items: [api.ref(recordOf(data, 3))], moreMatched: false }),
      ),
    );

    await userEvent.clear(parentField());
    await userEvent.paste('Шаблон письма');
    await pick(/MSK-TASK-3 Шаблон письма/);
    await userEvent.click(save());

    expect(
      await screen.findByText(
        'Родителем может быть только рабочий элемент того же или более высокого уровня.',
      ),
    ).toBeInTheDocument();
    expect(parentField()).toHaveValue('MSK-TASK-3 Шаблон письма');
    expect(recordOf(data, 2).parentId).toBe(recordOf(data, 6).id);
    expect(within(related()).getByRole('link', { name: 'Открыть MSK-FEAT-6' })).toBeInTheDocument();
  });
});

describe('feat-001 US-2 Scenario: An item is not its own parent', () => {
  it('the picker on MSK-FEAT-6’s form never offers MSK-FEAT-6', async () => {
    await openForm('MSK-FEAT-6');

    await search('MSK-FEAT');

    await waitFor(() => expect(lines()).toEqual(['Нет родителя', 'MSK-FEAT-8 Вход через SSO']));
  });
});

describe('feat-001 US-2 Scenario: A cycle is refused, naming its path', () => {
  it('MSK-TASK-12 as the parent of MSK-TASK-10 reads the path under «Родитель», every link as it was', async () => {
    const { data } = await openForm('MSK-TASK-10');

    await search('TASK-12');
    await pick(/MSK-TASK-12/);
    await userEvent.click(save());

    expect(
      await screen.findByText(
        'Нельзя выбрать родителем MSK-TASK-12: он вложен в MSK-TASK-10 (MSK-TASK-10 → MSK-TASK-11 → MSK-TASK-12).',
      ),
    ).toBeInTheDocument();
    expect(parentField()).toHaveAttribute('aria-invalid', 'true');
    expect(recordOf(data, 10).parentId ?? null).toBeNull();
    expect(recordOf(data, 11).parentId).toBe(recordOf(data, 10).id);
    expect(recordOf(data, 12).parentId).toBe(recordOf(data, 11).id);
  });
});

describe('feat-001 US-2 Scenario: An item of another project is refused as not found', () => {
  it('no item of «Операции» is offered; one named anyway is refused with «Родитель не найден в этом проекте.»', async () => {
    const { api, data } = await openForm('MSK-FEAT-6');
    await search('Склад');
    expect(await screen.findByRole('option', { name: 'Ничего не найдено' })).toBeInTheDocument();
    const warehouse = data.items.find((record) => record.projectId === ids.ops)!;
    server.use(
      http.get(PARENT_CANDIDATES_URL, () =>
        HttpResponse.json({ items: [api.ref(warehouse)], moreMatched: false }),
      ),
    );

    await userEvent.clear(parentField());
    await userEvent.paste('Склад!');
    await pick(/OPS-EPIC-2 Склад/);
    await userEvent.click(save());

    expect(await screen.findByText('Родитель не найден в этом проекте.')).toBeInTheDocument();
    expect(parentField()).toHaveAttribute('aria-invalid', 'true');
    // Never a frame: the form stays.
    expect(screen.getByRole('heading', { level: 1, name: 'Фича MSK-FEAT-6' })).toBeInTheDocument();
    expect(recordOf(data, 6).parentId ?? null).toBeNull();
  });
});

describe('feat-001 US-2 Scenario: A parent chosen from a stale view is refused', () => {
  it('reads the item again: «Родитель» shows Борис’s MSK-FEAT-8 with the refusal under it, Анна’s title kept', async () => {
    const { api, data } = await openForm('MSK-US-2');
    const reads = () => api.reads('/workitems/MSK-US-2').filter((read) => !read.path.includes('?'));
    expect(reads()).toHaveLength(1);
    await userEvent.type(screen.getByRole('textbox', { name: 'Заголовок' }), '!');
    await search('ссылк');
    await pick(/MSK-FEAT-6/);
    // Борис gives it MSK-FEAT-8 meanwhile.
    recordOf(data, 2).parentId = recordOf(data, 8).id;

    await userEvent.click(save());

    expect(
      await screen.findByText(
        'Родитель уже изменён другим участником. Данные обновлены — проверьте и повторите.',
      ),
    ).toBeInTheDocument();
    expect(parentField()).toHaveValue('MSK-FEAT-8 Вход через SSO');
    expect(parentField()).toHaveAttribute('aria-invalid', 'true');
    expect(within(related()).getByRole('link', { name: 'Открыть MSK-FEAT-8' })).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: 'Заголовок' })).toHaveValue('Письмо со ссылкой!');
    expect(reads()).toHaveLength(2);
    expect(recordOf(data, 2).parentId).toBe(recordOf(data, 8).id);
    expect(recordOf(data, 2).title).toBe('Письмо со ссылкой');

    // Saving again sends Анна's own change only.
    await userEvent.click(save());
    expect(await screen.findByText('Изменения сохранены.')).toBeInTheDocument();
    expect(patches(api).at(-1)?.body).toEqual({ title: 'Письмо со ссылкой!' });
    expect(recordOf(data, 2).parentId).toBe(recordOf(data, 8).id);
  });
});

describe('feat-001 US-2 Scenario: A parent chosen from a stale view is refused — the read again fails', () => {
  it('keeps the refusal under «Родитель», shows the failed read in the Alert, and keeps the input', async () => {
    const { data } = await openForm('MSK-US-2');
    await search('ссылк');
    await pick(/MSK-FEAT-6/);
    recordOf(data, 2).parentId = recordOf(data, 8).id;
    server.use(
      http.get(
        WORK_ITEM_URL,
        () => HttpResponse.json({ status: 500, detail: 'Нет ответа.' }, { status: 500 }),
        { once: true },
      ),
    );

    await userEvent.click(save());

    expect(
      await screen.findByText(
        'Родитель уже изменён другим участником. Данные обновлены — проверьте и повторите.',
      ),
    ).toBeInTheDocument();
    // The card’s notice stands between the band and the body; the field’s own error line is the other alert.
    expect(screen.getAllByRole('alert')[0]).toHaveTextContent('Нет ответа.');
    expect(parentField()).toHaveValue('MSK-FEAT-6 Вход по ссылке');
    expect(save()).toBeEnabled();
  });
});

describe('feat-001 US-2 Scenario: A save with no answer keeps the input', () => {
  it('shows mod-003’s error Alert above the band; the chosen parent stays and «Сохранить» is enabled', async () => {
    await openForm('MSK-US-2');
    server.use(http.patch(WORK_ITEM_URL, () => HttpResponse.error(), { once: true }));
    await search('ссылк');
    await pick(/MSK-FEAT-6/);

    await userEvent.click(save());

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Не удалось соединиться с API. Проверьте, что Mustask.Api запущен.',
    );
    expect(parentField()).toHaveValue('MSK-FEAT-6 Вход по ссылке');
    expect(parentField()).not.toHaveAttribute('aria-invalid', 'true');
    expect(save()).toBeEnabled();
  });
});

describe('feat-001 US-3 Scenario: The children are listed by level, then by number', () => {
  it('MSK-EPIC-1 shows «Дочерние элементы (2)»: MSK-FEAT-6, then MSK-TASK-3, each with its type icon and state', async () => {
    await openForm(
      'MSK-EPIC-1',
      linked([
        [6, 1],
        [3, 1],
      ]),
    );

    const heading = within(related()).getByRole('heading', { level: 3 });
    expect(heading).toHaveTextContent('Дочерние элементы (2)');
    const list = within(related()).getByRole('list', { name: 'Дочерние элементы (2)' });
    const rows = within(list).getAllByRole('listitem');
    expect(rows.map((row) => row.textContent)).toEqual([
      'MSK-FEAT-6Вход по ссылкеНовая',
      'MSK-TASK-3Шаблон письмаНовая',
    ]);
    for (const row of rows) {
      expect(row.querySelector('svg[aria-hidden="true"]')).not.toBeNull();
    }
  });
});

describe('feat-001 US-3 Scenario: Only direct children are listed', () => {
  it('MSK-US-2 and MSK-BUG-4, the children of MSK-FEAT-6, are not on MSK-EPIC-1’s list', async () => {
    await openForm(
      'MSK-EPIC-1',
      linked([
        [6, 1],
        [3, 1],
        [2, 6],
        [4, 6],
      ]),
    );

    const rows = childRows(2).join(' ');
    expect(rows).not.toContain('MSK-US-2');
    expect(rows).not.toContain('MSK-BUG-4');
  });
});

describe('feat-001 US-3 Scenario: A child opens from the list', () => {
  it('MSK-FEAT-6 opens its form, whose «Родитель» reads «MSK-EPIC-1 Вход и регистрация»', async () => {
    await openForm(
      'MSK-EPIC-1',
      linked([
        [6, 1],
        [3, 1],
      ]),
    );

    await userEvent.click(within(related()).getByRole('link', { name: 'MSK-FEAT-6' }));

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Фича MSK-FEAT-6' }),
    ).toBeInTheDocument();
    expect(parentField()).toHaveValue('MSK-EPIC-1 Вход и регистрация');
  });
});

describe('feat-001 US-3 Scenario: An item with no child says so', () => {
  it('MSK-US-2 shows «Дочерние элементы (0)» and «Дочерних элементов нет.»', async () => {
    await openForm('MSK-US-2');

    expect(within(related()).getByRole('heading', { level: 3 })).toHaveTextContent(
      'Дочерние элементы (0)',
    );
    expect(within(related()).getByText('Дочерних элементов нет.')).toBeInTheDocument();
    expect(within(related()).queryByRole('list')).not.toBeInTheDocument();
  });
});

describe('feat-001 US-3 Scenario: The list follows a link written elsewhere at the next read', () => {
  it('MSK-FEAT-8’s list is unchanged until its form is saved, then reads MSK-US-2', async () => {
    const { data } = await openForm('MSK-FEAT-8');
    // Another tab gives MSK-US-2 the parent MSK-FEAT-8.
    recordOf(data, 2).parentId = recordOf(data, 8).id;
    expect(within(related()).getByText('Дочерних элементов нет.')).toBeInTheDocument();

    await userEvent.type(screen.getByRole('textbox', { name: 'Описание' }), 'Шаги');
    await userEvent.click(save());

    expect(await screen.findByText('Изменения сохранены.')).toBeInTheDocument();
    expect(childRows(1)).toEqual(['MSK-US-2Письмо со ссылкойНовая']);
  });
});

describe('feat-001 US-3 Scenario: The list is read-only', () => {
  it('offers no control on MSK-FEAT-6’s list but the children’s links', async () => {
    await openForm(
      'MSK-FEAT-6',
      linked([
        [2, 6],
        [4, 6],
      ]),
    );

    const list = within(related()).getByRole('list', { name: 'Дочерние элементы (2)' });
    expect(within(list).queryAllByRole('button')).toEqual([]);
    expect(within(list).queryAllByRole('checkbox')).toEqual([]);
    expect(
      within(list)
        .getAllByRole('link')
        .map((link) => link.textContent),
    ).toEqual(['MSK-US-2', 'MSK-BUG-4']);
  });
});

describe('feat-001 US-7 Scenario: The child’s form lets go of a deleted parent', () => {
  it('reads «MSK-EPIC-1 Вход и регистрация» with «Удалена»; «Нет родителя» saves, MSK-EPIC-1 unchanged', async () => {
    const data = linked([[8, 1]]);
    const epic = recordOf(data, 1);
    epic.stateId = stateId('EPIC', 'Удалена');
    const epicBefore = { ...epic };
    const { api } = await openForm('MSK-FEAT-8', data);
    expect(parentField()).toHaveValue('MSK-EPIC-1 Вход и регистрация');
    expect(within(related()).getByText('Удалена')).toBeInTheDocument();

    await userEvent.click(parentField());
    await pick('Нет родителя');
    await userEvent.click(save());

    expect(await screen.findByText('Изменения сохранены.')).toBeInTheDocument();
    expect(patches(api)[0]?.body).toEqual({
      parent: { parentId: null, seenParentId: epic.id },
    });
    expect(recordOf(data, 8).parentId).toBeNull();
    expect(recordOf(data, 1)).toEqual(epicBefore);
  });
});

describe('feat-001 spec § Screens and copy: loading covers the section', () => {
  it('shows one «Загрузка данных» and a hidden skeleton for «Связанная работа» while the item is read', async () => {
    serveWorkItems(hierarchyDataset());
    let release: () => void = () => {};
    const held = new Promise<void>((resolve) => {
      release = resolve;
    });
    server.use(
      http.get(
        WORK_ITEM_URL,
        async () => {
          await held;
          return HttpResponse.json({ status: 500, detail: 'Нет ответа.' }, { status: 500 });
        },
        { once: true },
      ),
    );
    renderWorkItems(mskForm('MSK-US-2'));

    expect(await screen.findByRole('status', { name: 'Загрузка данных' })).toBeInTheDocument();
    expect(document.querySelectorAll('[aria-label="Загрузка данных"]')).toHaveLength(1);
    expect(screen.queryByRole('region', { name: 'Связанная работа' })).not.toBeInTheDocument();
    release();
    expect(await screen.findByRole('alert')).toHaveTextContent('Нет ответа.');
  });
});
