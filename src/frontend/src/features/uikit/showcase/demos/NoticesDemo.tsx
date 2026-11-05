import Box from '@mui/material/Box';
import { ActionButton, ErrorBanner, InlineNotice, useNotice } from '../../../../shared/ui';
import { DemoGrid, DemoGroup, DemoRow } from '../parts';
import { noop } from '../noop';

// Section 17, «Уведомления» (tech-015 design § 10.3, § 10.5): the three short messages at
// the bottom left, the three inline severities, and an error with «Повторить».

export function NoticesDemo() {
  const notice = useNotice();
  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 3 }}>
      <DemoGroup caption="Короткое сообщение внизу слева исчезает через 6 секунд">
        <DemoRow>
          <ActionButton
            label="Показать уведомление"
            onClick={() => notice.show('Изменения сохранены.')}
          />
          <ActionButton
            label="Показать успех"
            onClick={() => notice.show('Рабочий элемент MSK-US-24 создан.', 'success')}
          />
          <ActionButton
            label="Показать ошибку"
            onClick={() => notice.show('Не удалось сохранить изменения.', 'error')}
          />
        </DemoRow>
      </DemoGroup>
      <DemoGrid>
        <DemoGroup caption="Ошибка с действием">
          <InlineNotice severity="error" action={{ label: 'Повторить', onClick: noop }}>
            Не удалось загрузить данные.
          </InlineNotice>
        </DemoGroup>
        <DemoGroup caption="Предупреждение">
          <InlineNotice severity="warning">Рабочий элемент изменён другим участником.</InlineNotice>
        </DemoGroup>
        <DemoGroup caption="Сведения">
          <InlineNotice severity="info">Рабочий элемент доступен только для чтения.</InlineNotice>
        </DemoGroup>
        <DemoGroup caption="Ошибка запроса (ErrorBanner)">
          <ErrorBanner error={new Error('Не удалось загрузить данные.')} />
        </DemoGroup>
      </DemoGrid>
    </Box>
  );
}
