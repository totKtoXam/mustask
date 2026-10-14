import { describe, expect, it, vi } from 'vitest';
import { createRef } from 'react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import AddIcon from '@mui/icons-material/Add';
import FilterListOffIcon from '@mui/icons-material/FilterListOff';
import InboxOutlinedIcon from '@mui/icons-material/InboxOutlined';
import { PageChromeContext, type PageChrome } from './pageChrome';
import {
  AccessDenied,
  NotFound,
  STATE_FRAME_CLASS,
  StateFrame,
  type NotFoundLevel,
} from './StateFrame';

const HINT = 'Проверьте адрес или вернитесь на главную.';

function renderRouted(element: React.ReactNode, path = '/no-such-page') {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/home" element={<div>HOME_PAGE</div>} />
        <Route path="*" element={element} />
      </Routes>
    </MemoryRouter>,
  );
}

describe('US-7 Scenario Outline: Not found names the missing context (BR-12, BR-18, D-15)', () => {
  it.each<[NotFoundLevel, string]>([
    ['organization', 'Организация не найдена'],
    ['project', 'Проект не найден'],
    ['team', 'Команда не найдена'],
    // mod-003 spec § Not-found answers: the new variant, icon `SearchOff`.
    ['workItem', 'Рабочий элемент не найден'],
  ])('the frame used for the %s level reads «%s», the hint and «На главную»', (level, heading) => {
    renderRouted(<NotFound level={level} />, '/organizations/org-x/settings/general');

    expect(screen.getByRole('heading', { level: 1, name: heading })).toBeInTheDocument();
    expect(screen.getByText(HINT)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'На главную' })).toHaveAttribute('href', '/home');
    expect(document.title).toBe(`${heading} — Mustask`);
  });
});

describe('US-7 Scenario: Not found (BR-13)', () => {
  it('reads «Страница не найдена» with the hint, and «На главную» opens «Главная»', async () => {
    renderRouted(<NotFound level="page" />);

    expect(
      screen.getByRole('heading', { level: 1, name: 'Страница не найдена' }),
    ).toBeInTheDocument();
    expect(screen.getByText(HINT)).toBeInTheDocument();
    expect(document.title).toBe('Страница не найдена — Mustask');

    await userEvent.click(screen.getByRole('link', { name: 'На главную' }));

    expect(screen.getByText('HOME_PAGE')).toBeInTheDocument();
  });

  // qa.md D-2: the shell learns from the frame that no page serves the
  // address, so it shows no settings navigation and marks nothing current.
  it.each<NotFoundLevel>(['page', 'organization', 'project', 'team', 'workItem'])(
    'tells the shell there is no page while it is shown — level %s',
    (level) => {
      const unregister = vi.fn();
      const registerNotFound = vi.fn(() => unregister);
      const chrome: PageChrome = {
        registerHeader: () => () => {},
        registerNotFound,
        trail: [],
        trailPlacement: 'topBar',
      };

      const { unmount } = render(
        <PageChromeContext.Provider value={chrome}>
          <MemoryRouter>
            <NotFound level={level} />
          </MemoryRouter>
        </PageChromeContext.Provider>,
      );
      expect(registerNotFound).toHaveBeenCalledTimes(1);
      expect(unregister).not.toHaveBeenCalled();

      unmount();
      expect(unregister).toHaveBeenCalledTimes(1);
    },
  );
});

describe('US-7 Scenario: Access denied — the shared frame (C-14)', () => {
  it('is the page’s level-1 heading and sets the tab title', () => {
    render(<AccessDenied />);

    expect(screen.getByRole('heading', { level: 1, name: 'Доступ ограничен' })).toBeInTheDocument();
    expect(document.title).toBe('Доступ ограничен — Mustask');
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
    expect(screen.queryByRole('link')).not.toBeInTheDocument();
  });
});

