import { describe, expect, it } from 'vitest';
import ListAltOutlinedIcon from '@mui/icons-material/ListAltOutlined';
import type { AccessCheck } from '../../shared/lib/access';
import type { WorkContextSelection } from '../../shared/lib/workContext';
import { navigationModel } from './model';
import type { NavigationEntry, NavigationModel } from './types';
import {
  deriveTrail,
  entryMarker,
  isEntryVisible,
  isSettingsPage,
  visibleModel,
  type NavigationContext,
} from './visibility';

const allowEverything: AccessCheck = () => true;
const placeholder: NavigationContext = {
  isReal: false,
  selection: { organizationId: 'org-1', projectId: 'prj-1', teamId: 'team-1' },
};
const real: NavigationContext = { ...placeholder, isReal: true };

const organizationEntry: NavigationEntry = {
  id: 'test-organization',
  label: 'Тестовый раздел организации',
  scope: 'organization',
  permission: 'test.organization.read',
  landing: (selection) =>
    selection.organizationId ? `/organizations/${selection.organizationId}/test` : null,
};

const projectEntry: NavigationEntry = {
  id: 'test-project',
  label: 'Тестовый раздел проекта',
  scope: 'project',
  permission: 'test.project.read',
  landing: (selection) =>
    selection.organizationId && selection.projectId
      ? `/organizations/${selection.organizationId}/projects/${selection.projectId}/test`
      : null,
};

function labels(model: ReturnType<typeof visibleModel>): string[] {
  return [...model.top, ...model.work, ...model.settings.flatMap((section) => section.entries)].map(
    (entry) => entry.label,
  );
}

describe('US-3 Scenario: Pages that do not exist yet never appear (BR-7)', () => {
  // mod-001 plan-frontend § 8.4: «Процесс» exists now, so it is no longer a
  // page that does not exist, and «Организация» holds a built page — shown
  // once the context is real, still hidden on the placeholder dataset.
  it.each([
    ['any code, placeholder context', allowEverything, placeholder, true],
    ['any code, even a real context', allowEverything, real, false],
    ['no code at all', (() => false) as AccessCheck, placeholder, true],
  ])('shows only this task’s pages — %s', (_case, access, context, organizationHidden) => {
    const visible = visibleModel(navigationModel, access, context);
    const shown = [...labels(visible), ...visible.settings.map(({ section }) => section.label)];

    for (const label of [
      'Спринты',
      'Запросы',
      // mod-002: «Проект» holds built pages now, hidden only on the placeholder;
      // mod-003: so is «Рабочие элементы», a project-scoped entry;
      // mod-006: and «Доски» and «Бэклоги», team-scoped ones.
      ...(organizationHidden
        ? ['Организация', 'Процесс', 'Проект', 'Рабочие элементы', 'Доски', 'Бэклоги']
        : []),
    ]) {
      expect(shown).not.toContain(label);
    }
  });

  it('registers only built pages: «Главная», «Рабочие элементы», «Доски», «Бэклоги», «Организация» → «Общие», «Проекты», «Процесс», «Проект» → «Общие», «Команды», «Области» and «Справочники» → «Языки»', () => {
    const registered = [
      ...navigationModel.top,
      ...navigationModel.work,
      ...navigationModel.settings.flatMap((section) => section.entries),
    ];
    expect(registered.map((entry) => entry.label)).toEqual([
      'Главная',
      'Рабочие элементы',
      'Доски',
      'Бэклоги',
      'Общие',
      'Проекты',
      'Процесс',
      'Общие',
      'Команды',
      'Области',
      'Языки',
    ]);
  });
});

/** The «Процесс» entry as the model registers it (mod-001 D-5). */
function processEntry(): NavigationEntry {
  const section = navigationModel.settings.find((candidate) => candidate.id === 'organization');
  const entry = section?.entries.find((candidate) => candidate.id === 'process');
  if (!entry) throw new Error('«Процесс» is not registered under «Организация».');
  return entry;
}

const DEMONSTRATION_ID = 'd0000000-0000-4000-8000-000000000001';

describe('mod-001 US-2 Scenario: The read’s code is registered, and nothing grants it yet (frontend half)', () => {
  it('declares exactly process.definition.read, organization-scoped, on the «Процесс» entry', () => {
    expect(processEntry().permission).toBe('process.definition.read');
    expect(processEntry().scope).toBe('organization');
  });
});

