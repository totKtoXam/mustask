import { describe, expect, it } from 'vitest';
import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { server } from '../../../test/mocks/server';
import { serveWorkItems, type WorkItemsTestApi } from '../testApi';
import { WORK_ITEM_URL, exampleDataset, ids, validationProblem } from '../testFixtures';
import { MSK_LIST, mskForm, renderWorkItems } from '../testRender';

const BUG_4 = mskForm('MSK-BUG-4');

async function openForm(path = BUG_4, data = exampleDataset()) {
  const api = serveWorkItems(data);
  const view = renderWorkItems(path);
  await screen.findByRole('textbox', { name: 'Заголовок' });
  return {
    api,
    view,
    title: () => screen.getByRole('textbox', { name: 'Заголовок' }),
    description: () => screen.getByRole('textbox', { name: 'Описание' }),
    save: () => screen.getByRole('button', { name: 'Сохранить' }),
  };
}

function patches(api: WorkItemsTestApi) {
  return api.writes().filter((request) => request.method === 'PATCH');
}

describe('mod-003 US-3 Scenario: The form opens at the item’s address', () => {
  it('reads «Баг MSK-BUG-4» as the h1 and the tab, «Рабочие элементы» leads back, and Тип has no control', async () => {
    await openForm();

    expect(screen.getByRole('heading', { level: 1, name: 'Баг MSK-BUG-4' })).toBeInTheDocument();
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
    await waitFor(() => expect(document.title).toBe('Баг MSK-BUG-4 — Mustask'));
    expect(screen.queryByRole('combobox', { name: 'Тип' })).not.toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: 'Заголовок' })).toHaveValue('Падение при входе');

    const trail = screen.getByRole('navigation', { name: 'Путь к странице' });
    await userEvent.click(within(trail).getByRole('link', { name: 'Рабочие элементы' }));

    expect(screen.getByTestId('location')).toHaveTextContent(MSK_LIST);
    expect(await screen.findByRole('table', { name: 'Рабочие элементы' })).toBeInTheDocument();
  });

  it('marks «Рабочие элементы» in the main navigation as the page’s entry (D-5)', async () => {
    await openForm();

    const entry = within(screen.getByRole('navigation', { name: 'Основное меню' })).getByRole(
      'link',
      { name: 'Рабочие элементы' },
    );
    expect(entry).toHaveAttribute('aria-current', 'true');
    expect(entry).toHaveAttribute('href', MSK_LIST);
  });
});

describe('mod-003 US-3 Scenario Outline: An identifier that resolves to nothing', () => {
  it.each([
    ['msk-bug-4', 'the letters are not capitals'],
    ['MSK-BUG-04', 'the number has a leading zero'],
    ['MSK-BUG-999', '«Мастаск» has no item 999'],
    ['MSK-TASK-4', 'item 4 of «Мастаск» is a «Баг»'],
    ['ABC-BUG-4', 'ABC is not the key of the addressed project'],
  ])('%s (%s) shows «Рабочий элемент не найден» and keeps the entry marked', async (identifier) => {
    const api = serveWorkItems();
    renderWorkItems(mskForm(identifier));

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Рабочий элемент не найден' }),
    ).toBeInTheDocument();
    expect(screen.getByText('Проверьте адрес или вернитесь на главную.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'На главную' })).toHaveAttribute('href', '/home');
    expect(api.reads('/workitems/').map((request) => request.path)).toEqual([
      `/api/organizations/${ids.example}/projects/${ids.msk}/workitems/${identifier}`,
    ]);
    expect(
      within(screen.getByRole('navigation', { name: 'Основное меню' })).getByRole('link', {
        name: 'Рабочие элементы',
      }),
    ).toHaveAttribute('aria-current', 'true');
  });

  it.each([
    [
      'the project id is malformed',
      `/organizations/${ids.example}/projects/xyz/workitems/MSK-BUG-4`,
    ],
    [
      'the project id is of another organization’s project',
      `/organizations/${ids.example}/projects/${ids.otherMsk}/workitems/MSK-BUG-4`,
    ],
  ])('MSK-BUG-4 where %s shows «Проект не найден»', async (_case, path) => {
    serveWorkItems();
    renderWorkItems(path);

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Проект не найден' }),
    ).toBeInTheDocument();
  });

  it('MSK-BUG-4 of a deactivated «Мастаск» shows «Проект не найден»', async () => {
    const data = exampleDataset();
    const project = data.projects.find((candidate) => candidate.id === ids.msk);
    if (project) project.active = false;
    serveWorkItems(data);
    renderWorkItems(BUG_4);

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Проект не найден' }),
    ).toBeInTheDocument();
  });

  it('an unknown organization shows «Организация не найдена»', async () => {
    serveWorkItems();
    renderWorkItems(`/organizations/${ids.msk}/projects/${ids.msk}/workitems/MSK-BUG-4`);

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Организация не найдена' }),
    ).toBeInTheDocument();
  });
});

