import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { server } from '../../../test/mocks/server';
import { CATEGORY_MARKER_CLASS } from '../../../shared/ui/WorkTrackingMarks';
import { serveProblem, serveWorkItems, type WorkItemsTestApi } from '../testApi';
import {
  AREAS_URL,
  WORK_ITEM_URL,
  emptyDataset,
  exampleDataset,
  ids,
  item,
  problem,
  stateId,
  type Dataset,
} from '../testFixtures';
import { MSK_LIST, mskForm, renderWorkItems } from '../testRender';

async function openForm(identifier: string, data: Dataset = exampleDataset()) {
  const api = serveWorkItems(data);
  renderWorkItems(mskForm(identifier));
  await screen.findByRole('textbox', { name: 'Заголовок' });
  return api;
}

function patches(api: WorkItemsTestApi) {
  return api.writes().filter((request) => request.method === 'PATCH');
}

const save = () => screen.getByRole('button', { name: 'Сохранить' });
const assignee = () => screen.getByRole('combobox', { name: 'Исполнитель' });
const area = () => screen.getByRole('combobox', { name: 'Область' });
const status = () => screen.getByRole('combobox', { name: 'Статус' });

/** The open list's lines, by their visible text without the avatars' initials. */
function lines(): string[] {
  return within(screen.getByRole('listbox'))
    .getAllByRole('option')
    .map((option) => {
      const name = option.querySelector('.MuiListItemText-primary');
      return (name ?? option).textContent ?? '';
    });
}

describe('mod-003 US-4 Scenario: The area picker offers the project’s whole tree', () => {
  it('offers «Мастаск», «Отчёты», «Платформа», «Платформа / API» in tree order, not «Склад»', async () => {
    const api = await openForm('MSK-TASK-5');
    expect(api.reads('/areas')).toEqual([]);

    await userEvent.click(area());

    await screen.findByRole('option', { name: 'Мастаск' });
    expect(lines()).toEqual(['Мастаск', 'Отчёты', 'Платформа', 'Платформа / API']);
    expect(screen.queryByRole('option', { name: 'Склад' })).not.toBeInTheDocument();
  });

  it('says «Загрузка…» while the tree loads; a failed read says so, and opening again retries', async () => {
    await openForm('MSK-TASK-5');
    let release: () => void = () => {};
    const held = new Promise<void>((resolve) => {
      release = resolve;
    });
    server.use(
      http.get(
        AREAS_URL,
        async () => {
          await held;
          return HttpResponse.json({ status: 500, detail: 'Сбой.' }, { status: 500 });
        },
        { once: true },
      ),
    );

    await userEvent.click(area());
    expect(await screen.findByText('Загрузка…')).toBeInTheDocument();
    release();
    expect(
      await screen.findByText('Не удалось загрузить области. Попробуйте ещё раз.'),
    ).toBeInTheDocument();

    await userEvent.keyboard('{Escape}');
    await userEvent.click(area());

    expect(await screen.findByRole('option', { name: 'Отчёты' })).toBeInTheDocument();
  });

  it('a text matching no area: «Ничего не найдено»', async () => {
    await openForm('MSK-TASK-5');
    await userEvent.click(area());
    await screen.findByRole('option', { name: 'Мастаск' });

    await userEvent.clear(area());
    await userEvent.type(area(), 'Склад');

    expect(await screen.findByText('Ничего не найдено')).toBeInTheDocument();
  });
});

describe('mod-003 US-4 Scenario: An area is chosen and saved', () => {
  it('sends the area only, and the form shows «Платформа / API»', async () => {
    const api = await openForm('MSK-TASK-5');
    expect(area()).toHaveValue('Мастаск');

    await userEvent.click(area());
    await userEvent.click(await screen.findByRole('option', { name: 'Платформа / API' }));
    await userEvent.click(save());

    expect(await screen.findByText('Изменения сохранены.')).toBeInTheDocument();
    expect(patches(api)[0]?.body).toEqual({ areaId: ids.api });
    expect(area()).toHaveValue('Платформа / API');
  });
});

describe('mod-003 US-4 Scenario: An area of another project is refused (and US-6 "A deletion and an item entering the area", the save’s half)', () => {
  it('puts «Область не найдена.» under «Область», the choice kept, never a frame', async () => {
    const api = await openForm('MSK-TASK-5');
    await userEvent.click(area());
    await userEvent.click(await screen.findByRole('option', { name: 'Отчёты' }));
    // «Отчёты» is deleted before the save lands.
    api.data.areas.splice(
      api.data.areas.findIndex((candidate) => candidate.id === ids.reports),
      1,
    );

    await userEvent.click(save());

    expect(await screen.findByText('Область не найдена.')).toBeInTheDocument();
    expect(area()).toHaveAttribute('aria-invalid', 'true');
    expect(area()).toHaveValue('Отчёты');
    expect(api.record(ids.msk, 5).areaId).toBe(ids.mskRoot);
    expect(screen.queryByRole('heading', { level: 1, name: /не найден/ })).toBeNull();
  });
});

/** One item per type, all in «Новая», MSK-<TYPE>-1..5. */
function allTypes(): Dataset {
  const data = emptyDataset();
  ['EPIC', 'FEAT', 'US', 'BUG', 'TASK'].forEach((key, index) => {
    data.items.push(item(ids.msk, index + 1, key, `Элемент ${key}`));
  });
  return data;
}

