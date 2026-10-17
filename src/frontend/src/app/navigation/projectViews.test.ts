import { describe, expect, it } from 'vitest';
import HomeOutlinedIcon from '@mui/icons-material/HomeOutlined';
import type { AccessCheck } from '../../shared/lib/access';
import type { WorkContextSelection } from '../../shared/lib/workContext';
import { navigationModel } from './model';
import { switchTarget } from './switchTarget';
import type { NavigationEntry, NavigationModel } from './types';
import { deriveTrail, entryMarker, isEntryVisible, visibleModel } from './visibility';

// feat-039 BR-PB-9: a project's own backlog and board are project-level pages of the
// existing «Бэклоги» and «Канбан» entries — current there like the entry's team pages,
// and kept, with their level, by a switch of project or organization. The model is the
// real one; the resolver and the marker are the shell's.

const entry = (id: string) =>
  navigationModel.work.find((candidate) => candidate.id === id) as NavigationEntry;

/** «Альфа» (o1) with «Пример проекта» (p1, team t1, last chosen t7) and «Аналитика» (p2); «Бета» (o2) with p3. */
const alfaPrm = { organizationId: 'o1', projectId: 'p1', teamId: 't7' };
const alfaAnl = { organizationId: 'o1', projectId: 'p2', teamId: 't2' };
const betaRep = { organizationId: 'o2', projectId: 'p3', teamId: 't3' };
const betaNone = { organizationId: 'o2' };
/** What the address of a project page carries: the organization and the project, no team. */
const carried = { organizationId: 'o1', projectId: 'p1' };

const model: NavigationModel = {
  top: [
    {
      id: 'home',
      label: 'Главная',
      icon: HomeOutlinedIcon,
      scope: 'instance',
      landing: () => '/home',
    },
  ],
  work: navigationModel.work.filter((candidate) => ['boards', 'backlogs'].includes(candidate.id)),
  settings: [],
};
const allowEverything: AccessCheck = () => true;

function from(
  pathname: string,
  next: WorkContextSelection,
  access: AccessCheck = allowEverything,
  isCodesRead?: (organizationId: string | undefined) => boolean,
) {
  return switchTarget(model, access, { pathname, carried, next }, isCodesRead);
}

describe('feat-039 BR-PB-9 (a): a project’s page is the entry’s page', () => {
  it.each([
    ['backlogs', 'backlogs', 'boards'],
    ['boards', 'boards', 'backlogs'],
  ])(
    '«%s» marks `page` on the project’s %s at every level, leaves no trail, and the other entry is not marked',
    (surface, id, other) => {
      const visible = visibleModel(navigationModel, allowEverything, {
        isReal: true,
        selection: alfaPrm,
      });
      for (const level of ['epics', 'features', 'stories']) {
        const address = `/organizations/o1/projects/p1/${surface}/${level}`;
        expect(entryMarker(address, entry(id), alfaPrm)).toBe('page');
        expect(entryMarker(address, entry(other), alfaPrm)).toBeNull();
        expect(deriveTrail(address, visible, alfaPrm)).toEqual([]);
      }
    },
  );

  it('keeps the entry’s landing: pressing it opens the switcher’s team’s «Истории» page', () => {
    expect(entry('backlogs').landing(alfaPrm)).toBe(
      '/organizations/o1/projects/p1/teams/t7/backlogs/stories',
    );
    expect(entry('boards').landing(alfaPrm)).toBe(
      '/organizations/o1/projects/p1/teams/t7/boards/stories',
    );
  });

  it('marks a trailing slash as the same page', () => {
    expect(
      entryMarker('/organizations/o1/projects/p1/backlogs/epics/', entry('backlogs'), alfaPrm),
    ).toBe('page');
  });

  it.each([
    ['a level the page does not serve', '/organizations/o1/projects/p1/backlogs/tasks'],
    ['no level', '/organizations/o1/projects/p1/backlogs'],
    ['a level in another case', '/organizations/o1/projects/p1/backlogs/Epics'],
    ['a page beneath a level', '/organizations/o1/projects/p1/backlogs/epics/more'],
  ])('marks nothing on %s', (_case, address) => {
    expect(entryMarker(address, entry('backlogs'), alfaPrm)).toBeNull();
  });

  it('marks the page of the selection’s project only', () => {
    expect(
      entryMarker('/organizations/o1/projects/p2/backlogs/epics', entry('backlogs'), alfaPrm),
    ).toBeNull();
    expect(
      entryMarker('/organizations/o9/projects/p1/backlogs/epics', entry('backlogs'), alfaPrm),
    ).toBeNull();
  });

  it('marks nothing while a not-found frame answers the address', () => {
    expect(entryMarker(null, entry('backlogs'), alfaPrm)).toBeNull();
  });

  it('is hidden with no team at the switcher: the entries are not shown and none is marked', () => {
    const noTeam = { organizationId: 'o1', projectId: 'p1' };
    for (const id of ['boards', 'backlogs']) {
      expect(isEntryVisible(entry(id), allowEverything, { isReal: true, selection: noTeam })).toBe(
        false,
      );
      expect(
        entryMarker('/organizations/o1/projects/p1/backlogs/stories', entry(id), noTeam),
      ).toBeNull();
    }
  });

  it('leaves the team pages’ marks as they were', () => {
    const team = '/organizations/o1/projects/p1/teams/t7';
    for (const level of ['epics', 'features', 'stories']) {
      expect(entryMarker(`${team}/backlogs/${level}`, entry('backlogs'), alfaPrm)).toBe('page');
      expect(entryMarker(`${team}/boards/${level}`, entry('boards'), alfaPrm)).toBe('page');
    }
  });
});

