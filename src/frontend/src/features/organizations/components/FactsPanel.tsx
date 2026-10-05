import { Fragment, type ReactNode } from 'react';
import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';

export interface Fact {
  label: string;
  value: ReactNode;
  /** A note under the value. */
  note?: ReactNode;
}

/**
 * The facts of an «Общие» page or a team's page (design § 4.2): an outlined
 * panel at most `layout.formColumnWidth` wide holding a `dl`, label and value
 * in two columns of 200 px and the rest; below 640 px the label goes above
 * its value.
 */
export function FactsPanel({ facts }: { facts: readonly Fact[] }) {
  return (
    <Paper
      variant="outlined"
      sx={{ p: 3, maxWidth: (theme) => theme.layout.formColumnWidth, boxSizing: 'border-box' }}
    >
      <Box
        component="dl"
        sx={(theme) => ({
          m: 0,
          display: 'grid',
          gridTemplateColumns: { xs: '1fr', sm: `${theme.spacing(25)} 1fr` },
          columnGap: 2,
          rowGap: { xs: 0.5, sm: 2 },
        })}
      >
        {facts.map((fact) => (
          <Fragment key={fact.label}>
            <Typography
              component="dt"
              variant="subtitle2"
              color="text.secondary"
              sx={{ mt: { xs: 1, sm: 0 } }}
            >
              {fact.label}
            </Typography>
            <Box component="dd" sx={{ m: 0, minWidth: 0 }}>
              <Typography component="div" variant="body1" sx={{ overflowWrap: 'anywhere' }}>
                {fact.value}
              </Typography>
              {fact.note && (
                <Typography variant="caption" color="text.secondary" component="div">
                  {fact.note}
                </Typography>
              )}
            </Box>
          </Fragment>
        ))}
      </Box>
    </Paper>
  );
}
