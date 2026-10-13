import { useEffect, type ReactNode } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { Link, Route, Routes } from 'react-router-dom';
import { act, fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderWithProviders } from '../../../test/renderWithProviders';
import { theme } from '../../../test/kitTesting';
import { NoticeProvider } from './NoticeProvider';
import type { NoticeApi } from './noticeContext';
import { useNotice } from './useNotice';

// tech-015 design § 5.12 «NoticeProvider and useNotice»: a short result message, bottom left,
// 6 s, one at a time, the same text announced again, a click elsewhere ignored, and a notice
// that survives the change of page that raised it.

function Raise({ onApi }: { onApi: (api: NoticeApi) => void }) {
  const api = useNotice();
  useEffect(() => {
    onApi(api);
  }, [api, onApi]);
  return null;
}

/** Mounts the provider and hands back its `show`. */
function mount(children: ReactNode = null) {
  let api: NoticeApi | undefined;
  render(
    <NoticeProvider>
      <Raise onApi={(value) => (api = value)} />
      {children}
    </NoticeProvider>,
  );
  return {
    show: (text: string, severity?: Parameters<NoticeApi['show']>[1]) =>
      act(() => (api as NoticeApi).show(text, severity)),
    api: () => api as NoticeApi,
  };
}

afterEach(() => {
  vi.useRealTimers();
});

describe('tech-015 Scenario: useNotice and NoticeProvider (design § 5.12)', () => {
  describe('Scenario: a short result message', () => {
    it('shows the text as a plain message that is read out, bottom left', () => {
      const { show } = mount();
      expect(screen.queryByRole('alert')).toBeNull();
      show('Изменения сохранены.');
      const notice = screen.getByRole('alert');
      expect(notice).toHaveTextContent('Изменения сохранены.');
      const snackbar = notice.closest('.MuiSnackbar-root') as HTMLElement;
      expect(snackbar).toHaveClass('MuiSnackbar-anchorOriginBottomLeft');
    });

    it('is plain by default: a message, no alert box', () => {
      const { show } = mount();
      show('Рабочий элемент MSK-US-24 создан.');
      expect(document.querySelector('.MuiAlert-root')).toBeNull();
      expect(document.querySelector('.MuiSnackbarContent-root')).not.toBeNull();
    });

    it.each(['success', 'error'] as const)(
      'draws a %s notice as an Alert of that severity',
      (severity) => {
        const { show } = mount();
        show('Не удалось переместить.', severity);
        const alert = screen.getByRole('alert');
        expect(alert).toHaveClass('MuiAlert-root');
        expect(alert).toHaveClass(`MuiAlert-color${severity === 'success' ? 'Success' : 'Error'}`);
        expect(alert).toHaveClass('MuiAlert-standard');
        expect(alert).toHaveTextContent('Не удалось переместить.');
      },
    );

    it('closes an Alert notice by its «Закрыть»', async () => {
      const user = userEvent.setup();
      const { show } = mount();
      show('Связь добавлена.', 'success');
      await user.click(screen.getByRole('button', { name: 'Закрыть' }));
      expect(screen.queryByRole('alert')).toBeNull();
    });
  });

  describe('Scenario: one at a time', () => {
    it('replaces the shown notice with a newer one, whatever the severities', () => {
      const { show } = mount();
      show('Первое.');
      show('Второе.', 'error');
      expect(screen.getAllByRole('alert')).toHaveLength(1);
      expect(screen.getByRole('alert')).toHaveTextContent('Второе.');
      show('Третье.');
      expect(screen.getAllByRole('alert')).toHaveLength(1);
      expect(screen.getByRole('alert')).toHaveTextContent('Третье.');
    });

    it('announces the same text again: a repeat is a new element', () => {
      const { show } = mount();
      show('Изменения сохранены.');
      const first = screen.getByRole('alert');
      show('Изменения сохранены.');
      const second = screen.getByRole('alert');
      expect(second).toHaveTextContent('Изменения сохранены.');
      expect(second).not.toBe(first);
      expect(first.isConnected).toBe(false);
    });
  });

  describe('Scenario: it goes by itself after theme.noticeDurationMs, and not by a click elsewhere', () => {
    it('is gone after 6 s and not a moment before', () => {
      vi.useFakeTimers();
      const { show } = mount();
      show('Изменения сохранены.');
      act(() => {
        vi.advanceTimersByTime(theme.noticeDurationMs - 1);
      });
      expect(screen.getByRole('alert')).toBeInTheDocument();
      act(() => {
        vi.advanceTimersByTime(1);
      });
      expect(screen.queryByRole('alert')).toBeNull();
      expect(theme.noticeDurationMs).toBe(6000);
    });

    it('starts the 6 s again for a replacement', () => {
      vi.useFakeTimers();
      const { show } = mount();
      show('Первое.');
      act(() => {
        vi.advanceTimersByTime(4000);
      });
      show('Второе.');
      act(() => {
        vi.advanceTimersByTime(4000);
      });
      expect(screen.getByRole('alert')).toHaveTextContent('Второе.');
      act(() => {
        vi.advanceTimersByTime(2000);
      });
      expect(screen.queryByRole('alert')).toBeNull();
    });

    it('ignores a click elsewhere — often the next action', () => {
      const { show } = mount(<button type="button">Следующее действие</button>);
      show('Изменения сохранены.');
      fireEvent.click(screen.getByRole('button', { name: 'Следующее действие' }));
      fireEvent.mouseDown(document.body);
      fireEvent.click(document.body);
      expect(screen.getByRole('alert')).toBeInTheDocument();
    });
  });

  describe('Scenario: it stands above the routes, so it survives a change of page', () => {
    function Pages() {
      const { show } = useNotice();
      return (
        <Routes>
          <Route
            path="/"
            element={
              <>
                <button type="button" onClick={() => show('Рабочий элемент MSK-US-24 создан.')}>
                  Создать
                </button>
                <Link to="/other">К другой странице</Link>
              </>
            }
          />
          <Route path="/other" element={<p>Другая страница</p>} />
        </Routes>
      );
    }

    it('keeps a notice the page that raised it is gone', async () => {
      const user = userEvent.setup();
      renderWithProviders(<Pages />);
      await user.click(screen.getByRole('button', { name: 'Создать' }));
      await user.click(screen.getByRole('link', { name: 'К другой странице' }));
      expect(screen.getByText('Другая страница')).toBeInTheDocument();
      expect(screen.getByRole('alert')).toHaveTextContent('Рабочий элемент MSK-US-24 создан.');
    });
  });

  describe('Scenario: the hook', () => {
    it('keeps the identity of show across renders, so an effect can depend on it', () => {
      const seen: NoticeApi[] = [];
      function Collect() {
        seen.push(useNotice());
        return null;
      }
      const { rerender } = render(
        <NoticeProvider>
          <Collect />
        </NoticeProvider>,
      );
      rerender(
        <NoticeProvider>
          <Collect />
        </NoticeProvider>,
      );
      expect(seen.length).toBeGreaterThan(1);
      expect(new Set(seen.map((api) => api.show)).size).toBe(1);
    });

    it('refuses to run outside a provider, saying what to mount', () => {
      const error = vi.spyOn(console, 'error').mockImplementation(() => {});
      function Orphan() {
        useNotice();
        return null;
      }
      expect(() => render(<Orphan />)).toThrow(/NoticeProvider/);
      error.mockRestore();
    });
  });
});
