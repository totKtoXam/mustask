import { useImperativeHandle, useRef, useState, type ReactNode, type Ref } from 'react';
import Box from '@mui/material/Box';
import InputBase from '@mui/material/InputBase';
import MenuItem from '@mui/material/MenuItem';
import Select from '@mui/material/Select';
import { useTheme } from '@mui/material/styles';
import { ThinChevronDownIcon } from '../icons/ThinIcons';
import { FieldFrame } from './FieldFrame';
import { selectRules } from './fieldStyles';
import type { FieldInputProps, FieldProps } from './fieldTypes';
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