describe('mod-003 US-3 Scenario: The identifier is resolved inside the address’s organization', () => {
  it('the address of «Пример организации» with the id of «Орг Б»’s MSK shows «Проект не найден»', async () => {
    serveWorkItems();
    renderWorkItems(`/organizations/${ids.example}/projects/${ids.otherMsk}/workitems/MSK-BUG-4`);

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Проект не найден' }),
    ).toBeInTheDocument();
  });
});

describe('mod-003 US-3 Scenario: A title and a description are changed in one save', () => {
  it('sends both and nothing else, notifies, disables «Сохранить», and the list shows the save’s time', async () => {
    const { api, title, description, save } = await openForm();
    expect(save()).toBeDisabled();

    await userEvent.clear(title());
    await userEvent.paste('Падение при входе через SSO');
    await userEvent.type(description(), 'Шаги');
    expect(save()).toBeEnabled();
    await userEvent.click(save());

    expect(await screen.findByText('Изменения сохранены.')).toBeInTheDocument();
    expect(save()).toBeDisabled();
    expect(patches(api)).toEqual([
      {
        method: 'PATCH',
        path: `/api/organizations/${ids.example}/projects/${ids.msk}/workitems/MSK-BUG-4`,
        body: { title: 'Падение при входе через SSO', description: 'Шаги' },
      },
    ]);
    expect(title()).toHaveValue('Падение при входе через SSO');
    expect(description()).toHaveValue('Шаги');
    expect(screen.getByRole('heading', { level: 1, name: 'Баг MSK-BUG-4' })).toBeInTheDocument();

    const trail = screen.getByRole('navigation', { name: 'Путь к странице' });
    await userEvent.click(within(trail).getByRole('link', { name: 'Рабочие элементы' }));
    const table = await screen.findByRole('table', { name: 'Рабочие элементы' });
    const first = within(table).getAllByRole('row')[1] as HTMLElement;
    expect(first).toHaveTextContent('MSK-BUG-4');
    expect(first).toHaveTextContent('Падение при входе через SSO');
    expect(first).not.toHaveTextContent('05.10.2026 14:30');
  });

  it('reopening the form shows the stored values', async () => {
    const { api, view, title, description, save } = await openForm();
    await userEvent.type(description(), 'Сохранено');
    await userEvent.click(save());
    await screen.findByText('Изменения сохранены.');

    view.unmount();
    renderWorkItems(BUG_4);

    expect(await screen.findByRole('textbox', { name: 'Описание' })).toHaveValue('Сохранено');
    expect(title()).toHaveValue('Падение при входе');
    expect(api.record(ids.msk, 4).description).toBe('Сохранено');
  });
});

describe('mod-003 US-3 Scenario: Nothing to save', () => {
  it('«Сохранить» is disabled when the form just opened, and again once a change is undone', async () => {
    const { title, save } = await openForm();
    expect(save()).toBeDisabled();

    await userEvent.type(title(), '!');
    expect(save()).toBeEnabled();
    await userEvent.type(title(), '{Backspace}');

    expect(save()).toBeDisabled();
  });
});

