import { describe, expect, it } from 'vitest';
import { planningNumber, valueAreaLabel } from './values';

// feat-027 BR-T-3: how the planning values read in the backlog table.

describe('planningNumber', () => {
  it('writes the shortest form with «,» as the decimal mark', () => {
    expect(planningNumber(20)).toBe('20');
    expect(planningNumber(2.5)).toBe('2,5');
    expect(planningNumber(0.25)).toBe('0,25');
    expect(planningNumber(300)).toBe('300');
  });

  it('leaves the cell blank for an empty value or a type without the field', () => {
    expect(planningNumber(null)).toBe('');
  });

  it('writes a zero as «0», not as blank: zero is a value', () => {
    expect(planningNumber(0)).toBe('0');
  });
});

describe('valueAreaLabel', () => {
  it('names the list’s two keys and leaves a task’s or a bug’s blank', () => {
    expect(valueAreaLabel('Business')).toBe('Бизнес');
    expect(valueAreaLabel('Architectural')).toBe('Архитектура');
    expect(valueAreaLabel(null)).toBe('');
  });

  it('shows a key the panel does not know as it was sent', () => {
    expect(valueAreaLabel('Compliance')).toBe('Compliance');
  });
});
