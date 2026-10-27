import { describe, expect, it, vi } from 'vitest';
import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { server } from '../../../../test/mocks/server';
import { cardTexts } from '../../lib/cardTexts';
import type { WorkItemsTestApi } from '../../testApi';
import { WORK_ITEM_URL, ids, recordOf } from '../../testFixtures';
import { fill, held, openCard, typeInto } from './testCard';

// Each user action renders the kit's fields in jsdom; a test of several steps outlasts the default.
vi.setConfig({ testTimeout: 60_000 });

const title = held(() => screen.getByRole('textbox', { name: 'Заголовок' }));
const description = held(() => screen.getByRole('textbox', { name: 'Описание' }));
const save = held(() => screen.getByRole('button', { name: 'Сохранить' }));
const patches = (api: WorkItemsTestApi) => api.writes().filter((r) => r.method === 'PATCH');

/** Holds the item's read until `release()`; the real handler then answers it. */
function holdRead() {
  let release: () => void = () => {};
  const held = new Promise<void>((resolve) => {
    release = resolve;
  });
  server.use(
    http.get(WORK_ITEM_URL, async () => {
      await held;
      // No answer of its own: MSW passes the request on to the next handler.
      return undefined;
    }),
  );
  return release;
}

describe('feat-029 US-5: «Обновить»', () => {
  it('shows what others changed and tells the user', async () => {
    const { api, notify } = await openCard();
    const record = recordOf(api.data, 2);
    record.fields.priority = 1;

    await userEvent.click(screen.getByRole('button', { name: 'Обновить' }));

    await waitFor(() =>
      expect(screen.getByRole('combobox', { name: 'Приоритет' })).toHaveTextContent('1'),
    );
    expect(notify).toHaveBeenCalledWith(cardTexts.notices.refreshed);
    expect(patches(api)).toHaveLength(0);
  });

  it('keeps what the user typed, and a save then sends it alone', async () => {
    const { api, notify } = await openCard();
    typeInto(description(), 'Мой текст');
    const record = recordOf(api.data, 2);
    record.description = 'Чужой текст';
    record.fields.priority = 1;

    await userEvent.click(screen.getByRole('button', { name: 'Обновить' }));

    await waitFor(() => expect(notify).toHaveBeenCalledWith(cardTexts.notices.refreshedKept));
    expect(description()).toHaveValue('Мой текст');
    expect(screen.getByRole('combobox', { name: 'Приоритет' })).toHaveTextContent('1');
    expect(save()).toBeEnabled();

    await userEvent.click(save());
    await waitFor(() => expect(patches(api)).toHaveLength(1));
    expect(patches(api)[0]?.body).toEqual({ description: 'Мой текст' });
  });

  it('shows a failed read in an alert and leaves the card as it was', async () => {
    const { notify } = await openCard();
    typeInto(title(), '!');
    server.use(
      http.get(WORK_ITEM_URL, () => HttpResponse.json({ detail: 'Сбой чтения.' }, { status: 500 })),
    );

    await userEvent.click(screen.getByRole('button', { name: 'Обновить' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Сбой чтения.');
    expect(title()).toHaveValue('Письмо со ссылкой!');
    expect(notify).not.toHaveBeenCalled();
  });
});

describe('feat-029 US-5: «Обновить» is slow (R1-7)', () => {
  it('puts the Save group out of reach over an edit, so a click sends nothing and says it is waiting', async () => {
    const { api } = await openCard();
    typeInto(title(), '!');
    const release = holdRead();

    await userEvent.click(screen.getByRole('button', { name: 'Обновить' }));

    await waitFor(() => expect(save()).toHaveAttribute('aria-busy', 'true'));
    await userEvent.click(save());
    release();
    await waitFor(() => expect(save()).not.toHaveAttribute('aria-busy'));
    expect(patches(api)).toHaveLength(0);
    expect(save()).toBeEnabled();
  });

  it('does not draw a clean card’s Save group as busy', async () => {
    await openCard();
    const release = holdRead();

    await userEvent.click(screen.getByRole('button', { name: 'Обновить' }));

    expect(save()).toBeDisabled();
    expect(save()).not.toHaveAttribute('aria-busy');
    release();
  });

  it('lets the undo run: the edits go, and the notice does not claim they stayed', async () => {
    const { notify } = await openCard();
    typeInto(title(), '!');
    const release = holdRead();
    await userEvent.click(screen.getByRole('button', { name: 'Обновить' }));

    await userEvent.click(screen.getByRole('button', { name: 'Отменить все изменения' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Отменить изменения' }));
    await waitFor(() => expect(title()).toHaveValue('Письмо со ссылкой'));
    release();

    await waitFor(() => expect(notify).toHaveBeenCalledWith(cardTexts.notices.refreshed));
    expect(notify).toHaveBeenCalledWith(cardTexts.notices.undone);
    expect(notify).not.toHaveBeenCalledWith(cardTexts.notices.refreshedKept);
    expect(title()).toHaveValue('Письмо со ссылкой');
  });

  it('words the notice from what was typed during the read', async () => {
    const { notify } = await openCard();
    const release = holdRead();
    await userEvent.click(screen.getByRole('button', { name: 'Обновить' }));

    typeInto(description(), 'Текст, набранный во время чтения');
    release();

    await waitFor(() => expect(notify).toHaveBeenCalledWith(cardTexts.notices.refreshedKept));
    expect(description()).toHaveValue('Текст, набранный во время чтения');
  });
});

describe('feat-029 US-5: «Отменить все изменения»', () => {
  it('asks first, keeps the edits on «Продолжить редактирование», and drops them on the answer', async () => {
    const { notify } = await openCard();
    typeInto(title(), '!');
    typeInto(description(), 'Текст');

    await userEvent.click(screen.getByRole('button', { name: 'Отменить все изменения' }));
    const dialog = await screen.findByRole('dialog', { name: 'Отменить все изменения?' });
    expect(dialog).toHaveTextContent(
      'Поля вернутся к сохранённым значениям. Несохранённые изменения рабочего элемента MSK-US-2 будут потеряны.',
    );
    await waitFor(() =>
      expect(
        within(dialog).getByRole('button', { name: 'Продолжить редактирование' }),
      ).toHaveFocus(),
    );
    await userEvent.click(
      within(dialog).getByRole('button', { name: 'Продолжить редактирование' }),
    );
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(title()).toHaveValue('Письмо со ссылкой!');
    expect(notify).not.toHaveBeenCalled();

    await userEvent.click(screen.getByRole('button', { name: 'Отменить все изменения' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Отменить изменения' }));

    // The question is gone before the card is queried by role: its modal hides the page until then.
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(title()).toHaveValue('Письмо со ссылкой');
    expect(description()).toHaveValue('');
    expect(save()).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Отменить все изменения' })).toBeDisabled();
    expect(notify).toHaveBeenCalledWith(cardTexts.notices.undone);
  });
});

describe('feat-029 US-5: «Ещё действия»', () => {
  // mod-005 D-4 (design § 2): feat-029's "nothing else" is superseded for a holder of `workitems.workitem.delete` ("Удалить",
  // "Удалить навсегда" follow, WorkItemCard.delete.test.tsx). A card opened without `canDelete` (the default) is the reader's:
  // its menu holds the copy and nothing else.
  it('holds «Копировать ссылку» and nothing else for a reader without workitems.workitem.delete, and puts the page’s address on the clipboard', async () => {
    const user = userEvent.setup();
    const { notify } = await openCard();

    await user.click(screen.getByRole('button', { name: 'Ещё действия' }));
    const items = within(await screen.findByRole('menu')).getAllByRole('menuitem');
    expect(items.map((item) => item.textContent)).toEqual(['Копировать ссылку']);
    await user.click(items[0] as HTMLElement);

    const address = `${window.location.origin}/organizations/${ids.example}/projects/${ids.msk}/workitems/MSK-US-2`;
    expect(await navigator.clipboard.readText()).toBe(address);
    expect(notify).toHaveBeenCalledWith(cardTexts.notices.linkCopied);
  });

  it('says so when the browser refuses the clipboard', async () => {
    const user = userEvent.setup();
    const { notify } = await openCard();
    vi.spyOn(navigator.clipboard, 'writeText').mockRejectedValue(new Error('denied'));

    await user.click(screen.getByRole('button', { name: 'Ещё действия' }));
    await user.click(await screen.findByRole('menuitem', { name: 'Копировать ссылку' }));

    await waitFor(() => expect(notify).toHaveBeenCalledWith(cardTexts.notices.linkCopyFailed));
  });
});

describe('feat-029 BR-C-8: «Сохранить и закрыть»', () => {
  async function chooseSaveAndClose() {
    await userEvent.click(screen.getByRole('button', { name: 'Ещё варианты сохранения' }));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Сохранить и закрыть' }));
  }

  it('saves, and closes once the save was accepted', async () => {
    const onClose = vi.fn();
    const { api } = await openCard('MSK-US-2', { card: { onClose } });
    typeInto(title(), '!');

    await chooseSaveAndClose();

    await waitFor(() => expect(onClose).toHaveBeenCalledTimes(1));
    expect(patches(api)).toHaveLength(1);
    expect(patches(api)[0]?.body).toEqual({ title: 'Письмо со ссылкой!' });
  });

  it('closes nothing when the save is refused', async () => {
    const onClose = vi.fn();
    const { api } = await openCard('MSK-US-2', { card: { onClose } });
    fill(title(), '');

    await chooseSaveAndClose();

    expect(await screen.findByText('Введите заголовок.')).toBeVisible();
    expect(onClose).not.toHaveBeenCalled();
    expect(api.writes()).toHaveLength(0);
  });
});

describe('feat-029 BR-C-5: a related item in the dialog', () => {
  const withParent = (data: WorkItemsTestApi) => {
    recordOf(data.data, 2).parentId = recordOf(data.data, 6).id;
    recordOf(data.data, 3).parentId = recordOf(data.data, 2).id;
  };

  it('switches the dialog on a plain click, for the parent and for a child', async () => {
    const onOpenItem = vi.fn();
    await openCard('MSK-US-2', { prepare: withParent, card: { onOpenItem } });

    await userEvent.click(screen.getByRole('link', { name: 'Открыть MSK-FEAT-6' }));
    expect(onOpenItem).toHaveBeenLastCalledWith('MSK-FEAT-6');
    await userEvent.click(screen.getByRole('link', { name: 'MSK-TASK-3' }));
    expect(onOpenItem).toHaveBeenLastCalledWith('MSK-TASK-3');
    expect(onOpenItem).toHaveBeenCalledTimes(2);
  });

  it('leaves a modified click to the browser, and a page’s links alone', async () => {
    const onOpenItem = vi.fn();
    await openCard('MSK-US-2', { prepare: withParent, card: { onOpenItem } });

    const link = screen.getByRole('link', { name: 'Открыть MSK-FEAT-6' });
    expect(link).toHaveAttribute('href', expect.stringMatching(/\/workitems\/MSK-FEAT-6$/));
    fireEvent.click(link, { ctrlKey: true });
    fireEvent.click(link, { metaKey: true });
    fireEvent.click(link, { shiftKey: true });
    fireEvent.click(link, { button: 1 });
    expect(onOpenItem).not.toHaveBeenCalled();
  });

  it('asks «Перейти без сохранения?» first when edits stand', async () => {
    const onOpenItem = vi.fn();
    await openCard('MSK-US-2', { prepare: withParent, card: { onOpenItem } });
    typeInto(description(), 'Текст');

    await userEvent.click(screen.getByRole('link', { name: 'Открыть MSK-FEAT-6' }));
    const dialog = await screen.findByRole('dialog', { name: 'Перейти без сохранения?' });
    expect(dialog).toHaveTextContent(
      'Несохранённые изменения рабочего элемента MSK-US-2 будут потеряны.',
    );
    expect(onOpenItem).not.toHaveBeenCalled();

    await userEvent.click(
      within(dialog).getByRole('button', { name: 'Продолжить редактирование' }),
    );
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(onOpenItem).not.toHaveBeenCalled();
    expect(description()).toHaveValue('Текст');

    await userEvent.click(screen.getByRole('link', { name: 'Открыть MSK-FEAT-6' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Перейти без сохранения' }));
    expect(onOpenItem).toHaveBeenCalledWith('MSK-FEAT-6');
  });
});
