import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import { ColumnLayout, Section, Surface } from '../../../../shared/ui';
import { DemoGroup } from '../parts';

// Section 13, «Разделы и колонки» (tech-015 design § 10.3): the two surfaces, a section
// with a level-3 head, and one `ColumnLayout` in three fixed-width boxes. The columns
// follow the container's content width, not the window's: with the layout's own 12 px
// gutters 720 is a little over the two-column threshold and 1120 over the three-column one.

const BOX_WIDTHS = [1120, 720, 520] as const;

function Text({ children }: { children: string }) {
  return (
    <Typography variant="body2" component="p">
      {children}
    </Typography>
  );
}

function ColumnsBox({ width }: { width: number }) {
  return (
    <DemoGroup caption={`Блок шириной ${width} пикселей`}>
      <Box sx={{ overflowX: 'auto' }}>
        <Box
          role="group"
          aria-label={`Блок ${width}`}
          sx={{
            boxSizing: 'border-box',
            width,
            border: 1,
            borderColor: 'divider',
            backgroundColor: 'background.paper',
          }}
        >
          <ColumnLayout
            main={
              <>
                <Section title="Основное" headingLevel={3}>
                  <Text>Главная колонка.</Text>
                </Section>
                <Section title="Детали" headingLevel={3}>
                  <Text>Второй раздел той же колонки.</Text>
                </Section>
              </>
            }
            side={
              <Section title="Свойства" headingLevel={3}>
                <Text>Боковая колонка.</Text>
              </Section>
            }
            extra={
              <Section title="Связи" headingLevel={3}>
                <Text>Дополнительная колонка.</Text>
              </Section>
            }
          />
        </Box>
      </Box>
    </DemoGroup>
  );
}

export function SectionsDemo() {
  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 3 }}>
      <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 2 }}>
        <Surface variant="card" padding="dense">
          <Text>Поверхность «card»: белая, с мягким свечением.</Text>
        </Surface>
        <Surface variant="band" padding="dense">
          <Text>Поверхность «band»: серая полоса свойств.</Text>
        </Surface>
      </Box>
      <DemoGroup caption="Раздел с заголовком третьего уровня и числом">
        <Section title="Теги раздела" headingLevel={3} count={2}>
          <Text>Раздел — заголовок, линия и содержимое. Своего отступа снаружи у него нет.</Text>
        </Section>
      </DemoGroup>
      {BOX_WIDTHS.map((width) => (
        <ColumnsBox key={width} width={width} />
      ))}
    </Box>
  );
}
