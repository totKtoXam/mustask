import { useRef, type ChangeEvent, type ReactElement } from 'react';
import { VisuallyHidden } from '../text/VisuallyHidden';
import { ActionButton, type ActionTone } from './ActionButton';

export interface FilePickerButtonProps {
  /** The button's name, drawn (and the name of the file input behind it). */
  label: string;
  /** Called with the one file the user picked; not called when the dialog is cancelled. */
  onPick: (file: File) => void;
  /** The `accept` attribute of the file input, e.g. `.xlsx,.csv`. */
  accept?: string;
  tone?: ActionTone;
  icon?: ReactElement;
  busy?: boolean;
  disabled?: boolean;
}

/**
 * A button that opens the system's file dialog (mod-024). An `ActionButton` that clicks a visually
 * hidden file input: the screen never draws an `<input>` of its own (KIT-23). One file at a time;
 * picking the same file twice reports it twice (the input is cleared after each pick). The hidden
 * input is named like the button for a test and for assistive technology that reaches it, and is
 * out of the Tab order: the button is the control.
 */
export function FilePickerButton({
  label,
  onPick,
  accept,
  tone = 'neutral',
  icon,
  busy = false,
  disabled = false,
}: FilePickerButtonProps) {
  const input = useRef<HTMLInputElement>(null);

  function picked(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (file) onPick(file);
  }

  return (
    <>
      <ActionButton
        label={label}
        tone={tone}
        icon={icon}
        busy={busy}
        disabled={disabled}
        onClick={() => input.current?.click()}
      />
      <VisuallyHidden>
        <input
          ref={input}
          type="file"
          accept={accept}
          aria-label={label}
          tabIndex={-1}
          disabled={disabled}
          onChange={picked}
        />
      </VisuallyHidden>
    </>
  );
}
