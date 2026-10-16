import { describe, expect, it, vi } from 'vitest';
import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { server } from '../../../test/mocks/server';
import { setViewportWidth } from '../../../test/viewport';
import { serveProblem, serveWorkItems } from '../testApi';
import {
  ITEM_OF_TYPE,
  choose,
  columnOf,
  controlsOf,
  list,
  openFieldsForm,
  optionsOf,
  patches,
  retype,
  save,
  section,
  sectionNames,
  textbox,
} from '../testFields';
import { WORK_ITEM_URL, hierarchyDataset, recordOf, validationProblem } from '../testFixtures';
import { mskForm, renderWorkItems } from '../testRender';

// Each user action draws the kit’s fields in jsdom; a test of several steps outlasts the default.
vi.setConfig({ testTimeout: 120_000 });

// feat-022 US-1, US-4 and US-5 on the form (spec § Screens and copy). The
// dataset is feat-001's: one item of every type in «Мастаск».

describe('feat-022 US-1 Scenario Outline: Each type shows its own sections', () => {
  it.each([
    ['Эпик', ['Описание'], ['Планирование', 'Классификация']],
    ['Фича', ['Описание'], ['Планирование', 'Классификация']],
    [
      'Пользовательская история',
      ['Описание', 'Критерии приёмки'],
      ['Планирование', 'Классификация'],
    ],
    ['Задача', ['Описание'], ['Планирование', 'Трудозатраты (часы)']],
    [
      'Баг',
      ['Описание', 'Шаги воспроизведения', 'Сведения о системе'],
      ['Планирование', 'Трудозатраты (часы)'],
    ],
  ] as const)(
    'a «%s»: the first column holds %j, the second %j, the third «Связанная работа» (feat-029 BR-C-14)',
    async (type, first, second) => {
      await openFieldsForm(ITEM_OF_TYPE[type]);

      expect(columnOf('Описание')).toEqual(first);
      expect(columnOf('Планирование')).toEqual(second);
      expect(columnOf('Связанная работа')).toEqual(['Связанная работа']);
      expect(sectionNames()).toEqual([...first, ...second, 'Связанная работа']);
      // The one tab the product has (BR-C-13).
      expect(screen.getAllByRole('tab').map((tab) => tab.textContent)).toEqual(['Детали']);
    },
  );
});

// The columns fold by the card’s own width, which jsdom does not lay out: the kit’s
// ColumnLayout test pins the container queries and E2E 27.3 measures them. What a window
// of any width keeps is the reading and Tab order of the three columns.
describe('feat-022 US-1 Scenario: The window’s width changes nothing in the order', () => {
  it('Tab moves from «Описание» through the bug’s texts into «Планирование», at 1279 px and at 640 px', async () => {
    setViewportWidth(640);
    await openFieldsForm(ITEM_OF_TYPE['Баг']);
    textbox('Описание').focus();

    await userEvent.tab();
    expect(textbox('Шаги воспроизведения')).toHaveFocus();
    await userEvent.tab();
    expect(textbox('Сведения о системе')).toHaveFocus();
    await userEvent.tab();
    expect(textbox('Оценка в пунктах')).toHaveFocus();
    await userEvent.tab();
    expect(list('Приоритет')).toHaveFocus();
  });
});

describe('feat-022 US-1 Scenario: The form stays usable at 640 px', () => {
  it('an «Эпик» at 640 px shows every section and field, each enabled', async () => {
    setViewportWidth(640);
    await openFieldsForm(ITEM_OF_TYPE['Эпик']);

    expect(sectionNames()).toEqual([
      'Описание',
      'Планирование',
      'Классификация',
      'Связанная работа',
    ]);
    for (const name of ['Приоритет', 'Риск', 'Вид ценности']) {
      expect(list(name)).not.toHaveAttribute('aria-disabled');
    }
    for (const name of ['Трудоёмкость', 'Бизнес-ценность', 'Критичность по времени']) {
      expect(textbox(name)).toBeEnabled();
    }
    expect(screen.getByLabelText('Дата начала')).toBeEnabled();
    expect(screen.getByLabelText('Целевая дата')).toBeEnabled();
  });
});

describe('feat-022 US-1 Scenario: The band, the strip and the other screens do not change', () => {
  it('a «Задача» keeps «Задача <ID>», the title, «Сохранить» and the strip of three', async () => {
    await openFieldsForm(ITEM_OF_TYPE['Задача']);

    expect(
      screen.getByRole('heading', { level: 1, name: 'Задача MSK-TASK-3' }),
    ).toBeInTheDocument();
    expect(textbox('Заголовок')).toHaveValue('Шаблон письма');
    expect(save()).toBeDisabled();
    for (const name of ['Исполнитель', 'Статус', 'Область']) {
      expect(list(name)).toBeInTheDocument();
    }
  });
});

