import { describe, expect, it, vi } from 'vitest';
import { Link, Route, useLocation } from 'react-router-dom';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { PageHeader } from '../../shared/ui/PageHeader';
import type { WorkContextOffers, WorkContextSource } from '../../shared/lib/workContext';
import { authValue, renderShell } from '../../test/renderShell';

// feat-043 US-8, design § 9: what the shell's host says and does around the four selectors —
// the fallback notice of a remembered context that is gone, the notice a link carries, and
// the focus that lands on the page's `h1` after a choice or a row link.

const ALFA = 'org-1';
const MARS = 'team-mars';
const VENUS = 'team-venus';

const OFFERS: WorkContextOffers = {
  departments: [],
  teams: [
    { id: MARS, name: 'MARS', isMember: true, departmentId: 'd1' },
    { id: VENUS, name: 'VENUS', isMember: false, departmentId: 'd1' },
  ],
  projects: [],
};

function source(overrides: Partial<WorkContextSource> = {}): WorkContextSource {
  return {
    status: 'ready',
    levelStatus: { project: 'ready', team: 'ready' },
    organizations: [{ id: ALFA, name: 'Альфа', projects: [], teams: [] }],
    selection: { organizationId: ALFA },
    offers: OFFERS,
    isReal: false,
    ...overrides,
  };
}

function LocationState() {
  const { state } = useLocation();
  return <span data-testid="location-state">{JSON.stringify(state)}</span>;
}

function page(title: string) {
  return (
    <>
      <PageHeader title={title} />
      <LocationState />
      <Link to={`/organizations/${ALFA}/teams/${VENUS}/overview`} state={{ focusHeading: true }}>
        К команде
      </Link>
      <Link
        to={`/organizations/${ALFA}/teams/${VENUS}/overview`}
        state={{ resetNotice: 'Выбор сброшен: проект A.' }}
      >
        К команде со сбросом
      </Link>
    </>
  );
}

const ROUTES = (
  <>
    <Route path="/home" element={page('Главная')} />
    <Route path={`/organizations/${ALFA}/teams/:teamId/overview`} element={page('Команда')} />
    <Route path="*" element={page('Другая страница')} />
  </>
);

function open(overrides: Partial<WorkContextSource> = {}, path = '/home') {
  return renderShell({
    path,
    auth: authValue([]),
    contextSource: source(overrides),
    routes: ROUTES,
  });
}

describe('feat-043 ADR-0032 § 6: the fallback notice', () => {
  it('shows the notice with the name of what is selected instead', async () => {
    open({ fallback: { id: 1, names: ['Альфа', 'MARS'] } });

    expect(
      await screen.findByText('Ранее выбранный контекст недоступен. Выбрано: Альфа / MARS.'),
    ).toBeInTheDocument();
  });

  it('shows the shorter notice when nothing is left', async () => {
    open({ organizations: [], selection: {}, fallback: { id: 1, names: [] } });

    expect(await screen.findByText('Ранее выбранный контекст недоступен.')).toBeInTheDocument();
  });

  it('shows the notice again for the next fallback, not for a re-render of the same one', async () => {
    const shell = open({ fallback: { id: 1, names: ['Альфа'] } });
    expect(
      await screen.findByText('Ранее выбранный контекст недоступен. Выбрано: Альфа.'),
    ).toBeInTheDocument();

    shell.update({ contextSource: source({ fallback: { id: 2, names: ['Альфа', 'VENUS'] } }) });

    expect(
      await screen.findByText('Ранее выбранный контекст недоступен. Выбрано: Альфа / VENUS.'),
    ).toBeInTheDocument();
  });
});

describe('feat-043 design § 9: focus after a choice', () => {
  it('lands on the h1 after a team is chosen in a selector', async () => {
    open();
    await screen.findByRole('heading', { level: 1, name: 'Главная' });

    await userEvent.click(screen.getByRole('combobox', { name: 'Команда: все' }));
    await userEvent.click(await screen.findByRole('option', { name: /VENUS/ }));

    const heading = await screen.findByRole('heading', { level: 1, name: 'Команда' });
    await waitFor(() => expect(heading).toHaveFocus());
  });

  it('lands on the h1 after a link that asks for it, and not after one that does not', async () => {
    open();
    await screen.findByRole('heading', { level: 1, name: 'Главная' });

    await userEvent.click(
      within(screen.getByRole('main')).getByRole('link', { name: 'К команде' }),
    );

    const heading = await screen.findByRole('heading', { level: 1, name: 'Команда' });
    await waitFor(() => expect(heading).toHaveFocus());
  });

  it('leaves focus alone on a plain navigation', async () => {
    open({}, `/organizations/${ALFA}/teams/${MARS}/overview`);
    await screen.findByRole('heading', { level: 1, name: 'Команда' });

    expect(screen.getByRole('heading', { level: 1, name: 'Команда' })).not.toHaveFocus();
  });
});

