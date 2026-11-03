import { describe, expect, it } from 'vitest';
import { diagramAreas, diagramOffers } from './diagramFixture';
import {
  buildModel,
  cardName,
  emphasisOf,
  isChoosable,
  relatedFilter,
  searchMatches,
  summaryText,
} from './diagramModel';

const model = buildModel(diagramAreas, diagramOffers);

describe('feat-049 diagram model (design §§ 4.1, 4.7)', () => {
  it('stands a team under every department it is linked to, projects first', () => {
    expect(model.childrenOf.get('d1')!.map((n) => n.id)).toEqual([
      'nA',
      'team:JUPITER@d1',
      'team:MARS@d1',
      'team:VENUS@d1',
    ]);
    expect(model.childrenOf.get('d2')!.map((n) => n.id)).toEqual([
      'nB',
      'nC',
      'team:JUPITER@d2',
      'team:SATURN@d2',
    ]);
  });

  it('puts top-level projects and teams with no department in the outside section', () => {
    expect(model.roots.outside.map((n) => n.id)).toEqual(['nF', 'team:ATLAS@-']);
    expect(model.roots.departments.map((n) => n.id)).toEqual(['d1', 'd2']);
  });

  it('ignores a link to a department that is not in the tree', () => {
    expect(model.departmentsOfTeam.get('VENUS')).toEqual(['d1']);
  });

  it('counts each team once and the lines as pairs plus copy joins', () => {
    expect(model.counts).toEqual({ departments: 2, projects: 4, areas: 2, teams: 5 });
    expect(model.pairs).toHaveLength(5);
    expect(model.lineTotal).toBe(6);
  });

  it('draws no pairs for a deactivated project and takes keys from the offers', () => {
    expect(model.pairs.some((p) => p.project === 'nC')).toBe(false);
    expect(model.nodes.get('nA')!.key).toBe('WH');
    expect(model.nodes.get('nC')!.key).toBeNull();
  });

  it('builds the hierarchy without teams when the offers are missing', () => {
    const bare = buildModel(diagramAreas, null);
    expect(bare.hasOffers).toBe(false);
    expect(bare.copies.size).toBe(0);
    expect(bare.counts.teams).toBeNull();
    expect(bare.nodes.get('nA')!.key).toBeNull();
  });

  it('takes the deepest level of the whole tree for the chips', () => {
    expect(model.maxLevel).toBe(4);
  });

  it('chooses departments, active projects and teams only', () => {
    expect(isChoosable(model, 'd1')).toBe(true);
    expect(isChoosable(model, 'nA')).toBe(true);
    expect(isChoosable(model, 'MARS')).toBe(true);
    expect(isChoosable(model, 'a1')).toBe(false);
    expect(isChoosable(model, 'nC')).toBe(false);
  });

  it('emphasises a department with its whole branch and the far ends of relations', () => {
    const emphasis = emphasisOf(model, 'd1');
    expect([...emphasis.ids].sort()).toEqual(
      ['JUPITER', 'MARS', 'VENUS', 'a1', 'a2', 'd1', 'nA', 'nB', 'nF'].sort(),
    );
    expect(emphasis.lines.has('nA>MARS')).toBe(true);
    expect(emphasis.lines.has('nB>MARS')).toBe(true);
    expect(emphasis.lines.has('copy:JUPITER')).toBe(true);
  });

  it('emphasises a team with all its copies, projects and departments', () => {
    const emphasis = emphasisOf(model, 'JUPITER');
    expect([...emphasis.ids].sort()).toEqual(['JUPITER', 'd1', 'd2', 'nF']);
  });

  it('summarises a team, a project and a department', () => {
    expect(summaryText(model, 'ATLAS')).toBe('Проекты команды: нет. Департаменты команды: нет');
    expect(summaryText(model, 'nA')).toBe('Ветка проекта: областей 2. Команды проекта: MARS, VENUS');
    expect(summaryText(model, 'd2')).toMatch(
      /^Ветка департамента: проектов 2, областей 0, команд 2\. Связано вне ветки: /,
    );
  });

  it('keeps the branch, the related, their ancestors and related teams departments', () => {
    const filter = relatedFilter(model, emphasisOf(model, 'nF'));
    expect([...filter.keep].sort()).toEqual(['d1', 'd2', 'nF']);
    expect([...filter.teams]).toEqual(['JUPITER']);
  });

  it('searches names only, ignoring case and ё/е', () => {
    expect(searchMatches(model, 'прием')).toEqual(['a1']);
    expect(searchMatches(model, 'mars')).toEqual(['MARS']);
    expect(searchMatches(model, 'nA')).toEqual([]);
    expect(searchMatches(model, '  ')).toEqual([]);
  });

  it('names a card by type, parent and relation count', () => {
    expect(cardName(model, model.nodes.get('nA')!)).toBe(
      'Проект Склад (WH), в департаменте Логистика, команд: 2',
    );
    expect(cardName(model, model.nodes.get('team:JUPITER@d1')!)).toBe(
      'Команда JUPITER, проектов: 1, в департаменте Логистика, ещё в: Финансы',
    );
    expect(cardName(model, model.nodes.get('team:ATLAS@-')!)).toBe(
      'Команда ATLAS, проектов: 0, без департамента',
    );
    expect(cardName(model, model.nodes.get('nC')!)).toBe(
      'Проект Архив, деактивирован, в департаменте Финансы',
    );
    expect(cardName(model, model.nodes.get('a1')!)).toBe('Область Приёмка, в «Склад»');
  });
});
