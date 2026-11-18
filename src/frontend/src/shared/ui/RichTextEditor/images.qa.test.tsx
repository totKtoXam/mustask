import { act, cleanup, fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { Editor } from '@tiptap/core';
import { NodeSelection, TextSelection } from '@tiptap/pm/state';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { mountEditor, pasteInto, positionOf } from '../../../test/richTextEditor';
import { formatSize } from './images/dialogs';
import {
  CANNOT_REDUCE_CODE,
  ImageRefusal,
  type ImageReductionResult,
  type RichTextImages,
  type StoredImage,
} from './images/host';

/**
 * feat-078 QA, frontend (qa.md § Frontend, checks F-n): what the developer's images.test.tsx
 * leaves out — order, insert place, drop position, boundaries, refusals, rich paste, the
 * placeholder's cancel, the image menu, focus return, the reduction queue and its other bodies.
 * A test marked `it.fails` pins an open defect of qa.md (D-n): it turns red when the defect is
 * fixed, and is then flipped to `it`.
 */

const ID = '3f2b8c1e-9a4d-4e6b-8f21-0a1b2c3d4e5f';
const ids = [
  '11111111-1111-4111-8111-111111111111',
  '22222222-2222-4222-8222-222222222222',
  '33333333-3333-4333-8333-333333333333',
];
const stored = (id = ID, alt = 'схема') => `<img data-image-id="${id}" alt="${alt}">`;
const image = (id: string, byteLength = 1000): StoredImage => ({
  id,
  format: 'png',
  byteLength,
  width: 40,
  height: 30,
});

function file(name: string, type: string, size = 100, bytes = new Uint8Array(8)): File {
  const made = new File(['x'], name, { type });
  Object.defineProperty(made, 'size', { value: size });
  Object.defineProperty(made, 'arrayBuffer', { value: async () => bytes.buffer });
  return made;
}

function host(over: Partial<RichTextImages> = {}) {
  const released: string[] = [];
  const images: RichTextImages = {
    upload: vi.fn(async () => image(ID)),
    reduce: vi.fn(async (sent: File): Promise<ImageReductionResult> => ({
      image: image(sent.name.startsWith('восемь') ? ids[2]! : ids[1]!, 4_000_000),
      original: { format: 'jpeg', byteLength: sent.size, width: 8160, height: 6120 },
    })),
    address: vi.fn(async (reference: string) => `blob:test/${reference}`),
    release: (address) => released.push(address),
    ...over,
  };
  return { images, released };
}

function choose(...files: File[]) {
  fireEvent.change(screen.getByTestId('rt-image-input'), { target: { files } });
}

function drop(body: HTMLElement, files: File[]): Event {
  const event = new Event('drop', { bubbles: true, cancelable: true });
  Object.defineProperty(event, 'dataTransfer', {
    value: { types: ['Files'], files, items: [], getData: () => '' },
  });
  Object.defineProperty(event, 'clientX', { value: 10 });
  Object.defineProperty(event, 'clientY', { value: 10 });
  act(() => {
    body.dispatchEvent(event);
  });
  return event;
}

/** The top-level node names, images as `img:<id>`. */
function outline(editor: Editor): string[] {
  const names: string[] = [];
  editor.state.doc.forEach((node) => {
    names.push(
      node.type.name === 'rtImage'
        ? `img:${node.attrs.imageId as string}`
        : node.isTextblock
          ? `${node.type.name}:${node.textContent}`
          : node.type.name,
    );
  });
  return names;
}

function imagePos(editor: Editor): number {
  let at = -1;
  editor.state.doc.descendants((node, pos) => {
    if (at < 0 && (node.type.name === 'rtImage' || node.type.name === 'rtImagePlaceholder')) {
      at = pos;
    }
  });
  return at;
}

function selectNode(editor: Editor, pos: number) {
  act(() => {
    editor.view.dispatch(editor.state.tr.setSelection(NodeSelection.create(editor.state.doc, pos)));
  });
}

function caret(editor: Editor, pos: number) {
  act(() => {
    editor.view.dispatch(editor.state.tr.setSelection(TextSelection.create(editor.state.doc, pos)));
  });
}

/** Everything the polite notice line said, in order, from now on (it is replaced in place). */
function recordNotices(): string[] {
  const region = screen
    .getAllByRole('status')
    .find((element) => element.getAttribute('aria-live') === 'polite')!;
  const said: string[] = [];
  new MutationObserver(() => {
    const text = region.textContent ?? '';
    if (text !== '' && said.at(-1) !== text) said.push(text);
  }).observe(region, { childList: true, characterData: true, subtree: true });
  return said;
}

const notice = () =>
  screen
    .getAllByRole('status')
    .map((element) => element.textContent ?? '')
    .join('\n');

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe('F-1 the control', () => {
  it('opens a multiple chooser of the four formats', async () => {
    const { images } = host();
    await mountEditor('<p>до</p>', { images });

    const input = screen.getByTestId('rt-image-input');
    expect(input).toHaveAttribute('multiple');
    expect(input).toHaveAttribute('type', 'file');
    expect(input.getAttribute('accept')).toBe('image/png,image/jpeg,image/gif,image/webp');
  });

  // D-5: the tooltip is the control's name, US-1 copy says «Вставить изображение».
  it.fails('has the tooltip «Вставить изображение» (US-1 copy)', async () => {
    const user = userEvent.setup();
    const { images } = host();
    const { button } = await mountEditor('<p>до</p>', { images });

    await user.hover(button('Изображение'));

    expect(await screen.findByRole('tooltip')).toHaveTextContent('Вставить изображение');
  });

  it('is present but disabled while the card saves (design § states)', async () => {
    const { images } = host();
    const { button } = await mountEditor('<p>до</p>', { images, disabled: true });

    expect(button('Изображение')).toBeDisabled();
  });
});

describe('F-2, F-3 order, placeholder, alternative', () => {
  it('keeps the chosen order when the uploads finish in reverse', async () => {
    const finish: ((value: StoredImage) => void)[] = [];
    const { images } = host({
      upload: vi.fn(() => new Promise<StoredImage>((resolve) => finish.push(resolve))),
    });
    const { editor } = await mountEditor('<p>до</p><p>после</p>', { images });
    caret(editor(), positionOf(editor(), 'до') + 2);

    choose(file('a.jpg', 'image/jpeg'), file('b.jpg', 'image/jpeg'), file('c.jpg', 'image/jpeg'));
    await waitFor(() => expect(finish).toHaveLength(3));
    expect(screen.getAllByText('Загрузка изображения…')).toHaveLength(3);
    expect(screen.getAllByRole('group', { name: 'Загрузка изображения…' })[0]).toHaveAttribute(
      'aria-busy',
      'true',
    );

    await act(async () => finish[2]!(image(ids[2]!)));
    await act(async () => finish[1]!(image(ids[1]!)));
    await act(async () => finish[0]!(image(ids[0]!)));

    await waitFor(() =>
      expect(outline(editor())).toEqual([
        'paragraph:до',
        `img:${ids[0]}`,
        `img:${ids[1]}`,
        `img:${ids[2]}`,
        'paragraph:после',
      ]),
    );
    expect(editor().getHTML()).toContain('alt="a"');
    expect(editor().getHTML()).toContain('alt="c"');
    expect(notice()).toContain('Изображение вставлено.');
  });

  // D-11: an insert that ends the document leaves the new image selected, and the keyboard hint
  // («Enter — открыть…») replaces «Изображение вставлено.», though no key selected it.
  it.fails('says «Изображение вставлено.» after an insert at the end of the text', async () => {
    const { images } = host();
    const { editor } = await mountEditor('<p>до</p>', { images });
    caret(editor(), positionOf(editor(), 'до') + 2);

    choose(file('a.png', 'image/png'));

    await waitFor(() => expect(editor().getHTML()).toContain(`data-image-id="${ID}"`));
    await act(async () => new Promise((resolve) => setTimeout(resolve, 20)));
    expect(notice()).toContain('Изображение вставлено.');
  });

  it('F-19 reports a file name with angle brackets and a CR in its stored form (image-alt-angles)', async () => {
    const { images } = host();
    const mounted = await mountEditor('<p>до</p>', { images });

    choose(file('a <b>\rc.png', 'image/png'));

    await waitFor(() => expect(mounted.editor().getHTML()).toContain(`data-image-id="${ID}"`));
    mounted.flush();
    const html = mounted.changes.at(-1)?.html ?? '';
    expect(html).toContain('alt="a b\nc"');
    expect(html).not.toMatch(/alt="[^"]*[<>\r]/);
  });

  it('names a clipboard image «Изображение» and marks the description changed', async () => {
    const { images } = host();
    const mounted = await mountEditor('<p>до</p>', { images });

    pasteInto(mounted.body, { files: [file('image.png', 'image/png')] as never });

    await waitFor(() => expect(mounted.editor().getHTML()).toContain('alt="Изображение"'));
    mounted.flush();
    expect(mounted.changes.at(-1)?.html).toContain(`data-image-id="${ID}"`);
  });
});

