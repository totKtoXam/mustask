import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { RichTextEditor } from './RichTextEditor';
import { loadEditorImpl } from './loadEditorImpl';

vi.mock('./loadEditorImpl', () => ({ loadEditorImpl: vi.fn() }));

const loadReal = () => import('./EditorImpl');

beforeEach(() => {
  vi.mocked(loadEditorImpl).mockReset();
  vi.mocked(loadEditorImpl).mockImplementation(loadReal);
});

function editor(props: Partial<Parameters<typeof RichTextEditor>[0]> = {}) {
  return (
    <RichTextEditor
      label="Описание"
      value=""
      onChange={() => {}}
      helperText="До 100 000 символов"
      {...props}
    />
  );
}

describe('states (design § States)', () => {
  it('shows a skeleton with a hidden «Загрузка редактора» while the chunk loads, helper row already in place', async () => {
    let release: (value: Awaited<ReturnType<typeof loadReal>>) => void = () => {};
    vi.mocked(loadEditorImpl).mockImplementation(
      () => new Promise((resolve) => (release = resolve)),
    );

    const { container } = render(editor());

    expect(screen.getByText('Загрузка редактора')).toBeInTheDocument();
    expect(container.querySelector('[aria-busy="true"]')).not.toBeNull();
    expect(screen.queryByRole('textbox', { name: 'Описание' })).not.toBeInTheDocument();
    expect(screen.getByText('До 100 000 символов')).toBeInTheDocument();

    release(await loadReal());
    expect(await screen.findByRole('textbox', { name: 'Описание' })).toBeInTheDocument();
    expect(screen.queryByText('Загрузка редактора')).not.toBeInTheDocument();
  });

  it('shows «Не удалось загрузить редактор.» with «Повторить», and a retry loads it', async () => {
    const user = userEvent.setup();
    vi.mocked(loadEditorImpl).mockRejectedValueOnce(new Error('chunk failed'));
    vi.spyOn(console, 'error').mockImplementation(() => {});

    render(editor());

    expect(await screen.findByText('Не удалось загрузить редактор.')).toBeInTheDocument();
    expect(screen.getByText('До 100 000 символов')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Повторить' }));

    expect(await screen.findByRole('textbox', { name: 'Описание' })).toBeInTheDocument();
    expect(screen.queryByText('Не удалось загрузить редактор.')).not.toBeInTheDocument();
    expect(vi.mocked(loadEditorImpl)).toHaveBeenCalledTimes(2);
  });

  it('keeps the helper and the always-present notice line, and names the body in describedby', async () => {
    render(editor());
    const body = await screen.findByRole('textbox', { name: 'Описание' });

    expect(screen.getByRole('status')).toBeInTheDocument();
    await waitFor(() =>
      expect(body.getAttribute('aria-describedby')).toContain(
        screen.getByText('До 100 000 символов').id,
      ),
    );
    expect(body).toHaveAttribute('aria-placeholder', 'Добавьте описание');
  });

  it('does not show the counter before 90 000 characters, and shows it from there', async () => {
    const { rerender } = render(editor({ value: '<p>короткий</p>' }));
    await screen.findByRole('textbox', { name: 'Описание' });
    expect(screen.queryByText(/\/ 100\u00a0000/)).not.toBeInTheDocument();

    rerender(editor({ value: `<p>${'я'.repeat(98_450)}</p>` }));

    // ru-RU groups with a no-break space; Testing Library's default normaliser would turn it into a space.
    const exact = { normalizer: (text: string) => text };
    const format = new Intl.NumberFormat('ru-RU');
    expect(
      await screen.findByText(`${format.format(98_450)} / ${format.format(100_000)}`, exact),
    ).toBeInTheDocument();
    expect(
      screen.getByText(`Символов: ${format.format(98_450)} из ${format.format(100_000)}`, exact),
    ).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('over the visible limit: the live message replaces the helper once, as an alert, and the card is invalid', async () => {
    render(editor({ value: `<p>${'я'.repeat(11)}</p>`, maxVisible: 10, counterFrom: 5 }));
    const body = await screen.findByRole('textbox', { name: 'Описание' });

    expect(await screen.findByRole('alert')).toHaveTextContent('Не более 100 000 символов.');
    expect(screen.queryByText('До 100 000 символов')).not.toBeInTheDocument();
    await waitFor(() => expect(body).toHaveAttribute('aria-invalid', 'true'));
  });

  it('exactly at the limit is within', async () => {
    render(editor({ value: `<p>${'я'.repeat(10)}</p>`, maxVisible: 10, counterFrom: 5 }));
    await screen.findByRole('textbox', { name: 'Описание' });

    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(screen.getByText('До 100 000 символов')).toBeInTheDocument();
  });

  it('a refusal replaces the helper and the live limit text and keeps the input', async () => {
    render(
      editor({
        value: `<p>${'я'.repeat(11)}</p>`,
        maxVisible: 10,
        errorText: 'Слишком много разметки. Упростите форматирование или сократите текст.',
      }),
    );
    const body = await screen.findByRole('textbox', { name: 'Описание' });

    expect(
      screen.getByText('Слишком много разметки. Упростите форматирование или сократите текст.'),
    ).toBeInTheDocument();
    expect(screen.queryByText('Не более 100 000 символов.')).not.toBeInTheDocument();
    expect(body).toHaveTextContent('я'.repeat(11));
    await waitFor(() => expect(body).toHaveAttribute('aria-invalid', 'true'));
  });

  it('while disabled the body is not editable and the focus is not moved', async () => {
    const { rerender } = render(editor({ value: '<p>a</p>' }));
    const body = await screen.findByRole('textbox', { name: 'Описание' });
    expect(body).toHaveAttribute('contenteditable', 'true');

    rerender(editor({ value: '<p>a</p>', disabled: true }));

    await waitFor(() => expect(body).toHaveAttribute('contenteditable', 'false'));
    expect(body).toHaveAttribute('aria-disabled', 'true');
    expect(body).toHaveTextContent('a');
  });
});

describe('a new value (plan § 7.2)', () => {
  it('replaces the document, with the history cleared, when the value is not what it holds', async () => {
    const user = userEvent.setup();
    const { rerender } = render(editor({ value: '<p>первое</p>' }));
    const body = await screen.findByRole('textbox', { name: 'Описание' });
    await user.click(body);
    await user.keyboard('X');
    expect(body).toHaveTextContent('Xпервое');

    rerender(editor({ value: '<p>другое</p>' }));

    await waitFor(() =>
      expect(screen.getByRole('textbox', { name: 'Описание' })).toHaveTextContent('другое'),
    );
    expect(screen.getByRole('textbox', { name: 'Описание' })).not.toHaveTextContent('первое');
    await waitFor(() => expect(screen.getByRole('button', { name: 'Отменить' })).toBeDisabled());
  });
});
