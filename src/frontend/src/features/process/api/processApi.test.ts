import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { HttpResponse, http } from 'msw';
import { ApiError } from '../../../shared/api/httpClient';
import { server } from '../../../test/mocks/server';
import {
  DEMONSTRATION_ORGANIZATION_ID,
  PROCESS_DEFINITION_URL,
  cloneProcessDefinition,
  sampleProcessDefinition,
} from '../testFixtures';
import {
  UnreadableProcessDefinitionError,
  getProcessDefinition,
  processDefinitionApiPath,
} from './processApi';

/** § 1.4's body as plain JSON, so a test can take a required part out. */
type Loose = Record<string, unknown>;

function typeAt(definition: Loose, index: number): Loose {
  return (definition.workItemTypes as Loose[])[index]!;
}

function stateAt(type: Loose, index: number): Loose {
  return (type.states as Loose[])[index]!;
}

const requests: { method: string; url: string }[] = [];
const onRequest = ({ request }: { request: Request }) => {
  if (!request.url.includes('/api/session/')) {
    requests.push({ method: request.method, url: request.url });
  }
};

beforeEach(() => {
  requests.length = 0;
  server.events.on('request:start', onRequest);
});

afterEach(() => {
  server.events.removeListener('request:start', onRequest);
});

describe('processDefinitionApiPath (design DD-4)', () => {
  it('sends the id as the address carries it, re-encoded, never parsed', () => {
    expect(processDefinitionApiPath(DEMONSTRATION_ORGANIZATION_ID)).toBe(
      `/api/organizations/${DEMONSTRATION_ORGANIZATION_ID}/process`,
    );
    expect(processDefinitionApiPath('not-a-uuid')).toBe('/api/organizations/not-a-uuid/process');
    expect(processDefinitionApiPath('a/b')).toBe('/api/organizations/a%2Fb/process');
    expect(processDefinitionApiPath('a%2Fb')).toBe('/api/organizations/a%252Fb/process');
  });
});

describe('getProcessDefinition (api-contract § 1)', () => {
  it('sends one GET to the read and returns the body as it is', async () => {
    server.use(http.get(PROCESS_DEFINITION_URL, () => HttpResponse.json(sampleProcessDefinition)));

    const definition = await getProcessDefinition(DEMONSTRATION_ORGANIZATION_ID);

    expect(definition).toEqual(sampleProcessDefinition);
    expect(requests).toEqual([
      {
        method: 'GET',
        url: `http://localhost:5000/api/organizations/${DEMONSTRATION_ORGANIZATION_ID}/process`,
      },
    ]);
  });

  it.each([
    ['no workItemTypes', { id: 'p', backlogLevels: [] }],
    ['workItemTypes that is not an array', { id: 'p', backlogLevels: [], workItemTypes: {} }],
    ['an empty workItemTypes', { id: 'p', backlogLevels: [], workItemTypes: [] }],
    ['a null body', null],
  ])('treats a 200 with %s as a broken answer (design DD-5)', async (_case, body) => {
    server.use(http.get(PROCESS_DEFINITION_URL, () => HttpResponse.json(body)));

    const failure = getProcessDefinition(DEMONSTRATION_ORGANIZATION_ID);

    await expect(failure).rejects.toBeInstanceOf(UnreadableProcessDefinitionError);
    await expect(failure).rejects.not.toHaveProperty('name', 'TypeError');
  });

  // Review round 1 F2, qa.md D-4: a part the cards dereference is missing.
  // Without the check each of these rendered and threw a TypeError, which
  // replaced the whole shell with the crash screen.
  it.each<[string, (definition: Loose) => void]>([
    [
      'a type with no states',
      (definition) => {
        delete typeAt(definition, 0).states;
      },
    ],
    [
      'a type whose states is not an array',
      (definition) => {
        typeAt(definition, 2).states = {};
      },
    ],
    [
      'a type with no backlogLevel',
      (definition) => {
        delete typeAt(definition, 1).backlogLevel;
      },
    ],
    [
      'a state with no category',
      (definition) => {
        delete stateAt(typeAt(definition, 4), 3).category;
      },
    ],
    [
      'a type that is null',
      (definition) => {
        (definition.workItemTypes as unknown[])[3] = null;
      },
    ],
    [
      'a state that is null',
      (definition) => {
        (typeAt(definition, 0).states as unknown[])[0] = null;
      },
    ],
  ])(
    'treats a 200 with %s as a broken answer (design § States, Read failed)',
    async (_case, breakIt) => {
      const body = cloneProcessDefinition() as unknown as Loose;
      breakIt(body);
      server.use(http.get(PROCESS_DEFINITION_URL, () => HttpResponse.json(body)));

      const failure = getProcessDefinition(DEMONSTRATION_ORGANIZATION_ID);

      await expect(failure).rejects.toBeInstanceOf(UnreadableProcessDefinitionError);
      await expect(failure).rejects.not.toHaveProperty('name', 'TypeError');
    },
  );

  it('passes an ApiError through with its errorCode', async () => {
    server.use(
      http.get(PROCESS_DEFINITION_URL, () =>
        HttpResponse.json(
          {
            status: 404,
            detail: 'Организация не найдена.',
            errorCode: 'organizations.organization.not_found',
          },
          { status: 404, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    );

    const error = await getProcessDefinition('not-a-uuid').catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(404);
    expect((error as ApiError).errorCode).toBe('organizations.organization.not_found');
  });
});
