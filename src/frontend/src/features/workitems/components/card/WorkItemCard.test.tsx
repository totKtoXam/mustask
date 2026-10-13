import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { formatDateTime } from '../../../../shared/lib/format';
import { cardTexts } from '../../lib/cardTexts';
import { ids, recordOf } from '../../testFixtures';
import type { CardFrameProps } from './cardFrame';
import { PageFrame } from './KitFrames';
import { ITEM_OF_TYPE, openCard } from './testCard';

// Each user action draws the kit’s fields in jsdom; a test of several steps outlasts the default.
vi.setConfig({ testTimeout: 60_000 });

const region = (name: string) => screen.getByRole('region', { name });

/** What each type's card holds: its sections and, in each, the controls by role and name. */
const SECTIONS: Record<
  keyof typeof ITEM_OF_TYPE,
  Record<string, { textbox?: string[]; combobox?: string[]; date?: string[] }>
> = {
  Эпик: {
    Описание: { textbox: ['Описание'] },
    Планирование: {
      textbox: ['Трудоёмкость', 'Бизнес-ценность', 'Критичность по времени'],
      combobox: ['Приоритет', 'Риск'],
      date: ['Дата начала', 'Целевая дата'],
    },
    Классификация: { combobox: ['Вид ценности'] },
  },
  Фича: {
    Описание: { textbox: ['Описание'] },
    Планирование: {
      textbox: ['Трудоёмкость', 'Бизнес-ценность', 'Критичность по времени'],
      combobox: ['Приоритет', 'Риск'],
      date: ['Дата начала', 'Целевая дата'],
    },
    Классификация: { combobox: ['Вид ценности'] },
  },
  'Пользовательская история': {
    Описание: { textbox: ['Описание'] },
    'Критерии приёмки': { textbox: ['Критерии приёмки'] },
    Планирование: { textbox: ['Оценка в пунктах'], combobox: ['Приоритет', 'Риск'] },
    Классификация: { combobox: ['Вид ценности'] },
  },
  Задача: {
    Описание: { textbox: ['Описание'] },
    Планирование: { combobox: ['Приоритет', 'Вид работ'] },
    'Трудозатраты (часы)': {
      textbox: ['Исходная оценка', 'Оставшаяся работа', 'Выполненная работа'],
    },
  },
  Баг: {
    Описание: { textbox: ['Описание'] },
    'Шаги воспроизведения': { textbox: ['Шаги воспроизведения'] },
    'Сведения о системе': { textbox: ['Сведения о системе'] },
    Планирование: {
      textbox: ['Оценка в пунктах'],
      combobox: ['Приоритет', 'Серьёзность', 'Вид работ'],
    },
    'Трудозатраты (часы)': {
      textbox: ['Исходная оценка', 'Оставшаяся работа', 'Выполненная работа'],
    },
  },
};

const ALL_SECTIONS = [
  'Описание',
  'Критерии приёмки',
  'Шаги воспроизведения',
  'Сведения о системе',
  'Планирование',
  'Трудозатраты (часы)',
  'Классификация',
  'Связанная работа',
];

describe('feat-029 US-2: the header and the band keep their names (BR-C-20)', () => {
  it('names every control the form had, with the item’s values', async () => {
    const { api } = await openCard('MSK-US-2', {
      prepare: (data) => {
        const record = recordOf(data.data, 2);
        record.assigneeId = ids.anna;
        record.areaId = ids.api;
      },
    });

    expect(screen.getByRole('textbox', { name: 'Заголовок' })).toHaveValue('Письмо со ссылкой');
    expect(screen.getByRole('textbox', { name: 'Заголовок' })).toHaveAttribute(
      'aria-required',
      'true',
    );
    expect(screen.getByRole('combobox', { name: 'Исполнитель' })).toHaveValue('Анна Смирнова');
    expect(screen.getByRole('combobox', { name: 'Статус' })).toHaveTextContent('Новая');
    expect(screen.getByRole('combobox', { name: 'Область' })).toHaveValue('Платформа / API');
    expect(screen.getByRole('group', { name: 'Сохранение' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Сохранить' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Ещё варианты сохранения' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Отменить все изменения' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Обновить' })).toBeEnabled();
    expect(screen.getByRole('button', { name: 'Ещё действия' })).toBeEnabled();
    expect(api.writes()).toHaveLength(0);
  });

  it('reads «Не назначен» for nobody', async () => {
    await openCard('MSK-BUG-4');
    expect(screen.getByRole('combobox', { name: 'Исполнитель' })).toHaveValue('');
    expect(screen.getByText('Не назначен')).toBeInTheDocument();
  });

  it('reads «Неизвестный пользователь» for an assignee the directory lacks', async () => {
    await openCard('MSK-BUG-4', {
      prepare: (api) => {
        recordOf(api.data, 4).assigneeId = ids.ghost;
      },
    });
    expect(screen.getByRole('combobox', { name: 'Исполнитель' })).toHaveValue(
      'Неизвестный пользователь',
    );
  });

  it('lists the type’s states without «Удалена», in the process’s order, and the item’s own is chosen', async () => {
    await openCard('MSK-US-2');
    await userEvent.click(screen.getByRole('combobox', { name: 'Статус' }));

    const options = within(await screen.findByRole('listbox'))
      .getAllByRole('option')
      .map((option) => option.textContent);
    expect(options).toEqual(['Новая', 'Активна', 'Разрешена', 'Закрыта']);
  });

  it('shows the time of the latest change', async () => {
    const t = await openCard('MSK-US-2');
    const changedAt = recordOf(t.api.data, 2).changedAt;
    expect(
      screen.getByText(`Изменён: ${formatDateTime(changedAt)}`, { exact: false }),
    ).toBeInTheDocument();
  });

  it('shows «Изменён: —» when the time of the latest change cannot be read', async () => {
    await openCard('MSK-US-2', {
      prepare: (data) => {
        recordOf(data.data, 2).changedAt = 'не дата';
      },
    });
    expect(screen.getByText('Изменён: —')).toBeInTheDocument();
  });
});

