import type { AreaNode, Project, Team } from '../api/organizationsApi';

/**
 * «Структура» filter (doc-016 BR-TD-21, feat-061): the nodes the typed text finds, over the tree the
 * server already narrowed to the caller's sight, so a hidden object is never among the candidates.
 * Case ignored, from the first character. It matches a node's name, a project node's key and the
 * name of a team of a department (a team is no row of this tree: its department stands for it).
 * A match keeps its ancestors; a matching department keeps all its descendants. An empty text keeps
 * everything.
 */
export function filterStructure(
  areas: readonly AreaNode[],
  text: string,
  known: {
    projects?: readonly Pick<Project, 'id' | 'key'>[];
    teams?: readonly Pick<Team, 'name' | 'departmentId'>[];
  } = {},
): AreaNode[] {
  const needle = text.trim().toLowerCase();
  if (needle === '') {
    return [...areas];
  }
  const keys = new Map((known.projects ?? []).map((project) => [project.id, project.key]));
  const teamDepartments = new Set(
    (known.teams ?? [])
      .filter((team) => team.name.toLowerCase().includes(needle))
      .map((team) => team.departmentId),
  );
  const byId = new Map(areas.map((area) => [area.id, area]));
  const keep = new Set<string>();
  const keepWithAncestors = (area: AreaNode) => {
    let current: AreaNode | undefined = area;
    while (current && !keep.has(current.id)) {
      keep.add(current.id);
      current = current.parentId === null ? undefined : byId.get(current.parentId);
    }
  };
  const matchedDepartments = new Set<string>();
  for (const area of areas) {
    const key =
      area.projectId !== null && area.type === 'project' ? keys.get(area.projectId) : null;
    const named = area.name.toLowerCase().includes(needle);
    if (named || (key != null && key.toLowerCase().includes(needle))) {
      keepWithAncestors(area);
      if (named && area.type === 'department') matchedDepartments.add(area.id);
    }
    if (area.type === 'department' && teamDepartments.has(area.id)) keepWithAncestors(area);
  }
  // A matching department keeps its children: every node under one is kept.
  for (const area of areas) {
    let current: AreaNode | undefined = area;
    while (current) {
      if (matchedDepartments.has(current.id)) {
        keepWithAncestors(area);
        break;
      }
      current = current.parentId === null ? undefined : byId.get(current.parentId);
    }
  }
  return areas.filter((area) => keep.has(area.id));
}
