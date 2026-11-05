import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { px, rgb, theme } from '../../../test/kitTesting';
import { InlineEmpty } from './InlineEmpty';

// tech-015 design § 5.5 «InlineEmpty»: one quiet line for an empty list or section — not
// a StateFrame; the words are the host's.

describe('tech-015 Scenario: InlineEmpty (design § 5.5)', () => {
  it('says the host’s words in body2, text.secondary', () => {
    render(<InlineEmpty>Дочерних элементов нет.</InlineEmpty>);
    const text = screen.getByText('Дочерних элементов нет.');
    const style = getComputedStyle(text);
    expect(style.color).toBe(rgb(theme.palette.text.secondary));
    expect(px(style.fontSize)).toBe(14);
    expect(text).toHaveClass('MuiTypography-body2');
  });

  it('is not a frame: no role, no heading, no button', () => {
    render(<InlineEmpty>Пока ничего нет.</InlineEmpty>);
    expect(screen.queryByRole('heading')).toBeNull();
    expect(screen.queryByRole('button')).toBeNull();
    expect(screen.queryByRole('alert')).toBeNull();
    expect(screen.queryByRole('status')).toBeNull();
  });
});
