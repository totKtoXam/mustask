import { useRef, useState } from 'react';
import Typography from '@mui/material/Typography';
import { apiErrorText } from '../../../../shared/lib/apiErrorText';
import {
  CategoryMarker,
  FieldsSkeleton,
  FormDialog,
  InlineNotice,
  InlineSelectField,
} from '../../../../shared/ui';
import { useProcessDefinition } from '../../../process';
import type { WorkItem, WorkItemStateReference } from '../../api/workItemsApi';
import { useRestoreWorkItem } from '../../hooks/mutations';
import { deletionTexts } from '../../lib/cardTexts';
import { isDeleted } from '../../lib/deletion';
import {
  deletionRefusal,
  isStateNotAllowed,
  type DeletionRefusal,
} from '../../lib/deletionRefusals';
import { editableStates, initialState, typeById } from '../../lib/process';

/**
 * «Восстановить рабочий элемент?» (mod-005 US-4; design § 5): a form dialog of one field, the
 * state the item takes. It proposes the state held at deletion (BR-DR-10); an item with no marks
 * proposes its type's first state; any other state of the type — «Удалена» never — may be
 * chosen, with no reason asked. The dialog sends the state it shows. While the process is read
 * the field lists the preset alone, and with no preset to show it is a skeleton and the primary
 * waits; a failed read there is an error with «Повторить». A state the server refuses stands
 * under the field; any other failure stays above it with the dialog open.
 */
export function RestoreWorkItemDialog({
  organizationId,
  projectId,
  item: current,
  onRestored,
  onRefused,
  onClose,
}: {
  organizationId: string;
  projectId: string;
  /** The deleted item as the card holds it: its type, its `deletion` and its stored parent. */
  item: WorkItem;
  /** The item as stored: live, `deletion: null`. */
  onRestored: (restored: WorkItem) => void;
  /** `gone` or `stale`: the dialog closes and the card does the rest. */
  onRefused: (refusal: Exclude<DeletionRefusal, { kind: 'stays' }>) => void;
  onClose: () => void;
}) {
  // The item as it was when the dialog opened: the card turns live under the dialog the moment the
  // answer is cached, and the preset and the sentences must not change while it closes.
  const [item] = useState(current);
  const process = useProcessDefinition(organizationId);
  const mutation = useRestoreWorkItem(organizationId, projectId, item.identifier);
  const [chosen, setChosen] = useState<string | null>(null);
  const [alert, setAlert] = useState<string | null>(null);
  const [fieldError, setFieldError] = useState<string | undefined>(undefined);
  const sending = useRef(false);

  const type = process.data ? typeById(process.data.workItemTypes, item.type.id) : undefined;
  const offered: readonly WorkItemStateReference[] = type ? editableStates(type) : [];
  const held = item.deletion?.fromState;
  // The held state, when the type still has it; else (and for an item with no marks) the first.
  const preset: WorkItemStateReference | undefined =
    held && (type === undefined || offered.some((state) => state.id === held.id))
      ? held
      : type && initialState(type);
  const options: readonly WorkItemStateReference[] = type ? offered : preset ? [preset] : [];
  const value = chosen ?? preset?.id ?? '';
  const shown = options.find((state) => state.id === value);
  const reading = !preset && (process.isPending || (process.isError && process.isFetching));
  const failed = !preset && process.isError && !process.isFetching;

  async function confirm() {
    if (sending.current || value === '') {
      return;
    }
    sending.current = true;
    setAlert(null);
    setFieldError(undefined);
    try {
      onRestored(await mutation.mutateAsync(value));
    } catch (caught) {
      if (isStateNotAllowed(caught)) {
        setFieldError(apiErrorText(caught));
        return;
      }
      const refusal = deletionRefusal(caught);
      if (refusal.kind === 'stays') {
        setAlert(refusal.text);
      } else {
        onRefused(refusal);
      }
    } finally {
      sending.current = false;
    }
  }

  const texts = deletionTexts.restoreDialog;
  const parent = item.parent !== null && isDeleted(item.parent) ? item.parent : null;
  return (
    <FormDialog
      open
      title={texts.title}
      submitLabel={texts.confirm}
      busy={mutation.isPending}
      error={alert}
      submitDisabled={value === '' || reading || failed}
      onSubmit={() => void confirm()}
      onClose={onClose}
    >
      <Typography variant="body1">{texts.text(item.identifier)}</Typography>
      {parent && (
        <Typography variant="body2" sx={{ color: 'text.secondary' }}>
          {texts.deletedParent(parent.identifier)}
        </Typography>
      )}
      {reading ? (
        <FieldsSkeleton count={1} />
      ) : failed ? (
        <InlineNotice
          severity="error"
          action={{ label: 'Повторить', onClick: () => void process.refetch() }}
        >
          {apiErrorText(process.error)}
        </InlineNotice>
      ) : (
        <InlineSelectField<string>
          label={texts.field}
          frame="always"
          value={value}
          options={options.map((state) => ({
            value: state.id,
            label: state.name,
            startAdornment: <CategoryMarker code={state.category.code} size="field" />,
          }))}
          onChange={(next) => {
            if (next !== '') {
              setChosen(next);
              setFieldError(undefined);
            }
          }}
          startAdornment={
            shown ? <CategoryMarker code={shown.category.code} size="field" /> : undefined
          }
          renderValue={(option) => option.label}
          hint={held && value === held.id ? texts.hint : undefined}
          error={fieldError}
          busy={mutation.isPending}
        />
      )}
    </FormDialog>
  );
}
