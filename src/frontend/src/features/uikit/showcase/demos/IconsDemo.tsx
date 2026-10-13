import type { ComponentType } from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import {
  BacklogIcon,
  IconAction,
  ThinAddIcon,
  ThinChevronDownIcon,
  ThinChevronRightIcon,
  ThinCloseIcon,
  ThinExpandIcon,
  ThinMoreVerticalIcon,
  ThinPersonIcon,
  ThinRefreshIcon,
  ThinRemoveIcon,
  ThinSaveIcon,
  ThinUndoIcon,
} from '../../../../shared/ui';
import { DemoGroup, DemoRow } from '../parts';
import { noop } from '../noop';

// Section 4, «Значки» (tech-015 design § 10.3): the eleven hairline glyphs at 24 px with
// their names, `BacklogIcon`, and `IconAction` in its four sizes and its states.

const GLYPHS: readonly (readonly [string, ComponentType])[] = [
  ['ThinChevronDownIcon', ThinChevronDownIcon],
  ['ThinChevronRightIcon', ThinChevronRightIcon],
  ['ThinAddIcon', ThinAddIcon],
  ['ThinRemoveIcon', ThinRemoveIcon],
  ['ThinCloseIcon', ThinCloseIcon],
  ['ThinExpandIcon', ThinExpandIcon],
  ['ThinMoreVerticalIcon', ThinMoreVerticalIcon],
  ['ThinRefreshIcon', ThinRefreshIcon],
  ['ThinUndoIcon', ThinUndoIcon],
  ['ThinSaveIcon', ThinSaveIcon],
  ['ThinPersonIcon', ThinPersonIcon],
  ['BacklogIcon', BacklogIcon],
];

export function IconsDemo() {
  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 3 }}>
      <DemoGroup caption="Значки панели, 24 пикселя">
        <Box
          component="ul"
          sx={{
            display: 'grid',
            gridTemplateColumns: 'repeat(auto-fill, minmax(168px, 1fr))',
            gap: 2,
            listStyle: 'none',
            m: 0,
            p: 0,
          }}
        >
          {GLYPHS.map(([name, Glyph]) => (
            <Box key={name} component="li" sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
              <Glyph />
              <Typography variant="caption" component="span" sx={{ color: 'text.secondary' }}>
                {name}
              </Typography>
            </Box>
          ))}
        </Box>
      </DemoGroup>
      <DemoGroup caption="Кнопка-значок в четырёх размерах">
        <DemoRow>
          <IconAction size="row" label="Размер: строка" icon={<ThinAddIcon />} onClick={noop} />
          <IconAction
            size="chevron"
            label="Размер: шеврон"
            icon={<ThinChevronRightIcon />}
            onClick={noop}
          />
          <IconAction
            size="header"
            label="Размер: заголовок"
            icon={<ThinRemoveIcon />}
            onClick={noop}
          />
          <IconAction size="bar" label="Размер: панель" icon={<ThinRefreshIcon />} onClick={noop} />
        </DemoRow>
      </DemoGroup>
      <DemoGroup caption="Состояния (наведите курсор и нажмите Tab)">
        <DemoRow>
          <IconAction size="bar" label="В покое" icon={<ThinRefreshIcon />} onClick={noop} />
          <IconAction
            size="bar"
            tone="muted"
            label="Приглушённо"
            icon={<ThinRefreshIcon />}
            onClick={noop}
          />
          <IconAction
            size="bar"
            inactive
            label="Неактивно"
            icon={<ThinUndoIcon />}
            onClick={noop}
          />
          <IconAction size="bar" busy label="Занято" icon={<ThinSaveIcon />} onClick={noop} />
          <IconAction
            size="bar"
            disabled
            label="Отключено"
            icon={<ThinMoreVerticalIcon />}
            onClick={noop}
          />
          <IconAction
            size="bar"
            outlined
            label="С рамкой"
            icon={<ThinExpandIcon />}
            onClick={noop}
          />
        </DemoRow>
      </DemoGroup>
    </Box>
  );
}
