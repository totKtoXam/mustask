import { useId, type ReactNode } from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';

/**
 * One section of the form's «Детали» grid (mod-015 § 10.3; feat-022 spec
 * § The form's sections): an `h2` with a `divider` rule, naming the section
 * as a region.
 */
export function FormSection({ title, children }: { title: string; children: ReactNode }) {
  const headingId = useId();
  return (
    <Box component="section" aria-labelledby={headingId}>
      <Typography
        id={headingId}
        variant="h2"
        component="h2"
        sx={{ pb: 1, mb: 2, borderBottom: 1, borderColor: 'divider' }}
      >
        {title}
      </Typography>
      {children}
    </Box>
  );
}
