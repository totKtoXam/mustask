// Frontend QA, re-check 1 (mod-001, 2026-10-04): the two fixes of code review
// round 1 (qa.md D-4, D-5) seen through the real routes, the real shell and the
// real ErrorBoundary, composed the way App.tsx composes them. A throw while a
// card renders would show «Что-то пошло не так» here, as it does to a user, so
// "no crash screen" is asserted on the text, not inferred from a green test.
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { AppRoutes } from '../../../app/routes';
import { AuthContext } from '../../auth/AuthContext';
import { ErrorBoundary } from '../../../shared/ui/ErrorBoundary';
import { gate, type Gate } from '../../../test/gate';
import { server } from '../../../test/mocks/server';
import { authValue } from '../../../test/renderShell';
import { processDefinitionPath } from '../paths';
import {
  DEMONSTRATION_ORGANIZATION_ID,
  PROCESS_DEFINITION_URL,
  TYPE_NAMES_IN_ORDER,
  cloneProcessDefinition,
  sampleProcessDefinition,
} from '../testFixtures';

const NOT_FOUND_CODE = 'organizations.organization.not_found';
const ADDRESS = processDefinitionPath(DEMONSTRATION_ORGANIZATION_ID);

/** § 1.4's body as plain JSON, so a test can take a part out or put a wrong kind in. */
type Loose = Record<string, unknown>;
type Answer = () => Response | Promise<Response>;

const typeAt = (body: Loose, index: number) => (body.workItemTypes as Loose[])[index]!;
const stateAt = (type: Loose, index: number) => (type.states as Loose[])[index]!;

// The page's own reads of the process: the organization gate above it reads the
// access first (mod-016), which these tests do not count.
const reads: string[] = [];
const onRequest = ({ request }: { request: Request }) => {
  if (request.url.includes('/api/organizations/') && request.url.endsWith('/process')) {
    reads.push(request.url);
  }
};

const ACCESS_URL = 'http://localhost:5000/api/organizations/:organizationId/access';

beforeEach(() => {
  reads.length = 0;
  server.events.on('request:start', onRequest);
});

afterEach(() => {
  server.events.removeListener('request:start', onRequest);
});

function problem(status: number, errorCode: string, detail: string): Answer {
  return () =>
    HttpResponse.json(
      { status, detail, errorCode, traceId: '00-trace-00' },
      { status, headers: { 'Content-Type': 'application/problem+json' } },
    );
}

const serverError = problem(500, 'http.internal_error', 'Внутренняя ошибка сервера.');
const notFoundBody = problem(404, NOT_FOUND_CODE, 'Организация не найдена.');

/**
 * The caller is no longer a member (api-contract § 0.1): the process read says
 * so, and so does every access read after it — the one the shell starts when it
 * sees the 404 included, which is why the answer flips the access read first.
 */
const notFound: Answer = () => {
  server.use(http.get(ACCESS_URL, notFoundBody));
  return notFoundBody();
};
const good: Answer = () => HttpResponse.json(sampleProcessDefinition);

function broken(breakIt: (body: Loose) => void): Answer {
  const body = cloneProcessDefinition() as unknown as Loose;
  breakIt(body);
  return () => HttpResponse.json(body as never);
}

/** An answer that does not go out until `held` is opened: the read stays pending. */
function afterGate(held: Gate, answer: Answer): Answer {
  return async () => {
    await held.opened;
    return answer();
  };
}

/** The read answers these in order; the last answer repeats. */
function serveInOrder(...answers: Answer[]) {
  let call = 0;
  server.use(
    http.get(PROCESS_DEFINITION_URL, () => answers[Math.min(call++, answers.length - 1)]!()),
  );
}

/** App.tsx's composition without the sign-in provider: boundary, query client, routes, the shell. */
function renderApp(path = ADDRESS) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <ErrorBoundary>
      <QueryClientProvider client={queryClient}>
        <AuthContext.Provider value={authValue([])}>
          <MemoryRouter initialEntries={[path]}>
            <AppRoutes />
          </MemoryRouter>
        </AuthContext.Provider>
      </QueryClientProvider>
    </ErrorBoundary>,
  );
}