describe('mod-001 US-2 Scenario: No visible entry or link leads to the screen before mod-002', () => {
  it('holds «Процесс» under «Настройки» → «Организация», landing on the organization’s process address', () => {
    const section = navigationModel.settings.find((candidate) => candidate.id === 'organization');

    expect(section?.label).toBe('Организация');
    expect(section?.entries.map((entry) => entry.label)).toEqual(['Общие', 'Проекты', 'Процесс']);
    expect(processEntry().landing({ organizationId: DEMONSTRATION_ID })).toBe(
      `/organizations/${DEMONSTRATION_ID}/settings/process`,
    );
    expect(processEntry().landing({ organizationId: 'a/b' })).toBe(
      '/organizations/a%2Fb/settings/process',
    );
    expect(processEntry().landing({})).toBeNull();
  });

  it.each([
    ['every code', allowEverything],
    ['no code', (() => false) as AccessCheck],
  ])('is hidden on the placeholder dataset — %s', (_case, access) => {
    expect(isEntryVisible(processEntry(), access, placeholder)).toBe(false);
    const visible = visibleModel(navigationModel, access, placeholder);
    expect(labels(visible)).not.toContain('Процесс');
  });

  it('is shown once the context is real, and «Настройки» then opens the organization’s «Общие» first (BR-19, mod-002 design § 2.1)', () => {
    const visible = visibleModel(navigationModel, allowEverything, real);

    expect(isEntryVisible(processEntry(), allowEverything, real)).toBe(true);
    expect(visible.settings[0]?.section.label).toBe('Организация');
    expect(visible.settingsLanding).toBe('/organizations/org-1/settings/general');
  });
});

// Code review F-3: the rail shows only the icon, so a main entry registered
// without one used to vanish from the screen while it passed every
// visibility test. Now the compiler refuses it: `npm run build` runs `tsc -b`
// over this file, and the directive fails the build if the error goes away.
describe('The navigation model: a main entry carries an icon (design § 4.1, § 4.3)', () => {
  it('refuses a main navigation entry without an icon at compile time', () => {
    const model: NavigationModel = {
      top: navigationModel.top,
      work: [
        // @ts-expect-error -- `icon` is required on «Основное меню» entries (design § 4.3).
        { id: 'no-icon', label: 'Без значка', scope: 'instance', landing: () => '/no-icon' },
      ],
      settings: [],
    };

    expect(model.work).toHaveLength(1);
  });

  it('takes a settings entry without an icon (design § 4.1)', () => {
    const model: NavigationModel = {
      top: [],
      work: [],
      settings: [{ id: 'section', label: 'Раздел', entries: [organizationEntry] }],
    };

    expect(model.settings[0]?.entries).toHaveLength(1);
  });
});

describe('US-3 Scenario: An organization-scoped entry waits for a real context (BR-8, D-18)', () => {
  it('is hidden on the placeholder dataset, whatever codes the user holds', () => {
    expect(isEntryVisible(organizationEntry, allowEverything, placeholder)).toBe(false);
  });

  it('is shown once the context source reports a real organization', () => {
    expect(isEntryVisible(organizationEntry, allowEverything, real)).toBe(true);
  });

  it('still needs its code to pass the shared check', () => {
    expect(isEntryVisible(organizationEntry, () => false, real)).toBe(false);
  });

  it('a project-scoped entry also needs a project selected', () => {
    const noProject: NavigationContext = { isReal: true, selection: { organizationId: 'org-1' } };
    expect(isEntryVisible(projectEntry, allowEverything, noProject)).toBe(false);
    expect(isEntryVisible(projectEntry, allowEverything, real)).toBe(true);
  });
});

describe('US-3 Scenario: «Настройки» opens the first settings page the user may open (BR-19)', () => {
  const model: NavigationModel = {
    top: navigationModel.top,
    work: [],
    settings: [
      { id: 'organization', label: 'Организация', entries: [organizationEntry] },
      ...navigationModel.settings.filter((section) => section.id === 'dictionaries'),
    ],
  };

  it('follows the model order among the visible entries', () => {
    expect(visibleModel(model, allowEverything, real).settingsLanding).toBe(
      '/organizations/org-1/test',
    );
    expect(visibleModel(model, allowEverything, placeholder).settingsLanding).toBe(
      '/dictionaries/languages',
    );
  });

  it('is hidden when no settings entry is visible, and the empty sections with it', () => {
    const visible = visibleModel(model, () => false, placeholder);
    expect(visible.settingsLanding).toBeNull();
    expect(visible.settings).toHaveLength(0);
  });
});

describe('US-3 Scenario: The current page is marked', () => {
  const languages = navigationModel.settings
    .flatMap((section) => section.entries)
    .find((entry) => entry.id === 'languages') as NavigationEntry;
  const selection: WorkContextSelection = {};

  it('marks the entry `page` on its landing, `true` beneath it, none elsewhere', () => {
    expect(entryMarker('/dictionaries/languages', languages, selection)).toBe('page');
    expect(entryMarker('/dictionaries/languages/new', languages, selection)).toBe('true');
    expect(entryMarker('/dictionaries/languages/ru', languages, selection)).toBe('true');
    expect(entryMarker('/dictionaries/languagesx', languages, selection)).toBeNull();
    expect(entryMarker('/home', languages, selection)).toBeNull();
  });

  it('knows a settings page by the registered settings entries', () => {
    expect(isSettingsPage('/dictionaries/languages', navigationModel, selection)).toBe(true);
    expect(isSettingsPage('/dictionaries/languages/new', navigationModel, selection)).toBe(true);
    expect(isSettingsPage('/home', navigationModel, selection)).toBe(false);
    expect(isSettingsPage('/no-such-page', navigationModel, selection)).toBe(false);
  });

  // qa.md D-1: the router serves `/home/` as `/home`, so the slash must not
  // make a page beneath its own landing.
  it('reads an address with a trailing slash as the same page', () => {
    const home = navigationModel.top.find((entry) => entry.id === 'home') as NavigationEntry;
    expect(entryMarker('/home/', home, selection)).toBe('page');
    expect(entryMarker('/dictionaries/languages/', languages, selection)).toBe('page');
    expect(entryMarker('/dictionaries/languages//', languages, selection)).toBe('page');
    expect(entryMarker('/dictionaries/languages/new/', languages, selection)).toBe('true');
    expect(entryMarker('/home/', languages, selection)).toBeNull();
  });

  // qa.md D-2: while a not-found frame answers the address there is no page.
  it('marks nothing and knows no settings page where no page is current', () => {
    expect(entryMarker(null, languages, selection)).toBeNull();
    expect(isSettingsPage(null, navigationModel, selection)).toBe(false);
  });
});

