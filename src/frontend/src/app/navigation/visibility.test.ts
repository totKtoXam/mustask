import { describe, expect, it } from 'vitest';
import ListAltOutlinedIcon from '@mui/icons-material/ListAltOutlined';
import type { AccessCheck } from '../../shared/lib/access';
import type { WorkContextSelection } from '../../shared/lib/workContext';
import { buildNavigationModel } from './model';
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
const organizationOnly: WorkContextSelection = { organizationId: 'org-1' };
const model = buildNavigationModel(organizationOnly);

const organizationEntry: NavigationEntry = {
  id: 'test-organization',
  label: 'Тестовый раздел организации',
  scope: 'organization',
  permission: 'test.organization.read',
  landing: (selection) =>
    selection.organizationId ? `/organizations/${selection.organizationId}/test` : null,
  pages: ['/organizations/:organizationId/test'],
  beneath: ['/organizations/:organizationId/test/*'],
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
  pages: ['/organizations/:organizationId/projects/:projectId/test'],
};

function labels(visible: ReturnType<typeof visibleModel>): string[] {
  return [...visible.top, ...visible.work, ...visible.settings.flatMap((s) => s.entries)].map(
    (entry) => entry.label,
  );
}

function entryIn(built: NavigationModel, id: string): NavigationEntry {
  const found = [...built.top, ...built.work, ...built.settings.flatMap((s) => s.entries)].find(
    (candidate) => candidate.id === id,
  );
  if (!found) throw new Error(`No entry ${id} in the model.`);
  return found;
}

describe('US-3 Scenario: Pages that do not exist yet never appear (BR-7)', () => {
  it.each([
    ['any code, placeholder context', allowEverything, placeholder],
    ['no code at all', (() => false) as AccessCheck, placeholder],
  ])('hides the scoped entries without a real context — %s', (_case, access, context) => {
    const shown = labels(visibleModel(model, access, context));

    for (const label of ['Обзор', 'Структура', 'Проекты', 'Команды', 'Участники', 'Процесс']) {
      expect(shown).not.toContain(label);
    }
  });

  it('registers the organization menu from the table, then the settings of ADR-0032', () => {
    const registered = [...model.top, ...model.work, ...model.settings.flatMap((s) => s.entries)];
    expect(registered.map((entry) => entry.label)).toEqual([
      'Главная',
      'Обзор',
      'Структура',
      'Проекты',
      'Команды',
      'Рабочие элементы',
      'Канбан',
      'Бэклог',
      'Участники',
      'Общие',
      'Процесс',
      'Языки',
      'Витрина компонентов',
    ]);
  });

  it('adds one «Параметры» group per chosen object, in order of the levels', () => {
    const chosen = buildNavigationModel({
      organizationId: 'org-1',
      departmentId: 'dep-1',
      teamId: 'team-1',
      projectId: 'prj-1',
    });

    expect(chosen.settings.map((section) => section.label)).toEqual([
      'Организация',
      'Департамент',
      'Команда',
      'Проект',
      'Справочники',
      'Интерфейс',
    ]);
  });

  // tech-015 design § 10.1: the showcase's entry is the last of the settings.
  it('puts «Интерфейс» → «Витрина компонентов» last, instance-scoped and gated by its code', () => {
    const section = model.settings.at(-1);
    expect(section?.label).toBe('Интерфейс');
    expect(section?.entries.map((entry) => entry.label)).toEqual(['Витрина компонентов']);
    expect(section?.entries[0]?.scope).toBe('instance');
    expect(section?.entries[0]?.permission).toBe('dictionaries.languages.write');
    expect(section?.entries[0]?.landing({})).toBe('/ui-kit');
  });

  it.each([
    ['holds the showcase code', ['dictionaries.languages.write'], true],
    ['holds the read code only', ['dictionaries.languages.read'], false],
  ])('shows the showcase entry only to who %s', (_case, codes, shown) => {
    const visible = visibleModel(model, (code) => codes.includes(code), real);
    expect(labels(visible).includes('Витрина компонентов')).toBe(shown);
  });
});

describe('feat-043 C-3: the «Структура» landing keeps the chosen team (BR-NV-19)', () => {
  const chosen = buildNavigationModel({
    organizationId: 'org-1',
    departmentId: 'dep-1',
    teamId: 'team-1',
    projectId: 'prj-1',
    structureId: 'node-1',
  });
  const structure = entryIn(chosen, 'project-structure');

  it('stands on the project’s node, the team riding in the query', () => {
    expect(
      structure.landing({
        organizationId: 'org-1',
        projectId: 'prj-1',
        teamId: 'team-1',
        structureId: 'node-1',
      }),
    ).toBe('/organizations/org-1/areas/node-1/overview?teamId=team-1');
  });

  it('has no query when no team is chosen, and no address without the node', () => {
    expect(
      structure.landing({ organizationId: 'org-1', projectId: 'prj-1', structureId: 'node-1' }),
    ).toBe('/organizations/org-1/areas/node-1/overview');
    expect(structure.landing({ organizationId: 'org-1', projectId: 'prj-1' })).toBeNull();
  });
});

