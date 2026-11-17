import { describe, expect, it, vi } from 'vitest';
import userEvent from '@testing-library/user-event';
import { render, screen } from '@testing-library/react';
import { FilePickerButton } from './FilePickerButton';

describe('FilePickerButton', () => {
  it('is a button named by its label and reports the one picked file', async () => {
    const onPick = vi.fn();
    render(<FilePickerButton label="Выбрать файл" accept=".xlsx,.csv" onPick={onPick} />);
    expect(screen.getByRole('button', { name: 'Выбрать файл' })).toBeInTheDocument();

    const file = new File(['a;b'], 'Реестр.csv', { type: 'text/csv' });
    await userEvent.upload(screen.getByLabelText('Выбрать файл', { selector: 'input' }), file);

    expect(onPick).toHaveBeenCalledTimes(1);
    expect(onPick).toHaveBeenCalledWith(file);
  });

  it('opens the file dialog from the button', async () => {
    const click = vi.spyOn(HTMLInputElement.prototype, 'click');
    render(<FilePickerButton label="Выбрать файл" onPick={() => undefined} />);

    await userEvent.click(screen.getByRole('button', { name: 'Выбрать файл' }));

    expect(click).toHaveBeenCalled();
    click.mockRestore();
  });

  it('does nothing while disabled', () => {
    render(<FilePickerButton label="Выбрать файл" disabled onPick={() => undefined} />);
    expect(screen.getByRole('button', { name: 'Выбрать файл' })).toBeDisabled();
  });
});
