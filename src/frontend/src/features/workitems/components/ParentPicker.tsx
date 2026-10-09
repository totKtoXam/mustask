import { useMemo, useState } from 'react';
import Autocomplete from '@mui/material/Autocomplete';
import Box from '@mui/material/Box';
import InputAdornment from '@mui/material/InputAdornment';
import ListItemText from '@mui/material/ListItemText';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import { TypeMark } from '../../../shared/ui/WorkTrackingMarks';
import type { WorkItemRef } from '../api/workItemsApi';
import { useParentCandidates } from '../hooks/queries';
import { useDebouncedValue } from '../hooks/useDebouncedValue';
import {
  parentOptionKey,
  parentOptionLabel,
  parentOptionOf,
  parentOptions,
  sameParentOption,
  type ParentOption,
  type ParentSearch,
} from '../lib/parentOptions';
import { StateName } from './Marks';

/**
 * «Родитель» (feat-001 US-1; spec § Screens and copy): an `Autocomplete` that
 * searches this item's parent candidates as the user types (api-contract
 * § 3.4) — from an empty text, which asks for the 20 most recently changed —
 * «Нет родителя» first. Nothing is asked until the list first opens. Hints —
 * searching, nothing found, the first 20 only, a failed search — are lines
 * that cannot be chosen. The list is the search's answer as it is; the field
 * never filters it again. The assignee picker's pattern.
 *
 * The backlog's dialog «Изменить родителя» (feat-001 US-5) takes it without
 * «Нет родителя» (`offerNone={false}`; an empty value then reads as an empty
 * field) and searches only the level directly above (`levelAbove`, § 3.4);
 * the value is kept whatever its level.
 */
export function ParentPicker({
  organizationId,
  projectId,
  identifier,
  value,
  disabled,
  error,
  helperText,
  levelAbove = false,
  offerNone = true,
  autoFocus = false,
  onChange,
}: {
  organizationId: string;
  projectId: string;
  /** The item whose parent is chosen: the search is its own. */
  identifier: string;
  value: WorkItemRef | null;
  disabled: boolean;
  error: string | undefined;
  /** «Родителем может быть: …», shown while there is no error. */
  helperText: string | null;
  /** feat-001 § 3.4 `levelAbove=true`: only the level directly above the item's own. */
  levelAbove?: boolean;
  /** Whether «Нет родителя» is offered; without it, no value is an empty field. */
  offerNone?: boolean;
  /** The field takes focus when it mounts: a dialog's first field (mod-015 § 9.5). */
  autoFocus?: boolean;
  onChange(parent: WorkItemRef | null): void;
}) {
  const labelOf = (option: ParentOption) =>
    option.kind === 'none' && !offerNone ? '' : parentOptionLabel(option);
  // One object per value: MUI resets the typed text whenever `value` is a new
  // object, so a fresh option on every render would wipe each keystroke.
  const selected = useMemo(() => parentOptionOf(value), [value]);
  const [inputValue, setInputValue] = useState(labelOf(selected));
  const [typed, setTyped] = useState('');
  const [opened, setOpened] = useState(false);
  const text = typed.trim();
  const settled = useDebouncedValue(text);
  const query = useParentCandidates(organizationId, projectId, identifier, settled, {
    enabled: opened,
    levelAbove,
  });

  let search: ParentSearch;
  if (settled !== text || query.isPending || (query.isError && query.isFetching)) {
    search = { status: 'searching' };
  } else if (query.isError) {
    search = { status: 'failed' };
  } else {
    search = { status: 'done', result: query.data };
  }
  const shown = parentOptions(search, { none: offerNone });
  // The chosen item stays among the options MUI knows even while the search
  // shows others, so the field keeps its value; only `shown` is listed.
  const options = shown.some((option) => sameParentOption(option, selected))
    ? shown
    : [...shown, selected];

  return (
    <Autocomplete<ParentOption, false, true>
      options={options}
      value={selected}
      inputValue={inputValue}
      disabled={disabled}
      disableClearable
      filterOptions={() => shown}
      onOpen={() => {
        setOpened(true);
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
        } else if (option.kind === 'item') {
          onChange(option.item);
        }
      }}
      getOptionLabel={labelOf}
      getOptionKey={parentOptionKey}
      getOptionDisabled={(option) => option.kind === 'hint'}
      isOptionEqualToValue={sameParentOption}
      sx={(theme) => ({
        flex: '1 1 auto',
        minWidth: theme.spacing(32),
        maxWidth: theme.layout.formColumnWidth,
      })}
      renderOption={(props, option) => {
        const { key, ...rest } = props;
        return (
          <li key={key} {...rest}>
            <ParentLine option={option} />
          </li>
        );
      }}
      renderInput={(params) => (
        <TextField
          {...params}
          label="Родитель"
          size="small"
          autoFocus={autoFocus}
          error={error !== undefined}
          helperText={error ?? helperText ?? undefined}
          slotProps={{
            ...params.slotProps,
            input: {
              ...params.slotProps.input,
              // The value reads as an option: the type's icon before «<ID> <Заголовок>».
              startAdornment:
                value !== null ? (
                  <InputAdornment position="start">
                    <TypeMark appearance={value.type.appearance} />
                  </InputAdornment>
                ) : undefined,
            },
          }}
        />
      )}
    />
  );
}

/** A line of the list: the type's icon, «<ID> <Заголовок>», the state as secondary text. */
function ParentLine({ option }: { option: ParentOption }) {
  switch (option.kind) {
    case 'none':
      return <>{parentOptionLabel(option)}</>;
    case 'hint':
      return (
        <Typography variant="body2" color="text.secondary">
          {option.text}
        </Typography>
      );
    case 'item':
      return (
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, minWidth: 0 }}>
          <TypeMark appearance={option.item.type.appearance} />
          <ListItemText
            primary={parentOptionLabel(option)}
            secondary={<StateName state={option.item.state} />}
            slotProps={{ secondary: { component: 'span', sx: { display: 'block' } } }}
            sx={{ my: 0 }}
          />
        </Box>
      );
  }
}
