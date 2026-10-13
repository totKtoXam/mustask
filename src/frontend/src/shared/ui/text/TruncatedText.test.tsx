import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { rgb, theme } from '../../../test/kitTesting';
import { TruncatedText } from './TruncatedText';

// tech-015 design § 5.5 «TruncatedText»: one line, an ellipsis, the whole text in the
// DOM; the full text in the native title (default) or in a tooltip; a router link in
// a plain or a link look.

const LONG = 'Описать end-to-end сценарии использования текущей системы';

describe('tech-015 Scenario: TruncatedText (design § 5.5)', () => {
  it('cuts the text to one line with an ellipsis and lets a flex cell shrink it', () => {
    render(<TruncatedText>{LONG}</TruncatedText>);
    const style = getComputedStyle(screen.getByText(LONG));
    expect(style.overflow).toBe('hidden');
    expect(style.textOverflow).toBe('ellipsis');
    expect(style.whiteSpace).toBe('nowrap');
    expect(parseFloat(style.minWidth)).toBe(0);
  });

  it('keeps the whole text in the DOM, so the accessible name is complete', () => {
    render(<TruncatedText>{LONG}</TruncatedText>);
    expect(screen.getByText(LONG)).toHaveTextContent(LONG);
  });

  describe('Scenario: where the full text is', () => {
    it('is the native title by default, the reference’s hover', () => {
      render(<TruncatedText>{LONG}</TruncatedText>);
      expect(screen.getByText(LONG)).toHaveAttribute('title', LONG);
    });

    it('is a tooltip when asked, and then there is no title to double it', async () => {
      const user = userEvent.setup();
      render(<TruncatedText hint="tooltip">{LONG}</TruncatedText>);
      const text = screen.getByText(LONG);
      expect(text).not.toHaveAttribute('title');
      await user.hover(text);
      expect(await screen.findByRole('tooltip')).toHaveTextContent(LONG);
    });
  });

  describe('Scenario: the element', () => {
    it('is a span by default and any element on request', () => {
      const { container, rerender } = render(<TruncatedText>Текст</TruncatedText>);
      expect(container.firstElementChild?.tagName).toBe('SPAN');
      rerender(<TruncatedText component="div">Текст</TruncatedText>);
      expect(container.firstElementChild?.tagName).toBe('DIV');
    });
  });

  describe('Scenario: a router link', () => {
    function renderLink(look?: 'plain' | 'link') {
      render(
        <MemoryRouter>
          <TruncatedText to="/organizations/1/projects/2/backlog" look={look}>
            {LONG}
          </TruncatedText>
        </MemoryRouter>,
      );
      return screen.getByRole('link', { name: LONG });
    }

    it('is a link named by the whole text, truncated like any other', () => {
      const link = renderLink();
      expect(link).toHaveAttribute('href', '/organizations/1/projects/2/backlog');
      expect(link).toHaveAttribute('title', LONG);
      expect(getComputedStyle(link).textOverflow).toBe('ellipsis');
    });

    it('plain (default) takes the surrounding colour and is underlined on hover only', () => {
      const link = renderLink();
      const style = getComputedStyle(link);
      expect(style.color).not.toBe(rgb(theme.palette.primary.dark));
      expect(style.textDecorationLine || style.textDecoration).not.toMatch(/underline/);
      expect(link).toHaveClass('MuiLink-underlineHover');
    });

    it('link takes the theme’s link colour and stays underlined', () => {
      const link = renderLink('link');
      expect(getComputedStyle(link).color).toBe(rgb(theme.palette.primary.dark));
      expect(link).toHaveClass('MuiLink-underlineAlways');
    });
  });
});
