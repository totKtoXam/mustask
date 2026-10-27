import {
  useCallback,
  useEffect,
  useId,
  useMemo,
  useRef,
  useState,
  type KeyboardEvent,
  type MouseEvent,
  type ReactNode,
} from 'react';
import { useIsMutating } from '@tanstack/react-query';
import Box from '@mui/material/Box';
import { useProcessDefinition } from '../../../process';
import { ConfirmDialog, InlineTextField, type NoticeSeverity } from '../../../../shared/ui';
import { RequiresOrganizationCode } from '../../../../shared/ui/RequiresOrganizationCode';
import { formatDateTime } from '../../../../shared/lib/format';
import type { WorkItem } from '../../api/workItemsApi';
import { deletionMutationKey } from '../../hooks/mutations';
import { useGuardedLeave } from '../../hooks/useGuardedLeave';
import { useWorkItemEdits } from '../../hooks/useWorkItemEdits';
import {
  cardTexts,
  changedAtText,
  deletionTexts,
  switchGuardTexts,
  undoGuardTexts,
  type GuardTexts,
} from '../../lib/cardTexts';
import { deletionLine, isDeleted } from '../../lib/deletion';
import type { DeletionRefusal } from '../../lib/deletionRefusals';
import { editHandlers } from '../../lib/editHandlers';
import { createFieldRegistry } from '../../lib/fieldRegistry';
import { TEXT_FIELDS, layoutOf } from '../../lib/fields';
import type { WorkItemsNotFoundLevel } from '../../lib/notFoundLevel';
import { allowedParentTypes, editableStates, parentHelperText, typeById } from '../../lib/process';
import type { RefusalField } from '../../lib/refusals';
import { workItemPath } from '../../paths';
import { workItemsPermissions } from '../../permissions';
import { AcceptanceCriteriaSection } from '../AcceptanceCriteriaSection';
import { AssigneeField } from './AssigneeField';
import { CardActions } from './CardActions';
import { CardTeamField } from './CardTeamField';
import { ProjectField } from './ProjectField';
import type { CardColumnItem, CardFrameProps, CardNotice } from './cardFrame';
import { ChildItems } from './ChildItems';
import { DeleteWorkItemDialog } from './DeleteWorkItemDialog';
import { oneColumnCap } from './oneColumn';
import { ParentField } from './ParentField';
import { PropertyField, TextBlock } from './PropertyField';
import { PurgeWorkItemDialog } from './PurgeWorkItemDialog';
import { RestoreWorkItemDialog } from './RestoreWorkItemDialog';
import { StateField } from './StateField';
import type { WorkItemCardSlots } from './cardSlots';

/** Which of mod-005's three dialogs is open over the card, if any. */
type DeletionDialog = 'delete' | 'restore' | 'purge';

export interface WorkItemCardProps {
  organizationId: string;
  projectId: string;
  /** The item as the page or the dialog read it. */
  item: WorkItem;
  mode: 'dialog' | 'page';
  /**
   * Tells the user a short result (the kit's `useNotice().show`); a refusal says it is an
   * error (mod-005 design § 8).
   */
  notify: (text: string, severity?: NoticeSeverity) => void;
  /** The path's own level is gone: the host replaces the card with that level's frame. */
  onGone: (level: WorkItemsNotFoundLevel) => void;
  /**
   * Arranges the card's content in a shell: the page's (`PageFrame`), which the host
   * wraps with what only it knows — the trail — or, for the dialog, hands it to the
   * dialog's one shell (`PublishFrame` into `DialogFrame`), which the host drew before
   * the card existed.
   */
  renderFrame: (frame: CardFrameProps) => ReactNode;
  /**
   * BR-C-19: every field read-only, no Save group, no undo. The hosts set it from the
   * served access — the reader holds no `workitems.workitem.write` (mod-016 design § 6.5).
   */
  readOnly?: boolean;
  /** The reason a read-only card gives; default «Рабочий элемент доступен только для чтения.». */
  readOnlyText?: string;
  /**
   * «Родитель» alone is read-only on a card that is editable otherwise: the member who
   * holds `workitems.workitem.write` but not `workitems.link.write` (mod-016 design § 6.2).
   * A read-only card has it already.
   */
  parentReadOnly?: boolean;
  /** «Сохранить и закрыть», once the save was accepted: the dialog closes, the page goes back. */
  onClose?: () => void;
  /**
   * A plain click on a related item (BR-C-5): the dialog switches to it. Absent
   * on the page, whose links navigate. A card with edits asks first.
   */
  onOpenItem?: (identifier: string) => void;
  /** An accepted save: the dialog's host reads the page behind it again (BR-C-21). */
  onSaved?: (saved: WorkItem) => void;
  /** An accepted «Обновить»: the same host reads the page behind it again (BR-C-21). */
  onRefreshed?: (fresh: WorkItem) => void;
  /**
   * `workitems.workitem.delete` (mod-005 design § 9): «Удалить» on a live card, «Восстановить»
   * on a deleted one, and the sentence that says how to get the item back.
   */
  canDelete?: boolean;
  /** `workitems.workitem.purge`: «Удалить навсегда» on a deleted card — the administrator alone. */
  canPurge?: boolean;
  /** A Delete, a Restore or a Purge was accepted: the host reads its own views again (design § 7). */
  onChanged?: () => void;
  /**
   * A Purge was accepted (design § 6): the item is gone, so the dialog's host closes the card
   * and the page's goes to the list.
   */
  onPurged?: () => void;
  /** The places kept (BR-C-18). */
  slots?: WorkItemCardSlots;
}

