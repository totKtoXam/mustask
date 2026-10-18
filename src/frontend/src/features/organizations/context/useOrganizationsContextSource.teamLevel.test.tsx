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
import { ORG_TEAMS_URL, ids, problem } from '../testFixtures';
import { serveOrganizations, serveProblem } from '../testApi';
import { useOrganizationsContextSource } from './useOrganizationsContextSource';

// mod-015 BR-18 on a team-level address (feat-040 BR-TS-1: `…/teams/:t/…`, no project in it): a
// project choice moves nothing, since the address carries no project level; a team choice moves
// the page to the same entry's landing for that team; an organization choice reads the new
// organization's first project and the team its switcher falls back to — its default team, else
// the level's first entry — before it asks the resolver, and keeps the page while it reads; a
// failed read opens «Главная» (plan-frontend § 4.5). The resolver here is a stand-in that names
// what it was asked; `switchTarget.test.ts` and `routes.boards.test.tsx` cover what the real one
// answers.

const SUBJECT = 'user-1';
const TEAM_PAGE = `/organizations/${ids.alfa}/teams/${ids.platformTeam}/backlogs/features`;
const CARRIED = { organizationId: ids.alfa, teamId: ids.platformTeam };

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

/** Holds the teams read of one organization until `release()`, then lets the in-memory API answer it. */
function holdTeamsOf(organizationId: string) {
  let release: () => void = () => {};
  let arrived = false;
  const held = new Promise<void>((resolve) => {
    release = resolve;
  });
  server.use(
    http.get(ORG_TEAMS_URL, async ({ params }) => {
      if (params.organizationId === organizationId) {
        arrived = true;
        await held;
      }
      return undefined;
    }),
  );
  return { release, arrived: () => arrived };
}

