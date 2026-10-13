import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import { useTheme, type Theme } from '@mui/material/styles';
import { DemoGrid, DemoGroup } from '../parts';

// Section 3, «Размеры и отступы» (tech-015 design § 10.3): the numbers of `theme.layout`
// (each group of it), `theme.density` — read at run time, so the list is the theme's.

/** A token's value as the page shows it: a number in px, a group as its entries. */
function describeValue(value: unknown): string {
  if (typeof value === 'number') return `${value} px`;
  if (Array.isArray(value)) return value.map(describeValue).join(', ');
  if (value !== null && typeof value === 'object') {
    return Object.entries(value)
      .map(([key, inner]) => `${key} ${describeValue(inner)}`)
      .join(', ');
  }
  return String(value);
}

interface TokenGroup {
  caption: string;
  entries: readonly (readonly [string, string])[];
}

function tokenGroups(theme: Theme): TokenGroup[] {
  const layoutEntries = Object.entries(theme.layout);
  const groups: TokenGroup[] = [
    {
      caption: 'layout',
      entries: layoutEntries
        .filter(([, value]) => typeof value === 'number')
        .map(([key, value]) => [key, describeValue(value)] as const),
    },
    {
      caption: 'density',
      entries: Object.entries(theme.density).map(
        ([key, value]) => [key, describeValue(value)] as const,
      ),
    },
  ];
  for (const [name, value] of layoutEntries) {
    if (value !== null && typeof value === 'object' && !Array.isArray(value)) {
      groups.push({
        caption: `layout.${name}`,
        entries: Object.entries(value).map(([key, inner]) => [key, describeValue(inner)] as const),
      });
    }
  }
  return groups;
}

export function SizesDemo() {
  const theme = useTheme();
  return (
    <DemoGrid>
      {tokenGroups(theme).map((group) => (
        <DemoGroup key={group.caption} caption={group.caption}>
          <Box
            component="dl"
            sx={{
              display: 'grid',
              gridTemplateColumns: 'minmax(0, 1fr) auto',
              columnGap: 2,
              rowGap: 0.5,
              m: 0,
            }}
          >
            {group.entries.map(([key, text]) => (
              <Box key={key} sx={{ display: 'contents' }}>
                <Typography variant="body2" component="dt" sx={{ overflowWrap: 'anywhere' }}>
                  {key}
                </Typography>
                <Typography
                  variant="body2"
                  component="dd"
                  sx={{ m: 0, color: 'text.secondary', textAlign: 'right' }}
                >
                  {text}
                </Typography>
              </Box>
            ))}
          </Box>
        </DemoGroup>
      ))}
    </DemoGrid>
  );
}
