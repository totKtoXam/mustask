import { useMemo, useState, type Ref, memo } from 'react';
import Box from '@mui/material/Box';
import ListItemText from '@mui/material/ListItemText';
import Typography from '@mui/material/Typography';
import { PersonField, UserAvatar } from '../../../../shared/ui';
import type { WorkItemAssignee } from '../../api/workItemsApi';
import { useAssigneeCandidates } from '../../hooks/queries';
import { useDebouncedValue } from '../../hooks/useDebouncedValue';
import { UNASSIGNED_LABEL } from '../../lib/assignee';
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
} from '../../lib/assigneeOptions';
import { cardTexts } from '../../lib/cardTexts';

/**
 * «Исполнитель» of the card's header (US-4; mod-003 spec § Pickers): the kit's
 * `PersonField` over the organization's assignee candidates (api-contract
 * § 2.5), «Не назначен» always first. Its label is not drawn — the avatar and
 * the name are the value — but it names the combobox. Hints (too short, searching,
 * nobody, the first 20 only, a failed search) are lines that cannot be chosen,
 * and the list is the search's answer as it is; the field never filters it again.
 */
function AssigneeFieldView({
  organizationId,
  value,
  error,
  busy,
  readOnly,
  onChange,
  inputRef,
}: {
  organizationId: string;
  value: WorkItemAssignee | null;
  error: string | undefined;
  busy: boolean;
  readOnly: boolean;
  onChange: (assignee: WorkItemAssignee | null) => void;
  inputRef?: Ref<HTMLInputElement>;
}) {
  // One object per value: MUI resets the typed text whenever `value` is a new
  // object, so a fresh option on every render would wipe each keystroke.
  const selected = useMemo(() => (value === null ? null : optionOf(value)), [value]);
  const [inputValue, setInputValue] = useState(selected === null ? '' : optionLabel(selected));
  const [typed, setTyped] = useState('');
  // A field that turns read-only (the role lost the write code, mod-016 design § 7) shows
  // its value, not the text a search was typed with.
  const [wasReadOnly, setWasReadOnly] = useState(readOnly);
  if (readOnly !== wasReadOnly) {
    setWasReadOnly(readOnly);
    if (readOnly) {
      setInputValue(selected === null ? '' : optionLabel(selected));
      setTyped('');
    }
  }
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
  const options =
    selected === null || shown.some((option) => sameOption(option, selected))
      ? shown
      : [...shown, selected];

  return (
    <PersonField<AssigneeOption>
      label={cardTexts.assignee.label}
      labelPlacement="hidden"
      emptyText={UNASSIGNED_LABEL}
      options={options}
      value={selected}
      onChange={(option) => {
        if (option?.kind === 'none') {
          onChange(null);
        } else if (option?.kind === 'user') {
          onChange(option.user);
        }
      }}
      getOptionLabel={optionLabel}
      getOptionKey={optionKey}
      getOptionDisabled={(option) => option.kind === 'hint'}
      isOptionEqualToValue={sameOption}
      filterOptions={() => shown}
      inputValue={inputValue}
      onInputChange={(next, reason) => {
        setInputValue(next);
        setTyped(reason === 'input' ? next : '');
      }}
      renderOption={(option) => <AssigneeLine option={option} />}
      error={error}
      busy={busy}
      readOnly={readOnly}
      ref={inputRef}
    />
  );
}

function AssigneeLine({ option }: { option: AssigneeOption }) {
  switch (option.kind) {
    case 'none':
      return <>{optionLabel(option)}</>;
    case 'hint':
      return (
        <Typography variant="body2" sx={{ color: 'text.secondary' }}>
          {option.text}
        </Typography>
      );
    case 'user':
      return (
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, minWidth: 0 }}>
          <UserAvatar name={optionLabel(option)} />
          <ListItemText
            primary={optionLabel(option)}
            secondary={option.user.email || undefined}
            sx={{ my: 0 }}
          />
        </Box>
      );
  }
}

export const AssigneeField = memo(AssigneeFieldView);