describe('feat-029 US-3: each type has its sections', () => {
  it.each(Object.entries(ITEM_OF_TYPE))('%s: %s', async (type, identifier) => {
    await openCard(identifier);
    const expected = SECTIONS[type as keyof typeof ITEM_OF_TYPE];
    const names = [...Object.keys(expected), cardTexts.relatedWork];

    for (const name of ALL_SECTIONS) {
      if (names.includes(name)) {
        expect(region(name)).toBeInTheDocument();
      } else {
        expect(screen.queryByRole('region', { name })).not.toBeInTheDocument();
      }
    }
    for (const [section, controls] of Object.entries(expected)) {
      const inside = within(region(section));
      for (const name of controls.textbox ?? []) {
        expect(inside.getByRole('textbox', { name })).toBeInTheDocument();
      }
      for (const name of controls.combobox ?? []) {
        expect(inside.getByRole('combobox', { name })).toBeInTheDocument();
      }
      for (const name of controls.date ?? []) {
        expect(inside.getByLabelText(name)).toHaveAttribute('type', 'date');
      }
    }
    expect(
      within(region('Связанная работа')).getByRole('combobox', { name: 'Родитель' }),
    ).toBeInTheDocument();
  });

  it('has no control named after a type the item lacks', async () => {
    await openCard('MSK-US-2');
    expect(screen.queryByRole('textbox', { name: 'Шаги воспроизведения' })).not.toBeInTheDocument();
    expect(screen.queryByRole('combobox', { name: 'Серьёзность' })).not.toBeInTheDocument();
    expect(screen.queryByRole('combobox', { name: 'Тип' })).not.toBeInTheDocument();
  });

  it('names a text by its head: one «Описание» textbox with the type’s hint and placeholder', async () => {
    await openCard('MSK-US-2');
    const description = screen.getAllByRole('textbox', { name: 'Описание' });

    expect(description).toHaveLength(1);
    expect(description[0]).toHaveAttribute('placeholder', 'Добавьте описание');
    expect(description[0]).toHaveAccessibleDescription('До 100 000 символов');
    expect(screen.getByRole('textbox', { name: 'Критерии приёмки' })).toHaveAttribute(
      'placeholder',
      'Опишите, по каким признакам история считается готовой',
    );
  });

  it('keeps the fields’ hints in their descriptions and their placeholders as attributes', async () => {
    await openCard('MSK-US-2');

    expect(screen.getByRole('combobox', { name: 'Приоритет' })).toHaveAccessibleDescription(
      '1 — самый высокий',
    );
    const points = screen.getByRole('textbox', { name: 'Оценка в пунктах' });
    expect(points).toHaveAccessibleDescription('От 0 до 9 999');
    expect(points).toHaveAttribute('placeholder', 'Не оценено');
    expect(points).toHaveAttribute('inputmode', 'decimal');
  });
});

