import type { MouseEvent, ReactNode } from 'react';
import ContentCopyOutlinedIcon from '@mui/icons-material/ContentCopyOutlined';
import {
  IconAction,
  MoreActionsMenu,
  SplitButton,
  ThinRefreshIcon,
  ThinSaveIcon,
  ThinUndoIcon,
} from '../../../../shared/ui';
import { cardTexts } from '../../lib/cardTexts';

/**
 * The action row's right end (feat-029 BR-C-8..BR-C-11; design § 4.2), in the
 * order the Tab key meets it: a place kept for «Следить», the Save group, then
 * «Обновить», «Отменить все изменения» and «Ещё действия». Nothing here is the
 * dialog's: its «Развернуть на страницу» and «Закрыть» are the shell's.
 *
 * The Save group is the card's one filled button. Clean it is `disabled`, and the
 * kit draws that in the reference's pale grey; while a save runs it is `busy`,
 * inert with the focus kept — and so it is while a read runs over edits, since a save
 * started then is absorbed (the kit has no quieter inert state for it). «Обновить» is busy
 * while a read runs and inactive while a save does; «Отменить все изменения» is
 * disabled without edits and works during a read. A read-only card draws neither the
 * Save group nor the undo (BR-C-19).
 */
export function CardActions({
  readOnly,
  dirty,
  saving,
  refreshing,
  follow,
  onSave,
  onSaveAndClose,
  onRefresh,
  onUndo,
  onCopyLink,
}: {
  readOnly: boolean;
  dirty: boolean;
  saving: boolean;
  refreshing: boolean;
  /** The place kept for «Следить»; absent draws nothing. */
  follow?: ReactNode;
  /** The click, so the card can tell whether the button held focus when it was pressed. */
  onSave: (event: MouseEvent<HTMLElement>) => void;
  onSaveAndClose: () => void;
  onRefresh: () => void;
  onUndo: () => void;
  onCopyLink: () => void;
}) {
  return (
    <>
      {follow}
      {!readOnly && (
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
      <MoreActionsMenu
        items={[
          { label: cardTexts.copyLink, icon: <ContentCopyOutlinedIcon />, onSelect: onCopyLink },
        ]}
      />
    </>
  );
}