async function expectFailedRead() {
  const title = await screen.findByText('Не удалось загрузить процесс');
  const alert = title.closest('[role="alert"]') as HTMLElement;
  expect(within(alert).getByRole('button', { name: 'Повторить' })).toBeInTheDocument();
  expect(screen.queryByText('Что-то пошло не так')).not.toBeInTheDocument();
  expect(screen.queryByText('Организация не найдена')).not.toBeInTheDocument();
  // Inside the shell, with the page's own title and description kept, and no part of the process.
  expect(screen.getByRole('banner')).toBeInTheDocument();
  expect(screen.getByRole('heading', { level: 1, name: 'Процесс' })).toBeInTheDocument();
  expect(screen.queryByRole('region')).not.toBeInTheDocument();
  for (const name of TYPE_NAMES_IN_ORDER) {
    expect(screen.queryByText(name)).not.toBeInTheDocument();
  }
  return alert;
}

async function retry(alert: HTMLElement) {
  const user = userEvent.setup();
  await user.click(within(alert).getByRole('button', { name: 'Повторить' }));
  return user;
}

// Review round 4 F6. The tests that pin focus going back to «Повторить» hold
// the retry's answer. An answer that comes at once settles the retry's
// fetching state and its failure in one render, so the skeleton never replaces
// the alert, the button that was clicked stays mounted and keeps the focus the
// click gave it — and the test passes with the focus call removed. Held, the
// clicked button is gone and focus can be on the new one only if
// `useRetryFocus` put it there.

/** Clicks «Повторить» and waits, with the answer held, for the skeleton; returns the button clicked. */
async function retryHeld(alert: HTMLElement) {
  const clicked = within(alert).getByRole('button', { name: 'Повторить' });
  await retry(alert);

  expect(await screen.findByRole('status', { name: 'Загрузка данных' })).toBeInTheDocument();
  expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  expect(clicked).not.toBeInTheDocument();
  return clicked;
}

/** The held answer has gone out as another failure: a new alert, a NEW «Повторить», and focus on it. */
async function expectFocusOnNewRetry(clicked: HTMLElement) {
  const alert = await expectFailedRead();
  const button = within(alert).getByRole('button', { name: 'Повторить' });
  expect(button).not.toBe(clicked);
  await waitFor(() => expect(button).toHaveFocus());
  return alert;
}

describe('QA re-check 1, D-4: a 200 with a part missing is a failed read, not the crash screen', () => {
  it.each<[string, (body: Loose) => void]>([
    // The three parts the review's F2 names.
    [
      'a type with no states',
      (body) => {
        delete typeAt(body, 0).states;
      },
    ],
    [
      'a type with no backlogLevel',
      (body) => {
        delete typeAt(body, 1).backlogLevel;
      },
    ],
    [
      'a state with no category',
      (body) => {
        delete stateAt(typeAt(body, 2), 1).category;
      },
    ],
    // Each of them in the last place the check has to reach.
    [
      'the last type with no states',
      (body) => {
        delete typeAt(body, 4).states;
      },
    ],
    [
      'the last state of the last type with no category',
      (body) => {
        const states = typeAt(body, 4).states as Loose[];
        delete states[states.length - 1]!.category;
      },
    ],
    // The same parts present but of the wrong shape.
    [
      'states null',
      (body) => {
        typeAt(body, 0).states = null;
      },
    ],
    [
      'states an object',
      (body) => {
        typeAt(body, 3).states = {};
      },
    ],
    [
      'backlogLevel null',
      (body) => {
        typeAt(body, 0).backlogLevel = null;
      },
    ],
    [
      'backlogLevel a string',
      (body) => {
        typeAt(body, 0).backlogLevel = 'Эпики';
      },
    ],
    [
      'a category null',
      (body) => {
        stateAt(typeAt(body, 0), 0).category = null;
      },
    ],
    [
      'a category a string',
      (body) => {
        stateAt(typeAt(body, 0), 0).category = 'Новые';
      },
    ],
    [
      'a type that is a string',
      (body) => {
        (body.workItemTypes as unknown[])[1] = 'Фича';
      },
    ],
    [
      'a state that is a number',
      (body) => {
        (typeAt(body, 0).states as unknown[])[2] = 3;
      },
    ],
    [
      'workItemTypes an object',
      (body) => {
        body.workItemTypes = {};
      },
    ],
    [
      'no workItemTypes at all',
      (body) => {
        delete body.workItemTypes;
      },
    ],
  ])('shows the alert with «Повторить» inside the shell for %s', async (_case, breakIt) => {
    serveInOrder(broken(breakIt));
    renderApp();

    await expectFailedRead();
    expect(reads).toHaveLength(1);
  });

  it.each<[string, Answer]>([
    ['an array', () => HttpResponse.json([] as never)],
    ['a null', () => HttpResponse.json(null as never)],
    ['a string', () => HttpResponse.json('process' as never)],
  ])('shows the alert with «Повторить» for a 200 whose body is %s', async (_case, answer) => {
    serveInOrder(answer);
    renderApp();

    await expectFailedRead();
  });

  it('shows the content and moves focus to the title when «Повторить» then gets a whole answer', async () => {
    serveInOrder(
      broken((body) => {
        delete typeAt(body, 0).states;
      }),
      good,
    );
    renderApp();
    const alert = await expectFailedRead();

    await retry(alert);

    expect(await screen.findByRole('region', { name: 'Эпик' })).toBeInTheDocument();
    expect(screen.getAllByRole('region')).toHaveLength(5);
    const title = screen.getByRole('heading', { level: 1, name: 'Процесс' });
    await waitFor(() => expect(document.activeElement).toBe(title));
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(reads).toHaveLength(2);
  });

  it('brings the alert back, with focus on its «Повторить», when the retry gets another broken answer', async () => {
    const withoutCategory = broken((body) => {
      delete stateAt(typeAt(body, 0), 0).category;
    });
    const held = gate();
    serveInOrder(withoutCategory, afterGate(held, withoutCategory));
    renderApp();
    const first = await expectFailedRead();

    const clicked = await retryHeld(first);
    held.open();

    await expectFocusOnNewRetry(clicked);
    expect(reads).toHaveLength(2);
  });
});

