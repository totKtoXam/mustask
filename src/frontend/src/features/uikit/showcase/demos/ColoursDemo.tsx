import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import { useTheme, type Theme } from '@mui/material/styles';
import { DEMO_STATES, DEMO_TYPES } from '../data';
import { DemoGrid } from '../parts';

// Section 1, «Цвета» (tech-015 design § 10.3): every swatch reads its colour from the
// theme at run time, so the page can never show a value the panel does not use.

interface Swatch {
  name: string;
  read: (theme: Theme) => string;
}

const GROUPS: readonly { caption: string; swatches: readonly Swatch[] }[] = [
  {
    caption: 'Основа',
    swatches: [
      { name: 'background.default', read: (t) => t.palette.background.default },
      { name: 'background.paper', read: (t) => t.palette.background.paper },
      { name: 'text.primary', read: (t) => t.palette.text.primary },
      { name: 'text.secondary', read: (t) => t.palette.text.secondary },
      { name: 'divider', read: (t) => t.palette.divider },
      { name: 'shell.navigation', read: (t) => t.palette.shell.navigation },
      { name: 'shell.tableHeader', read: (t) => t.palette.shell.tableHeader },
      { name: 'shell.rowSelected', read: (t) => t.palette.shell.rowSelected },
      { name: 'action.hover', read: (t) => t.palette.action.hover },
      { name: 'shell.inputBorder', read: (t) => t.palette.shell.inputBorder },
      { name: 'shell.fieldHover', read: (t) => t.palette.shell.fieldHover },
      { name: 'shell.cardLabel', read: (t) => t.palette.shell.cardLabel },
      { name: 'shell.sectionRule', read: (t) => t.palette.shell.sectionRule },
    ],
  },
  {
    caption: 'Акцент и ссылки',
    swatches: [
      { name: 'primary.main', read: (t) => t.palette.primary.main },
      { name: 'primary.dark', read: (t) => t.palette.primary.dark },
      { name: 'shell.addAction', read: (t) => t.palette.shell.addAction },
      { name: 'shell.buttonNeutral', read: (t) => t.palette.shell.buttonNeutral },
      { name: 'shell.buttonNeutralHover', read: (t) => t.palette.shell.buttonNeutralHover },
      { name: 'shell.splitDisabledFill', read: (t) => t.palette.shell.splitDisabledFill },
      { name: 'shell.splitDisabledText', read: (t) => t.palette.shell.splitDisabledText },
    ],
  },
  {
    caption: 'Смысловые',
    swatches: [
      { name: 'error.main', read: (t) => t.palette.error.main },
      { name: 'warning.main', read: (t) => t.palette.warning.main },
      { name: 'success.main', read: (t) => t.palette.success.main },
      { name: 'info.main', read: (t) => t.palette.info.main },
    ],
  },
  {
    caption: 'Типы работ',
    swatches: DEMO_TYPES.map(({ appearance, name }) => ({
      name: `${appearance} — ${name}`,
      read: (t: Theme) => t.workTracking.typeAppearance(appearance).colour,
    })),
  },
  {
    caption: 'Категории состояний',
    swatches: DEMO_STATES.map(({ code, name }) => ({
      name: `${code} — ${name}`,
      read: (t: Theme) => t.workTracking.categoryColour(code),
    })),
  },
];

function SwatchCell({ name, value }: { name: string; value: string }) {
  return (
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, minWidth: 0 }}>
      <Box
        aria-hidden="true"
        sx={(theme) => ({
          width: theme.spacing(6),
          height: theme.density.controlHeight,
          flexShrink: 0,
          borderRadius: 1,
          border: 1,
          borderColor: 'divider',
          backgroundColor: value,
        })}
      />
      <Box sx={{ minWidth: 0 }}>
        <Typography variant="body2" component="p" sx={{ overflowWrap: 'anywhere' }}>
          {name}
        </Typography>
        <Typography variant="caption" component="p" sx={{ color: 'text.secondary' }}>
          {value}
        </Typography>
      </Box>
    </Box>
  );
}

export function ColoursDemo() {
  const theme = useTheme();
  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 3 }}>
      {GROUPS.map((group) => (
        <Box key={group.caption}>
          <Typography variant="caption" component="p" sx={{ color: 'text.secondary', mb: 1 }}>
            {group.caption}
          </Typography>
          <DemoGrid>
            {group.swatches.map((swatch) => (
              <SwatchCell key={swatch.name} name={swatch.name} value={swatch.read(theme)} />
            ))}
          </DemoGrid>
        </Box>
      ))}
    </Box>
  );
}
