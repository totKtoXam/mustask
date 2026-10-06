import { describe, expect, it, vi } from 'vitest';
import type { ReactNode } from 'react';
import { MemoryRouter, useNavigate } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, renderHook, screen, waitFor } from '@testing-library/react';
import { HttpResponse, http } from 'msw';
import { server } from '../../../test/mocks/server';
import { LocationProbe } from '../../../test/renderShell';
import type { SwitchTargetResolver } from '../../../shared/lib/workContext';
import { organizationsKeys } from '../api/queryKeys';
import { TEAMS_URL, ids, problem } from '../testFixtures';
import { serveOrganizations, serveProblem } from '../testApi';
import { useOrganizationsContextSource } from './useOrganizationsContextSource';

// mod-015 BR-18 on a team-level address (mod-006's «Бэклоги» and «Доски»;
// review F1): a project or organization choice moves the page to the same
// entry for the first team of the project it lands in. The source reads the
// carried levels' first entries before it asks the resolver — a project's
// first team (mod-002 design § 3.5), an organization's first project and its
// first team (design § 2.2) — and keeps the page while it reads; a failed read
// opens «Главная» (plan-frontend § 4.5). The resolver here is a stand-in that
// names what it was asked; `switchTarget.test.ts` and `routes.boards.test.tsx`
// cover what the real one answers.

const SUBJECT = 'user-1';
const TEAM_PAGE = `/organizations/${ids.alfa}/projects/${ids.prm}/teams/${ids.platformTeam}/backlogs/features`;
const CARRIED = { organizationId: ids.alfa, projectId: ids.prm, teamId: ids.platformTeam };

function mount(path: string, resolver: SwitchTargetResolver) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[path]}>
        {children}
        <LocationProbe />
      </MemoryRouter>
    </QueryClientProvider>
  );
  const rendered = renderHook(
    () => ({
      source: useOrganizationsContextSource({
        enabled: true,
        subject: SUBJECT,
        resolveSwitchTarget: resolver,
      }),
      navigate: useNavigate(),
    }),
    { wrapper },
  );
  return { ...rendered, queryClient };
}

/** A resolver that answers with the project and team it was asked about. */
function namingResolver() {
  return vi.fn<SwitchTargetResolver>(({ next }) => `/to/${next.projectId}/${next.teamId}`);
}

const location = () => screen.getByTestId('location');

/** Holds the teams read of one project until `release()`, then lets the in-memory API answer it. */
function holdTeamsOf(projectId: string) {
  let release: () => void = () => {};
  let arrived = false;
  const held = new Promise<void>((resolve) => {
    release = resolve;
  });
  server.use(
    http.get(TEAMS_URL, async ({ params }) => {
      if (params.projectId === projectId) {
        arrived = true;
        await held;
      }
      return undefined;
    }),
  );
  return { release, arrived: () => arrived };
}

/** Lets a released read and whatever it resolves run to the end. */
async function settle(queryClient: QueryClient, projectId: string) {
  await waitFor(() =>
    expect(queryClient.getQueryState(organizationsKeys.teams(ids.alfa, projectId))?.status).toBe(
      'success',
    ),
  );
  await act(() => new Promise((resolve) => setTimeout(resolve, 0)));
}

/** Mounted on «Платформа»'s «Фичи» backlog, the switcher synced to it. */
async function onTeamPage(resolver: SwitchTargetResolver) {
  const mounted = mount(TEAM_PAGE, resolver);
  await waitFor(() =>
    expect(mounted.result.current.source.selection.teamId).toBe(ids.platformTeam),
  );
  return mounted;
}

