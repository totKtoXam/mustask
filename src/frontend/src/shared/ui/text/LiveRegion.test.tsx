import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { LiveRegion } from './LiveRegion';

// tech-015 design § 5.5 «LiveRegion»: a `role="status"` region, visually hidden; every
// message replaces the text node, so a repeat is spoken again (feat-027 F-11).

describe('tech-015 Scenario: LiveRegion (design § 5.5)', () => {
  it('is a status region with its text, visually hidden', () => {
    render(<LiveRegion text="MSK-EPIC-1 перемещён: позиция 2 из 5." />);
    const region = screen.getByRole('status');
    expect(region).toHaveTextContent('MSK-EPIC-1 перемещён: позиция 2 из 5.');
    expect(getComputedStyle(region).position).toBe('absolute');
    expect(getComputedStyle(region).width).toBe('1px');
  });

  it('exists before its first message, so the first one is spoken', () => {
    render(<LiveRegion text="" />);
    expect(screen.getByRole('status')).toHaveTextContent(/^$/);
  });

  it('replaces the text node for a message with a new serial — even an equal text', () => {
    const { rerender } = render(<LiveRegion text="Показано строк: 5." serial={1} />);
    const first = screen.getByText('Показано строк: 5.');
    rerender(<LiveRegion text="Показано строк: 5." serial={2} />);
    const second = screen.getByText('Показано строк: 5.');
    expect(second).not.toBe(first);
    expect(first.isConnected).toBe(false);
  });

  it('keeps the node while the serial stays, so a re-render does not speak again', () => {
    const { rerender } = render(<LiveRegion text="Показано строк: 5." serial={3} />);
    const first = screen.getByText('Показано строк: 5.');
    rerender(<LiveRegion text="Показано строк: 5." serial={3} />);
    expect(screen.getByText('Показано строк: 5.')).toBe(first);
  });

  it('has the serial 0 by default, and a different text is spoken in the same node', () => {
    const { rerender } = render(<LiveRegion text="Раз" />);
    const first = screen.getByText('Раз');
    rerender(<LiveRegion text="Два" />);
    expect(screen.getByRole('status')).toHaveTextContent('Два');
    expect(first.isConnected).toBe(true);
    expect(first).toHaveTextContent('Два');
  });
});
