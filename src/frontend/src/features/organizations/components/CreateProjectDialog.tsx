import { useState } from 'react';
import TextField from '@mui/material/TextField';
import { FormDialog } from '../../../shared/ui/FormDialog';
import type { Project } from '../api/organizationsApi';
import { placeRefusal } from '../lib/refusals';
import { keyError, nameError } from '../lib/validation';
import { requiredField } from './requiredField';

/**
 * «Новый проект» (US-3; design § 7.4): «Название» and «Ключ», both checked
 * on submit — the key as typed, never trimmed or upper-cased — and nothing
 * sent while either is refused. A refusal lands on its field or in the
 * dialog's `Alert` by its code; the input is kept.
 */
export function CreateProjectDialog({
  submit,
  onSucceeded,
  onClose,
}: {
  submit(body: { name: string; key: string }): Promise<Project>;
  onSucceeded(project: Project): void;
  onClose(contextGone: boolean): void;
}) {
  const [name, setName] = useState('');
  const [key, setKey] = useState('');
  const [errors, setErrors] = useState<{ name?: string | null; key?: string | null }>({});
  const [alert, setAlert] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [contextGone, setContextGone] = useState(false);

  async function handleSubmit() {
    const refused = { name: nameError(name), key: keyError(key) };
    if (refused.name || refused.key) {
      setErrors(refused);
      return;
    }
    setBusy(true);
    setAlert(null);
    try {
      onSucceeded(await submit({ name, key }));
    } catch (error) {
      const placed = placeRefusal(error, ['name', 'key']);
      setErrors({ name: placed.fields.name ?? null, key: placed.fields.key ?? null });
      setAlert(placed.alert);
      setContextGone((gone) => gone || placed.contextGone);
      setBusy(false);
    }
  }

  return (
    <FormDialog
      open
      title="Новый проект"
      submitLabel="Создать"
      busy={busy}
      error={alert}
      onSubmit={() => void handleSubmit()}
      onClose={() => onClose(contextGone)}
    >
      <TextField
        label="Название"
        autoFocus
        value={name}
        disabled={busy}
        error={Boolean(errors.name)}
        helperText={
          errors.name ??
          'От 1 до 100 символов. Так же будут названы первая команда проекта и его корневая область.'
        }
        onChange={(event) => {
          setName(event.target.value);
          setErrors((current) => ({ ...current, name: null }));
        }}
        slotProps={requiredField(Boolean(errors.name))}
      />
      <TextField
        label="Ключ"
        value={key}
        disabled={busy}
        error={Boolean(errors.key)}
        helperText={
          errors.key ??
          'От 2 до 10 латинских заглавных букв и цифр, начиная с буквы. С ключа начинаются номера рабочих элементов, после создания он не меняется.'
        }
        onChange={(event) => {
          setKey(event.target.value);
          setErrors((current) => ({ ...current, key: null }));
        }}
        slotProps={{
          inputLabel: { required: true },
          htmlInput: {
            autoComplete: 'off',
            spellCheck: false,
            autoCapitalize: 'characters',
            'aria-required': true,
            'aria-invalid': Boolean(errors.key),
          },
        }}
      />
    </FormDialog>
  );
}
