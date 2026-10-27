import type {
  WorkItemAssignee,
  WorkItemFieldName,
  WorkItemRef,
  WorkItemTeam,
} from '../api/workItemsApi';
import type { DraftCriterion } from './draft';
import type { Edit } from './edits';
import { WORK_ITEM_FIELD_NAMES } from './fields';

/**
 * One function per field, each reporting its field's edit: what a control is given as
 * its `onChange`. They are made once for a `change`, so a control that receives the
 * same function twice can skip a render — the card has twenty fields, and typing in
 * one must not draw the other nineteen again.
 */
export interface EditHandlers {
  title: (value: string) => void;
  description: (value: string) => void;
  /** The state's id. */
  state: (stateId: string) => void;
  assignee: (assignee: WorkItemAssignee | null) => void;
  team: (team: WorkItemTeam | null) => void;
  parent: (parent: WorkItemRef | null) => void;
  acceptanceCriteria: (criteria: DraftCriterion[]) => void;
  /** feat-022's fields per type, as their controls hold them. */
  field: Readonly<Record<WorkItemFieldName, (value: string) => void>>;
}

export function editHandlers(change: (edit: Edit) => void): EditHandlers {
  const field = Object.fromEntries(
    WORK_ITEM_FIELD_NAMES.map((name) => [
      name,
      name === 'acceptanceCriteria'
        ? (_value: string) => {}
        : (value: string) => change({ field: name, value }),
    ]),
  ) as Record<WorkItemFieldName, (value: string) => void>;
  return {
    title: (value) => change({ field: 'title', value }),
    description: (value) => change({ field: 'description', value }),
    state: (value) => change({ field: 'state', value }),
    assignee: (value) => change({ field: 'assignee', value }),
    team: (value) => change({ field: 'team', value }),
    parent: (value) => change({ field: 'parent', value }),
    acceptanceCriteria: (criteria) => change({ field: 'acceptanceCriteria', value: criteria }),
    field,
  };
}
