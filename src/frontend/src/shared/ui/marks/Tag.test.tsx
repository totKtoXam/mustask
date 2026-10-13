import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { px, rgb, theme } from '../../../test/kitTesting';
import { Tag, type TagTone } from './Tag';

// tech-015 design § 5.4 «Tag»: MUI's small Chip in four tones, filled or outlined; a
// remove button named «Удалить тег «<label>»» only when the host can remove it.

const chip = (container: HTMLElement) => container.querySelector('.MuiChip-root') as HTMLElement;

describe('tech-015 Scenario: Tag (design § 5.4)', () => {
  it('is a small pill with its label as text', () => {
    const { container } = render(<Tag label="Discovery" />);
    expect(chip(container)).toHaveClass('MuiChip-sizeSmall');
    expect(chip(container)).toHaveTextContent(/^Discovery$/);
  });

  it('draws the label at the table-cell type, 13 px', () => {
    render(<Tag label="Discovery" />);
    expect(px(getComputedStyle(screen.getByText('Discovery')).fontSize)).toBe(13);
  });

  describe('Scenario: tones', () => {
    it('neutral (default) is the grey pill: buttonNeutral under text.primary', () => {
      const { container } = render(<Tag label="Functional" />);
      const style = getComputedStyle(chip(container));
      expect(style.backgroundColor).toBe(rgb(theme.palette.shell.buttonNeutral));
      expect(style.color).toBe(rgb(theme.palette.text.primary));
    });

    it('accent is the pale-blue pill: rowSelected under primary.dark', () => {
      const { container } = render(<Tag label="UseCases" tone="accent" />);
      const style = getComputedStyle(chip(container));
      expect(style.backgroundColor).toBe(rgb(theme.palette.shell.rowSelected));
      expect(style.color).toBe(rgb(theme.palette.primary.dark));
    });

    it.each([
      ['success', 'MuiChip-colorSuccess'],
      ['warning', 'MuiChip-colorWarning'],
    ] as const)('%s takes MUI’s own tone', (tone: TagTone, className) => {
      const { container } = render(<Tag label="Активен" tone={tone} />);
      expect(chip(container)).toHaveClass(className, 'MuiChip-filled');
    });

    it('outlined draws a 1 px frame and no fill, in every tone', () => {
      const { container } = render(
        <>
          <Tag label="Раз" variant="outlined" />
          <Tag label="Два" variant="outlined" tone="accent" />
          <Tag label="Три" variant="outlined" tone="success" />
        </>,
      );
      for (const element of container.querySelectorAll('.MuiChip-root')) {
        expect(element).toHaveClass('MuiChip-outlined');
        expect(getComputedStyle(element).borderTopWidth).toBe('1px');
      }
    });
  });

  describe('Scenario: it has no remove button unless it is given one', () => {
    it('is not interactive: no button, no tab stop, and not a role of its own', async () => {
      const { container } = render(<Tag label="Discovery" />);
      expect(screen.queryByRole('button')).toBeNull();
      expect(chip(container)).not.toHaveAttribute('tabindex');
      expect(chip(container)).not.toHaveAttribute('role');
      await userEvent.tab();
      expect(document.body).toHaveFocus();
    });
  });

  describe('Scenario: onRemove', () => {
    it('adds a remove button named «Удалить тег «<label>»» with the thin cross', () => {
      render(<Tag label="Discovery" onRemove={() => {}} />);
      const button = screen.getByRole('button', { name: 'Удалить тег «Discovery»' });
      expect(button.querySelector('[data-testid="ThinCloseIcon"]')).not.toBeNull();
    });

    it('is not a button with a button inside it: the pill itself stays plain', () => {
      const { container } = render(<Tag label="Discovery" onRemove={() => {}} />);
      expect(chip(container)).not.toHaveAttribute('role', 'button');
      expect(screen.getAllByRole('button')).toHaveLength(1);
    });

    it('keeps the label as the tag’s text, apart from the button’s name', () => {
      const { container } = render(<Tag label="Discovery" onRemove={() => {}} />);
      expect(chip(container).textContent).toBe('Discovery');
    });

    it('calls onRemove on a click and on Enter, and takes the focus ring from the button', async () => {
      const user = userEvent.setup();
      const onRemove = vi.fn();
      render(<Tag label="Discovery" onRemove={onRemove} />);
      const button = screen.getByRole('button', { name: 'Удалить тег «Discovery»' });
      await user.click(button);
      expect(onRemove).toHaveBeenCalledTimes(1);
      button.focus();
      await user.keyboard('{Enter}');
      expect(onRemove).toHaveBeenCalledTimes(2);
    });

    it('keeps the remove button a target of at least 24 px', () => {
      render(<Tag label="Discovery" onRemove={() => {}} />);
      const style = getComputedStyle(
        screen.getByRole('button', { name: 'Удалить тег «Discovery»' }),
      );
      expect(px(style.width)).toBeGreaterThanOrEqual(24);
      expect(px(style.height)).toBeGreaterThanOrEqual(24);
    });
  });
});
