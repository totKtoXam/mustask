import { describe, expect, it } from 'vitest';
import {
  NO_SELECTION,
  applyChoice,
  offerFor,
  openObject,
  pairFits,
  primaryKind,
  resetNotice,
  resetUnfitted,
  type FitData,
  type ObjectSelection,
  type SelectorKind,
} from './selectorFit';

// The cast of US-8: «Логистика» holds A and B and is linked to MARS and VENUS; «Финансы» holds C;
// D is at the top level; MARS is linked to A, B, C; VENUS to A; JUPITER to none. Olga belongs to
// VENUS. Department links are direct for «Логистика»; MARS reaches «Финансы» through C only.
const DATA: FitData = {
  departments: [
    { id: 'fin', name: 'Финансы' },
    { id: 'log', name: 'Логистика' },
  ],
  projects: [
    { id: 'd', name: 'D', key: 'D', departmentId: null, teamIds: [] },
    { id: 'c', name: 'C', key: 'C', departmentId: 'fin', teamIds: ['mars'] },
    { id: 'b', name: 'B', key: 'B', departmentId: 'log', teamIds: ['mars'] },
    { id: 'a', name: 'A', key: 'A', departmentId: 'log', teamIds: ['mars', 'venus'] },
  ],
  teams: [
    { id: 'jupiter', name: 'JUPITER', isMember: false, departmentIds: [] },
    { id: 'mars', name: 'MARS', isMember: false, departmentIds: ['log'] },
    { id: 'venus', name: 'VENUS', isMember: true, departmentIds: ['log'] },
  ],
};

function select(partial: Partial<ObjectSelection>): ObjectSelection {
  return { ...NO_SELECTION, ...partial };
}

function names(options: readonly { id: string }[]): string[] {
  return options.map((option) => option.id);
}

describe('pairFits', () => {
  const cases: [string, SelectorKind, string, SelectorKind, string, boolean][] = [
    ['project in its department', 'project', 'a', 'department', 'log', true],
    ['project in another department', 'project', 'a', 'department', 'fin', false],
    ['top-level project fits no department', 'project', 'd', 'department', 'log', false],
    ['team linked to the project', 'team', 'venus', 'project', 'a', true],
    ['team not linked to the project', 'team', 'venus', 'project', 'b', false],
    ['team linked to the department directly', 'team', 'venus', 'department', 'log', true],
    ['team linked through a placed project', 'team', 'mars', 'department', 'fin', true],
    ['team with no link at all', 'team', 'jupiter', 'department', 'log', false],
    ['department of the team, reversed', 'department', 'fin', 'team', 'mars', true],
    ['department of the project, reversed', 'department', 'log', 'project', 'c', false],
  ];
  it.each(cases)('%s', (_name, a, idA, b, idB, expected) => {
    expect(pairFits(DATA, a, idA, b, idB)).toBe(expected);
  });
});

describe('offerFor', () => {
  const cases: [string, Partial<ObjectSelection>, SelectorKind, string[], string[]][] = [
    ['nothing chosen: everything fits, by name', {}, 'project', ['a', 'b', 'c', 'd'], []],
    [
      'project selector under a department',
      { department: 'log' },
      'project',
      ['a', 'b'],
      ['c', 'd'],
    ],
    [
      'project selector under a department and a team',
      { department: 'log', team: 'venus' },
      'project',
      ['a'],
      ['b', 'c', 'd'],
    ],
    [
      'team selector under a project: own team first among the fitting, by name',
      { project: 'a' },
      'team',
      ['venus', 'mars'],
      ['jupiter'],
    ],
    [
      'team selector under a department reached through a project',
      { department: 'fin' },
      'team',
      ['mars'],
      ['venus', 'jupiter'],
    ],
    ['department selector under a project', { project: 'a' }, 'department', ['log'], ['fin']],
    [
      'the selector ignores its own chosen value',
      { project: 'a', team: 'mars' },
      'team',
      ['venus', 'mars'],
      ['jupiter'],
    ],
    [
      'a top-level project leaves every department among the others',
      { project: 'd' },
      'department',
      [],
      ['log', 'fin'],
    ],
  ];
  it.each(cases)('%s', (_name, selection, kind, fitting, others) => {
    const offer = offerFor(DATA, select(selection), kind);
    expect(names(offer.fitting)).toEqual(fitting);
    expect(names(offer.others)).toEqual(others);
  });

  it('puts the user own teams first in «Остальные» too', () => {
    const offer = offerFor(DATA, select({ project: 'b' }), 'team');
    expect(names(offer.fitting)).toEqual(['mars']);
    expect(names(offer.others)).toEqual(['venus', 'jupiter']);
  });

  it('offers only the three kinds of object the data holds', () => {
    const everything = offerFor(DATA, NO_SELECTION, 'project');
    expect(everything.fitting.map((option) => option.key)).toEqual(['A', 'B', 'C', 'D']);
  });
});

