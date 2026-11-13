/**
 * What the header selectors offer and what a choice resets (feat-043: spec BR-NV-17, ADR-0032
 * § 5, design § 3, decision D-7). Pure: the shell reads `selector-offers` once and asks these
 * functions; nothing here touches the DOM, the address or the network.
 *
 * Fit is judged pair by pair, never against the selection as a whole:
 * - a project fits a department when it stands in it, a team when the team is linked to it;
 * - a team fits a department when it stands in it (its place, mod-021 BR-TD-1; the department–team
 *   links are retired);
 * - a department fits whatever those two say of it.
 * A selector hides nothing: `offerFor` returns the fitting objects and the rest. A fitting
 * choice resets nothing; a choice from the rest resets each chosen value it no longer fits.
 */

/** The three object selectors, least specific first (the organization is the fourth, outside this). */
export type SelectorKind = 'department' | 'team' | 'project';

/** The order of specificity: a later kind is more specific (ADR-0032 § 1). */
export const SELECTOR_KINDS: readonly SelectorKind[] = ['department', 'team', 'project'];

/** The chosen object of each kind, or `null` for «все». */
export interface ObjectSelection {
  department: string | null;
  team: string | null;
  project: string | null;
}

export const NO_SELECTION: ObjectSelection = { department: null, team: null, project: null };

/** What `GET …/selector-offers` returns (api-contract § 2), as far as fit needs it. */
export interface FitData {
  departments: readonly { id: string; name: string }[];
  projects: readonly {
    id: string;
    name: string;
    key: string;
    departmentId: string | null;
    teamIds: readonly string[];
  }[];
  teams: readonly {
    id: string;
    name: string;
    isMember: boolean;
    departmentId: string;
  }[];
}

/** One option of a selector. `key` is a project's, `isMember` a team's. */
export interface SelectorOption {
  id: string;
  name: string;
  key?: string;
  isMember?: boolean;
}

export interface SelectorOffer {
  /** Fit every other chosen selector. */
  fitting: readonly SelectorOption[];
  /** «Остальные»: offered, and choosing one resets what no longer fits. */
  others: readonly SelectorOption[];
}

/** One value that a choice reset to «все». */
export interface ResetValue {
  kind: SelectorKind;
  name: string;
}

export interface ChoiceResult {
  selection: ObjectSelection;
  /** The kinds reset, least specific first, with the names they had. Empty: no notice. */
  reset: readonly ResetValue[];
}

const KIND_WORDS: Record<SelectorKind, string> = {
  department: 'департамент',
  team: 'команда',
  project: 'проект',
};

const collator = new Intl.Collator('ru', { sensitivity: 'base' });

function byName(a: SelectorOption, b: SelectorOption): number {
  return collator.compare(a.name, b.name) || a.id.localeCompare(b.id);
}

/** Own teams first, then by name (design § 3). */
function teamOrder(a: SelectorOption, b: SelectorOption): number {
  if (a.isMember !== b.isMember) return a.isMember ? -1 : 1;
  return byName(a, b);
}

function teamStandsInDepartment(data: FitData, teamId: string, departmentId: string): boolean {
  return data.teams.some((team) => team.id === teamId && team.departmentId === departmentId);
}

/** Whether the object `idA` of kind `a` fits the object `idB` of kind `b`; symmetric. */
export function pairFits(
  data: FitData,
  a: SelectorKind,
  idA: string,
  b: SelectorKind,
  idB: string,
): boolean {
  if (a === b) return idA === idB;
  if (a > b) return pairFits(data, b, idB, a, idA);
  // Here `a` sorts before `b` as a string: department < project < team.
  if (a === 'department' && b === 'project') {
    return data.projects.some((project) => project.id === idB && project.departmentId === idA);
  }
  if (a === 'department' && b === 'team') return teamStandsInDepartment(data, idB, idA);
  // a === 'project', b === 'team'
  return data.projects.some((project) => project.id === idA && project.teamIds.includes(idB));
}

function optionsOf(data: FitData, kind: SelectorKind): SelectorOption[] {
  switch (kind) {
    case 'department':
      return data.departments.map(({ id, name }) => ({ id, name }));
    case 'project':
      return data.projects.map(({ id, name, key }) => ({ id, name, key }));
    case 'team':
      return data.teams.map(({ id, name, isMember }) => ({ id, name, isMember }));
  }
}

/** Whether `id` of `kind` fits every other chosen value of the selection. */
function fitsSelection(
  data: FitData,
  selection: ObjectSelection,
  kind: SelectorKind,
  id: string,
): boolean {
  return SELECTOR_KINDS.every((other) => {
    if (other === kind) return true;
    const chosen = selection[other];
    return chosen === null || pairFits(data, kind, id, other, chosen);
  });
}

/**
 * The offer of the selector `kind` under `selection`: the fitting values, then the rest, each
 * by name (teams: the user's own first). With nothing else chosen everything fits.
 */
export function offerFor(
  data: FitData,
  selection: ObjectSelection,
  kind: SelectorKind,
): SelectorOffer {
  const order = kind === 'team' ? teamOrder : byName;
  const fitting: SelectorOption[] = [];
  const others: SelectorOption[] = [];
  for (const option of optionsOf(data, kind)) {
    (fitsSelection(data, selection, kind, option.id) ? fitting : others).push(option);
  }
  return { fitting: fitting.sort(order), others: others.sort(order) };
}

