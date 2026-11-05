import { describe, expect, it } from 'vitest';
import { render } from '@testing-library/react';
import {
  BacklogIcon,
  ThinAddIcon,
  ThinChevronDownIcon,
  ThinChevronRightIcon,
  ThinChevronUpIcon,
  ThinCloseIcon,
  ThinExpandIcon,
  ThinMoreVerticalIcon,
  ThinPersonIcon,
  ThinRefreshIcon,
  ThinRemoveIcon,
  ThinSaveIcon,
  ThinUndoIcon,
} from '../index';

// tech-015 design § 5.1: the kit's glyphs, each one open stroked path in the
// 24 × 24 box — «⋮» and the backlog's bars are the filled ones. The paths are
// the contract the side-by-side look starts from, so they are pinned.

interface Glyph {
  Icon: typeof ThinAddIcon;
  testId: string;
  path: string;
  filled?: boolean;
}

const GLYPHS: readonly Glyph[] = [
  { Icon: ThinChevronDownIcon, testId: 'ThinChevronDownIcon', path: 'M4.5 8.5 12 16l7.5-7.5' },
  { Icon: ThinChevronUpIcon, testId: 'ThinChevronUpIcon', path: 'M4.5 15.5 12 8l7.5 7.5' },
  { Icon: ThinChevronRightIcon, testId: 'ThinChevronRightIcon', path: 'M8.5 4.5 16 12l-7.5 7.5' },
  { Icon: ThinAddIcon, testId: 'ThinAddIcon', path: 'M12 5v14M5 12h14' },
  { Icon: ThinRemoveIcon, testId: 'ThinRemoveIcon', path: 'M5 12h14' },
  { Icon: ThinCloseIcon, testId: 'ThinCloseIcon', path: 'M6 6l12 12M18 6 6 18' },
  {
    Icon: ThinExpandIcon,
    testId: 'ThinExpandIcon',
    path: 'M14 4h6v6M20 4l-7 7M10 20H4v-6M4 20l7-7',
  },
  {
    Icon: ThinMoreVerticalIcon,
    testId: 'ThinMoreVerticalIcon',
    path: 'M12 7.5a1.5 1.5 0 1 0 0-3 1.5 1.5 0 0 0 0 3zm0 6a1.5 1.5 0 1 0 0-3 1.5 1.5 0 0 0 0 3zm0 6a1.5 1.5 0 1 0 0-3 1.5 1.5 0 0 0 0 3z',
    filled: true,
  },
  {
    Icon: ThinRefreshIcon,
    testId: 'ThinRefreshIcon',
    path: 'M19.5 12a7.5 7.5 0 1 1-2.2-5.3M13.3 6.7h4v-4',
  },
  {
    Icon: ThinUndoIcon,
    testId: 'ThinUndoIcon',
    path: 'M9 14 4.5 9.5 9 5M4.5 9.5H15a4.5 4.5 0 0 1 0 9h-4',
  },
  {
    Icon: ThinSaveIcon,
    testId: 'ThinSaveIcon',
    path: 'M5 4.5h11.5l3 3V19a.5.5 0 0 1-.5.5H5a.5.5 0 0 1-.5-.5V5a.5.5 0 0 1 .5-.5zM8 4.5V9h7V4.5M7.5 19.5v-6h9v6',
  },
  {
    Icon: ThinPersonIcon,
    testId: 'ThinPersonIcon',
    path: 'M12 10.75a3.5 3.5 0 1 0 0-7 3.5 3.5 0 0 0 0 7zM6 20.25c0-3.3 2.7-5.5 6-5.5s6 2.2 6 5.5',
  },
  {
    Icon: BacklogIcon,
    testId: 'BacklogIcon',
    path: 'M8 6h13v2H8zM3 11h18v2H3zM9 16h12v2H9z',
    filled: true,
  },
];

describe('tech-015 Scenario: the kit’s icons (design § 5.1)', () => {
  it('has the thirteen glyphs of the table', () => {
    expect(GLYPHS).toHaveLength(13);
  });

  describe.each(GLYPHS)('$testId', ({ Icon, testId, path, filled }) => {
    it('renders an aria-hidden svg with its path', () => {
      const { container } = render(<Icon />);
      const svg = container.querySelector(`svg[data-testid="${testId}"]`);
      expect(svg).not.toBeNull();
      expect(svg).toHaveAttribute('aria-hidden', 'true');
      expect(svg).toHaveAttribute('viewBox', '0 0 24 24');
      expect(svg?.querySelector('path')).toHaveAttribute('d', path);
    });

    it(
      filled
        ? 'is a filled shape: no stroke of its own'
        : 'is one stroked hairline, 1.5 units, in the colour of its button',
      () => {
        const { container } = render(<Icon />);
        const drawn = container.querySelector('path');
        if (filled) {
          expect(drawn).not.toHaveAttribute('stroke');
          expect(drawn).not.toHaveAttribute('fill');
        } else {
          expect(drawn).toHaveAttribute('fill', 'none');
          expect(drawn).toHaveAttribute('stroke', 'currentColor');
          expect(drawn).toHaveAttribute('stroke-width', '1.5');
        }
      },
    );
  });
});
