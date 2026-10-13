import type { ReactNode, Ref } from 'react';
import Alert from '@mui/material/Alert';
import AlertTitle from '@mui/material/AlertTitle';
import { ActionButton } from '../actions/ActionButton';

export interface InlineNoticeAction {
  /** The host's word: «Повторить». */
  label: string;
  onClick: () => void;
  /** A handle on the button, for a page that moves focus back to it after a retry that failed again. */
  ref?: Ref<HTMLButtonElement>;
}

export interface InlineNoticeProps {
  severity: 'error' | 'warning' | 'info';
  /** Names what failed above the text, e.g. «Не удалось загрузить процесс». */
  title?: string;
  /** The text. */
  children: ReactNode;
  action?: InlineNoticeAction;
}

/**
 * An error, a warning or a note that stays in the layout (tech-015 design § 5.12): a
 * refusal that lands on no field, a load error with «Повторить», a read-only note, a
 * version conflict. MUI's standard `Alert`; the retry is an `ActionButton` in the subtle
 * tone.
 *
 * **It owns no margin**: the container — a `Stack` gap, a section, the shell's fixed
 * `alert` area — spaces it, so nothing doubles. An `error` is `role="alert"`, a `warning`
 * or an `info` `role="status"`: only a failure interrupts.
 */
export function InlineNotice({ severity, title, children, action }: InlineNoticeProps) {
  return (
    <Alert
      severity={severity}
      role={severity === 'error' ? 'alert' : 'status'}
      action={action && <NoticeAction {...action} />}
    >
      {title && <AlertTitle>{title}</AlertTitle>}
      {children}
    </Alert>
  );
}

/**
 * The button of a notice. Its own component: `ref` arrives as a prop here, which is how
 * a forwarded handle is passed on, and the notice never reads it while it renders.
 */
function NoticeAction({ label, onClick, ref }: InlineNoticeAction) {
  return <ActionButton tone="subtle" size="compact" label={label} onClick={onClick} ref={ref} />;
}
