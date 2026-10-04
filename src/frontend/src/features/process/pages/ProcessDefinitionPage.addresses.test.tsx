import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { Route, Routes, useNavigate } from 'react-router-dom';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { server } from '../../../test/mocks/server';
import { LocationProbe } from '../../../test/renderShell';
import { renderWithProviders } from '../../../test/renderWithProviders';
import { PROCESS_DEFINITION_ROUTE, processDefinitionPath } from '../paths';
import {
  DEMONSTRATION_ORGANIZATION_ID,
  PROCESS_DEFINITION_URL,
  cloneProcessDefinition,
  sampleProcessDefinition,
} from '../testFixtures';
import { ProcessDefinitionPage } from './ProcessDefinitionPage';

// QA additions, mod-001 frontend QA pass 1 (qa.md § Frontend, F-A2, F-A3,
// F-A6, F-A12): what the page does with the identifier its address carries,
// and with an answer whose types are not in the contract's order.

const API = 'http://localhost:5000';
const UNKNOWN_ID = '0b9f7f9e-4c55-4d5b-9a51-0d6c3a7e2f10';

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

function notFound() {
  return HttpResponse.json(
    {
      status: 404,
      detail: 'Организация не найдена.',
      errorCode: 'organizations.organization.not_found',
      traceId: '00-trace-00',
    },
    { status: 404, headers: { 'Content-Type': 'application/problem+json' } },
  );
}

/** Two client-side moves a person makes without reloading: another id, and back. */
function Navigator() {
  const navigate = useNavigate();
  return (
    <>
      <button type="button" onClick={() => navigate(processDefinitionPath(UNKNOWN_ID))}>
        qa: unknown id
      </button>
      <button type="button" onClick={() => navigate(-1)}>
        qa: back
      </button>
    </>
  );
}

function renderAt(address: string, withNavigator = false) {
  return renderWithProviders(
    <>
      <Routes>
        <Route path={PROCESS_DEFINITION_ROUTE} element={<ProcessDefinitionPage />} />
      </Routes>
      {withNavigator && <Navigator />}
      <LocationProbe />
    </>,
    { initialEntries: [address] },
  );
}

describe('mod-001 US-2 Scenario: An identifier with no process is not found — what the address carries (QA)', () => {
  it.each([
    ['a question mark', '/organizations/abc%3Fx/settings/process', 'abc%3Fx'],
    ['a hash', '/organizations/abc%23x/settings/process', 'abc%23x'],
    ['a space', '/organizations/abc%20x/settings/process', 'abc%20x'],
    ['Cyrillic', '/organizations/%D0%BE%D1%80%D0%B3/settings/process', '%D0%BE%D1%80%D0%B3'],
    ['a percent sign', '/organizations/%25/settings/process', '%25'],
    ['a broken percent escape', '/organizations/%E0%A4%A/settings/process', '%25E0%25A4%25A'],
    [
      'a 300-character value',
      `/organizations/${'a'.repeat(300)}/settings/process`,
      'a'.repeat(300),
    ],
    [
      'the id in upper case',
      `/organizations/${UNKNOWN_ID.toUpperCase()}/settings/process`,
      UNKNOWN_ID.toUpperCase(),
    ],
  ])(
    '%s reaches the read as one encoded path segment, and the answer is the not-found frame',
    async (_case, address, sent) => {
      server.use(http.get(PROCESS_DEFINITION_URL, notFound));
      renderAt(address);

      expect(
        await screen.findByRole('heading', { level: 1, name: 'Организация не найдена' }),
      ).toBeInTheDocument();
      expect(readUrls).toEqual([`${API}/api/organizations/${sent}/process`]);
      expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    },
  );
});

