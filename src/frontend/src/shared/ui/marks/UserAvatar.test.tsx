import { describe, expect, it } from 'vitest';
import { render } from '@testing-library/react';
import { px, theme } from '../../../test/kitTesting';
import { UserAvatar } from './UserAvatar';

// tech-015 design § 5.4 «UserAvatar»: initials in MUI's Avatar, 32 px (`menu`) or 24 px
// (`inline`), aria-hidden — the name beside it is what is read.

const avatar = (container: HTMLElement) => container.firstElementChild as HTMLElement;

describe('tech-015 Scenario: UserAvatar (design § 5.4)', () => {
  it('shows the initials of the first two words and is aria-hidden', () => {
    const { container } = render(<UserAvatar name="Иван Петров" />);
    expect(avatar(container)).toHaveAttribute('aria-hidden', 'true');
    expect(avatar(container)).toHaveTextContent(/^ИП$/);
    expect(avatar(container)).toHaveClass('MuiAvatar-root');
  });

  it('shows one letter for a one-word name', () => {
    const { container } = render(<UserAvatar name="e2e-fixture" />);
    expect(avatar(container)).toHaveTextContent(/^E$/);
  });

  it.each([
    ['menu', theme.layout.avatarSize],
    ['inline', theme.layout.mark.avatarInline],
  ] as const)('size %s is %i px', (size, expected) => {
    const { container } = render(<UserAvatar name="Иван Петров" size={size} />);
    const style = getComputedStyle(avatar(container));
    expect(px(style.width)).toBe(expected);
    expect(px(style.height)).toBe(expected);
  });

  it('is the 24 px inline avatar by default', () => {
    const { container } = render(<UserAvatar name="Иван Петров" />);
    expect(px(getComputedStyle(avatar(container)).width)).toBe(24);
  });

  it('sets the initials at the avatar type, 13 px semibold', () => {
    const { container } = render(<UserAvatar name="Иван Петров" size="menu" />);
    const style = getComputedStyle(avatar(container));
    expect(px(style.fontSize)).toBe(13);
    expect(style.fontWeight).toBe('600');
  });

  it('shows the thin person glyph when the name has no initials', () => {
    const { container } = render(<UserAvatar name="   " />);
    expect(avatar(container).querySelector('[data-testid="ThinPersonIcon"]')).not.toBeNull();
    expect(avatar(container).textContent).toBe('');
  });
});
