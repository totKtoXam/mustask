import { useId, useMemo, useRef, useState, type KeyboardEvent } from 'react';
import Box from '@mui/material/Box';
import ButtonBase from '@mui/material/ButtonBase';
import List from '@mui/material/List';
import ListItemButton from '@mui/material/ListItemButton';
import ListSubheader from '@mui/material/ListSubheader';
import Popover from '@mui/material/Popover';
import Typography from '@mui/material/Typography';
import CheckOutlinedIcon from '@mui/icons-material/CheckOutlined';
import { ThinChevronDownIcon } from '../icons/ThinIcons';
import { FieldsSkeleton } from '../feedback/FieldsSkeleton';
import { InlineNotice } from '../feedback/InlineNotice';
import { InlineTextField } from '../fields/InlineTextField';
import { InlineEmpty } from '../text/InlineEmpty';
import { TruncatedText } from '../text/TruncatedText';

/** One choice of a `SearchMenu`. */
export interface SearchMenuOption {
  id: string;
  label: string;
  /** A quiet second word beside the label, e.g. a project's key. */
  hint?: string;
}

/** A group of choices under a heading; `note` is a quiet line under the heading. */
export interface SearchMenuGroup {
  /** Absent: the group has no heading. */
  heading?: string;
  note?: string;
  options: readonly SearchMenuOption[];
}

export interface SearchMenuProps {
  /** The caption over the value, and the menu's name: «Проект». */
  label: string;
  /** What is chosen, as the host says it: «все» or a name. */
  value: string;
  /** Marks the trigger when something other than «все» is chosen. */
  chosen?: boolean;
  /** The id of the chosen option, so that it is marked and focused on open. */
  currentId?: string | null;
  /** The first row, choosing «no object»: `onSelect(null)`. Absent: the menu has none. */
  allLabel?: string;
  groups: readonly SearchMenuGroup[];
  /** The last row, outside the search («Все организации»): `onSelect(id)`. */
  footer?: SearchMenuOption;
  /** The search field's name and placeholder: «Найти…». */
  searchLabel: string;
  /** The words of a search with no result: «Ничего не найдено». */
  emptyText: string;
  /** `loading` shows a skeleton, `error` the notice with the retry. Default `ready`. */
  status?: 'ready' | 'loading' | 'error';
  errorText?: string;
  retryLabel?: string;
  onRetry?: () => void;
  /** The host does the choosing; `null` is the «все» row. The menu closes first. */
  onSelect: (id: string | null) => void;
}

const SKELETON_ROWS = 4;

function matches(option: SearchMenuOption, query: string): boolean {
  return `${option.label} ${option.hint ?? ''}`.toLocaleLowerCase('ru').includes(query);
}

/**
 * A header selector (feat-043 design § 3): a trigger of a caption over a value, opening a
 * panel with a search field, a «все» row, grouped options and states (loading, error with a
 * retry, no match). The trigger is a combobox over a labelled listbox; Arrow keys move,
 * Enter chooses, Escape closes and gives focus back to the trigger. Searching is local, by
 * the part of a name, in every group. The kit has it because a `Popover` may be imported
 * here only (KIT-3); it knows no business word — the host passes every text and every group.
 */
