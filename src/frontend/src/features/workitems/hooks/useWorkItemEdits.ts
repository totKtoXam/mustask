import { useCallback, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { apiErrorText } from '../../../shared/lib/apiErrorText';
import type { SaveWorkItemBody, WorkItem } from '../api/workItemsApi';
import { cardTexts } from '../lib/cardTexts';
import { draftErrors, type WorkItemDraft } from '../lib/draft';
import {
  NO_EDITS,
  editedDraft,
  firstErrorField,
  hasUnsavedEdits,
  normaliseEdits,
  rebaseEdits,
  touchedChanges,
  withEdit,
  type Edit,
  type WorkItemEdits,
} from '../lib/edits';
import { fieldsOf } from '../lib/fields';
import { notFoundLevel, type WorkItemsNotFoundLevel } from '../lib/notFoundLevel';
import { isParentChanged, placeRefusal, type RefusalField } from '../lib/refusals';
import { useSaveWorkItem } from './mutations';
import { useRefreshWorkItem } from './queries';

/** mod-003's fields of the card a refusal can land on; feat-022 adds the type's own. */
const CARD_FIELDS: readonly RefusalField[] = [
  'title',
  'description',
  'state',
  'assignee',
  'area',
  'parent',
];

/** A field's error text, by the field it stands under. */
export type FieldErrors = Partial<Record<RefusalField, string>>;

/**
 * How a save ended. `saved`: accepted. `clean`: nothing to save. `busy`: another
 * save or read of this card was running, so this trigger was absorbed.
 * `invalid`: the browser's checks refused it and nothing was sent. `refused`:
 * the server said no — at a field or at the card. `gone`: the item, its project
 * or its organization is not there any more; the host shows that frame.
 */
export type SaveOutcome = 'saved' | 'clean' | 'busy' | 'invalid' | 'refused' | 'gone';

/** A request to move focus to a field with an error; `serial` makes a repeat count. */
export interface FocusRequest {
  field: RefusalField;
  serial: number;
}

export interface WorkItemEditsOptions {
  organizationId: string;
  projectId: string;
  /** The item's identifier: its address in the API. */
  identifier: string;
  /** The item as stored now: the cache entry, which a save and a read replace. */
  item: WorkItem;
  /**
   * Edits carried from another card (the dialog's «Развернуть на страницу»,
   * BR-C-3), read once; an edit that equals what `item` stores is dropped.
   */
  initialEdits?: WorkItemEdits;
  /** Tells the user a short result («Изменения сохранены.») — the kit's `useNotice().show`. */
  notify: (text: string) => void;
  /** The path's own level is gone (BR-C-17): the host shows that level's frame. */
  onGone: (level: WorkItemsNotFoundLevel) => void;
  /** An accepted save: the dialog's host reads the page behind it again (BR-C-21). */
  onSaved?: (saved: WorkItem) => void;
  /** An accepted read («Обновить»): the dialog's host reads the page behind it again (BR-C-21). */
  onRefreshed?: (fresh: WorkItem) => void;
}

export interface WorkItemEditsState {
  /** What the user touched; the host carries it to the page when it expands the dialog. */
  edits: WorkItemEdits;
  /** The values the controls show: the stored item with the touched fields laid over it. */
  draft: WorkItemDraft;
  /** «Сохранить» and «Отменить все изменения» are enabled only while this is true. */
  isDirty: boolean;
  /** The body a save would send now: the touched fields that differ, and no other. */
  changes: SaveWorkItemBody;
  errors: FieldErrors;
  /** A refusal no field owns, a failed read: the card's notice. */
  alert: string | null;
  saving: boolean;
  refreshing: boolean;
  /** The first field with an error, when a save was refused: the card focuses it. */
  focus: FocusRequest | null;
  /** One edit of one field; its error clears (and «Дата начала» clears «Целевая дата»'s). */
  change: (edit: Edit) => void;
  save: () => Promise<SaveOutcome>;
  /** The same save, then `close` — only when it was accepted (BR-C-8). */
  saveAndClose: (close: () => void) => Promise<SaveOutcome>;
  /** Reads the item again; the touched fields keep the user's values (BR-C-9). */
  refresh: () => Promise<void>;
  /** Drops every edit and every error (BR-C-10); the host has already asked. */
  undo: () => void;
}

/** The error of `field` is cleared by editing it; «Дата начала» also clears «Целевая дата»'s (BR-M3-35). */
function withoutError(errors: FieldErrors, field: RefusalField): FieldErrors {
  const next = { ...errors };
  delete next[field];
  if (field === 'startDate') {
    delete next.targetDate;
  }
  return next;
}

/**
 * The card's edits and the work they lead to (feat-029 BR-C-8..BR-C-10, BR-C-16,
 * BR-C-17): the draft of touched fields only, the save and its refusals, the
 * read that keeps what was touched, and the undo.
 *
 * One piece of work at a time: a save or a read that is running absorbs a second
 * trigger — Enter and a click at once send one request — and a read cannot land
 * over a save that is on its way (it would put the pre-save item in the cache). The
 * undo is the exception for a read: it only empties the draft, which the read then
 * rebases like any other, so it works while a read runs and is absorbed by a save alone.
 */
export function useWorkItemEdits({
  organizationId,
  projectId,
  identifier,
  item,
  initialEdits,
  notify,
  onGone,
  onSaved,
  onRefreshed,
}: WorkItemEditsOptions): WorkItemEditsState {
  const { mutateAsync: sendChanges } = useSaveWorkItem(organizationId, projectId, identifier);
  const readItem = useRefreshWorkItem(organizationId, projectId, identifier);
  const [edits, setEdits] = useState<WorkItemEdits>(() =>
    initialEdits ? normaliseEdits(item, initialEdits) : NO_EDITS,
  );
  const [errors, setErrors] = useState<FieldErrors>({});
  const [alert, setAlert] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const [focus, setFocus] = useState<FocusRequest | null>(null);
  const working = useRef<'save' | 'read' | null>(null);
  // The draft as last drawn, for a read that ends after the user kept typing.
  const latestEdits = useRef(edits);
  useLayoutEffect(() => {
    latestEdits.current = edits;
  });
  const focusSerial = useRef(0);

  const appearance = item.type.appearance;
  const formFields = useMemo<readonly RefusalField[]>(
    () => [...CARD_FIELDS, ...fieldsOf(appearance)],
    [appearance],
  );

  const focusFirst = useCallback(
    (refused: FieldErrors) => {
      const field = firstErrorField(refused, appearance);
      if (field !== undefined) {
        focusSerial.current += 1;
        setFocus({ field, serial: focusSerial.current });
      }
    },
    [appearance],
  );

  const change = useCallback(
    (edit: Edit) => {
      setEdits((current) => withEdit(item, current, edit));
      setErrors((current) => withoutError(current, edit.field));
    },
    [item],
  );

  /** Places a refused save: a frame, the field it is about, or the card's notice. */
  const refuse = useCallback(
    async (error: unknown): Promise<SaveOutcome> => {
      const placed = placeRefusal(error, formFields);
      if (placed.frame) {
        onGone(placed.frame);
        return 'gone';
      }
      if (isParentChanged(error)) {
        // feat-001 BR-H-4: read the item again first, so «Родитель» shows the
        // stored parent; the user's other edits stay where they are.
        try {
          const fresh = await readItem();
          setEdits((current) => rebaseEdits(fresh, current, { dropParent: true }));
        } catch (readError) {
          const level = notFoundLevel(readError);
          if (level) {
            onGone(level);
            return 'gone';
          }
          setErrors(placed.fields);
          setAlert(apiErrorText(readError));
          focusFirst(placed.fields);
          return 'refused';
        }
      }
      setErrors(placed.fields);
      setAlert(placed.alert);
      focusFirst(placed.fields);
      return 'refused';
    },
    [formFields, onGone, readItem, focusFirst],
  );

  const save = useCallback(async (): Promise<SaveOutcome> => {
    if (working.current) {
      return 'busy';
    }
    if (!hasUnsavedEdits(item, edits)) {
      return 'clean';
    }
    // The browser's checks: nothing is sent while one stands.
    const checked = draftErrors(item, editedDraft(item, edits));
    if (Object.keys(checked).length > 0) {
      setErrors((current) => ({ ...current, ...checked }));
      focusFirst(checked);
      return 'invalid';
    }
    working.current = 'save';
    setSaving(true);
    setAlert(null);
    try {
      const saved = await sendChanges(touchedChanges(item, edits));
      setEdits(NO_EDITS);
      setErrors({});
      notify(cardTexts.notices.saved);
      onSaved?.(saved);
      return 'saved';
    } catch (error) {
      return await refuse(error);
    } finally {
      working.current = null;
      setSaving(false);
    }
  }, [item, edits, sendChanges, notify, onSaved, refuse, focusFirst]);

  const saveAndClose = useCallback(
    async (close: () => void): Promise<SaveOutcome> => {
      const outcome = await save();
      if (outcome === 'saved') {
        close();
      }
      return outcome;
    },
    [save],
  );

  const refresh = useCallback(async (): Promise<void> => {
    if (working.current) {
      return;
    }
    working.current = 'read';
    setRefreshing(true);
    setAlert(null);
    try {
      const fresh = await readItem();
      // The fields stay editable while the read runs: lay the edits as they are
      // now over the fresh item, not as they were when the read began.
      setEdits((current) => rebaseEdits(fresh, current));
      // The notice says what the draft is now, not what it was when the read began.
      notify(
        hasUnsavedEdits(fresh, rebaseEdits(fresh, latestEdits.current))
          ? cardTexts.notices.refreshedKept
          : cardTexts.notices.refreshed,
      );
      onRefreshed?.(fresh);
    } catch (error) {
      const level = notFoundLevel(error);
      if (level) {
        onGone(level);
      } else {
        setAlert(apiErrorText(error));
      }
    } finally {
      working.current = null;
      setRefreshing(false);
    }
  }, [readItem, notify, onGone, onRefreshed]);

  const undo = useCallback(() => {
    // A save sends the draft as it stands; a read only rebases it, whatever it is by then.
    if (working.current === 'save') {
      return;
    }
    setEdits(NO_EDITS);
    setErrors({});
    setAlert(null);
    notify(cardTexts.notices.undone);
  }, [notify]);

  return {
    edits,
    draft: editedDraft(item, edits),
    isDirty: hasUnsavedEdits(item, edits),
    changes: touchedChanges(item, edits),
    errors,
    alert,
    saving,
    refreshing,
    focus,
    change,
    save,
    saveAndClose,
    refresh,
    undo,
  };
}
