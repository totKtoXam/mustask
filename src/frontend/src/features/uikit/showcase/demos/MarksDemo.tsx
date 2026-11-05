import type { ReactNode } from 'react';
import Box from '@mui/material/Box';
import { PersonLabel, StateLabel, TypeLabel, UserAvatar } from '../../../../shared/ui';
import { DEMO_PEOPLE, DEMO_STATES, DEMO_TYPES } from '../data';
import { DemoGrid, DemoGroup } from '../parts';

// Section 7, «Типы, состояния и люди» (tech-015 design § 10.3): a mark is always followed
// by its name; colour and icon only help.

function Column({ children }: { children: ReactNode }) {
  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-start', gap: 1 }}>
      {children}
    </Box>
  );
}

export function MarksDemo() {
  return (
    <DemoGrid>
      <DemoGroup caption="Типы работ">
        <Column>
          {DEMO_TYPES.map(({ appearance, name }) => (
            <TypeLabel key={appearance} appearance={appearance} name={name} />
          ))}
        </Column>
      </DemoGroup>
      <DemoGroup caption="Типы работ, компактно (как в таблице)">
        <Column>
          {DEMO_TYPES.map(({ appearance, name }) => (
            <TypeLabel key={appearance} appearance={appearance} name={name} size="compact" />
          ))}
        </Column>
      </DemoGroup>
      <DemoGroup caption="Выбранный и над заголовком карточки">
        <Column>
          <TypeLabel appearance="story" name="Пользовательская история" selected />
          <TypeLabel appearance="bug" name="Баг MSK-B-3" eyebrow />
        </Column>
      </DemoGroup>
      <DemoGroup caption="Состояния в строке, 8 пикселей">
        <Column>
          {DEMO_STATES.map(({ code, name }) => (
            <StateLabel key={code} categoryCode={code} name={name} />
          ))}
        </Column>
      </DemoGroup>
      <DemoGroup caption="Состояния в поле, 10 пикселей">
        <Column>
          {DEMO_STATES.map(({ code, name }) => (
            <StateLabel key={code} categoryCode={code} name={name} size="field" />
          ))}
        </Column>
      </DemoGroup>
      <DemoGroup caption="Длинное название обрезается (clip)">
        <Box sx={(theme) => ({ width: theme.spacing(15) })}>
          <StateLabel categoryCode="InProgress" name="На согласовании у владельца продукта" clip />
        </Box>
      </DemoGroup>
      <DemoGroup caption="Аватары, 32 и 24 пикселя">
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
          <UserAvatar name={DEMO_PEOPLE[0]} size="menu" />
          <UserAvatar name={DEMO_PEOPLE[1]} size="inline" />
        </Box>
      </DemoGroup>
      <DemoGroup caption="Человек и «Не назначен»">
        <Column>
          <PersonLabel name={DEMO_PEOPLE[0]} />
          <PersonLabel name={null} />
        </Column>
      </DemoGroup>
    </DemoGrid>
  );
}
