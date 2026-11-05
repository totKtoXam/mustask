import { describe, expect, it, vi } from 'vitest';
import { useState } from 'react';
import { MemoryRouter } from 'react-router-dom';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import InboxOutlinedIcon from '@mui/icons-material/InboxOutlined';
import { LANGUAGES_READ, authValue, renderShell } from '../../test/renderShell';
import { px } from '../../test/kitTesting';
import { Route } from 'react-router-dom';
import { DialogShell } from './dialog/DialogShell';
import { PageChromeContext, type PageChrome } from './pageChrome';
import { NotFound, STATE_FRAME_CLASS, StateFrame } from './StateFrame';

// tech-015 design § 5.12 «StateFrame, AccessDenied, NotFound»: the additions for the card —
// `StateFrame compact` (pt 2, pb 2, an h2, no tab title) and `NotFound({ compact, hint, action })`,
// which leaves the page behind it alone.

function frame(container: HTMLElement): HTMLElement {
  return container.querySelector(`.${STATE_FRAME_CLASS}`) as HTMLElement;
}

describe('tech-015 Scenario: StateFrame compact (design § 5.12)', () => {
  it('is an h2 with no change to the tab, whatever fullPage says', () => {
    document.title = 'Бэклоги — Mustask';
    render(
      <StateFrame icon={InboxOutlinedIcon} heading="Дочерних элементов нет" fullPage compact />,
    );
    expect(
      screen.getByRole('heading', { level: 2, name: 'Дочерних элементов нет' }),
    ).toBeInTheDocument();
    expect(screen.queryByRole('heading', { level: 1 })).toBeNull();
    expect(document.title).toBe('Бэклоги — Mustask');
  });

  it('stands 16 px under its top and above its foot, not the page frame’s 64 and 32', () => {
    const compact = render(<StateFrame icon={InboxOutlinedIcon} heading="Пусто" compact />);
    const style = getComputedStyle(frame(compact.container));
    expect(px(style.paddingTop)).toBe(16);
    expect(px(style.paddingBottom)).toBe(16);
    compact.unmount();

    const page = render(<StateFrame icon={InboxOutlinedIcon} heading="Пусто" />);
    const pageStyle = getComputedStyle(frame(page.container));
    expect(px(pageStyle.paddingTop)).toBe(64);
    expect(px(pageStyle.paddingBottom)).toBe(32);
  });

  it('keeps the canon: the hint and one action, centred', async () => {
    const user = userEvent.setup();
    const onClick = vi.fn();
    render(
      <StateFrame
        icon={InboxOutlinedIcon}
        heading="Пусто"
        hint="Добавьте первое."
        action={{ label: 'Добавить', onClick }}
        compact
      />,
    );
    expect(screen.getByText('Добавьте первое.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Добавить' }));
    expect(onClick).toHaveBeenCalledOnce();
  });

  it('is unchanged without it: a full page keeps its h1 and its tab', () => {
    render(<StateFrame icon={InboxOutlinedIcon} heading="Доступ ограничен" fullPage />);
    expect(screen.getByRole('heading', { level: 1, name: 'Доступ ограничен' })).toBeInTheDocument();
    expect(document.title).toBe('Доступ ограничен — Mustask');
  });
});

