import type { SelectorOffers } from '../api/navigationApi';
import type { AreaNode } from '../api/organizationsApi';
import { TREE_EXPAND_ALL_LIMIT, structureDiagram as texts } from './areaTexts';

/**
 * The diagram's data model (feat-049 design §§ 4.1, 4.5, 4.7): the area tree and the selector
 * offers, with a team drawn once, inside the department it stands in (mod-021, BR-TD-1). Pure functions, no
 * DOM; the layout and the page read this.
 */

export type SectionKey = 'outside' | 'departments';
export const SECTION_KEYS: readonly SectionKey[] = ['outside', 'departments'];

export type DiagramKind = 'department' | 'project' | 'area' | 'team';

export interface DiagramNode {
  /** The area's id, or `team:<teamId>@<departmentId | ->` for a team copy. */
  id: string;
  kind: DiagramKind;
  /** The area's id, or the team's id: the thing a user chooses (all copies of a team share it). */
  baseId: string;
  parentId: string | null;
  name: string;
  /** `false`: the node of a deactivated project or one of its sub-areas. */
  active: boolean;
  /** A project's key, from the offers; `null` without an offer. */
  key: string | null;
  /** The project of a project node or a sub-area. */
  projectId: string | null;
  section: SectionKey;
}

export interface DiagramModel {
  nodes: ReadonlyMap<string, DiagramNode>;
  /** Children by parent node id, in drawing order: projects and sub-areas first, then team copies. */
  childrenOf: ReadonlyMap<string, readonly DiagramNode[]>;
  roots: Readonly<Record<SectionKey, readonly DiagramNode[]>>;
  hasOffers: boolean;
  teamNames: ReadonlyMap<string, string>;
  /** Copies of a team by team id, in the order of its department links. */
  copies: ReadonlyMap<string, readonly DiagramNode[]>;
  /** Project–team pairs: the project's node id and the team id. */
  pairs: readonly { project: string; team: string }[];
  teamsOfProject: ReadonlyMap<string, readonly string[]>;
  projectsOfTeam: ReadonlyMap<string, readonly string[]>;
  departmentsOfTeam: ReadonlyMap<string, readonly string[]>;
  teamsOfDepartment: ReadonlyMap<string, readonly string[]>;
  /** Project–team pairs plus the joins of consecutive copies (BR-SD-17). */
  lineTotal: number;
  /** The deepest level of the whole tree: the number of chips. */
  maxLevel: number;
  sectionDepth: Readonly<Record<SectionKey, number>>;
  counts: { departments: number; projects: number; areas: number; teams: number | null };
}

function push<K, V>(map: Map<K, V[]>, key: K, value: V) {
  const list = map.get(key);
  if (list) list.push(value);
  else map.set(key, [value]);
}

export function buildModel(
  areas: readonly AreaNode[],
  offers: SelectorOffers | null,
): DiagramModel {
  const nodes = new Map<string, DiagramNode>();
  const childrenOf = new Map<string, DiagramNode[]>();
  const keyOfProject = new Map((offers?.projects ?? []).map((p) => [p.id, p.key]));

  // Sections follow the top-level ancestor: a department makes its subtree «departments».
  const sectionOf = new Map<string, SectionKey>();
  for (const area of areas) {
    const inherited = area.parentId === null ? undefined : sectionOf.get(area.parentId);
    sectionOf.set(area.id, inherited ?? (area.type === 'department' ? 'departments' : 'outside'));
  }
  for (const area of areas) {
    const node: DiagramNode = {
      id: area.id,
      kind: area.type,
      baseId: area.id,
      parentId: area.parentId,
      name: area.name,
      active: area.projectActive !== false,
      key:
        area.type === 'project' && area.projectId
          ? (keyOfProject.get(area.projectId) ?? null)
          : null,
      projectId: area.projectId,
      section: sectionOf.get(area.id) ?? 'outside',
    };
    nodes.set(node.id, node);
    if (area.parentId !== null) push(childrenOf, area.parentId, node);
  }
  const roots: Record<SectionKey, DiagramNode[]> = { outside: [], departments: [] };
  for (const area of areas) {
    if (area.parentId === null) {
      const node = nodes.get(area.id)!;
      roots[node.section].push(node);
    }
  }

  const teamNames = new Map<string, string>();
  const copies = new Map<string, DiagramNode[]>();
  const departmentsOfTeam = new Map<string, string[]>();
  const teamsOfDepartment = new Map<string, string[]>();
  for (const team of offers?.teams ?? []) {
    teamNames.set(team.id, team.name);
    // A place that is missing from the tree leaves the team outside any department (BR-SD-16).
    const departments =
      nodes.get(team.departmentId)?.kind === 'department' ? [team.departmentId] : [];
    departmentsOfTeam.set(team.id, departments);
    for (const department of departments.length > 0 ? departments : [null]) {
      const copy: DiagramNode = {
        id: `team:${team.id}@${department ?? '-'}`,
        kind: 'team',
        baseId: team.id,
        parentId: department,
        name: team.name,
        active: true,
        key: null,
        projectId: null,
        section: department === null ? 'outside' : 'departments',
      };
      nodes.set(copy.id, copy);
      push(copies, team.id, copy);
      if (department === null) {
        roots.outside.push(copy);
      } else {
        push(childrenOf, department, copy);
        push(teamsOfDepartment, department, team.id);
      }
    }
  }

  const pairs: { project: string; team: string }[] = [];
  const teamsOfProject = new Map<string, string[]>();
  const projectsOfTeam = new Map<string, string[]>();
  if (offers) {
    const nodeOfProject = new Map(
      areas.filter((a) => a.type === 'project' && a.projectId).map((a) => [a.projectId!, a]),
    );
    for (const project of offers.projects) {
      const node = nodeOfProject.get(project.id);
      if (!node || node.projectActive === false) continue;
      for (const teamId of project.teamIds) {
        if (!teamNames.has(teamId)) continue;
        pairs.push({ project: node.id, team: teamId });
        push(teamsOfProject, node.id, teamId);
        push(projectsOfTeam, teamId, node.id);
      }
    }
  }
  let joins = 0;
  for (const list of copies.values()) joins += list.length - 1;

  const sectionDepth: Record<SectionKey, number> = { outside: 0, departments: 0 };
  for (const key of SECTION_KEYS) {
    const walk = (list: readonly DiagramNode[], depth: number) => {
      for (const node of list) {
        sectionDepth[key] = Math.max(sectionDepth[key], depth);
        walk(childrenOf.get(node.id) ?? [], depth + 1);
      }
    };
    walk(roots[key], 1);
  }

  return {
    nodes,
    childrenOf,
    roots,
    hasOffers: offers !== null,
    teamNames,
    copies,
    pairs,
    teamsOfProject,
    projectsOfTeam,
    departmentsOfTeam,
    teamsOfDepartment,
    lineTotal: pairs.length + joins,
    maxLevel: Math.max(1, sectionDepth.outside, sectionDepth.departments),
    sectionDepth,
    counts: {
      departments: areas.filter((a) => a.type === 'department').length,
      projects: areas.filter((a) => a.type === 'project').length,
      areas: areas.filter((a) => a.type === 'area').length,
      teams: offers ? offers.teams.length : null,
    },
  };
}

