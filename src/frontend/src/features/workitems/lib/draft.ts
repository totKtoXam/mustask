import type {
  AcceptanceCriterion,
  SaveWorkItemBody,
  WorkItem,
  WorkItemAssignee,
  WorkItemFieldName,
  WorkItemRef,
  WorkItemTeam,
} from '../api/workItemsApi';
import { WORK_ITEM_FIELD_NAMES } from './fields';
import {
  changedFields,
  fieldChanges,
  fieldErrors,
  fieldsDraftOf,
  type FieldsDraft as FieldValuesDraft,
} from './fieldValues';
import type { RefusalField } from './refusals';
import { descriptionError, titleError } from './rules';

/** A scenario item of the acceptance criteria draft */
export interface DraftCriterion {
  id: string;
  given: string;
  when: string;
  then: string;
}

/**
 * What the form edits for the fields per type.
 * acceptanceCriteria holds structured DraftCriterion items for User Story.
 */
export type FieldsDraft = {
  [K in Exclude<WorkItemFieldName, 'acceptanceCriteria'>]: string;
} & {
  acceptanceCriteria: DraftCriterion[];
};

let criterionIdCounter = 0;

export function createCriterionDraft(
  criterion?: Partial<AcceptanceCriterion>,
  id?: string,
): DraftCriterion {
  criterionIdCounter += 1;
  const unique =
    typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function'
      ? crypto.randomUUID()
      : Math.random().toString(36).slice(2, 9);
  return {
    id: id ?? `ac-${criterionIdCounter}-${unique}`,
    given: criterion?.given ?? '',
    when: criterion?.when ?? '',
    then: criterion?.then ?? '',
  };
}

/**
 * What the form edits (spec § Page: a work item's form): the six values, as
 * typed or picked, and feat-022's fields per type as their controls hold them.
 */
export interface WorkItemDraft {
  title: string;
  description: string;
  stateId: string;
  /** `null`: «Не назначен». */
  assignee: WorkItemAssignee | null;
  /** mod-017: `null` is «Без команды». */
  team: WorkItemTeam | null;
  /** feat-001 BR-H-6: `null` is «Нет родителя». */
  parent: WorkItemRef | null;
  /** feat-022: all 17, those the type lacks never sent. */
  fields: FieldsDraft;
}

/** The form's values as the item was loaded or last saved. */
export function draftOf(item: WorkItem): WorkItemDraft {
  const baseFields = fieldsDraftOf(item);
  const prefix = item.identifier || item.id || 'item';
  const acceptanceCriteria: DraftCriterion[] = (item.acceptanceCriteria ?? []).map((c, index) =>
    createCriterionDraft(c, `${prefix}-ac-${index}`),
  );

  return {
    title: item.title,
    description: item.description,
    stateId: item.state.id,
    assignee: item.assignee,
    team: item.team ?? null,
    parent: item.parent,
    fields: {
      ...(baseFields as unknown as Record<string, string>),
      acceptanceCriteria,
    } as FieldsDraft,
  };
}

/** The draft's value of each field as the save compares it: an id where the field names one. */
function comparable(
  draft: WorkItemDraft,
): Record<Exclude<keyof WorkItemDraft, 'fields'>, string | null> {
  return {
    title: draft.title,
    description: draft.description,
    stateId: draft.stateId,
    assignee: draft.assignee?.id ?? null,
    team: draft.team?.id ?? null,
    parent: draft.parent?.id ?? null,
  };
}

/**
 * The save's body: exactly the fields whose value differs from the loaded one
 * (BR-M3-25; api-contract § 2.4), so a field the user did not change keeps
 * whatever is stored when the save lands. A value changed and changed back is
 * no change; the current state picked again is no change (US-5). The parent
 * likewise (feat-001 § 3.3): sent only when another one, or none, is chosen,
 * with the loaded parent as the one the user saw (BR-H-4).
 */
export function criteriaDiffer(
  stored: readonly AcceptanceCriterion[] | null,
  draftCriteria: readonly DraftCriterion[],
): boolean {
  const original = stored ?? [];
  if (draftCriteria.length !== original.length) {
    return true;
  }
  return draftCriteria.some((c, i) => {
    const o = original[i];
    if (!o) {
      return true;
    }
    return c.given !== o.given || c.when !== o.when || c.then !== o.then;
  });
}

/**
 * The save's body: exactly the fields whose value differs from the loaded one
 * (BR-M3-25; api-contract § 2.4), so a field the user did not change keeps
 * whatever is stored when the save lands. A value changed and changed back is
 * no change; the current state picked again is no change (US-5). The parent
 * likewise (feat-001 § 3.3): sent only when another one, or none, is chosen,
 * with the loaded parent as the one the user saw (BR-H-4).
 */
