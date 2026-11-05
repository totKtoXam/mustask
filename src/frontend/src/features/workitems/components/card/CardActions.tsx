import type { MouseEvent, ReactNode, Ref } from 'react';
import Box from '@mui/material/Box';
import ContentCopyOutlinedIcon from '@mui/icons-material/ContentCopyOutlined';
import DeleteForeverOutlinedIcon from '@mui/icons-material/DeleteForeverOutlined';
import DeleteOutlineOutlinedIcon from '@mui/icons-material/DeleteOutlineOutlined';
import {
  ActionButton,
  IconAction,
  MoreActionsMenu,
  SplitButton,
  ThinRefreshIcon,
  ThinSaveIcon,
  ThinUndoIcon,
  type MenuEntry,
} from '../../../../shared/ui';
import { cardTexts, deletionTexts } from '../../lib/cardTexts';

/**
 * The action row's right end (feat-029 BR-C-8..BR-C-11; design § 4.2), in the
 * order the Tab key meets it: a place kept for «Следить», the Save group, then
 * «Обновить», «Отменить все изменения» and «Ещё действия». Nothing here is the
 * dialog's: its «Развернуть на весь экран» and «Закрыть» are the shell's.
 *
 * The Save group is the card's one filled button. Clean it is `disabled`, and the
 * kit draws that in the reference's pale grey; while a save runs it is `busy`,
 * inert with the focus kept — and so it is while a read runs over edits, since a save
 * started then is absorbed (the kit has no quieter inert state for it). «Обновить» is busy
 * while a read runs and inactive while a save does; «Отменить все изменения» is
 * disabled without edits and works during a read. A read-only card draws neither the
 * Save group nor the undo (BR-C-19).
 *
 * A deleted card (mod-005 design § 4) is read-only, and «Восстановить» — its one filled button —
 * stands where the Save group stood, for a holder of the delete code. «⋮» offers «Копировать
 * ссылку» and, after a divider, «Удалить» on a live card for a holder of the delete code and
 * «Удалить навсегда» on a deleted one for a holder of the purge code (design § 2); the divider
 * stands only between two drawn groups.
 */
export function CardActions({
  readOnly,
  deleted,
  canRestore,
  canDelete,
  canPurge,
  restoring,
  dirty,
  saving,
  refreshing,
  follow,
  moreRef,
  onSave,
  onSaveAndClose,
  onRefresh,
  onUndo,
  onCopyLink,
  onRestore,
  onDelete,
  onPurge,
}: {
  readOnly: boolean;
  /** The item is in «Удалена»: «Восстановить» and «Удалить навсегда» take the place of the Save group and «Удалить». */
  deleted: boolean;
  /** `workitems.workitem.delete`, on a deleted card. */
  canRestore: boolean;
  /** `workitems.workitem.delete`, on a live card. */
  canDelete: boolean;
  /** `workitems.workitem.purge`, on a deleted card. */
  canPurge: boolean;
  /** A restore is under way: «Восстановить» is busy. */
  restoring: boolean;
  dirty: boolean;
  saving: boolean;
  refreshing: boolean;
  /** The place kept for «Следить»; absent draws nothing. */
  follow?: ReactNode;
  /** The element around «Ещё действия»: the card moves focus to its button after a Delete. */
  moreRef?: Ref<HTMLSpanElement>;
  /** The click, so the card can tell whether the button held focus when it was pressed. */
  onSave: (event: MouseEvent<HTMLElement>) => void;
  onSaveAndClose: () => void;
  onRefresh: () => void;
  onUndo: () => void;
  onCopyLink: () => void;
  onRestore: () => void;
  onDelete: () => void;
  onPurge: () => void;
}) {
  const items: MenuEntry[] = [
    { label: cardTexts.copyLink, icon: <ContentCopyOutlinedIcon />, onSelect: onCopyLink },
  ];
  if (!deleted && canDelete) {
    items.push('divider', {
      label: deletionTexts.menu.delete,
      icon: <DeleteOutlineOutlinedIcon />,
      onSelect: onDelete,
    });
  }
  if (deleted && canPurge) {
    items.push('divider', {
      label: deletionTexts.menu.purge,
      icon: <DeleteForeverOutlinedIcon />,
      destructive: true,
      onSelect: onPurge,
    });
  }
  return (
    <>
      {follow}
      {deleted
        ? canRestore && (
            <ActionButton
              label={deletionTexts.restore}
              tone="primary"
              busy={restoring}
              onClick={onRestore}
            />
          )
        : !readOnly && (
            <SplitButton
              label={cardTexts.save.label}
              icon={<ThinSaveIcon />}
              groupLabel={cardTexts.save.group}
              menuLabel={cardTexts.save.menu}
              items={[{ label: cardTexts.save.andClose, onSelect: onSaveAndClose }]}
              busy={saving || (refreshing && dirty)}
              disabled={!dirty && !saving}
              onClick={onSave}
            />
          )}
      <IconAction
        label={cardTexts.refresh}
        icon={<ThinRefreshIcon />}
        busy={refreshing}
        inactive={saving}
        onClick={onRefresh}
      />
      {!readOnly && (
        <IconAction
          label={cardTexts.undo}
          icon={<ThinUndoIcon />}
          disabled={!dirty}
          inactive={saving}
          onClick={onUndo}
        />
      )}
      {/* No layout of its own: the wrapper only gives the card a handle on the «⋮» button. */}
      <Box component="span" ref={moreRef} sx={{ display: 'contents' }}>
        <MoreActionsMenu items={items} />
      </Box>
    </>
  );
}
