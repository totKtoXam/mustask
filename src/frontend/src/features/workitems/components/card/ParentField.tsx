import { useMemo, useState, type MouseEvent, type Ref, memo } from 'react';
import { Link as RouterLink } from 'react-router-dom';
import Box from '@mui/material/Box';
import Link from '@mui/material/Link';
import ListItemText from '@mui/material/ListItemText';
import Typography from '@mui/material/Typography';
import OpenInNewOutlinedIcon from '@mui/icons-material/OpenInNewOutlined';
import { InlinePickerField, StateLabel, TypeMark } from '../../../../shared/ui';
import type { WorkItemRef } from '../../api/workItemsApi';
import { useParentCandidates } from '../../hooks/queries';
import { useDebouncedValue } from '../../hooks/useDebouncedValue';
import { cardTexts } from '../../lib/cardTexts';
import {
  parentOptionKey,
  parentOptionLabel,
  parentOptionOf,
  parentOptions,
  sameParentOption,
  type ParentOption,
  type ParentSearch,
} from '../../lib/parentOptions';
import { isPlainClick } from '../../lib/plainClick';
import { workItemPath } from '../../paths';

/**
 * «Родитель» (feat-001 US-1; spec § Screens and copy): the composition tech-015
 * names and this slice owns — the kit's `InlinePickerField` over this item's
 * parent candidates as the user types (api-contract § 3.4), from an empty text,
 * which asks for the 20 most recently changed, «Нет родителя» first. Nothing is
 * asked until the list first opens. Hints — searching, nothing found, the first
 * 20 only, a failed search — are lines that cannot be chosen; the list is the
 * search's answer as it is.
 *
 * The value reads as the linked item: its type's icon before «<ID> <Заголовок>»,
 * its state and «Открыть <ID>» after it. That link is `endAction`: a plain click
 * calls `onOpenItem` when the host has one (the dialog switches to the item,
 * BR-C-5), otherwise it follows the page's address.
 */
function ParentFieldView({
  organizationId,
  projectId,
  identifier,
  value,
  stored,
  hint,
  error,
  busy,
  readOnly,
  offerNone = true,
  onChange,
  onOpenItem,
  inputRef,
}: {
  organizationId: string;
  projectId: string;
  /** The item whose parent is chosen: the search is its own. */
  identifier: string;
  /** The draft's parent: what the field shows. */
  value: WorkItemRef | null;
  /** The parent as stored: what «Открыть» opens. */
  stored: WorkItemRef | null;
  /** «Родителем может быть: …», on focus. */
  hint: string | null;
  error: string | undefined;
  busy: boolean;
  readOnly: boolean;
  /** Whether «Нет родителя» is offered. */
  offerNone?: boolean;
  onChange: (parent: WorkItemRef | null) => void;
  onOpenItem?: (identifier: string) => void;
  inputRef?: Ref<HTMLInputElement>;
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
    <InlinePickerField<ParentOption>
      label={cardTexts.parent.label}
      options={options}
      value={selected}
      onChange={(option) => {
        if (option?.kind === 'none') {
          onChange(null);
        } else if (option?.kind === 'item') {
          onChange(option.item);
        }
      }}
      getOptionLabel={labelOf}
      getOptionKey={parentOptionKey}
      getOptionDisabled={(option) => option.kind === 'hint'}
      isOptionEqualToValue={sameParentOption}
      filterOptions={() => shown}
      inputValue={inputValue}
      onInputChange={(next, reason) => {
        setInputValue(next);
        setTyped(reason === 'input' ? next : '');
      }}
      onOpen={() => {
        setOpened(true);
        if (query.isError) {
          void query.refetch();
        }
      }}
      renderOption={(option) => <ParentLine option={option} />}
      startAdornment={
        value !== null ? <TypeMark appearance={value.type.appearance} compact /> : undefined
      }
      endAction={
        value !== null || stored !== null ? (
          <Box component="span" sx={{ display: 'inline-flex', alignItems: 'center', gap: 1 }}>
            {value !== null && (
              <StateLabel categoryCode={value.state.category.code} name={value.state.name} clip />
            )}
            {stored !== null && (
              <OpenLink
                organizationId={organizationId}
                projectId={projectId}
                target={stored.identifier}
                onOpenItem={onOpenItem}
              />
            )}
          </Box>
        ) : undefined
      }
      hint={hint ?? undefined}
      error={error}
      busy={busy}
      readOnly={readOnly}
      ref={inputRef}
    />
  );
}

/** «Открыть <ID>»: an icon link to the item's page; the dialog switches to it instead. */
export function OpenLink({
  organizationId,
  projectId,
  target,
  onOpenItem,
}: {
  organizationId: string;
  projectId: string;
  target: string;
  onOpenItem?: (identifier: string) => void;
}) {
  return (
    <Link
      component={RouterLink}
      to={workItemPath(organizationId, projectId, target)}
      aria-label={`Открыть ${target}`}
      underline="none"
      sx={{ display: 'inline-flex', alignItems: 'center', color: 'text.primary' }}
      onClick={(event: MouseEvent<HTMLAnchorElement>) => {
        if (onOpenItem && isPlainClick(event)) {
          event.preventDefault();
          onOpenItem(target);
        }
      }}
    >
      <OpenInNewOutlinedIcon fontSize="small" />
    </Link>
  );
}

/** A line of the list: the type's icon, «<ID> <Заголовок>», the state as secondary text. */
function ParentLine({ option }: { option: ParentOption }) {
  switch (option.kind) {
    case 'none':
      return <>{parentOptionLabel(option)}</>;
    case 'hint':
      return (
        <Typography variant="body2" sx={{ color: 'text.secondary' }}>
          {option.text}
        </Typography>
      );
    case 'item':
      return (
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, minWidth: 0 }}>
          <TypeMark appearance={option.item.type.appearance} />
          <ListItemText
            primary={parentOptionLabel(option)}
            secondary={
              <StateLabel
                categoryCode={option.item.state.category.code}
                name={option.item.state.name}
              />
            }
            slotProps={{ secondary: { component: 'span', sx: { display: 'block' } } }}
            sx={{ my: 0 }}
          />
        </Box>
      );
  }
}

export const ParentField = memo(ParentFieldView);
