import { memo, useId, useState, type Ref } from 'react';
import Box from '@mui/material/Box';
import { InlinePickerField } from '../../../../shared/ui';
import { useOpenSprints, type SprintState } from '../../../iterations';
import type { WorkItemSprint } from '../../api/workItemsApi';

/** The words of the card's «Спринт» (mod-007 design § 7), final there and nowhere else. */
const sprintFieldTexts = {
  label: 'Спринт',
  none: 'Без спринта',
  noTeam: 'Сначала выберите команду',
  loading: 'Загрузка…',
  noMatch: 'Ничего не найдено',
  failed: 'Не удалось загрузить спринты. Попробуйте ещё раз.',
  cleared: 'Спринт будет очищен: он принадлежит прежней команде.',
  active: (name: string) => `${name} · активен`,
  closed: (name: string) => `${name} · завершён`,
} as const;

/** One choice of the picker: a sprint of the team, or none (`id: null`). */
interface SprintOption {
  id: string | null;
  name: string;
  state: SprintState | null;
}

const NO_SPRINT_OPTION: SprintOption = { id: null, name: sprintFieldTexts.none, state: null };
const NO_TEAM_OPTION: SprintOption = {
  id: '__no-team',
  name: sprintFieldTexts.noTeam,
  state: null,
};

function labelOf(option: SprintOption): string {
  if (option.state === 'Active') return sprintFieldTexts.active(option.name);
  if (option.state === 'Closed') return sprintFieldTexts.closed(option.name);
  return option.name;
}

/**
 * «Спринт» of the band (mod-007 US-4; design § 7): row 3 of block B, under «Команда». The team's
 * Active and Planned sprints are read when the list first opens, «Без спринта» first; an Active one
 * is marked. A kept Closed sprint shows «… · завершён». With no owning team the field says so and
 * is read-only; when the team was changed under a sprint, the field shows «Без спринта» and a line
 * says the sprint will be cleared. A failed read is said in the list, and opening it again reads
 * again (so does a refusal of the save, at the next opening).
 */
function CardSprintFieldView({
  organizationId,
  teamId,
  value,
  cleared,
  error,
  busy,
  readOnly,
  onChange,
  inputRef,
}: {
  organizationId: string;
  /** The team the draft shows; `null` is «Без команды». */
  teamId: string | null;
  value: WorkItemSprint | null;
  /** The team was changed under a stored sprint: the line says it will be cleared. */
  cleared: boolean;
  error: string | undefined;
  busy: boolean;
  readOnly: boolean;
  onChange: (sprint: WorkItemSprint | null) => void;
  inputRef?: Ref<HTMLInputElement>;
}) {
  const captionId = useId();
  const [opened, setOpened] = useState(false);
  const sprints = useOpenSprints(organizationId, teamId, { enabled: opened });
  const failed = sprints.isError && !sprints.isFetching;

  const open: SprintOption[] = (sprints.data?.items ?? []).map((row) => ({
    id: row.id,
    name: row.name,
    state: row.state,
  }));
  const selected: SprintOption | null =
    teamId === null
      ? NO_TEAM_OPTION
      : value === null
        ? NO_SPRINT_OPTION
        : { id: value.id, name: value.name, state: value.state };
  // The list is empty until the read answers, so «Загрузка…» and the failure show in it, as «Команда»'s do.
  const options: SprintOption[] =
    teamId === null
      ? [NO_TEAM_OPTION]
      : sprints.isSuccess
        ? [
            NO_SPRINT_OPTION,
            ...open,
            // A kept Closed sprint is the stored value, so the list has it for the value to match.
            ...(selected?.id && selected.state === 'Closed' ? [selected] : []),
          ]
        : [];

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column' }}>
      <InlinePickerField<SprintOption>
        label={sprintFieldTexts.label}
        labelPlacement="start"
        options={options}
        value={selected}
        onChange={(option) => {
          if (option !== null && option !== NO_TEAM_OPTION) {
            onChange(
              option.id === null || option.state === null
                ? null
                : { id: option.id, name: option.name, state: option.state },
            );
          }
        }}
        onOpen={() => {
          setOpened(true);
          if (sprints.isError || (sprints.isSuccess && error !== undefined)) {
            void sprints.refetch();
          }
        }}
        getOptionLabel={labelOf}
        getOptionKey={(option) => option.id ?? 'none'}
        isOptionEqualToValue={(option, chosen) => option.id === chosen.id}
        loading={opened && teamId !== null && !sprints.isSuccess && !failed}
        loadingText={sprintFieldTexts.loading}
        noOptionsText={failed ? sprintFieldTexts.failed : sprintFieldTexts.noMatch}
        error={error}
        busy={busy}
        readOnly={readOnly || teamId === null}
        inputProps={cleared ? { 'aria-describedby': captionId } : undefined}
        ref={inputRef}
      />
      {cleared && (
        <Box id={captionId} sx={{ color: 'text.secondary', typography: 'caption', pl: 2 }}>
          {sprintFieldTexts.cleared}
        </Box>
      )}
    </Box>
  );
}

export const CardSprintField = memo(CardSprintFieldView);
