import type {
  SaveWorkItemBody,
  WorkItem,
  WorkItemArea,
  WorkItemAssignee,
  WorkItemRef,
} from '../api/workItemsApi';

/** What the form edits (spec § Page: a work item's form): the six values, as typed or picked. */
export interface WorkItemDraft {
  title: string;
  description: string;
  stateId: string;
  /** `null`: «Не назначен». */
  assignee: WorkItemAssignee | null;
  area: WorkItemArea;
  /** feat-001 BR-H-6: `null` is «Нет родителя». */
  parent: WorkItemRef | null;
}

/** The form's values as the item was loaded or last saved. */
export function draftOf(item: WorkItem): WorkItemDraft {
  return {
    title: item.title,
    description: item.description,
    stateId: item.state.id,
    assignee: item.assignee,
    area: item.area,
    parent: item.parent,
  };
}

/** The draft's value of each field as the save compares it: an id where the field names one. */
function comparable(draft: WorkItemDraft): Record<keyof WorkItemDraft, string | null> {
  return {
    title: draft.title,
    description: draft.description,
    stateId: draft.stateId,
    assignee: draft.assignee?.id ?? null,
    area: draft.area.id,
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
  if (draft.area.id !== item.area.id) {
    body.areaId = draft.area.id;
  }
  const parentId = draft.parent?.id ?? null;
  const seenParentId = item.parent?.id ?? null;
  if (parentId !== seenParentId) {
    // `parentId: null` clears it; an absent `parent` would keep it.
    body.parent = { parentId, seenParentId };
  }
  return body;
}

/** «Сохранить» is enabled only while this is true (spec § Page: a work item's form). */
export function hasChanges(body: SaveWorkItemBody): boolean {
  return Object.keys(body).length > 0;
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
  const before = comparable(draftOf(loaded));
  const now = comparable(draft);
  const touched = (field: keyof WorkItemDraft) => now[field] !== before[field];
  const next = draftOf(fresh);
  return {
    title: touched('title') ? draft.title : next.title,
    description: touched('description') ? draft.description : next.description,
    stateId: touched('stateId') ? draft.stateId : next.stateId,
    assignee: touched('assignee') ? draft.assignee : next.assignee,
    area: touched('area') ? draft.area : next.area,
    parent: next.parent,
  };
}
