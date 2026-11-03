import { useCallback, useState } from 'react';
import { useAuth } from '../../auth/AuthContext';

/**
 * What «Структура» remembers per user and organization (feat-049 BR-SD-9, design § 3): the view,
 * the display mode and the layout, each under a key of its own, never inside
 * `mustask.context.v2` (ADR-0032 locks that value's shape). A value outside the allowed set, an
 * absent or a refusing storage means the default, with no error; a value is written only on the
 * user's own choice. The level choice, the section folds, the search and the zoom are not stored.
 */

export const STRUCTURE_VIEWS = ['tree', 'diagram'] as const;
export type StructureView = (typeof STRUCTURE_VIEWS)[number];
export const DISPLAY_MODES = ['dim', 'only', 'hl'] as const;
export type DisplayMode = (typeof DISPLAY_MODES)[number];
export const LAYOUT_ORIENTATIONS = ['h', 'v'] as const;
export type LayoutOrientation = (typeof LAYOUT_ORIENTATIONS)[number];

const KEY_ROOT = 'mustask.structure';

type Setting = 'view' | 'mode' | 'layout';

export function structureStorageKey(
  setting: Setting,
  subject: string,
  organizationId: string,
): string {
  return `${KEY_ROOT}.${setting}.v1.${subject}.${organizationId}`;
}

function read<T extends string>(key: string, allowed: readonly T[], fallback: T): T {
  try {
    const stored = window.localStorage.getItem(key);
    return allowed.find((value) => value === stored) ?? fallback;
  } catch {
    return fallback;
  }
}

function write(key: string, value: string) {
  try {
    window.localStorage.setItem(key, value);
  } catch {
    // A full or blocked store only costs the memory, never the page.
  }
}

export interface StructureSettings {
  view: StructureView;
  mode: DisplayMode;
  layout: LayoutOrientation;
  setView: (view: StructureView) => void;
  setMode: (mode: DisplayMode) => void;
  setLayout: (layout: LayoutOrientation) => void;
}

interface Remembered {
  scope: string;
  view: StructureView;
  mode: DisplayMode;
  layout: LayoutOrientation;
}

export function useStructureSettings(organizationId: string): StructureSettings {
  const { subject: authSubject } = useAuth();
  const subject = authSubject ?? '';
  const scope = `${subject}.${organizationId}`;
  const load = (): Remembered => ({
    scope,
    view: read(structureStorageKey('view', subject, organizationId), STRUCTURE_VIEWS, 'tree'),
    mode: read(structureStorageKey('mode', subject, organizationId), DISPLAY_MODES, 'dim'),
    layout: read(structureStorageKey('layout', subject, organizationId), LAYOUT_ORIENTATIONS, 'h'),
  });
  const [remembered, setRemembered] = useState<Remembered>(load);
  // Another organization or user: read its own memory (state adjusted while rendering).
  let current = remembered;
  if (remembered.scope !== scope) {
    current = load();
    setRemembered(current);
  }

  const setView = useCallback(
    (view: StructureView) => {
      write(structureStorageKey('view', subject, organizationId), view);
      setRemembered((state) => ({ ...state, view }));
    },
    [subject, organizationId],
  );
  const setMode = useCallback(
    (mode: DisplayMode) => {
      write(structureStorageKey('mode', subject, organizationId), mode);
      setRemembered((state) => ({ ...state, mode }));
    },
    [subject, organizationId],
  );
  const setLayout = useCallback(
    (layout: LayoutOrientation) => {
      write(structureStorageKey('layout', subject, organizationId), layout);
      setRemembered((state) => ({ ...state, layout }));
    },
    [subject, organizationId],
  );

  return {
    view: current.view,
    mode: current.mode,
    layout: current.layout,
    setView,
    setMode,
    setLayout,
  };
}
