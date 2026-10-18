import { describe, expect, it, vi } from 'vitest';
import { Route } from 'react-router-dom';
import { screen, within } from '@testing-library/react';
import { HttpResponse, http } from 'msw';
import { PROCESS_DEFINITION_ROUTE, ProcessDefinitionPage } from '../features/process';
import {
  DEMONSTRATION_ORGANIZATION_ID,
  PROCESS_DEFINITION_URL,
  sampleProcessDefinition,
} from '../features/process/testFixtures';
import type { WorkContextSource } from '../shared/lib/workContext';
import { server } from '../test/mocks/server';
import {
  authValue,
  LANGUAGES_READ,
  LANGUAGES_WRITE,
  renderShell,
  stubRoutes,
} from '../test/renderShell';

const PROCESS_ADDRESS = `/organizations/${DEMONSTRATION_ORGANIZATION_ID}/settings/process`;

/** The shell's stub pages plus the real process page at its route. */
const routes = (
  <>
    {stubRoutes()}
    <Route path={PROCESS_DEFINITION_ROUTE} element={<ProcessDefinitionPage />} />
  </>
);

/** What mod-002 will supply: a real context whose organization is the address's (mod-015 § 5.7). */
const realSource: WorkContextSource = {
  status: 'ready',
  organizations: [
    { id: DEMONSTRATION_ORGANIZATION_ID, name: 'Демонстрация', projects: [], teams: [] },
  ],
  selection: { organizationId: DEMONSTRATION_ORGANIZATION_ID },
  isReal: true,
  select: vi.fn(),
};

function serveProcess() {
  server.use(http.get(PROCESS_DEFINITION_URL, () => HttpResponse.json(sampleProcessDefinition)));
}

function serveNotFound() {
  server.use(
    http.get(PROCESS_DEFINITION_URL, () =>
      HttpResponse.json(
        { status: 404, errorCode: 'organizations.organization.not_found' },
        { status: 404, headers: { 'Content-Type': 'application/problem+json' } },
      ),
    ),
  );
}

function mainNavigation() {
  return screen.getByRole('navigation', { name: 'Основное меню' });
}

function expectNoProcessEntryInAnyNavigation() {
  for (const navigation of screen.getAllByRole('navigation')) {
    expect(within(navigation).queryByText('Процесс')).not.toBeInTheDocument();
  }
  for (const link of screen.queryAllByRole('link')) {
    expect(link.getAttribute('href') ?? '').not.toContain('/settings/process');
  }
}

describe('mod-001 US-2 Scenario: No visible entry or link leads to the screen before mod-002 — on screen', () => {
  it.each([
    ['«Главная»', '/home'],
    ['«Языки»', '/dictionaries/languages'],
  ])('%s with the placeholder source holds no «Процесс» and no link to it', (_page, path) => {
    renderShell({ path, auth: authValue([LANGUAGES_READ, LANGUAGES_WRITE]), routes });

    expectNoProcessEntryInAnyNavigation();
  });

  // plan-frontend § 6.3 first row (P-1 (a)): before mod-002 the selection is
  // the placeholder's, never the address's id, so the shell does not take the
  // demonstration address for a settings page.
  it('on the process address with the placeholder source: no «Меню настроек», no entry current, no trail', async () => {
    serveProcess();
    renderShell({ path: PROCESS_ADDRESS, auth: authValue([LANGUAGES_READ]), routes });

    expect(await screen.findByRole('region', { name: 'Эпик' })).toBeInTheDocument();
    expectNoProcessEntryInAnyNavigation();
    expect(screen.queryByRole('navigation', { name: 'Меню настроек' })).not.toBeInTheDocument();
    expect(screen.queryByRole('navigation', { name: 'Путь к странице' })).not.toBeInTheDocument();
    expect(mainNavigation().querySelectorAll('[aria-current]')).toHaveLength(0);
  });
});

describe('mod-001 US-2: «Процесс» in the settings navigation once the context is real (D-5, mod-015 BR-19)', () => {
  it('marks «Процесс» `page` in «Меню настроек», «Настройки» `true` in the rail, and shows the trail «Настройки / Организация»', async () => {
    serveProcess();
    renderShell({ path: PROCESS_ADDRESS, auth: authValue([]), contextSource: realSource, routes });

    expect(await screen.findByRole('region', { name: 'Эпик' })).toBeInTheDocument();
    const settingsNavigation = screen.getByRole('navigation', { name: 'Меню настроек' });
    expect(within(settingsNavigation).getByText('Организация')).toBeInTheDocument();
    const entry = within(settingsNavigation).getByRole('link', { name: 'Процесс' });
    expect(entry).toHaveAttribute('aria-current', 'page');
    expect(entry).toHaveAttribute('href', PROCESS_ADDRESS);
    expect(within(mainNavigation()).getByRole('link', { name: 'Настройки' })).toHaveAttribute(
      'aria-current',
      'true',
    );
    const trail = screen.getByRole('navigation', { name: 'Путь к странице' });
    expect(trail).toHaveTextContent('Настройки/Организация');
    expect(within(trail).queryByRole('link')).not.toBeInTheDocument();
  });

  it('«Настройки» opens the organization’s «Общие» first, by model order (mod-002 design § 2.1)', () => {
    renderShell({
      path: '/home',
      auth: authValue([LANGUAGES_READ]),
      contextSource: realSource,
      routes,
    });

    expect(within(mainNavigation()).getByRole('link', { name: 'Настройки' })).toHaveAttribute(
      'href',
      `/organizations/${DEMONSTRATION_ORGANIZATION_ID}/settings/general`,
    );
  });

  it('on not found, still no «Меню настроек», no entry current and no trail (mod-015 BR-13)', async () => {
    serveNotFound();
    renderShell({ path: PROCESS_ADDRESS, auth: authValue([]), contextSource: realSource, routes });

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Организация не найдена' }),
    ).toBeInTheDocument();
    expect(screen.queryByRole('navigation', { name: 'Меню настроек' })).not.toBeInTheDocument();
    expect(screen.queryByRole('navigation', { name: 'Путь к странице' })).not.toBeInTheDocument();
    expect(mainNavigation().querySelectorAll('[aria-current]')).toHaveLength(0);
  });
});
