import { useContext } from 'react';
import { NoticeContext, type NoticeApi } from './noticeContext';

/**
 * The panel's one way to say a short result («Рабочий элемент MSK-US-24 создан.»,
 * tech-015 design § 5.12). `AppLayout` mounts the provider once, so a notice survives a
 * route change; `renderWithProviders` mounts one in a test. `show` keeps its identity.
 *
 * Its own file, so a file that exports a component never exports a hook as well
 * (`react-refresh/only-export-components`).
 */
export function useNotice(): NoticeApi {
  const api = useContext(NoticeContext);
  if (!api) {
    throw new Error(
      'useNotice needs a NoticeProvider above it: AppLayout mounts one, and renderWithProviders mounts one in a test.',
    );
  }
  return api;
}
