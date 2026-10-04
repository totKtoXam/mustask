import { createRef } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ApiError } from '../api/httpClient';
import { ErrorBanner } from './ErrorBanner';

describe('ErrorBanner', () => {
  it('рендерится без кнопки повтора, если onRetry не передан (существующие вызовы)', () => {
    render(<ErrorBanner error={new ApiError(500, null)} />);

    expect(screen.getByText('Ошибка API (HTTP 500).')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Повторить' })).not.toBeInTheDocument();
  });

  it('рендерит кнопку "Повторить" только когда передан onRetry', () => {
    const onRetry = vi.fn();
    render(<ErrorBanner error={new ApiError(500, null)} onRetry={onRetry} />);

    expect(screen.getByRole('button', { name: 'Повторить' })).toBeInTheDocument();
  });

  it('вызывает onRetry по клику на "Повторить"', async () => {
    const onRetry = vi.fn();
    const user = userEvent.setup();
    render(<ErrorBanner error={new ApiError(500, null)} onRetry={onRetry} />);

    await user.click(screen.getByRole('button', { name: 'Повторить' }));

    expect(onRetry).toHaveBeenCalledTimes(1);
  });

  // mod-001 plan-frontend § 11 P-2 (a): a page moves focus back to «Повторить»
  // after a retry that failed again.
  it('hands «Повторить» to retryRef when one is given, and renders as before without it', () => {
    const retryRef = createRef<HTMLButtonElement>();
    const { unmount } = render(
      <ErrorBanner error={new ApiError(500, null)} onRetry={vi.fn()} retryRef={retryRef} />,
    );

    expect(retryRef.current).toBe(screen.getByRole('button', { name: 'Повторить' }));
    unmount();

    render(<ErrorBanner error={new ApiError(500, null)} onRetry={vi.fn()} />);
    expect(screen.getByRole('button', { name: 'Повторить' })).toBeInTheDocument();
  });
});
