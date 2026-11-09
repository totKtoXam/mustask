import { roleBundles } from '../shared/lib/roleBundles';

/**
 * The BR-SA-18 guard's scan (mod-019): which source files name a code that is decided at a narrower scope
 * than the organization (a project's work-item codes, a team's reorder, a department's write...), and
 * whether each one says where it reads that code from. The organization answer carries organization-scope
 * codes only, so a surface that asks it for any other code stops working the day the server serves that
 * answer by the contract. A pure function over sources, so a test can feed it a surface that breaks the
 * rule and see it fail.
 */

/** A code the organization answer does not carry: the catalog's, minus instance codes and organization scope. */
export const NARROWER_CODES: ReadonlySet<string> = new Set(
  roleBundles.catalog
    .filter((entry) => entry.source !== 'instance' && entry.resourceScope !== 'organization')
    .map((entry) => entry.code),
);

/** The permission-constant objects of the slices, by the name the sources import them under. */
export type PermissionConstants = Readonly<Record<string, Readonly<Record<string, string>>>>;

/** What one surface owes: the marker, in the file named, that proves the object's codes are read. */
export interface Source {
  readonly file: string;
  readonly marker: string;
}

export interface SurfaceEntry {
  /** What the surface asks the object's codes through, in its own file or in the host that mounts it. */
  readonly via: readonly Source[];
  readonly why: string;
}

/** The narrower codes a source names: by literal, by a permission constant, or by an alias of one. */
export function narrowerCodesIn(text: string, constants: PermissionConstants): string[] {
  const found = new Set<string>();
  for (const match of text.matchAll(/'((?:[a-z]+\.){2,}[a-z]+)'/g)) {
    const code = match[1];
    if (code !== undefined && NARROWER_CODES.has(code)) {
      found.add(code);
    }
  }
  const aliases = new Map<string, string>();
  for (const match of text.matchAll(/const (\w+) = (\w+);/g)) {
    const [, alias, target] = match;
    if (alias !== undefined && target !== undefined && constants[target] !== undefined) {
      aliases.set(alias, target);
    }
  }
  for (const match of text.matchAll(/\b(\w+)\.(\w+)\b/g)) {
    const [, object, member] = match;
    if (object === undefined || member === undefined) {
      continue;
    }
    const code = constants[aliases.get(object) ?? object]?.[member];
    if (code !== undefined && NARROWER_CODES.has(code)) {
      found.add(code);
    }
  }
  return [...found].sort();
}

export interface Finding {
  file: string;
  message: string;
}

/**
 * The surfaces that break the rule: a file that names a narrower code and is neither a definition nor a
 * registered surface; a registered surface whose marker is missing from the file it names; a registered
 * surface that no longer names a narrower code (the entry is stale and goes).
 */
export function findViolations(
  sources: ReadonlyMap<string, string>,
  constants: PermissionConstants,
  surfaces: Readonly<Record<string, SurfaceEntry>>,
  definitions: readonly string[],
): Finding[] {
  const findings: Finding[] = [];
  for (const [file, text] of sources) {
    if (definitions.includes(file)) {
      continue;
    }
    const codes = narrowerCodesIn(text, constants);
    const entry = surfaces[file];
    if (codes.length > 0 && entry === undefined) {
      findings.push({
        file,
        message: `names ${codes.join(', ')}, which the organization answer does not carry: read them from the object's codes (useObjectAccess, ProjectAccess, useProjectCodes...) and register the surface`,
      });
    }
    if (codes.length === 0 && entry !== undefined) {
      findings.push({
        file,
        message: 'is registered as a surface but names no narrower code: drop the entry',
      });
    }
  }
  for (const [file, entry] of Object.entries(surfaces)) {
    for (const via of entry.via) {
      const text = sources.get(via.file);
      if (text === undefined || !text.includes(via.marker)) {
        findings.push({
          file,
          message: `owes \`${via.marker}\` in ${via.file}: the object's codes are not read there`,
        });
      }
    }
  }
  return findings;
}