export function changesOf(item: WorkItem, draft: WorkItemDraft): SaveWorkItemBody {
  const body: SaveWorkItemBody = {};
  if (draft.title !== item.title) {
    body.title = draft.title;
  }
  if (draft.description !== item.description) {
    body.description = draft.description;
  }
  if (draft.stateId !== item.state.id) {
    body.stateId = draft.stateId;
  }
  const assigneeId = draft.assignee?.id ?? null;
  if (assigneeId !== (item.assignee?.id ?? null)) {
    // `{ userId: null }` clears it; an absent `assignee` would keep it.
    body.assignee = { userId: assigneeId };
  }
  const teamId = draft.team?.id ?? null;
  if (teamId !== (item.team?.id ?? null)) {
    // mod-017 api-contract § 0.2: `{ teamId: null }` is «Без команды»; an absent `team` keeps the team.
    body.team = { teamId };
  }
  const parentId = draft.parent?.id ?? null;
  const seenParentId = item.parent?.id ?? null;
  if (parentId !== seenParentId) {
    // `parentId: null` clears it; an absent `parent` would keep it.
    body.parent = { parentId, seenParentId };
  }
  // feat-022 § 3.2: the type's fields that changed, by the same rule.
  const fields = fieldChanges(item, draft.fields as unknown as FieldValuesDraft);

  const isStory =
    item.type.appearance === 'story' ||
    item.type.key === 'US' ||
    (item as { typeKey?: string }).typeKey === 'US';
  if (isStory && criteriaDiffer(item.acceptanceCriteria, draft.fields.acceptanceCriteria)) {
    body.acceptanceCriteria = draft.fields.acceptanceCriteria.map(({ given, when, then }) => ({
      given,
      when,
      then,
    }));
  }

  return { ...body, ...fields };
}

/** Whether a save body carries anything. */
export function hasChanges(body: SaveWorkItemBody): boolean {
  return Object.keys(body).length > 0;
}

/**
 * «Сохранить» is enabled only while this is true (spec § Page: a work item's
 * form; feat-022 spec § Saving): a value differs from the loaded one — a
 * number field's text that is not a number, or a date typed in part, counts,
 * so its check can speak.
 */
export function isDirty(item: WorkItem, draft: WorkItemDraft): boolean {
  const isStory =
    item.type.appearance === 'story' ||
    item.type.key === 'US' ||
    (item as { typeKey?: string }).typeKey === 'US';
  const criteriaChanged =
    isStory && criteriaDiffer(item.acceptanceCriteria, draft.fields.acceptanceCriteria);

  const otherFieldsChanged =
    changedFields(item, draft.fields as unknown as FieldValuesDraft).filter(
      (name) => name !== 'acceptanceCriteria',
    ).length > 0;

  return hasChanges(changesOf(item, draft)) || criteriaChanged || otherFieldsChanged;
}

/**
 * The browser's checks of the changed values, run on «Сохранить», each under
 * its field: «Заголовок» and «Описание» (mod-003), then the fields per type
 * (feat-022 spec § Controls and copy). Nothing is sent while one stands.
 */
export function draftErrors(
  item: WorkItem,
  draft: WorkItemDraft,
): Partial<Record<RefusalField, string>> {
  const errors: Partial<Record<RefusalField, string>> = {};
  const title = draft.title !== item.title ? titleError(draft.title) : null;
  const description =
    draft.description !== item.description ? descriptionError(draft.description) : null;
  if (title) {
    errors.title = title;
  }
  if (description) {
    errors.description = description;
  }
  return { ...errors, ...fieldErrors(item, draft.fields as unknown as FieldValuesDraft) };
}

/**
 * The draft laid over a fresh read of the item (feat-001 spec § Screens and
 * copy, after `workitems.link.parent_changed`): a field the user changed keeps
 * their value, every other one takes the fresh read's, and «Родитель» takes the
 * stored parent — so the next save still sends only what the user touched.
 */
export function rebaseDraft(
  loaded: WorkItem,
  fresh: WorkItem,
  draft: WorkItemDraft,
): WorkItemDraft {
  const loadedDraft = draftOf(loaded);
  const before = comparable(loadedDraft);
  const now = comparable(draft);
  const touched = (field: Exclude<keyof WorkItemDraft, 'fields'>) => now[field] !== before[field];
  const next = draftOf(fresh);
  const fields = { ...next.fields };
  for (const name of WORK_ITEM_FIELD_NAMES) {
    if (name === 'acceptanceCriteria') {
      if (criteriaDiffer(loaded.acceptanceCriteria, draft.fields.acceptanceCriteria)) {
        fields.acceptanceCriteria = draft.fields.acceptanceCriteria;
      }
    } else {
      const fieldKey = name as Exclude<WorkItemFieldName, 'acceptanceCriteria'>;
      if (draft.fields[fieldKey] !== loadedDraft.fields[fieldKey]) {
        fields[fieldKey] = draft.fields[fieldKey];
      }
    }
  }
  return {
    title: touched('title') ? draft.title : next.title,
    description: touched('description') ? draft.description : next.description,
    stateId: touched('stateId') ? draft.stateId : next.stateId,
    assignee: touched('assignee') ? draft.assignee : next.assignee,
    team: touched('team') ? draft.team : next.team,
    parent: next.parent,
    fields,
  };
}
