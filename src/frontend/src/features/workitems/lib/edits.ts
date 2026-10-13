// The card's draft of touched fields only (feat-029 BR-C-16, tech-010's m1 for
// the card). The older form copied the whole item into its draft on the first
// edit, so a field the user never touched kept the value it was read with and a
// later read of the item could not reach it. Here the draft holds only what the
// user changed; every other field always shows the latest stored value. The
// comparisons, the checks and the save body are `draft.ts`'s, applied to the
// stored item with the touched fields laid over it.
import type {
  SaveWorkItemBody,
  WorkItem,
  WorkItemArea,
  WorkItemAssignee,
  WorkItemFieldName,
  WorkItemRef,
} from '../api/workItemsApi';
import { changesOf, draftOf, isDirty, type WorkItemDraft } from './draft';
import { fieldsDraftOf, type FieldsDraft } from './fieldValues';
import { WORK_ITEM_FIELD_NAMES, layoutOf } from './fields';
import type { RefusalField } from './refusals';

/**
 * A touched parent: the value, and the parent the user saw when they first
 * touched the field (feat-001 BR-H-4) — what the save sends as `seenParentId`,
 * so a parent changed by someone else meanwhile is refused, not overwritten.
 */
export interface ParentEdit {
  value: WorkItemRef | null;
  seenParentId: string | null;
}

/**
 * What the user touched. A key that is absent shows the stored value; a key
 * that is present is the user's, and `assignee: null` is «Не назначен» chosen.
 */
export interface WorkItemEdits {
  title?: string;
  description?: string;
  stateId?: string;
  assignee?: WorkItemAssignee | null;
  area?: WorkItemArea;
  parent?: ParentEdit;
  /** feat-022's fields per type, as their controls hold them. */
  fields?: Partial<FieldsDraft>;
}

/** One edit of one field, as a control reports it. */
export type Edit =
  | { field: 'title' | 'description'; value: string }
  | { field: 'state'; value: string }
  | { field: 'assignee'; value: WorkItemAssignee | null }
  | { field: 'area'; value: WorkItemArea }
  | { field: 'parent'; value: WorkItemRef | null }
  | { field: WorkItemFieldName; value: string };

export const NO_EDITS: WorkItemEdits = {};

/** Whether the user touched nothing. */
export function isUntouched(edits: WorkItemEdits): boolean {
  return Object.keys(edits).length === 0;
}

/** The form's values: the stored item with the touched fields laid over it. */
export function editedDraft(item: WorkItem, edits: WorkItemEdits): WorkItemDraft {
  const stored = draftOf(item);
  return {
    title: edits.title ?? stored.title,
    description: edits.description ?? stored.description,
    stateId: edits.stateId ?? stored.stateId,
    assignee: edits.assignee !== undefined ? edits.assignee : stored.assignee,
    area: edits.area ?? stored.area,
    parent: edits.parent !== undefined ? edits.parent.value : stored.parent,
    fields: { ...stored.fields, ...edits.fields },
  };
}

/**
 * The save's body: the touched fields that differ from the stored item, and
 * nothing else (BR-C-8, BR-M3-25). A touched parent is sent with the parent the
 * user saw as `seenParentId`, not the stored one now.
 */
export function touchedChanges(item: WorkItem, edits: WorkItemEdits): SaveWorkItemBody {
  const body = changesOf(item, editedDraft(item, edits));
  if (edits.parent !== undefined && body.parent !== undefined) {
    body.parent = { parentId: body.parent.parentId, seenParentId: edits.parent.seenParentId };
  }
  return body;
}

/** «Сохранить» and «Отменить все изменения» are enabled only while this is true. */
export function hasUnsavedEdits(item: WorkItem, edits: WorkItemEdits): boolean {
  return !isUntouched(edits) && isDirty(item, editedDraft(item, edits));
}

/** Sets `key`, or removes it when the value is the stored one again. */
function put<K extends keyof WorkItemEdits>(
  edits: WorkItemEdits,
  key: K,
  value: WorkItemEdits[K],
  touched: boolean,
): WorkItemEdits {
  const next = { ...edits };
  if (touched) {
    next[key] = value;
  } else {
    delete next[key];
  }
  return next;
}

function putField(
  item: WorkItem,
  edits: WorkItemEdits,
  name: WorkItemFieldName,
  value: string,
): WorkItemEdits {
  const fields = { ...edits.fields };
  if (value !== fieldsDraftOf(item)[name]) {
    fields[name] = value;
  } else {
    delete fields[name];
  }
  return put(edits, 'fields', fields, Object.keys(fields).length > 0);
}

/**
 * The edits after one more edit. A value changed back to the stored one is no
 * edit (BR-C-16), so the field follows the store again.
 */
