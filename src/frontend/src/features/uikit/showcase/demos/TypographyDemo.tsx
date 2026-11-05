import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import { useTheme, type Theme } from '@mui/material/styles';
import { DemoGroup } from '../parts';

// Section 2, «Шрифт и размеры текста» (tech-015 design § 10.3): every entry of the
// theme's type scale set in one sample line, with the size, line and weight it reads.

const SAMPLE = 'Съешьте ещё этих мягких французских булок';

const VARIANTS = [
  'h1',
  'h2',
  'h3',
  'body1',
  'body2',
  'subtitle2',
  'caption',
  'button',
  'tableCell',
  'cardTitle',
] as const;

type ScaleVariant = (typeof VARIANTS)[number];

/** The size, line height and weight of a variant in px, read back from the theme's rem values. */
function describeVariant(theme: Theme, variant: ScaleVariant): string {
  const style = theme.typography[variant];
  const size = parseFloat(String(style.fontSize)) * theme.typography.htmlFontSize;
  const line = Number(style.lineHeight) * size;
  return `${variant}: ${Math.round(size)} / ${Math.round(line)} px, вес ${style.fontWeight}`;
}

export function TypographyDemo() {
  const theme = useTheme();
  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      {VARIANTS.map((variant) => (
        <DemoGroup key={variant} caption={describeVariant(theme, variant)}>
          <Typography variant={variant} component="p">
            {SAMPLE}
          </Typography>
        </DemoGroup>
      ))}
    </Box>
  );
}