// mod-016 design § 2.2, DC-12: a 404 «Организация не найдена» from the page's own
// read turns the page into the gate's full-page frame at once, and focus goes to
// the main region (P-10) — the page that would have focused its heading is gone.
describe('QA re-check 1, D-5: a retry that ends in «Организация не найдена» puts focus in the main region', () => {
  async function expectFocusInMainRegion() {
    const heading = await screen.findByRole('heading', {
      level: 1,
      name: 'Организация не найдена',
    });
    await waitFor(() => expect(document.activeElement).toBe(screen.getByRole('main')));
    expect(document.activeElement).not.toBe(document.body);
    expect(screen.getAllByRole('heading', { level: 1 })).toEqual([heading]);
    return heading;
  }

  it('moves focus from the removed «Повторить» to the main region after a 500 and then the not-found code', async () => {
    serveInOrder(serverError, notFound);
    renderApp();
    const alert = await expectFailedRead();

    await retry(alert);

    await expectFocusInMainRegion();
    await waitFor(() => expect(document.title).toBe('Организация не найдена — Mustask'));
    expect(screen.queryByRole('button', { name: 'Повторить' })).not.toBeInTheDocument();
    expect(reads).toHaveLength(2);
  });

  it('lets the next Tab reach «На главную» from the main region', async () => {
    serveInOrder(serverError, notFound);
    renderApp();
    const alert = await expectFailedRead();
    const user = await retry(alert);
    await expectFocusInMainRegion();

    await user.tab();

    expect(screen.getByRole('link', { name: 'На главную' })).toHaveFocus();
  });

  it('follows every attempt: «Повторить» after a second 500, the main region after the not-found code', async () => {
    const held = gate();
    serveInOrder(serverError, afterGate(held, serverError), notFound);
    renderApp();
    const first = await expectFailedRead();

    const clicked = await retryHeld(first);
    held.open();
    const second = await expectFocusOnNewRetry(clicked);

    await retry(second);

    await expectFocusInMainRegion();
    expect(reads).toHaveLength(3);
  });

  it('puts focus in the main region when the first answer was an unreadable 200', async () => {
    serveInOrder(
      broken((body) => {
        delete typeAt(body, 0).backlogLevel;
      }),
      notFound,
    );
    renderApp();
    const alert = await expectFailedRead();

    await retry(alert);

    await expectFocusInMainRegion();
  });

  it('puts focus back on «Повторить» when a retry after a 500 gets an unreadable 200', async () => {
    const held = gate();
    serveInOrder(
      serverError,
      afterGate(
        held,
        broken((body) => {
          delete typeAt(body, 0).states;
        }),
      ),
    );
    renderApp();
    const alert = await expectFailedRead();

    const clicked = await retryHeld(alert);
    held.open();

    await expectFocusOnNewRetry(clicked);
  });

  it('moves no focus when the access read already finds no organization, and the page reads nothing', async () => {
    server.use(http.get(ACCESS_URL, notFoundBody));
    serveInOrder(good);
    renderApp();

    await screen.findByRole('heading', { level: 1, name: 'Организация не найдена' });
    expect(document.activeElement).toBe(document.body);
    expect(reads).toEqual([]);
  });
});