export function withEdit(item: WorkItem, edits: WorkItemEdits, edit: Edit): WorkItemEdits {
  const stored = draftOf(item);
  switch (edit.field) {
    case 'title':
      return put(edits, 'title', edit.value, edit.value !== stored.title);
    case 'description':
      return put(edits, 'description', edit.value, edit.value !== stored.description);
    case 'state':
      return put(edits, 'stateId', edit.value, edit.value !== stored.stateId);
    case 'assignee':
      return put(
        edits,
        'assignee',
        edit.value,
        (edit.value?.id ?? null) !== (stored.assignee?.id ?? null),
      );
    case 'area':
      return put(edits, 'area', edit.value, edit.value.id !== stored.area.id);
    case 'parent': {
      const seenParentId =
        edits.parent !== undefined ? edits.parent.seenParentId : (stored.parent?.id ?? null);
      return put(
        edits,
        'parent',
        { value: edit.value, seenParentId },
        (edit.value?.id ?? null) !== (stored.parent?.id ?? null),
      );
    }
    default:
      return putField(item, edits, edit.field, edit.value);
  }
}

/** Drops every edit that equals what `item` stores. */
export function normaliseEdits(item: WorkItem, edits: WorkItemEdits): WorkItemEdits {
  let next: WorkItemEdits = {};
  const apply = (edit: Edit) => {
    next = withEdit(item, next, edit);
  };
  if (edits.title !== undefined) apply({ field: 'title', value: edits.title });
  if (edits.description !== undefined) apply({ field: 'description', value: edits.description });
  if (edits.stateId !== undefined) apply({ field: 'state', value: edits.stateId });
  if (edits.assignee !== undefined) apply({ field: 'assignee', value: edits.assignee });
  if (edits.area !== undefined) apply({ field: 'area', value: edits.area });
  for (const name of WORK_ITEM_FIELD_NAMES) {
    const value = edits.fields?.[name];
    if (value !== undefined) apply({ field: name, value });
  }
  if (
    edits.parent !== undefined &&
    (edits.parent.value?.id ?? null) !== (item.parent?.id ?? null)
  ) {
    // Kept as it was touched: the parent the user saw is not the stored one now.
    next = { ...next, parent: edits.parent };
  }
  return next;
}

/**
 * The edits over a fresh read (BR-C-9): a touched field keeps the user's value,
 * every other one takes the fresh read's — which needs no work, as an untouched
 * field is never held. An edit the fresh read now agrees with is dropped. With
 * `dropParent` the parent edit goes too: after `workitems.link.parent_changed`
 * «Родитель» shows the stored parent (feat-001 BR-H-4).
 */
export function rebaseEdits(
  fresh: WorkItem,
  edits: WorkItemEdits,
  options: { dropParent?: boolean } = {},
): WorkItemEdits {
  const kept = { ...edits };
  if (options.dropParent) {
    delete kept.parent;
  }
  return normaliseEdits(fresh, kept);
}

/** The fields of `appearance`'s card in the order the first error is looked for (design § 4.6). */
export function cardFieldOrder(appearance: string): readonly RefusalField[] {
  const layout = layoutOf(appearance);
  return [
    'title',
    'assignee',
    'state',
    'area',
    'description',
    ...layout.texts,
    ...layout.sections.flatMap((section) => section.fields),
    'parent',
  ];
}

/** The first field with an error, in the card's order — the one that takes focus. */
export function firstErrorField(
  errors: Partial<Record<RefusalField, string>>,
  appearance: string,
): RefusalField | undefined {
  return cardFieldOrder(appearance).find((field) => errors[field] !== undefined);
}

/** The router-state key the dialog's «Развернуть на страницу» carries its edits in. */
export const CARRIED_EDITS_KEY = 'edits';

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null;

const hasId = (value: unknown): boolean => isRecord(value) && typeof value.id === 'string';

/**
 * The edits a router state carries from the dialog to the page (BR-C-3), or none.
 * Router state survives a reload and a pasted history entry, so nothing in it is
 * trusted: a key of the wrong kind is left out.
 */
export function readCarriedEdits(state: unknown): WorkItemEdits | undefined {
  const carried = isRecord(state) ? state[CARRIED_EDITS_KEY] : undefined;
  if (!isRecord(carried)) {
    return undefined;
  }
  const edits: WorkItemEdits = {};
  if (typeof carried.title === 'string') edits.title = carried.title;
  if (typeof carried.description === 'string') edits.description = carried.description;
  if (typeof carried.stateId === 'string') edits.stateId = carried.stateId;
  if (carried.assignee === null || hasId(carried.assignee)) {
    edits.assignee = carried.assignee as WorkItemAssignee | null;
  }
  if (hasId(carried.area)) edits.area = carried.area as WorkItemArea;
  if (isRecord(carried.parent)) {
    const { value, seenParentId } = carried.parent;
    if (
      (value === null || hasId(value)) &&
      (seenParentId === null || typeof seenParentId === 'string')
    ) {
      edits.parent = { value: value as WorkItemRef | null, seenParentId };
    }
  }
  if (isRecord(carried.fields)) {
    const fields: Partial<FieldsDraft> = {};
    for (const name of WORK_ITEM_FIELD_NAMES) {
      const value = carried.fields[name];
      if (typeof value === 'string') fields[name] = value;
    }
    if (Object.keys(fields).length > 0) edits.fields = fields;
  }
  return isUntouched(edits) ? undefined : edits;
}
