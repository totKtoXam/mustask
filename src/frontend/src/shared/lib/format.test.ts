import { describe, expect, it } from 'vitest';
import { formatDate, formatDateTime } from './format';

describe('formatDateTime', () => {
  it('keeps the minute-resolution form beside it', () => {
    expect(formatDateTime('2026-10-05T14:30:00')).toBe('05.10.2026 14:30');
    expect(formatDateTime(undefined)).toBe('—');
  });
});

describe('formatDate', () => {
  it('shows the day without the time (feat-033 BR-RS-12)', () => {
    expect(formatDate('2026-10-05T14:30:00')).toBe('05.10.2026');
  });

  it('shows a dash for nothing and for nonsense', () => {
    expect(formatDate(null)).toBe('—');
    expect(formatDate(undefined)).toBe('—');
    expect(formatDate('')).toBe('—');
    expect(formatDate('not a date')).toBe('—');
  });
});
