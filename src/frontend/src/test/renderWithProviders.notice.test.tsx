import { describe, expect, it } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useNotice } from '../shared/ui';
import { renderWithProviders } from './renderWithProviders';

// tech-015 design § 14: `renderWithProviders` mounts the kit's `NoticeProvider`, as `AppLayout`
// does in the product, so a page under test can call `useNotice().show(...)`.

function Page() {
  const { show } = useNotice();
  return (
    <button type="button" onClick={() => show('Рабочий элемент MSK-US-24 создан.')}>
      Создать
    </button>
  );
}

describe('tech-015 Scenario: renderWithProviders mounts the notice host', () => {
  it('draws the notice a page raises', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Page />);
    await user.click(screen.getByRole('button', { name: 'Создать' }));
    expect(screen.getByRole('alert')).toHaveTextContent('Рабочий элемент MSK-US-24 создан.');
  });

  it('draws it for a page under the `wrapper` too', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Page />, {
      wrapper: (children) => <div data-testid="wrapped">{children}</div>,
    });
    await user.click(screen.getByRole('button', { name: 'Создать' }));
    expect(screen.getByTestId('wrapped')).toContainElement(
      screen.getByRole('button', { name: 'Создать' }),
    );
    expect(screen.getByRole('alert')).toBeInTheDocument();
  });
});
