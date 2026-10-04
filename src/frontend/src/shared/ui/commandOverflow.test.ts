import { describe, expect, it } from 'vitest';
import { splitCommands } from './commandOverflow';

describe('US-6 Scenario: Pivot tabs and overflow in the pattern — splitCommands', () => {
  it('keeps every command inline when they all fit', () => {
    expect(splitCommands([100, 120, 80], 300, 40)).toBe(3);
    expect(splitCommands([100, 120, 80], 400, 40)).toBe(3);
  });

  it('moves commands from the end until the rest and «Ещё действия» fit', () => {
    // 100 + 120 + 40 = 260 fits 270; 100 + 120 + 80 = 300 does not.
    expect(splitCommands([100, 120, 80], 270, 40)).toBe(2);
    // 100 + 40 = 140 fits 150.
    expect(splitCommands([100, 120, 80], 150, 40)).toBe(1);
  });

  it('moves every secondary command when none fits beside the overflow button', () => {
    expect(splitCommands([100, 120, 80], 60, 40)).toBe(0);
    expect(splitCommands([100], 0, 40)).toBe(0);
  });

  it('has nothing to split without secondary commands', () => {
    expect(splitCommands([], 0, 40)).toBe(0);
  });
});
