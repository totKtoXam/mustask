import { useState } from 'react';
import Typography from '@mui/material/Typography';
import { iterationErrorCodes } from '../../../shared/api/errorCodes';
import { formatDate } from '../../../shared/lib/format';
import {
  FieldsSkeleton,
  FormDialog,
  InlineNotice,
  InlineSelectField,
  type SelectOption,
} from '../../../shared/ui';
import type { CloseDestination, SprintRow } from '../api/iterationsApi';
import { useCloseSprint } from '../hooks/mutations';
import { useClosePreview } from '../hooks/queries';
import { placeSprintRefusal, type SprintRefusal } from '../lib/refusals';
import { sprintTexts } from '../lib/sprintTexts';

/** The «Без спринта» option's value: a real choice, distinct from none chosen (`''`). */
const NO_SPRINT = 'none';

function optionLabel(destination: CloseDestination): string {
  return destination.startDate && destination.endDate
    ? `${destination.name} (${formatDate(destination.startDate)} – ${formatDate(destination.endDate)})`
    : destination.name;
}

/**
 * «Завершить спринт «<название>»» (design § 5.5, US-5): what moves and what stays, after
 * visibility, and where the unfinished items go. The destination is required; the Planned sprint
 * with the lowest number is preselected, or nothing when the team has none. The preview is read
 * when the dialog opens; closing is one operation on the server.
 */
export function CloseSprintDialog({
  organizationId,
  teamId,
  sprint,
  onDone,
  onClose,
}: {
  organizationId: string;
  teamId: string;
  sprint: SprintRow;
  onDone(text: string): void;
  onClose(refusal: SprintRefusal | null): void;
}) {
  const preview = useClosePreview(organizationId, teamId, sprint.id);
  const close = useCloseSprint(organizationId, teamId);
  // `null`: the user has not chosen yet, so the preselection shows; `''`: the choice was cleared.
  const [chosen, setChosen] = useState<string | null>(null);
  const [fieldError, setFieldError] = useState<string | undefined>();
  const [alert, setAlert] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const data = preview.data;
  const destinations = data?.destinations ?? [];
  const choice = chosen ?? destinations[0]?.id ?? '';
  const options: SelectOption<string>[] = [
    ...destinations.map((destination) => ({
      value: destination.id,
      label: optionLabel(destination),
    })),
    { value: NO_SPRINT, label: sprintTexts.close.noSprint },
  ];

  async function submit() {
    if (choice === '') return;
    setBusy(true);
    setAlert(null);
    setFieldError(undefined);
    try {
      await close.mutateAsync({
        sprintId: sprint.id,
        destinationSprintId: choice === NO_SPRINT ? null : choice,
      });
      onDone(sprintTexts.notices.closed);
    } catch (error) {
      const refusal = placeSprintRefusal(error);
      setBusy(false);
      if (refusal.teamGone || refusal.forbidden || refusal.code === iterationErrorCodes.notActive) {
        onClose(refusal);
      } else if (refusal.code === iterationErrorCodes.notFound) {
        // The destination (or the sprint) names nothing: the options are read again and the
        // choice is cleared; a sprint that is gone itself shows in that read and closes the dialog.
        setFieldError(refusal.text);
        setChosen('');
        const result = await preview.refetch();
        if (result.isError) onClose(placeSprintRefusal(result.error));
      } else if (refusal.field === 'destination') {
        setFieldError(refusal.text);
      } else {
        setAlert(refusal.text);
      }
    }
  }

  const loading = preview.isPending || (preview.isError && preview.isFetching);

  return (
    <FormDialog
      open
      title={sprintTexts.close.title(sprint.name)}
      submitLabel={busy ? sprintTexts.close.busy : sprintTexts.close.submit}
      busy={busy}
      error={alert}
      submitDisabled={!data || choice === ''}
      onSubmit={() => void submit()}
      onClose={() => onClose(null)}
    >
      {loading ? (
        <FieldsSkeleton count={2} />
      ) : preview.isError ? (
        <InlineNotice
          severity="error"
          action={{ label: sprintTexts.close.retry, onClick: () => void preview.refetch() }}
        >
          {sprintTexts.close.optionsFailed}
        </InlineNotice>
      ) : (
        data && (
          <>
            <Typography variant="body1">
              {data.moving.items === 0
                ? sprintTexts.close.nothingToMove
                : sprintTexts.close.moving(data.moving.items)}
            </Typography>
            <Typography variant="body1">
              {sprintTexts.close.staying(data.staying.items, data.staying.storyPoints)}
            </Typography>
            <InlineSelectField<string>
              label={sprintTexts.close.destination}
              frame="always"
              required
              emptyText={sprintTexts.close.destinationEmpty}
              value={choice}
              options={options}
              busy={busy}
              error={fieldError}
              onChange={(next) => {
                setChosen(next);
                setFieldError(undefined);
              }}
            />
            <Typography variant="body2" sx={{ color: 'text.secondary' }}>
              {sprintTexts.close.irreversible}
            </Typography>
          </>
        )
      )}
    </FormDialog>
  );
}