describe('feat-022 US-1 Scenario: Several new fields are saved in one save', () => {
  it('a story’s «Оценка в пунктах» 5, «Риск» «2 — Средний» and «Критерии приёмки» go in one PATCH and show on reopening', async () => {
    const { api, view } = await openFieldsForm(ITEM_OF_TYPE['Пользовательская история']);

    await retype('Оценка в пунктах', '5');
    await choose('Риск', '2 — Средний');
    await userEvent.click(screen.getByRole('button', { name: 'Добавить критерий' }));
    fireEvent.change(textbox('GIVEN (предусловие)'), { target: { value: 'Дано' } });
    fireEvent.change(textbox('WHEN (действие)'), { target: { value: 'Вход без пароля' } });
    fireEvent.change(textbox('THEN (ожидаемый результат)'), { target: { value: 'Успех' } });
    await userEvent.click(save());

    expect(await screen.findByText('Изменения сохранены.')).toBeInTheDocument();
    expect(save()).toBeDisabled();
    expect(patches(api).map((request) => request.body)).toEqual([
      {
        storyPoints: { value: 5 },
        risk: { value: 2 },
        acceptanceCriteria: [
          {
            given: 'Дано',
            when: 'Вход без пароля',
            then: 'Успех',
          },
        ],
      },
    ]);

    view.unmount();
    renderWorkItems(mskForm('MSK-US-2'));
    expect(await screen.findByRole('textbox', { name: 'Оценка в пунктах' })).toHaveValue('5');
    expect(list('Риск')).toHaveTextContent('2 — Средний');
    expect(textbox('WHEN (действие)')).toHaveValue('Вход без пароля');
  });
});

describe('feat-022 US-1 Scenario: A save sends only the fields the user changed', () => {
  it('B’s save of «Вид работ» carries it alone, so A’s «Оставшаяся работа» 4 stays', async () => {
    const data = hierarchyDataset();
    const { api } = await openFieldsForm(ITEM_OF_TYPE['Задача'], data);
    // A saved «Оставшаяся работа» meanwhile.
    recordOf(data, 3).fields.remainingWork = 4;

    await choose('Вид работ', 'Тестирование');
    await userEvent.click(save());

    expect(await screen.findByText('Изменения сохранены.')).toBeInTheDocument();
    expect(patches(api)[0]?.body).toEqual({ activity: { value: 'Testing' } });
    expect(recordOf(data, 3).fields).toMatchObject({ remainingWork: 4, activity: 'Testing' });
    expect(textbox('Оставшаяся работа')).toHaveValue('4');
  });
});

describe('feat-022 US-1 Scenario: One refused value stores nothing', () => {
  it('the server’s «Допустимо значение от 0 до 9 999.» lands under «Бизнес-ценность», the inputs kept', async () => {
    const data = hierarchyDataset();
    const { api } = await openFieldsForm(ITEM_OF_TYPE['Фича'], data);
    serveProblem(
      'patch',
      WORK_ITEM_URL,
      validationProblem({ businessValue: 'workitems.workitem.number_out_of_range' }),
      { once: true },
    );

    await choose('Риск', '1 — Высокий');
    await retype('Бизнес-ценность', '40');
    await userEvent.click(save());

    expect(await screen.findByText('Допустимо значение от 0 до 9 999.')).toBeInTheDocument();
    expect(textbox('Бизнес-ценность')).toHaveAttribute('aria-invalid', 'true');
    expect(textbox('Бизнес-ценность')).toHaveValue('40');
    expect(list('Риск')).toHaveTextContent('1 — Высокий');
    expect(recordOf(data, 6).fields.risk).toBeNull();
    // The card's notice is absent: the only alert is the field's own error line.
    expect(screen.getAllByRole('alert').map((alert) => alert.textContent)).toEqual([
      'Допустимо значение от 0 до 9 999.',
    ]);
    expect(save()).toBeEnabled();
    expect(patches(api)).toEqual([]);
  });
});