/** Lets a released read and whatever it resolves run to the end. */
async function settle(queryClient: QueryClient, organizationId: string) {
  await waitFor(() =>
    expect(queryClient.getQueryState(organizationsKeys.orgTeams(organizationId))?.status).toBe(
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
  await waitFor(() => expect(mounted.result.current.source.levelStatus?.project).toBe('ready'));
  return mounted;
}

describe('mod-015 BR-18 on a team-level page: the source reads the first entries, then moves the page', () => {
  it('a project choice moves nothing: the address carries no project level, the team stays chosen', async () => {
    serveOrganizations();
    const resolver = namingResolver();
    const { result } = await onTeamPage(resolver);

    act(() => result.current.source.select('project', ids.prm));

    await waitFor(() => expect(result.current.source.selection.projectId).toBe(ids.prm));
    expect(result.current.source.selection.teamId).toBe(ids.platformTeam);
    expect(resolver).not.toHaveBeenCalled();
    expect(location()).toHaveTextContent(TEAM_PAGE);
  });

  it('a team address sets the organization and the team, and the project level shows its first entry', async () => {
    serveOrganizations();
    const { result } = await onTeamPage(namingResolver());

    expect(result.current.source.selection).toEqual({
      organizationId: ids.alfa,
      projectId: ids.anl,
      teamId: ids.platformTeam,
    });
  });

  it('an organization choice reads its first project, then its teams, then asks with all of them', async () => {
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

  it('an organization choice lands on the first project’s default team, else the level’s first entry', async () => {
    const api = serveOrganizations();
    api.data.teams.push({
      id: 'c0000000-0000-4000-8000-0000000000b2',
      organizationId: ids.beta,
      name: 'Вторая',
      deleted: false,
    });
    api.data.links.push({ projectId: ids.rep, teamId: 'c0000000-0000-4000-8000-0000000000b2' });
    api.data.projects.find((project) => project.id === ids.rep)!.defaultTeamId =
      'c0000000-0000-4000-8000-0000000000b2';
    const resolver = namingResolver();
    const { result } = await onTeamPage(resolver);

    act(() => result.current.source.select('organization', ids.beta));

    await waitFor(() =>
      expect(location()).toHaveTextContent(`/to/${ids.rep}/c0000000-0000-4000-8000-0000000000b2`),
    );
  });

  it('a team choice asks at once, with nothing to read, and names the project level as it stands', async () => {
    serveOrganizations();
    const resolver = namingResolver();
    const { result } = await onTeamPage(resolver);

    act(() => result.current.source.select('team', ids.prmTeam));

    expect(resolver).toHaveBeenCalledWith({
      pathname: TEAM_PAGE,
      carried: CARRIED,
      next: { organizationId: ids.alfa, projectId: ids.anl, teamId: ids.prmTeam },
    });
    await waitFor(() => expect(location()).toHaveTextContent(`/to/${ids.anl}/${ids.prmTeam}`));
  });

  it('an organization with no project still has its team: asks with the team and no project', async () => {
    const api = serveOrganizations();
    api.data.projects.find((project) => project.id === ids.rep)!.active = false;
    const resolver = namingResolver();
    const { result } = await onTeamPage(resolver);

    act(() => result.current.source.select('organization', ids.beta));

    await waitFor(() => expect(resolver).toHaveBeenCalledTimes(1));
    expect(resolver).toHaveBeenCalledWith({
      pathname: TEAM_PAGE,
      carried: CARRIED,
      next: { organizationId: ids.beta, projectId: undefined, teamId: ids.repTeam },
    });
  });

  it('an organization with no team: asks with no team', async () => {
    const api = serveOrganizations();
    api.data.teams = api.data.teams.filter((candidate) => candidate.organizationId !== ids.beta);
    const resolver = namingResolver();
    const { result } = await onTeamPage(resolver);

    act(() => result.current.source.select('organization', ids.beta));

    await waitFor(() => expect(resolver).toHaveBeenCalledTimes(1));
    expect(resolver).toHaveBeenCalledWith({
      pathname: TEAM_PAGE,
      carried: CARRIED,
      next: { organizationId: ids.beta, projectId: ids.rep, teamId: undefined },
    });
  });

  it('an organization’s teams list comes back empty: asks with no team', async () => {
    serveOrganizations();
    server.use(
      http.get(ORG_TEAMS_URL, ({ params }) =>
        params.organizationId === ids.beta ? HttpResponse.json([]) : undefined,
      ),
    );
    const resolver = namingResolver();
    const { result } = await onTeamPage(resolver);

    act(() => result.current.source.select('organization', ids.beta));

    await waitFor(() => expect(resolver).toHaveBeenCalledTimes(1));
    expect(resolver).toHaveBeenCalledWith({
      pathname: TEAM_PAGE,
      carried: CARRIED,
      next: { organizationId: ids.beta, projectId: ids.rep, teamId: undefined },
    });
  });

  it('opens «Главная» when the new organization’s teams cannot be read, and the team level shows the failure', async () => {
    serveOrganizations();
    const resolver = namingResolver();
    const { result } = await onTeamPage(resolver);
    serveProblem('get', ORG_TEAMS_URL, problem('organizations.validation_failed'));

    act(() => result.current.source.select('organization', ids.beta));

    await waitFor(() => expect(location()).toHaveTextContent('/home'));
    expect(resolver).not.toHaveBeenCalled();
    await waitFor(() => expect(result.current.source.levelStatus?.team).toBe('failed'));
    expect(result.current.source.selection.organizationId).toBe(ids.beta);
  });

  it('keeps the page while the teams read is slow, and moves only once it answers', async () => {
    serveOrganizations();
    const hold = holdTeamsOf(ids.beta);
    const resolver = namingResolver();
    const { result } = await onTeamPage(resolver);

    act(() => result.current.source.select('organization', ids.beta));
    await waitFor(() => expect(hold.arrived()).toBe(true));
    await act(() => new Promise((resolve) => setTimeout(resolve, 0)));

    expect(location()).toHaveTextContent(TEAM_PAGE);
    expect(resolver).not.toHaveBeenCalled();
    expect(result.current.source.levelStatus?.team).toBe('loading');

    hold.release();
    await waitFor(() => expect(location()).toHaveTextContent(`/to/${ids.rep}/${ids.repTeam}`));
    expect(resolver).toHaveBeenCalledTimes(1);
  });

  it('a later choice overtakes a pending one: only the latest moves the page', async () => {
    serveOrganizations();
    const beta = holdTeamsOf(ids.beta);
    const resolver = namingResolver();
    const { result, queryClient } = await onTeamPage(resolver);

    act(() => result.current.source.select('organization', ids.beta));
    await waitFor(() => expect(beta.arrived()).toBe(true));
    // The team choice of the page it was made on moves it at once, and drops the pending move.
    act(() => result.current.source.select('team', ids.platformTeam));
    expect(resolver).toHaveBeenCalledTimes(1);
    await waitFor(() => expect(location()).not.toHaveTextContent(TEAM_PAGE));

    beta.release();
    await settle(queryClient, ids.beta);
    expect(resolver).toHaveBeenCalledTimes(1);
  });

  it('leaving the page while the read is pending drops the move', async () => {
    serveOrganizations();
    const hold = holdTeamsOf(ids.beta);
    const resolver = namingResolver();
    const { result, queryClient } = await onTeamPage(resolver);

    act(() => result.current.source.select('organization', ids.beta));
    await waitFor(() => expect(hold.arrived()).toBe(true));
    act(() => {
      void result.current.navigate('/dictionaries/languages');
    });
    expect(location()).toHaveTextContent('/dictionaries/languages');

    hold.release();
    await settle(queryClient, ids.beta);
    expect(resolver).not.toHaveBeenCalled();
    expect(location()).toHaveTextContent('/dictionaries/languages');
  });
});
