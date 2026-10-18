import { describe, expect, it } from 'vitest';
import { servedFor } from '../../test/organizationRoles';
import { API, ids, exampleDataset, type Dataset, type MembershipRecord } from './testFixtures';
import { serveOrganizations } from './testApi';
import { codeOfRoute, serveMemberships } from './testMembership';

// The test double of the scoped check (api-contract § 0.1): the order of its
// answers, and what it lets through to `serveOrganizations()`'s routes.

const ME = 'user-1';

function datasetWith(...memberships: Pick<MembershipRecord, 'organizationId' | 'role'>[]): Dataset {
  const data = exampleDataset();
  data.actingUserId = ME;
  data.memberships = memberships.map((membership) => ({ ...membership, userId: ME }));
  return data;
}

function serve(data: Dataset) {
  serveOrganizations(data);
  serveMemberships(data);
}

function call(method: string, path: string, body?: unknown) {
  return fetch(`${API}/api/organizations${path}`, {
    method,
    headers: { 'Content-Type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
}

async function errorCodeOf(response: Response): Promise<string | undefined> {
  return ((await response.json()) as { errorCode?: string }).errorCode;
}

describe('the access read (api-contract § 1.1)', () => {
  it.each(['admin', 'member', 'viewer'] as const)('serves the %s bundle', async (role) => {
    serve(datasetWith({ organizationId: ids.alfa, role }));

    const response = await call('GET', `/${ids.alfa}/access`);

    expect(response.status).toBe(200);
    expect(await response.json()).toEqual(servedFor(role));
  });

  it('is the 404 «Организация не найдена» for a non-member, an unknown and a deactivated organization', async () => {
    serve(
      datasetWith(
        { organizationId: ids.alfa, role: 'admin' },
        { organizationId: ids.gamma, role: 'admin' },
      ),
    );

    for (const id of [ids.beta, 'a0000000-0000-4000-8000-0000000000ff', ids.gamma, 'not-an-id']) {
      const response = await call('GET', `/${id}/access`);
      expect(response.status).toBe(404);
      expect(await errorCodeOf(response)).toBe('organizations.organization.not_found');
    }
  });

  it('reads the dataset live: a role changed behind an open page is served at once', async () => {
    const data = datasetWith({ organizationId: ids.alfa, role: 'member' });
    serve(data);
    expect(await (await call('GET', `/${ids.alfa}/access`)).json()).toEqual(servedFor('member'));

    data.memberships![0]!.role = 'viewer';

    expect(await (await call('GET', `/${ids.alfa}/access`)).json()).toEqual(servedFor('viewer'));
  });

  it('a dataset that names no memberships makes the acting user administrator of every active organization', async () => {
    serve(exampleDataset());

    expect(await (await call('GET', `/${ids.beta}/access`)).json()).toEqual(servedFor('admin'));
    expect((await call('GET', `/${ids.gamma}/access`)).status).toBe(404);
  });
});

describe('the scoped guard (api-contract § 0.1 order)', () => {
  it('answers a non-member 404 before it looks at the code', async () => {
    serve(datasetWith({ organizationId: ids.alfa, role: 'admin' }));

    const response = await call('PUT', `/${ids.beta}/name`, { name: 'Новое' });

    expect(response.status).toBe(404);
    expect(await errorCodeOf(response)).toBe('organizations.organization.not_found');
  });

  it('answers a viewer 403 «Недостаточно прав» on a write, even for a project that does not exist', async () => {
    serve(datasetWith({ organizationId: ids.alfa, role: 'viewer' }));

    const rename = await call('PUT', `/${ids.alfa}/name`, { name: 'Новое' });
    expect(rename.status).toBe(403);
    expect(await errorCodeOf(rename)).toBe('organizations.access.forbidden');

    const unknownProject = await call('PUT', `/${ids.alfa}/projects/${ids.arc}/name`, {
      name: 'Новое',
    });
    expect(unknownProject.status).toBe(403);
  });

  it('lets a read through to the routes, which answer as before; a member reading an unknown project gets the project’s 404', async () => {
    serve(datasetWith({ organizationId: ids.alfa, role: 'member' }));

    expect((await call('GET', `/${ids.alfa}/projects`)).status).toBe(200);
    const unknown = await call('GET', `/${ids.alfa}/projects/${ids.arc}`);
    expect(unknown.status).toBe(404);
    expect(await errorCodeOf(unknown)).toBe('organizations.project.not_found');
  });

  it('holds the member to the administrator-only writes and the reader to the work writes', async () => {
    serve(
      datasetWith(
        { organizationId: ids.alfa, role: 'member' },
        { organizationId: ids.beta, role: 'viewer' },
      ),
    );

    expect((await call('POST', `/${ids.alfa}/projects`, { name: 'П', key: 'PP' })).status).toBe(
      403,
    );
    expect((await call('POST', `/${ids.beta}/projects/${ids.rep}/workitems`, {})).status).toBe(403);
    expect((await call('GET', `/${ids.beta}/projects/${ids.rep}`)).status).toBe(200);
  });

  it('accepts a deactivated organization only for the restore, and a non-member never', async () => {
    serve(
      datasetWith(
        { organizationId: ids.gamma, role: 'admin' },
        { organizationId: ids.alfa, role: 'admin' },
      ),
    );

    expect((await call('GET', `/${ids.gamma}`)).status).toBe(404);
    expect((await call('POST', `/${ids.gamma}/restore`)).status).toBe(200);
    expect((await call('POST', `/${ids.beta}/restore`)).status).toBe(404);
  });

  it('leaves the two lists and an unknown route alone', async () => {
    serve(datasetWith());

    expect((await call('GET', '')).status).toBe(200);
    expect((await call('GET', '/deactivated')).status).toBe(200);
  });
});

describe('the route table', () => {
  it('names the code of each kind of route', () => {
    expect(codeOfRoute('GET', '/projects')).toBe('organizations.project.read');
    expect(codeOfRoute('GET', '/projects/deactivated')).toBe('organizations.project.deactivate');
    expect(codeOfRoute('POST', '/members')).toBe('organizations.membership.write');
    expect(codeOfRoute('PUT', '/workitems/x')).toBeUndefined();
    expect(codeOfRoute('GET', '/workitems/assignee-candidates')).toBe('workitems.workitem.write');
    expect(codeOfRoute('POST', '/projects/p/workitems/MSK-US-1/delete')).toBe(
      'workitems.workitem.delete',
    );
    expect(codeOfRoute('POST', '/projects/p/workitems/MSK-US-1/restore')).toBe(
      'workitems.workitem.delete',
    );
    expect(codeOfRoute('POST', '/projects/p/workitems/MSK-US-1/purge')).toBe(
      'workitems.workitem.purge',
    );
    expect(codeOfRoute('POST', '/projects/p/teams/t/backlogs/stories/reorder')).toBe(
      'boards.backlog.reorder',
    );
  });
});