describe('mod-015 BR-18 on a team-level page: the source reads the first entries, then moves the page', () => {
  it('a project choice reads the new project’s teams first, then asks with its first team', async () => {
    serveOrganizations();
    const resolver = namingResolver();
    const { result } = await onTeamPage(resolver);

    act(() => result.current.source.select('project', ids.anl));

    await waitFor(() => expect(location()).toHaveTextContent(`/to/${ids.anl}/${ids.anlTeam}`));
    expect(resolver).toHaveBeenCalledTimes(1);
    expect(resolver).toHaveBeenCalledWith({
      pathname: TEAM_PAGE,
      carried: CARRIED,
      next: { organizationId: ids.alfa, projectId: ids.anl, teamId: ids.anlTeam },
    });
  });

  it('an organization choice reads its first project, then that project’s teams, then asks with both', async () => {
    serveOrganizations();
    const resolver = namingResolver();
    const { result } = await onTeamPage(resolver);

    act(() => result.current.source.select('organization', ids.beta));

    await waitFor(() => expect(location()).toHaveTextContent(`/to/${ids.rep}/${ids.repTeam}`));
    expect(resolver).toHaveBeenCalledTimes(1);
    expect(resolver).toHaveBeenCalledWith({
      pathname: TEAM_PAGE,
      carried: CARRIED,
      next: { organizationId: ids.beta, projectId: ids.rep, teamId: ids.repTeam },
    });
  });

  it('a team choice asks at once, with nothing to read', async () => {
    serveOrganizations();
    const resolver = namingResolver();
    const { result } = await onTeamPage(resolver);

    act(() => result.current.source.select('team', ids.prmTeam));

    expect(resolver).toHaveBeenCalledWith({
      pathname: TEAM_PAGE,
      carried: CARRIED,
      next: { organizationId: ids.alfa, projectId: ids.prm, teamId: ids.prmTeam },
    });
    await waitFor(() => expect(location()).toHaveTextContent(`/to/${ids.prm}/${ids.prmTeam}`));
  });

  it('an organization with no project: asks with no project, and reads no team', async () => {
    const api = serveOrganizations();
    api.data.projects.find((project) => project.id === ids.rep)!.active = false;
    const resolver = namingResolver();
    const { result } = await onTeamPage(resolver);

    act(() => result.current.source.select('organization', ids.beta));

    await waitFor(() => expect(resolver).toHaveBeenCalledTimes(1));
    expect(resolver).toHaveBeenCalledWith({
      pathname: TEAM_PAGE,
      carried: CARRIED,
      next: { organizationId: ids.beta, projectId: undefined },
    });
    expect(
      api.requests.filter((request) => request.path.includes(`/${ids.beta}/projects/`)),
    ).toEqual([]);
  });

  it('a project whose teams list comes back empty: asks with no team', async () => {
    serveOrganizations();
    server.use(
      http.get(TEAMS_URL, ({ params }) =>
        params.projectId === ids.anl ? HttpResponse.json([]) : undefined,
      ),
    );
    const resolver = namingResolver();
    const { result } = await onTeamPage(resolver);

    act(() => result.current.source.select('project', ids.anl));

    await waitFor(() => expect(resolver).toHaveBeenCalledTimes(1));
    expect(resolver).toHaveBeenCalledWith({
      pathname: TEAM_PAGE,
      carried: CARRIED,
      next: { organizationId: ids.alfa, projectId: ids.anl, teamId: undefined },
    });
  });

  it('opens «Главная» when the new project’s teams cannot be read, and the team level shows the failure', async () => {
    serveOrganizations();
    const resolver = namingResolver();
    const { result } = await onTeamPage(resolver);
    serveProblem('get', TEAMS_URL, problem('organizations.validation_failed'));

    act(() => result.current.source.select('project', ids.anl));

    await waitFor(() => expect(location()).toHaveTextContent('/home'));
    expect(resolver).not.toHaveBeenCalled();
    await waitFor(() => expect(result.current.source.levelStatus?.team).toBe('failed'));
    expect(result.current.source.selection.projectId).toBe(ids.anl);
  });

  it('keeps the page while the teams read is slow, and moves only once it answers', async () => {
    serveOrganizations();
    const hold = holdTeamsOf(ids.anl);
    const resolver = namingResolver();
    const { result } = await onTeamPage(resolver);

    act(() => result.current.source.select('project', ids.anl));
    await waitFor(() => expect(hold.arrived()).toBe(true));
    await act(() => new Promise((resolve) => setTimeout(resolve, 0)));

    expect(location()).toHaveTextContent(TEAM_PAGE);
    expect(resolver).not.toHaveBeenCalled();
    expect(result.current.source.levelStatus?.team).toBe('loading');

    hold.release();
    await waitFor(() => expect(location()).toHaveTextContent(`/to/${ids.anl}/${ids.anlTeam}`));
    expect(resolver).toHaveBeenCalledTimes(1);
  });

  it('a later choice overtakes a pending one: only the latest moves the page', async () => {
    serveOrganizations();
    const anl = holdTeamsOf(ids.anl);
    const rep = holdTeamsOf(ids.rep);
    const resolver = namingResolver();
    const { result, queryClient } = await onTeamPage(resolver);

    act(() => result.current.source.select('project', ids.anl));
    await waitFor(() => expect(anl.arrived()).toBe(true));
    act(() => result.current.source.select('organization', ids.beta));
    await waitFor(() => expect(rep.arrived()).toBe(true));

    anl.release();
    await settle(queryClient, ids.anl);
    expect(resolver).not.toHaveBeenCalled();
    expect(location()).toHaveTextContent(TEAM_PAGE);

    rep.release();
    await waitFor(() => expect(location()).toHaveTextContent(`/to/${ids.rep}/${ids.repTeam}`));
    expect(resolver).toHaveBeenCalledTimes(1);
  });

  it('leaving the page while the read is pending drops the move', async () => {
    serveOrganizations();
    const hold = holdTeamsOf(ids.anl);
    const resolver = namingResolver();
    const { result, queryClient } = await onTeamPage(resolver);

    act(() => result.current.source.select('project', ids.anl));
    await waitFor(() => expect(hold.arrived()).toBe(true));
    act(() => {
      void result.current.navigate('/dictionaries/languages');
    });
    expect(location()).toHaveTextContent('/dictionaries/languages');

    hold.release();
    await settle(queryClient, ids.anl);
    expect(resolver).not.toHaveBeenCalled();
    expect(location()).toHaveTextContent('/dictionaries/languages');
  });
});