/**
 * A work item's card (feat-029): the header, the band, the tab and the three
 * columns of the reference, in one component that the dialog and the page both
 * host. It reads nothing itself but the process; the item, the notices and the
 * shell come from the host.
 *
 * Everything the card shows is a field of the kit, held in a draft of touched
 * fields only (`useWorkItemEdits`): the header's title and assignee, the band's
 * state and area, the texts and the type's sections, «Родитель». The card has no
 * `<form>` — Enter in the title saves through the field's key handler and the
 * Save group is a plain button — so no shell has to host one (design § 12).
 *
 * Typing in one field draws that field, not the card's twenty: each control is
 * memoised and is given the same handler, ref and options on every render.
 */
export function WorkItemCard({
  organizationId,
  projectId,
  item,
  mode,
  notify,
  onGone,
  renderFrame,
  readOnly: hostReadOnly = false,
  readOnlyText = cardTexts.readOnly,
  parentReadOnly = false,
  onClose,
  onOpenItem,
  onSaved,
  onRefreshed,
  canDelete = false,
  canPurge = false,
  onChanged,
  onPurged,
  slots = {},
}: WorkItemCardProps) {
  const headingId = useId();
  const childrenHeadId = useId();
  const process = useProcessDefinition(organizationId);
  const [fields] = useState(() => createFieldRegistry<RefusalField>());
  const state = useWorkItemEdits({
    organizationId,
    projectId,
    identifier: item.identifier,
    item,
    notify,
    onGone,
    onSaved,
    onRefreshed,
  });
  const [confirmingUndo, setConfirmingUndo] = useState(false);
  const leave = useGuardedLeave(state.isDirty);
  // The mode follows the read (mod-005 design § 4): a state in the Removed category makes the
  // card read-only, whatever the role holds; the role decides only what it may do about it.
  const deleted = isDeleted(item);
  const readOnly = hostReadOnly || deleted;
  const [dialog, setDialog] = useState<DeletionDialog | null>(null);
  const restoring =
    useIsMutating({ mutationKey: deletionMutationKey('restore', item.identifier) }) > 0;
  // Where focus goes once a dialog has closed over a card that changed under it (design § 3, § 5).
  const [refocus, setRefocus] = useState<{ target: 'more' | 'title'; serial: number } | null>(null);
  const moreRef = useRef<HTMLSpanElement>(null);

  const { focus } = state;
  useEffect(() => {
    if (focus) {
      fields.focus(focus.field);
    }
  }, [focus, fields]);
  // After the dialog has gone: MUI gives focus back to what it remembered when it opened — for a
  // dialog chosen from «⋮» a menu entry that is gone — so the card names the place itself.
  useEffect(() => {
    if (refocus?.target === 'title') {
      fields.focus('title');
    } else if (refocus?.target === 'more') {
      moreRef.current?.querySelector('button')?.focus();
    }
  }, [refocus, fields]);

  /** The dialog is closed; focus goes to «Ещё действия», where it was chosen from. */
  function closeDialogToMenu() {
    setDialog(null);
    setRefocus((current) => ({ target: 'more', serial: (current?.serial ?? 0) + 1 }));
  }

  /** A refusal that leaves the card stale or gone (design § 8): the dialog closes, the card does the rest. */
  function refusedByTheServer(refusal: Exclude<DeletionRefusal, { kind: 'stays' }>) {
    setDialog(null);
    if (refusal.kind === 'gone') {
      onGone(refusal.level);
      return;
    }
    notify(refusal.text, 'error');
    void state.refresh({ notice: false });
  }

  const { draft, errors, saving, refreshing, change } = state;
  const on = useMemo(() => editHandlers(change), [change]);
  const layout = layoutOf(item.type.appearance);
  const common = { busy: saving, readOnly };

  // «Статус» offers the type's states without «Удалена» once the process is
  // read; until then, only the current one.
  const processData = process.data;
  const states = useMemo(() => {
    const type = processData ? typeById(processData.workItemTypes, item.type.id) : undefined;
    const offered = type ? editableStates(type) : [item.state];
    return offered.some((candidate) => candidate.id === item.state.id)
      ? offered
      : [item.state, ...offered];
  }, [processData, item.type.id, item.state]);
  // «Родитель»'s hint names the types the item's level allows once the process
  // is read (feat-001 api-contract § 1); until then, none.
  const parentHint = processData
    ? parentHelperText(allowedParentTypes(processData, item.type.id))
    : null;

  function submitOnEnter(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key === 'Enter' && !event.nativeEvent.isComposing && !readOnly) {
      event.preventDefault();
      void state.save();
    }
  }

  // The clean main button is natively disabled once a save is accepted, and a disabled
  // button gives its focus up: to the page's body, or to the dialog's frame a moment later.
  // A user who pressed it keeps their place by being taken to the title (design § 4.2).
  async function saveByButton(event: MouseEvent<HTMLElement>) {
    const held = event.currentTarget === document.activeElement;
    if ((await state.save()) === 'saved' && held) {
      fields.focus('title');
    }
  }

  async function copyLink() {
    const address = `${window.location.origin}${workItemPath(
      organizationId,
      projectId,
      item.identifier,
    )}`;
    try {
      await navigator.clipboard.writeText(address);
      notify(cardTexts.notices.linkCopied);
    } catch {
      notify(cardTexts.notices.linkCopyFailed);
    }
  }

  // A plain click on a related item asks first when edits stand (BR-C-5, D-4).
  const { ask } = leave;
  const openItem = useCallback(
    (identifier: string) => {
      if (onOpenItem) {
        ask(() => onOpenItem(identifier));
      }
    },
    [ask, onOpenItem],
  );

  // A card error (a failed read) takes the area before the deleted card's note (design § 4); a
  // deleted card says why it is read-only and, for a holder of the delete code, how to get the
  // item back, then who deleted it and when (BR-DR-7).
  const notice: CardNotice | null = state.alert
    ? { severity: 'error', text: state.alert }
    : deleted
      ? {
          severity: 'info',
          text: canDelete
            ? `${deletionTexts.notice.readOnly} ${deletionTexts.notice.restoreHint}`
            : deletionTexts.notice.readOnly,
          detail: deletionLine(item.deletion) ?? undefined,
        }
      : readOnly
        ? { severity: 'info', text: readOnlyText }
        : null;

  const isStory =
    layout.isStory ||
    item.type.appearance === 'story' ||
    item.type.key === 'US' ||
    (item as { typeKey?: string }).typeKey === 'US';

  const main: CardColumnItem[] = [
    {
      key: 'description',
      title: cardTexts.description.label,
      content: (
        <TextBlock
          name="description"
          value={draft.description}
          error={errors.description}
          onChange={on.description}
          inputRef={fields.ref('description')}
          {...common}
        />
      ),
    },
    ...(isStory
      ? [
          {
            key: 'acceptanceCriteria',
            title: 'Критерии приёмки',
            content: (
              <AcceptanceCriteriaSection
                criteria={draft.fields.acceptanceCriteria}
                disabled={common.busy}
                readOnly={common.readOnly}
                errors={errors}
                onChange={on.acceptanceCriteria}
              />
            ),
          },
        ]
      : []),
    ...layout.texts.map((name): CardColumnItem => ({
      key: name,
      title: TEXT_FIELDS[name].label,
      content: (
        <TextBlock
          name={name}
          value={draft.fields[name]}
          error={errors[name]}
          onChange={on.field[name]}
          inputRef={fields.ref(name)}
          {...common}
        />
      ),
    })),
  ];
  if (slots.discussion) {
    main.push({ key: 'discussion', node: slots.discussion });
  }

  const side: CardColumnItem[] = layout.sections.map((section): CardColumnItem => ({
    key: section.title,
    title: section.title,
    content: (
      <Box sx={oneColumnCap}>
        {section.fields.map((name) => (
          <PropertyField
            key={name}
            name={name}
            value={draft.fields[name]}
            error={errors[name]}
            onChange={on.field[name]}
            inputRef={fields.ref(name)}
            {...common}
          />
        ))}
      </Box>
    ),
  }));

  const extra: CardColumnItem[] = [];
  if (slots.deployment) {
    extra.push({ key: 'deployment', node: slots.deployment });
  }
  if (slots.development) {
    extra.push({ key: 'development', node: slots.development });
  }
  extra.push({
    key: 'related',
    title: cardTexts.relatedWork,
    content: (
      <Box sx={oneColumnCap}>
        <ParentField
          organizationId={organizationId}
          projectId={projectId}
          identifier={item.identifier}
          value={draft.parent}
          stored={item.parent}
          hint={parentHint}
          error={errors.parent}
          onChange={on.parent}
          onOpenItem={onOpenItem ? openItem : undefined}
          inputRef={fields.ref('parent')}
          busy={saving}
          readOnly={readOnly || parentReadOnly}
        />
      </Box>
    ),
    subsection: {
      id: childrenHeadId,
      title: cardTexts.children,
      count: item.children.length,
      content: (
        <Box sx={oneColumnCap}>
          <ChildItems
            organizationId={organizationId}
            projectId={projectId}
            items={item.children}
            labelledBy={childrenHeadId}
            onOpenItem={onOpenItem ? openItem : undefined}
          />
        </Box>
      ),
    },
  });

  const frame: CardFrameProps = {
    mode,
    appearance: item.type.appearance,
    typeName: item.type.name,
    identifier: item.identifier,
    headingId,
    title: (
      <InlineTextField
        label={cardTexts.title.label}
        labelPlacement="hidden"
        size="title"
        required
        placeholder={cardTexts.title.placeholder}
        value={draft.title}
        onChange={on.title}
        error={errors.title}
        ref={fields.ref('title')}
        inputProps={{ onKeyDown: submitOnEnter }}
        {...common}
      />
    ),
    headerMeta: (
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 2, minWidth: 0 }}>
        {/* A kit field fills its container: the card sizes the assignee's. */}
        <Box sx={(theme) => ({ flex: `0 1 ${theme.spacing(32.5)}`, minWidth: theme.spacing(20) })}>
          <AssigneeField
            organizationId={organizationId}
            value={draft.assignee}
            error={errors.assignee}
            onChange={on.assignee}
            inputRef={fields.ref('assignee')}
            {...common}
          />
        </Box>
        {slots.commentsCount}
        {slots.tags}
      </Box>
    ),
    headerActions: (
      <CardActions
        readOnly={readOnly}
        deleted={deleted}
        canRestore={canDelete}
        canDelete={canDelete}
        canPurge={canPurge}
        restoring={restoring}
        dirty={state.isDirty}
        saving={saving}
        refreshing={refreshing}
        follow={slots.follow}
        moreRef={moreRef}
        onSave={(event) => void saveByButton(event)}
        onSaveAndClose={() => void state.saveAndClose(() => onClose?.())}
        onRefresh={() => void state.refresh()}
        onUndo={() => setConfirmingUndo(true)}
        onCopyLink={() => void copyLink()}
        onRestore={() => setDialog('restore')}
        onDelete={() => setDialog('delete')}
        onPurge={() => setDialog('purge')}
      />
    ),
    bandBlocks: [
      <>
        <StateField
          states={states}
          value={draft.stateId}
          error={errors.state}
          onChange={on.state}
          inputRef={fields.ref('state')}
          {...common}
        />
        {/* feat-040 design § 7, D-2: row 2 of block A, under «Статус»; «Причина» follows. */}
        <ProjectField name={item.project.name} />
        {slots.reason}
      </>,
      <>
        {/* mod-018 design § 8, D-6: «Команда» is row 1 of block B, «Итерация» row 2. */}
        <CardTeamField
          organizationId={organizationId}
          projectId={projectId}
          value={draft.team}
          error={errors.team}
          onChange={on.team}
          inputRef={fields.ref('team')}
          {...common}
        />
        {slots.iteration}
      </>,
    ],
    bandMeta: slots.updatedBy ?? changedAtText(formatDateTime(item.changedAt)),
    tabs: {
      label: cardTexts.tabs.label,
      details: cardTexts.tabs.details,
      extra: slots.tabs ?? [],
    },
    notice,
    main,
    side,
    extra,
    busy: saving,
    dirty: state.isDirty,
    pagePath: workItemPath(organizationId, projectId, item.identifier),
  };

  return (
    <>
      {renderFrame(frame)}
      <CardConfirm
        open={confirmingUndo}
        texts={undoGuardTexts(item.identifier)}
        onConfirm={() => {
          setConfirmingUndo(false);
          state.undo();
        }}
        onClose={() => setConfirmingUndo(false)}
      />
      <CardConfirm
        open={leave.asking}
        texts={switchGuardTexts(item.identifier)}
        onConfirm={leave.confirm}
        onClose={leave.cancel}
      />
      {/* mod-005 design § 8: a lost code disables the primary and leaves «Отмена». */}
      <RequiresOrganizationCode code={workItemsPermissions.workItemDelete}>
        {dialog === 'delete' && (
          <DeleteWorkItemDialog
            organizationId={organizationId}
            projectId={projectId}
            identifier={item.identifier}
            liveChildren={item.children.length}
            hasDraft={state.isDirty}
            onDeleted={() => {
              setDialog(null);
              setRefocus((current) => ({ target: 'more', serial: (current?.serial ?? 0) + 1 }));
              notify(deletionTexts.delete.done(item.identifier));
              onChanged?.();
            }}
            onRefused={refusedByTheServer}
            onClose={closeDialogToMenu}
          />
        )}
        {dialog === 'restore' && (
          <RestoreWorkItemDialog
            organizationId={organizationId}
            projectId={projectId}
            item={item}
            onRestored={() => {
              setDialog(null);
              setRefocus((current) => ({ target: 'title', serial: (current?.serial ?? 0) + 1 }));
              notify(deletionTexts.restoreDialog.done(item.identifier));
              onChanged?.();
            }}
            onRefused={refusedByTheServer}
            onClose={() => setDialog(null)}
          />
        )}
      </RequiresOrganizationCode>
      <RequiresOrganizationCode code={workItemsPermissions.workItemPurge}>
        {dialog === 'purge' && (
          <PurgeWorkItemDialog
            organizationId={organizationId}
            projectId={projectId}
            identifier={item.identifier}
            title={item.title}
            liveChildren={item.children.length}
            onPurged={() => {
              setDialog(null);
              notify(deletionTexts.purgeDialog.done(item.identifier));
              onChanged?.();
              onPurged?.();
            }}
            onRefused={refusedByTheServer}
            onClose={closeDialogToMenu}
          />
        )}
      </RequiresOrganizationCode>
    </>
  );
}

/** A question over the card: destructive, the cancel focused so Enter never discards. */
function CardConfirm({
  open,
  texts,
  onConfirm,
  onClose,
}: {
  open: boolean;
  texts: GuardTexts;
  onConfirm: () => void;
  onClose: () => void;
}) {
  return (
    <ConfirmDialog
      open={open}
      title={texts.title}
      confirmLabel={texts.confirmLabel}
      cancelLabel={texts.cancelLabel}
      destructive
      cancelAutoFocus
      onConfirm={onConfirm}
      onClose={onClose}
    >
      {texts.text}
    </ConfirmDialog>
  );
}