describe('feat-039 BR-PB-9 (c): a switch of project or organization keeps the project page and its level — the resolver', () => {
  it.each([
    ['backlogs', 'epics'],
    ['backlogs', 'features'],
    ['backlogs', 'stories'],
    ['boards', 'epics'],
    ['boards', 'features'],
    ['boards', 'stories'],
  ])(
    'on the project’s %s at «%s», another project opens the same page at the same level',
    (surface, level) => {
      expect(from(`/organizations/o1/projects/p1/${surface}/${level}`, alfaAnl)).toBe(
        `/organizations/o1/projects/p2/${surface}/${level}`,
      );
    },
  );

  it('another organization opens the same page of its first project at the same level', () => {
    expect(from('/organizations/o1/projects/p1/backlogs/features', betaRep)).toBe(
      '/organizations/o2/projects/p3/backlogs/features',
    );
    expect(from('/organizations/o1/projects/p1/boards/epics', betaRep)).toBe(
      '/organizations/o2/projects/p3/boards/epics',
    );
  });

  it('opens «Главная» when the organization has no project', () => {
    expect(from('/organizations/o1/projects/p1/backlogs/features', betaNone)).toBe('/home');
  });

  it('does not ask for the new project’s team: a project page names none', () => {
    expect(
      from('/organizations/o1/projects/p1/backlogs/stories', {
        organizationId: 'o1',
        projectId: 'p2',
      }),
    ).toBe('/organizations/o1/projects/p2/backlogs/stories');
  });

  it('asks the entry’s code about the organization it opens, once that organization’s codes are read', () => {
    const askedAbout: (string | undefined)[] = [];
    const onlyBeta: AccessCheck = (_code, _scope, organizationId) => {
      askedAbout.push(organizationId);
      return organizationId === 'o2';
    };
    const pathname = '/organizations/o1/projects/p1/backlogs/features';

    expect(from(pathname, betaRep, onlyBeta, () => true)).toBe(
      '/organizations/o2/projects/p3/backlogs/features',
    );
    expect(askedAbout).toEqual(['o2']);
    expect(from(pathname, alfaAnl, onlyBeta, () => true)).toBe('/home');
  });

  it('does not wait for the codes of the organization it opens (mod-016 design § 2.5)', () => {
    expect(
      from(
        '/organizations/o1/projects/p1/boards/stories',
        betaRep,
        () => false,
        () => false,
      ),
    ).toBe('/organizations/o2/projects/p3/boards/stories');
  });

  it('leaves a team page to BR-18: the entry kept, the level back to «Истории»', () => {
    expect(
      switchTarget(model, allowEverything, {
        pathname: '/organizations/o1/projects/p1/teams/t7/backlogs/features',
        carried: alfaPrm,
        next: alfaAnl,
      }),
    ).toBe('/organizations/o1/projects/p2/teams/t2/backlogs/stories');
  });

  it('does not take a level the page does not serve for a project page', () => {
    expect(from('/organizations/o1/projects/p1/backlogs/tasks', alfaAnl)).toBe('/home');
  });
});
