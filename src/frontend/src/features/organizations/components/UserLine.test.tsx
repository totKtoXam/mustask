import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import type { UserRef } from '../api/membershipApi';
import { plainName } from '../lib/userName';
import { UserLine } from './UserLine';

// mod-016 design § 3.3: one component names a user wherever this task draws
// one, and a plain-text name never carries the mark.

const active: UserRef = {
  id: 'u-1',
  displayName: 'Анна Смирнова',
  email: 'anna@example.org',
  state: 'active',
};
const inactive: UserRef = { ...active, id: 'u-2', displayName: 'Олег Иванов', state: 'inactive' };
const unknown: UserRef = { id: 'u-3', displayName: null, email: null, state: 'unknown' };

describe('mod-016 design § 3.3: UserLine', () => {
  it('shows an active user by the display name alone', () => {
    const { container } = render(<UserLine user={active} />);

    expect(container).toHaveTextContent('Анна Смирнова');
    expect(screen.queryByText('неактивен')).not.toBeInTheDocument();
  });

  it('shows an inactive user’s name and then the mark, as text in an outlined chip', () => {
    const { container } = render(<UserLine user={inactive} />);

    expect(container).toHaveTextContent('Олег Ивановнеактивен');
    const mark = screen.getByText('неактивен');
    expect(mark.closest('.MuiChip-root')).toHaveClass('MuiChip-outlined', 'MuiChip-sizeSmall');
    // The mark follows the name in document order, so it is read after it.
    expect(
      screen.getByText('Олег Иванов').compareDocumentPosition(mark) &
        Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();
  });

  it('shows an unknown user as «Неизвестный пользователь» in secondary text', () => {
    render(<UserLine user={unknown} />);

    const text = screen.getByText('Неизвестный пользователь');
    expect(getComputedStyle(text).color).not.toBe(getComputedStyle(document.body).color);
  });

  it('flows inline without truncation when asked, so a comma can follow the mark', () => {
    const { container } = render(
      <p>
        <UserLine user={inactive} truncate={false} />
        {', 02.10.2026 09:15'}
      </p>,
    );

    expect(container).toHaveTextContent('Олег Ивановнеактивен, 02.10.2026 09:15');
    expect(container.querySelector('.MuiChip-root')).toBeInTheDocument();
  });

  it('names a user in plain text without the mark: the display name, or «Неизвестный пользователь»', () => {
    expect(plainName(active)).toBe('Анна Смирнова');
    expect(plainName(inactive)).toBe('Олег Иванов');
    expect(plainName(unknown)).toBe('Неизвестный пользователь');
  });
});
