import { useImperativeHandle, useRef, useState, type ReactNode, type Ref } from 'react';
import Box from '@mui/material/Box';
import InputBase from '@mui/material/InputBase';
import MenuItem from '@mui/material/MenuItem';
import Select from '@mui/material/Select';
import { useTheme, type CSSObject, type Theme } from '@mui/material/styles';
import { ThinChevronDownIcon } from '../icons/ThinIcons';
import { FieldFrame } from './FieldFrame';
import { OPEN } from './fieldStyles';
import type { FieldFrameKind, FieldInputProps, FieldProps } from './fieldTypes';
import { fieldControlState, useFieldIds } from './useFieldWiring';

export interface SelectOption<V extends string> {
  value: V;
  label: string;
  /** A dot or an icon before the label, in the list and in the value. */
  startAdornment?: ReactNode;
}

export interface InlineSelectFieldProps<V extends string> extends FieldProps {
  /** `''` is none. */
  value: V | '';
  onChange: (value: V | '') => void;
  options: readonly SelectOption<V>[];
  /**
   * Adds a first option with this label («Не указан»); its presence is what makes the
   * field optional, and choosing it gives `''`.
   */
  emptyLabel?: string;
  /** What the value shows, when the option's own label and adornment do not say it. */
  renderValue?: (option: { value: V; label: string }) => ReactNode;
  inputProps?: FieldInputProps;
  /** The combobox: focus it. */
  ref?: Ref<HTMLElement>;
}

/**
 * A choice among a few options (tech-015 design § 5.7): MUI's `Select` on the kit's
 * frame. Its caret is hidden at rest and shows with the frame; it opens on Enter,
 * Space or ArrowDown. **Its accessible name is its label only** — MUI would add the
 * shown value, so the combobox is named by the label's id and nothing else; a
 * read-only select does not open.
 */
export function InlineSelectField<V extends string>({
  value,
  onChange,
  options,
  emptyLabel,
  renderValue,
  inputProps,
  ref,
  ...field
}: InlineSelectFieldProps<V>) {
  const theme = useTheme();
  const ids = useFieldIds(field.id);
  const state = fieldControlState(field, ids);
  const rootRef = useRef<HTMLDivElement>(null);
  const combobox = () => rootRef.current?.querySelector<HTMLElement>('[role="combobox"]') ?? null;
  useImperativeHandle(
    ref,
    () => rootRef.current?.querySelector<HTMLElement>('[role="combobox"]') as HTMLElement,
    [],
  );
  const optional = emptyLabel !== undefined;
  // The menu takes the focus away while it is open; the frame says so by `data-open`.
  const [open, setOpen] = useState(false);

  return (
    <FieldFrame
      {...field}
      ids={ids}
      empty={value === '' && !optional}
      open={open}
      // A label cannot name a div; it moves focus to the combobox itself.
      labelFor={null}
      onLabelClick={() => combobox()?.focus()}
      kindRules={selectRules(theme, field.frame ?? 'hover')}
    >
      <Select
        ref={rootRef}
        value={value}
        displayEmpty={optional}
        onOpen={() => setOpen(true)}
        onClose={() => setOpen(false)}
        onChange={(event) => onChange(event.target.value as V | '')}
        variant="standard"
        input={<InputBase />}
        labelId={ids.label}
        readOnly={state.locked}
        disabled={field.disabled}
        IconComponent={ThinChevronDownIcon}
        renderValue={
          renderValue
            ? (selected) => {
                const option = options.find((candidate) => candidate.value === selected);
                return option ? renderValue(option) : (emptyLabel ?? '');
              }
            : undefined
        }
        SelectDisplayProps={{
          ...inputProps,
          id: ids.control,
          'aria-labelledby': ids.label,
          ...state.aria,
        }}
      >
        {optional && <MenuItem value="">{emptyLabel}</MenuItem>}
        {options.map((option) => (
          <MenuItem key={option.value} value={option.value}>
            {option.startAdornment ? (
              <Box
                component="span"
                sx={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: `${theme.layout.mark.nameGap}px`,
                }}
              >
                {option.startAdornment}
                {option.label}
              </Box>
            ) : (
              option.label
            )}
          </MenuItem>
        ))}
      </Select>
    </FieldFrame>
  );
}

/**
 * The caret shows with the frame; the display fills the box and keeps the caret's room.
 * A state rule names the caret as a descendant of `&` (the root, once): a second `&` in it
 * would be the root's class again, and no element holds that class inside the root.
 */
function selectRules(theme: Theme, frame: FieldFrameKind): CSSObject {
  const icon = '.MuiSelect-icon';
  const caret = `&& ${icon}`;
  const shown = { opacity: 1 };
  return {
    '&& .MuiSelect-select': {
      display: 'flex',
      alignItems: 'center',
      minWidth: 0,
      padding: 0,
      paddingRight: `${theme.layout.iconAction.chevron.glyph}px`,
      height: 'auto',
      minHeight: 0,
      overflow: 'hidden',
      textOverflow: 'ellipsis',
      whiteSpace: 'nowrap',
    },
    [caret]: {
      right: 0,
      fontSize: theme.layout.iconAction.chevron.glyph,
      color: theme.palette.text.primary,
      pointerEvents: 'none',
      opacity: frame === 'always' ? 1 : 0,
    },
    [`&:hover ${icon}`]: shown,
    [`&:focus-within ${icon}`]: shown,
    [`&${OPEN} ${icon}`]: shown,
    '@media (hover: none)': { [caret]: shown },
    '&& .MuiSelect-select.Mui-disabled': { WebkitTextFillColor: theme.palette.text.disabled },
  };
}