function nameOf(data: FitData, kind: SelectorKind, id: string): string {
  return optionsOf(data, kind).find((option) => option.id === id)?.name ?? '';
}

/**
 * Chooses `id` (`null`: «все») in the selector `kind`. Each other chosen value that the new
 * one does not fit resets to «все» and is reported; a fitting choice and «все» reset nothing.
 */
export function applyChoice(
  data: FitData,
  selection: ObjectSelection,
  kind: SelectorKind,
  id: string | null,
): ChoiceResult {
  const next: ObjectSelection = { ...selection, [kind]: id };
  const reset: ResetValue[] = [];
  if (id !== null) {
    for (const other of SELECTOR_KINDS) {
      const chosen = selection[other];
      if (other === kind || chosen === null) continue;
      if (!pairFits(data, kind, id, other, chosen)) {
        next[other] = null;
        reset.push({ kind: other, name: nameOf(data, other, chosen) });
      }
    }
  }
  return { selection: next, reset };
}

/**
 * Opening an object from a row or a link (D-3): it becomes the most specific one. The kinds
 * more specific than it reset to «все» without a notice; a less specific value stays when it
 * fits, else it resets and is reported, as a choice from «Остальные» does (design § 3).
 */
export function openObject(
  data: FitData,
  selection: ObjectSelection,
  kind: SelectorKind,
  id: string,
): ChoiceResult {
  const next: ObjectSelection = { ...selection, [kind]: id };
  const reset: ResetValue[] = [];
  const at = SELECTOR_KINDS.indexOf(kind);
  SELECTOR_KINDS.forEach((other, index) => {
    const chosen = selection[other];
    if (index === at || chosen === null) return;
    if (index > at) {
      next[other] = null;
    } else if (!pairFits(data, kind, id, other, chosen)) {
      next[other] = null;
      reset.push({ kind: other, name: nameOf(data, other, chosen) });
    }
  });
  return { selection: next, reset };
}

/**
 * A link change (a team linked or unlinked, a project moved) re-judged the offers: each chosen
 * value that fitted a less specific one before and no longer does resets to «все», the more
 * specific of the pair (BR-NV-17, ADR-0032 § 5, US-7). A value the new offers do not hold is a
 * gone object, not a link change: it is not reset here. Nothing reset: the selection as it was.
 */
export function resetUnfitted(
  before: FitData,
  after: FitData,
  selection: ObjectSelection,
): ChoiceResult {
  const next: ObjectSelection = { ...selection };
  const reset: ResetValue[] = [];
  const held = (kind: SelectorKind, id: string) =>
    optionsOf(after, kind).some((option) => option.id === id);
  SELECTOR_KINDS.forEach((kind, index) => {
    const id = selection[kind];
    if (id === null || !held(kind, id)) return;
    const unfitted = SELECTOR_KINDS.slice(0, index).some((other) => {
      const chosen = next[other];
      return (
        chosen !== null &&
        held(other, chosen) &&
        pairFits(before, kind, id, other, chosen) &&
        !pairFits(after, kind, id, other, chosen)
      );
    });
    if (unfitted) {
      next[kind] = null;
      reset.push({ kind, name: nameOf(after, kind, id) });
    }
  });
  return { selection: next, reset };
}

/**
 * A remembered selection read against the offers it meets for the first time (US-7, ADR-0032
 * §§ 5, 6): each chosen value that does not fit a less specific chosen one resets to «все», the
 * more specific of the pair, so no menu entry carries a pair the address would answer not found
 * for. A link change was missed while the tab was closed; nothing else differs from
 * `resetUnfitted`, which judges the change itself. A value the offers do not hold is a gone
 * object, not judged here. Nothing reset: the selection as it was.
 */
export function repairUnfit(data: FitData, selection: ObjectSelection): ChoiceResult {
  const next: ObjectSelection = { ...selection };
  const reset: ResetValue[] = [];
  const held = (kind: SelectorKind, id: string) =>
    optionsOf(data, kind).some((option) => option.id === id);
  SELECTOR_KINDS.forEach((kind, index) => {
    const id = selection[kind];
    if (id === null || !held(kind, id)) return;
    const unfitted = SELECTOR_KINDS.slice(0, index).some((other) => {
      const chosen = next[other];
      return chosen !== null && held(other, chosen) && !pairFits(data, kind, id, other, chosen);
    });
    if (unfitted) {
      next[kind] = null;
      reset.push({ kind, name: nameOf(data, kind, id) });
    }
  });
  return { selection: next, reset };
}

/** «Выбор сброшен: команда MARS, проект A.», or `null` when nothing was reset. */
export function resetNotice(reset: readonly ResetValue[]): string | null {
  if (reset.length === 0) return null;
  const parts = reset.map(({ kind, name }) => `${KIND_WORDS[kind]} ${name}`);
  return `Выбор сброшен: ${parts.join(', ')}.`;
}

/**
 * The most specific chosen object (ADR-0032 § 1): project, else team, else department; the
 * others ride in the query. `null`: nothing is chosen.
 */
export function primaryKind(selection: ObjectSelection): SelectorKind | null {
  if (selection.project !== null) return 'project';
  if (selection.team !== null) return 'team';
  if (selection.department !== null) return 'department';
  return null;
}
