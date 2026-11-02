/** The separator of an area path, as the server writes it. */
const SEPARATOR = ' / ';

interface PathNode {
  type: string;
  name: string;
  path: string;
}

/**
 * mod-018 design § 8, BR-AW-13: an item's area is named from its project down («Alpha / Платформа»,
 * «Alpha» for the project's node), never with the department above the project. The project subtree
 * read answers the organization tree's full paths, so the part above the project's node is cut off.
 */
export function projectRelativePath(nodes: readonly PathNode[], node: PathNode): string {
  const root = nodes.find((candidate) => candidate.type === 'project');
  if (!root) {
    return node.path;
  }
  const above = root.path.slice(0, root.path.length - root.name.length);
  return above !== '' && above.endsWith(SEPARATOR) && node.path.startsWith(above)
    ? node.path.slice(above.length)
    : node.path;
}
