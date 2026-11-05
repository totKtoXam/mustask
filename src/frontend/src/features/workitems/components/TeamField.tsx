import { memo, type Ref } from 'react';
import { InlinePickerField } from '../../../shared/ui';
import type { WorkItemTeam } from '../api/workItemsApi';
import { NO_TEAM_OPTION, teamOptions, teamTexts, type TeamOption } from '../lib/teams';

export interface TeamFieldProps {
  /** The item's owning team; `null` is «Без команды», which is a value and is selected. */
  value: WorkItemTeam | null;
  /** The project's teams as the read gives them, by name; `undefined` until they are read. */
  teams: readonly { id: string; name: string }[] | undefined;
  /** The list says «Загрузка…» (the read is on its way). */
  loading?: boolean;
  /** The list says it could not load the teams; opening it again reads again. */
  failed?: boolean;
  /** The list opens: the card reads the teams now. */
  onOpen?: () => void;
  onChange: (team: WorkItemTeam | null) => void;
  /** The card's `start` (label at the left) or the dialog's `top`. */
  labelPlacement?: 'top' | 'start';
  /** The card's `hover` or the dialog's `always`. */
  frame?: 'hover' | 'always';
  error?: string;
  busy?: boolean;
  readOnly?: boolean;
  inputRef?: Ref<HTMLInputElement>;
  /** The caption under the dialog's field is linked to the combobox. */
  describedBy?: string;
}

/**
 * «Команда» (mod-017 US-1, US-2; design §§ 3, 4): the kit's `InlinePickerField` over the
 * project's teams, «Без команды» first. The value is never empty — the item has a team or it
 * has «Без команды», and the list marks the one that stands. Typing narrows the list; no
 * match says «Ничего не найдено». The card reads the teams when the list first opens
 * (`onOpen`); the dialog has read them already and passes them in.
 */
function TeamFieldView({
  value,
  teams,
  loading = false,
  failed = false,
  onOpen,
  onChange,
  labelPlacement = 'start',
  frame = 'hover',
  error,
  busy = false,
  readOnly = false,
  inputRef,
  describedBy,
}: TeamFieldProps) {
  const options = teams ? teamOptions(teams) : [];
  const selected: TeamOption = value === null ? NO_TEAM_OPTION : value;

  return (
    <InlinePickerField<TeamOption>
      label={teamTexts.label}
      labelPlacement={labelPlacement}
      frame={frame}
      options={options}
      value={selected}
      onChange={(team) => {
        if (team !== null) {
          onChange(team.id === null ? null : { id: team.id, name: team.name });
        }
      }}
      onOpen={onOpen}
      getOptionLabel={(team) => team.name}
      getOptionKey={(team) => team.id ?? 'none'}
      isOptionEqualToValue={(option, chosen) => option.id === chosen.id}
      loading={loading}
      loadingText={teamTexts.loading}
      noOptionsText={failed ? teamTexts.failed : teamTexts.noMatch}
      error={error}
      busy={busy}
      readOnly={readOnly}
      inputProps={describedBy ? { 'aria-describedby': describedBy } : undefined}
      ref={inputRef}
    />
  );
}

export const TeamField = memo(TeamFieldView);
