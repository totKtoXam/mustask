import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { mountEditor } from '../../../test/richTextEditor';

// The emoji data cannot be loaded (design gap G-1): the popover shows the editor's own
// failure text with «Повторить», not an empty box.
vi.mock('virtual:rich-text-emoji', () => {
  throw new Error('chunk failed');
});

describe('emoji picker: the data fails to load (G-1)', () => {
  it('shows «Не удалось загрузить редактор.» with «Повторить», which asks again', async () => {
    const user = userEvent.setup();
    const { button } = await mountEditor('<p>до</p>');

    await user.click(button('Эмодзи'));
    const dialog = await screen.findByRole('dialog', { name: 'Эмодзи' });

    expect(await within(dialog).findByText('Не удалось загрузить редактор.')).toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', { name: 'Повторить' }));
    expect(await within(dialog).findByText('Не удалось загрузить редактор.')).toBeInTheDocument();
    await waitFor(() => expect(dialog).toBeInTheDocument());
  });
});
