import { createRef } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {
  ThinAddIcon,
  ThinChevronRightIcon,
  ThinCloseIcon,
  ThinMoreVerticalIcon,
} from '../icons/ThinIcons';
import { px, rgb, theme } from '../../../test/kitTesting';
import { IconAction, type IconActionSize } from './IconAction';

// tech-015 design § 5.2 «IconAction»: the label is the name and the tooltip, the
// four sizes come from `layout.iconAction`, `inactive` and `busy` keep the button
// focusable, and the host's attributes pass through to the button.

const SIZES: readonly IconActionSize[] = ['row', 'chevron', 'header', 'bar'];

afterEach(() => {
  vi.restoreAllMocks();
});

describe('tech-015 Scenario: IconAction (design § 5.2)', () => {
  describe('Scenario: its box and glyph come from the tokens', () => {
    it.each(SIZES)('size %s has the box and glyph of layout.iconAction', (size) => {
      render(<IconAction label="Ещё действия" icon={<ThinMoreVerticalIcon />} size={size} />);
      const button = screen.getByRole('button', { name: 'Ещё действия' });
      const { box, glyph } = theme.layout.iconAction[size];
      const style = getComputedStyle(button);
      expect(px(style.width)).toBe(box);
      expect(px(style.height)).toBe(box);
      expect(px(style.fontSize)).toBe(glyph);
      // The glyph takes the button's size; nothing sizes it by hand.
      expect(button.querySelector('svg')).toHaveClass('MuiSvgIcon-fontSizeInherit');
    });

    it('is the bar size by default: the card’s toolbar, 32 / 18', () => {
      render(<IconAction label="Обновить" icon={<ThinMoreVerticalIcon />} />);
      const style = getComputedStyle(screen.getByRole('button', { name: 'Обновить' }));
      expect(px(style.width)).toBe(32);
      expect(px(style.fontSize)).toBe(18);
    });

    it('is MUI’s medium button, so the legacy sizeSmall override never applies', () => {
      render(<IconAction label="Закрыть" icon={<ThinCloseIcon />} size="row" />);
      const button = screen.getByRole('button', { name: 'Закрыть' });
      expect(button).not.toHaveClass('MuiIconButton-sizeSmall');
      expect(button.querySelector('svg')).not.toHaveClass('MuiSvgIcon-fontSizeSmall');
    });

    it('keeps every target at 24 px or more (WCAG 2.5.8)', () => {
      for (const size of SIZES) {
        expect(theme.layout.iconAction[size].box).toBeGreaterThanOrEqual(24);
      }
    });
  });

  describe('Scenario: the label is the name and the tooltip', () => {
    it('names the button by its label and hides the glyph from assistive technology', () => {
      render(<IconAction label="Развернуть на страницу" icon={<ThinCloseIcon />} />);
      const button = screen.getByRole('button', { name: 'Развернуть на страницу' });
      expect(button.querySelector('svg')).toHaveAttribute('aria-hidden', 'true');
    });

    // Keyboard focus shows the tooltip in a browser (MUI asks for `:focus-visible`,
    // which jsdom does not answer): the showcase E2E reads it there.
    it('shows the label as a tooltip on hover', async () => {
      const user = userEvent.setup();
      render(<IconAction label="Обновить" icon={<ThinCloseIcon />} />);
      await user.hover(screen.getByRole('button', { name: 'Обновить' }));
      expect(await screen.findByRole('tooltip')).toHaveTextContent('Обновить');
    });

    it('draws no tooltip when it is told not to (a tree chevron, which its row names)', async () => {
      const user = userEvent.setup();
      render(
        <IconAction
          label="Развернуть MSK-EPIC-1"
          icon={<ThinChevronRightIcon />}
          tooltip={false}
        />,
      );
      await user.hover(screen.getByRole('button', { name: 'Развернуть MSK-EPIC-1' }));
      await new Promise((resolve) => setTimeout(resolve, 700));
      expect(screen.queryByRole('tooltip')).toBeNull();
    });
  });

  describe('Scenario: its tone and look', () => {
    it.each([
      ['default', theme.palette.text.primary],
      ['muted', theme.palette.text.secondary],
      ['add', theme.palette.shell.addAction],
    ] as const)('tone %s colours the glyph', (tone, colour) => {
      render(<IconAction label="Добавить" icon={<ThinAddIcon />} tone={tone} />);
      expect(getComputedStyle(screen.getByRole('button', { name: 'Добавить' })).color).toBe(
        rgb(colour),
      );
    });

    it('draws the header size outlined: a 1 px frame in the glyph’s colour on the paper', () => {
      render(<IconAction label="Свернуть" icon={<ThinAddIcon />} size="header" outlined />);
      const style = getComputedStyle(screen.getByRole('button', { name: 'Свернуть' }));
      expect(style.borderTopWidth).toBe(`${theme.layout.frame.thin}px`);
      expect(style.borderTopColor).toBe(rgb(theme.palette.text.primary));
      expect(style.backgroundColor).toBe(rgb(theme.palette.background.paper));
      expect(style.borderRadius).toBe(`${Number(theme.shape.borderRadius) / 2}px`);
    });

    it('draws no frame when it is not outlined, or not the header size', () => {
      render(
        <>
          <IconAction label="Плоская" icon={<ThinAddIcon />} size="header" />
          <IconAction label="Другая" icon={<ThinAddIcon />} size="row" outlined />
        </>,
      );
      for (const name of ['Плоская', 'Другая']) {
        expect(getComputedStyle(screen.getByRole('button', { name })).borderTopWidth).not.toBe(
          '1px',
        );
      }
    });
  });

  describe('Scenario: inactive — nothing to do now, but still there', () => {
    it('is aria-disabled, muted, in the Tab order, and ignores the click', async () => {
      const user = userEvent.setup();
      const onClick = vi.fn();
      render(<IconAction label="Развернуть" icon={<ThinAddIcon />} inactive onClick={onClick} />);
      const button = screen.getByRole('button', { name: 'Развернуть' });
      expect(button).toHaveAttribute('aria-disabled', 'true');
      expect(button).not.toBeDisabled();
      expect(getComputedStyle(button).color).toBe(rgb(theme.palette.text.secondary));
      await user.tab();
      expect(button).toHaveFocus();
      await user.click(button);
      await user.keyboard('{Enter}');
      expect(onClick).not.toHaveBeenCalled();
    });

    it('keeps the outline and the glyph readable: text.secondary, not the disabled grey', () => {
      render(
        <IconAction label="Развернуть" icon={<ThinAddIcon />} size="header" outlined inactive />,
      );
      const style = getComputedStyle(screen.getByRole('button', { name: 'Развернуть' }));
      expect(style.color).toBe(rgb(theme.palette.text.secondary));
      expect(style.borderTopColor).toBe(rgb(theme.palette.text.secondary));
    });
  });

  describe('Scenario: disabled — native, out of the Tab order', () => {
    it('is disabled, text.disabled, and a tooltip on it does not warn', async () => {
      const error = vi.spyOn(console, 'error').mockImplementation(() => {});
      const onClick = vi.fn();
      render(<IconAction label="Сохранить" icon={<ThinAddIcon />} disabled onClick={onClick} />);
      const button = screen.getByRole('button', { name: 'Сохранить' });
      expect(button).toBeDisabled();
      expect(getComputedStyle(button).color).toBe(theme.palette.text.disabled);
      // MUI draws a disabled button with `pointer-events: none`; the click is still tried.
      await userEvent.setup({ pointerEventsCheck: 0 }).click(button);
      expect(onClick).not.toHaveBeenCalled();
      expect(error).not.toHaveBeenCalled();
    });

    it('still shows its tooltip on hover, through a wrapper', async () => {
      const user = userEvent.setup();
      render(<IconAction label="Сохранить" icon={<ThinAddIcon />} disabled />);
      const button = screen.getByRole('button', { name: 'Сохранить' });
      await user.hover(button.parentElement as HTMLElement);
      expect(await screen.findByRole('tooltip')).toHaveTextContent('Сохранить');
    });
  });

  describe('Scenario: busy — the work this button started is running', () => {
    it('swaps the glyph for a spinner, is aria-busy and inert, and stays focusable', async () => {
      const user = userEvent.setup();
      const onClick = vi.fn();
      render(<IconAction label="Обновить" icon={<ThinAddIcon />} busy onClick={onClick} />);
      const button = screen.getByRole('button', { name: 'Обновить' });
      expect(button).toHaveAttribute('aria-busy', 'true');
      expect(button).toHaveAttribute('aria-disabled', 'true');
      expect(button).not.toBeDisabled();
      expect(button.querySelector('svg[data-testid="ThinAddIcon"]')).toBeNull();
      const spinner = screen.getByRole('progressbar');
      expect(button).toContainElement(spinner);
      expect(px(getComputedStyle(spinner).width)).toBe(theme.layout.iconAction.bar.glyph);
      await user.tab();
      expect(button).toHaveFocus();
      await user.click(button);
      expect(onClick).not.toHaveBeenCalled();
    });

    it('is not busy by default: no aria-busy, the glyph is there', () => {
      render(<IconAction label="Обновить" icon={<ThinAddIcon />} />);
      const button = screen.getByRole('button', { name: 'Обновить' });
      expect(button).not.toHaveAttribute('aria-busy');
      expect(button).not.toHaveAttribute('aria-disabled');
      expect(screen.queryByRole('progressbar')).toBeNull();
    });
  });

  describe('Scenario: the host’s attributes and ref pass through', () => {
    it('passes tabIndex, aria-*, data-*, id and the key handlers to the button', async () => {
      const onKeyDown = vi.fn();
      render(
        <IconAction
          label="Ещё действия: MSK-EPIC-1"
          icon={<ThinMoreVerticalIcon />}
          tabIndex={-1}
          id="more-1"
          aria-haspopup="menu"
          aria-expanded={false}
          aria-controls="menu-1"
          data-testid="more"
          onKeyDown={onKeyDown}
        />,
      );
      const button = screen.getByTestId('more');
      expect(button).toBe(screen.getByRole('button', { name: 'Ещё действия: MSK-EPIC-1' }));
      expect(button).toHaveAttribute('tabindex', '-1');
      expect(button).toHaveAttribute('id', 'more-1');
      expect(button).toHaveAttribute('aria-haspopup', 'menu');
      expect(button).toHaveAttribute('aria-expanded', 'false');
      expect(button).toHaveAttribute('aria-controls', 'menu-1');
      button.focus();
      await userEvent.keyboard('{ArrowDown}');
      expect(onKeyDown).toHaveBeenCalledTimes(1);
    });

    it('forwards the ref to the button', () => {
      const ref = createRef<HTMLButtonElement>();
      render(<IconAction ref={ref} label="Закрыть" icon={<ThinCloseIcon />} />);
      expect(ref.current).toBe(screen.getByRole('button', { name: 'Закрыть' }));
    });

    it('calls onClick with the click event', async () => {
      const onClick = vi.fn();
      render(<IconAction label="Закрыть" icon={<ThinCloseIcon />} onClick={onClick} />);
      await userEvent.click(screen.getByRole('button', { name: 'Закрыть' }));
      expect(onClick).toHaveBeenCalledTimes(1);
      expect(onClick.mock.calls[0]?.[0]).toMatchObject({ type: 'click' });
    });
  });
});
