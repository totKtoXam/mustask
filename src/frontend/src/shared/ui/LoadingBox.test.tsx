import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { LoadingBox } from './LoadingBox';

// mod-015: the centred loading indicator of the sign-in screen and of a dialog while its
// content comes. Pages and cards load with a skeleton of their content, not with this.

describe('mod-015 Scenario: LoadingBox', () => {
  it('is one progressbar named «Загрузка»', () => {
    render(<LoadingBox />);
    expect(screen.getAllByRole('progressbar')).toHaveLength(1);
    expect(screen.getByRole('progressbar', { name: 'Загрузка' })).toBeInTheDocument();
  });

  it('centres its indicator in a row of its own', () => {
    const { container } = render(<LoadingBox />);
    const row = container.firstElementChild as HTMLElement;
    expect(getComputedStyle(row).display).toBe('flex');
    expect(getComputedStyle(row).justifyContent).toBe('center');
  });
});
