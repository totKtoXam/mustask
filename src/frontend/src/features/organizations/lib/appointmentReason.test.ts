import { describe, expect, it } from 'vitest';
import { reasonLength, reasonProblem, trimmedReason } from './appointmentReason';

// feat-033 BR-RS-5: 1–500 Unicode code points of the trimmed text, counted as the server counts.

describe('the appointment reason', () => {
  it('trims as String.prototype.trim does: U+FEFF goes, U+0085 stays', () => {
    expect(trimmedReason('  причина  ')).toBe('причина');
    expect(trimmedReason('﻿причина﻿')).toBe('причина');
    expect(trimmedReason('\u0085причина')).toBe('\u0085причина');
  });

  it('counts code points: an emoji is one', () => {
    expect(reasonLength('😀😀')).toBe(2);
    expect(reasonLength('  😀  ')).toBe(1);
  });

  it('refuses an empty or blank reason as required', () => {
    expect(reasonProblem('')).toBe('required');
    expect(reasonProblem(' \t\n ')).toBe('required');
  });

  it('accepts 500 code points and refuses 501, whatever the UTF-16 length', () => {
    expect(reasonProblem('я'.repeat(500))).toBeNull();
    expect(reasonProblem('я'.repeat(501))).toBe('tooLong');
    expect(reasonProblem('😀'.repeat(500))).toBeNull();
    expect(reasonProblem('😀'.repeat(501))).toBe('tooLong');
    expect(reasonProblem(`  ${'я'.repeat(500)}  `)).toBeNull();
  });
});
