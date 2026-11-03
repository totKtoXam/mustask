import { Children } from 'react';
import { createRoutesFromChildren, matchRoutes, type RouteObject } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import { ORGANIZATIONS_ROUTES } from '../features/organizations/paths';
import {
  KIND_ROUTE_PREFIX,
  MENU_TABLE,
  RETIRED_ADDRESSES,
  rowRoute,
  type MenuKind,
} from './navigation/addressModel';
import { routeTree } from './routeTree';

// feat-043 step 8: the route table (`routeTree.tsx`) against the address model. Every menu entry
// has a route, every route of the slice is in the table, and no retired address has a route of
// its own — such an address ends in the wildcard that answers «Страница не найдена».

const routes: RouteObject[] = createRoutesFromChildren(Children.toArray(routeTree));

function patterns(list: readonly RouteObject[], base = ''): string[] {
  return list.flatMap((route) => {
    const own = route.path ? `${base.replace(/\/$/, '')}/${route.path.replace(/^\//, '')}` : base;
    const here = route.path && !route.index ? [own] : [];
    return [
      ...here,
      ...patterns(route.children ?? [], route.path?.startsWith('/') ? route.path : own),
    ];
  });
}

/** A concrete address for a route pattern. */
function fill(pattern: string): string {
  return pattern
    .replace(':level', 'stories')
    .replace(/:[A-Za-z]+/g, 'x1')
    .replace(/\/\*$/, '/zzz');
}

function leafOf(pathname: string): string | undefined {
  const matches = matchRoutes(routes, pathname);
  return matches?.at(-1)?.route.path;
}

describe('every address of the model has a route', () => {
  const kinds = Object.keys(MENU_TABLE) as MenuKind[];
  const cases = kinds.flatMap((kind) =>
    MENU_TABLE[kind].map((row) => ({ kind, label: row.label, pattern: rowRoute(kind, row) })),
  );

  it.each(cases)('$kind: «$label» ($pattern)', ({ pattern }) => {
    const leaf = leafOf(fill(pattern));
    expect(leaf).toBeDefined();
    expect(leaf).not.toBe('*');
    expect(leaf).not.toBe('/organizations/:organizationId/*');
  });

  it.each(kinds)('the %s prefix opens an entry', (kind) => {
    const leaf = leafOf(fill(KIND_ROUTE_PREFIX[kind]));
    expect(leaf).toBeDefined();
    expect(leaf).not.toMatch(/\*/);
  });

  it.each(ORGANIZATIONS_ROUTES.map((route) => [route]))(
    'the slice route %s is in the table',
    (route) => {
      const leaf = leafOf(fill(route));
      expect(leaf).toBeDefined();
      expect(leaf).not.toMatch(/\*/);
    },
  );
});

describe('no retired address has a route (BR-NV-6)', () => {
  it.each(RETIRED_ADDRESSES.map((retired) => [retired.was, retired.pattern]))(
    '%s: %s ends in the wildcard',
    (_was, pattern) => {
      const leaf = leafOf(fill(pattern));
      expect(leaf).toMatch(/\*$/);
    },
  );

  it('registers no pattern of the retired shapes', () => {
    const registered = patterns(routes);
    for (const retired of RETIRED_ADDRESSES) {
      expect(registered).not.toContain(retired.pattern);
    }
  });
});
