import { describe, expect, it } from 'vitest';
import { ApiError } from '../../shared/api/httpClient';
import {
  addTeam,
  createProject,
  deleteTeam,
  getAreaTree,
  getOrganizations,
  getOwnedItemsCount,
  getProjects,
  getTeam,
  getTeams,
  setDefaultTeam,
} from './api/organizationsApi';
import { emptyDataset, exampleDataset, ids } from './testFixtures';
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
    // mod-017 BR-OT-11: by name, no team first.
    expect((await getTeams(ids.alfa, ids.prm)).map((t) => t.name)).toEqual([
      'Платформа',
      'Пример проекта',
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

  it('creates a project with its one team (the default as the only one) and root, and a team with no area (BR-OT-11)', async () => {
    const api = serveOrganizations();

    const project = await createProject(ids.beta, { name: 'Ядро', key: 'CORE' });
    const [first] = await getTeams(ids.beta, project.id);
    expect(first).toMatchObject({ name: 'Ядро', defaultMark: 'onlyTeam' });
    const treeBefore = await getAreaTree(ids.beta, project.id);

    const team = await addTeam(ids.beta, project.id, 'Сервис');
    expect(team).toMatchObject({ name: 'Сервис', defaultMark: null });
    expect(await getAreaTree(ids.beta, project.id)).toEqual(treeBefore);
    // Two teams and no setting: nobody carries the mark.
    expect((await getTeams(ids.beta, project.id)).map((t) => t.defaultMark)).toEqual([null, null]);
    expect(api.writes()).toHaveLength(2);
  });

  it('names the default team, refuses the other project’s as 409 and an unknown as 404 (mod-017 § 0.2)', async () => {
    serveOrganizations();

    await setDefaultTeam(ids.alfa, ids.prm, ids.platformTeam);
    expect((await getTeams(ids.alfa, ids.prm)).map((t) => [t.name, t.defaultMark])).toEqual([
      ['Платформа', 'named'],
      ['Пример проекта', null],
    ]);
    expect(await codeOf(setDefaultTeam(ids.alfa, ids.prm, ids.anlTeam))).toBe(
      'organizations.team.not_in_project',
    );
    expect(await codeOf(setDefaultTeam(ids.alfa, ids.prm, ids.repTeam))).toBe(
      'organizations.team.not_found',
    );
    await setDefaultTeam(ids.alfa, ids.prm, null);
    expect((await getTeams(ids.alfa, ids.prm)).map((t) => t.defaultMark)).toEqual([null, null]);
  });

  it('deletes any team but the last, clears a default naming it, and counts what it owns (US-7)', async () => {
    const data = exampleDataset();
    data.ownedItemCounts = { [ids.platformTeam]: 5 };
    serveOrganizations(data);
    await setDefaultTeam(ids.alfa, ids.prm, ids.platformTeam);

    expect(await getOwnedItemsCount(ids.alfa, ids.prm, ids.platformTeam)).toEqual({ count: 5 });
    await deleteTeam(ids.alfa, ids.prm, ids.platformTeam);

    expect(await codeOf(getTeam(ids.alfa, ids.prm, ids.platformTeam))).toBe(
      'organizations.team.not_found',
    );
    // The setting went with the team, and the one left is the default as the only team.
    expect((await getTeams(ids.alfa, ids.prm)).map((t) => [t.name, t.defaultMark])).toEqual([
      ['Пример проекта', 'onlyTeam'],
    ]);
    expect(await codeOf(deleteTeam(ids.alfa, ids.prm, ids.prmTeam))).toBe(
      'organizations.team.last_team_cannot_be_deleted',
    );
  });
});
