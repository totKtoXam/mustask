import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { MemoryRouter } from 'react-router-dom';
import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { server } from '../../test/mocks/server';
import { setViewportWidth } from '../../test/viewport';
import {
  WorkContextContext,
  type WorkContextOrganization,
  type WorkContextSource,
} from '../../shared/lib/workContext';
import { ContextSwitcher } from './ContextSwitcher';
import { PLACEHOLDER_ORGANIZATIONS, useStaticContextSource } from './placeholderContext';

function StaticSwitcher({ organizations }: { organizations: readonly WorkContextOrganization[] }) {
  const source = useStaticContextSource(organizations);
  return (
    <WorkContextContext.Provider value={source}>
      <ContextSwitcher />
    </WorkContextContext.Provider>
  );
}

function renderSwitcher(organizations = PLACEHOLDER_ORGANIZATIONS) {
  return render(
    <MemoryRouter>
      <StaticSwitcher organizations={organizations} />
    </MemoryRouter>,
  );
}

function renderSource(source: WorkContextSource) {
  return render(
    <MemoryRouter>
      <WorkContextContext.Provider value={source}>
        <ContextSwitcher />
      </WorkContextContext.Provider>
    </MemoryRouter>,
  );
}

function level(name: RegExp | string) {
  return screen.getByRole('button', { name });
}

async function openLevel(name: RegExp) {
  await userEvent.click(level(name));
  return screen.getByRole('menu');
}

function entries(menu: HTMLElement) {
  return within(menu)
    .getAllByRole('menuitemradio')
    .map((item) => ({ name: item.textContent, checked: item.getAttribute('aria-checked') }));
}

describe('US-5 Scenario: Initial context (BR-11, D-11)', () => {
  it('shows the first organization, project and team, each named by its level', () => {
    renderSwitcher();

    const group = screen.getByRole('group', { name: 'Контекст работы' });
    expect(
      within(group)
        .getAllByRole('button')
        .map((button) => button.textContent),
    ).toEqual(['Пример организации 1', 'Пример проекта 1', 'Пример команды 1']);
    expect(level('Организация: Пример организации 1')).toHaveAttribute('aria-haspopup', 'menu');
    expect(level('Проект: Пример проекта 1')).toBeInTheDocument();
    expect(level('Команда: Пример команды 1')).toBeInTheDocument();
  });
});

describe('US-5 Scenario: Levels list only what belongs to the level above', () => {
  it('lists the organization’s two projects, the first current, and the organization’s three teams (feat-040 BR-TS-10)', async () => {
    renderSwitcher();

    const projects = await openLevel(/^Проект:/);
    expect(within(projects).getByText('Проекты')).toBeInTheDocument();
    expect(entries(projects)).toEqual([
      { name: 'Пример проекта 1', checked: 'true' },
      { name: 'Пример проекта 2', checked: 'false' },
    ]);
    await userEvent.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('menu')).not.toBeInTheDocument());

    const teams = await openLevel(/^Команда:/);
    expect(entries(teams).map((entry) => entry.name)).toEqual([
      'Пример команды 1',
      'Пример команды 2',
      'Пример команды 3',
    ]);
  });
});

describe('US-5 Scenario: Changing a level resets the levels below it', () => {
  it('resets project and team to the new organization’s first; a project change keeps the team (feat-040 BR-TS-10)', async () => {
    renderSwitcher();

    await userEvent.click(
      within(await openLevel(/^Организация:/)).getByRole('menuitemradio', {
        name: 'Пример организации 2',
      }),
    );

    expect(level('Организация: Пример организации 2')).toBeInTheDocument();
    expect(level('Проект: Пример проекта 3')).toBeInTheDocument();
    expect(level('Команда: Пример команды 4')).toBeInTheDocument();

    await userEvent.click(
      within(await openLevel(/^Организация:/)).getByRole('menuitemradio', {
        name: 'Пример организации 1',
      }),
    );
    await userEvent.click(
      within(await openLevel(/^Команда:/)).getByRole('menuitemradio', {
        name: 'Пример команды 2',
      }),
    );
    await userEvent.click(
      within(await openLevel(/^Проект:/)).getByRole('menuitemradio', { name: 'Пример проекта 2' }),
    );

    expect(level('Проект: Пример проекта 2')).toBeInTheDocument();
    expect(level('Команда: Пример команды 2')).toBeInTheDocument();
  });
});