describe('feat-043 D-3: the notice a link carries', () => {
  it('shows «Выбор сброшен: …» for the navigation that carries it', async () => {
    open();
    await screen.findByRole('heading', { level: 1, name: 'Главная' });
    expect(screen.queryByText(/Выбор сброшен/)).not.toBeInTheDocument();

    await userEvent.click(
      within(screen.getByRole('main')).getByRole('link', { name: 'К команде со сбросом' }),
    );

    expect(await screen.findByText('Выбор сброшен: проект A.')).toBeInTheDocument();
  });

  it('takes the notice off the history entry once said: a reload or a remount does not say it again', async () => {
    open();
    await screen.findByRole('heading', { level: 1, name: 'Главная' });

    await userEvent.click(
      within(screen.getByRole('main')).getByRole('link', { name: 'К команде со сбросом' }),
    );
    await screen.findByText('Выбор сброшен: проект A.');

    await waitFor(() => expect(screen.getByTestId('location-state')).toHaveTextContent('{}'));
  });
});

describe('feat-043 C-1: a link change that unfits the selection', () => {
  const A = 'project-a';
  const linked: WorkContextOffers = {
    ...OFFERS,
    projects: [{ id: A, name: 'A', key: 'A', departmentId: null, teamIds: [MARS] }],
  };
  const unlinked: WorkContextOffers = {
    ...linked,
    projects: [{ id: A, name: 'A', key: 'A', departmentId: null, teamIds: [] }],
  };
  const selection = { organizationId: ALFA, teamId: MARS, projectId: A };
  const path = `/organizations/${ALFA}/projects/${A}/teams?teamId=${MARS}`;

  it('resets the project with the notice, remembers it and moves the page to the team', async () => {
    const remember = vi.fn();
    const shell = open({ offers: linked, selection, remember }, path);
    await screen.findByRole('heading', { level: 1, name: 'Другая страница' });
    expect(screen.queryByText(/Выбор сброшен/)).not.toBeInTheDocument();

    shell.update({ contextSource: source({ offers: unlinked, selection, remember }) });

    expect(await screen.findByText('Выбор сброшен: проект A.')).toBeInTheDocument();
    expect(remember).toHaveBeenCalledWith(ALFA, { department: null, team: MARS, project: null });
    expect(await screen.findByRole('heading', { level: 1, name: 'Команда' })).toBeInTheDocument();
  });

  it('leaves a page that names no object where it is, and remembers the reset (D-7)', async () => {
    const remember = vi.fn();
    const members = `/organizations/${ALFA}/members`;
    const shell = open({ offers: linked, selection, remember }, members);
    await screen.findByRole('heading', { level: 1, name: 'Другая страница' });

    shell.update({ contextSource: source({ offers: unlinked, selection, remember }) });

    expect(await screen.findByText('Выбор сброшен: проект A.')).toBeInTheDocument();
    expect(remember).toHaveBeenCalledWith(ALFA, { department: null, team: MARS, project: null });
    expect(screen.getByRole('heading', { level: 1, name: 'Другая страница' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { level: 1, name: 'Команда' })).not.toBeInTheDocument();
  });

  it('says nothing when the offers change and the pair still fits', async () => {
    const shell = open({ offers: linked, selection }, path);
    await screen.findByRole('heading', { level: 1, name: 'Другая страница' });

    shell.update({
      contextSource: source({ offers: { ...linked, projects: [...linked.projects] }, selection }),
    });

    expect(screen.queryByText(/Выбор сброшен/)).not.toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1, name: 'Другая страница' })).toBeInTheDocument();
  });

  it('says nothing for an address that did not fit at its first read', async () => {
    open({ offers: unlinked, selection }, path);
    await screen.findByRole('heading', { level: 1, name: 'Другая страница' });

    expect(screen.queryByText(/Выбор сброшен/)).not.toBeInTheDocument();
  });
});
