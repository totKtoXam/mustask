import { readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import { boardsPermissions } from '../features/boards';
import { organizationsPermissions } from '../features/organizations';
import { workItemsPermissions } from '../features/workitems';
import {
  NARROWER_CODES,
  findViolations,
  narrowerCodesIn,
  type PermissionConstants,
  type SurfaceEntry,
} from './scopeGuard';

/**
 * mod-019 BR-SA-18 (X-3): every panel surface reads a code of a scope other than the organization from the
 * served codes of its object, never from the organization answer. A source scan, as `kitGuard`: a file that
 * names such a code must be registered below with where the object's codes come from, and the marker named
 * must be in the file that provides them. A new surface that asks the organization answer for a project's
 * or a team's code is not registered, and this test fails on it.
 */

const CONSTANTS: PermissionConstants = {
  organizationsPermissions,
  workItemsPermissions,
  boardsPermissions,
};

const SRC = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

function sourcesUnder(dir: string, into: Map<string, string>): void {
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    const relative = path.relative(SRC, full).split(path.sep).join('/');
    if (entry.isDirectory()) {
      if (relative !== 'test') {
        sourcesUnder(full, into);
      }
    } else if (
      /\.tsx?$/.test(entry.name) &&
      !/\.test\.tsx?$/.test(entry.name) &&
      !/^test[A-Z]\w*\.tsx?$/.test(entry.name)
    ) {
      into.set(relative, readFileSync(full, 'utf8'));
    }
  }
}

function productionSources(): Map<string, string> {
  const into = new Map<string, string>();
  sourcesUnder(SRC, into);
  return into;
}

/** The files that define the codes themselves: nothing is asked there. */
const DEFINITIONS = [
  'features/organizations/permissions.ts',
  'features/workitems/permissions.ts',
  'features/boards/permissions.ts',
];

const o = 'features/organizations/';
const w = 'features/workitems/';
const b = 'features/boards/';
const self = (file: string, marker: string) => [{ file, marker }];
const cardHosts = [
  { file: `${w}components/WorkItemCardDialog.tsx`, marker: 'ProjectAccess' },
  { file: `${w}pages/WorkItemFormPage.tsx`, marker: 'ProjectAccess' },
];

/** Where each surface reads the object's codes from (every file is relative to `src/`). */
const SURFACES: Readonly<Record<string, SurfaceEntry>> = {
  'app/navigation/model.ts': {
    why: '«Параметры» of the chosen project, team, department or node: the entry names its `object`',
    via: [
      { file: 'app/navigation/model.ts', marker: 'object:' },
      { file: 'app/navigation/visibility.ts', marker: 'objectCodes' },
    ],
  },
  [`${b}components/AggregateBacklogView.tsx`]: {
    why: 'a group is one project: its reorder is the project rows codes',
    via: self(`${b}components/AggregateBacklogView.tsx`, 'useProjectCodes'),
  },
  [`${b}components/AggregateBoardView.tsx`]: {
    why: 'a card is moved by its own project codes (D-4, F-6)',
    via: self(`${b}components/AggregateBoardView.tsx`, 'useProjectCodes'),
  },
  [`${b}components/BoardColumns.tsx`]: {
    why: 'a team board card is moved by its own project codes',
    via: self(`${b}components/BoardColumns.tsx`, 'useProjectCodes'),
  },
  [`${b}hooks/useAggregateNewWorkItem.tsx`]: {
    why: 'the aggregate board create offers the projects whose own codes hold the write (feat-060)',
    via: self(`${b}hooks/useAggregateNewWorkItem.tsx`, 'useProjectCodes'),
  },
  [`${b}hooks/useBoardAccess.ts`]: {
    why: 'the checks of a backlog: project rows codes and the team own read',
    via: self(`${b}hooks/useBoardAccess.ts`, 'useProjectCodes'),
  },
  [`${o}components/AreaNodeDialogs.tsx`]: {
    why: 'the dialogs lock on the code of the page that mounts them, which provides the object codes',
    via: [
      { file: `${o}pages/AreaPage.tsx`, marker: 'ObjectAccess' },
      { file: `${o}pages/AreasPage.tsx`, marker: 'ObjectAccess' },
    ],
  },
  [`${o}components/TeamMembersSection.tsx`]: {
    why: 'a section of the team page',
    via: self(`${o}pages/TeamPage.tsx`, 'ObjectAccess'),
  },
  [`${o}pages/AreaMembersPage.tsx`]: {
    why: 'the department own read',
    via: self(`${o}pages/AreaMembersPage.tsx`, 'useObjectAccess'),
  },
  [`${o}pages/AreaPage.tsx`]: {
    why: 'the department own read',
    via: self(`${o}pages/AreaPage.tsx`, 'useObjectAccess'),
  },
  [`${o}pages/AreasPage.tsx`]: {
    why: 'the tree writable ids, once',
    via: self(`${o}pages/AreasPage.tsx`, 'useWritableAreaIds'),
  },
  [`${o}pages/OrganizationTeamsPage.tsx`]: {
    why: 'team rows carry no codes: the role floor',
    via: self(`${o}pages/OrganizationTeamsPage.tsx`, 'RoleFloorAccess'),
  },
  [`${o}pages/ProjectAccessPage.tsx`]: {
    why: 'the project own read',
    via: self(`${o}pages/ProjectAccessPage.tsx`, 'useObjectAccess'),
  },
  [`${o}pages/ProjectGeneralPage.tsx`]: {
    why: 'the project and its node own reads',
    via: self(`${o}pages/ProjectGeneralPage.tsx`, 'useObjectAccess'),
  },
  [`${o}pages/ProjectListPage.tsx`]: {
    why: 'the link dialog asks the role floor',
    via: self(`${o}pages/ProjectListPage.tsx`, 'RoleFloorAccess'),
  },
  [`${o}pages/TeamPage.tsx`]: {
    why: 'the team own read',
    via: self(`${o}pages/TeamPage.tsx`, 'useObjectAccess'),
  },
  [`${o}pages/TeamsPage.tsx`]: {
    why: 'the project own read',
    via: self(`${o}pages/TeamsPage.tsx`, 'useObjectAccess'),
  },
  [`${w}components/card/WorkItemCard.tsx`]: {
    why: 'the delete and purge locks, under the host project codes',
    via: cardHosts,
  },
  [`${w}hooks/useWorkItemAccess.ts`]: {
    why: 'asked below a host that provides the project codes',
    via: cardHosts,
  },
  [`${w}pages/WorkItemsPage.tsx`]: {
    why: 'the project list',
    via: self(`${w}pages/WorkItemsPage.tsx`, 'ProjectAccess'),
  },
};