describe('F-4 where an insert lands (BR-IM-13)', () => {
  it.each([
    [
      'a heading',
      '<h3>Заголовок</h3><p>после</p>',
      'Заголовок',
      ['heading:Заголовок', `img:${ID}`, 'paragraph:после'],
    ],
    [
      'a code block',
      '<pre><code>код</code></pre><p>после</p>',
      'код',
      ['codeBlock:код', `img:${ID}`, 'paragraph:после'],
    ],
    ['a paragraph (split)', '<p>аб</p>', 'аб', ['paragraph:а', `img:${ID}`, 'paragraph:б']],
  ])('in %s', async (_place, html, text, expected) => {
    const { images } = host();
    const { editor } = await mountEditor(html, { images });
    caret(editor(), positionOf(editor(), text) + 1);

    choose(file('a.png', 'image/png'));

    await waitFor(() => expect(outline(editor())).toEqual(expected));
  });

  it('in a table cell: right after the table', async () => {
    const { images } = host();
    const { editor } = await mountEditor(
      '<table><tbody><tr><td><p>ячейка</p></td></tr></tbody></table><p>после</p>',
      { images },
    );
    caret(editor(), positionOf(editor(), 'ячейка') + 2);

    choose(file('a.png', 'image/png'));

    await waitFor(() =>
      expect(outline(editor())).toEqual(['table', `img:${ID}`, 'paragraph:после']),
    );
  });

  it('in a list item: a block of its own in the item', async () => {
    const { images } = host();
    const { editor } = await mountEditor('<ul><li><p>пункт</p></li></ul>', { images });
    caret(editor(), positionOf(editor(), 'пункт') + 5);

    choose(file('a.png', 'image/png'));

    await waitFor(() =>
      expect(editor().getHTML()).toContain(`<li><p>пункт</p><img data-image-id="${ID}"`),
    );
  });
});