describe('mod-001 US-2: the «Процесс» entry', () => {
  const processEntry = entryIn(model, 'process');
  const DEMONSTRATION_ID = 'd0000000-0000-4000-8000-000000000001';

  it('declares exactly process.definition.read, organization-scoped', () => {
    expect(processEntry.permission).toBe('process.definition.read');
    expect(processEntry.scope).toBe('organization');
  });

  it('lands on the organization’s process address, encoded, and nowhere without one', () => {
    expect(processEntry.landing({ organizationId: DEMONSTRATION_ID })).toBe(
      `/organizations/${DEMONSTRATION_ID}/settings/process`,
    );
    expect(processEntry.landing({ organizationId: 'a/b' })).toBe(
      '/organizations/a%2Fb/settings/process',
    );
    expect(processEntry.landing({})).toBeNull();
  });

  it('carries the chosen objects to the other settings pages (QA F-3)', () => {
    expect(processEntry.landing({ organizationId: 'org-1', teamId: 't', projectId: 'p' })).toBe(
      '/organizations/org-1/settings/process?teamId=t&projectId=p',
    );
  });

  it.each([
    ['every code', allowEverything],
    ['no code', (() => false) as AccessCheck],
  ])('is hidden without a real context — %s', (_case, access) => {
    expect(isEntryVisible(processEntry, access, placeholder)).toBe(false);
    expect(labels(visibleModel(model, access, placeholder))).not.toContain('Процесс');
  });

  it('is shown once the context is real, and «Настройки» then opens the organization’s «Общие» first (BR-19)', () => {
    const visible = visibleModel(model, allowEverything, real);

    expect(isEntryVisible(processEntry, allowEverything, real)).toBe(true);
    expect(visible.settings[0]?.section.label).toBe('Организация');
    // The landing carries the chosen objects, so the settings address names the selection.
    expect(visible.settingsLanding).toBe(
      '/organizations/org-1/settings/general?teamId=team-1&projectId=prj-1',
    );
  });
});

// Code review F-3: the rail shows only the icon, so a main entry registered without one used to
// vanish from the screen. The compiler refuses it: `tsc -b` runs over this file.
describe('The navigation model: a main entry carries an icon (design § 4.1, § 4.3)', () => {
  it('refuses a main navigation entry without an icon at compile time', () => {
    const built: NavigationModel = {
      top: model.top,
      work: [
        // @ts-expect-error -- `icon` is required on «Основное меню» entries (design § 4.3).
        {
          id: 'no-icon',
          label: 'Без значка',
          scope: 'instance',
          landing: () => '/no-icon',
          pages: [],
        },
      ],
      settings: [],
    };

    expect(built.work).toHaveLength(1);
  });

  it('takes a settings entry without an icon (design § 4.1)', () => {
    const built: NavigationModel = {
      top: [],
      work: [],
      settings: [{ id: 'section', label: 'Раздел', entries: [organizationEntry] }],
    };

    expect(built.settings[0]?.entries).toHaveLength(1);
  });
});

