import { useMemo, useState } from 'react';
import Autocomplete from '@mui/material/Autocomplete';
import ListItemText from '@mui/material/ListItemText';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import type { UserCandidate } from '../api/membershipApi';
import { useUserSearch, type UserSearchScope } from '../hooks/useUserSearch';
import {
  USER_SEARCH_MAX_LENGTH,
  optionKey,
  optionLabel,
  sameOption,
  userOptions,
  type UserOption,
} from '../lib/userSearch';

export interface UserSearchFieldProps {
  /** «Пользователь» or «Участник организации». */
  label: string;
  scope: UserSearchScope;
  /** The chosen user; the field shows their name. */
  value: UserCandidate | null;
  onChange(user: UserCandidate | null): void;
  /** A refusal about the user, in the error colour; replaces the helper. */
  error?: string;
  /** The helper under the field while there is no refusal. */
  helperText?: string;
  disabled?: boolean;
  autoFocus?: boolean;
}

/**
 * A search for a user by name or e-mail (design § 3.5, § 4.4): the pattern of
 * «Исполнитель», in this slice because the picker's code belongs to another
 * (plan-frontend P-7, D-20 OP-3). An `Autocomplete` over the server's answer
 * as it is — the field never filters it again — with the hint lines of the
 * spec as options that cannot be chosen; the search starts at 2 trimmed
 * characters after a short pause; opening the list again retries a failed
 * search. The field is required: «Добавить» stays disabled until a user is
 * chosen, which the dialog decides.
 */
export function UserSearchField({
  label,
  scope,
  value,
  onChange,
  error,
  helperText,
  disabled = false,
  autoFocus = false,
}: UserSearchFieldProps) {
  // One object per value: MUI resets the typed text whenever `value` is a new
  // object, so a fresh option on every render would wipe each keystroke.
  const selected = useMemo<UserOption | null>(
    () => (value ? { kind: 'user', user: value } : null),
    [value],
  );
  const [inputValue, setInputValue] = useState(selected ? optionLabel(selected) : '');
  const [typed, setTyped] = useState('');
  const { search, retry } = useUserSearch(scope, typed);

  const shown = userOptions(search);
  // The chosen user stays among the options MUI knows even while the search
  // shows others, so the field keeps its value; only `shown` is listed.
  const options =
    selected === null || shown.some((option) => sameOption(option, selected))
      ? shown
      : [...shown, selected];

  return (
    <Autocomplete<UserOption, false, false>
      options={options}
      value={selected}
      inputValue={inputValue}
      disabled={disabled}
      filterOptions={() => shown}
      onOpen={retry}
      onInputChange={(_event, next, reason) => {
        setInputValue(next);
        setTyped(reason === 'input' ? next : '');
      }}
      onChange={(_event, option) => {
        onChange(option?.kind === 'user' ? option.user : null);
      }}
      getOptionLabel={optionLabel}
      getOptionKey={optionKey}
      getOptionDisabled={(option) => option.kind === 'hint'}
      isOptionEqualToValue={sameOption}
      renderOption={(props, option) => {
        const { key, ...rest } = props;
        return (
          <li key={key} {...rest}>
            {option.kind === 'user' ? (
              <ListItemText
                primary={option.user.displayName}
                secondary={option.user.email || undefined}
                sx={{ my: 0 }}
              />
            ) : (
              <Typography variant="body2" color="text.secondary">
                {option.text}
              </Typography>
            )}
          </li>
        );
      }}
      renderInput={(params) => (
        <TextField
          {...params}
          label={label}
          autoFocus={autoFocus}
          error={error !== undefined}
          helperText={error ?? helperText}
          slotProps={{
            ...params.slotProps,
            inputLabel: { ...params.slotProps.inputLabel, required: true },
            htmlInput: {
              ...params.slotProps.htmlInput,
              maxLength: USER_SEARCH_MAX_LENGTH,
              'aria-required': true,
              'aria-invalid': error !== undefined,
            },
          }}
        />
      )}
    />
  );
}