describe('F-5, F-7 drop', () => {
  it('inserts at the drop position, not at the caret, and keeps the browser where it is', async () => {
    const { images } = host();
    const { body, editor } = await mountEditor('<p>один</p><p>два</p>', { images });
    caret(editor(), 1);
    // jsdom has no layout: the drop position is what the view answers for the pointer.
    const end = positionOf(editor(), 'один') + 4;
    editor().view.posAtCoords = () => ({ pos: end, inside: -1 });

    const event = drop(body, [file('a.png', 'image/png')]);

    expect(event.defaultPrevented).toBe(true);
    await waitFor(() =>
      expect(outline(editor())).toEqual(['paragraph:один', `img:${ID}`, 'paragraph:два']),
    );
  });

  it('of a PNG, a PDF and a WebP inserts the two images and tells the format once', async () => {
    const { images } = host();
    const { body, editor } = await mountEditor('<p>до</p>', { images });
    editor().view.posAtCoords = () => null;
    const said = recordNotices();

    drop(body, [
      file('a.png', 'image/png'),
      file('b.pdf', 'application/pdf'),
      file('c.webp', 'image/webp'),
    ]);

    await waitFor(() => expect(images.upload).toHaveBeenCalledTimes(2));
    const format = 'Можно вставить только изображения PNG, JPEG, GIF или WebP.';
    await waitFor(() => expect(said).toContain(format));
    expect(said.filter((text) => text === format)).toHaveLength(1);
  });

  // D-11: «Изображение вставлено.» of the valid files replaces the refusal of the same action in
  // the one notice line, so a sighted member never reads why the PDF is missing.
  it.fails('keeps the refusal readable after the valid files of the same drop go in', async () => {
    const { images } = host();
    const { body, editor } = await mountEditor('<p>до</p>', { images });
    editor().view.posAtCoords = () => null;

    drop(body, [file('a.png', 'image/png'), file('b.pdf', 'application/pdf')]);

    await waitFor(() => expect(editor().getHTML()).toContain(`data-image-id="${ID}"`));
    await act(async () => new Promise((resolve) => setTimeout(resolve, 20)));
    expect(notice()).toContain('Можно вставить только изображения PNG, JPEG, GIF или WebP.');
  });
});

describe('F-6 the client checks at the boundaries', () => {
  it.each([
    [5_242_880, 'upload'],
    [5_242_881, 'offer'],
    [20_971_520, 'offer'],
  ] as const)('a %d-byte still image: %s', async (size, outcome) => {
    const { images } = host();
    await mountEditor('<p>до</p>', { images });

    choose(file('a.jpg', 'image/jpeg', size));

    if (outcome === 'upload') {
      await waitFor(() => expect(images.upload).toHaveBeenCalledTimes(1));
      expect(screen.queryByRole('dialog')).toBeNull();
    } else {
      expect(await screen.findByRole('dialog', { name: 'Изображение больше 5 МБ' })).toBeVisible();
      expect(images.upload).not.toHaveBeenCalled();
    }
  });

  it.each([
    [
      'a 20 971 521-byte JPEG',
      file('a.jpg', 'image/jpeg', 20_971_521),
      'Файл больше 20 МБ. Уменьшите изображение в другой программе и вставьте снова.',
    ],
    [
      'an SVG',
      file('a.svg', 'image/svg+xml'),
      'Можно вставить только изображения PNG, JPEG, GIF или WebP.',
    ],
    [
      'a 6 MB animated GIF',
      file(
        'a.gif',
        'image/gif',
        6_000_000,
        new Uint8Array([0x47, 0x49, 0x46, 0x21, 0xf9, 0x04, 0, 0x21, 0xf9, 0x04, 0, 0]),
      ),
      'Анимация больше 5 МБ. Уменьшите её в другой программе и вставьте снова.',
    ],
  ])('%s is refused before any request', async (_what, refused, text) => {
    const { images } = host();
    const { editor } = await mountEditor('<p>до</p>', { images });

    choose(refused);

    await waitFor(() => expect(notice()).toContain(text));
    expect(images.upload).not.toHaveBeenCalled();
    expect(images.reduce).not.toHaveBeenCalled();
    expect(screen.queryByRole('dialog')).toBeNull();
    expect(editor().getHTML()).toBe('<p>до</p>');
  });
});

