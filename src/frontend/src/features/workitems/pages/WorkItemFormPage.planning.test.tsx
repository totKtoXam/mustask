import { describe, expect, it, vi } from 'vitest';
import { fireEvent, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { serveProblem } from '../testApi';
import {
  ITEM_OF_TYPE,
  choose,
  controlsOf,
  dateField,
  list,
  openFieldsForm,
  optionsOf,
  patches,
  retype,
  save,
  section,
  textbox,
} from '../testFields';
import { WORK_ITEM_URL, hierarchyDataset, recordOf, validationProblem } from '../testFixtures';
import { mskForm, renderWorkItems } from '../testRender';

// Each user action draws the kit’s fields in jsdom; a test of several steps outlasts the default.
vi.setConfig({ testTimeout: 120_000 });

// feat-022 US-2 and US-3 on the form (spec § Controls and copy, § Saving):
// planning, classification and estimation. The dataset is feat-001's: one
// item of every type in «Мастаск».

/** Sets a date field as the browser's date field reports a picked date. */
function setDate(name: string, value: string) {
  fireEvent.change(dateField(name), { target: { value } });
}

describe('feat-022 US-2 Scenario: An epic’s planning and classification are saved', () => {
  it('sends the seven values in one PATCH and shows them on reopening, «Трудоёмкость» as «13,5»', async () => {
    const { api, view } = await openFieldsForm(ITEM_OF_TYPE['Эпик']);

    await choose('Риск', '1 — Высокий');
    await retype('Трудоёмкость', '13,5');
    await retype('Бизнес-ценность', '40');
    await retype('Критичность по времени', '8');
    setDate('Дата начала', '2026-11-01');
    setDate('Целевая дата', '2027-03-31');
    await choose('Вид ценности', 'Архитектура');
    await userEvent.click(save());

    expect(await screen.findByText('Изменения сохранены.')).toBeInTheDocument();
    expect(patches(api).map((request) => request.body)).toEqual([
      {
        risk: { value: 1 },
        effort: { value: 13.5 },
        businessValue: { value: 40 },
        timeCriticality: { value: 8 },
        startDate: { value: '2026-11-01' },
        targetDate: { value: '2027-03-31' },
        valueArea: { value: 'Architectural' },
      },
    ]);

    view.unmount();
    renderWorkItems(mskForm('MSK-EPIC-1'));
    expect(await screen.findByRole('textbox', { name: 'Трудоёмкость' })).toHaveValue('13,5');
    expect(list('Риск')).toHaveTextContent('1 — Высокий');
    expect(textbox('Бизнес-ценность')).toHaveValue('40');
    expect(textbox('Критичность по времени')).toHaveValue('8');
    expect(dateField('Дата начала')).toHaveValue('2026-11-01');
    expect(dateField('Целевая дата')).toHaveValue('2027-03-31');
    expect(list('Вид ценности')).toHaveTextContent('Архитектура');
  });

  it('a «Фича» offers the same fields in the same order', async () => {
    await openFieldsForm(ITEM_OF_TYPE['Фича']);

    expect(controlsOf('Планирование')).toEqual([
      'Приоритет',
      'Риск',
      'Трудоёмкость',
      'Бизнес-ценность',
      'Критичность по времени',
      'Дата начала',
      'Целевая дата',
    ]);
    expect(controlsOf('Классификация')).toEqual(['Вид ценности']);
    expect(dateField('Целевая дата')).toHaveAccessibleDescription('Не раньше даты начала');
    expect(dateField('Дата начала')).toHaveAttribute('min', '2000-01-01');
    expect(dateField('Дата начала')).toHaveAttribute('max', '2099-12-31');
  });
});

describe('feat-022 US-2 Scenario: A story’s planning and classification are saved', () => {
  it('sends «Оценка в пунктах» 3 and «Риск» «3 — Низкий»; the story shows no epic field', async () => {
    const { api } = await openFieldsForm(ITEM_OF_TYPE['Пользовательская история']);

    expect(controlsOf('Планирование')).toEqual(['Оценка в пунктах', 'Приоритет', 'Риск']);
    for (const name of ['Трудоёмкость', 'Бизнес-ценность', 'Критичность по времени']) {
      expect(screen.queryByRole('textbox', { name })).not.toBeInTheDocument();
    }
    expect(screen.queryByLabelText('Дата начала')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Целевая дата')).not.toBeInTheDocument();

    await retype('Оценка в пунктах', '3');
    await choose('Риск', '3 — Низкий');
    await choose('Вид ценности', 'Бизнес');
    await userEvent.click(save());

    expect(await screen.findByText('Изменения сохранены.')).toBeInTheDocument();
    // «Бизнес» picked again is no change.
    expect(patches(api)[0]?.body).toEqual({ storyPoints: { value: 3 }, risk: { value: 3 } });
    expect(textbox('Оценка в пунктах')).toHaveValue('3');
    expect(list('Риск')).toHaveTextContent('3 — Низкий');
  });
});

describe('feat-022 US-2 Scenario: Risk can be emptied, the value area cannot', () => {
  it('«Риск» offers «Не указан» first, and choosing it sends `{ value: null }`; «Вид ценности» offers two', async () => {
    const data = hierarchyDataset();
    recordOf(data, 6).fields.risk = 2;
    const { api } = await openFieldsForm(ITEM_OF_TYPE['Фича'], data);

    expect(await optionsOf('Риск')).toEqual([
      'Не указан',
      '1 — Высокий',
      '2 — Средний',
      '3 — Низкий',
    ]);
    expect(await optionsOf('Вид ценности')).toEqual(['Бизнес', 'Архитектура']);

    await choose('Риск', 'Не указан');
    await userEvent.click(save());

    expect(await screen.findByText('Изменения сохранены.')).toBeInTheDocument();
    expect(patches(api)[0]?.body).toEqual({ risk: { value: null } });
    expect(recordOf(data, 6).fields.risk).toBeNull();
    expect(list('Риск')).toHaveTextContent('Не указан');
  });
});

describe('feat-022 US-2 Scenario: A number field may be emptied, and 0 is kept as 0', () => {
  it('a cleared «Трудоёмкость» is saved empty with «Не оценено»; then «0» is saved and shown as «0»', async () => {
    const data = hierarchyDataset();
    recordOf(data, 1).fields.effort = 5;
    const { api } = await openFieldsForm(ITEM_OF_TYPE['Эпик'], data);
    expect(textbox('Трудоёмкость')).toHaveValue('5');

    await retype('Трудоёмкость', '');
    await userEvent.click(save());
    expect(await screen.findByText('Изменения сохранены.')).toBeInTheDocument();
    expect(textbox('Трудоёмкость')).toHaveValue('');
    expect(textbox('Трудоёмкость')).toHaveAttribute('placeholder', 'Не оценено');

    await retype('Трудоёмкость', '0');
    await userEvent.click(save());
    await screen.findByText('Изменения сохранены.');
    await screen.findByRole('textbox', { name: 'Трудоёмкость' });

    expect(patches(api).map((request) => request.body)).toEqual([
      { effort: { value: null } },
      { effort: { value: 0 } },
    ]);
    expect(textbox('Трудоёмкость')).toHaveValue('0');
    expect(recordOf(data, 1).fields.effort).toBe(0);
  });
});

describe('feat-022 US-2 Scenario Outline: An invalid number is refused, not rounded', () => {
  it.each([
    ['Трудоёмкость', '-1', 'Допустимо значение от 0 до 9 999.'],
    ['Критичность по времени', '10000', 'Допустимо значение от 0 до 9 999.'],
    ['Трудоёмкость', '2,555', 'Не более двух знаков после запятой.'],
    ['Бизнес-ценность', '2,5', 'Введите целое число.'],
  ])('«%s» = «%s»: «%s» under it and nothing is sent', async (name, text, message) => {
    const { api } = await openFieldsForm(ITEM_OF_TYPE['Эпик']);

    await retype(name, text);
    await userEvent.click(save());

    expect(textbox(name)).toHaveAttribute('aria-invalid', 'true');
    expect(textbox(name)).toHaveAccessibleDescription(message);
    expect(textbox(name)).toHaveValue(text);
    expect(save()).toBeEnabled();
    expect(patches(api)).toEqual([]);
  });

  it('the server’s reason with the same text lands under the field', async () => {
    await openFieldsForm(ITEM_OF_TYPE['Эпик']);
    serveProblem(
      'patch',
      WORK_ITEM_URL,
      validationProblem({ effort: 'workitems.workitem.number_too_precise' }),
      { once: true },
    );

    await retype('Трудоёмкость', '2,5');
    await userEvent.click(save());

    expect(await screen.findByText('Не более двух знаков после запятой.')).toBeInTheDocument();
    expect(textbox('Трудоёмкость')).toHaveAccessibleDescription(
      'Не более двух знаков после запятой.',
    );
  });
});

describe('feat-022 US-2 Scenario: Text that is not a number never leaves the browser', () => {
  it('«пять» in «Трудоёмкость» enables «Сохранить», says «Введите число.», and the message clears on the next edit', async () => {
    const { api } = await openFieldsForm(ITEM_OF_TYPE['Эпик']);

    await retype('Трудоёмкость', 'пять');
    expect(save()).toBeEnabled();
    await userEvent.click(save());

    expect(textbox('Трудоёмкость')).toHaveAccessibleDescription('Введите число.');
    expect(patches(api)).toEqual([]);

    await userEvent.type(textbox('Трудоёмкость'), '!');
    expect(textbox('Трудоёмкость')).toHaveAccessibleDescription('От 0 до 9 999');
    expect(textbox('Трудоёмкость')).not.toHaveAttribute('aria-invalid', 'true');
  });
});

describe('feat-022 US-2 Scenario: The target date may not precede the start date', () => {
  it('a «Дата начала» after the stored «Целевая дата» is refused under «Целевая дата»; both in one save are accepted', async () => {
    const data = hierarchyDataset();
    recordOf(data, 1).fields.targetDate = '2027-03-31';
    const { api } = await openFieldsForm(ITEM_OF_TYPE['Эпик'], data);

    setDate('Дата начала', '2027-04-01');
    await userEvent.click(save());

    expect(dateField('Целевая дата')).toHaveAccessibleDescription(
      'Целевая дата не может быть раньше даты начала.',
    );
    expect(dateField('Целевая дата')).toHaveAttribute('aria-invalid', 'true');
    expect(patches(api)).toEqual([]);

    setDate('Целевая дата', '2027-06-30');
    expect(dateField('Целевая дата')).toHaveAccessibleDescription('Не раньше даты начала');
    await userEvent.click(save());

    expect(await screen.findByText('Изменения сохранены.')).toBeInTheDocument();
    expect(patches(api)[0]?.body).toEqual({
      startDate: { value: '2027-04-01' },
      targetDate: { value: '2027-06-30' },
    });
  });

  it('either date alone may be cleared', async () => {
    const data = hierarchyDataset();
    Object.assign(recordOf(data, 1).fields, { startDate: '2026-11-01', targetDate: '2027-03-31' });
    const { api } = await openFieldsForm(ITEM_OF_TYPE['Эпик'], data);

    setDate('Дата начала', '');
    await userEvent.click(save());

    expect(await screen.findByText('Изменения сохранены.')).toBeInTheDocument();
    expect(patches(api)[0]?.body).toEqual({ startDate: { value: null } });
    expect(dateField('Целевая дата')).toHaveValue('2027-03-31');
  });

  it('the server’s `target_before_start` lands under «Целевая дата»', async () => {
    await openFieldsForm(ITEM_OF_TYPE['Фича']);
    serveProblem(
      'patch',
      WORK_ITEM_URL,
      validationProblem({ targetDate: 'workitems.workitem.target_before_start' }),
      { once: true },
    );

    setDate('Дата начала', '2026-11-01');
    await userEvent.click(save());

    expect(
      await screen.findByText('Целевая дата не может быть раньше даты начала.'),
    ).toBeInTheDocument();
    expect(dateField('Целевая дата')).toHaveAttribute('aria-invalid', 'true');
  });
});

describe('feat-022 spec § Controls and copy: a date typed in part, a date outside 2000–2099', () => {
  it('a date typed in part: «Введите дату полностью.», nothing sent', async () => {
    const { api } = await openFieldsForm(ITEM_OF_TYPE['Эпик']);
    const start = dateField('Дата начала');
    // The browser's own field holds no value but a bad input, and may fire no change.
    Object.defineProperty(start, 'validity', { configurable: true, value: { badInput: true } });

    fireEvent.keyUp(start, { key: '1' });
    expect(save()).toBeEnabled();
    await userEvent.click(save());

    expect(start).toHaveAccessibleDescription('Введите дату полностью.');
    expect(patches(api)).toEqual([]);
  });

  it('31.12.1999 typed in full: «Укажите дату с 2000 по 2099 год.», nothing sent', async () => {
    const { api } = await openFieldsForm(ITEM_OF_TYPE['Эпик']);

    setDate('Дата начала', '1999-12-31');
    await userEvent.click(save());

    expect(dateField('Дата начала')).toHaveAccessibleDescription(
      'Укажите дату с 2000 по 2099 год.',
    );
    expect(patches(api)).toEqual([]);
  });
});

describe('feat-022 US-3 Scenario: A task’s estimates are saved in hours', () => {
  it('«Разработка», «8», «5,5», «2.5» are sent as numbers and shown as «8», «5,5», «2,5»', async () => {
    const { api } = await openFieldsForm(ITEM_OF_TYPE['Задача']);
    expect(section('Трудозатраты (часы)')).toBeInTheDocument();
    expect(screen.queryByRole('textbox', { name: 'Оценка в пунктах' })).not.toBeInTheDocument();
    expect(screen.queryByRole('combobox', { name: 'Серьёзность' })).not.toBeInTheDocument();

    await choose('Вид работ', 'Разработка');
    await retype('Исходная оценка', '8');
    await retype('Оставшаяся работа', '5,5');
    await retype('Выполненная работа', '2.5');
    await userEvent.click(save());

    expect(await screen.findByText('Изменения сохранены.')).toBeInTheDocument();
    expect(patches(api)[0]?.body).toEqual({
      activity: { value: 'Development' },
      originalEstimate: { value: 8 },
      remainingWork: { value: 5.5 },
      completedWork: { value: 2.5 },
    });
    expect(list('Вид работ')).toHaveTextContent('Разработка');
    expect(textbox('Исходная оценка')).toHaveValue('8');
    expect(textbox('Оставшаяся работа')).toHaveValue('5,5');
    expect(textbox('Выполненная работа')).toHaveValue('2,5');
    expect(save()).toBeDisabled();
  });
});

describe('feat-022 US-3 Scenario: A bug carries points, severity, activity and hours', () => {
  it('lays them out in order, and saves «Оценка в пунктах» 1, «1 — Критическая», «Оставшаяся работа» 3', async () => {
    const data = hierarchyDataset();
    const { api } = await openFieldsForm(ITEM_OF_TYPE['Баг'], data);
    expect(controlsOf('Планирование')).toEqual([
      'Оценка в пунктах',
      'Приоритет',
      'Серьёзность',
      'Вид работ',
    ]);
    expect(controlsOf('Трудозатраты (часы)')).toEqual([
      'Исходная оценка',
      'Оставшаяся работа',
      'Выполненная работа',
    ]);

    await retype('Оценка в пунктах', '1');
    await choose('Серьёзность', '1 — Критическая');
    await retype('Оставшаяся работа', '3');
    await userEvent.click(save());

    expect(await screen.findByText('Изменения сохранены.')).toBeInTheDocument();
    expect(patches(api)[0]?.body).toEqual({
      storyPoints: { value: 1 },
      severity: { value: 1 },
      remainingWork: { value: 3 },
    });
    expect(recordOf(data, 4).fields).toMatchObject({
      storyPoints: 1,
      severity: 1,
      remainingWork: 3,
    });
  });
});

describe('feat-022 US-3 Scenario: The lists offer their values in order', () => {
  it('«Серьёзность» has no empty choice; «Вид работ» starts with «Не указан»', async () => {
    await openFieldsForm(ITEM_OF_TYPE['Баг']);

    expect(await optionsOf('Серьёзность')).toEqual([
      '1 — Критическая',
      '2 — Высокая',
      '3 — Средняя',
      '4 — Низкая',
    ]);
    expect(await optionsOf('Вид работ')).toEqual([
      'Не указан',
      'Развёртывание',
      'Проектирование',
      'Разработка',
      'Документирование',
      'Требования',
      'Тестирование',
    ]);
  });
});

describe('feat-022 US-3 Scenario: Hours are refused beyond two decimals, never rounded', () => {
  it('«1,333» in «Исходная оценка»: «Не более двух знаков после запятой.», nothing sent', async () => {
    const { api } = await openFieldsForm(ITEM_OF_TYPE['Задача']);

    await retype('Исходная оценка', '1,333');
    await userEvent.click(save());

    expect(textbox('Исходная оценка')).toHaveAccessibleDescription(
      'Не более двух знаков после запятой.',
    );
    expect(textbox('Исходная оценка')).toHaveValue('1,333');
    expect(patches(api)).toEqual([]);
  });
});

describe('feat-022 US-3 Scenario: An empty estimate is not zero', () => {
  it('a new task’s «Оставшаяся работа» is empty with «Не оценено»; «0» is kept; cleared again it is empty', async () => {
    const data = hierarchyDataset();
    const { api } = await openFieldsForm(ITEM_OF_TYPE['Задача'], data);
    expect(textbox('Оставшаяся работа')).toHaveValue('');
    expect(textbox('Оставшаяся работа')).toHaveAttribute('placeholder', 'Не оценено');

    await retype('Оставшаяся работа', '0');
    await userEvent.click(save());
    await screen.findByText('Изменения сохранены.');
    expect(textbox('Оставшаяся работа')).toHaveValue('0');
    expect(recordOf(data, 3).fields.remainingWork).toBe(0);

    await retype('Оставшаяся работа', '');
    await userEvent.click(save());
    await screen.findByText('Изменения сохранены.');

    expect(patches(api).map((request) => request.body)).toEqual([
      { remainingWork: { value: 0 } },
      { remainingWork: { value: null } },
    ]);
    expect(textbox('Оставшаяся работа')).toHaveValue('');
    expect(recordOf(data, 3).fields.remainingWork).toBeNull();
  });
});

describe('feat-022 US-3 Scenario: Closing a task leaves its remaining work as entered', () => {
  it('a state change sends the state alone; «Оставшаяся работа» still reads 3', async () => {
    const data = hierarchyDataset();
    recordOf(data, 3).fields.remainingWork = 3;
    const { api } = await openFieldsForm(ITEM_OF_TYPE['Задача'], data);

    await choose('Статус', 'Закрыта');
    await userEvent.click(save());

    expect(await screen.findByText('Изменения сохранены.')).toBeInTheDocument();
    expect(Object.keys(patches(api)[0]?.body as object)).toEqual(['stateId']);
    expect(textbox('Оставшаяся работа')).toHaveValue('3');
  });
});
