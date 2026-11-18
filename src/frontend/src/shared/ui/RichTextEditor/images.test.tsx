import { act, cleanup, fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { NodeSelection } from '@tiptap/pm/state';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { mountEditor, pasteInto } from '../../../test/richTextEditor';
import { ImageRefusal, type RichTextImages, type StoredImage } from './images/host';

/**
 * feat-078, the editor's half: US-1 (insert by button and paste, placeholders, refusals, host
 * absent), US-4 (stored images, unavailable, read-only, full size, addresses released), US-6 (the
 * reduction dialog), design § Keyboard and screen reader.
 */

const ID = '3f2b8c1e-9a4d-4e6b-8f21-0a1b2c3d4e5f';
const STORED: StoredImage = { id: ID, format: 'png', byteLength: 1000, width: 40, height: 30 };

function file(name: string, type: string, size = 100): File {
  const made = new File(['x'], name, { type });
  Object.defineProperty(made, 'size', { value: size });
  // jsdom has no Blob.arrayBuffer; the editor sniffs the header with it.
  Object.defineProperty(made, 'arrayBuffer', { value: async () => new ArrayBuffer(8) });
  return made;
}

function host(over: Partial<RichTextImages> = {}) {
  const released: string[] = [];
  const images: RichTextImages = {
    upload: vi.fn(async () => STORED),
    reduce: vi.fn(async () => ({
      image: { ...STORED, byteLength: 4_000_000, width: 2000, height: 1500 },
      original: { format: 'png' as const, byteLength: 7_400_000, width: 4000, height: 3000 },
    })),
    address: vi.fn(async (reference: string) => `blob:test/${reference}`),
    release: (address) => released.push(address),
    ...over,
  };
  return { images, released };
}

const stored = (alt = 'схема') => `<img data-image-id="${ID}" alt="${alt}">`;

afterEach(() => cleanup());

describe('without a host (US-1, D-8)', () => {
  it('has no «Изображение» control', async () => {
    const { toolbar } = await mountEditor('<p>до</p>');

    expect(within(toolbar).queryByRole('button', { name: 'Изображение' })).toBeNull();
  });
});

describe('insert by the file chooser (US-1)', () => {
  it('uploads the file and inserts the stored image with the file name as its alternative', async () => {
    const { images } = host();
    const { editor, button } = await mountEditor('<p>до</p>', { images });

    expect(button('Изображение')).toBeInTheDocument();
    fireEvent.change(screen.getByTestId('rt-image-input'), {
      target: { files: [file('схема.png', 'image/png')] },
    });

    await waitFor(() => expect(editor().getHTML()).toContain(`data-image-id="${ID}"`));
    expect(images.upload).toHaveBeenCalledTimes(1);
    expect(editor().getHTML()).not.toContain('src=');
    expect(editor().getHTML()).toContain('alt="схема"');
  });

  it('says why a file that is not an image is not inserted, and uploads nothing', async () => {
    const { images } = host();
    const { editor } = await mountEditor('<p>до</p>', { images });

    fireEvent.change(screen.getByTestId('rt-image-input'), {
      target: { files: [file('a.txt', 'text/plain')] },
    });

    await waitFor(() =>
      expect(screen.getByRole('status')).toHaveTextContent(
        'Можно вставить только изображения PNG, JPEG, GIF или WebP.',
      ),
    );
    expect(images.upload).not.toHaveBeenCalled();
    expect(editor().getHTML()).toBe('<p>до</p>');
  });

  it('tells the server reason when the upload is refused, and leaves no placeholder', async () => {
    const { images } = host({
      upload: vi.fn(async () => {
        throw new ImageRefusal('Файл не похож на изображение.', 'images.upload.not_an_image');
      }),
    });
    const { editor } = await mountEditor('<p>до</p>', { images });

    fireEvent.change(screen.getByTestId('rt-image-input'), {
      target: { files: [file('a.png', 'image/png')] },
    });

    await waitFor(() =>
      expect(screen.getByRole('status')).toHaveTextContent('Файл не похож на изображение.'),
    );
    expect(editor().getHTML()).toBe('<p>до</p>');
    expect(editor().view.dom.querySelector('[data-rt-image-placeholder]')).toBeNull();
  });

  it('inserts a pasted image file through the host', async () => {
    const { images } = host();
    const { body, editor } = await mountEditor('<p>до</p>', { images });

    const event = pasteInto(body, { files: [{ name: 'p.png', type: 'image/png' }] });

    expect(event.defaultPrevented).toBe(true);
    await waitFor(() => expect(images.upload).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(editor().getHTML()).toContain(`data-image-id="${ID}"`));
  });
});

describe('stored images (US-4)', () => {
  it('draws a stored image from the address the host derives, and releases it on unmount', async () => {
    const { images, released } = host();
    const { body } = await mountEditor(stored(), { images });

    const img = await waitFor(() => {
      const found = body.querySelector('img');
      expect(found).not.toBeNull();
      return found as HTMLImageElement;
    });
    expect(img).toHaveAttribute('src', `blob:test/${ID}`);
    expect(img).toHaveAttribute('alt', 'схема');

    cleanup();
    expect(released).toContain(`blob:test/${ID}`);
  });

  it('says «Изображение недоступно» when the host cannot read it', async () => {
    const { images } = host({
      address: vi.fn(async () => {
        throw new Error('404');
      }),
    });
    await mountEditor(stored(), { images });

    expect(await screen.findByText('Изображение недоступно')).toBeInTheDocument();
  });

  it('is one tab stop in the read-only view, named by its alternative, and opens full size', async () => {
    const user = userEvent.setup();
    const { images } = host();
    await mountEditor(stored('схема стенда'), { images, disabled: true });

    const open = await screen.findByRole('button', {
      name: 'Открыть в полном размере: схема стенда',
    });
    await user.click(open);

    const dialog = await screen.findByRole('dialog', { name: 'Просмотр изображения' });
    expect(within(dialog).getByRole('img', { name: 'схема стенда' })).toBeInTheDocument();
    await user.click(within(dialog).getAllByRole('button', { name: 'Закрыть' })[0]!);
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
  });

  it('shows no insert control in the read-only view', async () => {
    const { images } = host({ canInsert: false });
    const { toolbar } = await mountEditor(stored(), { images, disabled: true });

    expect(within(toolbar).queryByRole('button', { name: 'Изображение' })).toBeNull();
  });
});

describe('keyboard (design § Keyboard and screen reader)', () => {
  async function selected() {
    const { images } = host();
    const mounted = await mountEditor(`<p>до</p>${stored()}`, { images });
    await waitFor(() => expect(mounted.body.querySelector('img')).not.toBeNull());
    const editor = mounted.editor();
    let at = -1;
    editor.state.doc.descendants((node, pos) => {
      if (node.type.name === 'rtImage') at = pos;
    });
    act(() => {
      editor.view.dispatch(
        editor.state.tr.setSelection(NodeSelection.create(editor.state.doc, at)),
      );
    });
    return mounted;
  }

  it('opens the full-size view on Enter', async () => {
    const { body } = await selected();

    fireEvent.keyDown(body, { key: 'Enter' });

    expect(await screen.findByRole('dialog', { name: 'Просмотр изображения' })).toBeInTheDocument();
  });

  it('removes the image on Delete and says so', async () => {
    const { body, editor } = await selected();

    fireEvent.keyDown(body, { key: 'Delete' });

    await waitFor(() => expect(editor().getHTML()).not.toContain('data-image-id'));
    expect(screen.getByRole('status')).toHaveTextContent('Изображение удалено.');
  });

  it('does not replace the image by typing', async () => {
    const { body, editor } = await selected();

    fireEvent.keyDown(body, { key: 'a' });

    expect(editor().getHTML()).toContain('data-image-id');
  });
});

describe('a file over 5 MB (US-6)', () => {
  it('offers the reduction, shows before and after, and inserts the result on «Вставить»', async () => {
    const user = userEvent.setup();
    const { images } = host();
    const { editor } = await mountEditor('<p>до</p>', { images });

    fireEvent.change(screen.getByTestId('rt-image-input'), {
      target: { files: [file('big.png', 'image/png', 7_400_000)] },
    });

    const dialog = await screen.findByRole('dialog', { name: 'Изображение больше 5 МБ' });
    expect(images.upload).not.toHaveBeenCalled();
    await user.click(within(dialog).getByRole('button', { name: 'Уменьшить' }));

    await within(dialog).findByText(/Было: 7,1 МБ, 4000 × 3000/);
    expect(images.reduce).toHaveBeenCalledTimes(1);
    await user.click(within(dialog).getByRole('button', { name: 'Вставить' }));

    await waitFor(() => expect(editor().getHTML()).toContain(`data-image-id="${ID}"`));
  });

  it('inserts nothing on «Отмена»', async () => {
    const user = userEvent.setup();
    const { images } = host();
    const { editor } = await mountEditor('<p>до</p>', { images });

    fireEvent.change(screen.getByTestId('rt-image-input'), {
      target: { files: [file('big.png', 'image/png', 7_400_000)] },
    });
    const dialog = await screen.findByRole('dialog', { name: 'Изображение больше 5 МБ' });
    await user.click(within(dialog).getByRole('button', { name: 'Отмена' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    expect(images.reduce).not.toHaveBeenCalled();
    expect(editor().getHTML()).toBe('<p>до</p>');
  });
});

describe('saving waits for uploads (BR-IM-14)', () => {
  it('settles after the upload that was running', async () => {
    let finish: (image: StoredImage) => void = () => {};
    const { images } = host({
      upload: vi.fn(() => new Promise<StoredImage>((resolve) => (finish = resolve))),
    });
    const handle: { current: { settle(): Promise<void> | null } | null } = { current: null };
    const { editor } = await mountEditor('<p>до</p>', {
      images,
      ref: (value) => {
        handle.current = value;
      },
    });

    fireEvent.change(screen.getByTestId('rt-image-input'), {
      target: { files: [file('a.png', 'image/png')] },
    });
    await waitFor(() => expect(images.upload).toHaveBeenCalled());
    const waiting = handle.current?.settle() ?? null;
    expect(waiting).not.toBeNull();

    await act(async () => finish(STORED));
    await waiting;

    expect(editor().getHTML()).toContain(`data-image-id="${ID}"`);
    expect(handle.current?.settle()).toBeNull();
  });
});
