import { describe, expect, it, vi } from 'vitest';
import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { server } from '../../../../test/mocks/server';
import { cardTexts } from '../../lib/cardTexts';
import { serveProblem, type WorkItemsTestApi } from '../../testApi';
import {
  WORK_ITEM_URL,
  catalogText,
  ids,
  problem,
  recordOf,
  stateId,
  validationProblem,
} from '../../testFixtures';
import { fill, held, openCard, typeInto } from './testCard';

// Each user action renders the kit's fields in jsdom; a test of several steps outlasts the default.
vi.setConfig({ testTimeout: 60_000 });

const title = held(() => screen.getByRole('textbox', { name: 'Заголовок' }));
const save = held(() => screen.getByRole('button', { name: 'Сохранить' }));
const undo = held(() => screen.getByRole('button', { name: 'Отменить все изменения' }));
const patches = (api: WorkItemsTestApi) => api.writes().filter((r) => r.method === 'PATCH');

async function choose(field: string, option: string) {
  await userEvent.click(screen.getByRole('combobox', { name: field }));
  await userEvent.click(
    within(await screen.findByRole('listbox')).getByRole('option', { name: option }),
  );
}

describe('feat-029 US-4: nothing changed, nothing to save', () => {
  it('enables «Сохранить» and the undo for an edit, and disables both for a value changed back', async () => {
    await openCard();
    expect(save()).toBeDisabled();
    expect(undo()).toBeDisabled();

    await choose('Приоритет', '1');
    expect(save()).toBeEnabled();
    expect(screen.getByRole('button', { name: 'Ещё варианты сохранения' })).toBeEnabled();
    expect(undo()).toBeEnabled();

    await choose('Приоритет', '2');
    expect(save()).toBeDisabled();
    expect(undo()).toBeDisabled();

    typeInto(title(), '!');
    expect(save()).toBeEnabled();
    await userEvent.type(title(), '{Backspace}');
    expect(save()).toBeDisabled();
  });
});

describe('feat-029 US-4: only the touched fields are sent', () => {
  it('sends the title and the priority alone, and shows what is stored afterwards', async () => {
    const { api, notify } = await openCard();

    fill(title(), '');
    typeInto(title(), 'Письмо со ссылкой для входа');
    await choose('Приоритет', '1');
    await userEvent.click(save());

    await waitFor(() => expect(notify).toHaveBeenCalledWith(cardTexts.notices.saved));
    expect(patches(api)).toHaveLength(1);
    expect(patches(api)[0]?.body).toEqual({
      title: 'Письмо со ссылкой для входа',
      priority: { value: 1 },
    });
    expect(save()).toBeDisabled();
    expect(undo()).toBeDisabled();
    expect(title()).toHaveValue('Письмо со ссылкой для входа');
    expect(screen.getByText(/^Изменён: /)).toBeInTheDocument();
  });

  it('saves on Enter in the title, once', async () => {
    const { api } = await openCard();

    await userEvent.type(title(), '!{Enter}');

    await waitFor(() => expect(save()).toBeDisabled());
    expect(patches(api)).toHaveLength(1);
    expect(patches(api)[0]?.body).toEqual({ title: 'Письмо со ссылкой!' });
  });

  it('takes focus to the title after a save by the button, since the clean button gives it up (R1-6)', async () => {
    await openCard();
    typeInto(title(), '!');
    await userEvent.click(save());

    await waitFor(() => expect(save()).toBeDisabled());

    expect(title()).toHaveFocus();
  });

  it('leaves focus where it is when the button did not hold it', async () => {
    await openCard();
    typeInto(title(), '!');
    const other = screen.getByRole('textbox', { name: 'Описание' });
    fireEvent.click(save());
    other.focus();

    await waitFor(() => expect(save()).toBeDisabled());

    expect(other).toHaveFocus();
  });

  it('sends one request for a double click', async () => {
    const { api } = await openCard();
    typeInto(title(), '!');

    await userEvent.dblClick(save());

    await waitFor(() => expect(save()).toBeDisabled());
    expect(patches(api)).toHaveLength(1);
  });

  it('chooses the state in the band', async () => {
    const { api } = await openCard();

    await choose('Статус', 'Активна');
    expect(screen.getByRole('combobox', { name: 'Статус' })).toHaveTextContent('Активна');
    await userEvent.click(save());

    await waitFor(() => expect(patches(api)).toHaveLength(1));
    expect(patches(api)[0]?.body).toEqual({ stateId: stateId('US', 'Активна') });
  });

  it('types a number with a comma, and sends it as a number', async () => {
    const { api } = await openCard();

    typeInto(screen.getByRole('textbox', { name: 'Оценка в пунктах' }), '2,5');
    await userEvent.click(save());

    await waitFor(() => expect(patches(api)).toHaveLength(1));
    expect(patches(api)[0]?.body).toEqual({ storyPoints: { value: 2.5 } });
    expect(screen.getByRole('textbox', { name: 'Оценка в пунктах' })).toHaveValue('2,5');
  });
});

