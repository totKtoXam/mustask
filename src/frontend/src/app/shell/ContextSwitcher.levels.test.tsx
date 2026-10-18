import { describe, expect, it, vi } from 'vitest';
import { MemoryRouter } from 'react-router-dom';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { setViewportWidth } from '../../test/viewport';
import {
  WorkContextContext,
  type WorkContextOrganization,
  type WorkContextSource,
} from '../../shared/lib/workContext';
import { ContextSwitcher } from './ContextSwitcher';

/** «Альфа» with «Пример проекта» and its teams, as the real source gives them once read (feat-040 BR-TS-10: the teams are the organization's). */
const organizations: WorkContextOrganization[] = [
  {
    id: 'o1',
    name: 'Альфа',
    projects: [{ id: 'p1', name: 'Пример проекта' }],
    teams: [
      { id: 't1', name: 'Пример проекта' },
      { id: 't2', name: 'Платформа' },
    ],
  },
  { id: 'o2', name: 'Бета', projects: [], teams: [{ id: 't3', name: 'Гамма' }] },
  { id: 'o3', name: 'Дельта', projects: [], teams: [] },
];

function source(overrides: Partial<WorkContextSource> = {}): WorkContextSource {
  return {
    status: 'ready',
    organizations,
    selection: { organizationId: 'o1', projectId: 'p1', teamId: 't1' },
    isReal: true,
    organizationListPath: '/organizations',
    select: vi.fn(),
    retry: vi.fn(),
    ...overrides,
  };
}

function renderSwitcher(value: WorkContextSource) {
  return render(
    <MemoryRouter>
      <WorkContextContext.Provider value={value}>
        <ContextSwitcher />
      </WorkContextContext.Provider>
    </MemoryRouter>,
  );
}

function group() {
  return screen.getByRole('group', { name: 'Контекст работы' });
}

describe('mod-002 US-8 Scenario: The lists fail to load — a level loading on its own (design § 3.2)', () => {
  it('projects loading: the organization level, then two skeletons in one «Загрузка контекста»', () => {
    renderSwitcher(source({ levelStatus: { project: 'loading' } }));

    expect(within(group()).getByRole('button', { name: 'Организация: Альфа' })).toBeInTheDocument();
    const status = within(group()).getByRole('status', { name: 'Загрузка контекста' });
    expect(status.querySelectorAll('.MuiSkeleton-root')).toHaveLength(2);
    expect(within(group()).queryByRole('button', { name: /^Проект:/ })).not.toBeInTheDocument();
    expect(within(group()).queryByRole('button', { name: /^Команда:/ })).not.toBeInTheDocument();
  });

  it('teams loading: the organization and project levels, then one skeleton', () => {
    renderSwitcher(source({ levelStatus: { team: 'loading' } }));

    expect(
      within(group()).getByRole('button', { name: 'Проект: Пример проекта' }),
    ).toBeInTheDocument();
    const status = within(group()).getByRole('status', { name: 'Загрузка контекста' });
    expect(status.querySelectorAll('.MuiSkeleton-root')).toHaveLength(1);
    expect(within(group()).queryByRole('button', { name: /^Команда:/ })).not.toBeInTheDocument();
  });

  it('stops at the first level that is not ready: projects loading hides a failed team level', () => {
    renderSwitcher(source({ levelStatus: { project: 'loading', team: 'failed' } }));

    expect(screen.queryByText('Не удалось загрузить команды')).not.toBeInTheDocument();
    expect(within(group()).getByRole('status', { name: 'Загрузка контекста' })).toBeInTheDocument();
  });
});

