import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { mountEditor, positionOf, select } from '../../../test/richTextEditor';

const LINK = (href: string, text: string) =>
  `<a href="${href}" rel="noopener noreferrer">${text}</a>`;

afterEach(() => {
  vi.restoreAllMocks();
});

async function openPrompt(
  user: ReturnType<typeof userEvent.setup>,
  button: (n: string) => HTMLElement,
) {
  await user.click(button('Ссылка'));
  return screen.findByRole('dialog', { name: 'Ссылка' });
}

describe('link prompt (US-5, BR-RT-18)', () => {
  it('adds a link over a selection, keeping its text', async () => {
    const user = userEvent.setup();
    const { button, editor } = await mountEditor('<p>документация</p>');
    select(editor(), 1, 1 + 'документация'.length);

    const dialog = await openPrompt(user, button);
    expect(within(dialog).queryByLabelText('Текст ссылки')).not.toBeInTheDocument();
    await user.type(within(dialog).getByLabelText('Адрес ссылки'), 'https://example.org/docs');
    await user.click(within(dialog).getByRole('button', { name: 'Применить' }));

    expect(editor().getHTML()).toBe(`<p>${LINK('https://example.org/docs', 'документация')}</p>`);
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });

  it('with nothing selected asks the text and inserts it as a link; an empty text is the address', async () => {
    const user = userEvent.setup();
    const { button, editor } = await mountEditor('<p>до </p>');
    select(editor(), 4);

    let dialog = await openPrompt(user, button);
    await user.type(within(dialog).getByLabelText('Текст ссылки'), 'сайт');
    await user.type(within(dialog).getByLabelText('Адрес ссылки'), 'https://example.org');
    await user.click(within(dialog).getByRole('button', { name: 'Применить' }));
    expect(editor().getHTML()).toBe(`<p>до ${LINK('https://example.org', 'сайт')}</p>`);

    act(() => {
      editor().commands.setTextSelection(editor().state.doc.content.size - 1);
      editor().commands.insertContent(' ');
    });
    dialog = await openPrompt(user, button);
    await user.type(within(dialog).getByLabelText('Адрес ссылки'), 'mailto:a@example.org{Enter}');

    expect(editor().getHTML()).toContain(LINK('mailto:a@example.org', 'mailto:a@example.org'));
  });

  it('stores www. as https://www.', async () => {
    const user = userEvent.setup();
    const { button, editor } = await mountEditor('<p>сайт</p>');
    select(editor(), 1, 5);

    const dialog = await openPrompt(user, button);
    await user.type(within(dialog).getByLabelText('Адрес ссылки'), 'www.example.org{Enter}');

    expect(editor().getHTML()).toBe(`<p>${LINK('https://www.example.org', 'сайт')}</p>`);
  });

  it('refuses an unsupported address on the field and stays open', async () => {
    const user = userEvent.setup();
    const { button, editor } = await mountEditor('<p>сайт</p>');
    select(editor(), 1, 5);

    const dialog = await openPrompt(user, button);
    const address = within(dialog).getByLabelText('Адрес ссылки');
    await user.type(address, 'javascript:alert(1)');
    await user.click(within(dialog).getByRole('button', { name: 'Применить' }));

    expect(
      within(dialog).getByText(
        'Допустимы адреса, начинающиеся с http://, https://, mailto: или www.',
      ),
    ).toBeInTheDocument();
    expect(address).toHaveAttribute('aria-invalid', 'true');
    expect(editor().getHTML()).toBe('<p>сайт</p>');

    await user.clear(address);
    await user.click(within(dialog).getByRole('button', { name: 'Применить' }));
    expect(within(dialog).getByText('Укажите адрес ссылки.')).toBeInTheDocument();
    expect(screen.getByRole('dialog', { name: 'Ссылка' })).toBeInTheDocument();
  });

  it('opens with Ctrl+K from the body, prefilled when the caret is in a link, and changes the address', async () => {
    const user = userEvent.setup();
    const { body, editor } = await mountEditor(`<p>${LINK('https://old.example', 'тут')}</p>`);
    await user.click(body);
    select(editor(), positionOf(editor(), 'тут') + 1);

    await user.keyboard('{Control>}k{/Control}');
    const dialog = await screen.findByRole('dialog', { name: 'Ссылка' });
    const address = within(dialog).getByLabelText('Адрес ссылки');
    expect(address).toHaveValue('https://old.example');

    await user.clear(address);
    await user.type(address, 'https://new.example{Enter}');

    expect(editor().getHTML()).toBe(`<p>${LINK('https://new.example', 'тут')}</p>`);
  });

  it('closes on «Отмена» and on Escape, with the document untouched and focus back in the body', async () => {
    const user = userEvent.setup();
    const { button, editor, body } = await mountEditor('<p>текст</p>');
    select(editor(), 1, 3);

    let dialog = await openPrompt(user, button);
    await user.click(within(dialog).getByRole('button', { name: 'Отмена' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    await waitFor(() => expect(body).toHaveFocus());

    dialog = await openPrompt(user, button);
    await user.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(editor().getHTML()).toBe('<p>текст</p>');
  });

  it('is disabled in a code block', async () => {
    const { button, editor } = await mountEditor('<pre><code>x</code></pre>');
    select(editor(), 1);

    await waitFor(() => expect(button('Ссылка')).toBeDisabled());
  });
});

describe('unlink and open (BR-RT-18)', () => {
  it('removes the link over its whole extent and keeps the text', async () => {
    const user = userEvent.setup();
    const { button, editor } = await mountEditor(
      `<p>а ${LINK('https://example.org', 'ссылка')} б</p>`,
    );
    select(editor(), positionOf(editor(), 'ссылка') + 2);

    await waitFor(() => expect(button('Убрать ссылку')).toBeEnabled());
    await user.click(button('Убрать ссылку'));

    expect(editor().getHTML()).toBe('<p>а ссылка б</p>');
  });

  it('opens in a new tab on purpose, with noopener and noreferrer, and never navigates', async () => {
    const user = userEvent.setup();
    const open = vi.spyOn(window, 'open').mockImplementation(() => null);
    const { button, editor } = await mountEditor(`<p>${LINK('https://example.org', 'ссылка')}</p>`);

    expect(button('Открыть в новой вкладке')).toBeDisabled();
    select(editor(), positionOf(editor(), 'ссылка') + 1);
    await waitFor(() => expect(button('Открыть в новой вкладке')).toBeEnabled());
    await user.click(button('Открыть в новой вкладке'));

    expect(open).toHaveBeenCalledExactlyOnceWith(
      'https://example.org',
      '_blank',
      'noopener,noreferrer',
    );
  });

  it('a plain click on a link only places the caret; Ctrl+click opens it', async () => {
    const open = vi.spyOn(window, 'open').mockImplementation(() => null);
    const { editor } = await mountEditor(`<p>${LINK('https://example.org', 'ссылка')}</p>`);
    const view = editor().view;
    const position = positionOf(editor(), 'ссылка') + 2;

    const plain = new MouseEvent('click', { bubbles: true, cancelable: true });
    const handled = view.someProp('handleClick', (handler) => handler(view, position, plain));
    expect(handled).toBeFalsy();
    expect(open).not.toHaveBeenCalled();

    const withCtrl = new MouseEvent('click', { bubbles: true, cancelable: true, ctrlKey: true });
    const opened = view.someProp('handleClick', (handler) => handler(view, position, withCtrl));
    expect(opened).toBe(true);
    expect(open).toHaveBeenCalledExactlyOnceWith(
      'https://example.org',
      '_blank',
      'noopener,noreferrer',
    );
  });
});
