import { useState } from 'react';
import Typography from '@mui/material/Typography';
import AttachFileOutlinedIcon from '@mui/icons-material/AttachFileOutlined';
import ChatBubbleOutlineOutlinedIcon from '@mui/icons-material/ChatBubbleOutlineOutlined';
import HistoryOutlinedIcon from '@mui/icons-material/HistoryOutlined';
import LinkOutlinedIcon from '@mui/icons-material/LinkOutlined';
import { BoxedTabPanel, BoxedTabs, type BoxedTab } from '../../../../shared/ui';
import { DemoGrid, DemoGroup } from '../parts';
import { noop } from '../noop';

// Section 11, «Вкладки карточки» (tech-015 design § 10.3): a single tab (a strip of one is
// allowed), four tabs with icons and counts, and the panel linked to its tab.

const SINGLE: BoxedTab[] = [{ value: 'details', label: 'Детали' }];

const FOUR: BoxedTab[] = [
  { value: 'links', label: 'Связи', icon: <LinkOutlinedIcon />, count: 1 },
  { value: 'history', label: 'История', icon: <HistoryOutlinedIcon /> },
  { value: 'files', label: 'Вложения', icon: <AttachFileOutlinedIcon />, count: 0 },
  { value: 'comments', label: 'Комментарии', icon: <ChatBubbleOutlineOutlinedIcon />, count: 3 },
];

export function TabsDemo() {
  const [value, setValue] = useState('links');
  return (
    <DemoGrid>
      <DemoGroup caption="Одна вкладка">
        <BoxedTabs
          idBase="showcase-single"
          aria-label="Одна вкладка"
          value="details"
          onChange={noop}
          tabs={SINGLE}
        />
        <BoxedTabPanel idBase="showcase-single" value="details" selected>
          <Typography variant="body2" component="p" sx={{ pt: 1.5 }}>
            Содержимое вкладки «Детали».
          </Typography>
        </BoxedTabPanel>
      </DemoGroup>
      <DemoGroup caption="Четыре вкладки: стрелки двигают фокус, Enter выбирает">
        <BoxedTabs
          idBase="showcase-four"
          aria-label="Разделы демонстрационной карточки"
          value={value}
          onChange={setValue}
          tabs={FOUR}
        />
        {FOUR.map((tab) => (
          <BoxedTabPanel
            key={tab.value}
            idBase="showcase-four"
            value={tab.value}
            selected={tab.value === value}
          >
            <Typography variant="body2" component="p" sx={{ pt: 1.5 }}>
              {`Содержимое вкладки «${tab.label}».`}
            </Typography>
          </BoxedTabPanel>
        ))}
      </DemoGroup>
    </DemoGrid>
  );
}