describe('F-32 the 50-image count at the insert', () => {
  const fifty = (count: number) => Array.from({ length: count }, () => stored()).join('');
  const text = 'В описании может быть не больше 50 изображений.';

  it('refuses a 51st image and opens no offer for a 7 MB one', async () => {
    const { images } = host();
    await mountEditor(fifty(50), { images });

    choose(file('a.png', 'image/png'), file('big.jpg', 'image/jpeg', 7_000_000));

    await waitFor(() => expect(notice()).toContain(text));
    expect(images.upload).not.toHaveBeenCalled();
    expect(screen.queryByRole('dialog')).toBeNull();
  });

  it('counts the placeholder of the same insert', async () => {
    const { images } = host({ upload: vi.fn(() => new Promise<StoredImage>(() => {})) });
    await mountEditor(fifty(49), { images });

    choose(file('a.png', 'image/png'), file('b.png', 'image/png'));

    await waitFor(() => expect(notice()).toContain(text));
    expect(images.upload).toHaveBeenCalledTimes(1);
  });
});

describe('F-8 failures of the upload', () => {
  it('a network failure removes the placeholder and says «Не удалось загрузить…»', async () => {
    const { images } = host({
      upload: vi.fn(async () => {
        throw new TypeError('Failed to fetch');
      }),
    });
    const { editor } = await mountEditor('<p>до</p>', { images });

    choose(file('a.png', 'image/png'));

    await waitFor(() =>
      expect(notice()).toContain('Не удалось загрузить изображение. Попробуйте ещё раз.'),
    );
    expect(editor().getHTML()).toBe('<p>до</p>');
  });
});

describe('F-15 a placeholder removed cancels its upload (design D-5)', () => {
  it('aborts the request and ignores its late answer', async () => {
    let signal: AbortSignal | null = null;
    let finish: (value: StoredImage) => void = () => {};
    const { images } = host({
      upload: vi.fn((_file: File, sent: AbortSignal) => {
        signal = sent;
        return new Promise<StoredImage>((resolve) => (finish = resolve));
      }),
    });
    const { body, editor } = await mountEditor('<p>до</p>', { images });
    choose(file('a.png', 'image/png'));
    await waitFor(() => expect(signal).not.toBeNull());

    selectNode(editor(), imagePos(editor()));
    fireEvent.keyDown(body, { key: 'Delete' });

    await waitFor(() => expect(signal!.aborted).toBe(true));
    await act(async () => finish(image(ID)));
    expect(editor().getHTML()).not.toContain('data-image-id');
    expect(editor().view.dom.querySelector('[data-rt-image-placeholder]')).toBeNull();
  });
});