export function isTeam(model: DiagramModel, baseId: string): boolean {
  return model.teamNames.has(baseId);
}

/** Choosable: a department, an active project, a team (BR-SD-10). */
export function isChoosable(model: DiagramModel, baseId: string): boolean {
  if (isTeam(model, baseId)) return true;
  const node = model.nodes.get(baseId);
  return !!node && (node.kind === 'department' || (node.kind === 'project' && node.active));
}

/** The name of an area or a team by its base id. */
export function nameOfBase(model: DiagramModel, baseId: string): string {
  return model.teamNames.get(baseId) ?? model.nodes.get(baseId)?.name ?? '';
}

/** Ancestors of a node, nearest first. */
export function ancestorsOf(model: DiagramModel, id: string): string[] {
  const result: string[] = [];
  let current = model.nodes.get(id);
  while (current?.parentId) {
    result.push(current.parentId);
    current = model.nodes.get(current.parentId);
  }
  return result;
}

/** The node ids a base id is drawn as: its team copies, or the area itself. */
export function nodeIdsOfBase(model: DiagramModel, baseId: string): string[] {
  const copies = model.copies.get(baseId);
  return copies ? copies.map((c) => c.id) : model.nodes.has(baseId) ? [baseId] : [];
}

export interface Emphasis {
  /** Every emphasised base id, the chosen one included. */
  ids: ReadonlySet<string>;
  /** Keys of emphasised lines: `<projectNodeId>><teamId>` and `copy:<teamId>`. */
  lines: ReadonlySet<string>;
  /** The branch's areas, and its teams. */
  branchAreas: ReadonlySet<string>;
  branchTeams: ReadonlySet<string>;
}

function descendantAreas(model: DiagramModel, id: string, out: Set<string>) {
  for (const child of model.childrenOf.get(id) ?? []) {
    if (child.kind !== 'team') {
      out.add(child.id);
      descendantAreas(model, child.id, out);
    }
  }
}

/** Choosing emphasises the whole branch (BR-SD-18). */
export function emphasisOf(model: DiagramModel, baseId: string): Emphasis {
  const ids = new Set<string>();
  const lines = new Set<string>();
  const branchAreas = new Set<string>();
  const branchTeams = new Set<string>();
  if (isTeam(model, baseId)) {
    branchTeams.add(baseId);
    for (const department of model.departmentsOfTeam.get(baseId) ?? []) ids.add(department);
  } else {
    branchAreas.add(baseId);
    descendantAreas(model, baseId, branchAreas);
    for (const id of branchAreas) {
      if (model.nodes.get(id)?.kind === 'department') {
        for (const team of model.teamsOfDepartment.get(id) ?? []) branchTeams.add(team);
      }
    }
  }
  branchAreas.forEach((id) => ids.add(id));
  branchTeams.forEach((id) => ids.add(id));
  for (const pair of model.pairs) {
    if (branchAreas.has(pair.project) || branchTeams.has(pair.team)) {
      ids.add(pair.project);
      ids.add(pair.team);
      lines.add(`${pair.project}>${pair.team}`);
    }
  }
  for (const id of ids) if (isTeam(model, id)) lines.add(`copy:${id}`);
  return { ids, lines, branchAreas, branchTeams };
}

