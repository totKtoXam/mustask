import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { px, theme } from '../../../test/kitTesting';
import { InlineSpinner } from './InlineSpinner';

// tech-015 design § 5.2 (`layout.inlineSpinner`): the one spinner of a busy button and of a
// field's busy adornment — the colour of the text it stands in. How a button and a field
// show it is in ActionButton.test.tsx and FieldFrame.shared.test.tsx.

describe('tech-015 Scenario: InlineSpinner (design § 5.2)', () => {
  it('is a progressbar of the size the theme names', () => {
    render(<InlineSpinner />);
    const bar = screen.getByRole('progressbar');
    expect(px(getComputedStyle(bar).width)).toBe(theme.layout.inlineSpinner);
    expect(px(getComputedStyle(bar).height)).toBe(theme.layout.inlineSpinner);
  });

  it('takes the colour of the text it stands in, so a tone keeps its contrast', () => {
    render(<InlineSpinner />);
    expect(screen.getByRole('progressbar')).toHaveClass('MuiCircularProgress-colorInherit');
  });
});
