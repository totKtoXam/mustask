import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { ApiError } from '../api/httpClient';
import { ErrorBanner } from './ErrorBanner';

describe('US-7 Scenario: Error with retry — the shared error component takes an optional title', () => {
  it('names what failed above the server’s text', () => {
    render(
      <ErrorBanner
        title="Не удалось загрузить процесс"
        error={new ApiError(500, null)}
        onRetry={() => {}}
      />,
    );

    const alert = screen.getByRole('alert');
    const title = screen.getByText('Не удалось загрузить процесс');
    expect(title).toHaveClass('MuiAlertTitle-root');
    expect(alert).toHaveTextContent('Не удалось загрузить процессОшибка API (HTTP 500).');
    expect(screen.getByRole('button', { name: 'Повторить' })).toBeInTheDocument();
  });

  it('reads as before without a title', () => {
    render(<ErrorBanner error={new ApiError(500, null)} />);

    expect(document.querySelector('.MuiAlertTitle-root')).toBeNull();
    expect(screen.getByRole('alert')).toHaveTextContent('Ошибка API (HTTP 500).');
  });
});