describe('feat-022 US-1 Scenario: Nothing to save', () => {
  it('«2,50» in a stored 2,5, or a list changed and changed back, leaves «Сохранить» disabled', async () => {
    const data = hierarchyDataset();
    recordOf(data, 2).fields.storyPoints = 2.5;
    await openFieldsForm(ITEM_OF_TYPE['Пользовательская история'], data);
    expect(textbox('Оценка в пунктах')).toHaveValue('2,5');

    await retype('Оценка в пунктах', '2,50');
    expect(save()).toBeDisabled();

    await choose('Риск', '3 — Низкий');
    expect(save()).toBeEnabled();
    await choose('Риск', 'Не указан');
    expect(save()).toBeDisabled();
  });
});

describe('feat-022 US-1 Scenario: While the save runs', () => {
  it('every field is a value that cannot be edited and «Сохранить» is inert, focus kept, until the answer', async () => {
    await openFieldsForm(ITEM_OF_TYPE['Баг']);
    let release: () => void = () => {};
    const held = new Promise<void>((resolve) => {
      release = resolve;
    });
    server.use(
      http.patch(
        WORK_ITEM_URL,
        async () => {
          await held;
          return HttpResponse.json({ status: 503, detail: 'Нет ответа.' }, { status: 503 });
        },
        { once: true },
      ),
    );

    await retype('Оставшаяся работа', '3');
    await userEvent.click(save());

    // The kit’s busy: the read-only look and `aria-busy`, not `disabled` — the button
    // keeps its place in the Tab order (feat-029 design § 6, «Saving»).
    await waitFor(() => expect(save()).toHaveAttribute('aria-disabled', 'true'));
    expect(save()).toHaveAttribute('aria-busy', 'true');
    expect(save()).not.toBeDisabled();
    for (const name of [
      'Оценка в пунктах',
      'Исходная оценка',
      'Оставшаяся работа',
      'Шаги воспроизведения',
      'Заголовок',
    ]) {
      expect(textbox(name)).toHaveAttribute('readonly');
      expect(textbox(name)).toHaveAttribute('aria-busy', 'true');
    }
    for (const name of ['Приоритет', 'Серьёзность', 'Вид работ', 'Статус']) {
      expect(list(name)).toHaveAttribute('aria-busy', 'true');
    }
    release();
    expect(await screen.findByRole('alert')).toHaveTextContent('Нет ответа.');
    expect(textbox('Оставшаяся работа')).not.toHaveAttribute('readonly');
    expect(textbox('Оставшаяся работа')).toHaveValue('3');
  });
});

describe('feat-022 spec § States: loading', () => {
  it('shows one «Загрузка данных» while the item is read, a skeleton in each column', async () => {
    const data = hierarchyDataset();
    let release: () => void = () => {};
    const held = new Promise<void>((resolve) => {
      release = resolve;
    });
    await openFieldsForm(ITEM_OF_TYPE['Задача'], data).then(({ view }) => view.unmount());
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
    renderWorkItems(mskForm('MSK-TASK-3'));

    expect(await screen.findByRole('status', { name: 'Загрузка данных' })).toBeInTheDocument();
    expect(document.querySelectorAll('[aria-label="Загрузка данных"]')).toHaveLength(1);
    expect(screen.queryByRole('region')).not.toBeInTheDocument();
    release();
    expect(await screen.findByRole('alert')).toHaveTextContent('Нет ответа.');
  });
});

describe('feat-022 US-4 Scenario Outline: Each text field appears on its own type only', () => {
  it.each([
    ['Эпик', []],
    ['Фича', []],
    ['Пользовательская история', []],
    ['Задача', []],
    ['Баг', ['Шаги воспроизведения', 'Сведения о системе']],
  ] as const)('a «%s» shows %j', async (type, shown) => {
    await openFieldsForm(ITEM_OF_TYPE[type]);

    for (const name of ['Шаги воспроизведения', 'Сведения о системе']) {
      if ((shown as readonly string[]).includes(name)) {
        expect(textbox(name)).toBeInTheDocument();
      } else {
        expect(screen.queryByRole('textbox', { name })).not.toBeInTheDocument();
        expect(screen.queryByRole('region', { name })).not.toBeInTheDocument();
      }
    }
  });
});