describe('F-10, F-11 pasted rich content', () => {
  function stubFetch() {
    const fetched = vi.fn(async (_url: string) => ({
      blob: async () => new Blob(['x'], { type: 'image/png' }),
    }));
    vi.stubGlobal('fetch', fetched);
    return fetched;
  }

  it('drops a foreign image with its notice and no request, uploads a data image, keeps the text', async () => {
    const fetched = stubFetch();
    const { images } = host();
    const { body, editor } = await mountEditor('<p></p>', { images });
    const said = recordNotices();

    pasteInto(body, {
      text: 'текст',
      html: '<p>текст</p><img src="https://other.example/a.png"><img src="data:image/png;base64,iVBORw0KGgo=" alt="данные">',
    });

    await waitFor(() => expect(images.upload).toHaveBeenCalledTimes(1));
    await waitFor(() =>
      expect(said).toContain(
        'Изображение с другого сайта не вставлено. Сохраните его как файл и вставьте снова.',
      ),
    );
    expect(fetched.mock.calls.map(([url]) => url)).not.toContain('https://other.example/a.png');
    await waitFor(() => expect(editor().getHTML()).toContain(`data-image-id="${ID}"`));
    expect(editor().getHTML()).toContain('текст');
  });

  it('uploads again an image of the store copied from another item', async () => {
    stubFetch();
    const other = ids[0]!;
    const { images } = host();
    const { body, editor } = await mountEditor('<p></p>', { images });

    pasteInto(body, { text: '', html: stored(other, 'чужое') });

    await waitFor(() => expect(images.upload).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(editor().getHTML()).toContain(`data-image-id="${ID}"`));
    expect(editor().getHTML()).not.toContain(other);
  });

  it('leaves out an image of the store the member cannot read, with the upload-failed text', async () => {
    stubFetch();
    const { images } = host({
      address: vi.fn(async () => {
        throw new Error('404');
      }),
    });
    const { body, editor } = await mountEditor('<p></p>', { images });

    pasteInto(body, { text: 'слово', html: `<p>слово</p>${stored(ids[0])}` });

    await waitFor(() =>
      expect(notice()).toContain('Не удалось загрузить изображение. Попробуйте ещё раз.'),
    );
    expect(images.upload).not.toHaveBeenCalled();
    expect(editor().getHTML()).not.toContain('data-image-id');
    expect(editor().getHTML()).toContain('слово');
  });

  // D-6: after a cut the document no longer holds the reference, so the paste uploads a copy.
  it.fails('moves an image by cut and paste without a new upload (US-1)', async () => {
    stubFetch();
    const { images } = host();
    const { body, editor } = await mountEditor(`<p>до</p>${stored()}<p>после</p>`, { images });
    await waitFor(() => expect(body.querySelector('img')).not.toBeNull());

    selectNode(editor(), imagePos(editor()));
    // The cut: the selection leaves the document, the clipboard keeps its html.
    act(() => {
      editor().view.dispatch(editor().state.tr.deleteSelection());
    });
    caret(editor(), positionOf(editor(), 'после') + 5);
    pasteInto(body, { text: '', html: stored() });

    await waitFor(() => expect(editor().getHTML()).toContain(`data-image-id="${ID}"`));
    expect(images.upload).not.toHaveBeenCalled();
  });
});

describe('F-12, F-25 removal, undo and the image menu', () => {
  async function selected(html = `<p>до</p>${stored()}`) {
    const { images } = host();
    const mounted = await mountEditor(html, { images });
    await waitFor(() => expect(mounted.body.querySelector('img')).not.toBeNull());
    selectNode(mounted.editor(), imagePos(mounted.editor()));
    return mounted;
  }

  it('Backspace removes and undo restores', async () => {
    const { body, editor } = await selected();

    fireEvent.keyDown(body, { key: 'Backspace' });
    await waitFor(() => expect(editor().getHTML()).not.toContain('data-image-id'));

    act(() => {
      editor().commands.undo();
    });
    expect(editor().getHTML()).toContain(`data-image-id="${ID}"`);
  });

  it('Shift+F10 opens «Меню изображения» with its two items; «Удалить изображение» removes', async () => {
    const user = userEvent.setup();
    const { body, editor } = await selected();

    fireEvent.keyDown(body, { key: 'F10', shiftKey: true });

    const menu = await screen.findByRole('menu', { name: 'Меню изображения' });
    expect(
      within(menu)
        .getAllByRole('menuitem')
        .map((item) => item.textContent),
    ).toEqual(['Открыть в полном размере', 'Удалить изображение']);
    await user.click(within(menu).getByRole('menuitem', { name: 'Удалить изображение' }));

    await waitFor(() => expect(editor().getHTML()).not.toContain('data-image-id'));
    expect(notice()).toContain('Изображение удалено.');
  });

  it('the ContextMenu key opens the menu; «Открыть в полном размере» opens the view', async () => {
    const user = userEvent.setup();
    const { body } = await selected();

    fireEvent.keyDown(body, { key: 'ContextMenu' });
    const menu = await screen.findByRole('menu', { name: 'Меню изображения' });
    await user.click(within(menu).getByRole('menuitem', { name: 'Открыть в полном размере' }));

    expect(await screen.findByRole('dialog', { name: 'Просмотр изображения' })).toBeVisible();
  });

  it('Esc closes the menu and leaves the image selected in the focused editor', async () => {
    const user = userEvent.setup();
    const { body, editor } = await selected();
    const at = imagePos(editor());

    fireEvent.keyDown(body, { key: 'F10', shiftKey: true });
    await screen.findByRole('menu', { name: 'Меню изображения' });
    await user.keyboard('{Escape}');

    await waitFor(() => expect(screen.queryByRole('menu')).toBeNull());
    await waitFor(() => {
      const { selection } = editor().state;
      expect(selection instanceof NodeSelection && selection.from === at).toBe(true);
      expect(document.activeElement).toBe(body);
    });
  });

  it('Space opens the view; typing a letter leaves the image (D-3)', async () => {
    const { body, editor } = await selected();

    fireEvent.keyDown(body, { key: 'x' });
    expect(editor().getHTML()).toContain('data-image-id');
    fireEvent.keyDown(body, { key: ' ' });

    expect(await screen.findByRole('dialog', { name: 'Просмотр изображения' })).toBeVisible();
  });
});

