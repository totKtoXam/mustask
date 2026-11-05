import { useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import SvgIcon from '@mui/material/SvgIcon';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { IconToggleGroup, type IconToggleItem } from './IconToggleGroup';

// feat-049 design § 4.9: a one-of-N icon switch. The group is named after the control, each
// button after its value, the chosen one is `aria-pressed`, and the tooltip is the host's text.

function Glyph() {
  return (
    <SvgIcon>
      <path d="M0 0h24v24H0z" />
    </SvgIcon>
  );
}

const ITEMS: IconToggleItem[] = [
  {
    value: 'h',
    label: 'Горизонтально',
    icon: <Glyph />,
    tooltip: 'Раскладка: Горизонтально. Слева направо',
  },
  {
    value: 'v',
    label: 'Вертикально',
    icon: <Glyph />,
    tooltip: 'Раскладка: Вертикально. Сверху вниз',
  },
];

function Harness({ onChange = vi.fn() }: { onChange?: (value: string) => void }) {
  const [value, setValue] = useState('h');
  return (
    <IconToggleGroup
      label="Раскладка"
      items={ITEMS}
      value={value}
      onChange={(next) => {
        setValue(next);
        onChange(next);
      }}
    />
  );
}

describe('feat-049 IconToggleGroup (design § 4.9)', () => {
  it('is a group named after the control, its buttons named after their values', () => {
    render(<Harness />);
    const group = screen.getByRole('group', { name: 'Раскладка' });
    expect(group).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Горизонтально' })).toHaveAttribute(
      'aria-pressed',
      'true',
    );
    expect(screen.getByRole('button', { name: 'Вертикально' })).toHaveAttribute(
      'aria-pressed',
      'false',
    );
  });

  it('chooses the other value on a click and ignores a click on the chosen one', async () => {
    const onChange = vi.fn();
    render(<Harness onChange={onChange} />);
    await userEvent.click(screen.getByRole('button', { name: 'Горизонтально' }));
    expect(onChange).not.toHaveBeenCalled();
    await userEvent.click(screen.getByRole('button', { name: 'Вертикально' }));
    expect(onChange).toHaveBeenCalledWith('v');
    expect(screen.getByRole('button', { name: 'Вертикально' })).toHaveAttribute(
      'aria-pressed',
      'true',
    );
  });

  it('is one Tab stop, moves between buttons with the arrow keys and chooses on Enter', async () => {
    const onChange = vi.fn();
    render(<Harness onChange={onChange} />);
    await userEvent.tab();
    expect(screen.getByRole('button', { name: 'Горизонтально' })).toHaveFocus();
    await userEvent.keyboard('{ArrowRight}');
    expect(screen.getByRole('button', { name: 'Вертикально' })).toHaveFocus();
    await userEvent.keyboard('{Enter}');
    expect(onChange).toHaveBeenCalledWith('v');
  });

  it('shows the host’s tooltip on hover and on keyboard focus', async () => {
    render(<Harness />);
    await userEvent.hover(screen.getByRole('button', { name: 'Вертикально' }));
    expect(await screen.findByRole('tooltip')).toHaveTextContent(
      'Раскладка: Вертикально. Сверху вниз',
    );
  });
});
