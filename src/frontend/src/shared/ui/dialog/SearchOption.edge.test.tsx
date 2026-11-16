import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SearchOption } from './SearchOption';

// feat-070 QA: option edges.

describe('feat-070 QA: SearchOption edges', () => {
  it('is not selected by default', () => {
    render(
      <MemoryRouter>
        <ul role="listbox" aria-label="Р">
          <SearchOption id="a" to="/a" primary="Строка" />
        </ul>
      </MemoryRouter>,
    );
    const option = screen.getByRole('option');
    expect(option).toHaveAttribute('aria-selected', 'false');
    expect(option).not.toHaveAttribute('data-active');
  });

  it('navigates by the router when the link is clicked', async () => {
    const user = userEvent.setup();
    render(
      <MemoryRouter initialEntries={['/start']}>
        <Routes>
          <Route
            path="/start"
            element={
              <ul role="listbox" aria-label="Р">
                <SearchOption id="a" to="/target" primary="Строка" />
              </ul>
            }
          />
          <Route path="/target" element={<p>Цель</p>} />
        </Routes>
      </MemoryRouter>,
    );
    await user.click(screen.getByRole('option'));
    expect(screen.getByText('Цель')).toBeInTheDocument();
  });

  it('renders a result without `to` as a non-link option', () => {
    render(
      <MemoryRouter>
        <ul role="listbox" aria-label="Р">
          <SearchOption id="a" primary="Без адреса" />
        </ul>
      </MemoryRouter>,
    );
    expect(screen.getByRole('option')).not.toHaveAttribute('href');
  });

  it('the more variant shows the enter hint and aria-selected when active', () => {
    render(
      <MemoryRouter>
        <ul role="listbox" aria-label="Р">
          <SearchOption id="m" variant="more" primary="Ещё" active />
        </ul>
      </MemoryRouter>,
    );
    const option = screen.getByRole('option');
    expect(option).toHaveAttribute('aria-selected', 'true');
    expect(getComputedStyle(option.querySelector('kbd') as HTMLElement).visibility).toBe('visible');
  });
});