describe('applyChoice', () => {
  it('a fitting choice resets nothing', () => {
    const result = applyChoice(DATA, select({ department: 'log' }), 'project', 'a');
    expect(result.selection).toEqual(select({ department: 'log', project: 'a' }));
    expect(result.reset).toEqual([]);
    expect(resetNotice(result.reset)).toBeNull();
  });

  it('«все» resets nothing', () => {
    const result = applyChoice(DATA, select({ department: 'log', project: 'a' }), 'project', null);
    expect(result.selection).toEqual(select({ department: 'log' }));
    expect(result.reset).toEqual([]);
  });

  it('a choice from «Остальные» resets the project that no longer fits, the team stays', () => {
    const result = applyChoice(
      DATA,
      select({ department: 'log', team: 'mars', project: 'a' }),
      'department',
      'fin',
    );
    expect(result.selection).toEqual(select({ department: 'fin', team: 'mars' }));
    expect(resetNotice(result.reset)).toBe('Выбор сброшен: проект A.');
  });

  it('names two resets in the order department, team, project', () => {
    const result = applyChoice(DATA, select({ team: 'mars', project: 'a' }), 'department', 'fin');
    expect(result.selection.project).toBeNull();
    expect(result.selection.team).toBe('mars');

    const both = applyChoice(DATA, select({ team: 'venus', project: 'a' }), 'department', 'fin');
    expect(both.selection).toEqual(select({ department: 'fin' }));
    expect(resetNotice(both.reset)).toBe('Выбор сброшен: команда VENUS, проект A.');
  });

  it('choosing a team from «Остальные» resets the project it is not linked to', () => {
    const result = applyChoice(DATA, select({ project: 'a' }), 'team', 'jupiter');
    expect(result.selection).toEqual(select({ team: 'jupiter' }));
    expect(resetNotice(result.reset)).toBe('Выбор сброшен: проект A.');
  });

  it('a team that fits both chosen values keeps both', () => {
    // MARS fits «Финансы» through C even though the chosen project A is not in «Финансы».
    const result = applyChoice(DATA, select({ department: 'log', project: 'a' }), 'team', 'mars');
    expect(result.reset).toEqual([]);
  });
});

describe('openObject (D-3)', () => {
  it("from A's «Команды» opening MARS leaves MARS and the department", () => {
    const next = openObject(
      DATA,
      select({ department: 'log', project: 'a' }),
      'team',
      'mars',
    ).selection;
    expect(next).toEqual(select({ department: 'log', team: 'mars' }));
  });

  it("from a department's «Проекты» opening A keeps the department and A", () => {
    const next = openObject(DATA, select({ department: 'log' }), 'project', 'a').selection;
    expect(next).toEqual(select({ department: 'log', project: 'a' }));
  });

  it("from a team's «Проекты» opening A leaves MARS and A", () => {
    const next = openObject(DATA, select({ team: 'mars' }), 'project', 'a').selection;
    expect(next).toEqual(select({ team: 'mars', project: 'a' }));
  });

  it('a less specific value that does not fit resets with the notice', () => {
    const result = openObject(DATA, select({ department: 'fin' }), 'project', 'a');
    expect(result.selection).toEqual(select({ project: 'a' }));
    expect(resetNotice(result.reset)).toBe('Выбор сброшен: департамент Финансы.');
  });

  it('the kinds more specific than the opened one reset without a notice', () => {
    const result = openObject(DATA, select({ team: 'mars', project: 'a' }), 'department', 'log');
    expect(result.reset).toEqual([]);
  });

  it('opening a department drops the team and the project', () => {
    const next = openObject(
      DATA,
      select({ team: 'mars', project: 'a' }),
      'department',
      'log',
    ).selection;
    expect(next).toEqual(select({ department: 'log' }));
  });
});