describe('US-5 Scenario: No request, no persistence', () => {
  const requests: string[] = [];
  const onRequest = ({ request }: { request: Request }) => {
    requests.push(request.url);
  };
  beforeEach(() => {
    requests.length = 0;
    server.events.on('request:start', onRequest);
  });
  afterEach(() => {
    server.events.removeListener('request:start', onRequest);
  });

  it('sends nothing while every level is opened and changed, and a remount shows the initial context', async () => {
    const { unmount } = renderSwitcher();

    for (const [levelName, choice] of [
      [/^Организация:/, 'Пример организации 2'],
      [/^Проект:/, 'Пример проекта 3'],
      [/^Команда:/, 'Пример команды 4'],
      [/^Организация:/, 'Пример организации 1'],
      [/^Проект:/, 'Пример проекта 2'],
    ] as const) {
      await userEvent.click(
        within(await openLevel(levelName)).getByRole('menuitemradio', { name: choice }),
      );
    }
    // Organization 1 brought its first team back; the project change kept it.
    expect(level('Команда: Пример команды 1')).toBeInTheDocument();
    expect(requests).toEqual([]);

    unmount();
    renderSwitcher();

    expect(level('Организация: Пример организации 1')).toBeInTheDocument();
    expect(level('Проект: Пример проекта 1')).toBeInTheDocument();
    expect(level('Команда: Пример команды 1')).toBeInTheDocument();
    expect(localStorage.length).toBe(0);
    expect(sessionStorage.length).toBe(0);
  });
});

describe('US-5 Scenario: «Все организации» waits for its page (BR-11, BR-7, D-20)', () => {
  it('lists the two organizations and no «Все организации»', async () => {
    renderSwitcher();

    const menu = await openLevel(/^Организация:/);

    expect(entries(menu).map((entry) => entry.name)).toEqual([
      'Пример организации 1',
      'Пример организации 2',
    ]);
    expect(within(menu).queryByText('Все организации')).not.toBeInTheDocument();
  });

  it('shows «Все организации» as a link once mod-002 gives it a page', async () => {
    renderSource({
      status: 'ready',
      organizations: PLACEHOLDER_ORGANIZATIONS,
      selection: { organizationId: 'org-1', projectId: 'prj-1', teamId: 'team-1' },
      isReal: true,
      organizationListPath: '/organizations',
      select: vi.fn(),
    });

    const menu = await openLevel(/^Организация:/);

    expect(within(menu).getByRole('menuitem', { name: 'Все организации' })).toHaveAttribute(
      'href',
      '/organizations',
    );
  });
});

describe('US-5 Scenario: A long name is truncated, not lost (BR-11, D-22)', () => {
  const longName = `Организация ${'с очень длинным названием '.repeat(4)}`.slice(0, 100);

  it('keeps the full 100-character name in the accessible name and the tooltip', async () => {
    expect(longName).toHaveLength(100);
    renderSwitcher([
      {
        id: 'org-long',
        name: longName,
        projects: [{ id: 'prj-1', name: 'Пример проекта 1' }],
        teams: [],
      },
    ]);

    const organization = level(`Организация: ${longName}`);
    await userEvent.hover(organization);

    expect(await screen.findByRole('tooltip')).toHaveTextContent(longName);
    // One line, cut with an ellipsis (measured by QA in a browser).
    const name = within(organization).getByText(longName);
    expect(getComputedStyle(name).textOverflow).toBe('ellipsis');
    expect(getComputedStyle(name).whiteSpace).toBe('nowrap');
  });
});

