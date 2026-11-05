import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { px, rgb, theme } from '../../../test/kitTesting';
import { TypeLabel } from './TypeLabel';

// tech-015 design § 5.4 «TypeLabel»: the type's icon and its name, two sizes, a
// selected row's icon and the card's eyebrow (capitals by CSS, the DOM keeps its case).

const icon = (container: HTMLElement) => container.querySelector('svg') as SVGElement;
const root = (container: HTMLElement) => container.firstElementChild as HTMLElement;

describe('tech-015 Scenario: TypeLabel (design § 5.4)', () => {
  it('draws the type’s icon aria-hidden, and its name as text beside it', () => {
    const { container } = render(<TypeLabel appearance="story" name="История" />);
    expect(screen.getByText('История')).toBeInTheDocument();
    expect(icon(container)).toHaveAttribute('aria-hidden', 'true');
    expect(root(container)).toHaveTextContent(/^История$/);
  });

  it('colours the icon by the appearance token, never the name', () => {
    const { container } = render(<TypeLabel appearance="bug" name="Ошибка" />);
    expect(getComputedStyle(icon(container)).color).toBe(
      rgb(theme.workTracking.typeAppearance('bug').colour),
    );
    expect(getComputedStyle(screen.getByText('Ошибка')).color).not.toBe(
      rgb(theme.workTracking.typeAppearance('bug').colour),
    );
  });

  it('puts one gap, layout.mark.nameGap, between the icon and the name', () => {
    const { container } = render(<TypeLabel appearance="task" name="Задача" />);
    const style = getComputedStyle(root(container));
    expect(style.display).toBe('inline-flex');
    expect(style.columnGap || style.gap).toBe(`${theme.layout.mark.nameGap}px`);
  });

  describe('Scenario: sizes', () => {
    it('default: the 20 px icon at the body type', () => {
      const { container } = render(<TypeLabel appearance="epic" name="Эпик" />);
      expect(px(getComputedStyle(icon(container)).fontSize)).toBe(theme.layout.mark.typeIcon);
      expect(px(getComputedStyle(root(container)).fontSize)).toBe(14);
    });

    it('compact: the table’s 16 px icon at the table-cell type', () => {
      const { container } = render(<TypeLabel appearance="epic" name="Эпик" size="compact" />);
      expect(px(getComputedStyle(icon(container)).fontSize)).toBe(
        theme.layout.mark.typeIconCompact,
      );
      expect(px(getComputedStyle(root(container)).fontSize)).toBe(13);
    });
  });

  describe('Scenario: selected', () => {
    it('takes the icon to primary.dark on a selected row', () => {
      const { container } = render(<TypeLabel appearance="story" name="История" selected />);
      expect(getComputedStyle(icon(container)).color).toBe(rgb(theme.palette.primary.dark));
    });

    it('keeps the type’s own colour when it is not selected', () => {
      const { container } = render(<TypeLabel appearance="story" name="История" />);
      expect(getComputedStyle(icon(container)).color).toBe(
        rgb(theme.workTracking.typeAppearance('story').colour),
      );
    });
  });

  describe('Scenario: the card’s eyebrow', () => {
    it('is capitals by CSS: the DOM text and the accessible name keep their case', () => {
      const { container } = render(<TypeLabel appearance="story" name="User Story" eyebrow />);
      const style = getComputedStyle(root(container));
      expect(style.textTransform).toBe('uppercase');
      expect(root(container).textContent).toBe('User Story');
      expect(screen.getByText('User Story')).toBeInTheDocument();
    });

    it('draws the compact icon at the caption type in primary.dark, with no letter-spacing', () => {
      const { container } = render(<TypeLabel appearance="story" name="История" eyebrow />);
      const style = getComputedStyle(root(container));
      expect(px(getComputedStyle(icon(container)).fontSize)).toBe(
        theme.layout.mark.typeIconCompact,
      );
      expect(px(style.fontSize)).toBe(12);
      expect(style.color).toBe(rgb(theme.palette.primary.dark));
      expect(style.letterSpacing).toBe('normal');
    });

    it('draws no capitals when it is not the eyebrow', () => {
      const { container } = render(<TypeLabel appearance="story" name="История" />);
      expect(getComputedStyle(root(container)).textTransform).not.toBe('uppercase');
    });
  });

  it('answers the neutral fallback for an unknown token and does not throw', () => {
    const { container } = render(<TypeLabel appearance="no-such-token" name="Неизвестный" />);
    expect(icon(container)).not.toBeNull();
    expect(screen.getByText('Неизвестный')).toBeInTheDocument();
  });

  it('lets a long name shrink to one line with an ellipsis', () => {
    render(<TypeLabel appearance="task" name="Очень длинное название типа" />);
    const style = getComputedStyle(screen.getByText('Очень длинное название типа'));
    expect(style.textOverflow).toBe('ellipsis');
    expect(style.whiteSpace).toBe('nowrap');
    expect(style.overflow).toBe('hidden');
  });
});
