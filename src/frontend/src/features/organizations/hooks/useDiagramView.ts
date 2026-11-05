import { useCallback, useState } from 'react';
import { NO_LEVELS, type LevelChoice } from '../lib/diagramLevels';
import type { SectionKey } from '../lib/diagramModel';

/**
 * The diagram's page state (feat-049 design § 3): kept by the page across a view switch for the
 * visit, never stored, and dropped when the organization changes. The chosen node is not here:
 * it dies with the diagram on any switch.
 */
export interface DiagramViewState {
  /** `null`: the visit's start, the initial expansion of D-3. */
  expansion: ReadonlySet<string> | null;
  search: string;
  sections: Readonly<Record<SectionKey, boolean>>;
  levels: LevelChoice;
  zoom: number;
}

const START: DiagramViewState = {
  expansion: null,
  search: '',
  sections: { outside: true, departments: true },
  levels: NO_LEVELS,
  zoom: 1,
};

export type DiagramViewPatch = (change: Partial<DiagramViewState>) => void;

export function useDiagramView(organizationId: string): [DiagramViewState, DiagramViewPatch] {
  const [held, setHeld] = useState<{ organizationId: string; state: DiagramViewState }>({
    organizationId,
    state: START,
  });
  let state = held.state;
  if (held.organizationId !== organizationId) {
    state = START;
    setHeld({ organizationId, state });
  }
  const patch = useCallback<DiagramViewPatch>(
    (change) =>
      setHeld((current) => ({
        organizationId: current.organizationId,
        state: { ...current.state, ...change },
      })),
    [],
  );
  return [state, patch];
}
