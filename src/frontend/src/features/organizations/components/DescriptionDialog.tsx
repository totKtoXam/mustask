import { useState } from 'react';
import { FormDialog, InlineTextArea } from '../../../shared/ui';
import { useFocusInvalid } from '../hooks/useFocusInvalid';
import { useFocusOnMount } from '../hooks/useFocusOnMount';
import { placeRefusal } from '../lib/refusals';
import { descriptionDialog } from '../lib/teamTexts';
import { descriptionError } from '../lib/validation';

/**
 * «Описание команды» / «Описание проекта» (feat-028 design § 5.2): one component, the title and
 * the notice are the host's. One «Описание» with the stored text, «Сохранить» unavailable until
 * the text differs. An empty text is allowed and clears the description; the text is sent as
 * typed. Over 2 000 characters is refused under the field before anything is sent, and by the
 * server's own reason after. Mounted per opening, so it starts from the stored text every time.
 */
export function DescriptionDialog({
  title,
  initial,
  submit,
  onSucceeded,
  onClose,
}: {
  title: string;
  /** The stored description; `null` when there is none. */
  initial: string | null;
  /** Sends the text as typed, `null` for an empty one. */
  submit(description: string | null): Promise<unknown>;
  onSucceeded(): void;
  /** `contextGone`: a not-found answer; the page reads itself again. */
  onClose(contextGone: boolean): void;
}) {
  const stored = initial ?? '';
  const [text, setText] = useState(stored);
  const [fieldError, setFieldError] = useState<string | undefined>(undefined);
  const [alert, setAlert] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [contextGone, setContextGone] = useState(false);
  const focusField = useFocusOnMount<HTMLTextAreaElement>();
  const focusInvalid = useFocusInvalid();

  async function handleSubmit() {
    const refusal = descriptionError(text);
    if (refusal) {
      setFieldError(refusal);
      focusInvalid();
      return;
    }
    setBusy(true);
    setAlert(null);
    try {
      await submit(text === '' ? null : text);
      onSucceeded();
    } catch (error) {
      const placed = placeRefusal(error, ['description']);
      setFieldError(placed.fields.description);
      setAlert(placed.alert);
      setContextGone((gone) => gone || placed.contextGone);
      setBusy(false);
      focusInvalid();
    }
  }

  return (
    <FormDialog
      open
      title={title}
      submitLabel={descriptionDialog.submit}
      busy={busy}
      error={alert}
      submitDisabled={text === stored}
      onSubmit={() => void handleSubmit()}
      onClose={() => onClose(contextGone)}
    >
      <InlineTextArea
        ref={focusField}
        label={descriptionDialog.label}
        frame="always"
        hint={descriptionDialog.hint}
        value={text}
        busy={busy}
        error={fieldError}
        onChange={(next) => {
          setText(next);
          setFieldError(undefined);
        }}
      />
    </FormDialog>
  );
}
