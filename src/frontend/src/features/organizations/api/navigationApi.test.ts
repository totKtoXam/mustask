import { describe, expect, it } from 'vitest';
import { HttpResponse, http } from 'msw';
import { ApiError } from '../../../shared/api/httpClient';
import { server } from '../../../test/mocks/server';
import {
  OVERVIEW_URL,
  SELECTOR_OFFERS_URL,
  overviewFixture,
  selectorOffersFixture,
} from '../testFixtures';
import { getOverview, getSelectorOffers } from './navigationApi';

describe('feat-043 api-contract §§ 1-2: the two reads', () => {
  it('reads the overview and the offers from their routes, as received', async () => {
    const paths: string[] = [];
    server.use(
      http.get(OVERVIEW_URL, ({ request }) => {
        paths.push(new URL(request.url).pathname);
        return HttpResponse.json(overviewFixture);
      }),
      http.get(SELECTOR_OFFERS_URL, ({ request }) => {
        paths.push(new URL(request.url).pathname);
        return HttpResponse.json(selectorOffersFixture);
      }),
    );

    expect(await getOverview('o 1')).toEqual(overviewFixture);
    expect(await getSelectorOffers('o')).toEqual(selectorOffersFixture);
    expect(paths).toEqual([
      '/api/organizations/o%201/overview',
      '/api/organizations/o/selector-offers',
    ]);
  });

  it('answers the 404 of a non-member as an ApiError', async () => {
    server.use(
      http.get(SELECTOR_OFFERS_URL, () =>
        HttpResponse.json(
          { code: 'organizations.organization.not_found', message: 'Организация не найдена.' },
          { status: 404 },
        ),
      ),
    );
    await expect(getSelectorOffers('o')).rejects.toBeInstanceOf(ApiError);
  });
});