describe('feat-029 US-4: the assignee is a field like the rest', () => {
  it('searches from two characters, assigns, and unassigns with `{ userId: null }`', async () => {
    const { api } = await openCard();
    const assignee = () => screen.getByRole('combobox', { name: 'Исполнитель' });

    await userEvent.click(assignee());
    const hints = within(await screen.findByRole('listbox'));
    expect(hints.getByRole('option', { name: 'Не назначен' })).toBeInTheDocument();
    expect(hints.getByText('Введите не менее 2 символов имени или почты')).toBeInTheDocument();

    typeInto(assignee(), 'Ан');
    await userEvent.click(await screen.findByRole('option', { name: /Анна Смирнова/ }));
    expect(assignee()).toHaveValue('Анна Смирнова');
    await userEvent.click(save());
    await waitFor(() => expect(patches(api)).toHaveLength(1));
    expect(patches(api)[0]?.body).toEqual({ assignee: { userId: ids.anna } });

    await userEvent.click(assignee());
    await userEvent.click(await screen.findByRole('option', { name: 'Не назначен' }));
    await userEvent.click(save());
    await waitFor(() => expect(patches(api)).toHaveLength(2));
    expect(patches(api)[1]?.body).toEqual({ assignee: { userId: null } });
  });
});

describe('feat-029 US-4: «Родитель»', () => {
  it('chooses a parent, sends it with the parent that was seen, and then offers its link', async () => {
    const { api } = await openCard();
    const parent = () => screen.getByRole('combobox', { name: 'Родитель' });

    await userEvent.click(parent());
    expect(await screen.findByRole('option', { name: 'Нет родителя' })).toBeInTheDocument();
    await userEvent.click(await screen.findByRole('option', { name: /MSK-FEAT-6 Вход по ссылке/ }));
    expect(parent()).toHaveValue('MSK-FEAT-6 Вход по ссылке');
    // The link opens the stored parent, and there is none yet.
    expect(screen.queryByRole('link', { name: 'Открыть MSK-FEAT-6' })).not.toBeInTheDocument();
    await userEvent.click(save());

    await waitFor(() => expect(patches(api)).toHaveLength(1));
    expect(patches(api)[0]?.body).toEqual({
      parent: { parentId: recordOf(api.data, 6).id, seenParentId: null },
    });
    expect(await screen.findByRole('link', { name: 'Открыть MSK-FEAT-6' })).toBeInTheDocument();
  });

  it('shows the stored parent after a conflict, with the refusal under the field and the other edits kept', async () => {
    const { api } = await openCard('MSK-US-2', {
      prepare: (data) => {
        recordOf(data.data, 2).parentId = recordOf(data.data, 6).id;
      },
    });
    // Someone gave MSK-US-2 another parent after the card was read.
    recordOf(api.data, 2).parentId = recordOf(api.data, 8).id;

    typeInto(screen.getByRole('textbox', { name: 'Описание' }), 'Мой текст');
    await userEvent.click(screen.getByRole('combobox', { name: 'Родитель' }));
    await userEvent.click(await screen.findByRole('option', { name: 'Нет родителя' }));
    await userEvent.click(save());

    expect(await screen.findByText(catalogText('workitems.link.parent_changed'))).toBeVisible();
    await waitFor(() =>
      expect(screen.getByRole('combobox', { name: 'Родитель' })).toHaveValue(
        'MSK-FEAT-8 Вход через SSO',
      ),
    );
    expect(screen.getByRole('textbox', { name: 'Описание' })).toHaveValue('Мой текст');
    expect(screen.getByRole('combobox', { name: 'Родитель' })).toHaveFocus();
  });
});

