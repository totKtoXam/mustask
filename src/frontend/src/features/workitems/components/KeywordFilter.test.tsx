import { useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen } from '@testing-library/react';
import { KeywordFilter } from './KeywordFilter';

// feat-003 US-3, BR-WS-9, design § 2.1, D-3 A: «Поиск» applies the trimmed text after a pause of
// 250 ms, on Enter and on leaving the field; focus stays in the input; the address's own changes
// are shown whole. Time moves only when the test says so, and the events are synchronous: with
// faked timers the user-event library would wait for a timer that never comes.

/** The page’s side: an address that holds the keyword and answers a write, at once or when told to. */
function Harness({
  initial = '',
  writes,
  delayed = false,
}: {
  initial?: string;
  writes: string[];
  /** The address answers a write only when the test says so (the router applies it as a transition). */
  delayed?: boolean;
}) {
  const [address, setAddress] = useState(initial);
  const [queue, setQueue] = useState<string[]>([]);
  return (
    <>
      <KeywordFilter
        value={address}
        onChange={(keyword) => {
          writes.push(keyword);
          if (delayed) {
            setQueue((before) => [...before, keyword]);
          } else {
            setAddress(keyword);
          }
        }}
      />
      <button type="button" onClick={() => setAddress('из ссылки')}>
        Back
      </button>
      <button
        type="button"
        onClick={() => {
          const [next, ...rest] = queue;
          setQueue(rest);
          setAddress(next ?? '');
        }}
      >
        Answer
      </button>
      <output data-testid="address">{address}</output>
    </>
  );
}

const input = () => screen.getByRole('textbox', { name: 'Поиск' }) as HTMLInputElement;

beforeEach(() => {
  vi.useFakeTimers();
});

afterEach(() => {
  vi.useRealTimers();
});

function type(text: string) {
  input().focus();
  fireEvent.change(input(), { target: { value: text } });
}

function wait(ms: number) {
  act(() => void vi.advanceTimersByTime(ms));
}

describe('feat-003 D-3 when the keyword applies', () => {
  it('after a pause of 250 ms, not before, and with the trimmed text', () => {
    const writes: string[] = [];
    render(<Harness writes={writes} />);

    type('  оплат ');
    wait(249);
    expect(writes).toEqual([]);
    wait(1);

    expect(writes).toEqual(['оплат']);
    expect(input()).toHaveFocus();
    expect(input()).toHaveValue('  оплат ');
  });

  it('a pause restarts with every key: one write for the text typed', () => {
    const writes: string[] = [];
    render(<Harness writes={writes} />);

    type('оп');
    wait(200);
    type('оплат');
    wait(200);
    expect(writes).toEqual([]);
    wait(50);

    expect(writes).toEqual(['оплат']);
  });

  it('on Enter at once, and again on nothing: the same text is not written twice', () => {
    const writes: string[] = [];
    render(<Harness writes={writes} />);

    type('оплат');
    fireEvent.keyDown(input(), { key: 'Enter' });
    expect(writes).toEqual(['оплат']);
    wait(500);
    fireEvent.keyDown(input(), { key: 'Enter' });

    expect(writes).toEqual(['оплат']);
    expect(input()).toHaveFocus();
  });

  it('on leaving the field: the text is applied and the field shows it trimmed', () => {
    const writes: string[] = [];
    render(<Harness writes={writes} />);

    type('  оплат  ');
    fireEvent.blur(input());

    expect(writes).toEqual(['оплат']);
    expect(input()).toHaveValue('оплат');
  });

  it('a text of spaces sets no filter, and reads blank again on leaving', () => {
    const writes: string[] = [];
    render(<Harness writes={writes} />);

    type('   ');
    wait(500);
    fireEvent.blur(input());

    expect(writes).toEqual([]);
    expect(input()).toHaveValue('');
  });

  it('emptying the field clears the keyword after the pause', () => {
    const writes: string[] = [];
    render(<Harness writes={writes} initial="оплат" />);

    type('');
    wait(250);

    expect(writes).toEqual(['']);
  });
});

describe('feat-003 BR-WS-9 the text and the address', () => {
  it('takes at most 255 characters by hand and shows a longer keyword of a link whole', () => {
    const longer = 'я'.repeat(300);
    render(<Harness writes={[]} initial={longer} />);

    expect(input()).toHaveAttribute('maxlength', '255');
    expect(input()).toHaveValue(longer);
  });

  it('shows the hint «Заголовок или ID» as its description', () => {
    render(<Harness writes={[]} />);

    expect(input()).toHaveAccessibleDescription('Заголовок или ID');
  });

  it('takes the address’s text when it changes for any other reason (Back, a link, a clear)', () => {
    render(<Harness writes={[]} initial="старый" />);
    type('старый и ещё');

    fireEvent.click(screen.getByRole('button', { name: 'Back' }));

    expect(input()).toHaveValue('из ссылки');
  });

  it('does not take the answer to its own write for another’s change: the text typed meanwhile stays', () => {
    const writes: string[] = [];
    render(<Harness writes={writes} delayed />);

    type('оп');
    wait(250);
    expect(writes).toEqual(['оп']);
    type('оплат');
    fireEvent.click(screen.getByRole('button', { name: 'Answer' }));

    expect(screen.getByTestId('address')).toHaveTextContent('оп');
    expect(input()).toHaveValue('оплат');
    wait(250);
    expect(writes).toEqual(['оп', 'оплат']);
  });
});
