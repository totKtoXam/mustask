import { describe, expect, it } from 'vitest';
import { addressContext } from './addressContext';

describe('mod-002 US-8 Scenario: The address sets the switcher — what an address carries', () => {
  it.each([
    ['/home', {}],
    ['/dictionaries/languages', {}],
    ['/organizations', {}],
    ['/organizations/', {}],
    ['/organizations/deactivated', {}],
    ['/organizations/o-1', {}],
    ['/organizations/o-1/settings/general', { organizationId: 'o-1' }],
    ['/organizations/o-1/settings/projects/deactivated', { organizationId: 'o-1' }],
    ['/organizations/o-1/settings/process', { organizationId: 'o-1' }],
    ['/organizations/o-1/projects/p-1', { organizationId: 'o-1' }],
    ['/organizations/o-1/projects/p-1/settings/areas', { organizationId: 'o-1', projectId: 'p-1' }],
    ['/organizations/o-1/projects/p-1/settings/teams', { organizationId: 'o-1', projectId: 'p-1' }],
    [
      '/organizations/o-1/projects/p-1/settings/teams/t-1',
      { organizationId: 'o-1', projectId: 'p-1' },
    ],
    [
      '/organizations/o-1/projects/p-1/teams/t-1/board',
      { organizationId: 'o-1', projectId: 'p-1', teamId: 't-1' },
    ],
  ])('%s carries %j', (pathname, expected) => {
    expect(addressContext(pathname)).toEqual(expected);
  });

  it('returns ids decoded, as the lists hold them, and keeps a malformed escape as sent', () => {
    expect(addressContext('/organizations/a%2Fb/projects/p%20q/settings/general')).toEqual({
      organizationId: 'a/b',
      projectId: 'p q',
    });
    expect(addressContext('/organizations/%E0%A4%A/settings/general')).toEqual({
      organizationId: '%E0%A4%A',
    });
  });
});
