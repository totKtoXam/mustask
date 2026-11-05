import { describe, expect, it } from 'vitest';
import { levelIncluded, levelVisible, NO_LEVELS } from './diagramLevels';
import { clampZoom, scrollAfterZoom, stepZoom, wheelZoom, ZOOM_MAX, ZOOM_MIN } from './diagramZoom';

describe('feat-049 zoom (BR-SD-20)', () => {
  it('clamps to 50-200 %', () => {
    expect(clampZoom(0.1)).toBe(ZOOM_MIN);
    expect(clampZoom(5)).toBe(ZOOM_MAX);
  });

  it('steps by a factor of 1.1 and stops at the limits', () => {
    expect(stepZoom(1, 1)).toBe(1.1);
    expect(stepZoom(1, -1)).toBe(0.91);
    expect(stepZoom(ZOOM_MAX, 1)).toBe(ZOOM_MAX);
    expect(stepZoom(ZOOM_MIN, -1)).toBe(ZOOM_MIN);
  });

  it('zooms with the wheel, in on a negative delta', () => {
    expect(wheelZoom(1, -100)).toBeGreaterThan(1);
    expect(wheelZoom(1, 100)).toBeLessThan(1);
  });

  it('keeps the point under the pointer fixed', () => {
    // Picture point 300 under pointer 100 at zoom 1 (scroll 200) stays under it at zoom 2.
    expect(scrollAfterZoom(200, 100, 1, 2)).toBe(500);
  });
});

describe('feat-049 level choice (BR-SD-14)', () => {
  it('shows every level when none is pressed', () => {
    expect(levelVisible(NO_LEVELS, 4)).toBe(true);
  });

  it('shows the pressed levels, or the topmost one and deeper', () => {
    const only = { selected: new Set([2, 4]), mode: 'only' as const };
    expect([1, 2, 3, 4].filter((l) => levelVisible(only, l))).toEqual([2, 4]);
    const from = { selected: new Set([2, 4]), mode: 'from' as const };
    expect([1, 2, 3, 4].filter((l) => levelVisible(from, l))).toEqual([2, 3, 4]);
    expect([1, 2, 3, 4].filter((l) => levelIncluded(from, l))).toEqual([3]);
  });
});