describe('primaryKind', () => {
  const cases: [Partial<ObjectSelection>, SelectorKind | null][] = [
    [{}, null],
    [{ department: 'log' }, 'department'],
    [{ department: 'log', team: 'mars' }, 'team'],
    [{ department: 'log', team: 'mars', project: 'a' }, 'project'],
    [{ project: 'a' }, 'project'],
  ];
  it.each(cases)('%j', (selection, expected) => {
    expect(primaryKind(select(selection))).toBe(expected);
  });
});

describe('at volume (BR-NV-9)', () => {
  it('offers 200 projects and 200 teams within the budget', () => {
    const big: FitData = {
      departments: Array.from({ length: 50 }, (_, i) => ({ id: `d${i}`, name: `Dep ${i}` })),
      projects: Array.from({ length: 200 }, (_, i) => ({
        id: `p${i}`,
        name: `Project ${i}`,
        key: `P${i}`,
        departmentId: `d${i % 50}`,
        teamIds: [`t${i % 200}`],
      })),
      teams: Array.from({ length: 200 }, (_, i) => ({
        id: `t${i}`,
        name: `Team ${i}`,
        isMember: i % 10 === 0,
        departmentIds: [`d${i % 50}`],
      })),
    };
    const started = performance.now();
    const offer = offerFor(big, select({ department: 'd1', team: 't1' }), 'project');
    expect(offer.fitting.length + offer.others.length).toBe(200);
    expect(performance.now() - started).toBeLessThan(2000);
  });
});

describe('resetUnfitted (BR-NV-17, ADR-0032 § 5, US-7: a link change)', () => {
  const unlinked: FitData = {
    ...DATA,
    projects: DATA.projects.map((project) =>
      project.id === 'a' ? { ...project, teamIds: ['venus'] } : project,
    ),
  };

  it('resets the project when its team is unlinked from it, and says so', () => {
    const result = resetUnfitted(DATA, unlinked, select({ team: 'mars', project: 'a' }));
    expect(result.selection).toEqual(select({ team: 'mars' }));
    expect(resetNotice(result.reset)).toBe('Выбор сброшен: проект A.');
  });

  it('resets the team, the more specific of a department and a team, when the link is removed', () => {
    const after: FitData = {
      ...DATA,
      teams: DATA.teams.map((team) =>
        team.id === 'venus' ? { ...team, departmentIds: [] } : team,
      ),
      projects: DATA.projects.map((project) =>
        project.id === 'a' ? { ...project, teamIds: ['mars'] } : project,
      ),
    };
    const result = resetUnfitted(DATA, after, select({ department: 'log', team: 'venus' }));
    expect(result.selection).toEqual(select({ department: 'log' }));
    expect(resetNotice(result.reset)).toBe('Выбор сброшен: команда VENUS.');
  });

  it('resets nothing when the pair fitted before and fits still, or did not fit before', () => {
    const same = resetUnfitted(DATA, DATA, select({ team: 'mars', project: 'a' }));
    expect(same.reset).toEqual([]);
    const never = resetUnfitted(DATA, unlinked, select({ team: 'jupiter', project: 'a' }));
    expect(never.reset).toEqual([]);
    expect(never.selection).toEqual(select({ team: 'jupiter', project: 'a' }));
  });

  it('leaves a value the new offers no longer hold to the fallback, not to a link change', () => {
    const without: FitData = { ...DATA, teams: DATA.teams.filter((team) => team.id !== 'mars') };
    const result = resetUnfitted(DATA, without, select({ team: 'mars', project: 'a' }));
    expect(result.reset).toEqual([]);
  });
});
