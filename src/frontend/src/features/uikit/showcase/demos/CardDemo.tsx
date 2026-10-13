import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import {
  CardHeaderBar,
  IconAction,
  PanelShell,
  PersonLabel,
  PropertyBand,
  StateLabel,
  ThinRefreshIcon,
} from '../../../../shared/ui';
import { DEMO_PEOPLE } from '../data';
import { DemoGroup } from '../parts';
import { noop } from '../noop';
import { useDemoCard } from './useDemoCard';

// Section 14, «Карточка» (tech-015 design § 10.3): the `MSK-US-2` card as a page panel —
// header, band with its fixed blocks and a one-tab strip, body in columns with «Описание»
// as a text under its head — and a bug in its own colour in a 600 px box, where the
// header's third row wraps and the band's blocks stack.

function StoryCard() {
  const card = useDemoCard({ headingLevel: 3, idBase: 'showcase-card' });
  return (
    <PanelShell header={card.header} band={card.band}>
      {card.body}
    </PanelShell>
  );
}

function NarrowBugCard() {
  return (
    <Box sx={(theme) => ({ width: theme.spacing(75), maxWidth: '100%' })}>
      <PanelShell
        header={
          <CardHeaderBar
            appearance="bug"
            typeLabel="Баг"
            identifier="MSK-B-3"
            headingLevel={3}
            title={
              <Typography variant="cardTitle" component="span">
                Ссылка не открывается
              </Typography>
            }
            meta={<PersonLabel name={DEMO_PEOPLE[1]} />}
            actions={
              <IconAction
                size="bar"
                label="Обновить MSK-B-3"
                icon={<ThinRefreshIcon />}
                onClick={noop}
              />
            }
          />
        }
        band={
          <PropertyBand
            blocks={[
              <StateLabel key="state" categoryCode="Proposed" name="Новая" size="field" />,
              <PersonLabel key="author" name={DEMO_PEOPLE[0]} />,
            ]}
            meta="Изменено 5 октября"
          />
        }
      >
        {null}
      </PanelShell>
    </Box>
  );
}

export function CardDemo() {
  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 3 }}>
      <DemoGroup caption="Карточка на странице (измените заголовок — кнопка «Сохранить» оживёт)">
        <StoryCard />
      </DemoGroup>
      <DemoGroup caption="Баг в своём цвете, узкая форма: третья строка шапки переносится, блоки встают друг под друга">
        <NarrowBugCard />
      </DemoGroup>
    </Box>
  );
}