describe('mod-006 spec § Navigation: «Доски» and «Бэклоги», team-level entries whose level tabs are their page', () => {
  const entry = (id: string) =>
    navigationModel.work.find((candidate) => candidate.id === id) as NavigationEntry;
  const team = { organizationId: 'org-1', projectId: 'prj-1', teamId: 'team-1' };
  const prefix = '/organizations/org-1/projects/prj-1/teams/team-1';

  it('stand after «Рабочие элементы», «Доски» first; team-scoped; declare workitems.workitem.read', () => {
    expect(navigationModel.work.map((candidate) => candidate.label)).toEqual([
      'Рабочие элементы',
      'Доски',
      'Бэклоги',
    ]);
    for (const id of ['boards', 'backlogs']) {
      expect(entry(id).scope).toBe('team');
      expect(entry(id).permission).toBe('workitems.workitem.read');
      expect(entry(id).landing({ organizationId: 'org-1', projectId: 'prj-1' })).toBeNull();
    }
    expect(entry('boards').landing(team)).toBe(`${prefix}/boards/stories`);
    expect(entry('backlogs').landing(team)).toBe(`${prefix}/backlogs/stories`);
  });

  it('are shown once a real team is chosen, not with a project alone', () => {
    for (const id of ['boards', 'backlogs']) {
      expect(isEntryVisible(entry(id), allowEverything, { isReal: true, selection: team })).toBe(
        true,
      );
      expect(
        isEntryVisible(entry(id), allowEverything, {
          isReal: true,
          selection: { organizationId: 'org-1', projectId: 'prj-1' },
        }),
      ).toBe(false);
    }
  });

  it.each([
    ['boards', 'backlogs'],
    ['backlogs', 'boards'],
  ])('«%s» marks `page` on every level tab, leaves a trail on none, and not «%s»', (id, other) => {
    const visible = visibleModel(navigationModel, allowEverything, {
      isReal: true,
      selection: team,
    });
    for (const level of ['epics', 'features', 'stories']) {
      expect(entryMarker(`${prefix}/${id}/${level}`, entry(id), team)).toBe('page');
      expect(deriveTrail(`${prefix}/${id}/${level}`, visible, team)).toEqual([]);
    }
    expect(entryMarker(`${prefix}/${id}/tasks`, entry(id), team)).toBeNull();
    expect(entryMarker(`${prefix}/${other}/stories`, entry(id), team)).toBeNull();
  });
});

describe('US-2 Scenario: Page trail (BR-6) — derived from the navigation model', () => {
  const visible = visibleModel(navigationModel, allowEverything, placeholder);

  it.each([
    ['/home', []],
    ['/dictionaries/languages', [{ label: 'Настройки' }, { label: 'Справочники' }]],
    [
      '/dictionaries/languages/new',
      [
        { label: 'Настройки' },
        { label: 'Справочники' },
        { label: 'Языки', to: '/dictionaries/languages' },
      ],
    ],
    [
      '/dictionaries/languages/ru',
      [
        { label: 'Настройки' },
        { label: 'Справочники' },
        { label: 'Языки', to: '/dictionaries/languages' },
      ],
    ],
    ['/no-such-page', []],
    // qa.md D-1: a trailing slash names the same page, not a page beneath it.
    ['/home/', []],
    ['/dictionaries/languages/', [{ label: 'Настройки' }, { label: 'Справочники' }]],
  ])('%s', (pathname, trail) => {
    expect(deriveTrail(pathname, visible, placeholder.selection)).toEqual(trail);
  });

  it('beneath a main entry’s landing, the entry is a link', () => {
    const model: NavigationModel = {
      top: navigationModel.top,
      work: [
        {
          ...organizationEntry,
          label: 'Рабочие элементы',
          icon: ListAltOutlinedIcon,
          scope: 'organization',
        },
      ],
      settings: [],
    };
    const realVisible = visibleModel(model, allowEverything, real);

    expect(deriveTrail('/organizations/org-1/test/PRM-US-12', realVisible, real.selection)).toEqual(
      [{ label: 'Рабочие элементы', to: '/organizations/org-1/test' }],
    );
  });
});
