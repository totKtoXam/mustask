import { useRef, type Ref } from 'react';
import Box from '@mui/material/Box';
import InputBase from '@mui/material/InputBase';
import { alpha } from '@mui/material/styles';
import { IconAction } from '../actions/IconAction';
import { ThinCloseIcon, ThinSearchIcon } from '../icons/ThinIcons';
import type { PassThrough } from '../passThrough';

export interface SearchFieldProps extends Omit<PassThrough<HTMLInputElement>, 'role' | 'tabIndex'> {
  value: string;
  /** The value, not the event. */
  onChange: (value: string) => void;
  /** The name of the field: «Поиск». */
  label: string;
  /** The text of an empty field: «Поиск…». */
  placeholder: string;
  /** The shortcut shown while the field is empty: «Ctrl K» or «⌘K». Absent: none. */
  hint?: string;
  /** The `aria-keyshortcuts` value: «Control+K» or «Meta+K». */
  keyShortcuts?: string;
  /** The clear button's name and tooltip: «Очистить поле». */
  clearLabel: string;
  /** Called by the clear button; the field then takes the focus back. */
  onClear: () => void;
  /** The input's `maxLength`: the field stops, no error text. */
  maxLength?: number;
  /** IME composition: the host holds its reads while one runs. */
  onCompositionStart?: () => void;
  onCompositionEnd?: () => void;
  /** The input element: focus it, or read what has the focus. */
  ref?: Ref<HTMLInputElement>;
  /** The frame the panel anchors to. */
  anchorRef?: Ref<HTMLDivElement>;
}

/**
 * The header's search field (feat-065 design § 1, D-14): 28 px, borderless, a faint
 * grey fill that deepens on hover and focus, a 2 px accent underline on focus and a
 * width that grows from 300 to 360 px. A leading magnifier, the input as an ARIA 1.2
 * combobox (`aria-expanded`, `aria-controls` and `aria-activedescendant` come from the
 * host through the pass-through), the shortcut hint while empty and a clear button
 * while not. It fetches nothing and knows no panel; the host owns the popup.
 */
export function SearchField({
  value,
  onChange,
  label,
  placeholder,
  hint,
  keyShortcuts,
  clearLabel,
  onClear,
  maxLength,
  onCompositionStart,
  onCompositionEnd,
  ref,
  anchorRef,
  ...passThrough
}: SearchFieldProps) {
  const inputElement = useRef<HTMLInputElement | null>(null);
  const field = (input: HTMLInputElement | null) => {
    inputElement.current = input;
    if (typeof ref === 'function') ref(input);
    else if (ref) ref.current = input;
  };
  return (
    <Box
      ref={anchorRef}
      sx={(theme) => {
        const { height, width, widthFocused } = theme.layout.searchField;
        return {
          boxSizing: 'border-box',
          display: 'flex',
          alignItems: 'center',
          gap: 0.75,
          flex: 1,
          minWidth: 0,
          maxWidth: width,
          marginInline: 'auto',
          height,
          paddingLeft: 1,
          paddingRight: 0.5,
          color: theme.palette.shell.cardLabel,
          backgroundColor: alpha(theme.palette.text.primary, 0.04),
          borderRadius: theme.spacing(0.75),
          transition: theme.transitions.create(['background-color', 'max-width'], {
            duration: theme.transitions.duration.shortest,
          }),
          '&:hover': { backgroundColor: alpha(theme.palette.text.primary, 0.06) },
          '&:focus-within': {
            maxWidth: widthFocused,
            color: theme.palette.primary.dark,
            backgroundColor: alpha(theme.palette.text.primary, 0.06),
            boxShadow: `inset 0 -${theme.layout.frame.thick}px 0 ${theme.palette.primary.main}`,
          },
        };
      }}
    >
      <ThinSearchIcon aria-hidden sx={(theme) => ({ fontSize: theme.spacing(2), flex: 'none' })} />
      <InputBase
        inputRef={field}
        value={value}
        onChange={(event) => onChange(event.target.value)}
        onCompositionStart={onCompositionStart}
        onCompositionEnd={onCompositionEnd}
        placeholder={placeholder}
        autoComplete="off"
        fullWidth
        slotProps={{
          input: {
            role: 'combobox',
            'aria-label': label,
            'aria-autocomplete': 'list',
            'aria-haspopup': 'listbox',
            'aria-keyshortcuts': keyShortcuts,
            maxLength,
            ...passThrough,
          },
        }}
        sx={(theme) => ({
          flex: 1,
          minWidth: 0,
          color: theme.palette.text.primary,
          fontSize: theme.typography.pxToRem(13),
          lineHeight: theme.typography.pxToRem(18),
          '& input': { padding: 0, height: theme.typography.pxToRem(18) },
          '& input::placeholder': { color: theme.palette.shell.cardLabel, opacity: 1 },
        })}
      />
      {value === '' && hint ? (
        <Box
          component="kbd"
          aria-hidden
          sx={(theme) => ({
            font: 'inherit',
            fontSize: theme.typography.pxToRem(11),
            whiteSpace: 'nowrap',
            paddingRight: 0.5,
            color: theme.palette.shell.cardLabel,
          })}
        >
          {hint}
        </Box>
      ) : null}
      {value !== '' ? (
        <IconAction
          size="row"
          tone="muted"
          label={clearLabel}
          icon={<ThinCloseIcon />}
          onClick={() => {
            onClear();
            inputElement.current?.focus();
          }}
        />
      ) : null}
    </Box>
  );
}
