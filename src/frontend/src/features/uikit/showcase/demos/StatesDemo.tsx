import InboxOutlinedIcon from '@mui/icons-material/InboxOutlined';
import FilterListOffIcon from '@mui/icons-material/FilterListOff';
import LockOutlinedIcon from '@mui/icons-material/LockOutlined';
import {
  CardSkeleton,
  FieldsSkeleton,
  NotFound,
  StateFrame,
  ThinAddIcon,
} from '../../../../shared/ui';
import { DemoGrid, DemoGroup } from '../parts';
import { noop } from '../noop';

// Section 18, «Пустые, ошибочные и загружаемые состояния» (tech-015 design § 10.3, § 10.5).
// A frame here is compact: the full-page `AccessDenied` and `NotFound` rename the tab and
// add an `h1`, so the page's own gate (§ 10.1) is where `AccessDenied` is seen. The texts
// are the canon's (mod-015 § 7.2).

export function StatesDemo() {
  return (
    <DemoGrid>
      <DemoGroup caption="Пусто">
        <StateFrame
          compact
          icon={InboxOutlinedIcon}
          heading="Бэклог пуст"
          hint="Добавьте рабочий элемент — он встанет в конец бэклога."
          action={{ label: 'Новый рабочий элемент', icon: <ThinAddIcon />, onClick: noop }}
        />
      </DemoGroup>
      <DemoGroup caption="Пусто после фильтра">
        <StateFrame
          compact
          icon={FilterListOffIcon}
          heading="Ничего не найдено"
          hint="Ни один рабочий элемент не подходит под заданные условия фильтра."
          action={{ label: 'Сбросить фильтр', onClick: noop }}
        />
      </DemoGroup>
      <DemoGroup caption="Нет доступа">
        <StateFrame
          compact
          icon={LockOutlinedIcon}
          heading="Доступ ограничен"
          hint="У вас нет прав для просмотра этого раздела. Обратитесь к администратору, чтобы запросить доступ."
        />
      </DemoGroup>
      <DemoGroup caption="Не найдено">
        <NotFound level="workItem" compact />
      </DemoGroup>
      <DemoGroup caption="Загрузка полей">
        <FieldsSkeleton count={3} />
      </DemoGroup>
      <DemoGroup caption="Загрузка карточки">
        <CardSkeleton />
      </DemoGroup>
    </DemoGrid>
  );
}