describe('mod-003 US-3 Scenario Outline: An invalid value is refused and nothing is stored', () => {
  it('an empty title: «Введите заголовок.» under «Заголовок», the input stays, nothing is sent', async () => {
    const { api, title, save } = await openForm();
    await userEvent.clear(title());

    await userEvent.click(save());

    expect(screen.getByText('Введите заголовок.')).toBeInTheDocument();
    expect(title()).toHaveAttribute('aria-invalid', 'true');
    expect(title()).toHaveAccessibleDescription('Введите заголовок.');
    expect(title()).toHaveValue('');
    expect(patches(api)).toEqual([]);
  });

  it('a title of 256 characters: «Не более 255 символов.»', async () => {
    const { api, title, save } = await openForm();
    fireEvent.change(title(), { target: { value: 'т'.repeat(256) } });

    await userEvent.click(save());

    expect(screen.getByText('Не более 255 символов.')).toBeInTheDocument();
    expect(patches(api)).toEqual([]);
  });

  it('a description of 100 001 characters: «Не более 100 000 символов.» under «Описание»', async () => {
    const { api, description, save } = await openForm();
    fireEvent.change(description(), { target: { value: 'о'.repeat(100_001) } });

    await userEvent.click(save());

    expect(screen.getByText('Не более 100 000 символов.')).toBeInTheDocument();
    expect(screen.queryByText('До 100 000 символов')).not.toBeInTheDocument();
    expect(description()).toHaveAttribute('aria-invalid', 'true');
    expect(patches(api)).toEqual([]);
  });

  it('the server’s refusal lands under the field with the server’s text, the input kept', async () => {
    const { description, save } = await openForm();
    server.use(
      http.patch(
        WORK_ITEM_URL,
        () =>
          HttpResponse.json(
            validationProblem({ description: 'workitems.workitem.description_too_long' }),
            { status: 400 },
          ),
        { once: true },
      ),
    );
    await userEvent.type(description(), 'Длинно');

    await userEvent.click(save());

    expect(await screen.findByText('Не более 100 000 символов.')).toBeInTheDocument();
    expect(description()).toHaveValue('Длинно');
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
});

describe('mod-003 US-3 Scenario: The description is plain text', () => {
  it('keeps «<b>важно</b>» literally, with «шаг 2» on the next line', async () => {
    const { api, description, save } = await openForm();
    const text = '<b>важно</b>\nшаг 2';
    fireEvent.change(description(), { target: { value: text } });

    await userEvent.click(save());
    await screen.findByText('Изменения сохранены.');

    expect(description()).toHaveValue(text);
    expect(document.querySelector('b')).toBeNull();
    expect(api.record(ids.msk, 4).description).toBe(text);
  });
});

describe('mod-003 US-3 Scenario: Two users save the same form one after the other', () => {
  it('B’s save carries only the description, so A’s title stays and no conflict is shown', async () => {
    const { api, description, title, save } = await openForm();
    // A saved the title meanwhile.
    api.record(ids.msk, 4).title = 'Первый';
    await userEvent.type(description(), 'Шаги');

    await userEvent.click(save());

    expect(await screen.findByText('Изменения сохранены.')).toBeInTheDocument();
    expect(patches(api)[0]?.body).toEqual({ description: 'Шаги' });
    expect(api.record(ids.msk, 4).title).toBe('Первый');
    expect(title()).toHaveValue('Первый');
    // The notice is the only alert: nothing speaks of a conflict.
    expect(screen.getAllByRole('alert').map((alert) => alert.textContent)).toEqual([
      'Изменения сохранены.',
    ]);
  });
});

describe('mod-003 spec § Page: a work item’s form — loading, error and a gone item', () => {
  it('shows «Загрузка данных» while the item is read', async () => {
    serveWorkItems();
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
    renderWorkItems(BUG_4);

    expect(await screen.findByRole('status', { name: 'Загрузка данных' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { level: 1 })).not.toBeInTheDocument();
    release();
    expect(await screen.findByRole('alert')).toHaveTextContent('Нет ответа.');
  });

  it('a failed read shows the banner under «Рабочий элемент», and «Повторить» reads again', async () => {
    serveWorkItems();
    server.use(
      http.get(
        WORK_ITEM_URL,
        () => HttpResponse.json({ status: 500, detail: 'Нет ответа.' }, { status: 500 }),
        { once: true },
      ),
    );
    renderWorkItems(BUG_4);

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Нет ответа.');
    expect(screen.getByRole('heading', { level: 1, name: 'Рабочий элемент' })).toBeInTheDocument();

    await userEvent.click(within(alert).getByRole('button', { name: 'Повторить' }));

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Баг MSK-BUG-4' }),
    ).toBeInTheDocument();
  });

  it('a not-found answer to a save replaces the page with its frame', async () => {
    const { api, title, save } = await openForm();
    const project = api.data.projects.find((candidate) => candidate.id === ids.msk);
    if (project) project.active = false;
    await userEvent.type(title(), '!');

    await userEvent.click(save());

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Проект не найден' }),
    ).toBeInTheDocument();
  });

  it('any other refusal is an error Alert above the band, the input kept', async () => {
    const { title, save } = await openForm();
    server.use(
      http.patch(
        WORK_ITEM_URL,
        () =>
          HttpResponse.json(
            { status: 503, title: 'unavailable', detail: 'Сервис временно недоступен.' },
            { status: 503 },
          ),
        { once: true },
      ),
    );
    await userEvent.type(title(), '!');

    await userEvent.click(save());

    expect(await screen.findByRole('alert')).toHaveTextContent('Сервис временно недоступен.');
    expect(title()).toHaveValue('Падение при входе!');
    expect(save()).toBeEnabled();
  });
});