describe('US-5 Scenario: No organizations', () => {
  it('shows «Нет организаций» as plain text and no project or team level', () => {
    renderSwitcher([]);

    const group = screen.getByRole('group', { name: 'Контекст работы' });
    expect(within(group).getByText('Нет организаций').closest('a')).toBeNull();
    expect(within(group).queryByRole('button')).not.toBeInTheDocument();
    expect(within(group).queryByRole('link')).not.toBeInTheDocument();
  });

  it('links «Нет организаций» to the organization list once mod-002 gives it a page', () => {
    renderSource({
      status: 'ready',
      organizations: [],
      selection: {},
      isReal: true,
      organizationListPath: '/organizations',
      select: vi.fn(),
    });

    expect(screen.getByRole('link', { name: 'Нет организаций' })).toHaveAttribute(
      'href',
      '/organizations',
    );
  });
});

describe('US-5 Scenario: An organization with no projects — and feat-040 BR-TS-10: the team level is the organization’s', () => {
  it('shows «Нет проектов» at the project level and «Нет команд» at the team level when the organization has neither', () => {
    renderSwitcher([{ id: 'org-empty', name: 'Пустая организация', projects: [], teams: [] }]);

    expect(level('Организация: Пустая организация')).toBeInTheDocument();
    expect(screen.getByText('Нет проектов')).toBeInTheDocument();
    expect(screen.getByText('Нет команд')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^Проект:/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^Команда:/ })).not.toBeInTheDocument();
  });

  it('draws the team level when the organization has teams and no project: «Нет проектов» then the team', () => {
    renderSwitcher([
      {
        id: 'org-teams',
        name: 'Организация с командами',
        projects: [],
        teams: [{ id: 'team-1', name: 'Пример команды 1' }],
      },
    ]);

    expect(screen.getByText('Нет проектов')).toBeInTheDocument();
    expect(level('Команда: Пример команды 1')).toBeInTheDocument();
    expect(screen.queryByText('Нет команд')).not.toBeInTheDocument();
  });

  it('an organization with no team shows «Нет команд», whatever its projects', () => {
    renderSwitcher([
      {
        id: 'org-1',
        name: 'Пример организации 1',
        projects: [{ id: 'prj-1', name: 'Пример проекта 1' }],
        teams: [],
      },
    ]);

    expect(level('Проект: Пример проекта 1')).toBeInTheDocument();
    expect(screen.getByText('Нет команд')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^Команда:/ })).not.toBeInTheDocument();
  });
});

describe('US-5 Scenario: States mod-002 will wire are designed now — built from the source’s status (D-15)', () => {
  const base: WorkContextSource = {
    status: 'ready',
    organizations: [],
    selection: {},
    isReal: true,
    select: vi.fn(),
  };

  it('loading: three placeholders announced as «Загрузка контекста»', () => {
    renderSource({ ...base, status: 'loading' });

    const status = screen.getByRole('status', { name: 'Загрузка контекста' });
    expect(status.querySelectorAll('.MuiSkeleton-root')).toHaveLength(3);
  });

  it('failed: «Не удалось загрузить организации» with «Повторить», which retries', async () => {
    const retry = vi.fn();
    renderSource({ ...base, status: 'failed', retry });

    const group = screen.getByRole('group', { name: 'Контекст работы' });
    expect(within(group).getByText('Не удалось загрузить организации')).toBeInTheDocument();
    await userEvent.click(within(group).getByRole('button', { name: 'Повторить' }));

    expect(retry).toHaveBeenCalledTimes(1);
  });

  it('failed below 1024 px: the text is hidden visually but kept for screen readers', () => {
    setViewportWidth(900);
    renderSource({ ...base, status: 'failed', retry: vi.fn() });

    const text = screen.getByText('Не удалось загрузить организации');
    expect(getComputedStyle(text).position).toBe('absolute');
    expect(getComputedStyle(text).width).toBe('1px');
  });
});

describe('US-5 Scenario: the narrow window (design § 5.6)', () => {
  it('keeps the three levels and cuts names at the narrow width', () => {
    renderSwitcher();
    act(() => setViewportWidth(900));

    const name = within(level('Организация: Пример организации 1')).getByText(
      'Пример организации 1',
    );
    expect(getComputedStyle(name).maxWidth).toBe('96px');
    expect(level('Команда: Пример команды 1')).toBeInTheDocument();
  });
});
