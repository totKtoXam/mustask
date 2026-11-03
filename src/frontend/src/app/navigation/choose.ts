import { matchPath } from 'react-router-dom';
import {
  LANDING_LEVEL_SEGMENT,
  menuFor,
  rowRoute,
  rowTakesLevel,
  type MenuKind,
  type MenuRow,
} from './addressModel';
import { menuHref, menuKindOf } from './selection';
import type { ObjectSelection } from './selectorFit';

/** What the target of a choice needs of the page and of the user. */
export interface ChoiceRequest {
  organizationId: string;
  /** The address on screen. */
  pathname: string;
  /** The selection before the choice and after it. */
  from: ObjectSelection;
  to: ObjectSelection;
  /** The shared check, asked about the organization: an entry the user may not open is absent. */
  canOpen: (code: string) => boolean;
}

/** The row of `kind`'s menu the address is the page of, with the level it carries. */
function currentRow(
  kind: MenuKind,
  pathname: string,
): { row: MenuRow; level: string | undefined } | null {
  for (const row of menuFor(kind)) {
    const match = matchPath({ path: rowRoute(kind, row), end: true }, pathname);
    if (match) {
      const level = match.params.level;
      return { row, level };
    }
  }
  return null;
}

const SEGMENT_OF = (row: MenuRow) => row.segment;

/**
 * Where a choice in a selector moves the page (ADR-0032 § 5; design § 3): the current entry
 * stays when the new menu has it — the same segment, the same level, the old scope's filters
 * dropped — else the new menu's first entry the user may open. A menu with no entry the user
 * may open lands on the organization's «Обзор».
 */
export function targetAfterChoice({
  organizationId,
  pathname,
  from,
  to,
  canOpen,
}: ChoiceRequest): string {
  const rows = menuFor(menuKindOf(to)).filter((row) => row.id !== 'structure' && canOpen(row.code));
  const current = currentRow(menuKindOf(from), pathname);
  const kept = current && rows.find((row) => SEGMENT_OF(row) === SEGMENT_OF(current.row));
  const row = kept ?? rows[0];
  if (!row) {
    return menuHref(organizationId, to, { segment: 'overview' });
  }
  if (kept && rowTakesLevel(kept) && current?.level) {
    return menuHref(organizationId, to, {
      segment: kept.segment.replace(':level', current.level),
    });
  }
  return menuHref(organizationId, to, {
    segment: row.segment.replace(':level', LANDING_LEVEL_SEGMENT),
  });
}
