import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AssigneeAvatar } from './Marks';

// feat-032 design § 4.2 (BR-C-6): the assignee as an avatar alone, named by the same words
// as the list’s «Исполнитель». US-1 Scenario: A row shows what the reference shows /
// An assignee the directory does not know.

describe('AssigneeAvatar', () => {
  it('shows the initials of a known person and names the avatar by the person', () => {
    render(
      <AssigneeAvatar
        assignee={{ id: 'u1', displayName: 'Анна Смирнова', email: 'anna@example.com' }}
      />,
    );

    const avatar = screen.getByRole('img', { name: 'Анна Смирнова' });
    expect(avatar).toHaveTextContent('АС');
  });

  it('shows the neutral person icon named «Не назначен» for nobody', () => {
    render(<AssigneeAvatar assignee={null} />);

    const avatar = screen.getByRole('img', { name: 'Не назначен' });
    expect(avatar.querySelector('[data-testid="ThinPersonIcon"]')).not.toBeNull();
  });

  it('shows the neutral person icon named «Неизвестный пользователь» when the directory does not know the name', () => {
    render(<AssigneeAvatar assignee={{ id: 'u9', displayName: null, email: null }} />);

    const avatar = screen.getByRole('img', { name: 'Неизвестный пользователь' });
    expect(avatar.querySelector('[data-testid="ThinPersonIcon"]')).not.toBeNull();
  });

  it('carries the name in a tooltip for the pointer and is not a tab stop', async () => {
    render(
      <AssigneeAvatar
        assignee={{ id: 'u1', displayName: 'Анна Смирнова', email: 'anna@example.com' }}
      />,
    );
    const avatar = screen.getByRole('img', { name: 'Анна Смирнова' });

    await userEvent.hover(avatar);

    expect(await screen.findByRole('tooltip')).toHaveTextContent('Анна Смирнова');
    expect(avatar).not.toHaveAttribute('tabindex');
  });
});
