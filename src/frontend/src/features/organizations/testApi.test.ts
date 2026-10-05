import { describe, expect, it } from 'vitest';
import { ApiError } from '../../shared/api/httpClient';
import {
  addTeam,
  createProject,
  deleteTeam,
  getAreaTree,
  getOrganizations,
  getProjects,
  getTeam,
  getTeams,
} from './api/organizationsApi';
import { emptyDataset, ids } from './testFixtures';
import { serveOrganizations } from './testApi';

async function codeOf(promise: Promise<unknown>): Promise<string | undefined> {
  const failure = await promise.catch((error: unknown) => error);
  return failure instanceof ApiError ? failure.errorCode : undefined;
}

describe('the slice’s test API answers per api-contract (a check of the test double itself)', () => {
  it('lists in display order and leaves out what is deactivated', async () => {
    serveOrganizations();

    expect((await getOrganizations()).map((o) => o.name)).toEqual(['Альфа', 'Бета']);
    expect((await getOrganizations('deactivated')).map((o) => o.name)).toEqual(['Гамма']);
    expect((await getProjects(ids.alfa)).map((p) => p.name)).toEqual([
      'Аналитика',
      'Пример проекта',
    ]);
    expect((await getTeams(ids.alfa, ids.prm)).map((t) => t.name)).toEqual([
      'Пример проекта',
      'Платформа',
    ]);
  });

  it('serves an instance with no organization', async () => {
    serveOrganizations(emptyDataset());

    expect(await getOrganizations()).toEqual([]);
  });

  it('answers the outermost level not found (api-contract § 0.3)', async () => {
    serveOrganizations();

    expect(await codeOf(getProjects(ids.gamma))).toBe('organizations.organization.not_found');
    expect(await codeOf(getTeams(ids.alfa, ids.old))).toBe('organizations.project.not_found');
    expect(await codeOf(getTeams(ids.beta, ids.prm))).toBe('organizations.project.not_found');
    expect(await codeOf(getTeam(ids.gamma, ids.arc, ids.arcTeam))).toBe(
      'organizations.organization.not_found',
    );
    expect(await codeOf(getTeam(ids.alfa, ids.prm, ids.anlTeam))).toBe(
      'organizations.team.not_found',
    );
  });

  it('creates a project with its first team and root, and a team with its own default area', async () => {
    const api = serveOrganizations();

    const project = await createProject(ids.beta, { name: 'Ядро', key: 'CORE' });
    const [first] = await getTeams(ids.beta, project.id);
    expect(first).toMatchObject({ name: 'Ядро', isFirstTeam: true, defaultArea: { path: 'Ядро' } });

    const team = await addTeam(ids.beta, project.id, 'Сервис');
    expect(team.defaultArea.path).toBe('Сервис');
    expect(api.writes()).toHaveLength(2);
  });

  it('passes a deleted team’s areas to the first team (US-7)', async () => {
    serveOrganizations();

    await deleteTeam(ids.alfa, ids.prm, ids.platformTeam);

    const tree = await getAreaTree(ids.alfa, ids.prm);
    expect(tree.find((node) => node.id === ids.apiArea)?.ownerTeamId).toBe(ids.prmTeam);
    expect(tree.every((node) => node.resolvedOwner.id === ids.prmTeam)).toBe(true);
    expect(await codeOf(getTeam(ids.alfa, ids.prm, ids.platformTeam))).toBe(
      'organizations.team.not_found',
    );
  });
});
