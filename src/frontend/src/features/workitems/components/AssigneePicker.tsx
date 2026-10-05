import { useMemo, useState } from 'react';
import Autocomplete from '@mui/material/Autocomplete';
import Box from '@mui/material/Box';
import ListItemText from '@mui/material/ListItemText';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import type { WorkItemAssignee } from '../api/workItemsApi';
import { useAssigneeCandidates } from '../hooks/queries';
import { useDebouncedValue } from '../hooks/useDebouncedValue';
import {
  assigneeOptions,
  isSearchable,
  optionKey,
  optionLabel,
  optionOf,
  sameOption,
  searchTextOf,
  type AssigneeOption,
  type AssigneeSearch,
} from '../lib/assigneeOptions';
import { UserAvatar } from './Marks';

/**
 * «Исполнитель» (US-4; spec § Pickers): an `Autocomplete` that searches the
 * organization's assignee candidates as the user types (api-contract § 2.5),
 * «Не назначен» always first. Hints — too short, searching, nobody, the first
 * 20 only, a failed search — are lines that cannot be chosen. The list is the
 * search's answer as it is; the field never filters it again.
 */
export function AssigneePicker({
  organizationId,
  value,
  disabled,
  error,
  onChange,
}: {
  organizationId: string;
  value: WorkItemAssignee | null;
  disabled: boolean;
  error: string | undefined;
  onChange(assignee: WorkItemAssignee | null): void;
}) {
  // One object per value: MUI resets the typed text whenever `value` is a new
  // object, so a fresh option on every render would wipe each keystroke.
  const selected = useMemo(() => optionOf(value), [value]);
  const [inputValue, setInputValue] = useState(optionLabel(selected));
  const [typed, setTyped] = useState('');
  const text = searchTextOf(typed);
  const settled = useDebouncedValue(text);
  const query = useAssigneeCandidates(organizationId, settled, isSearchable(settled));

  let search: AssigneeSearch;
  if (!isSearchable(text)) {
    search = { status: 'tooShort' };
  } else if (settled !== text || query.isPending || (query.isError && query.isFetching)) {
    search = { status: 'searching' };
  } else if (query.isError) {
    search = { status: 'failed' };
  } else {
    search = { status: 'done', result: query.data };
  }
  const shown = assigneeOptions(search);
  // The chosen user stays among the options MUI knows even while the search
  // shows others, so the field keeps its value; only `shown` is listed.
  const options = shown.some((option) => sameOption(option, selected))
    ? shown
    : [...shown, selected];

  return (
    <Autocomplete<AssigneeOption, false, true>
      options={options}
      value={selected}
      inputValue={inputValue}
      disabled={disabled}
      disableClearable
      filterOptions={() => shown}
      onOpen={() => {
        if (query.isError) {
          void query.refetch();
        }
      }}
      onInputChange={(_event, next, reason) => {
        setInputValue(next);
        setTyped(reason === 'input' ? next : '');
      }}
      onChange={(_event, option) => {
        if (option.kind === 'none') {
          onChange(null);
        } else if (option.kind === 'user') {
          onChange(option.user);
        }
      }}
      getOptionLabel={optionLabel}
      getOptionKey={optionKey}
      getOptionDisabled={(option) => option.kind === 'hint'}
      isOptionEqualToValue={sameOption}
      sx={(theme) => ({ minWidth: theme.spacing(32) })}
      renderOption={(props, option) => {
        const { key, ...rest } = props;
        return (
          <li key={key} {...rest}>
            <AssigneeLine option={option} />
          </li>
        );
      }}
      renderInput={(params) => (
        <TextField
          {...params}
          label="Исполнитель"
          size="small"
          error={error !== undefined}
          helperText={error}
        />
      )}
    />
  );
}

function AssigneeLine({ option }: { option: AssigneeOption }) {
  switch (option.kind) {
    case 'none':
      return <>{optionLabel(option)}</>;
    case 'hint':
      return (
        <Typography variant="body2" color="text.secondary">
          {option.text}
        </Typography>
      );
    case 'user':
      return (
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, minWidth: 0 }}>
          <UserAvatar name={option.user.displayName} />
          <ListItemText
            primary={optionLabel(option)}
            secondary={option.user.email || undefined}
            sx={{ my: 0 }}
          />
        </Box>
      );
  }
}
