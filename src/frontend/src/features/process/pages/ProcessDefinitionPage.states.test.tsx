import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { Route, Routes } from 'react-router-dom';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { gate } from '../../../test/gate';
import { server } from '../../../test/mocks/server';
import { LocationProbe } from '../../../test/renderShell';
import { renderWithProviders } from '../../../test/renderWithProviders';
import { STATE_FRAME_CLASS } from '../../../shared/ui/StateFrame';
import type { ProcessDefinitionResponse, StateDefinition, WorkItemType } from '../api/processApi';
import { PROCESS_DEFINITION_ROUTE, processDefinitionPath } from '../paths';
import {
  DEMONSTRATION_ORGANIZATION_ID,
  PROCESS_DEFINITION_URL,
  TYPE_NAMES_IN_ORDER,
  cloneProcessDefinition,
  sampleProcessDefinition,
} from '../testFixtures';
import { ProcessDefinitionPage } from './ProcessDefinitionPage';

const NOT_FOUND_CODE = 'organizations.organization.not_found';
const DESCRIPTION =
  'Типы рабочих элементов организации, их статусы и уровни бэклога. Процесс задан системой и доступен только для просмотра.';

const readUrls: string[] = [];
const onRequest = ({ request }: { request: Request }) => {
  if (request.url.includes('/api/organizations/')) {
    readUrls.push(request.url);
  }
};

beforeEach(() => {
  readUrls.length = 0;
  server.events.on('request:start', onRequest);
});

afterEach(() => {
  server.events.removeListener('request:start', onRequest);
});

function problem(status: number, errorCode: string, detail: string) {
  return HttpResponse.json(
    { status, detail, errorCode, traceId: '00-trace-00' },
    { status, headers: { 'Content-Type': 'application/problem+json' } },
  );
}

function notFound() {
  return problem(404, NOT_FOUND_CODE, 'Организация не найдена.');
}

/** A `200` with § 1.4's body, one part a card reads taken out by `remove`. */
function answerWithout(remove: (definition: ProcessDefinitionResponse) => void) {
  const definition = cloneProcessDefinition();
  remove(definition);
  return () => HttpResponse.json(definition);
}

/** The page at its route, by an address typed as it is (not re-encoded here). */
function renderAt(address: string) {
  return renderWithProviders(
    <>
      <Routes>
        <Route path={PROCESS_DEFINITION_ROUTE} element={<ProcessDefinitionPage />} />
        <Route path="/home" element={<p>Главная страница</p>} />
      </Routes>
      <LocationProbe />
    </>,
    { initialEntries: [address] },
  );
}

function renderPage(organizationId = DEMONSTRATION_ORGANIZATION_ID) {
  return renderAt(processDefinitionPath(organizationId));
}

function expectNoProcess() {
  for (const name of TYPE_NAMES_IN_ORDER) {
    expect(screen.queryByText(name)).not.toBeInTheDocument();
  }
  expect(screen.queryByRole('region')).not.toBeInTheDocument();
}

describe('mod-001 US-2: loading in the canon frame (design § States, Loading)', () => {
  it('shows the title, the description and one status «Загрузка данных» with the real headers, and no type', async () => {
    const held = gate();
    server.use(
      http.get(PROCESS_DEFINITION_URL, async () => {
        await held.opened;
        return HttpResponse.json(sampleProcessDefinition);
      }),
    );
    renderPage();

    const status = await screen.findByRole('status', { name: 'Загрузка данных' });
    expect(screen.getByRole('heading', { level: 1, name: 'Процесс' })).toBeInTheDocument();
    expect(screen.getByText(DESCRIPTION)).toBeInTheDocument();
    expect(within(status).getAllByRole('columnheader', { name: 'Категория статуса' })).toHaveLength(
      5,
    );
    expectNoProcess();
    await waitFor(() => expect(document.title).toBe('Процесс — Mustask'));

    held.open();
    expect(await screen.findByRole('region', { name: 'Эпик' })).toBeInTheDocument();
    expect(screen.queryByRole('status')).not.toBeInTheDocument();
  });
});

