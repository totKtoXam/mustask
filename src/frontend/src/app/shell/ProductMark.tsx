import Box from '@mui/material/Box';
import { useTheme } from '@mui/material/styles';

/**
 * The product logo (feat-055): the mark — a ring around a disc with a check, an amber dot on the
 * ring — and the wordmark «mustask», whose amber «t» is the one «must» and «task» share. Both are
 * decorative: the link around them carries the product name as its accessible name.
 */
export function ProductMark() {
  const { palette } = useTheme();
  const { logoRing, logoMust, logoAccent } = palette.shell;
  return (
    <Box component="span" sx={{ display: 'inline-flex', alignItems: 'center', gap: 1 }}>
      <Box
        component="svg"
        viewBox="-50 -50 100 100"
        aria-hidden="true"
        focusable="false"
        data-testid="product-mark-icon"
        sx={{ width: 28, height: 28, flexShrink: 0 }}
      >
        <circle r="44" fill="none" stroke={logoRing} strokeWidth="8" />
        <circle r="28" fill={palette.primary.main} />
        <path
          d="M-12.5 0 L-3.5 9 L13.5 -9"
          fill="none"
          stroke={palette.primary.contrastText}
          strokeWidth="7"
          strokeLinecap="round"
          strokeLinejoin="round"
        />
        <circle cx="31.1" cy="-31.1" r="10" fill={logoAccent} />
      </Box>
      <Box component="span" aria-hidden="true" data-testid="product-mark-wordmark">
        <Box component="span" sx={{ color: logoMust }}>
          mus
        </Box>
        <Box component="span" sx={{ color: logoAccent }}>
          t
        </Box>
        ask
      </Box>
    </Box>
  );
}