describe('F-24 the full-size view', () => {
  it('names its scroll area, and «Закрыть» returns focus to the image, selected', async () => {
    const user = userEvent.setup();
    const { images } = host();
    const { body, editor } = await mountEditor(`<p>до</p>${stored()}`, { images });
    await waitFor(() => expect(body.querySelector('img')).not.toBeNull());
    const at = imagePos(editor());
    selectNode(editor(), at);

    fireEvent.keyDown(body, { key: 'Enter' });
    const dialog = await screen.findByRole('dialog', { name: 'Просмотр изображения' });
    const area = within(dialog).getByRole('region', { name: 'Изображение, полный размер' });
    expect(area).toHaveAttribute('tabindex', '0');
    expect(within(dialog).getByText('схема')).toBeInTheDocument();
    await user.click(within(dialog).getAllByRole('button', { name: 'Закрыть' }).at(-1)!);

    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    await waitFor(() => {
      const { selection } = editor().state;
      expect(selection instanceof NodeSelection && selection.from === at).toBe(true);
      expect(document.activeElement).toBe(body);
    });
  });

  it('read-only: Esc returns focus to the image button it was opened from', async () => {
    const user = userEvent.setup();
    const { images } = host({ canInsert: false });
    await mountEditor(stored(ID, 'схема стенда'), { images, disabled: true });
    const open = await screen.findByRole('button', {
      name: 'Открыть в полном размере: схема стенда',
    });

    act(() => open.focus());
    await user.keyboard('{Enter}');
    await screen.findByRole('dialog', { name: 'Просмотр изображения' });
    await user.keyboard('{Escape}');

    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    // The editor's own return runs a tick later: it must not take the focus away from the image.
    await act(async () => new Promise((resolve) => setTimeout(resolve, 20)));
    expect(document.activeElement).toBe(open);
  });

  it('says «Изображение недоступно» inside the view when the read fails', async () => {
    const { images } = host({
      address: vi.fn(async () => {
        throw new Error('404');
      }),
    });
    const { body, editor } = await mountEditor(`<p>до</p>${stored()}`, { images });
    await screen.findAllByRole('img', { name: 'Изображение недоступно' });
    selectNode(editor(), imagePos(editor()));

    fireEvent.keyDown(body, { key: 'Enter' });

    const dialog = await screen.findByRole('dialog', { name: 'Просмотр изображения' });
    expect(within(dialog).getByText('Изображение недоступно')).toBeInTheDocument();
  });
});

describe('F-14 a viewer without the right to insert (canInsert: false)', () => {
  it('ignores a pasted and a dropped image file, and the browser does not navigate', async () => {
    const { images } = host({ canInsert: false });
    const { body, editor, toolbar } = await mountEditor('<p>до</p>', { images });
    editor().view.posAtCoords = () => null;

    const pasted = pasteInto(body, { files: [file('a.png', 'image/png')] as never });
    const dropped = drop(body, [file('b.png', 'image/png')]);

    expect(pasted.defaultPrevented).toBe(true);
    expect(dropped.defaultPrevented).toBe(true);
    expect(within(toolbar).queryByRole('button', { name: 'Изображение' })).toBeNull();
    await act(async () => new Promise((resolve) => setTimeout(resolve, 20)));
    expect(images.upload).not.toHaveBeenCalled();
    expect(editor().getHTML()).toBe('<p>до</p>');
  });
});

describe('F-9 the helper while a save waits', () => {
  it('reads «Дожидаемся загрузки изображений…» as a status, then the helper again', async () => {
    let finish: (value: StoredImage) => void = () => {};
    const { images } = host({
      upload: vi.fn(() => new Promise<StoredImage>((resolve) => (finish = resolve))),
    });
    const handle: { current: { settle(): Promise<void> | null } | null } = { current: null };
    await mountEditor('<p>до</p>', {
      images,
      helperText: 'До 100 000 символов',
      ref: (value) => {
        handle.current = value;
      },
    });
    choose(file('a.png', 'image/png'));
    await waitFor(() => expect(images.upload).toHaveBeenCalled());

    let waiting: Promise<void> | null = null;
    act(() => {
      waiting = handle.current?.settle() ?? null;
    });
    expect(screen.getByText('Дожидаемся загрузки изображений…')).toHaveAttribute('role', 'status');

    await act(async () => {
      finish(image(ID));
      await waiting;
    });
    expect(screen.queryByText('Дожидаемся загрузки изображений…')).toBeNull();
    expect(screen.getByText('До 100 000 символов')).toBeInTheDocument();
  });
});

