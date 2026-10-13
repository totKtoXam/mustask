import Box from '@mui/material/Box';
import { InlineEmpty, LiveRegion, TruncatedText, VisuallyHidden } from '../../../../shared/ui';
import { UI_KIT_PATH } from '../../paths';
import { DEMO_LONG_TITLE } from '../data';
import { DemoGrid, DemoGroup } from '../parts';

// Section 9, «Длинный текст» (tech-015 design § 10.3): a long title cut in a narrow box,
// in its plain and link looks; the text only a screen reader hears; an empty list.

export function TextDemo() {
  return (
    <DemoGrid>
      <DemoGroup caption="Обычный вид: полный текст в подсказке браузера (наведите курсор)">
        <Box sx={(theme) => ({ width: theme.spacing(30) })}>
          <TruncatedText>{DEMO_LONG_TITLE}</TruncatedText>
        </Box>
      </DemoGroup>
      <DemoGroup caption="Ссылка с подсказкой на странице (наведите курсор)">
        <Box sx={(theme) => ({ width: theme.spacing(30) })}>
          <TruncatedText to={UI_KIT_PATH} look="link" hint="tooltip">
            {DEMO_LONG_TITLE}
          </TruncatedText>
        </Box>
      </DemoGroup>
      <DemoGroup caption="Слышно только читалке экрана">
        <VisuallyHidden>Скрытая подпись для читалки экрана</VisuallyHidden>
        <LiveRegion text="Показано 3 тега" />
      </DemoGroup>
      <DemoGroup caption="Пустой список внутри раздела">
        <InlineEmpty>Дочерних элементов нет.</InlineEmpty>
      </DemoGroup>
    </DemoGrid>
  );
}