describe('mod-002 US-8 Scenario: The lists fail to load — a level failing on its own (design § 3.2)', () => {
  it('projects failed: «Не удалось загрузить проекты» and «Повторить» retrying the project level', async () => {
    const value = source({ levelStatus: { project: 'failed' } });
    renderSwitcher(value);

    expect(within(group()).getByRole('button', { name: 'Организация: Альфа' })).toBeInTheDocument();
    expect(within(group()).getByText('Не удалось загрузить проекты')).toBeVisible();
    await userEvent.click(within(group()).getByRole('button', { name: 'Повторить' }));

    expect(value.retry).toHaveBeenCalledWith('project');
  });

  it('projects failed leaves the team level, which is the organization’s, as it is (feat-040 BR-TS-10)', () => {
    renderSwitcher(source({ levelStatus: { project: 'failed' } }));

    expect(within(group()).getByRole('button', { name: 'Команда: Пример проекта' })).toBeVisible();
    expect(within(group()).queryByText('Не удалось загрузить команды')).not.toBeInTheDocument();
  });

  it('teams failed: «Не удалось загрузить команды» and «Повторить» retrying the team level', async () => {
    const value = source({ levelStatus: { team: 'failed' } });
    renderSwitcher(value);

    expect(
      within(group()).getByRole('button', { name: 'Проект: Пример проекта' }),
    ).toBeInTheDocument();
    expect(within(group()).getByText('Не удалось загрузить команды')).toBeInTheDocument();
    await userEvent.click(within(group()).getByRole('button', { name: 'Повторить' }));

    expect(value.retry).toHaveBeenCalledWith('team');
  });

  it('below 1024 px hides the text visually and keeps it in the icon’s tooltip', async () => {
    setViewportWidth(800);
    renderSwitcher(source({ levelStatus: { project: 'failed' } }));

    const text = within(group()).getByText('Не удалось загрузить проекты');
    expect(getComputedStyle(text).position).toBe('absolute');
    expect(getComputedStyle(text).width).toBe('1px');
    await userEvent.hover(within(group()).getByTestId('ErrorOutlineOutlinedIcon'));
    expect(await screen.findByRole('tooltip')).toHaveTextContent('Не удалось загрузить проекты');
  });

  it('keeps the organization level’s own rows as mod-015 designed them', async () => {
    const value = source({ status: 'failed', levelStatus: { project: 'failed' } });
    renderSwitcher(value);

    expect(within(group()).getByText('Не удалось загрузить организации')).toBeInTheDocument();
    expect(screen.queryByText('Не удалось загрузить проекты')).not.toBeInTheDocument();
    await userEvent.click(within(group()).getByRole('button', { name: 'Повторить' }));
    expect(value.retry).toHaveBeenCalledWith();
  });

  it('draws every level as before when the source reports no level status', () => {
    renderSwitcher(source());

    expect(
      within(group())
        .getAllByRole('button')
        .map((button) => button.textContent),
    ).toEqual(['Альфа', 'Пример проекта', 'Пример проекта']);
  });
});

describe('mod-002 US-8 Scenario: A remembered selection that is gone falls back — the notice', () => {
  it('reads «Ранее выбранный контекст недоступен. Выбрано: <организация> / <проект> / <команда>.» as an info alert', () => {
    renderSwitcher(source({ fallback: { id: 1, names: ['Альфа', 'Аналитика', 'Аналитика'] } }));

    const alert = screen.getByRole('alert');
    expect(alert).toHaveTextContent(
      'Ранее выбранный контекст недоступен. Выбрано: Альфа / Аналитика / Аналитика.',
    );
    expect(alert.className).toMatch(/MuiAlert-(standard|color)Info/);
  });

  it('leaves out a level with nothing in it: «Выбрано: Бета.»', () => {
    renderSwitcher(source({ fallback: { id: 2, names: ['Бета'] } }));

    expect(screen.getByRole('alert')).toHaveTextContent(
      'Ранее выбранный контекст недоступен. Выбрано: Бета.',
    );
  });

  it('is dismissed through the source when closed', async () => {
    const dismissFallback = vi.fn();
    renderSwitcher(source({ fallback: { id: 3, names: ['Бета'] }, dismissFallback }));

    await userEvent.click(within(screen.getByRole('alert')).getByRole('button'));

    expect(dismissFallback).toHaveBeenCalled();
  });

  it('shows nothing without a fallback', () => {
    renderSwitcher(source({ fallback: null }));

    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
});
