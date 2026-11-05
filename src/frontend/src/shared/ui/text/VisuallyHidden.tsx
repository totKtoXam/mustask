import type { ElementType, ReactNode } from 'react';
import Box from '@mui/material/Box';

export interface VisuallyHiddenProps {
  children: ReactNode;
  /** The element drawn, default `span`. */
  component?: ElementType;
  /** For a hidden label or description another control points at. */
  id?: string;
}

/**
 * Text that is there for assistive technology and not for the eye
 * (`theme.mixins.visuallyHidden`, tech-015 design § 5.5): a hidden description or
 * label. It is not `display: none` — a screen reader reads it.
 */
export function VisuallyHidden({ children, component = 'span', id }: VisuallyHiddenProps) {
  return (
    <Box component={component} id={id} sx={(theme) => theme.mixins.visuallyHidden}>
      {children}
    </Box>
  );
}
