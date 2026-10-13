import { describe, expect, it } from 'vitest';
import { act, fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { server } from '../../../test/mocks/server';
import { pasteInto } from '../../../test/richTextEditor';
import { serveWorkItems, type WorkItemsTestApi } from '../testApi';
import { WORK_ITEM_URL, exampleDataset, ids, validationProblem } from '../testFixtures';
import { mskForm, renderWorkItems } from '../testRender';

// feat-018 US-1, US-3, US-4: the work item form's «Описание» is the rich-text editor, its
// baseline is the stored value, and an untouched one is never written.

const BUG_4 = mskForm('MSK-BUG-4');

interface Stored {
  description: string;
  format?: 'plain' | 'html';
}

async function openWith(stored: Stored) {
  const data = exampleDataset();
  const api = serveWorkItems(data);
  const record = api.record(ids.msk, 4);
  record.description = stored.description;
  record.descriptionFormat = stored.format ?? 'plain';
  const view = renderWorkItems(BUG_4);
  await screen.findByRole('textbox', { name: 'Заголовок' });
  // The first form of a file also pays the editor chunk's first transform.
  await screen.findByRole('textbox', { name: 'Описание' }, { timeout: 20_000 });
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

describe('feat-018 US-3: existing text opens as it was and is not a change', () => {
  it('shows each line of a stored plain text as a paragraph, markup characters as text, spaces kept', async () => {
    const { description, save } = await openWith({
      description: '  шаг 1:\n\t- <b>а</b>   б\n\nконец',
    });

    const paragraphs = Array.from(description().querySelectorAll('p'));
    expect(paragraphs.map((p) => p.textContent)).toEqual([
      '  шаг 1:',
      '\t- <b>а</b>   б',
      '',
      'конец',
    ]);
    expect(description().querySelector('b')).toBeNull();
    expect(save()).toBeDisabled();
  });

  it('another field is saved without the description and its format, whatever the stored one is', async () => {
    const { api, title, save, description } = await openWith({ description: 'старое\nописание' });

    await userEvent.click(title());
    await userEvent.type(title(), '!');
    await waitFor(() => expect(save()).toBeEnabled());
    await userEvent.click(save());
    await screen.findByText('Изменения сохранены.');

    expect(patches(api).map((request) => request.body)).toEqual([{ title: 'Падение при входе!' }]);
    // The stored legacy text is left exactly as it was.
    expect(api.record(ids.msk, 4).description).toBe('старое\nописание');
    expect(api.record(ids.msk, 4).descriptionFormat).toBe('plain');
    expect(description()).toHaveTextContent('старое');
  });

  it('the first save that changes it writes html with its format', async () => {
    const { api, description, save } = await openWith({ description: 'строка 1\nстрока 2' });

    await userEvent.click(description());
    await userEvent.keyboard('!');
    await waitFor(() => expect(save()).toBeEnabled());
    await userEvent.click(save());
    await screen.findByText('Изменения сохранены.');

    const [request] = patches(api);
    expect(request?.body).toEqual({
      description: expect.stringMatching(/^<p>.*<\/p><p>.*<\/p>$/) as unknown as string,
      descriptionFormat: 'html',
    });
    const sent = (request?.body as { description: string }).description;
    expect(sent.replace('!', '')).toBe('<p>строка 1</p><p>строка 2</p>');
    expect(api.record(ids.msk, 4)).toMatchObject({ description: sent, descriptionFormat: 'html' });
    await waitFor(() => expect(save()).toBeDisabled());
  });

  it('a change undone is not a change: «Сохранить» is disabled again', async () => {
    const { description, save } = await openWith({ description: 'текст' });

    await userEvent.click(description());
    await userEvent.keyboard('!');
    await waitFor(() => expect(save()).toBeEnabled());
    await userEvent.keyboard('{Control>}z{/Control}');

    await waitFor(() => expect(save()).toBeDisabled());
  });

  it('a stored table and nested lists open without a change, and the caret moving is not one', async () => {
    const stored =
      '<ul><li><p>а</p><ul><li><p>б</p></li></ul></li></ul>' +
      '<table><tbody><tr><th><p>з</p></th></tr><tr><td><p>я</p></td></tr></tbody></table>';
    const { api, description, save, title } = await openWith({
      description: stored,
      format: 'html',
    });

    await userEvent.click(description());
    await userEvent.keyboard('{ArrowDown}{ArrowDown}{ArrowUp}');
    await userEvent.click(title());
    await userEvent.tab();

    expect(save()).toBeDisabled();
    expect(patches(api)).toEqual([]);
  });

  it('blank lines only: untouched sends nothing; emptied explicitly stores ""', async () => {
    const { api, description, save } = await openWith({ description: '\n\n\n' });
    expect(save()).toBeDisabled();

    await userEvent.click(description());
    await userEvent.keyboard('x');
    await waitFor(() => expect(save()).toBeEnabled());
    await userEvent.keyboard('{Control>}z{/Control}');
    // «x» undone is the loaded document: no change.
    await waitFor(() => expect(save()).toBeDisabled());
    expect(patches(api)).toEqual([]);
  });

  it('50 000 emoji of a legacy text count 50 000 and still save after one more character', async () => {
    const { api, description, save } = await openWith({ description: '😀'.repeat(50_000) });

    await userEvent.click(description());
    await userEvent.keyboard('!');
    await waitFor(() => expect(save()).toBeEnabled(), { timeout: 30_000 });
    await userEvent.click(save());
    await screen.findByText('Изменения сохранены.', undefined, { timeout: 30_000 });

    expect(patches(api)).toHaveLength(1);
    expect(api.record(ids.msk, 4).descriptionFormat).toBe('html');
  }, 120_000);
});

describe('feat-018 US-1, US-4: limits, refusals and the server’s value', () => {
  it('exactly 100 000 characters are sent; one more is refused before the request', async () => {
    const { api, description, save } = await openWith({ description: '' });
    await userEvent.click(description());

    pasteInto(description(), { text: 'о'.repeat(100_000) });
    await waitFor(() => expect(save()).toBeEnabled());
    await userEvent.click(save());
    await screen.findByText('Изменения сохранены.');
    expect(patches(api)).toHaveLength(1);

    await userEvent.click(description());
    await userEvent.keyboard('о');
    await waitFor(() => expect(save()).toBeEnabled());
    await userEvent.click(save());

    expect(await screen.findByText('Не более 100 000 символов.')).toBeInTheDocument();
    expect(patches(api)).toHaveLength(1);
  }, 60_000);

  it('the markup refusal of the server lands under the editor, the input kept', async () => {
    const { description, save } = await openWith({ description: '' });
    server.use(
      http.patch(
        WORK_ITEM_URL,
        () =>
          HttpResponse.json(
            validationProblem({
              description: 'workitems.workitem.description_markup_too_large',
            }),
            { status: 400 },
          ),
        { once: true },
      ),
    );
    await userEvent.type(description(), 'Таблица');
    await waitFor(() => expect(save()).toBeEnabled());

    await userEvent.click(save());

    const text = 'Слишком много разметки. Упростите форматирование или сократите текст.';
    expect(await screen.findByText(text)).toBeInTheDocument();
    expect(description()).toHaveTextContent('Таблица');
    expect(description()).toHaveAttribute('aria-invalid', 'true');
    // Under the editor, not in the banner.
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('description_format_unknown is the form’s banner, not the editor’s error', async () => {
    const { description, save } = await openWith({ description: '' });
    server.use(
      http.patch(
        WORK_ITEM_URL,
        () =>
          HttpResponse.json(
            validationProblem({ description: 'workitems.workitem.description_format_unknown' }),
            { status: 400 },
          ),
        { once: true },
      ),
    );
    await userEvent.type(description(), 'Текст');
    await waitFor(() => expect(save()).toBeEnabled());

    await userEvent.click(save());

    const banner = await screen.findByRole('alert');
    expect(banner).toHaveTextContent(
      'Неизвестный формат описания. Обновите страницу и попробуйте ещё раз.',
    );
    expect(description()).not.toHaveAttribute('aria-invalid', 'true');
    expect(description()).toHaveTextContent('Текст');
  });

  it('after a save the editor shows the server’s reduced value and the form is not dirty', async () => {
    const { description, save } = await openWith({ description: '' });
    server.use(
      http.patch(
        WORK_ITEM_URL,
        async ({ request }) => {
          const body = (await request.json()) as { description: string };
          // The server's reduction differs from what the editor sent.
          return HttpResponse.json(
            {
              ...(await fetchItem()),
              description: '<p>Сервер: ' + body.description.length + '</p>',
              descriptionFormat: 'html',
            },
            { status: 200 },
          );
        },
        { once: true },
      ),
    );
    await userEvent.type(description(), 'ab');
    await waitFor(() => expect(save()).toBeEnabled());

    await userEvent.click(save());

    await waitFor(() => expect(description()).toHaveTextContent(/^Сервер: \d+$/));
    expect(save()).toBeDisabled();
  });
});

/** The item as the in-memory API answers a read, for a one-off PATCH override to build on. */
async function fetchItem(): Promise<Record<string, unknown>> {
  const response = await fetch(
    `http://localhost:5000/api/organizations/${ids.example}/projects/${ids.msk}/workitems/MSK-BUG-4`,
  );
  return (await response.json()) as Record<string, unknown>;
}

describe('feat-018: the editor inside the form', () => {
  it('a link applied in the prompt does not save the form', async () => {
    const { api, description } = await openWith({ description: 'документация' });
    await userEvent.click(description());
    await userEvent.keyboard('{Control>}a{/Control}');

    await userEvent.click(screen.getByRole('button', { name: 'Ссылка' }));
    const dialog = await screen.findByRole('dialog', { name: 'Ссылка' });
    await userEvent.type(
      within(dialog).getByLabelText('Адрес ссылки'),
      'https://example.org{Enter}',
    );

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(description().querySelector('a')).not.toBeNull();
    expect(patches(api)).toEqual([]);
  });

  it('a submit while the editor still holds a change reads it first (flush)', async () => {
    const { api, description, title } = await openWith({ description: '' });
    await userEvent.click(description());
    await userEvent.keyboard('быстро');

    // The form is submitted at once, before the editor's own delay has passed.
    act(() => {
      fireEvent.submit(title().closest('form') as HTMLFormElement);
    });

    await screen.findByText('Изменения сохранены.');
    expect(patches(api).map((request) => request.body)).toEqual([
      { description: '<p>быстро</p>', descriptionFormat: 'html' },
    ]);
  });

  it('Tab leaves the body to the next control, and the pending change makes «Сохранить» reachable', async () => {
    const { description, save } = await openWith({ description: '' });
    await userEvent.click(description());
    await userEvent.keyboard('текст');

    await userEvent.tab();

    expect(save()).toBeEnabled();
    expect(description()).not.toHaveFocus();
  });
});
