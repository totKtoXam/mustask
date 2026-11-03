import type { ReactNode } from 'react';
import Accordion from '@mui/material/Accordion';
import AccordionDetails from '@mui/material/AccordionDetails';
import AccordionSummary from '@mui/material/AccordionSummary';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import { Tag, ThinChevronDownIcon } from '../../../shared/ui';

/**
 * One group of a grouped view, or a lane of the board (feat-048 design § 1, § 3): a collapsible
 * MUI `Accordion` whose summary holds the name (an `h2`), the optional key `Tag` and the count
 * («40», «3 из 40»). It is the header only: the group's states and rows are the `children`, drawn
 * by `GroupBody`, and a closed group reads nothing. Renamed from the design's `ProjectGroup`: a
 * group is a department, a project or a team now.
 */
export function ViewGroup({
  id,
  name,
  tag,
  count,
  open,
  onToggle,
  children,
}: {
  id: string;
  name: string;
  tag?: string;
  count: string;
  open: boolean;
  onToggle(open: boolean): void;
  children: ReactNode;
}) {
  return (
    <Accordion
      expanded={open}
      onChange={(_event, expanded) => onToggle(expanded)}
      disableGutters
      slotProps={{ heading: { component: 'h2' }, transition: { unmountOnExit: true } }}
      variant="outlined"
    >
      <AccordionSummary
        expandIcon={<ThinChevronDownIcon />}
        aria-controls={`${id}-body`}
        id={`${id}-head`}
      >
        <Box
          sx={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: 1, width: '100%' }}
        >
          <Typography component="span" sx={{ fontWeight: 600 }}>
            {name}
          </Typography>
          {tag && <Tag label={tag} />}
          <Typography component="span" sx={{ ml: 'auto', color: 'text.secondary' }}>
            {count}
          </Typography>
        </Box>
      </AccordionSummary>
      <AccordionDetails id={`${id}-body`}>{children}</AccordionDetails>
    </Accordion>
  );
}