describe('mod-003 US-5 Scenario Outline: The control offers the type’s states without «Удалена»', () => {
  it.each([
    ['MSK-TASK-5', '«Задача»', ['Новая', 'Активна', 'Закрыта']],
    ['MSK-BUG-4', '«Баг»', ['Новая', 'Активна', 'Разрешена', 'Закрыта']],
    ['MSK-US-3', '«Пользовательская история»', ['Новая', 'Активна', 'Разрешена', 'Закрыта']],
    ['MSK-EPIC-1', '«Эпик»', ['Новая', 'Активна', 'Разрешена', 'Закрыта']],
    ['MSK-FEAT-2', '«Фича»', ['Новая', 'Активна', 'Разрешена', 'Закрыта']],
  ])('%s, a %s in «Новая», offers %j, each with its dot', async (identifier, _type, states) => {
    await openForm(identifier, allTypes());
    expect(status()).toHaveTextContent('Новая');

    await userEvent.click(status());

    const listbox = await screen.findByRole('listbox');
    const options = within(listbox).getAllByRole('option');
    expect(options.map((option) => option.textContent)).toEqual(states);
    options.forEach((option) => {
      expect(option.querySelector(`.${CATEGORY_MARKER_CLASS}`)).not.toBeNull();
    });
    expect(within(listbox).getByRole('option', { name: 'Новая' })).toHaveAttribute(
      'aria-selected',
      'true',
    );
  });
});

async function pickState(name: string) {
  await userEvent.click(status());
  await userEvent.click(await screen.findByRole('option', { name }));
}

describe('mod-003 US-5 Scenario: A forward change is saved (and "A state change touches no other field")', () => {
  it('«Разрешена» on MSK-BUG-4 sends the state only; the form and the list show it', async () => {
    const data = allTypes();
    const bug = data.items.find((record) => record.number === 4);
    if (bug) {
      bug.assigneeId = ids.anna;
      bug.areaId = ids.platform;
    }
    const api = await openForm('MSK-BUG-4', data);

    await pickState('Разрешена');
    await userEvent.click(save());

    expect(await screen.findByText('Изменения сохранены.')).toBeInTheDocument();
    expect(patches(api).map((request) => request.body)).toEqual([
      { stateId: stateId('BUG', 'Разрешена') },
    ]);
    expect(status()).toHaveTextContent('Разрешена');
    expect(assignee()).toHaveValue('Анна Смирнова');
    expect(area()).toHaveValue('Платформа');

    await userEvent.click(
      within(screen.getByRole('navigation', { name: 'Путь к странице' })).getByRole('link', {
        name: 'Рабочие элементы',
      }),
    );
    const row = within(await screen.findByRole('table', { name: 'Рабочие элементы' }))
      .getAllByRole('row')
      .find((candidate) => candidate.textContent?.includes('MSK-BUG-4'));
    expect(row).toHaveTextContent('Разрешена');
    expect(screen.getByTestId('location')).toHaveTextContent(MSK_LIST);
  });
});

describe('mod-003 US-5 Scenario: A backward change needs no reason', () => {
  it('a «Пользовательская история» in «Закрыта» goes back to «Новая» with no question asked', async () => {
    const data = allTypes();
    const story = data.items.find((record) => record.number === 3);
    if (story) story.stateId = stateId('US', 'Закрыта');
    const api = await openForm('MSK-US-3', data);

    await pickState('Новая');
    await userEvent.click(save());

    expect(await screen.findByText('Изменения сохранены.')).toBeInTheDocument();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(api.record(ids.msk, 3).stateId).toBe(stateId('US', 'Новая'));
  });
});

describe('mod-003 US-5 Scenario: A change may skip states', () => {
  it('a «Задача» in «Новая» goes straight to «Закрыта»', async () => {
    const api = await openForm('MSK-TASK-5', allTypes());

    await pickState('Закрыта');
    await userEvent.click(save());

    expect(await screen.findByText('Изменения сохранены.')).toBeInTheDocument();
    expect(api.record(ids.msk, 5).stateId).toBe(stateId('TASK', 'Закрыта'));
    expect(status()).toHaveTextContent('Закрыта');
  });
});

describe('mod-003 US-5 Scenario Outline: A state outside the type’s own is refused', () => {
  it('puts «Этот статус недоступен для рабочего элемента этого типа.» under «Статус»', async () => {
    const data = allTypes();
    const task = data.items.find((record) => record.number === 5);
    if (task) task.stateId = stateId('TASK', 'Активна');
    const api = await openForm('MSK-TASK-5', data);
    serveProblem('patch', WORK_ITEM_URL, problem('workitems.workitem.state_not_allowed'), {
      once: true,
    });

    await pickState('Закрыта');
    await userEvent.click(save());

    expect(
      await screen.findByText('Этот статус недоступен для рабочего элемента этого типа.'),
    ).toBeInTheDocument();
    expect(api.record(ids.msk, 5).stateId).toBe(stateId('TASK', 'Активна'));
    expect(status()).toHaveTextContent('Закрыта');
  });
});

describe('mod-003 US-5 Scenario: Choosing the current state changes nothing', () => {
  it('picking «Активна» again leaves «Сохранить» disabled', async () => {
    const data = allTypes();
    const task = data.items.find((record) => record.number === 5);
    if (task) task.stateId = stateId('TASK', 'Активна');
    await openForm('MSK-TASK-5', data);

    await pickState('Активна');

    await waitFor(() => expect(screen.queryByRole('listbox')).not.toBeInTheDocument());
    expect(save()).toBeDisabled();
  });
});
