import { createRef } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { InlineNotice } from './InlineNotice';

// tech-015 design § 5.12 «InlineNotice»: an error, a warning or a note that stays in the layout;
// `error` is `role="alert"`, the other two `role="status"`; the action is a subtle compact
// button; it owns no margin.

describe('tech-015 Scenario: InlineNotice (design § 5.12)', () => {
  describe('Scenario: roles by severity', () => {
    it('is role alert for an error: a failure interrupts', () => {
      render(<InlineNotice severity="error">Не удалось загрузить рабочий элемент.</InlineNotice>);
      const notice = screen.getByRole('alert');
      expect(notice).toHaveTextContent('Не удалось загрузить рабочий элемент.');
      expect(notice).toHaveClass('MuiAlert-colorError');
      expect(notice).toHaveClass('MuiAlert-standard');
    });

    it.each([
      ['warning', 'MuiAlert-colorWarning'],
      ['info', 'MuiAlert-colorInfo'],
    ] as const)('is role status for %s: a note does not interrupt', (severity, className) => {
      render(<InlineNotice severity={severity}>Запись только для чтения.</InlineNotice>);
      const notice = screen.getByRole('status');
      expect(notice).toHaveTextContent('Запись только для чтения.');
      expect(notice).toHaveClass(className);
      expect(screen.queryByRole('alert')).toBeNull();
    });
  });

  describe('Scenario: a title names what failed above the text', () => {
    it('draws the title, then the text, in one notice', () => {
      render(
        <InlineNotice severity="error" title="Не удалось загрузить процесс">
          Сервер не отвечает.
        </InlineNotice>,
      );
      const notice = screen.getByRole('alert');
      expect(within(notice).getByText('Не удалось загрузить процесс')).toBeInTheDocument();
      expect(notice.textContent).toBe('Не удалось загрузить процессСервер не отвечает.');
    });

    it('draws no title element without one', () => {
      render(<InlineNotice severity="info">Текст</InlineNotice>);
      expect(document.querySelector('.MuiAlertTitle-root')).toBeNull();
    });
  });

  describe('Scenario: the action', () => {
    it('is the host’s label on a subtle compact button inside the notice', async () => {
      const user = userEvent.setup();
      const onClick = vi.fn();
      render(
        <InlineNotice severity="error" action={{ label: 'Повторить', onClick }}>
          Сервер не отвечает.
        </InlineNotice>,
      );
      const button = within(screen.getByRole('alert')).getByRole('button', { name: 'Повторить' });
      expect(button).toHaveClass('MuiButton-text');
      expect(parseFloat(getComputedStyle(button).minHeight)).toBe(0);
      await user.click(button);
      expect(onClick).toHaveBeenCalledOnce();
    });

    it('hands a ref to the button, for a page that moves focus back to it', () => {
      const ref = createRef<HTMLButtonElement>();
      render(
        <InlineNotice severity="error" action={{ label: 'Повторить', onClick: () => {}, ref }}>
          Сервер не отвечает.
        </InlineNotice>,
      );
      expect(ref.current).toBe(screen.getByRole('button', { name: 'Повторить' }));
    });

    it('draws no button without an action', () => {
      render(<InlineNotice severity="error">Текст</InlineNotice>);
      expect(screen.queryByRole('button')).toBeNull();
    });
  });

  describe('Scenario: it owns no margin — the container spaces it', () => {
    it.each(['error', 'warning', 'info'] as const)(
      'has a computed margin of 0 on every side (%s)',
      (severity) => {
        render(<InlineNotice severity={severity}>Текст</InlineNotice>);
        const notice = screen.getByRole(severity === 'error' ? 'alert' : 'status');
        for (const side of ['marginTop', 'marginRight', 'marginBottom', 'marginLeft'] as const) {
          expect(parseFloat(getComputedStyle(notice)[side] || '0')).toBe(0);
        }
      },
    );
  });
});