describe('feat-029 US-4: refusals', () => {
  it('stops at the browser’s checks: the error under the title, framed, with focus; nothing is sent', async () => {
    const { api } = await openCard();

    fill(title(), '');
    await userEvent.click(save());

    expect(screen.getByText('Введите заголовок.')).toBeVisible();
    expect(title()).toHaveAttribute('aria-invalid', 'true');
    expect(title()).toHaveFocus();
    expect(api.writes()).toHaveLength(0);

    typeInto(title(), 'Письмо');
    expect(screen.queryByText('Введите заголовок.')).not.toBeInTheDocument();
    expect(title()).not.toHaveAttribute('aria-invalid');

    typeInto(screen.getByRole('textbox', { name: 'Оценка в пунктах' }), 'пять');
    await userEvent.click(save());
    expect(screen.getByText('Введите число.')).toBeVisible();
    expect(screen.getByRole('textbox', { name: 'Оценка в пунктах' })).toHaveFocus();
    expect(api.writes()).toHaveLength(0);
  });

  it('puts a refusal for a field on it and keeps every edit', async () => {
    const { api, onGone } = await openCard('MSK-EPIC-1');
    serveProblem(
      'patch',
      WORK_ITEM_URL,
      validationProblem({ targetDate: 'workitems.workitem.target_before_start' }),
    );

    fireEvent.change(screen.getByLabelText('Целевая дата'), { target: { value: '2026-12-31' } });
    typeInto(title(), '!');
    await userEvent.click(save());

    const message = catalogText('workitems.workitem.target_before_start');
    expect(await screen.findByText(message)).toBeVisible();
    expect(screen.getByLabelText('Целевая дата')).toHaveFocus();
    expect(screen.getByLabelText('Целевая дата')).toHaveAttribute('aria-invalid', 'true');
    expect(title()).toHaveValue('Вход и регистрация!');
    expect(screen.getByLabelText('Целевая дата')).toHaveValue('2026-12-31');
    expect(save()).toBeEnabled();
    expect(onGone).not.toHaveBeenCalled();
    expect(patches(api)).toHaveLength(0);

    fireEvent.change(screen.getByLabelText('Дата начала'), { target: { value: '2026-10-02' } });
    expect(screen.queryByText(message)).not.toBeInTheDocument();
  });

  it('shows a refusal no field owns in an alert, keeps the edits, and clears it on the next save', async () => {
    const { api } = await openCard();
    server.use(
      http.patch(
        WORK_ITEM_URL,
        () => HttpResponse.json({ detail: 'Сбой сервера.' }, { status: 500 }),
        {
          once: true,
        },
      ),
    );
    typeInto(title(), '!');
    await userEvent.click(save());

    expect(await screen.findByRole('alert')).toHaveTextContent('Сбой сервера.');
    expect(title()).toHaveValue('Письмо со ссылкой!');

    await userEvent.click(save());
    await waitFor(() => expect(patches(api)).toHaveLength(1));
    await waitFor(() => expect(screen.queryByRole('alert')).not.toBeInTheDocument());
  });

  it('hands the frame of an item that has gone to the host', async () => {
    const { onGone } = await openCard();
    serveProblem('patch', WORK_ITEM_URL, problem('workitems.workitem.not_found'));

    typeInto(title(), '!');
    await userEvent.click(save());

    await waitFor(() => expect(onGone).toHaveBeenCalledWith('workItem'));
  });
});
