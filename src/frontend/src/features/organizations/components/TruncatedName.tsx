import { Link as RouterLink } from 'react-router-dom';
import Box from '@mui/material/Box';
import Link from '@mui/material/Link';
import Tooltip from '@mui/material/Tooltip';

const truncated = {
  display: 'block',
  overflow: 'hidden',
  textOverflow: 'ellipsis',
  whiteSpace: 'nowrap',
} as const;

/**
 * A name in a cell (design § 4.6): one line, a long one cut with an
 * ellipsis, the full text in a `Tooltip` and in the accessible name. A link
 * when the row opens a page, plain text when it does not (a deactivated row).
 */
export function TruncatedName({ name, to }: { name: string; to?: string }) {
  return (
    <Tooltip title={name}>
      {to ? (
        <Link component={RouterLink} to={to} sx={truncated}>
          {name}
        </Link>
      ) : (
        <Box component="span" sx={truncated}>
          {name}
        </Box>
      )}
    </Tooltip>
  );
}