describe('mod-001 US-2: one session, several addresses (QA)', () => {
  it('never shows one id’s process under another id’s address, and keeps a read answer for the session', async () => {
    const user = userEvent.setup();
    server.use(
      http.get(PROCESS_DEFINITION_URL, ({ params }) =>
        params.organizationId === DEMONSTRATION_ORGANIZATION_ID
          ? HttpResponse.json(sampleProcessDefinition)
          : notFound(),
      ),
    );
    renderAt(processDefinitionPath(DEMONSTRATION_ORGANIZATION_ID), true);
    expect(await screen.findByRole('region', { name: 'Эпик' })).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'qa: unknown id' }));

    expect(screen.getByTestId('location')).toHaveTextContent(processDefinitionPath(UNKNOWN_ID));
    // Right after the move, before the answer: the other id's cards are gone.
    expect(screen.queryByRole('region', { name: 'Эпик' })).not.toBeInTheDocument();
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Организация не найдена' }),
    ).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'qa: back' }));

    expect(screen.getByTestId('location')).toHaveTextContent(
      processDefinitionPath(DEMONSTRATION_ORGANIZATION_ID),
    );
    expect(await screen.findByRole('region', { name: 'Эпик' })).toBeInTheDocument();
    expect(screen.queryByText('Организация не найдена')).not.toBeInTheDocument();
    // staleTime Infinity (design § States, Content): the demonstration id was
    // read once; only the unknown id added a read of its own.
    await waitFor(() =>
      expect(readUrls).toEqual([
        `${API}/api/organizations/${DEMONSTRATION_ORGANIZATION_ID}/process`,
        `${API}/api/organizations/${UNKNOWN_ID}/process`,
      ]),
    );
  });
});

describe('mod-001 US-2 Scenario: A seeded process is shown in full — the order received (QA)', () => {
  it('renders types and states in the order the answer gives them and sorts nothing itself', async () => {
    const definition = cloneProcessDefinition();
    definition.workItemTypes.reverse();
    const task = definition.workItemTypes.find((type) => type.key === 'TASK')!;
    task.states.reverse();
    server.use(http.get(PROCESS_DEFINITION_URL, () => HttpResponse.json(definition)));
    renderAt(processDefinitionPath(DEMONSTRATION_ORGANIZATION_ID));

    await screen.findByRole('region', { name: 'Эпик' });

    expect(
      screen
        .getAllByRole('region')
        .map((section) => within(section).getByRole('heading', { level: 2 }).textContent),
    ).toEqual(['Задача', 'Баг', 'Пользовательская история', 'Фича', 'Эпик']);
    const taskRows = within(screen.getByRole('table', { name: 'Статусы типа «Задача»' }))
      .getAllByRole('rowheader')
      .map((header) => header.firstChild?.textContent);
    expect(taskRows).toEqual(['Удалена', 'Закрыта', 'Активна', 'Новая']);
  });
});

describe('mod-001 US-2 Scenario: A failed read is not taken for a missing organization — then a retry finds none (QA)', () => {
  // api-contract § 4: on a stand that skipped the seed the read answers 500,
  // and once the schema is migrated, before the next start, it answers 404.
  it('replaces the alert with the not-found frame when «Повторить» gets the not-found code', async () => {
    const user = userEvent.setup();
    let migrated = false;
    server.use(
      http.get(PROCESS_DEFINITION_URL, () =>
        migrated
          ? notFound()
          : HttpResponse.json(
              {
                status: 500,
                detail: 'Внутренняя ошибка сервера.',
                errorCode: 'http.internal_error',
              },
              { status: 500, headers: { 'Content-Type': 'application/problem+json' } },
            ),
      ),
    );
    renderAt(processDefinitionPath(DEMONSTRATION_ORGANIZATION_ID));
    const alert = await screen.findByRole('alert');
    expect(screen.queryByText('Организация не найдена')).not.toBeInTheDocument();

    migrated = true;
    await user.click(within(alert).getByRole('button', { name: 'Повторить' }));

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Организация не найдена' }),
    ).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Процесс' })).not.toBeInTheDocument();
    expect(readUrls).toHaveLength(2);
  });
});
