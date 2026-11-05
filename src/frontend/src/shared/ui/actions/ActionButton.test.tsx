import { createRef } from 'react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ThinAddIcon } from '../icons/ThinIcons';
import { px, rgb, theme } from '../../../test/kitTesting';
import { ActionButton, type ActionTone } from './ActionButton';

// tech-015 design § 5.2 «ActionButton»: four tones on MUI's Button, a busy button
// that stays focusable, a menu trigger, a link.

const TONES: readonly [ActionTone, string, string][] = [
  ['primary', 'MuiButton-contained', 'MuiButton-colorPrimary'],
  ['neutral', 'MuiButton-contained', 'MuiButton-colorInherit'],
  ['subtle', 'MuiButton-text', 'MuiButton-colorInherit'],
  ['danger', 'MuiButton-contained', 'MuiButton-colorError'],
];

describe('tech-015 Scenario: ActionButton (design § 5.2)', () => {
  describe('Scenario: four tones', () => {
    it.each(TONES)('tone %s is MUI’s %s %s', (tone, variantClass, colourClass) => {
      render(<ActionButton label="Сохранить" tone={tone} />);
      const button = screen.getByRole('button', { name: 'Сохранить' });
      expect(button).toHaveClass(variantClass, colourClass);
    });

    it('is neutral by default — blue is deliberate', () => {
      render(<ActionButton label="Отмена" />);
      expect(screen.getByRole('button', { name: 'Отмена' })).toHaveClass('MuiButton-colorInherit');
    });

    it('is named by its label and is a button that does not submit unless told to', () => {
      render(
        <form>
          <ActionButton label="Закрыть" />
        </form>,
      );
      expect(screen.getByRole('button', { name: 'Закрыть' })).toHaveAttribute('type', 'button');
    });

    it('draws the neutral tone on the reference’s grey and the subtle one without a fill', () => {
      render(
        <>
          <ActionButton label="Серая" tone="neutral" />
          <ActionButton label="Тихая" tone="subtle" />
        </>,
      );
      const neutral = getComputedStyle(screen.getByRole('button', { name: 'Серая' }));
      expect(neutral.getPropertyValue('--variant-containedBg')).toBe(
        theme.palette.shell.buttonNeutral,
      );
      expect(neutral.getPropertyValue('--variant-containedColor')).toBe(theme.palette.text.primary);
      const subtle = getComputedStyle(screen.getByRole('button', { name: 'Тихая' }));
      expect(subtle.color).toBe(rgb(theme.palette.text.primary));
    });

    it('is as high as a control, and a compact one has no minimum', () => {
      render(
        <>
          <ActionButton label="Обычная" />
          <ActionButton label="Тесная" size="compact" />
        </>,
      );
      expect(px(getComputedStyle(screen.getByRole('button', { name: 'Обычная' })).minHeight)).toBe(
        theme.density.controlHeight,
      );
      expect(
        parseFloat(getComputedStyle(screen.getByRole('button', { name: 'Тесная' })).minHeight),
      ).toBe(0);
    });

    it('draws a leading icon at the bar’s glyph size', () => {
      render(<ActionButton label="Добавить" icon={<ThinAddIcon />} />);
      const button = screen.getByRole('button', { name: 'Добавить' });
      expect(button.querySelector('[data-testid="ThinAddIcon"]')).not.toBeNull();
    });
  });

  describe('Scenario: busy — the work this button started is running', () => {
    it('swaps the icon for a spinner, is aria-busy and aria-disabled, and is not disabled', () => {
      render(<ActionButton label="Сохранить" icon={<ThinAddIcon />} busy />);
      const button = screen.getByRole('button', { name: 'Сохранить' });
      expect(button).toHaveAttribute('aria-busy', 'true');
      expect(button).toHaveAttribute('aria-disabled', 'true');
      expect(button).not.toBeDisabled();
      expect(button.querySelector('[data-testid="ThinAddIcon"]')).toBeNull();
      const spinner = screen.getByRole('progressbar');
      expect(spinner).toBeInTheDocument();
      expect(px(getComputedStyle(spinner).width)).toBe(theme.layout.inlineSpinner);
    });

    it('shows the spinner where an icon would stand when there is no icon', () => {
      render(<ActionButton label="Повторить" busy />);
      expect(screen.getByRole('button', { name: 'Повторить' })).toContainElement(
        screen.getByRole('progressbar'),
      );
    });

    it('keeps the focus and ignores a click and the Enter key', async () => {
      const user = userEvent.setup();
      const onClick = vi.fn();
      render(<ActionButton label="Сохранить" busy onClick={onClick} />);
      const button = screen.getByRole('button', { name: 'Сохранить' });
      await user.tab();
      expect(button).toHaveFocus();
      await user.click(button);
      await user.keyboard('{Enter}');
      expect(onClick).not.toHaveBeenCalled();
      expect(button).toHaveFocus();
    });

    it('does not submit its form while busy, and does when it is not', async () => {
      const user = userEvent.setup();
      const onSubmit = vi.fn((event: { preventDefault: () => void }) => event.preventDefault());
      const { rerender } = render(
        <form onSubmit={onSubmit}>
          <ActionButton label="Сохранить" type="submit" busy />
        </form>,
      );
      await user.click(screen.getByRole('button', { name: 'Сохранить' }));
      expect(onSubmit).not.toHaveBeenCalled();
      rerender(
        <form onSubmit={onSubmit}>
          <ActionButton label="Сохранить" type="submit" />
        </form>,
      );
      await user.click(screen.getByRole('button', { name: 'Сохранить' }));
      expect(onSubmit).toHaveBeenCalledTimes(1);
    });

    it('keeps its tone’s fill, where a disabled button loses it', () => {
      render(
        <>
          <ActionButton label="Занята" tone="primary" busy />
          <ActionButton label="Недоступна" tone="primary" disabled />
        </>,
      );
      const busy = getComputedStyle(screen.getByRole('button', { name: 'Занята' }));
      expect(busy.getPropertyValue('--variant-containedBg')).toBe(theme.palette.primary.main);
      expect(screen.getByRole('button', { name: 'Занята' })).not.toHaveClass('Mui-disabled');
      expect(screen.getByRole('button', { name: 'Недоступна' })).toHaveClass('Mui-disabled');
    });
  });

  describe('Scenario: disabled — unavailable for a reason of state', () => {
    it('is natively disabled, leaves the Tab order and does nothing', async () => {
      // MUI draws a disabled button with `pointer-events: none`; the click is still tried.
      const user = userEvent.setup({ pointerEventsCheck: 0 });
      const onClick = vi.fn();
      render(<ActionButton label="Сохранить" disabled onClick={onClick} />);
      const button = screen.getByRole('button', { name: 'Сохранить' });
      expect(button).toBeDisabled();
      await user.tab();
      expect(button).not.toHaveFocus();
      await user.click(button);
      expect(onClick).not.toHaveBeenCalled();
    });
  });

  describe('Scenario: it opens a menu', () => {
    it('announces the menu, and a thin chevron stands after the label', () => {
      render(<ActionButton label="Новый рабочий элемент" menu={{ open: false }} />);
      const button = screen.getByRole('button', { name: 'Новый рабочий элемент' });
      expect(button).toHaveAttribute('aria-haspopup', 'menu');
      expect(button).toHaveAttribute('aria-expanded', 'false');
      expect(button.querySelector('[data-testid="ThinChevronDownIcon"]')).not.toBeNull();
    });

    it('says it is expanded while the menu is open', () => {
      render(<ActionButton label="Новый рабочий элемент" menu={{ open: true }} />);
      expect(screen.getByRole('button', { name: 'Новый рабочий элемент' })).toHaveAttribute(
        'aria-expanded',
        'true',
      );
    });

    it('keeps the announcement and draws no chevron when hideIndicator is set', () => {
      render(<ActionButton label="Тип" menu={{ open: false, hideIndicator: true }} />);
      const button = screen.getByRole('button', { name: 'Тип' });
      expect(button).toHaveAttribute('aria-haspopup', 'menu');
      expect(button.querySelector('[data-testid="ThinChevronDownIcon"]')).toBeNull();
    });

    it('announces no menu when it opens none', () => {
      render(<ActionButton label="Закрыть" />);
      const button = screen.getByRole('button', { name: 'Закрыть' });
      expect(button).not.toHaveAttribute('aria-haspopup');
      expect(button).not.toHaveAttribute('aria-expanded');
    });
  });

  describe('Scenario: it is a link', () => {
    it('renders a link to the address when `to` is given', async () => {
      render(
        <MemoryRouter>
          <ActionButton label="К списку" to="/organizations" />
        </MemoryRouter>,
      );
      const link = screen.getByRole('link', { name: 'К списку' });
      expect(link).toHaveAttribute('href', '/organizations');
      expect(link).toHaveClass('MuiButton-root');
      expect(screen.queryByRole('button')).toBeNull();
    });
  });

  describe('Scenario: the host’s attributes and ref pass through', () => {
    it('passes aria-*, data-*, id and tabIndex to the button, and the ref', () => {
      const ref = createRef<HTMLButtonElement>();
      render(
        <ActionButton
          ref={ref}
          label="Повторить"
          id="retry"
          tabIndex={-1}
          aria-describedby="why"
          data-testid="retry-button"
        />,
      );
      const button = screen.getByTestId('retry-button');
      expect(ref.current).toBe(button);
      expect(button).toHaveAttribute('id', 'retry');
      expect(button).toHaveAttribute('tabindex', '-1');
      expect(button).toHaveAttribute('aria-describedby', 'why');
    });

    it('calls onClick with the event', async () => {
      const onClick = vi.fn();
      render(<ActionButton label="Закрыть" onClick={onClick} />);
      await userEvent.click(screen.getByRole('button', { name: 'Закрыть' }));
      expect(onClick).toHaveBeenCalledTimes(1);
    });
  });
});
