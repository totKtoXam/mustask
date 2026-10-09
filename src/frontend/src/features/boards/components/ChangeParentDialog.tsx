import Typography from '@mui/material/Typography';
import { ParentPicker, type WorkItemRef } from '../../workitems';
import { FormDialog } from '../../../shared/ui/FormDialog';
import type { BacklogTreeItem } from '../api/boardsApi';

/**
 * The dialog «Изменить родителя» (feat-001 US-5; spec § Page «Бэклоги»;
 * mod-015 § 9.5, `maxWidth="sm"`): «<Тип> <ID> «<Заголовок>»», then the
 * form's «Родитель» picker without «Нет родителя», searching only the level
 * directly above (`levelAbove`), its helper naming that level; the current
 * parent preselected whatever its level, the field focused (mod-015 § 9.5).
 * «Сохранить» waits for a different parent. Busy per mod-015 § 9.1: every
 * control off, Escape ignored. A `workitems.link.*` refusal is the field's
 * error, any other the `Alert`; the caller owns the choice so that a read
 * after `parent_changed` can put the stored parent in it.
 */
export function ChangeParentDialog({
  organizationId,
  projectId,
  row,
  aboveLabel,
  value,
  busy,
  fieldError,
  alert,
  onChange,
  onSubmit,
  onClose,
}: {
  organizationId: string;
  projectId: string;
  /**
   * The row being given a parent, as the dialog opened on it or as the read
   * after `parent_changed` found it — never a later re-read: «Сохранить»
   * waits for a parent other than this one (BR-H-4).
   */
  row: Pick<BacklogTreeItem, 'identifier' | 'title' | 'type' | 'parent'>;
  /** The level above's label, «Эпики» or «Фичи». */
  aboveLabel: string;
  value: WorkItemRef | null;
  busy: boolean;
  fieldError: string | undefined;
  alert: string | null;
  onChange(parent: WorkItemRef | null): void;
  onSubmit(): void;
  onClose(): void;
}) {
  return (
    <FormDialog
      open
      title="Изменить родителя"
      submitLabel="Сохранить"
      busy={busy}
      error={alert}
      submitDisabled={value === null || value.id === row.parent?.id}
      onSubmit={onSubmit}
      onClose={onClose}
    >
      <Typography variant="body2">{`${row.type.name} ${row.identifier} «${row.title}»`}</Typography>
      <ParentPicker
        organizationId={organizationId}
        projectId={projectId}
        identifier={row.identifier}
        value={value}
        disabled={busy}
        error={fieldError}
        helperText={`Предлагаются элементы уровня «${aboveLabel}». Родителя другого уровня можно выбрать в форме рабочего элемента.`}
        levelAbove
        offerNone={false}
        autoFocus
        onChange={onChange}
      />
    </FormDialog>
  );
}
