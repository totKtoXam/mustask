import { useCallback, useEffect, useState } from 'react';

/**
 * A refusal puts the focus on the field it names (feat-028 design § 12): the first control of the open
 * dialog that is marked `aria-invalid`, in document order. The caller asks for it with the returned function
 * once it has set the field's error; the focus moves after that render, when the dialog's fields are enabled
 * again. The kit's fields and the legacy ones both set the attribute, so one rule serves a mixed form.
 */
export function useFocusInvalid(): () => void {
  const [requests, setRequests] = useState(0);

  useEffect(() => {
    if (requests > 0) {
      document.querySelector<HTMLElement>('[role="dialog"] [aria-invalid="true"]')?.focus();
    }
  }, [requests]);

  return useCallback(() => setRequests((count) => count + 1), []);
}
