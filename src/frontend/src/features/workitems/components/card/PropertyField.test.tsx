import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { INCOMPLETE_DATE } from '../../lib/fieldValues';
import { CARD_TEXT_ROWS, PropertyField, TextBlock } from './PropertyField';

// Wiring only: what the card passes the kit's text area. The field's own states are the kit's.
const seen = vi.hoisted(() => ({ areas: [] as Record<string, unknown>[] }));

vi.mock('../../../../shared/ui', async (importOriginal) => {
  const original = await importOriginal<typeof import('../../../../shared/ui')>();
  return {
    ...original,
    InlineTextArea: (props: Parameters<typeof original.InlineTextArea>[0]) => {
      seen.areas.push(props as unknown as Record<string, unknown>);
      return original.InlineTextArea(props);
    },
  };
});

const noop = () => {};
const common = { error: undefined, busy: false, readOnly: false, onChange: noop };

describe('feat-029 D-4: the card’s texts', () => {
  beforeEach(() => {
    seen.areas.length = 0;
  });

  it('take two rows, below the kit’s three, on every text', () => {
    expect(CARD_TEXT_ROWS).toBe(2);
    render(
      <>
        <TextBlock name="description" value="" {...common} />
        <TextBlock name="reproSteps" value="" {...common} />
        <TextBlock name="systemInfo" value="" {...common} />
      </>,
    );

    expect(seen.areas.map((props) => props.minRows)).toEqual([2, 2, 2]);
  });

  it('are named by their head: the label is the head’s text and is not drawn', () => {
    render(
      <>
        <TextBlock name="description" value="" {...common} />
        <TextBlock name="reproSteps" value="" {...common} />
      </>,
    );

    expect(seen.areas.map((props) => [props.label, props.labelPlacement])).toEqual([
      ['Описание', 'hidden'],
      ['Шаги воспроизведения', 'hidden'],
    ]);
    expect(screen.getByRole('textbox', { name: 'Описание' })).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: 'Шаги воспроизведения' })).toBeInTheDocument();
  });
});

describe('feat-022: a date typed in part is reported as incomplete', () => {
  it('holds the sentinel for the draft, and shows an empty control', () => {
    const changes: string[] = [];
    render(
      <PropertyField
        name="startDate"
        value={INCOMPLETE_DATE}
        error="Введите дату полностью."
        busy={false}
        readOnly={false}
        onChange={(value) => changes.push(value)}
      />,
    );

    const input = screen.getByLabelText<HTMLInputElement>('Дата начала');
    expect(input.value).toBe('');
    expect(input).toHaveAttribute('min', '2000-01-01');
    expect(input).toHaveAttribute('max', '2099-12-31');
    expect(input).toHaveAccessibleDescription('Введите дату полностью.');

    fireEvent.change(input, { target: { value: '2026-10-01' } });
    expect(changes).toEqual(['2026-10-01']);
  });

  it('reports a partial entry on a key-up, as the browser announces no change for it', () => {
    const changes: string[] = [];
    render(
      <PropertyField
        name="targetDate"
        value=""
        error={undefined}
        busy={false}
        readOnly={false}
        onChange={(value) => changes.push(value)}
      />,
    );
    const input = screen.getByLabelText<HTMLInputElement>('Целевая дата');
    Object.defineProperty(input, 'validity', { value: { badInput: true }, configurable: true });

    fireEvent.keyUp(input);

    expect(changes).toEqual([INCOMPLETE_DATE]);
  });
});