describe('US-7 Scenario: One frame for the full-area states', () => {
  it('lays out empty, empty after a filter, access denied and not found with the same component, never as an alert', () => {
    const { container } = renderRouted(
      <>
        <StateFrame
          icon={InboxOutlinedIcon}
          heading="Справочник пуст"
          hint="Языки ещё не добавлены."
        />
        <StateFrame
          icon={FilterListOffIcon}
          heading="Ничего не найдено"
          hint="Ни один язык не подходит под заданные условия фильтра."
          action={{ label: 'Сбросить фильтр', onClick: () => {} }}
        />
        <AccessDenied />
        <NotFound level="page" />
      </>,
    );

    const frames = container.querySelectorAll(`.${STATE_FRAME_CLASS}`);
    expect(frames).toHaveLength(4);
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('under a page header the heading is a level-2 heading and the tab title is left to the page', () => {
    document.title = 'Языки — Mustask';
    render(
      <StateFrame
        icon={InboxOutlinedIcon}
        heading="Справочник пуст"
        hint="Языки ещё не добавлены."
      />,
    );

    expect(screen.getByRole('heading', { level: 2, name: 'Справочник пуст' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { level: 1 })).not.toBeInTheDocument();
    expect(document.title).toBe('Языки — Mustask');
  });

  it('takes an h3 for a frame that stands under an h2 and looks the same (mod-016 design § 4.3)', () => {
    render(
      <>
        <StateFrame icon={InboxOutlinedIcon} heading="Как h2" />
        <StateFrame icon={InboxOutlinedIcon} heading="Как h3" headingLevel={3} />
      </>,
    );

    const h2 = screen.getByRole('heading', { level: 2, name: 'Как h2' });
    const h3 = screen.getByRole('heading', { level: 3, name: 'Как h3' });
    expect(h3.tagName).toBe('H3');
    expect(h3.className).toBe(h2.className);
    expect(getComputedStyle(h3).fontSize).toBe(getComputedStyle(h2).fontSize);
  });

  it('keeps a full-page frame the page’s h1 whatever the level asked', () => {
    render(<StateFrame fullPage icon={InboxOutlinedIcon} heading="Страница" headingLevel={3} />);

    expect(screen.getByRole('heading', { level: 1, name: 'Страница' })).toBeInTheDocument();
  });

  it('offers at most one action, an outlined button with its leading icon', async () => {
    const onClick = vi.fn();
    render(
      <StateFrame
        icon={InboxOutlinedIcon}
        heading="Справочник пуст"
        hint="Добавьте первый язык кнопкой «Добавить язык» выше."
        action={{ label: 'Добавить язык', icon: <AddIcon />, onClick }}
      />,
    );

    const action = screen.getByRole('button', { name: 'Добавить язык' });
    expect(action).toHaveClass('MuiButton-outlined');
    expect(action.querySelector('.MuiButton-startIcon svg')).not.toBeNull();
    await userEvent.click(action);
    expect(onClick).toHaveBeenCalledTimes(1);
  });
});

// mod-001 review round 1 F4: a page moves focus to the not-found frame's
// heading when a retry ends in not found, as P-2 did for the page title.
describe('The heading as a focus target (mod-001 review round 1 F4)', () => {
  it('hands the not-found h1 to headingRef with tabIndex -1, so it takes focus by script only', () => {
    const headingRef = createRef<HTMLHeadingElement>();
    renderRouted(<NotFound level="organization" headingRef={headingRef} />);

    const heading = screen.getByRole('heading', { level: 1, name: 'Организация не найдена' });
    expect(headingRef.current).toBe(heading);
    expect(heading).toHaveAttribute('tabindex', '-1');
    act(() => headingRef.current?.focus());
    expect(heading).toHaveFocus();
  });

  it('leaves the heading unfocusable, as before, without headingRef', () => {
    renderRouted(
      <>
        <NotFound level="page" />
        <StateFrame icon={InboxOutlinedIcon} heading="Справочник пуст" />
      </>,
    );

    expect(
      screen.getByRole('heading', { level: 1, name: 'Страница не найдена' }),
    ).not.toHaveAttribute('tabindex');
    expect(screen.getByRole('heading', { level: 2, name: 'Справочник пуст' })).not.toHaveAttribute(
      'tabindex',
    );
  });
});
