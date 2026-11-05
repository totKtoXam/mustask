/* eslint-disable react-refresh/only-export-components -- the provider and the
   hook the two shared dialogs read it through are one small pattern. */
import { createContext, useContext, type ReactNode } from 'react';
import { useAccessCheck } from '../lib/access';

/** Whether the dialog's code has gone: its primary can do nothing allowed. */
const DialogLockedContext = createContext(false);

/**
 * Wraps the dialogs of a page that need one organization code (mod-016
 * design § 2.3, plan P-9). A refused write keeps its dialog open with what
 * was typed and the refusal's `Alert`; once the re-read of the served codes
 * shows the code gone, the dialog stays and its primary action becomes
 * disabled — a reason of state: nothing it could do is allowed — and Enter no
 * longer submits. «Отмена» and «Закрыть» keep working.
 *
 * The check is the bound one the organization gate provides, so the code is
 * asked about the page's own organization. Outside a gate it fails closed and
 * the dialogs would be locked: wrap only inside one.
 */
export function RequiresOrganizationCode({
  code,
  children,
}: {
  code: string;
  children: ReactNode;
}) {
  const access = useAccessCheck();
  return (
    <DialogLockedContext.Provider value={!access(code, 'organization')}>
      {children}
    </DialogLockedContext.Provider>
  );
}

/** For `FormDialog` and `ConfirmDialog`: the code of the wrapping page is gone. */
export function useDialogLocked(): boolean {
  return useContext(DialogLockedContext);
}
