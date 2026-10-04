import { Link as RouterLink } from 'react-router-dom';
import Breadcrumbs from '@mui/material/Breadcrumbs';
import Link from '@mui/material/Link';
import Tooltip from '@mui/material/Tooltip';
import Typography from '@mui/material/Typography';
import type { SxProps, Theme } from '@mui/material/styles';
import type { TrailSegment } from './pageChrome';

const segmentSx: SxProps<Theme> = {
  display: 'block',
  maxWidth: (theme) => theme.layout.trailSegmentMaxWidth,
};

/**
 * The page trail (BR-6, design § 3.2): the navigation region «Путь к
 * странице» listing the page's ancestors. An ancestor with a page of its own
 * is a link; one without is plain text, and not a tab stop. Each segment is
 * cut with an ellipsis at `layout.trailSegmentMaxWidth` and keeps its full
 * text in a tooltip.
 */
export function PageTrail({ segments }: { segments: readonly TrailSegment[] }) {
  return (
    <Breadcrumbs
      aria-label="Путь к странице"
      separator="/"
      sx={{ minWidth: 0, '& .MuiBreadcrumbs-ol': { flexWrap: 'nowrap' } }}
    >
      {segments.map((segment) => (
        <Tooltip key={`${segment.label}|${segment.to ?? ''}`} title={segment.label}>
          {segment.to ? (
            <Link component={RouterLink} to={segment.to} underline="hover" noWrap sx={segmentSx}>
              {segment.label}
            </Link>
          ) : (
            <Typography component="span" noWrap color="text.secondary" sx={segmentSx}>
              {segment.label}
            </Typography>
          )}
        </Tooltip>
      ))}
    </Breadcrumbs>
  );
}
