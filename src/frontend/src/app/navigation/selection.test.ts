import { describe, expect, it } from 'vitest';
import { menuFor } from './addressModel';
import {
  fallbackNotice,
  fullSelectionQuery,
  menuHref,
  menuKindOf,
  organizationStorageKey,
  prefixPath,
  readAddressSelection,
  readRemembered,
  readRememberedOrganization,
  resolveKnownId,
  selectionQuery,
  namesObjects,
  selectionStorageKey,
  writeRemembered,
  writeRememberedOrganization,
  type KeyValueStore,
} from './selection';
import { NO_SELECTION, type ObjectSelection } from './selectorFit';

function select(part: Partial<ObjectSelection>): ObjectSelection {
  return { ...NO_SELECTION, ...part };
}

function memoryStore(initial: Record<string, string> = {}): KeyValueStore & {
  data: Record<string, string>;
} {
  const data = { ...initial };
  return {
    data,
    getItem: (key) => data[key] ?? null,
    setItem: (key, value) => {
      data[key] = value;
    },
  };
}

describe('readAddressSelection (ADR-0032 § 1)', () => {
  it('reads the organization alone from its pages', () => {
    expect(readAddressSelection('/organizations/o/overview', '')).toMatchObject({
      organizationId: 'o',
      selection: NO_SELECTION,
    });
  });

  it('reads the most specific object from the prefix and the rest from the query', () => {
    expect(
      readAddressSelection(
        '/organizations/o/projects/a/workitems',
        '?teamId=mars&departmentId=log',
      ),
    ).toMatchObject({
      organizationId: 'o',
      selection: select({ project: 'a', team: 'mars', department: 'log' }),
    });
    expect(
      readAddressSelection('/organizations/o/teams/mars/overview', '?departmentId=log'),
    ).toMatchObject({
      organizationId: 'o',
      selection: select({ team: 'mars', department: 'log' }),
    });
    expect(readAddressSelection('/organizations/o/areas/log/projects', '')).toMatchObject({
      organizationId: 'o',
      selection: select({ department: 'log' }),
    });
  });

  it('reads the project from the query on the settings pages only', () => {
    expect(readAddressSelection('/organizations/o/overview', '?projectId=a').selection).toEqual(
      NO_SELECTION,
    );
    expect(readAddressSelection('/organizations/o/projects', '?projectId=a').selection).toEqual(
      NO_SELECTION,
    );
  });

  it('reads the project from the query on a settings page, and under no prefix', () => {
    expect(
      readAddressSelection('/organizations/o/settings/general', '?teamId=mars&projectId=a'),
    ).toMatchObject({ organizationId: 'o', selection: select({ team: 'mars', project: 'a' }) });
    expect(
      readAddressSelection('/organizations/o/teams/mars/overview', '?projectId=a'),
    ).toMatchObject({
      organizationId: 'o',
      selection: select({ team: 'mars' }),
    });
  });

  it('decodes a percent-encoded id and ignores an empty query value', () => {
    const { selection } = readAddressSelection('/organizations/o/projects/%6Canguages', '?teamId=');
    expect(selection).toEqual(select({ project: 'languages' }));
  });

  it('does not take mod-017’s «Без команды» (teamId=none) for a team, in either order (D-1)', () => {
    const path = '/organizations/o/projects/a/backlogs/stories';
    expect(readAddressSelection(path, '?teamId=none').selection).toEqual(select({ project: 'a' }));
    expect(readAddressSelection(path, '?teamId=none&teamId=mars').selection).toEqual(
      select({ project: 'a', team: 'mars' }),
    );
    expect(readAddressSelection(path, '?teamId=mars&teamId=none').selection).toEqual(
      select({ project: 'a', team: 'mars' }),
    );
    expect(readAddressSelection(path, '?teamId=&teamId=mars').selection).toEqual(
      select({ project: 'a', team: 'mars' }),
    );
  });

  it('reads an address outside an organization as no selection', () => {
    expect(readAddressSelection('/dictionaries/languages', '?teamId=mars')).toMatchObject({
      selection: NO_SELECTION,
    });
  });

  it('does not take the lists for objects', () => {
    expect(readAddressSelection('/organizations/o/areas', '').selection).toEqual(NO_SELECTION);
    expect(readAddressSelection('/organizations/o/projects', '').selection).toEqual(NO_SELECTION);
  });
});

describe('the kind an address names (C-6: the prefix is the primary object)', () => {
  it('tells the prefix kind from the query kinds', () => {
    expect(
      readAddressSelection('/organizations/o/projects/a/general', '?teamId=m').prefixKind,
    ).toBe('project');
    expect(readAddressSelection('/organizations/o/teams/m/overview', '').prefixKind).toBe('team');
    expect(readAddressSelection('/organizations/o/areas/d/overview', '').prefixKind).toBe(
      'department',
    );
    expect(readAddressSelection('/organizations/o/settings/general', '?teamId=m').prefixKind).toBe(
      null,
    );
    expect(readAddressSelection('/dictionaries/languages', '').prefixKind).toBeNull();
  });

  it('names objects only when the address carries one', () => {
    expect(namesObjects(NO_SELECTION)).toBe(false);
    expect(namesObjects(select({ team: 'm' }))).toBe(true);
  });
});

