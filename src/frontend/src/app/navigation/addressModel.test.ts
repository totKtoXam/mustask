import { matchPath } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import { boardsLandingPath } from '../../features/boards';
import {
  LANDING_LEVEL_SEGMENT,
  MENU_TABLE,
  OBJECT_SETTINGS_SEGMENT,
  RETIRED_ADDRESSES,
  isRetiredAddress,
  menuFor,
  rowRoute,
  rowTakesLevel,
  type MenuKind,
} from './addressModel';

const KINDS = Object.keys(MENU_TABLE) as MenuKind[];

/** ADR-0032 § 2 as written, for the lines that exist today (feat-048 added «Рабочие элементы», «Канбан» and «Бэклог» above the project). */
const EXPECTED: Record<MenuKind, [label: string, segment: string][]> = {
  organization: [
    ['Обзор', 'overview'],
    ['Структура', 'areas'],
    ['Проекты', 'projects'],
    ['Команды', 'teams'],
    ['Рабочие элементы', 'workitems'],
    ['Канбан', 'boards/:level'],
    ['Бэклог', 'backlogs/:level'],
    ['Участники', 'members'],
  ],
  department: [
    ['Обзор', 'overview'],
    ['Проекты', 'projects'],
    ['Команды', 'teams'],
    ['Рабочие элементы', 'workitems'],
    ['Канбан', 'boards/:level'],
    ['Бэклог', 'backlogs/:level'],
    ['Участники', 'members'],
  ],
  team: [
    ['Обзор', 'overview'],
    ['Канбан', 'boards/:level'],
    ['Бэклог', 'backlogs/:level'],
    ['Рабочие элементы', 'workitems'],
    ['Спринты', 'sprints'],
    ['Проекты', 'projects'],
    ['Участники и доступ', 'members'],
  ],
  project: [
    ['Общие', 'general'],
    ['Структура', 'areas/:areaId/overview'],
    ['Рабочие элементы', 'workitems'],
    ['Канбан', 'boards/:level'],
    ['Бэклог', 'backlogs/:level'],
    ['Команды', 'teams'],
    ['Участники и доступ', 'access'],
  ],
};

describe('the menu table (ADR-0032 § 2)', () => {
  it.each(KINDS)('%s: entries in order, with their segments', (kind) => {
    expect(menuFor(kind).map((row) => [row.label, row.segment])).toEqual(EXPECTED[kind]);
  });

  it.each(KINDS)('%s: ids and segments are unique, every entry names a read code', (kind) => {
    const rows = menuFor(kind);
    expect(new Set(rows.map((row) => row.id)).size).toBe(rows.length);
    expect(new Set(rows.map((row) => row.segment)).size).toBe(rows.length);
    for (const row of rows) expect(row.code).toMatch(/^[a-z]+\.[a-z]+\.read$/i);
  });

  it('holds no «Настройки» row: the shell derives it, last (BR-NV-18)', () => {
    for (const kind of KINDS) {
      for (const row of menuFor(kind)) {
        expect(row.label).not.toBe('Настройки');
        expect(row.segment).not.toBe(OBJECT_SETTINGS_SEGMENT);
      }
    }
  });

  it("offers feat-048's entries above the project (OQ-9)", () => {
    const labels = (kind: MenuKind) => menuFor(kind).map((row) => row.label);
    for (const kind of ['organization', 'department', 'team'] as const) {
      expect(labels(kind)).toContain('Рабочие элементы');
    }
    for (const kind of ['organization', 'department'] as const) {
      expect(labels(kind)).toContain('Канбан');
      expect(labels(kind)).toContain('Бэклог');
    }
  });

  it("builds each route under the object's own prefix", () => {
    expect(routeOf('team', 'boards')).toBe(
      '/organizations/:organizationId/teams/:teamId/boards/:level',
    );
    expect(routeOf('project', 'workitems')).toBe(
      '/organizations/:organizationId/projects/:projectId/workitems',
    );
    expect(routeOf('department', 'overview')).toBe(
      '/organizations/:organizationId/areas/:areaId/overview',
    );
  });

  it('puts «Структура» on the area prefix, whatever the menu (ADR-0031 rule 5)', () => {
    const structure = menuFor('project').find((row) => row.id === 'structure');
    expect(structure && rowRoute('project', structure)).toBe(
      '/organizations/:organizationId/areas/:areaId/overview',
    );
  });

  it('takes a level exactly for boards and backlogs', () => {
    for (const kind of KINDS) {
      for (const row of menuFor(kind)) {
        expect(rowTakesLevel(row)).toBe(row.id === 'boards' || row.id === 'backlogs');
      }
    }
  });

  it('lands on the level boards land on', () => {
    expect(boardsLandingPath('o', 't')).toBe(
      `/organizations/o/teams/t/boards/${LANDING_LEVEL_SEGMENT}`,
    );
  });
});

describe('the retired addresses (BR-NV-6, ADR-0032 § 4)', () => {
  it('names each retired kind once', () => {
    expect(new Set(RETIRED_ADDRESSES.map((retired) => retired.pattern)).size).toBe(
      RETIRED_ADDRESSES.length,
    );
    expect(new Set(RETIRED_ADDRESSES.map((retired) => retired.was)).size).toBe(
      RETIRED_ADDRESSES.length,
    );
  });

  it.each(RETIRED_ADDRESSES.map((retired) => [retired.was, retired.pattern] as const))(
    '%s is retired, in any letter case, with any id',
    (_was, pattern) => {
      const address = pattern.replace(/:\w+/g, 'x1');
      expect(isRetiredAddress(address)).toBe(true);
      expect(isRetiredAddress(address.toUpperCase())).toBe(true);
    },
  );

  it('matches no route of the menu: a retired shape is never a section', () => {
    for (const kind of KINDS) {
      for (const row of menuFor(kind)) {
        const route = rowRoute(kind, row);
        const address = route.replace(/:\w+/g, 'x1');
        expect(isRetiredAddress(address)).toBe(false);
        for (const retired of RETIRED_ADDRESSES) {
          expect(matchPath({ path: retired.pattern, end: true }, address)).toBeNull();
        }
      }
    }
  });

  it("leaves the new «Параметры» and the organization's own settings alone", () => {
    for (const address of [
      '/organizations/o/projects/p/settings',
      '/organizations/o/teams/t/settings',
      '/organizations/o/areas/a/settings',
      '/organizations/o/settings/general',
      '/organizations/o/settings/process',
      '/dictionaries/languages',
    ]) {
      expect(isRetiredAddress(address)).toBe(false);
    }
  });
});

function routeOf(kind: MenuKind, id: string): string {
  const row = menuFor(kind).find((candidate) => candidate.id === id);
  if (!row) throw new Error(`no ${id} row for ${kind}`);
  return rowRoute(kind, row);
}
