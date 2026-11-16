import { useState } from 'react';
import Box from '@mui/material/Box';
import { FormDialog, InlineDateField, InlineTextField } from '../../../shared/ui';
import type { SprintRow } from '../api/iterationsApi';
import { useCreateSprint, useUpdateSprint } from '../hooks/mutations';
import { placeSprintRefusal, type SprintRefusal } from '../lib/refusals';
import { sprintTexts } from '../lib/sprintTexts';

/** What the dialog is: a new sprint, an edit of a Planned or Active one, or the rename of a Closed one. */
export type SprintFormMode = 'create' | 'edit' | 'rename';

interface Errors {
  name?: string;
  dates?: string;
}

const NAME_MAX = 100;

/**
 * «Новый спринт» / «Изменить спринт» / «Переименовать спринт» (design § 5.1, § 5.2): the name and
 * the two dates. The browser checks what the contract checks first (1–100 after trimming, both
 * dates or neither, an Active sprint keeps both); a refusal of the server stands under its field,
 * any other at the top, the dialog stays and the typed values stay. Mounted per opening.
 */
export function SprintFormDialog({
  organizationId,
  teamId,
  mode,
  sprint,
  onDone,
  onClose,
}: {
  organizationId: string;
  teamId: string;
  mode: SprintFormMode;
  /** The sprint edited; absent on a create. */
  sprint?: SprintRow;
  /** The write was accepted: the host shows the notice and reads the list again. */
  onDone(text: string): void;
  /** `refusal`: the server said the page is stale (team or sprint gone, code lost). */
  onClose(refusal: SprintRefusal | null): void;
}) {
  const create = useCreateSprint(organizationId, teamId);
  const update = useUpdateSprint(organizationId, teamId);
  const [name, setName] = useState(mode === 'create' ? '' : (sprint?.name ?? ''));
  const [start, setStart] = useState(sprint?.startDate ?? '');
  const [end, setEnd] = useState(sprint?.endDate ?? '');
  const [errors, setErrors] = useState<Errors>({});
  const [alert, setAlert] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [stale, setStale] = useState<SprintRefusal | null>(null);
  const active = sprint?.state === 'Active';

  function check(): Errors {
    const found: Errors = {};
    const trimmed = name.trim();
    if (trimmed.length > NAME_MAX || (trimmed.length === 0 && mode !== 'create')) {
      found.name = sprintTexts.form.nameInvalid;
    }
    if (mode !== 'rename') {
      if ((start === '') !== (end === '')) {
        found.dates = active
          ? sprintTexts.form.activeDatesRequired
          : sprintTexts.form.datesUnpaired;
      } else if (active && start === '') {
        found.dates = sprintTexts.form.activeDatesRequired;
      } else if (start !== '' && end < start) {
        found.dates = sprintTexts.form.datesReversed;
      }
    }
    return found;
  }

  async function submit() {
    const found = check();
    if (found.name || found.dates) {
      setErrors(found);
      return;
    }
    setBusy(true);
    setAlert(null);
    setErrors({});
    const body = {
      name: name.trim() === '' ? null : name.trim(),
      startDate: start === '' ? null : start,
      endDate: end === '' ? null : end,
    };
    try {
      if (mode === 'create') {
        await create.mutateAsync(body);
        onDone(sprintTexts.notices.created);
      } else if (sprint) {
        await update.mutateAsync({ sprintId: sprint.id, body: { ...body, name: name.trim() } });
        onDone(mode === 'rename' ? sprintTexts.notices.renamed : sprintTexts.notices.updated);
      }
    } catch (error) {
      const refusal = placeSprintRefusal(error);
      if (refusal.teamGone || refusal.forbidden || refusal.sprintGone) {
        setStale(refusal);
      }
      if (refusal.field === 'name') setErrors({ name: refusal.text });
      else if (refusal.field === 'dates') setErrors({ dates: refusal.text });
      else setAlert(refusal.text);
      setBusy(false);
    }
  }

  const title =
    mode === 'create'
      ? sprintTexts.form.createTitle
      : mode === 'rename'
        ? sprintTexts.form.renameTitle
        : sprintTexts.form.editTitle;

  return (
    <FormDialog
      open
      title={title}
      submitLabel={mode === 'create' ? sprintTexts.form.create : sprintTexts.form.save}
      busy={busy}
      error={alert}
      onSubmit={() => void submit()}
      onClose={() => onClose(stale)}
    >
      <InlineTextField
        label={sprintTexts.form.name}
        frame="always"
        hint={
          mode === 'create'
            ? sprintTexts.form.nameHintCreate
            : sprint
              ? sprintTexts.form.nameHint(sprint.number)
              : undefined
        }
        value={name}
        busy={busy}
        error={errors.name}
        onChange={(next) => {
          setName(next);
          setErrors((current) => ({ ...current, name: undefined }));
        }}
      />
      {mode !== 'rename' && (
        <>
          <Box
            sx={{
              display: 'grid',
              gap: 2,
              gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' },
            }}
          >
            <InlineDateField
              label={sprintTexts.form.startDate}
              frame="always"
              value={start}
              busy={busy}
              onChange={(next) => {
                setStart(next);
                setErrors((current) => ({ ...current, dates: undefined }));
              }}
            />
            <InlineDateField
              label={sprintTexts.form.endDate}
              frame="always"
              value={end}
              busy={busy}
              error={errors.dates}
              hint={active ? sprintTexts.form.activeDatesHint : undefined}
              onChange={(next) => {
                setEnd(next);
                setErrors((current) => ({ ...current, dates: undefined }));
              }}
            />
          </Box>
        </>
      )}
    </FormDialog>
  );
}