describe('feat-022 US-4 Scenario: The section sits under «Описание»', () => {
  it('a «Баг» has «Шаги воспроизведения», then «Сведения о системе», each with its placeholder and helper', async () => {
    await openFieldsForm(ITEM_OF_TYPE['Баг']);

    expect(columnOf('Описание')).toEqual([
      'Описание',
      'Шаги воспроизведения',
      'Сведения о системе',
    ]);
    expect(
      within(section('Шаги воспроизведения')).getByRole('heading', { level: 2 }),
    ).toHaveTextContent('Шаги воспроизведения');
    expect(textbox('Шаги воспроизведения')).toHaveAttribute(
      'placeholder',
      'Опишите шаги, ожидаемый и фактический результат',
    );
    expect(textbox('Шаги воспроизведения')).toHaveAccessibleDescription('До 100 000 символов');
    expect(textbox('Сведения о системе')).toHaveAttribute(
      'placeholder',
      'Опишите окружение: ОС, браузер, версию приложения',
    );
    expect(textbox('Сведения о системе')).toHaveAccessibleDescription('До 100 000 символов');
  });

  it('a «Пользовательская история» has «Критерии приёмки» there, with its button', async () => {
    await openFieldsForm(ITEM_OF_TYPE['Пользовательская история']);

    expect(columnOf('Описание')).toEqual(['Описание', 'Критерии приёмки']);
    expect(screen.getByRole('button', { name: 'Добавить критерий' })).toBeInTheDocument();
  });
});

describe('feat-022 US-4 Scenario: The field starts empty and is saved with the other fields', () => {
  it('a new bug’s two texts are empty; filled in with «Приоритет» 1 they go in one save', async () => {
    const { api } = await openFieldsForm(ITEM_OF_TYPE['Баг']);
    expect(textbox('Шаги воспроизведения')).toHaveValue('');
    expect(textbox('Сведения о системе')).toHaveValue('');

    await userEvent.type(textbox('Шаги воспроизведения'), 'Открыть ссылку');
    await userEvent.type(textbox('Сведения о системе'), 'Chrome 140');
    await choose('Приоритет', '1');
    await userEvent.click(save());

    expect(await screen.findByText('Изменения сохранены.')).toBeInTheDocument();
    expect(patches(api).map((request) => request.body)).toEqual([
      { priority: { value: 1 }, reproSteps: 'Открыть ссылку', systemInfo: 'Chrome 140' },
    ]);
  });
});

describe('feat-022 US-4 Scenario: The text is plain', () => {
  it('keeps «<b>важно</b>» literally, with «шаг 2» on the next line', async () => {
    const data = hierarchyDataset();
    await openFieldsForm(ITEM_OF_TYPE['Пользовательская история'], data);
    const text = '<b>важно</b>\nшаг 2';
    await userEvent.click(screen.getByRole('button', { name: 'Добавить критерий' }));
    fireEvent.change(textbox('GIVEN (предусловие)'), { target: { value: text } });

    await userEvent.click(save());
    await screen.findByText('Изменения сохранены.');

    expect(textbox('GIVEN (предусловие)')).toHaveValue(text);
    expect(document.querySelector('b')).toBeNull();
    expect(recordOf(data, 2).fields.acceptanceCriteria?.[0]?.given).toBe(text);
  });
});

describe('feat-022 US-4 Scenario: The length limit holds in the browser and on the server', () => {
  it('100 001 characters in «Сведения о системе»: «Не более 100 000 символов.» under it, nothing sent', async () => {
    const { api } = await openFieldsForm(ITEM_OF_TYPE['Баг']);
    fireEvent.change(textbox('Сведения о системе'), { target: { value: 'о'.repeat(100_001) } });

    await userEvent.click(save());

    expect(textbox('Сведения о системе')).toHaveAttribute('aria-invalid', 'true');
    expect(textbox('Сведения о системе')).toHaveAccessibleDescription('Не более 100 000 символов.');
    expect(patches(api)).toEqual([]);
  });

  it('the server’s `text_too_long` lands under «Шаги воспроизведения»', async () => {
    await openFieldsForm(ITEM_OF_TYPE['Баг']);
    serveProblem(
      'patch',
      WORK_ITEM_URL,
      validationProblem({ reproSteps: 'workitems.workitem.text_too_long' }),
      { once: true },
    );
    await userEvent.type(textbox('Шаги воспроизведения'), 'Шаги');

    await userEvent.click(save());

    await waitFor(() =>
      expect(textbox('Шаги воспроизведения')).toHaveAccessibleDescription(
        'Не более 100 000 символов.',
      ),
    );
    expect(textbox('Шаги воспроизведения')).toHaveValue('Шаги');
  });
});

