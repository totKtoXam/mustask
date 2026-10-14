import { describe, expect, it } from 'vitest';
import { formatDateTime } from './format';

describe('formatDateTime', () => {
  it('keeps the minute-resolution form beside it', () => {
    expect(formatDateTime('2026-10-05T14:30:00')).toBe('05.10.2026 14:30');
    expect(formatDateTime(undefined)).toBe('—');
  });
});