describe('feat-029 US-3: «Связанная работа»', () => {
  it('shows the parent with its state and a link to it, and the children as a list', async () => {
    await openCard('MSK-US-2', {
      prepare: (api) => {
        const story = recordOf(api.data, 2);
        story.parentId = recordOf(api.data, 6).id;
        recordOf(api.data, 3).parentId = story.id;
        recordOf(api.data, 4).parentId = story.id;
      },
    });

    const related = within(region('Связанная работа'));
    expect(related.getByRole('combobox', { name: 'Родитель' })).toHaveValue(
      'MSK-FEAT-6 Вход по ссылке',
    );
    expect(related.getByRole('link', { name: 'Открыть MSK-FEAT-6' })).toHaveAttribute(
      'href',
      expect.stringMatching(/\/workitems\/MSK-FEAT-6$/),
    );
    const children = related.getByRole('list', { name: 'Дочерние элементы (2)' });
    const rows = within(children).getAllByRole('listitem');
    expect(rows).toHaveLength(2);
    // By level, highest first, then by number: the bug before the task.
    expect(within(rows[0] as HTMLElement).getByRole('link', { name: 'MSK-BUG-4' })).toBeVisible();
    expect(within(rows[0] as HTMLElement).getByText('Ссылка истекает раньше срока')).toBeVisible();
    expect(within(rows[1] as HTMLElement).getByRole('link', { name: 'MSK-TASK-3' })).toBeVisible();
    expect(within(rows[1] as HTMLElement).getByText('Шаблон письма')).toBeVisible();
  });

  it('says so when there are no children, and offers no link without a parent', async () => {
    await openCard('MSK-US-2');

    const related = within(region('Связанная работа'));
    expect(related.getByText('Дочерних элементов нет.')).toBeVisible();
    expect(related.queryByRole('link', { name: /^Открыть / })).not.toBeInTheDocument();
    expect(related.getByRole('combobox', { name: 'Родитель' })).toHaveValue('Нет родителя');
  });

  it('names the types the item’s level allows as the parent field’s hint', async () => {
    await openCard('MSK-US-2');

    await waitFor(() =>
      expect(screen.getByRole('combobox', { name: 'Родитель' })).toHaveAccessibleDescription(
        /^Родителем может быть: .*Фича/,
      ),
    );
  });
});

describe('feat-029 BR-C-19: the read-only mode', () => {
  it('shows every field as a value, draws no Save group and no undo, and says why', async () => {
    await openCard('MSK-US-2', { card: { readOnly: true } });

    expect(screen.queryByRole('button', { name: 'Сохранить' })).not.toBeInTheDocument();
    expect(screen.queryByRole('group', { name: 'Сохранение' })).not.toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: 'Отменить все изменения' }),
    ).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Обновить' })).toBeEnabled();
    expect(screen.getByRole('button', { name: 'Ещё действия' })).toBeEnabled();
    expect(screen.getByRole('status')).toHaveTextContent(cardTexts.readOnly);

    const title = screen.getByRole('textbox', { name: 'Заголовок' });
    expect(title).toHaveAttribute('readonly');
    await userEvent.type(title, 'x');
    expect(title).toHaveValue('Письмо со ссылкой');
    expect(screen.getByRole('textbox', { name: 'Описание' })).toHaveAttribute('readonly');
    expect(screen.getByRole('combobox', { name: 'Приоритет' })).toHaveAttribute(
      'aria-readonly',
      'true',
    );
  });

  it('gives the host’s own reason', async () => {
    await openCard('MSK-US-2', {
      card: { readOnly: true, readOnlyText: 'Доступ на запись закрыт.' },
    });

    expect(screen.getByRole('status')).toHaveTextContent('Доступ на запись закрыт.');
  });
});

describe('feat-029: what the card hands its shell (the frame’s contract)', () => {
  function capture() {
    const seen: { frame: CardFrameProps | null } = { frame: null };
    return {
      seen,
      renderFrame: (frame: CardFrameProps) => {
        seen.frame = frame;
        return <PageFrame frame={frame} />;
      },
    };
  }

  it('names the type, the identifier and the sections, per column', async () => {
    const { seen, renderFrame } = capture();
    await openCard('MSK-BUG-4', { card: { renderFrame, mode: 'dialog' } });

    const frame = seen.frame as CardFrameProps;
    expect(frame.mode).toBe('dialog');
    expect(frame.appearance).toBe('bug');
    expect(frame.typeName).toBe('Баг');
    expect(frame.identifier).toBe('MSK-BUG-4');
    expect(frame.main.map((item) => item.key)).toEqual(['description', 'reproSteps', 'systemInfo']);
    expect(frame.side.map((item) => ('title' in item ? item.title : item.key))).toEqual([
      'Планирование',
      'Трудозатраты (часы)',
    ]);
    expect(frame.extra.map((item) => item.key)).toEqual(['related']);
    expect(frame.tabs).toMatchObject({ label: 'Разделы рабочего элемента', details: 'Детали' });
    expect(frame.notice).toBeNull();
    expect(frame.busy).toBe(false);
    expect(frame.dirty).toBe(false);
    expect(frame.pagePath).toMatch(/\/workitems\/MSK-BUG-4$/);
    const related = frame.extra[0];
    expect(related && 'subsection' in related ? related.subsection?.count : null).toBe(0);
  });

  it('hands over the dirty flag, so the dialog can guard', async () => {
    const { seen, renderFrame } = capture();
    await openCard('MSK-US-2', { card: { renderFrame } });

    await userEvent.type(screen.getByRole('textbox', { name: 'Заголовок' }), '!');

    expect((seen.frame as CardFrameProps).dirty).toBe(true);
  });
});