describe('feat-022 US-5 Scenario: Every type has Приоритет 1..4', () => {
  it.each(Object.entries(ITEM_OF_TYPE))(
    'a «%s» shows «Приоритет» in «Планирование», «1»..«4», no empty choice, «1 — самый высокий»',
    async (_type, identifier) => {
      await openFieldsForm(identifier);

      expect(controlsOf('Планирование')).toContain('Приоритет');
      expect(list('Приоритет')).toHaveTextContent('2');
      expect(list('Приоритет')).toHaveAccessibleDescription('1 — самый высокий');
      expect(await optionsOf('Приоритет')).toEqual(['1', '2', '3', '4']);
    },
  );

  it('choosing «1» and saving shows «1» on reopening', async () => {
    const { api, view } = await openFieldsForm(ITEM_OF_TYPE['Задача']);

    await choose('Приоритет', '1');
    await userEvent.click(save());
    await screen.findByText('Изменения сохранены.');

    expect(patches(api)[0]?.body).toEqual({ priority: { value: 1 } });
    view.unmount();
    renderWorkItems(mskForm('MSK-TASK-3'));
    expect(await screen.findByRole('combobox', { name: 'Приоритет' })).toHaveTextContent('1');
  });
});

describe('feat-022 US-5 Scenario Outline: A new item takes its type’s defaults', () => {
  it.each([
    ['Эпик', { Приоритет: '2', 'Вид ценности': 'Бизнес', Риск: 'Не указан' }],
    ['Фича', { Приоритет: '2', 'Вид ценности': 'Бизнес', Риск: 'Не указан' }],
    ['Пользовательская история', { Приоритет: '2', 'Вид ценности': 'Бизнес', Риск: 'Не указан' }],
    ['Задача', { Приоритет: '2', 'Вид работ': 'Не указан' }],
    ['Баг', { Приоритет: '2', Серьёзность: '3 — Средняя', 'Вид работ': 'Не указан' }],
  ] as const)('a «%s» shows %j and every other field empty', async (type, lists) => {
    await openFieldsForm(ITEM_OF_TYPE[type]);

    for (const [name, value] of Object.entries(lists)) {
      expect(list(name)).toHaveTextContent(value);
    }
    for (const input of screen.getAllByRole('textbox')) {
      if (input !== textbox('Заголовок') && !input.closest('[role="combobox"]')) {
        expect(input).toHaveValue('');
      }
    }
    for (const name of ['Дата начала', 'Целевая дата']) {
      const date = screen.queryByLabelText(name);
      if (date) expect(date).toHaveValue('');
    }
  });

  it('an empty estimate shows «Не оценено», an empty relative number «Не задано»', async () => {
    await openFieldsForm(ITEM_OF_TYPE['Эпик']);

    expect(textbox('Трудоёмкость')).toHaveAttribute('placeholder', 'Не оценено');
    expect(textbox('Бизнес-ценность')).toHaveAttribute('placeholder', 'Не задано');
    expect(textbox('Бизнес-ценность')).toHaveAccessibleDescription('Целое число от 0 до 9 999');
    expect(textbox('Критичность по времени')).toHaveAttribute('placeholder', 'Не задано');
    expect(textbox('Трудоёмкость')).toHaveAccessibleDescription('От 0 до 9 999');
  });
});

describe('feat-022 US-5 Scenario Outline: A field of another type is refused', () => {
  it('`field_not_on_type` is the error Alert above the band, no field marked', async () => {
    await openFieldsForm(ITEM_OF_TYPE['Задача']);
    serveProblem(
      'patch',
      WORK_ITEM_URL,
      validationProblem({ storyPoints: 'workitems.workitem.field_not_on_type' }),
      { once: true },
    );

    await choose('Приоритет', '3');
    await userEvent.click(save());

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Это поле недоступно для рабочего элемента этого типа.',
    );
    expect(list('Приоритет')).toHaveTextContent('3');
    expect(document.querySelector('[aria-invalid="true"]')).toBeNull();
  });
});

describe('feat-022 api-contract § 0: the type is recognised by its appearance, never its name', () => {
  it('an item of appearance `bug` under another name and key still shows a bug’s sections', async () => {
    const data = hierarchyDataset();
    const api = serveWorkItems(data);
    const answer = api.answer(recordOf(data, 4));
    server.use(
      http.get(
        WORK_ITEM_URL,
        () =>
          HttpResponse.json({ ...answer, type: { ...answer.type, name: 'Дефект', key: 'DEF' } }),
        { once: true },
      ),
    );
    renderWorkItems(mskForm('MSK-BUG-4'));

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Дефект MSK-BUG-4' }),
    ).toBeInTheDocument();
    expect(columnOf('Описание')).toEqual([
      'Описание',
      'Шаги воспроизведения',
      'Сведения о системе',
    ]);
    expect(controlsOf('Планирование')).toEqual([
      'Оценка в пунктах',
      'Приоритет',
      'Серьёзность',
      'Вид работ',
    ]);
  });
});
