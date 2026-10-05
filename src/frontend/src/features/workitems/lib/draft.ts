import type {
  SaveWorkItemBody,
  WorkItem,
  WorkItemArea,
  WorkItemAssignee,
} from '../api/workItemsApi';

/** What the form edits (spec § Page: a work item's form): the five values, as typed or picked. */
export interface WorkItemDraft {
  title: string;
  description: string;
  stateId: string;
  /** `null`: «Не назначен». */
  assignee: WorkItemAssignee | null;
  area: WorkItemArea;
}

/** The form's values as the item was loaded or last saved. */
export function draftOf(item: WorkItem): WorkItemDraft {
  return {
    title: item.title,
    description: item.description,
    stateId: item.state.id,
    assignee: item.assignee,
    area: item.area,
  };
}

/**
 * The save's body: exactly the fields whose value differs from the loaded one
 * (BR-M3-25; api-contract § 2.4), so a field the user did not change keeps
 * whatever is stored when the save lands. A value changed and changed back is
 * no change; the current state picked again is no change (US-5).
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
  return body;
}

/** «Сохранить» is enabled only while this is true (spec § Page: a work item's form). */
export function hasChanges(body: SaveWorkItemBody): boolean {
  return Object.keys(body).length > 0;
}
