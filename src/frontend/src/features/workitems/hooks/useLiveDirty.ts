import { useCallback } from 'react';
import { hasUnsavedEdits, withEdit, type WorkItemEdits } from '../lib/edits';
import type { WorkItem } from '../api/workItemsApi';

/**
 * "Is the card dirty right now?", asked at the moment a leave is decided (feat-018 CR-1/CR-6). The
 * description editor reports after a short delay, so `isDirty` alone can miss an edit made just
 * before a keyboard close; this flushes the editor first and counts what it reports.
 */
export function useLiveDirty(
  flushDescription: () => string | null,
  item: WorkItem,
  edits: WorkItemEdits,
  isDirty: boolean,
): () => boolean {
  return useCallback(() => {
    const held = flushDescription();
    if (held === null) return isDirty;
    return hasUnsavedEdits(item, withEdit(item, edits, { field: 'description', value: held }));
  }, [flushDescription, item, edits, isDirty]);
}
