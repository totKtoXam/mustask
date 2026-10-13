import { useEffect, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { CARRIED_EDITS_KEY, readCarriedEdits, type WorkItemEdits } from '../lib/edits';

function carries(state: unknown): boolean {
  return typeof state === 'object' && state !== null && CARRIED_EDITS_KEY in state;
}

/**
 * The edits the page was opened with by the dialog's «Развернуть на страницу»
 * (BR-C-3), read once, on the first render. The router state is cleared with a
 * replace right after, so a reload or a copied history entry starts clean. The
 * card validates them against the item it reads (`normaliseEdits`): an edit that
 * equals what is stored is dropped, and a field somebody else changed meanwhile
 * shows the stored value for every field the user did not touch.
 */
export function useCarriedEdits(): WorkItemEdits | undefined {
  const { pathname, search, hash, state } = useLocation();
  const navigate = useNavigate();
  const [carried] = useState(() => readCarriedEdits(state));
  const present = carries(state);

  useEffect(() => {
    if (!present) {
      return;
    }
    const rest = Object.fromEntries(
      Object.entries(state as Record<string, unknown>).filter(([key]) => key !== CARRIED_EDITS_KEY),
    );
    navigate(
      { pathname, search, hash },
      { replace: true, state: Object.keys(rest).length > 0 ? rest : undefined },
    );
  }, [present, state, navigate, pathname, search, hash]);

  return carried;
}