describe('mod-001 US-2 Scenario: A failed read is not taken for a missing organization', () => {
  it.each([
    [
      '500 http.internal_error',
      () => problem(500, 'http.internal_error', 'Внутренняя ошибка сервера.'),
    ],
    ['404 http.not_found', () => problem(404, 'http.not_found', 'Ресурс не найден.')],
    ['404 with no body', () => new HttpResponse(null, { status: 404 })],
    [
      '404 whose text alone reads «Организация не найдена.»',
      () =>
        HttpResponse.json(
          { status: 404, detail: 'Организация не найдена.' },
          { status: 404, headers: { 'Content-Type': 'application/problem+json' } },
        ),
    ],
    ['403', () => new HttpResponse(null, { status: 403 })],
    ['401 after the client’s own renewal', () => new HttpResponse(null, { status: 401 })],
    [
      '503 http.service_unavailable',
      () => problem(503, 'http.service_unavailable', 'Сервис недоступен.'),
    ],
    ['a network error', () => HttpResponse.error()],
    [
      'a 200 that is not JSON',
      () =>
        new HttpResponse('<html>not json</html>', {
          status: 200,
          headers: { 'Content-Type': 'text/html' },
        }),
    ],
    [
      'a 200 with no types',
      () => HttpResponse.json({ ...sampleProcessDefinition, workItemTypes: [] }),
    ],
    // Review round 1 F2, qa.md D-4: before the read checked these parts, each
    // one threw in render and the crash screen replaced the whole shell.
    [
      'a 200 whose type has no states',
      answerWithout((definition) => {
        delete (definition.workItemTypes[0] as Partial<WorkItemType>).states;
      }),
    ],
    [
      'a 200 whose type has no backlog level',
      answerWithout((definition) => {
        delete (definition.workItemTypes[1] as Partial<WorkItemType>).backlogLevel;
      }),
    ],
    [
      'a 200 whose state has no category',
      answerWithout((definition) => {
        delete (definition.workItemTypes[4]!.states[3] as Partial<StateDefinition>).category;
      }),
    ],
  ])(
    '%s: the alert with its title and «Повторить», never «Организация не найдена»',
    async (_case, answer) => {
      server.use(http.get(PROCESS_DEFINITION_URL, answer));
      renderPage();

      const alert = await screen.findByRole('alert');
      expect(within(alert).getByText('Не удалось загрузить процесс')).toBeInTheDocument();
      expect(within(alert).getByRole('button', { name: 'Повторить' })).toBeInTheDocument();
      expect(screen.getByRole('heading', { level: 1, name: 'Процесс' })).toBeInTheDocument();
      expect(screen.getByText(DESCRIPTION)).toBeInTheDocument();
      expect(screen.queryByText('Организация не найдена')).not.toBeInTheDocument();
      expect(document.querySelector(`.${STATE_FRAME_CLASS}`)).toBeNull();
      expectNoProcess();
      // The only control in a failed read.
      expect(screen.getAllByRole('button')).toEqual([
        within(alert).getByRole('button', { name: 'Повторить' }),
      ]);
      expect(screen.queryByRole('link')).not.toBeInTheDocument();
    },
  );

  it('shows the panel’s generic text for an answer with no types — no new copy', async () => {
    server.use(
      http.get(PROCESS_DEFINITION_URL, () =>
        HttpResponse.json({ ...sampleProcessDefinition, workItemTypes: [] }),
      ),
    );
    renderPage();

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Непредвиденная ошибка. Повторите попытку.',
    );
  });

  it('shows the skeleton while a retry runs, then the content, with focus on the title', async () => {
    const user = userEvent.setup();
    let failing = true;
    const held = gate();
    server.use(
      http.get(PROCESS_DEFINITION_URL, async () => {
        if (failing) {
          return problem(500, 'http.internal_error', 'Внутренняя ошибка сервера.');
        }
        await held.opened;
        return HttpResponse.json(sampleProcessDefinition);
      }),
    );
    renderPage();
    const alert = await screen.findByRole('alert');

    failing = false;
    await user.click(within(alert).getByRole('button', { name: 'Повторить' }));

    expect(await screen.findByRole('status', { name: 'Загрузка данных' })).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    held.open();
    expect(await screen.findByRole('region', { name: 'Эпик' })).toBeInTheDocument();
    await waitFor(() =>
      expect(screen.getByRole('heading', { level: 1, name: 'Процесс' })).toHaveFocus(),
    );
  });

  // Review round 4 F6: the retry's answer is held. An answer that comes at
  // once settles its fetching state and its failure in one render, so the
  // skeleton never replaces the alert, the button the user clicked stays
  // mounted and keeps its focus, and this test passed with the focus call
  // removed. Held, the clicked button unmounts and focus can only be on the
  // new one if `useRetryFocus` put it there.
  it('brings the alert back when the retry fails again, with focus on its new «Повторить»', async () => {
    const user = userEvent.setup();
    const held = gate();
    let reads = 0;
    server.use(
      http.get(PROCESS_DEFINITION_URL, async () => {
        if (reads++ > 0) {
          await held.opened;
        }
        return problem(500, 'http.internal_error', 'Внутренняя ошибка сервера.');
      }),
    );
    renderPage();
    const first = await screen.findByRole('alert');
    const clicked = within(first).getByRole('button', { name: 'Повторить' });

    await user.click(clicked);

    expect(await screen.findByRole('status', { name: 'Загрузка данных' })).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(clicked).not.toBeInTheDocument();
    held.open();
    const again = await screen.findByRole('alert');
    const retryAgain = within(again).getByRole('button', { name: 'Повторить' });
    expect(retryAgain).not.toBe(clicked);
    await waitFor(() => expect(retryAgain).toHaveFocus());
    expect(readUrls).toHaveLength(2);
    expectNoProcess();
  });

  it('moves no focus on a first load', async () => {
    server.use(http.get(PROCESS_DEFINITION_URL, () => HttpResponse.json(sampleProcessDefinition)));
    renderPage();

    await screen.findByRole('region', { name: 'Эпик' });
    expect(document.activeElement).toBe(document.body);
  });

  // Review round 1 F4, qa.md D-5: neither the title nor «Повторить» is
  // mounted once the frame replaces the page, so focus used to fall to the
  // document. The frame's heading is now the page's h1 and takes it.
  it('moves focus to the frame’s heading when the retry finds no organization', async () => {
    const user = userEvent.setup();
    let failing = true;
    server.use(
      http.get(PROCESS_DEFINITION_URL, () =>
        failing ? problem(500, 'http.internal_error', 'Внутренняя ошибка сервера.') : notFound(),
      ),
    );
    renderPage();
    const alert = await screen.findByRole('alert');

    failing = false;
    await user.click(within(alert).getByRole('button', { name: 'Повторить' }));

    const heading = await screen.findByRole('heading', {
      level: 1,
      name: 'Организация не найдена',
    });
    await waitFor(() => expect(heading).toHaveFocus());
    expect(heading).toHaveAttribute('tabindex', '-1');
  });

  it('moves no focus when the first load finds no organization', async () => {
    server.use(http.get(PROCESS_DEFINITION_URL, notFound));
    renderPage();

    await screen.findByRole('heading', { level: 1, name: 'Организация не найдена' });
    expect(document.activeElement).toBe(document.body);
  });
});

