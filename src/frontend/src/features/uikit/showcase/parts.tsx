import type { ReactNode } from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';

/** The grid every section lays its demo groups in (tech-015 design § 10.2). */
export function DemoGrid({ children }: { children: ReactNode }) {
  return (
    <Box
      sx={{
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fill, minmax(240px, 1fr))',
        gap: 2,
        alignItems: 'start',
      }}
    >
      {children}
    </Box>
  );
}

/**
 * A caption over a live component. A states matrix is a row of these, one per state; a
 * state that needs a pointer or a key says what to do in its caption — it is not faked.
 */
export function DemoGroup({ caption, children }: { caption: string; children: ReactNode }) {
  return (
    <Box sx={{ minWidth: 0 }}>
      <Typography variant="caption" component="p" sx={{ color: 'text.secondary', mb: 1 }}>
        {caption}
      </Typography>
      {children}
    </Box>
  );
}

/** A group of demos that stand in a row, wrapping when the section is narrow. */
export function DemoRow({ children }: { children: ReactNode }) {
  return (
    <Box sx={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: 1.5 }}>{children}</Box>
  );
}
