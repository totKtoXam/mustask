import { describe, expect, it } from 'vitest';
import { ids } from '../testFixtures';
import { listOriginOf, listOriginState } from './listOrigin';

// feat-003 BR-WS-5, design § 6, § 11: a card's page knows it was opened from a row of this
// project's list by the mark on the row's link.

const ORG = ids.example;
const MSK = ids.msk;
const LIST = `/organizations/${ORG}/projects/${MSK}/workitems`;

describe('feat-003 BR-WS-5 the mark of a row’s link', () => {
  it('carries the list’s address, filters included, and gives it back', () => {
    const state = listOriginState(LIST, '?state=%D0%9D%D0%BE%D0%B2%D0%B0%D1%8F&teamId=none');

    expect(listOriginOf(state, ORG, MSK)).toBe(
      `${LIST}?state=%D0%9D%D0%BE%D0%B2%D0%B0%D1%8F&teamId=none`,
    );
  });

  it('gives back an unfiltered list’s address as it is', () => {
    expect(listOriginOf(listOriginState(LIST, ''), ORG, MSK)).toBe(LIST);
  });

  it.each([
    ['no state', null],
    ['a state of no object', 'text'],
    ['a state without the mark', { cardDepth: 1 }],
    ['a mark that is no text', { workItemsListOrigin: 7 }],
    [
      'a mark of another project’s list',
      listOriginState(`/organizations/${ORG}/projects/${ids.ops}/workitems`, ''),
    ],
    ['a mark of another page', listOriginState(`${LIST}/MSK-BUG-4`, '')],
    ['a mark that only starts like the list', listOriginState(`${LIST}x`, '?a=b')],
  ])('is no mark: %s', (_name, state) => {
    expect(listOriginOf(state, ORG, MSK)).toBeNull();
  });
});
