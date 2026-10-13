import Box from '@mui/material/Box';
import {
  PageHeader,
  ThinAddIcon,
  ThinRefreshIcon,
  ThinSaveIcon,
  ThinUndoIcon,
} from '../../../../shared/ui';
import { PageChromeContext, type PageChrome } from '../../../../shared/ui/pageChrome';
import { UI_KIT_PATH } from '../../paths';
import { noop } from '../noop';

// Section 10, «Заголовок страницы» (tech-015 design § 10.3). The page's own head is a
// plain `PageHeader`; this one is a demo, so it stands in an inert chrome: it registers
// nothing with the shell and its `documentTitle` is this page's, so the tab and the
// trail are not touched. This file is one of the guard's two named seams for KIT-22.

const INERT_CHROME: PageChrome = {
  registerHeader: () => noop,
  registerNotFound: () => noop,
  trail: [],
  trailPlacement: 'topBar',
};

const PAGE_TITLE = 'Витрина компонентов';

export function PageHeadDemo() {
  return (
    <PageChromeContext.Provider value={INERT_CHROME}>
      <Box
        sx={(theme) => ({
          maxWidth: theme.spacing(80),
          px: 2,
          border: 1,
          borderColor: 'divider',
          borderRadius: 1,
          backgroundColor: 'background.paper',
        })}
      >
        <PageHeader
          variant="team"
          headingLevel={3}
          title="Платформа"
          documentTitle={PAGE_TITLE}
          primaryAction={{ label: 'Новый рабочий элемент', icon: <ThinAddIcon />, onClick: noop }}
          commands={[
            { label: 'Обновить', icon: <ThinRefreshIcon />, onClick: noop },
            { label: 'Отменить', icon: <ThinUndoIcon />, onClick: noop },
            { label: 'Сохранить', icon: <ThinSaveIcon />, onClick: noop },
          ]}
          views={[
            { label: 'Список', to: UI_KIT_PATH },
            { label: 'Доска', to: `${UI_KIT_PATH}#page-head` },
          ]}
          viewsLabel="Виды демонстрационной страницы"
        />
      </Box>
    </PageChromeContext.Provider>
  );
}
