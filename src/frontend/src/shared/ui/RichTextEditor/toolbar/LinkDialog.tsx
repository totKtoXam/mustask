import { useState } from 'react';
import TextField from '@mui/material/TextField';
import { promptAddress } from '../../../lib/richText';
import { FormDialog } from '../../FormDialog';
import type { RichTextCopy } from '../copy';

interface LinkDialogProps {
  copy: RichTextCopy['link'];
  /** The address when the caret is in a link, else `''`. */
  initialAddress: string;
  /** Show «Текст ссылки» (nothing selected, caret not in a link). */
  askText: boolean;
  /** Called with an address that passed `promptAddress`, and the text (maybe empty). */
  onApply: (address: string, text: string) => void;
  onClose: () => void;
}

/**
 * The link prompt (design § Links, plan § 8.1) on `FormDialog`: «Адрес ссылки»
 * (autofocus, hint «https://…»), «Текст ссылки» when nothing is selected, the
 * filled «Применить» and «Отмена». Enter applies. A refusal sits on the field
 * and the dialog stays. Mounted only while open, so each opening starts fresh.
 */
export function LinkDialog({ copy, initialAddress, askText, onApply, onClose }: LinkDialogProps) {
  const [address, setAddress] = useState(initialAddress);
  const [text, setText] = useState('');
  const [error, setError] = useState<string | null>(null);

  const submit = () => {
    if (address.trim().length === 0) {
      setError(copy.addressRequired);
      return;
    }
    const checked = promptAddress(address);
    if (checked === null) {
      setError(copy.addressRefused);
      return;
    }
    onApply(checked, text);
  };

  return (
    <FormDialog
      open
      title={copy.title}
      submitLabel={copy.apply}
      onSubmit={submit}
      onClose={onClose}
    >
      <TextField
        autoFocus
        label={copy.address}
        placeholder={copy.addressHint}
        value={address}
        onChange={(event) => {
          setAddress(event.target.value);
          setError(null);
        }}
        error={error !== null}
        helperText={error ?? undefined}
        fullWidth
        slotProps={{ htmlInput: { autoComplete: 'off', spellCheck: false } }}
      />
      {askText && (
        <TextField
          label={copy.text}
          value={text}
          onChange={(event) => setText(event.target.value)}
          fullWidth
        />
      )}
    </FormDialog>
  );
}
