// The read of an organization's process definition, strictly per
// docs/features/mod-001-process-seed/api-contract.md § 1. camelCase, as all of
// Mustask.Api. The slice sends this one GET and nothing else (BR-M2-13).
import { apiFetch } from '../../../shared/api/httpClient';

/** api-contract § 1.2 `backlogLevels[].kind`. */
export type BacklogLevelKind = 'portfolio' | 'requirement' | 'task';

/** api-contract § 1.2 `workItemTypes[].key`: Latin, fixed, unique in the process. */
export type WorkItemTypeKey = 'EPIC' | 'FEAT' | 'US' | 'BUG' | 'TASK';

/** The appearance token a type stores (D-6); the theme draws it. */
export type AppearanceToken = 'epic' | 'feature' | 'story' | 'task' | 'bug';

/** doc-007's state-category codes; never shown, only looked up in the theme. */
export type CategoryCode = 'Proposed' | 'InProgress' | 'Resolved' | 'Completed' | 'Removed';

export interface BacklogLevel {
  id: string;
  name: string;
  rank: number;
  kind: BacklogLevelKind;
}

export interface BacklogLevelReference {
  id: string;
  name: string;
}

export interface StateCategory {
  code: CategoryCode;
  /** The Russian label the screen shows; the client holds no map of its own (design R-26). */
  label: string;
}

export interface StateDefinition {
  id: string;
  name: string;
  /** 1-based and dense. */
  order: number;
  isInitial: boolean;
  category: StateCategory;
}

export interface WorkItemType {
  id: string;
  key: WorkItemTypeKey;
  name: string;
  appearance: AppearanceToken;
  backlogLevel: BacklogLevelReference;
  /** Ordered by `order` ascending; rendered as received. */
  states: StateDefinition[];
}

export interface ProcessDefinitionResponse {
  id: string;
  /** Ordered by `rank` descending. */
  backlogLevels: BacklogLevel[];
  /** In display order (design DD-2); the client never sorts them. */
  workItemTypes: WorkItemType[];
}

/**
 * A `200` the screen cannot draw: no types, or a type or state missing a part
 * the cards read. A process always has five types, each with a backlog level
 * and its states, each state with its category (BR-M2-8, api-contract § 1.2,
 * § 1.4), so this is a broken answer, not an empty process: the page shows it
 * as a failed read (design § States, Read failed; DD-5) instead of throwing
 * while it renders. Its name is not `TypeError`, so the banner gives the
 * panel's existing generic text.
 */
export class UnreadableProcessDefinitionError extends Error {
  constructor(missing: string) {
    super(`The process definition answer cannot be read: ${missing}.`);
    this.name = 'UnreadableProcessDefinitionError';
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

/**
 * The first part of the answer the cards would read and cannot, or nothing.
 * Only what rendering dereferences is checked; a missing text field shows as
 * an empty cell and does not break the page.
 */
function missingPart(definition: unknown): string | undefined {
  if (!isRecord(definition)) {
    return 'the body is not an object';
  }
  const types = definition.workItemTypes;
  if (!Array.isArray(types) || types.length === 0) {
    return 'no work item types';
  }
  for (const type of types as unknown[]) {
    if (!isRecord(type)) {
      return 'a work item type is not an object';
    }
    if (!isRecord(type.backlogLevel)) {
      return 'a work item type has no backlog level';
    }
    if (!Array.isArray(type.states)) {
      return 'a work item type has no states';
    }
    for (const state of type.states as unknown[]) {
      if (!isRecord(state)) {
        return 'a state is not an object';
      }
      if (!isRecord(state.category)) {
        return 'a state has no category';
      }
    }
  }
  return undefined;
}

/** The read's path. The id is sent as the address carries it, re-encoded, never validated (DD-4). */
export function processDefinitionApiPath(organizationId: string): string {
  return `/api/organizations/${encodeURIComponent(organizationId)}/process`;
}

export async function getProcessDefinition(
  organizationId: string,
): Promise<ProcessDefinitionResponse> {
  const definition = await apiFetch<unknown>(processDefinitionApiPath(organizationId));
  const missing = missingPart(definition);
  if (missing) {
    throw new UnreadableProcessDefinitionError(missing);
  }
  return definition as ProcessDefinitionResponse;
}
