import type { FormEventHandler, ReactNode } from 'react';
import Alert from '@mui/material/Alert';
import Paper from '@mui/material/Paper';
import Stack from '@mui/material/Stack';

export interface FormPanelProps {
  /** The fields, top to bottom. */
  children: ReactNode;
  /** «Отмена» and the primary, or whatever takes their place (a conflict panel). */
  footer: ReactNode;
  /** An error not tied to a field: a banner above the first field (US-9). */
  error?: ReactNode;
  onSubmit: FormEventHandler<HTMLFormElement>;
}

/**
 * A form page's column (US-9, design § 9.3): an outlined panel of limited
 * width holding the fields in one column, an error banner above the first
 * field when the error belongs to no field, and the footer below the last
 * one. Field errors stay on the fields (`error`, `helperText`), which marks
 * them `aria-invalid`.
 */
export function FormPanel({ children, footer, error, onSubmit }: FormPanelProps) {
  return (
    <Paper
      variant="outlined"
      component="form"
      onSubmit={onSubmit}
      sx={{ p: 3, maxWidth: (theme) => theme.layout.formColumnWidth }}
    >
      <Stack spacing={2}>
        {error && <Alert severity="error">{error}</Alert>}
        {children}
        {footer}
      </Stack>
    </Paper>
  );
}

/** The footer of a form: «Отмена» left of the filled primary, at the right. */
export function FormFooter({ children }: { children: ReactNode }) {
  return (
    <Stack direction="row" spacing={1} sx={{ justifyContent: 'flex-end' }}>
      {children}
    </Stack>
  );
}
