import { useEffect, useState, type KeyboardEvent, type RefObject } from 'react';
import Box from '@mui/material/Box';
import { useTheme } from '@mui/material/styles';
import { InlineTextField } from '../../../shared/ui';
import { TypeMark } from '../../../shared/ui/WorkTrackingMarks';
import { titleError } from '../../workitems';
import type { AddField, ChecklistActions } from '../hooks/useChecklists';

/**
 * The inline field of «Добавить задачу» (feat-032 design § 5, BR-C-9): feat-027
 * § 4.8's field as it is — 28 px high, `tableCell` text, a 1 px accent border, the
 * placeholder «Введите заголовок» — under the button, the task's icon before it
 * offset by one checkbox slot so it lines up with the rows' icons.
 *
 * Enter makes one create: the title is checked first (mod-003's `titleError`, so
 * «Не более 255 символов.» shows here and nothing is sent) and handed to the
 * board, which keeps **one `requestId` for every retry of this opening** (BR-M3-26).
 * A blank title is no refusal: the field closes. Escape closes it, and is stopped here
 * so it cancels nothing else. Leaving the field empty closes it; with text typed it
 * stays. While the create is saved the field is read-only with a progress indicator.
 * While **another** write is saved (`off`) Enter does nothing and the text is kept
 * (BR-C-8). The field takes focus only when it is opened or brought back to a user
 * who asked for it, not whenever it is drawn again.
 */
export function ChecklistAddField({
  field,
  off,
  inputRef,
  actions,
}: {
  field: AddField;
  /** Another write is saved: Enter does nothing. */
  off: boolean;
  inputRef: RefObject<HTMLInputElement | null>;
  actions: Pick<ChecklistActions, 'editAdd' | 'submitAdd' | 'cancelAdd' | 'addFocused'>;
}) {
  const theme = useTheme();
  const [title, setTitle] = useState(field.title);
  const [checked, setChecked] = useState<string | null>(null);
  const [takeFocus] = useState(field.focus);
  const { addFocused } = actions;

  useEffect(() => {
    if (takeFocus) {
      inputRef.current?.focus();
      addFocused();
    }
  }, [takeFocus, inputRef, addFocused]);

  function submit() {
    if (field.busy || off) {
      return;
    }
    if (title.trim() === '') {
      actions.cancelAdd('empty');
      return;
    }
    const refused = titleError(title);
    if (refused) {
      setChecked(refused);
      return;
    }
    actions.submitAdd(title);
  }

  function handleKeyDown(event: KeyboardEvent<HTMLElement>) {
    if (event.nativeEvent.isComposing) {
      return;
    }
    if (event.key === 'Enter') {
      event.preventDefault();
      submit();
    } else if (event.key === 'Escape') {
      event.preventDefault();
      event.stopPropagation();
      if (!field.busy) {
        actions.cancelAdd('escape');
      }
    }
  }

  const shown = checked ?? field.error;
  return (
    <Box
      data-add-field={field.cardIdentifier}
      sx={{ display: 'flex', alignItems: 'flex-start', gap: 0.5, ml: 3.5, minWidth: 0 }}
    >
      <Box
        component="span"
        sx={{
          display: 'inline-flex',
          alignItems: 'center',
          height: theme.spacing(3.5),
          flexShrink: 0,
        }}
      >
        <TypeMark appearance="task" compact colour={theme.palette.primary.dark} />
      </Box>
      <InlineTextField
        label={`Заголовок: ${field.typeName} (${field.cardIdentifier})`}
        labelPlacement="hidden"
        size="table"
        frame="always"
        ref={inputRef}
        value={title}
        placeholder="Введите заголовок"
        error={shown ?? undefined}
        busy={field.busy}
        onChange={(value) => {
          setTitle(value);
          setChecked(null);
          actions.editAdd(value);
        }}
        inputProps={{
          onKeyDown: handleKeyDown,
          onBlur: () => {
            if (!field.busy && title.trim() === '') {
              actions.cancelAdd('blur');
            }
          },
        }}
      />
    </Box>
  );
}
