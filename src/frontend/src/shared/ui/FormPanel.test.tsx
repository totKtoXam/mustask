import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import Button from '@mui/material/Button';
import TextField from '@mui/material/TextField';
import { theme } from '../../app/theme';
import { FormFooter, FormPanel } from './FormPanel';

describe('US-9 Scenario: Form page layout — the form column', () => {
  it('stands the fields in one column of limited width with the footer below the last field', () => {
    const { container } = render(
      <FormPanel
        onSubmit={(event) => event.preventDefault()}
        footer={
          <FormFooter>
            <Button>Отмена</Button>
            <Button type="submit" variant="contained">
              Добавить
            </Button>
          </FormFooter>
        }
      >
        <TextField label="Код" />
        <TextField label="Эндоним" />
      </FormPanel>,
    );

    const form = container.querySelector('form');
    expect(form).not.toBeNull();
    expect(getComputedStyle(form as Element).maxWidth).toBe(`${theme.layout.formColumnWidth}px`);
    const lastField = screen.getByLabelText('Эндоним');
    const cancel = screen.getByRole('button', { name: 'Отмена' });
    expect(
      lastField.compareDocumentPosition(cancel) & Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();
  });
});

describe('US-9 Scenario: Field and form errors — the banner', () => {
  it('shows an error not tied to a field in a banner above the first field', () => {
    render(
      <FormPanel onSubmit={() => {}} error="Ошибка API (HTTP 500)." footer={null}>
        <TextField label="Код" />
      </FormPanel>,
    );

    const alert = screen.getByRole('alert');
    expect(alert).toHaveTextContent('Ошибка API (HTTP 500).');
    expect(
      alert.compareDocumentPosition(screen.getByLabelText('Код')) &
        Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();
  });

  it('submits through its own form', async () => {
    const onSubmit = vi.fn((event: { preventDefault(): void }) => event.preventDefault());
    render(
      <FormPanel onSubmit={onSubmit} footer={<Button type="submit">Сохранить</Button>}>
        <TextField label="Эндоним" />
      </FormPanel>,
    );

    await userEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    expect(onSubmit).toHaveBeenCalledTimes(1);
  });
});
