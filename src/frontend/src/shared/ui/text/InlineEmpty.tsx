import type { ReactNode } from 'react';
import Typography from '@mui/material/Typography';

export interface InlineEmptyProps {
  /** The host's words, e.g. «Дочерних элементов нет.» — the kit has no text for it. */
  children: ReactNode;
}

/**
 * An empty list or section, said in one quiet line (tech-015 design § 5.5):
 * `body2` in `text.secondary`. Not a `StateFrame` — no centred frame inside a section.
 */
export function InlineEmpty({ children }: InlineEmptyProps) {
  return (
    // `sx`, not the `color` prop: in MUI 9 the prop knows `textSecondary`, not `text.secondary`.
    <Typography variant="body2" sx={{ color: 'text.secondary' }}>
      {children}
    </Typography>
  );
}
