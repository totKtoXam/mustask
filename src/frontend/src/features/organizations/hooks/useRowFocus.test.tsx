import { useState } from 'react';
import { describe, expect, it } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useRowFocus } from './useRowFocus';

// mod-016 design § 3.7, § 4.4 (the rule of mod-002 § 5.5): after a removal
// focus goes to the next row's button, else the previous one's, else the
// command beside the table; after a refusal, back to the row's own button.

function Harness({ initial }: { initial: string[] }) {
  const [rows, setRows] = useState(initial);
  const focus = useRowFocus(rows, () => document.getElementById('command'));
  return (
    <>
      <button id="command" type="button">
        Добавить
      </button>
      {rows.map((id) => (
        <button key={id} type="button" ref={focus.buttonRef(id)}>
          {`Действия: ${id}`}
        </button>
      ))}
      <button
        type="button"
        onClick={() => {
          const target = rows[1] ?? rows[0]!;
          const neighbours = focus.neighboursOf(target);
          setRows((current) => current.filter((id) => id !== target));
          focus.afterRemoval(target, neighbours);
        }}
      >
        Убрать вторую
      </button>
      <button
        type="button"
        onClick={() => {
          focus.afterFailure(rows[0]!);
          setRows((current) => [...current]);
        }}
      >
        Отказ
      </button>
    </>
  );
}

describe('mod-016 hooks/useRowFocus', () => {
  it('focuses the next row once the removed one is gone', async () => {
    render(<Harness initial={['a', 'b', 'c']} />);

    await userEvent.click(screen.getByRole('button', { name: 'Убрать вторую' }));

    await waitFor(() => expect(screen.getByRole('button', { name: 'Действия: c' })).toHaveFocus());
  });

  it('focuses the previous row when the removed one was the last', async () => {
    render(<Harness initial={['a', 'b']} />);

    await userEvent.click(screen.getByRole('button', { name: 'Убрать вторую' }));

    await waitFor(() => expect(screen.getByRole('button', { name: 'Действия: a' })).toHaveFocus());
  });

  it('focuses the command beside the table when no row is left', async () => {
    render(<Harness initial={['a']} />);

    await userEvent.click(screen.getByRole('button', { name: 'Убрать вторую' }));

    await waitFor(() => expect(screen.getByRole('button', { name: 'Добавить' })).toHaveFocus());
  });

  it('focuses the row’s own button again after a refusal', async () => {
    render(<Harness initial={['a', 'b']} />);

    await userEvent.click(screen.getByRole('button', { name: 'Отказ' }));

    await waitFor(() => expect(screen.getByRole('button', { name: 'Действия: a' })).toHaveFocus());
  });
});