describe('mod-001 US-2 Scenario: An unmigrated Development stand starts without the demonstration process (frontend half)', () => {
  it('reads the 500 an unmigrated schema answers as a failed read, never as not found', async () => {
    server.use(
      http.get(PROCESS_DEFINITION_URL, () =>
        problem(500, 'http.internal_error', 'Внутренняя ошибка сервера.'),
      ),
    );
    renderPage(DEMONSTRATION_ORGANIZATION_ID);

    expect(await screen.findByText('Не удалось загрузить процесс')).toBeInTheDocument();
    expect(screen.queryByText('Организация не найдена')).not.toBeInTheDocument();
  });
});

describe('mod-001 US-2 Scenario: An identifier with no process is not found', () => {
  it('replaces the whole page with the shared frame, its h1, hint and «На главную», and sets the tab', async () => {
    const user = userEvent.setup();
    server.use(http.get(PROCESS_DEFINITION_URL, notFound));
    renderPage('0b9f7f9e-4c55-4d5b-9a51-0d6c3a7e2f10');

    const heading = await screen.findByRole('heading', {
      level: 1,
      name: 'Организация не найдена',
    });
    expect(heading.closest(`.${STATE_FRAME_CLASS}`)).not.toBeNull();
    expect(screen.getByText('Проверьте адрес или вернитесь на главную.')).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Процесс' })).not.toBeInTheDocument();
    expect(screen.queryByText(DESCRIPTION)).not.toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(screen.queryByText('Организация не найдена.')).not.toBeInTheDocument();
    expectNoProcess();
    await waitFor(() => expect(document.title).toBe('Организация не найдена — Mustask'));
    // The only control of the frame.
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
    const home = screen.getByRole('link', { name: 'На главную' });
    expect(screen.getAllByRole('link')).toEqual([home]);

    await user.click(home);

    expect(screen.getByTestId('location')).toHaveTextContent('/home');
  });

  it.each([
    ['a malformed id', '/organizations/not-a-uuid/settings/process', 'not-a-uuid'],
    ['an encoded slash', '/organizations/a%2Fb/settings/process', 'a%2Fb'],
  ])(
    'sends %s as the address carries it, re-encoded, and answers the same frame',
    async (_case, address, sent) => {
      server.use(http.get(PROCESS_DEFINITION_URL, notFound));
      renderAt(address);

      expect(
        await screen.findByRole('heading', { level: 1, name: 'Организация не найдена' }),
      ).toBeInTheDocument();
      expect(readUrls).toEqual([`http://localhost:5000/api/organizations/${sent}/process`]);
    },
  );
});