describe('F-35 addresses are released', () => {
  it('releases an address that arrives after the editor is gone', async () => {
    let answer: (url: string) => void = () => {};
    const { images, released } = host({
      address: vi.fn(() => new Promise<string>((resolve) => (answer = resolve))),
    });
    await mountEditor(stored(), { images });
    await waitFor(() => expect(images.address).toHaveBeenCalled());

    cleanup();
    await act(async () => answer('blob:late'));

    expect(released).toContain('blob:late');
  });
});

describe('US-6 the reduction dialog', () => {
  async function offered(over: Partial<RichTextImages> = {}, ...files: File[]) {
    const { images } = host(over);
    const mounted = await mountEditor('<p>до</p>', { images });
    caret(mounted.editor(), positionOf(mounted.editor(), 'до') + 2);
    choose(...(files.length > 0 ? files : [file('семь.jpg', 'image/jpeg', 7_759_462)]));
    const dialog = await screen.findByRole('dialog', { name: 'Изображение больше 5 МБ' });
    return { ...mounted, images, dialog };
  }

  it('F-27 says the size in МБ with a comma and focuses «Уменьшить»', async () => {
    const { dialog } = await offered();

    expect(dialog).toHaveTextContent('Размер файла — 7,4 МБ, а в описание можно вставить');
    expect(within(dialog).getByText('семь.jpg')).toBeInTheDocument();
  });

  it('F-27 a pasted file over 5 MB: initial focus on «Уменьшить»', async () => {
    const { images } = host();
    const { body } = await mountEditor('<p>до</p>', { images });

    pasteInto(body, { files: [file('семь.jpg', 'image/jpeg', 7_759_462)] as never });

    const dialog = await screen.findByRole('dialog', { name: 'Изображение больше 5 МБ' });
    await waitFor(() =>
      expect(within(dialog).getByRole('button', { name: 'Уменьшить' })).toHaveFocus(),
    );
  });

  // D-10: the chooser path calls the editor's delayed focus, which lands after the dialog opened;
  // the focus trap then holds the dialog frame, not «Уменьшить» (the paste path above is right).
  it.fails('F-27 a chosen file over 5 MB: initial focus on «Уменьшить»', async () => {
    const { dialog } = await offered();

    await act(async () => new Promise((resolve) => setTimeout(resolve, 100)));
    await waitFor(() =>
      expect(within(dialog).getByRole('button', { name: 'Уменьшить' })).toHaveFocus(),
    );
  });

  it('F-28 «Отмена» while reducing aborts the request and ignores a late answer', async () => {
    const user = userEvent.setup();
    let signal: AbortSignal | null = null;
    let finish: (value: ImageReductionResult) => void = () => {};
    const { dialog, editor } = await offered({
      reduce: vi.fn((_file: File, sent: AbortSignal) => {
        signal = sent;
        return new Promise<ImageReductionResult>((resolve) => (finish = resolve));
      }),
    });

    await user.click(within(dialog).getByRole('button', { name: 'Уменьшить' }));
    expect(within(dialog).getByRole('status')).toHaveTextContent('Уменьшаем изображение…');
    await user.click(within(dialog).getByRole('button', { name: 'Отмена' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    expect(signal!.aborted).toBe(true);
    await act(async () =>
      finish({
        image: image(ids[1]!),
        original: { format: 'jpeg', byteLength: 7_759_462, width: 1, height: 1 },
      }),
    );
    expect(editor().getHTML()).not.toContain('data-image-id');
  });

  it('F-29 shows «Было» and «Стало» as the dialog description, the preview, and «Вставить» focused', async () => {
    const user = userEvent.setup();
    const { dialog } = await offered();

    await user.click(within(dialog).getByRole('button', { name: 'Уменьшить' }));

    await within(dialog).findByText('Стало: 3,8 МБ, 40 × 30');
    expect(dialog).toHaveAccessibleDescription(/Было: 7,4 МБ, 8160 × 6120/);
    expect(
      await within(dialog).findByRole('img', { name: 'Уменьшенное изображение' }),
    ).toHaveAttribute('src', `blob:test/${ids[1]}`);
    await waitFor(() =>
      expect(within(dialog).getByRole('button', { name: 'Вставить' })).toHaveFocus(),
    );
  });

  it('F-29 a preview that cannot be read makes «Вставить» aria-disabled and inert', async () => {
    const user = userEvent.setup();
    const { dialog, editor } = await offered({
      address: vi.fn(async () => {
        throw new Error('404');
      }),
    });

    await user.click(within(dialog).getByRole('button', { name: 'Уменьшить' }));
    await within(dialog).findByText('Изображение недоступно');
    const insert = within(dialog).getByRole('button', { name: 'Вставить' });
    expect(insert).toHaveAttribute('aria-disabled', 'true');
    await user.click(insert);

    expect(screen.getByRole('dialog')).toBeVisible();
    expect(editor().getHTML()).not.toContain('data-image-id');
  });

  it('F-29 Esc at the result inserts nothing', async () => {
    const user = userEvent.setup();
    const { dialog, editor } = await offered();
    await user.click(within(dialog).getByRole('button', { name: 'Уменьшить' }));
    await within(dialog).findByText(/Стало:/);

    await user.keyboard('{Escape}');

    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    expect(editor().getHTML()).not.toContain('data-image-id');
  });

  it('F-30 cannot reduce: the warning and «Закрыть»', async () => {
    const user = userEvent.setup();
    const cannot =
      'Не удалось уменьшить изображение до 5 МБ без сильной потери качества. Уменьшите его в другой программе и вставьте снова.';
    const { dialog, editor } = await offered({
      reduce: vi.fn(async () => {
        throw new ImageRefusal(cannot, CANNOT_REDUCE_CODE);
      }),
    });

    await user.click(within(dialog).getByRole('button', { name: 'Уменьшить' }));

    expect(await within(dialog).findByText(cannot)).toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', { name: 'Закрыть' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    expect(editor().getHTML()).not.toContain('data-image-id');
  });

  it('F-30 a failed reduction says so as an alert and «Повторить» sends the file again', async () => {
    const user = userEvent.setup();
    const reduce = vi
      .fn()
      .mockRejectedValueOnce(new TypeError('Failed to fetch'))
      .mockResolvedValueOnce({
        image: image(ids[1]!, 4_000_000),
        original: { format: 'jpeg', byteLength: 7_759_462, width: 8160, height: 6120 },
      });
    const { dialog } = await offered({ reduce });

    await user.click(within(dialog).getByRole('button', { name: 'Уменьшить' }));
    expect(await within(dialog).findByRole('alert')).toHaveTextContent(
      'Не удалось уменьшить изображение. Попробуйте ещё раз.',
    );
    await user.click(within(dialog).getByRole('button', { name: 'Повторить' }));

    await within(dialog).findByText(/Стало:/);
    expect(reduce).toHaveBeenCalledTimes(2);
  });

  // D-7: the second slot of the queue is mapped to the left of the first confirmed image.
  it.fails(
    'F-31 a queue: the small file goes in at once, the offers one after another, the chosen order kept',
    async () => {
      const user = userEvent.setup();
      const { dialog, editor, images } = await offered(
        { upload: vi.fn(async () => image(ids[0]!)) },
        file('малый.png', 'image/png', 2_000_000),
        file('семь.jpg', 'image/jpeg', 7_000_000),
        file('восемь.jpg', 'image/jpeg', 8_000_000),
      );

      await waitFor(() => expect(images.upload).toHaveBeenCalledTimes(1));
      expect(within(dialog).getByText('Файл 1 из 2 · семь.jpg')).toBeInTheDocument();
      await user.click(within(dialog).getByRole('button', { name: 'Уменьшить' }));
      await user.click(await within(dialog).findByRole('button', { name: 'Вставить' }));

      const second = await screen.findByText('Файл 2 из 2 · восемь.jpg');
      const next = second.closest('[role="dialog"]') as HTMLElement;
      await user.click(within(next).getByRole('button', { name: 'Уменьшить' }));
      await user.click(await within(next).findByRole('button', { name: 'Вставить' }));

      await waitFor(() =>
        expect(outline(editor())).toEqual([
          'paragraph:до',
          `img:${ids[0]}`,
          `img:${ids[1]}`,
          `img:${ids[2]}`,
        ]),
      );
    },
  );

  it('F-31 a cancelled offer leaves no gap and the next opens', async () => {
    const user = userEvent.setup();
    const { dialog, editor } = await offered(
      {},
      file('семь.jpg', 'image/jpeg', 7_000_000),
      file('восемь.jpg', 'image/jpeg', 8_000_000),
    );

    await user.click(within(dialog).getByRole('button', { name: 'Отмена' }));
    const second = await screen.findByText('Файл 2 из 2 · восемь.jpg');
    const next = second.closest('[role="dialog"]') as HTMLElement;
    await user.click(within(next).getByRole('button', { name: 'Уменьшить' }));
    await user.click(await within(next).findByRole('button', { name: 'Вставить' }));

    await waitFor(() => expect(outline(editor())).toEqual(['paragraph:до', `img:${ids[2]}`]));
  });
});

describe('F-34 sizes', () => {
  it('are binary megabytes, as the 5 MB = 5 242 880 bytes cap', () => {
    expect(formatSize(7_759_462)).toBe('7,4 МБ');
    expect(formatSize(5_242_880)).toBe('5,0 МБ');
    expect(formatSize(7_400_000)).toBe('7,1 МБ');
  });
});