describe('tech-015 Scenario: NotFound overrides (design § 5.12)', () => {
  function renderNotFound(ui: React.ReactElement) {
    return render(<MemoryRouter>{ui}</MemoryRouter>);
  }

  it('says the host’s hint and takes the host’s action — the dialog’s «Закрыть»', async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();
    renderNotFound(
      <NotFound
        level="workItem"
        compact
        hint="Проверьте адрес или закройте окно."
        action={{ label: 'Закрыть', onClick: onClose }}
      />,
    );
    expect(
      screen.getByRole('heading', { level: 2, name: 'Рабочий элемент не найден' }),
    ).toBeInTheDocument();
    expect(screen.getByText('Проверьте адрес или закройте окно.')).toBeInTheDocument();
    expect(screen.queryByText('Проверьте адрес или вернитесь на главную.')).toBeNull();
    expect(screen.queryByRole('link', { name: 'На главную' })).toBeNull();
    await user.click(screen.getByRole('button', { name: 'Закрыть' }));
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('keeps the canon’s hint and «На главную» when the host gives neither', () => {
    renderNotFound(<NotFound level="team" />);
    expect(screen.getByText('Проверьте адрес или вернитесь на главную.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'На главную' })).toHaveAttribute('href', '/home');
  });

  it('overrides on a full page too: the page keeps its h1 and its tab', () => {
    renderNotFound(
      <NotFound level="page" hint="Свой текст." action={{ label: 'Назад', to: '/back' }} />,
    );
    expect(
      screen.getByRole('heading', { level: 1, name: 'Страница не найдена' }),
    ).toBeInTheDocument();
    expect(screen.getByText('Свой текст.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Назад' })).toHaveAttribute('href', '/back');
    expect(document.title).toBe('Страница не найдена — Mustask');
  });
});

describe('tech-015 Scenario: NotFound compact leaves the page behind it alone (design § 5.12)', () => {
  function chrome(registerNotFound: () => () => void): PageChrome {
    return {
      registerHeader: () => () => {},
      registerNotFound,
      trail: [],
      trailPlacement: 'topBar',
    };
  }

  it('never registers not-found with the shell, and is an h2 with no tab title', () => {
    document.title = 'Бэклоги — Mustask';
    const registerNotFound = vi.fn(() => () => {});
    render(
      <MemoryRouter>
        <PageChromeContext.Provider value={chrome(registerNotFound)}>
          <NotFound level="workItem" compact />
        </PageChromeContext.Provider>
      </MemoryRouter>,
    );
    expect(registerNotFound).not.toHaveBeenCalled();
    expect(screen.getByRole('heading', { level: 2 })).toBeInTheDocument();
    expect(document.title).toBe('Бэклоги — Mustask');
  });

  it('registers when it is not compact, as before', () => {
    const registerNotFound = vi.fn(() => () => {});
    render(
      <MemoryRouter>
        <PageChromeContext.Provider value={chrome(registerNotFound)}>
          <NotFound level="workItem" />
        </PageChromeContext.Provider>
      </MemoryRouter>,
    );
    expect(registerNotFound).toHaveBeenCalledOnce();
    expect(screen.getByRole('heading', { level: 1 })).toBeInTheDocument();
  });

  describe('Scenario: inside a DialogShell, in the shell', () => {
    function CardPage({ compact }: { compact: boolean }) {
      const [open, setOpen] = useState(false);
      return (
        <>
          <button type="button" onClick={() => setOpen(true)}>
            Открыть окно карточки
          </button>
          <DialogShell
            open={open}
            size="card"
            label="Рабочий элемент MSK-US-99"
            onClose={() => setOpen(false)}
          >
            <NotFound
              level="workItem"
              compact={compact}
              hint="Проверьте адрес или закройте окно."
              action={{ label: 'Закрыть окно', onClick: () => setOpen(false) }}
            />
          </DialogShell>
        </>
      );
    }

    function shell(compact: boolean) {
      return renderShell({
        path: '/dictionaries/languages',
        auth: authValue([LANGUAGES_READ]),
        routes: <Route path="/dictionaries/languages" element={<CardPage compact={compact} />} />,
      });
    }

    async function snapshot() {
      // The dialog makes the shell inert (aria-hidden), but its state is still the shell's.
      const main = screen.getByRole('navigation', { name: 'Основное меню', hidden: true });
      return {
        title: document.title,
        settingsNavigation:
          screen.queryByRole('navigation', { name: 'Меню настроек', hidden: true }) !== null,
        current: within(main)
          .queryAllByRole('link', { hidden: true })
          .filter((link) => link.getAttribute('aria-current'))
          .map((link) => link.textContent),
      };
    }

    it('leaves the tab’s title, the navigation’s current entry and the settings navigation as they were', async () => {
      const user = userEvent.setup();
      shell(true);
      await screen.findByRole('button', { name: 'Открыть окно карточки' });
      await waitFor(() => expect(document.title).toContain('Mustask'));
      const before = await snapshot();
      expect(before.settingsNavigation).toBe(true);
      expect(before.current.length).toBeGreaterThan(0);

      await user.click(screen.getByRole('button', { name: 'Открыть окно карточки' }));
      const dialog = await screen.findByRole('dialog', { name: 'Рабочий элемент MSK-US-99' });
      expect(
        within(dialog).getByRole('heading', { level: 2, name: 'Рабочий элемент не найден' }),
      ).toBeInTheDocument();
      expect(within(dialog).getByText('Проверьте адрес или закройте окно.')).toBeInTheDocument();

      expect(await snapshot()).toEqual(before);
    });

    it('is the contrast: without compact the same frame renames the tab, marks nothing current and hides the settings navigation', async () => {
      const user = userEvent.setup();
      shell(false);
      await screen.findByRole('button', { name: 'Открыть окно карточки' });
      expect(screen.getByRole('navigation', { name: 'Меню настроек' })).toBeInTheDocument();

      await user.click(screen.getByRole('button', { name: 'Открыть окно карточки' }));
      await screen.findByRole('dialog', { name: 'Рабочий элемент MSK-US-99' });
      await waitFor(() => expect(document.title).toBe('Рабочий элемент не найден — Mustask'));
      expect(screen.queryByRole('navigation', { name: 'Меню настроек', hidden: true })).toBeNull();
    });
  });
});
