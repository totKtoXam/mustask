import { useState } from 'react';
import SvgIcon from '@mui/material/SvgIcon';
import { IconToggleGroup } from '../../../../shared/ui';
import { DemoGrid, DemoGroup } from '../parts';

// «Переключатели со значками» (feat-049): one of N, every value its own icon button.

function Bars({ filled }: { filled: readonly number[] }) {
  return (
    <SvgIcon>
      {[0, 1, 2].map((row) => (
        <rect
          key={row}
          x="4"
          y={3.5 + row * 6.7}
          width="16"
          height="3.6"
          rx="1"
          fill={filled.includes(row) ? 'currentColor' : 'none'}
          stroke="currentColor"
          strokeWidth="1.8"
        />
      ))}
    </SvgIcon>
  );
}

export function ToggleGroupDemo() {
  const [value, setValue] = useState('one');
  return (
    <DemoGrid>
      <DemoGroup caption="Выбрано одно значение; наведите курсор, чтобы увидеть подсказку, и нажмите Tab">
        <IconToggleGroup
          label="Режим уровней"
          value={value}
          onChange={setValue}
          items={[
            {
              value: 'one',
              label: 'Только выбранные уровни',
              icon: <Bars filled={[1]} />,
              tooltip: 'Режим уровней: Только выбранные уровни. Показаны отмеченные уровни',
            },
            {
              value: 'down',
              label: 'Уровень и вложенные',
              icon: <Bars filled={[1, 2]} />,
              tooltip:
                'Режим уровней: Уровень и вложенные. Самый верхний из отмеченных и все более глубокие',
            },
          ]}
        />
      </DemoGroup>
    </DemoGrid>
  );
}
