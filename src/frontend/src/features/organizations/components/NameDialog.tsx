import { useState } from 'react';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import { FormDialog } from '../../../shared/ui/FormDialog';
import { placeRefusal } from '../lib/refusals';
import { nameError } from '../lib/validation';
import { requiredField } from './requiredField';

export interface NameDialogProps<R> {
  title: string;
  /** A text above the field (the project rename's, design § 8.3). */
  text?: string;
  /** The field's helper; none where the design gives none. */
  helper?: string;
  /** The current name for a rename, '' for a create. */
  initialName?: string;
  submitLabel: string;
  /** Sends the trimmed-as-typed name; resolves with the answer. */
  submit(name: string): Promise<R>;
  onSucceeded(result: R, name: string): void;
  /** `contextGone`: a not-found answer; the page reads itself again (design § 4.4). */
  onClose(contextGone: boolean): void;
}

/**
 * The one name form for create and rename at every level (DC-5, design
 * § 4.3): «Название» focused, a rename's current name prefilled and
 * selected, and its primary disabled while the name is the very same
 * string. The length is checked on submit and cleared as the field is
 * edited (design § 4.4); a refusal lands on the field or in the dialog's
 * `Alert` by its code, and the input is kept. Mounted per opening, so it
 * starts from the current name every time.
 */
export function NameDialog<R>({
  title,
  text,
  helper,
  initialName = '',
  submitLabel,
  submit,
  onSucceeded,
  onClose,
}: NameDialogProps<R>) {
  const [name, setName] = useState(initialName);
  const [fieldError, setFieldError] = useState<string | null>(null);
  const [alert, setAlert] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [contextGone, setContextGone] = useState(false);
  const [selectedOnce, setSelectedOnce] = useState(false);
  const isRename = initialName !== '';

  async function handleSubmit() {
    const refusal = nameError(name);
    if (refusal) {
      setFieldError(refusal);
      return;
    }
    setBusy(true);
    setAlert(null);
    try {
      const result = await submit(name);
      onSucceeded(result, name);
    } catch (error) {
      const placed = placeRefusal(error, ['name']);
      setFieldError(placed.fields.name ?? null);
      setAlert(placed.alert);
      setContextGone((gone) => gone || placed.contextGone);
      setBusy(false);
    }
  }

  return (
    <FormDialog
      open
      title={title}
      submitLabel={submitLabel}
      busy={busy}
      error={alert}
      submitDisabled={isRename && name === initialName}
      onSubmit={() => void handleSubmit()}
      onClose={() => onClose(contextGone)}
    >
      {text && (
        <Typography variant="body2" color="text.secondary">
          {text}
        </Typography>
      )}
      <TextField
        label="Название"
        autoFocus
        value={name}
        disabled={busy}
        error={fieldError !== null}
        helperText={fieldError ?? helper}
        onChange={(event) => {
          setName(event.target.value);
          setFieldError(null);
        }}
        slotProps={requiredField(fieldError !== null, (event) => {
          // A rename opens with the current name selected (design § 4.3).
          if (isRename && !selectedOnce) {
            setSelectedOnce(true);
            event.currentTarget.select();
          }
        })}
      />
    </FormDialog>
  );
}