export function SearchMenu({
  label,
  value,
  chosen = false,
  currentId = null,
  allLabel,
  groups,
  footer,
  searchLabel,
  emptyText,
  status = 'ready',
  errorText,
  retryLabel,
  onRetry,
  onSelect,
}: SearchMenuProps) {
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);
  const [query, setQuery] = useState('');
  const listRef = useRef<HTMLUListElement | null>(null);
  const listId = useId();
  const headingBase = useId();
  const open = anchor !== null;

  const needle = query.trim().toLocaleLowerCase('ru');
  const shown = useMemo(
    () =>
      groups
        .map((group) => ({
          ...group,
          options: needle
            ? group.options.filter((option) => matches(option, needle))
            : group.options,
        }))
        .filter((group) => group.options.length > 0),
    [groups, needle],
  );

  function close() {
    setAnchor(null);
    setQuery('');
  }

  function choose(id: string | null) {
    close();
    onSelect(id);
  }

  function optionElements(): HTMLElement[] {
    return Array.from(listRef.current?.querySelectorAll<HTMLElement>('[role="option"]') ?? []);
  }

  function move(from: HTMLElement | null, step: 1 | -1 | 'first' | 'last') {
    const options = optionElements();
    if (options.length === 0) return;
    const at = from ? options.indexOf(from) : -1;
    const next =
      step === 'first'
        ? 0
        : step === 'last'
          ? options.length - 1
          : Math.min(options.length - 1, Math.max(0, at + step));
    options[next]?.focus();
  }

  function onPanelKeyDown(event: KeyboardEvent<HTMLElement>) {
    const target = event.target as HTMLElement;
    const onOption = target.getAttribute('role') === 'option';
    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault();
        move(onOption ? target : null, 1);
        break;
      case 'ArrowUp':
        event.preventDefault();
        if (onOption && optionElements()[0] === target) {
          // Back to the search field.
          (event.currentTarget.querySelector('input') as HTMLInputElement | null)?.focus();
        } else {
          move(onOption ? target : null, -1);
        }
        break;
      case 'Home':
        if (onOption) {
          event.preventDefault();
          move(null, 'first');
        }
        break;
      case 'End':
        if (onOption) {
          event.preventDefault();
          move(null, 'last');
        }
        break;
    }
  }

  const noMatch = status === 'ready' && needle !== '' && shown.length === 0;
  const nothingToShow = status === 'ready' && shown.length === 0 && !allLabel && !footer;

  function renderOption(option: SearchMenuOption) {
    const current = option.id === currentId;
    return (
      <ListItemButton
        key={option.id}
        role="option"
        aria-selected={current}
        selected={current}
        tabIndex={-1}
        onClick={() => choose(option.id)}
        sx={{ gap: 1 }}
      >
        <TruncatedText>{option.label}</TruncatedText>
        {option.hint && (
          <Typography variant="body2" component="span" sx={{ color: 'text.secondary' }}>
            {option.hint}
          </Typography>
        )}
        {current && <CheckOutlinedIcon fontSize="small" aria-hidden sx={{ ml: 'auto' }} />}
      </ListItemButton>
    );
  }

  return (
    <>
      <ButtonBase
        role="combobox"
        aria-label={`${label}: ${value}`}
        aria-haspopup="listbox"
        aria-expanded={open}
        aria-controls={open ? listId : undefined}
        data-chosen={chosen ? 'true' : undefined}
        onClick={(event) => setAnchor(event.currentTarget)}
        onKeyDown={(event) => {
          if (event.key === 'ArrowDown') {
            event.preventDefault();
            setAnchor(event.currentTarget);
          }
        }}
        sx={(theme) => ({
          display: 'flex',
          alignItems: 'center',
          gap: 0.5,
          minWidth: 0,
          px: 1,
          py: 0.5,
          borderRadius: 1,
          textAlign: 'left',
          color: 'inherit',
          '&:hover, &[aria-expanded="true"]': { bgcolor: 'action.hover' },
          '&:focus-visible': { outline: `2px solid ${theme.palette.primary.main}` },
        })}
      >
        <Box sx={{ minWidth: 0, display: 'flex', flexDirection: 'column' }}>
          <Typography variant="caption" component="span" sx={{ color: 'text.secondary' }}>
            {label}
          </Typography>
          <Typography
            variant="body2"
            component="span"
            sx={{ fontWeight: chosen ? 'fontWeightMedium' : 'fontWeightRegular' }}
          >
            <TruncatedText>{value}</TruncatedText>
          </Typography>
        </Box>
        <ThinChevronDownIcon fontSize="small" aria-hidden />
      </ButtonBase>
      <Popover
        open={open}
        anchorEl={anchor}
        onClose={close}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'left' }}
        transformOrigin={{ vertical: 'top', horizontal: 'left' }}
        slotProps={{
          paper: {
            onKeyDown: onPanelKeyDown,
            sx: { width: { xs: '100%', sm: 35 * 8 }, maxWidth: '100%' },
          },
        }}
      >
        <Box sx={{ p: 1 }}>
          <InlineTextField
            label={searchLabel}
            labelPlacement="hidden"
            placeholder={searchLabel}
            frame="always"
            value={query}
            onChange={setQuery}
            ref={(element) => element?.focus()}
          />
        </Box>
        {status === 'loading' && (
          <Box sx={{ p: 1 }}>
            <FieldsSkeleton count={SKELETON_ROWS} />
          </Box>
        )}
        {status === 'error' && (
          <Box sx={{ p: 1 }}>
            <InlineNotice
              severity="error"
              action={onRetry && retryLabel ? { label: retryLabel, onClick: onRetry } : undefined}
            >
              {errorText}
            </InlineNotice>
          </Box>
        )}
        {status === 'ready' && (
          <List
            ref={listRef}
            id={listId}
            role="listbox"
            aria-label={label}
            dense
            sx={{ maxHeight: 40 * 8, overflowY: 'auto' }}
          >
            {allLabel && !needle && (
              <ListItemButton
                role="option"
                aria-selected={currentId === null}
                selected={currentId === null}
                tabIndex={-1}
                onClick={() => choose(null)}
              >
                {allLabel}
                {currentId === null && (
                  <CheckOutlinedIcon fontSize="small" aria-hidden sx={{ ml: 'auto' }} />
                )}
              </ListItemButton>
            )}
            {shown.map((group, index) => {
              const headingId = `${headingBase}-${index}`;
              return (
                <li key={group.heading ?? index} role="presentation">
                  <ul
                    role="group"
                    aria-labelledby={group.heading ? headingId : undefined}
                    style={{ padding: 0 }}
                  >
                    {group.heading && (
                      <ListSubheader id={headingId} component="div" role="presentation">
                        {group.heading}
                      </ListSubheader>
                    )}
                    {group.note && (
                      <Typography
                        variant="caption"
                        component="div"
                        sx={{ px: 2, pb: 0.5, color: 'text.secondary' }}
                      >
                        {group.note}
                      </Typography>
                    )}
                    {group.options.map(renderOption)}
                  </ul>
                </li>
              );
            })}
            {footer && renderOption(footer)}
          </List>
        )}
        {(noMatch || nothingToShow) && (
          <Box sx={{ p: 2 }}>
            <InlineEmpty>{emptyText}</InlineEmpty>
          </Box>
        )}
      </Popover>
    </>
  );
}
