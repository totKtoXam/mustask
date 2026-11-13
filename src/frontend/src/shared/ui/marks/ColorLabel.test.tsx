import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { px, rgb, theme } from '../../../test/kitTesting';
import { ColorLabel } from './ColorLabel';

// doc-016 BR-TD-20, design § 7.1: the stored palette key as a dot beside the name.

describe('doc-016 Scenario: ColorLabel', () => {
  it('draws the dot aria-hidden in the key’s hex and the name as text', () => {
    render(<ColorLabel colour="orange" name="MARS" size="child" />);
    const dot = screen.getByTestId('colour-dot');
    expect(dot).toHaveAttribute('aria-hidden', 'true');
    expect(getComputedStyle(dot).backgroundColor).toBe(rgb('#D8661B'));
    expect(px(getComputedStyle(dot).width)).toBe(theme.layout.categoryDot);
    expect(screen.getByText('MARS')).toBeInTheDocument();
  });

  it('a department’s dot is the 10 px one', () => {
    render(<ColorLabel colour="blue" name="Логистика" size="department" />);
    expect(px(getComputedStyle(screen.getByTestId('colour-dot')).width)).toBe(
      theme.layout.mark.categoryDotField,
    );
  });

  it('draws the name alone when there is no colour, and a neutral dot for an unknown key', () => {
    const { rerender } = render(<ColorLabel colour={null} name="Alpha" />);
    expect(screen.queryByTestId('colour-dot')).toBeNull();
    rerender(<ColorLabel colour="mauve" name="Alpha" />);
    expect(getComputedStyle(screen.getByTestId('colour-dot')).backgroundColor).toBe(
      rgb(theme.palette.text.secondary),
    );
  });

  it('draws the name as a link when it has an address', () => {
    render(
      <MemoryRouter>
        <ColorLabel colour="green" name="SATURN" to="/teams/1" />
      </MemoryRouter>,
    );
    expect(screen.getByRole('link', { name: 'SATURN' })).toHaveAttribute('href', '/teams/1');
  });

  it('maps every one of the 12 keys to a distinct hex', () => {
    const keys = [
      'blue',
      'orange',
      'green',
      'purple',
      'red',
      'teal',
      'amber',
      'pink',
      'indigo',
      'lime',
      'magenta',
      'cyan',
    ];
    const hexes = keys.map((key) => theme.workTracking.objectColour(key));
    expect(new Set(hexes).size).toBe(12);
  });
});