describe('building addresses', () => {
  it('names the menu kind of a selection', () => {
    expect(menuKindOf(NO_SELECTION)).toBe('organization');
    expect(menuKindOf(select({ department: 'log', team: 'mars' }))).toBe('team');
    expect(menuKindOf(select({ department: 'log', team: 'mars', project: 'a' }))).toBe('project');
  });

  it('puts the most specific object in the prefix and the others in the query', () => {
    const selection = select({ department: 'log', team: 'mars', project: 'a' });
    expect(prefixPath('o', selection)).toBe('/organizations/o/projects/a');
    expect(selectionQuery(selection)).toBe('?teamId=mars&departmentId=log');
    expect(selectionQuery(select({ department: 'log' }))).toBe('');
  });

  it('puts every chosen object in the query of an address with no prefix (settings)', () => {
    const selection = select({ department: 'log', team: 'mars', project: 'a' });
    const query = fullSelectionQuery(selection);
    expect(query).toBe('?teamId=mars&departmentId=log&projectId=a');
    expect(readAddressSelection('/organizations/o/settings/general', query).selection).toEqual(
      selection,
    );
    expect(fullSelectionQuery(NO_SELECTION)).toBe('');
  });

  it('builds a menu row address, a board landing on its first level', () => {
    const selection = select({ team: 'mars', department: 'log' });
    const boards = menuFor('team').find((row) => row.id === 'boards');
    expect(boards).toBeDefined();
    expect(menuHref('o', selection, boards ?? { segment: '' })).toBe(
      '/organizations/o/teams/mars/boards/stories?departmentId=log',
    );
  });

  it('round-trips an address through the reader', () => {
    const selection = select({ department: 'log', team: 'mars', project: 'a' });
    const row = menuFor('project')[0] ?? { segment: 'general' };
    const href = menuHref('o', selection, row);
    const [path = '', search = ''] = href.split('?');
    expect(readAddressSelection(path, `?${search}`).selection).toEqual(selection);
  });
});

describe('ids are compared as the router does (tech-009 F4)', () => {
  it('finds a known id whatever the letter case of the address', () => {
    expect(resolveKnownId('ABC-1', ['abc-1', 'x'])).toBe('abc-1');
    expect(resolveKnownId('nope', ['abc-1'])).toBeNull();
  });
});

describe('the remembered selection (ADR-0032 § 6)', () => {
  it('writes the selection per user and organization, and the last organization', () => {
    const store = memoryStore();
    writeRemembered(store, 'u1', 'o', select({ team: 'mars' }));
    expect(JSON.parse(store.data[selectionStorageKey('u1', 'o')] ?? 'null')).toEqual({
      department: null,
      team: 'mars',
      project: null,
    });
    expect(store.data[organizationStorageKey('u1')]).toBe('o');
    expect(readRemembered(store, 'u1', 'o')).toEqual(select({ team: 'mars' }));
    expect(readRememberedOrganization(store, 'u1')).toBe('o');
    expect(readRemembered(store, 'u2', 'o')).toBeNull();
  });

  it('notes the last organization alone when a visit names no object', () => {
    const store = memoryStore();
    writeRemembered(store, 'u1', 'o', select({ team: 'mars' }));
    writeRememberedOrganization(store, 'u1', 'p');
    expect(readRememberedOrganization(store, 'u1')).toBe('p');
    expect(readRemembered(store, 'u1', 'o')).toEqual(select({ team: 'mars' }));
    expect(readRemembered(store, 'u1', 'p')).toBeNull();
  });

  it.each([['not json'], ['[1]'], ['null'], ['"text"']])('treats %s as absent', (raw) => {
    const store = memoryStore({ [selectionStorageKey('u1', 'o')]: raw });
    expect(readRemembered(store, 'u1', 'o')).toBeNull();
  });

  it('keeps only string ids of the three kinds', () => {
    const store = memoryStore({
      [selectionStorageKey('u1', 'o')]: JSON.stringify({ team: 5, project: 'a', other: 'x' }),
    });
    expect(readRemembered(store, 'u1', 'o')).toEqual(select({ project: 'a' }));
  });

  it('survives a store that throws', () => {
    const broken: KeyValueStore = {
      getItem: () => {
        throw new Error('blocked');
      },
      setItem: () => {
        throw new Error('full');
      },
    };
    expect(readRemembered(broken, 'u', 'o')).toBeNull();
    expect(readRememberedOrganization(broken, 'u')).toBeNull();
    expect(() => writeRemembered(broken, 'u', 'o', NO_SELECTION)).not.toThrow();
  });
});

describe('the fallback notice', () => {
  it('words the notice with the name, or without it when nothing is left', () => {
    expect(fallbackNotice(['Ромашка'])).toBe(
      'Ранее выбранный контекст недоступен. Выбрано: Ромашка.',
    );
    expect(fallbackNotice(['Ромашка', 'MARS'])).toBe(
      'Ранее выбранный контекст недоступен. Выбрано: Ромашка / MARS.',
    );
    expect(fallbackNotice([])).toBe('Ранее выбранный контекст недоступен.');
  });
});