function names(model: DiagramModel, ids: readonly string[]): string {
  return ids.length > 0 ? ids.map((id) => nameOfBase(model, id)).join(', ') : texts.summary.none;
}

/** The announced summary of a chosen node (design § 4.7). */
export function summaryText(model: DiagramModel, baseId: string): string {
  if (isTeam(model, baseId)) {
    return texts.summary.team(
      names(model, model.projectsOfTeam.get(baseId) ?? []),
      names(model, model.departmentsOfTeam.get(baseId) ?? []),
    );
  }
  const emphasis = emphasisOf(model, baseId);
  let projects = 0;
  let areas = 0;
  for (const id of emphasis.branchAreas) {
    const kind = model.nodes.get(id)?.kind;
    if (kind === 'project') projects += 1;
    else if (kind === 'area') areas += 1;
  }
  if (model.nodes.get(baseId)?.kind === 'department') {
    const outside = [...emphasis.ids].filter(
      (id) => !emphasis.branchAreas.has(id) && !emphasis.branchTeams.has(id),
    );
    return texts.summary.department(
      projects,
      areas,
      emphasis.branchTeams.size,
      names(model, outside),
    );
  }
  return texts.summary.project(areas, names(model, model.teamsOfProject.get(baseId) ?? []));
}

export interface RelatedFilter {
  /** Kept area node ids (ancestors included). */
  keep: ReadonlySet<string>;
  /** Kept team ids. */
  teams: ReadonlySet<string>;
}

/** «Только связанные»: the branch, what it relates to, their ancestors and related teams' departments. */
export function relatedFilter(model: DiagramModel, emphasis: Emphasis): RelatedFilter {
  const keep = new Set<string>();
  const teams = new Set<string>();
  for (const id of emphasis.ids) {
    if (isTeam(model, id)) {
      teams.add(id);
      for (const department of model.departmentsOfTeam.get(id) ?? []) keep.add(department);
    } else {
      keep.add(id);
      ancestorsOf(model, id).forEach((a) => keep.add(a));
    }
  }
  return { keep, teams };
}

function fold(text: string): string {
  return text.toLowerCase().replace(/ё/g, 'е');
}

/** Base ids whose name contains the text, case and «ё/е» ignored (BR-SD-8): areas, then teams. */
export function searchMatches(model: DiagramModel, text: string): string[] {
  const needle = fold(text.trim());
  if (needle === '') return [];
  const found: string[] = [];
  for (const node of model.nodes.values()) {
    if (node.kind !== 'team' && fold(node.name).includes(needle)) found.push(node.id);
  }
  for (const [id, name] of model.teamNames) if (fold(name).includes(needle)) found.push(id);
  return found;
}

/** «ещё в: …» of a team copy, or `null` when the team stands in one place only. */
export function alsoInText(model: DiagramModel, node: DiagramNode): string | null {
  if (node.kind !== 'team') return null;
  const others = (model.departmentsOfTeam.get(node.baseId) ?? [])
    .filter((id) => id !== node.parentId)
    .map((id) => model.nodes.get(id)?.name ?? '');
  return others.length > 0 ? texts.alsoIn(others.join(', ')) : null;
}

/** The accessible name of a card: type, name, parent, relation count (design § 7). */
export function cardName(model: DiagramModel, node: DiagramNode): string {
  const parent = node.parentId ? (model.nodes.get(node.parentId) ?? null) : null;
  switch (node.kind) {
    case 'department':
      return texts.names.department(
        node.name,
        model.hasOffers ? (model.teamsOfDepartment.get(node.id) ?? []).length : null,
      );
    case 'project':
      return texts.names.project(
        node.name,
        node.key,
        node.active,
        parent?.kind === 'department' ? parent.name : null,
        model.hasOffers ? (model.teamsOfProject.get(node.id) ?? []).length : null,
      );
    case 'area':
      return texts.names.area(node.name, parent?.name ?? '', node.active);
    case 'team':
      return texts.names.team(
        node.name,
        (model.projectsOfTeam.get(node.baseId) ?? []).length,
        parent?.name ?? null,
        alsoInText(model, node),
      );
  }
}

/** Every node with children, team copies counted: «Развернуть всё». */
export function expandableNodeIds(model: DiagramModel): Set<string> {
  return new Set(
    [...model.childrenOf.entries()].filter(([, kids]) => kids.length > 0).map(([id]) => id),
  );
}

/** The diagram's initial expansion follows the tree's D-3 (BR-SD-11). */
export function initialDiagramExpansion(model: DiagramModel): Set<string> {
  const areas = [...model.nodes.values()].filter((node) => node.kind !== 'team').length;
  return areas > TREE_EXPAND_ALL_LIMIT ? new Set() : expandableNodeIds(model);
}
