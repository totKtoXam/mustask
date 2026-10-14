import { ApiError } from '../../../shared/api/httpClient';
import { organizationErrorCodes as codes } from '../../../shared/api/errorCodes';
import { apiErrorText } from '../../../shared/lib/apiErrorText';
import { notFoundLevel } from './notFoundLevel';

/**
 * The surfaces a membership write can be refused on (mod-016 design § 9):
 * «Добавление участника», «Изменение роли», the two removal confirmations,
 * «Добавление в команду», and «Исключить из команды», which has no dialog.
 */
export type MembershipSurface = 'add' | 'role' | 'remove' | 'teamAdd' | 'teamRemove';

/** Where a refusal shows, and what is read again (design § 9). */
export interface PlacedMembershipRefusal {
  /**
   * A refusal about the surface's one field — the user, or «Роль» — as the
   * field's helper text in the error colour; the chosen value stays so the
   * administrator can pick another.
   */
  field: string | null;
  /** Any other refusal of a dialog: the dialog's error `Alert`. */
  alert: string | null;
  /** The surface has no dialog: the error `Snackbar` of the slice's host. */
  notice: string | null;
  /** The list on screen is read again — at once or in the background. */
  reread: boolean;
  /**
   * A project or team of a found organization is gone: the page refetches its
   * own entity and shows its frame (mod-002 § 12).
   */
  contextGone: boolean;
}

const NOTHING: PlacedMembershipRefusal = {
  field: null,
  alert: null,
  notice: null,
  reread: false,
  contextGone: false,
};

/** The code → surface → place table of design § 9; a missing cell is the surface's alert. */
const FIELD_OF_CODE: Readonly<Record<string, { surface: MembershipSurface; reread: boolean }[]>> = {
  [codes.membershipAlreadyMember]: [{ surface: 'add', reread: true }],
  [codes.membershipUserUnavailable]: [{ surface: 'add', reread: false }],
  [codes.membershipLastAdmin]: [{ surface: 'role', reread: false }],
  [codes.teamMemberExists]: [{ surface: 'teamAdd', reread: true }],
  [codes.teamMemberNotInOrganization]: [{ surface: 'teamAdd', reread: false }],
};

/** Codes whose refusal re-reads the list, wherever they land. */
const REREAD_SURFACES: Readonly<Record<string, readonly MembershipSurface[]>> = {
  [codes.membershipNotFound]: ['role', 'remove'],
  [codes.teamMemberNotFound]: ['teamRemove'],
};

function isDialog(surface: MembershipSurface): boolean {
  return surface !== 'teamRemove';
}

/**
 * Places a membership write's refusal by its `errorCode` alone, never by its
 * text (frontend.md "Named values"); the text is always the server's
 * (`apiErrorText`). `organizations.organization.not_found` is placed nowhere:
 * the whole page is already «Организация не найдена» and a dialog open on it
 * has gone with it (design § 2.2, DC-12). `organizations.access.forbidden`
 * lands where the surface shows its refusals; the watcher re-reads the codes
 * on its own.
 */
export function placeMembershipRefusal(
  error: unknown,
  surface: MembershipSurface,
): PlacedMembershipRefusal {
  const code = error instanceof ApiError ? error.errorCode : undefined;
  if (code === codes.notFound) {
    return NOTHING;
  }

  const text = apiErrorText(error);
  const fieldRule =
    code === undefined ? undefined : FIELD_OF_CODE[code]?.find((cell) => cell.surface === surface);
  if (fieldRule) {
    return { ...NOTHING, field: text, reread: fieldRule.reread };
  }

  const reread = code !== undefined && (REREAD_SURFACES[code]?.includes(surface) ?? false);
  const contextGone = notFoundLevel(error) !== null;
  return isDialog(surface)
    ? { ...NOTHING, alert: text, reread, contextGone }
    : { ...NOTHING, notice: text, reread, contextGone };
}
