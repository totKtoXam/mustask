import { describe, expect, it } from 'vitest';
import { ApiError } from '../../shared/api/httpClient';
import {
  createProject,
  createTeam,
  deleteTeam,
  describeTeam,
  getAreaTree,
  getDeletionCounts,
  getOrganizationTeams,
  getOrganizations,
  getProjects,
  getTeam,
  getTeamProjects,
  getTeams,
  getUnlinkCount,
  linkProjectsToTeam,
  linkTeamsToProject,
  renameTeam,
  setDefaultTeam,
  unlinkTeam,
} from './api/organizationsApi';
import { emptyDataset, exampleDataset, ids, noTeamsDataset, ownedKey } from './testFixtures';
import { serveOrganizations } from './testApi';

// Retired by name (doc-012 P-1, feat-028 BR-OL-10): «deletes any team but the last, clears a default naming
// it, and counts what it owns (US-7)» — the last-team guard is gone; its twin is «deletes a team that is the
// last of its project…» below. «creates a project with its one team (the default as the only one) and root,
// and a team with no area (BR-OT-11)» is retired with the project's own team (BR-OL-2); its twin creates a
// project with no team.
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
    // feat-028 BR-OL-1: the organization's list, by name, with the active projects of each.
    expect((await getOrganizationTeams(ids.alfa)).map((t) => [t.name, t.projects.length])).toEqual([
      ['Аналитика', 1],
      ['Платформа', 1],
      ['Пример проекта', 1],
      ['Старый проект', 0],
    ]);
    // BR-OL-7: by name, no team first.
    expect((await getTeams(ids.alfa, ids.prm)).map((t) => t.name)).toEqual([
      'Платформа',
      'Пример проекта',
    ]);
  });

  it('serves an instance with no organization, and an organization with no team (US-7)', async () => {
    serveOrganizations(emptyDataset());
    expect(await getOrganizations()).toEqual([]);

    serveOrganizations(noTeamsDataset());
    expect(await getOrganizationTeams(ids.alfa)).toEqual([]);
    expect(await getTeams(ids.alfa, ids.prm)).toEqual([]);
  });

  it('answers the outermost level not found (api-contract § 0.4)', async () => {
    serveOrganizations();

    expect(await codeOf(getProjects(ids.gamma))).toBe('organizations.organization.not_found');
    expect(await codeOf(getTeams(ids.alfa, ids.old))).toBe('organizations.project.not_found');
    expect(await codeOf(getTeams(ids.beta, ids.prm))).toBe('organizations.project.not_found');
    expect(await codeOf(getTeam(ids.gamma, ids.arcTeam))).toBe(
      'organizations.organization.not_found',
    );
    // A team of another organization is a team that names nothing.
    expect(await codeOf(getTeam(ids.alfa, ids.repTeam))).toBe('organizations.team.not_found');
  });

  it('creates a project with no team, and a team with its description, members and links (BR-OL-2)', async () => {
    const api = serveOrganizations();

    const project = await createProject(ids.beta, { name: 'Ядро', key: 'CORE', description: 'Д' });
    expect(project.description).toBe('Д');
    expect(await getTeams(ids.beta, project.id)).toEqual([]);
    const treeBefore = await getAreaTree(ids.beta);

    const team = await createTeam(ids.beta, {
      name: 'Сервис',
      description: ' Первая\nВторая',
      projectIds: [project.id],
    });
    expect(team).toEqual({ id: team.id, name: 'Сервис', description: ' Первая\nВторая' });
    expect(await getAreaTree(ids.beta)).toEqual(treeBefore);
    // One linked team and no setting: it is the project's default as the only one.
    expect((await getTeams(ids.beta, project.id)).map((t) => [t.name, t.defaultMark])).toEqual([
      ['Сервис', 'onlyTeam'],
    ]);
    expect(api.writes()).toHaveLength(2);
  });

  it('refuses a name held by an active team of the organization, in any case, and frees a deleted team’s', async () => {
    serveOrganizations();

    expect(await codeOf(createTeam(ids.alfa, { name: ' платформа ' }))).toBe(
      'organizations.team.name_taken',
    );
    // Another organization may share it.
    expect((await createTeam(ids.beta, { name: 'Платформа' })).name).toBe('Платформа');
    await deleteTeam(ids.alfa, ids.platformTeam);
    expect((await createTeam(ids.alfa, { name: 'Платформа' })).name).toBe('Платформа');
    expect(await codeOf(renameTeam(ids.alfa, ids.anlTeam, 'пример проекта'))).toBe(
      'organizations.team.name_taken',
    );
  });

  it('answers a project of another organization, in a team’s creation, as a project that names nothing', async () => {
    serveOrganizations();

    expect(await codeOf(createTeam(ids.alfa, { name: 'Х', projectIds: [ids.rep] }))).toBe(
      'organizations.project.not_found',
    );
    expect((await getOrganizationTeams(ids.alfa)).map((t) => t.name)).not.toContain('Х');
  });

  it('keeps a description exactly as sent, an empty one being none, and refuses over 2 000 (BR-OL-3)', async () => {
    serveOrganizations();

    expect((await describeTeam(ids.alfa, ids.platformTeam, '  a\nb  ')).description).toBe(
      '  a\nb  ',
    );
    expect((await describeTeam(ids.alfa, ids.platformTeam, '')).description).toBeNull();
    expect(await codeOf(describeTeam(ids.alfa, ids.platformTeam, 'я'.repeat(2001)))).toBe(
      'organizations.validation_failed',
    );
  });

  it('links once, all or nothing, and lists the active projects of a team and the teams of a project', async () => {
    serveOrganizations();

    await linkProjectsToTeam(ids.alfa, ids.platformTeam, [ids.anl, ids.anl]);
    await linkProjectsToTeam(ids.alfa, ids.platformTeam, [ids.anl]);
    expect((await getTeamProjects(ids.alfa, ids.platformTeam)).map((p) => p.name)).toEqual([
      'Аналитика',
      'Пример проекта',
    ]);
    expect(await codeOf(linkProjectsToTeam(ids.alfa, ids.platformTeam, [ids.old, ids.prm]))).toBe(
      'organizations.project.not_found',
    );

    await linkTeamsToProject(ids.alfa, ids.anl, [ids.prmTeam]);
    // «Платформа» was linked to «Аналитика» above, «Пример проекта» just now.
    expect((await getTeams(ids.alfa, ids.anl)).map((t) => t.name)).toEqual([
      'Аналитика',
      'Платформа',
      'Пример проекта',
    ]);
    expect(await codeOf(linkTeamsToProject(ids.alfa, ids.anl, [ids.repTeam]))).toBe(
      'organizations.team.not_found',
    );
  });

  it('names the default team among the linked ones only: 409 for an unlinked, 404 for a foreign one (BR-OL-6)', async () => {
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

  it('unlinks with a destination: the items move, the setting naming the team is cleared (SC-09)', async () => {
    const data = exampleDataset();
    data.ownedItemCounts = { [ownedKey(ids.prm, ids.platformTeam)]: 7 };
    serveOrganizations(data);
    await setDefaultTeam(ids.alfa, ids.prm, ids.platformTeam);

    expect(await getUnlinkCount(ids.alfa, ids.prm, ids.platformTeam)).toEqual({ count: 7 });
    expect(await codeOf(unlinkTeam(ids.alfa, ids.prm, ids.platformTeam, ids.anlTeam))).toBe(
      'organizations.team.not_in_project',
    );
    await unlinkTeam(ids.alfa, ids.prm, ids.platformTeam, ids.prmTeam);

    expect(await getUnlinkCount(ids.alfa, ids.prm, ids.prmTeam)).toEqual({ count: 7 });
    expect((await getTeams(ids.alfa, ids.prm)).map((t) => [t.name, t.defaultMark])).toEqual([
      ['Пример проекта', 'onlyTeam'],
    ]);
    // Unlinking an unlinked pair succeeds and changes nothing; the team itself stays.
    await unlinkTeam(ids.alfa, ids.prm, ids.platformTeam, null);
    expect(await codeOf(getUnlinkCount(ids.alfa, ids.prm, ids.platformTeam))).toBe(
      'organizations.team.not_found',
    );
    expect((await getTeam(ids.alfa, ids.platformTeam)).name).toBe('Платформа');
  });

  it('deletes a team that is the last of its project: counts per project first, then the items go, the link and the setting end (BR-OL-9, BR-OL-10)', async () => {
    const data = exampleDataset();
    data.ownedItemCounts = {
      [ownedKey(ids.anl, ids.anlTeam)]: 4,
      [ownedKey(ids.prm, ids.anlTeam)]: 0,
    };
    data.links.push(
      { projectId: ids.prm, teamId: ids.anlTeam },
      { projectId: ids.old, teamId: ids.anlTeam },
    );
    data.ownedItemCounts[ownedKey(ids.old, ids.anlTeam)] = 3;
    serveOrganizations(data);

    // By project name, zeros and the deactivated project included.
    expect(await getDeletionCounts(ids.alfa, ids.anlTeam)).toEqual({
      projects: [
        { projectId: ids.anl, name: 'Аналитика', active: true, count: 4 },
        { projectId: ids.prm, name: 'Пример проекта', active: true, count: 0 },
        { projectId: ids.old, name: 'Старый проект', active: false, count: 3 },
      ],
    });
    await deleteTeam(ids.alfa, ids.anlTeam);

    expect(await codeOf(getTeam(ids.alfa, ids.anlTeam))).toBe('organizations.team.not_found');
    // BR-OL-10: the project is left with no team, and the guard code is not answered.
    expect(await getTeams(ids.alfa, ids.anl)).toEqual([]);
  });
});
