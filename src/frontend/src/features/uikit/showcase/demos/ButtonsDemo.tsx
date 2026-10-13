import Box from '@mui/material/Box';
import {
  ActionButton,
  SplitButton,
  ThinSaveIcon,
  type ActionButtonProps,
  type ActionTone,
} from '../../../../shared/ui';
import { DemoGrid, DemoGroup } from '../parts';
import { noop } from '../noop';

// Section 5, «Кнопки» (tech-015 design § 10.3): the four tones in four states, and the
// split button enabled, disabled (the reference's clean look) and busy.

const TONES: readonly { tone: ActionTone; name: string }[] = [
  { tone: 'primary', name: 'Основная' },
  { tone: 'neutral', name: 'Нейтральная' },
  { tone: 'subtle', name: 'Тихая' },
  { tone: 'danger', name: 'Опасная' },
];

const STATES: readonly { caption: string; name: string; props: Partial<ActionButtonProps> }[] = [
  { caption: 'В покое (наведите курсор и нажмите Tab)', name: 'в покое', props: {} },
  { caption: 'Отключено', name: 'отключено', props: { disabled: true } },
  { caption: 'Занято', name: 'занято', props: { busy: true } },
  { caption: 'Меню открыто', name: 'меню открыто', props: { menu: { open: true } } },
];

const SPLIT_ITEMS = [{ label: 'Сохранить и закрыть' }, { label: 'Сохранить и создать новую' }];

const SPLITS = [
  { caption: 'Кнопка с меню', name: 'в покое', props: {} },
  { caption: 'Отключено: чистый вид образца', name: 'отключено', props: { disabled: true } },
  { caption: 'Занято', name: 'занято', props: { busy: true } },
] as const;

export function ButtonsDemo() {
  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 3 }}>
      <DemoGrid>
        {STATES.map(({ caption, name: state, props }) => (
          <DemoGroup key={caption} caption={caption}>
            <Box
              sx={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-start', gap: 1 }}
            >
              {TONES.map(({ tone, name }) => (
                <ActionButton
                  key={tone}
                  tone={tone}
                  label={`${name}: ${state}`}
                  onClick={noop}
                  {...props}
                />
              ))}
            </Box>
          </DemoGroup>
        ))}
      </DemoGrid>
      <DemoGrid>
        {SPLITS.map(({ caption, name, props }) => (
          <DemoGroup key={caption} caption={caption}>
            <SplitButton
              label="Сохранить"
              icon={<ThinSaveIcon />}
              groupLabel={`Сохранение: ${name}`}
              menuLabel={`Ещё варианты: ${name}`}
              items={SPLIT_ITEMS}
              onClick={noop}
              {...props}
            />
          </DemoGroup>
        ))}
      </DemoGrid>
    </Box>
  );
}