describe('US-3 Scenario: A scoped entry waits for a real context (BR-8, D-18)', () => {
  it('is hidden without a real context, whatever codes the user holds', () => {
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
  const small: NavigationModel = {
    top: model.top,
    work: [],
    settings: [
      { id: 'organization', label: 'Организация', entries: [organizationEntry] },
      ...model.settings.filter((section) => section.id === 'dictionaries'),
    ],
  };

  it('follows the model order among the visible entries', () => {
    expect(visibleModel(small, allowEverything, real).settingsLanding).toBe(
      '/organizations/org-1/test',
    );
    expect(visibleModel(small, allowEverything, placeholder).settingsLanding).toBe(
      '/dictionaries/languages',
    );
  });

  it('is hidden when no settings entry is visible, and the empty sections with it', () => {
    const visible = visibleModel(small, () => false, placeholder);
    expect(visible.settingsLanding).toBeNull();
    expect(visible.settings).toHaveLength(0);
  });
});

describe('US-3 Scenario: The current page is marked', () => {
  const languages = entryIn(model, 'languages');
  const selection: WorkContextSelection = {};

  it('marks the entry `page` on its landing, `true` beneath it, none elsewhere', () => {
    expect(entryMarker('/dictionaries/languages', languages, selection)).toBe('page');
    expect(entryMarker('/dictionaries/languages/new', languages, selection)).toBe('true');
    expect(entryMarker('/dictionaries/languages/ru', languages, selection)).toBe('true');
    expect(entryMarker('/dictionaries/languagesx', languages, selection)).toBeNull();
    expect(entryMarker('/home', languages, selection)).toBeNull();
  });

  it('knows a settings page by the settings entries the user sees', () => {
    const visible = visibleModel(model, allowEverything, { isReal: false, selection });
    expect(isSettingsPage('/dictionaries/languages', visible, selection)).toBe(true);
    expect(isSettingsPage('/dictionaries/languages/new', visible, selection)).toBe(true);
    expect(isSettingsPage('/home', visible, selection)).toBe(false);
    expect(isSettingsPage('/no-such-page', visible, selection)).toBe(false);
  });

  // qa.md D-1: the router serves `/home/` as `/home`, so the slash must not make a page beneath.
  it('reads an address with a trailing slash as the same page', () => {
    const home = entryIn(model, 'home');
    expect(entryMarker('/home/', home, selection)).toBe('page');
    expect(entryMarker('/dictionaries/languages/', languages, selection)).toBe('page');
    expect(entryMarker('/dictionaries/languages/new/', languages, selection)).toBe('true');
    expect(entryMarker('/home/', languages, selection)).toBeNull();
  });

  // qa.md D-2: while a not-found frame answers the address there is no page.
  it('marks nothing and knows no settings page where no page is current', () => {
    const visible = visibleModel(model, allowEverything, { isReal: false, selection });
    expect(entryMarker(null, languages, selection)).toBeNull();
    expect(isSettingsPage(null, visible, selection)).toBe(false);
  });
});

// bug-003 and tech-009 F4: the marker, the settings menu and the trail match as the router does.
describe('bug-003 / tech-009 F4: an address is matched as the router matches it', () => {
  const languages = entryIn(model, 'languages');
  const selection: WorkContextSelection = {};
  const visible = visibleModel(model, allowEverything, { isReal: false, selection });

  it.each(['/Dictionaries/Languages', '/dictionaries/%6Canguages', '/DICTIONARIES/LANGUAGES/'])(
    'marks «Языки» `page` at %s, a settings page with its trail',
    (address) => {
      expect(entryMarker(address, languages, selection)).toBe('page');
      expect(isSettingsPage(address, visible, selection)).toBe(true);
      expect(deriveTrail(address, visible, selection)).toEqual([
        { label: 'Настройки' },
        { label: 'Справочники' },
      ]);
    },
  );

  it('marks the menu entry of an object page whatever the case and encoding of its ids', () => {
    const chosen = { organizationId: 'org-1', teamId: 'team-1' };
    const team = buildNavigationModel(chosen);
    const overview = entryIn(team, 'team-overview');

    expect(entryMarker('/Organizations/org-1/Teams/team%2D1/Overview', overview, chosen)).toBe(
      'page',
    );
  });
});

describe('mod-016 design § 2.6: the check is asked about the selection’s organization', () => {
  it('passes the selection’s organization to the shared check for every entry with a code', () => {
    const asked: [string, string, string | undefined][] = [];
    const access: AccessCheck = (code, scope, organizationId) => {
      asked.push([code, scope, organizationId]);
      return true;
    };

    isEntryVisible(organizationEntry, access, real);
    isEntryVisible(projectEntry, access, real);

    expect(asked).toEqual([
      ['test.organization.read', 'organization', 'org-1'],
      ['test.project.read', 'project', 'org-1'],
    ]);
  });

  it('shows an organization’s entry only by that organization’s codes', () => {
    const heldByA: AccessCheck = (_code, _scope, organizationId) => organizationId === 'org-a';

    expect(
      isEntryVisible(organizationEntry, heldByA, {
        isReal: true,
        selection: { organizationId: 'org-a' },
      }),
    ).toBe(true);
    expect(
      isEntryVisible(organizationEntry, heldByA, {
        isReal: true,
        selection: { organizationId: 'org-b' },
      }),
    ).toBe(false);
  });
});

describe('mod-016 design § 2.5, DC-8: the switch resolver does not wait for unread codes', () => {
  const unread: AccessCheck = (_code, scope) => scope === 'instance';

  it('does not ask the code of a non-instance entry when told the codes are not read', () => {
    expect(isEntryVisible(organizationEntry, unread, { ...real, skipServedCodes: true })).toBe(
      true,
    );
    expect(isEntryVisible(projectEntry, unread, { ...real, skipServedCodes: true })).toBe(true);
  });

  it('still needs a real context and the landing', () => {
    expect(
      isEntryVisible(organizationEntry, unread, { ...placeholder, skipServedCodes: true }),
    ).toBe(false);
    expect(
      isEntryVisible(projectEntry, unread, {
        isReal: true,
        selection: { organizationId: 'org-1' },
        skipServedCodes: true,
      }),
    ).toBe(false);
  });

  it('keeps asking an instance entry its code from the token', () => {
    const languages = entryIn(model, 'languages');

    expect(isEntryVisible(languages, () => false, { ...real, skipServedCodes: true })).toBe(false);
    expect(isEntryVisible(languages, () => true, { ...real, skipServedCodes: true })).toBe(true);
  });

  it('is not what the shell’s own navigation does: unread codes hide the entries', () => {
    expect(isEntryVisible(organizationEntry, unread, real)).toBe(false);
  });
});

describe('mod-016 design § 6.1: a settings page needs a visible entry', () => {
  const small: NavigationModel = {
    top: model.top,
    work: [],
    settings: [{ id: 'organization', label: 'Организация', entries: [organizationEntry] }],
  };
  const selection: WorkContextSelection = { organizationId: 'org-1' };
  const page = '/organizations/org-1/test';

  it('is not a settings page while the entry’s organization codes are unread', () => {
    const hidden = visibleModel(small, () => false, { isReal: true, selection });

    expect(isSettingsPage(page, hidden, selection)).toBe(false);
    expect(deriveTrail(page, hidden, selection)).toEqual([]);
    expect(hidden.settingsLanding).toBeNull();
  });

  it('is one, with its trail, once the codes are read', () => {
    const shown = visibleModel(small, allowEverything, { isReal: true, selection });

    expect(isSettingsPage(page, shown, selection)).toBe(true);
    expect(isSettingsPage(`${page}/deeper`, shown, selection)).toBe(true);
    expect(deriveTrail(page, shown, selection)).toEqual([
      { label: 'Настройки' },
      { label: 'Организация' },
    ]);
  });
});

describe('The menu of a team: «Канбан» and «Бэклог», whose levels are their page', () => {
  const team: WorkContextSelection = {
    organizationId: 'org-1',
    projectId: 'prj-1',
    teamId: 'team-1',
  };
  const teamModel = buildNavigationModel(team);
  const entry = (id: string) => entryIn(teamModel, id);
  // The prefix names the most specific chosen object: the project here (ADR-0032 § 1).
  const projectModel = buildNavigationModel(team);
  const prefix = '/organizations/org-1/projects/prj-1';

  it('declares workitems.workitem.read and lands on the stories level', () => {
    for (const id of ['project-boards', 'project-backlogs']) {
      expect(entryIn(projectModel, id).permission).toBe('workitems.workitem.read');
    }
    expect(entryIn(projectModel, 'project-boards').landing(team)).toBe(
      `${prefix}/boards/stories?teamId=team-1`,
    );
    expect(entryIn(projectModel, 'project-backlogs').landing(team)).toBe(
      `${prefix}/backlogs/stories?teamId=team-1`,
    );
  });

  it('are the team’s own when only a team is chosen', () => {
    const only = { organizationId: 'org-1', teamId: 'team-1' };
    const onlyModel = buildNavigationModel(only);
    expect(entryIn(onlyModel, 'team-boards').landing(only)).toBe(
      '/organizations/org-1/teams/team-1/boards/stories',
    );
    expect(
      isEntryVisible(entryIn(onlyModel, 'team-boards'), allowEverything, {
        isReal: true,
        selection: only,
      }),
    ).toBe(true);
    expect(entry('project-boards').scope).toBe('organization');
  });

  it.each([
    ['boards', 'backlogs'],
    ['backlogs', 'boards'],
  ])('«%s» marks `page` on every level, leaves a trail on none, and not «%s»', (id, other) => {
    const visible = visibleModel(projectModel, allowEverything, { isReal: true, selection: team });
    const own = entryIn(projectModel, `project-${id}`);
    for (const level of ['epics', 'features', 'stories']) {
      expect(entryMarker(`${prefix}/${id}/${level}`, own, team)).toBe('page');
    }
    expect(entryMarker(`${prefix}/${other}/stories`, own, team)).toBeNull();
    expect(deriveTrail(`${prefix}/${id}/stories`, visible, team)).toEqual([]);
  });
});

describe('US-2 Scenario: Page trail (BR-6) — derived from the navigation model', () => {
  const visible = visibleModel(model, allowEverything, placeholder);

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
    ['/no-such-page', []],
    // qa.md D-1: a trailing slash names the same page, not a page beneath it.
    ['/home/', []],
    ['/dictionaries/languages/', [{ label: 'Настройки' }, { label: 'Справочники' }]],
  ])('%s', (pathname, trail) => {
    expect(deriveTrail(pathname, visible, placeholder.selection)).toEqual(trail);
  });

  it('beneath a main entry’s landing, the entry is a link', () => {
    const small: NavigationModel = {
      top: model.top,
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
    const realVisible = visibleModel(small, allowEverything, real);

    expect(deriveTrail('/organizations/org-1/test/PRM-US-12', realVisible, real.selection)).toEqual(
      [{ label: 'Рабочие элементы', to: '/organizations/org-1/test' }],
    );
  });
});
