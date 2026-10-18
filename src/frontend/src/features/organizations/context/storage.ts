import type { WorkContextSelection } from '../../../shared/lib/workContext';

/**
 * The remembered selection (design § 3.4): `localStorage` under
 * `mustask.context.<sub>`, where `<sub>` is the token's subject, as
 * `{"organizationId":"…","projectId":"…","teamId":"…"}`. Without a subject,
 * without storage (a private window with storage blocked) or with a value
 * that does not parse, nothing is remembered and nothing fails.
 */
const KEY_PREFIX = 'mustask.context.';

export function storageKey(subject: string): string {
  return `${KEY_PREFIX}${subject}`;
}

function text(value: unknown): string | undefined {
  return typeof value === 'string' && value !== '' ? value : undefined;
}

export function readRemembered(subject: string | undefined): WorkContextSelection {
  if (!subject) {
    return {};
  }
  try {
    const raw = window.localStorage.getItem(storageKey(subject));
    if (raw === null) {
      return {};
    }
    const value: unknown = JSON.parse(raw);
    if (typeof value !== 'object' || value === null || Array.isArray(value)) {
      return {};
    }
    const record = value as Record<string, unknown>;
    const organizationId = text(record.organizationId);
    if (!organizationId) {
      return {};
    }
    const projectId = text(record.projectId);
    // The team is the organization's, whatever its projects (feat-040 BR-TS-10); only a
    // chosen team is written (see the hook).
    const teamId = text(record.teamId);
    return { organizationId, projectId, teamId };
  } catch {
    return {};
  }
}

export function writeRemembered(
  subject: string | undefined,
  selection: WorkContextSelection,
): void {
  if (!subject) {
    return;
  }
  try {
    window.localStorage.setItem(
      storageKey(subject),
      JSON.stringify({
        organizationId: selection.organizationId,
        projectId: selection.projectId,
        teamId: selection.teamId,
      }),
    );
  } catch {
    // Storage blocked or full: nothing is remembered, and nothing fails.
  }
}