describe('mod-019 BR-SA-18: no surface reads a narrower code from the organization answer', () => {
  it('every source that names one reads it from its object', () => {
    const findings = findViolations(productionSources(), CONSTANTS, SURFACES, DEFINITIONS);
    expect(findings.map((finding) => `${finding.file} ${finding.message}`)).toEqual([]);
  });

  it('knows the codes of the catalog that are not the organization one', () => {
    for (const code of [
      'workitems.workitem.write',
      'workitems.link.write',
      'boards.backlog.reorder',
      'boards.projectbacklog.reorder',
      'organizations.project.write',
      'organizations.team.link',
      'organizations.area.write',
    ]) {
      expect(NARROWER_CODES.has(code), code).toBe(true);
    }
    for (const code of ['organizations.organization.read', 'organizations.area.read']) {
      expect(NARROWER_CODES.has(code), code).toBe(false);
    }
  });

  describe('the scan fails on a surface that breaks the rule', () => {
    const newcomer = 'features/x/pages/Newcomer.tsx';
    const asks = `
      import { workItemsPermissions } from '../../workitems';
      const canWrite = access(workItemsPermissions.workItemWrite, 'organization');
    `;
    const claim = { why: 'claims the project codes', via: self(newcomer, 'ProjectAccess') };

    it('reads a code named through a constant', () => {
      expect(narrowerCodesIn(asks, CONSTANTS)).toEqual(['workitems.workitem.write']);
    });

    it('reads a code named through an alias and a literal', () => {
      const text = `
        const read = organizationsPermissions;
        const a = access(read.projectWrite, 'organization');
        const b = access('boards.backlog.reorder', 'organization');
      `;
      expect(narrowerCodesIn(text, CONSTANTS)).toEqual([
        'boards.backlog.reorder',
        'organizations.project.write',
      ]);
    });

    it('reads a code named through a destructuring, renamed and through an alias too', () => {
      const text = `
        const { workItemWrite } = workItemsPermissions;
        const { projectWrite: canWriteProject, teamCreate } = organizationsPermissions;
        const read = boardsPermissions;
        const { backlogReorder = 'x' } = read;
        access(workItemWrite, 'organization');
      `;
      expect(narrowerCodesIn(text, CONSTANTS)).toEqual([
        'boards.backlog.reorder',
        'organizations.project.write',
        'workitems.workitem.write',
      ]);
    });

    it('reads the narrower codes of an object taken whole by a rest element', () => {
      const text = `const { teamCreate, ...rest } = boardsPermissions;`;
      expect(narrowerCodesIn(text, CONSTANTS)).toContain('boards.backlog.reorder');
    });

    it('reports an unregistered surface that destructures a narrower code', () => {
      const text = `const { workItemWrite } = workItemsPermissions; access(workItemWrite, 'organization');`;
      const findings = findViolations(new Map([[newcomer, text]]), CONSTANTS, {}, []);
      expect(findings.map((finding) => finding.file)).toEqual([newcomer]);
    });

    it('is silent on a destructured organization code', () => {
      const text = `const { teamCreate } = organizationsPermissions;`;
      expect(narrowerCodesIn(text, CONSTANTS)).toEqual([]);
    });

    it('is silent on an organization code', () => {
      const text = `access(organizationsPermissions.teamCreate, 'organization'); access('organizations.area.read', 'organization');`;
      expect(narrowerCodesIn(text, CONSTANTS)).toEqual([]);
    });

    it('reports an unregistered surface', () => {
      const findings = findViolations(new Map([[newcomer, asks]]), CONSTANTS, {}, []);
      expect(findings.map((finding) => finding.file)).toEqual([newcomer]);
    });

    it('reports a registered surface whose object-codes marker is missing', () => {
      const findings = findViolations(
        new Map([[newcomer, asks]]),
        CONSTANTS,
        { [newcomer]: claim },
        [],
      );
      expect(findings).toHaveLength(1);
    });

    it('lets a registered surface through, and reports a stale entry', () => {
      const surfaces = { [newcomer]: claim };
      const ok = new Map([[newcomer, `${asks} // ProjectAccess`]]);
      expect(findViolations(ok, CONSTANTS, surfaces, [])).toEqual([]);
      const stale = new Map([[newcomer, '// ProjectAccess']]);
      expect(findViolations(stale, CONSTANTS, surfaces, [])).toHaveLength(1);
    });
  });
});
