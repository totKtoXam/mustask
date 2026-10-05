import MenuItem from '@mui/material/MenuItem';
import TextField from '@mui/material/TextField';
import type { WorkItemStateReference } from '../api/workItemsApi';
import { StateName } from './Marks';

/**
 * «Статус» (US-5; spec § Pickers): the type's states in the seeded order
 * without «Удалена», each with its category dot, the current one selected.
 * Any state is reachable from any other, forwards or backwards (BR-M3-21);
 * the choice is saved with «Сохранить», like every field.
 */
export function StatePicker({
  states,
  value,
  disabled,
  error,
  onChange,
}: {
  states: readonly WorkItemStateReference[];
  value: string;
  disabled: boolean;
  error: string | undefined;
  onChange(stateId: string): void;
}) {
  return (
    <TextField
      select
      label="Статус"
      size="small"
      value={value}
      disabled={disabled}
      error={error !== undefined}
      helperText={error}
      onChange={(event) => onChange(event.target.value)}
      sx={(theme) => ({ minWidth: theme.spacing(24) })}
      slotProps={{
        select: {
          renderValue: (selected) => {
            const state = states.find((candidate) => candidate.id === selected);
            return state ? <StateName state={state} /> : null;
          },
        },
      }}
    >
      {states.map((state) => (
        <MenuItem key={state.id} value={state.id}>
          <StateName state={state} />
        </MenuItem>
      ))}
    </TextField>
  );
}
