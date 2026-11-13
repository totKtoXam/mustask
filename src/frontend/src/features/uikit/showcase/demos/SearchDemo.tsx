import { useState } from 'react';
import Box from '@mui/material/Box';
import { useTheme } from '@mui/material/styles';
import {
  ActionButton,
  HighlightedText,
  SearchField,
  SearchOption,
  SearchPanel,
  ThinChevronDownIcon,
  ThinSearchIcon,
} from '../../../../shared/ui';
import { UI_KIT_PATH } from '../../paths';
import { DemoGrid, DemoGroup } from '../parts';
import { noop } from '../noop';

// Section «Поиск» (feat-070): the header field, the highlight, the rows and the panel under
// the field. Every part is alive; the panel opens on focus of its field and closes on a
// click outside it or on Esc.

const ROWS = [
  { id: 'demo-search-1', primary: 'Оплата картой', secondary: 'В работе · Альфа' },
  { id: 'demo-search-2', primary: 'Оплата по счёту', secondary: 'Новый · Альфа' },
  { id: 'demo-search-3', primary: 'Приёмка оплаты', secondary: 'Проект · ALP · Логистика' },
];

export function SearchDemo() {
  const theme = useTheme();
  const [value, setValue] = useState('');
  const [open, setOpen] = useState(false);
  const [anchor, setAnchor] = useState<HTMLDivElement | null>(null);
  const [fixed, setFixed] = useState('оплата');
  const [active, setActive] = useState(0);
  return (
    <DemoGrid>
      <DemoGroup caption="Поле в шапке: нажмите в поле, чтобы открыть панель (Esc закрывает, текст остаётся)">
        <SearchField
          anchorRef={setAnchor}
          value={value}
          onChange={setValue}
          label="Поиск"
          placeholder="Поиск…"
          hint="Ctrl K"
          keyShortcuts="Control+K"
          clearLabel="Очистить поле"
          onClear={() => setValue('')}
          maxLength={100}
          aria-expanded={open}
          onFocus={() => setOpen(true)}
          onKeyDown={(event) => {
            if (event.key === 'Escape') setOpen(false);
          }}
        />
        <SearchPanel
          anchorEl={anchor}
          open={open}
          onClose={() => setOpen(false)}
          scope={<span>Искать в: Ромашка</span>}
          fullSearch={
            <ActionButton
              label="Полный поиск"
              tone="subtle"
              icon={<ThinSearchIcon />}
              onClick={noop}
            />
          }
          keys={
            <>
              <span>↑↓ выбрать</span>
              <span>↵ открыть</span>
              <span>Esc закрыть</span>
            </>
          }
        >
          <Box component="ul" role="listbox" aria-label="Результаты поиска" sx={{ m: 0, p: 0 }}>
            {ROWS.map((row, index) => (
              <SearchOption
                key={row.id}
                id={`${row.id}-panel`}
                to={UI_KIT_PATH}
                icon={<ThinSearchIcon />}
                primary={<HighlightedText text={row.primary} query={value} />}
                secondary={row.secondary}
                active={index === 0}
              />
            ))}
          </Box>
        </SearchPanel>
      </DemoGroup>
      <DemoGroup caption="Выделение совпадения: жирный шрифт и фон; регистр не важен, «ё» равно «е»">
        <SearchField
          value={fixed}
          onChange={setFixed}
          label="Запрос для выделения"
          placeholder="Поиск…"
          hint="Ctrl K"
          clearLabel="Очистить запрос"
          onClear={() => setFixed('')}
        />
        <Box sx={{ mt: 1 }}>
          <HighlightedText text="Приёмка оплаты ОПЛАТА" query={fixed} />
        </Box>
      </DemoGroup>
      <DemoGroup caption="Строки: обычная, активная (наведите курсор), с цветной полосой, «Показать ещё»">
        <Box component="ul" role="listbox" aria-label="Строки поиска" sx={{ m: 0, p: 0 }}>
          <SearchOption
            id="demo-row-plain"
            to={UI_KIT_PATH}
            primary="Оплата картой"
            secondary="В работе · Альфа"
            active={active === 0}
            onPointerEnter={() => setActive(0)}
          />
          <SearchOption
            id="demo-row-stripe"
            to={UI_KIT_PATH}
            primary="Логистика"
            secondary="Ромашка"
            stripe={theme.palette.primary.main}
            active={active === 1}
            onPointerEnter={() => setActive(1)}
          />
          <SearchOption
            id="demo-row-more"
            variant="more"
            icon={<ThinChevronDownIcon />}
            primary="Показать ещё"
            active={active === 2}
            onPointerEnter={() => setActive(2)}
          />
        </Box>
      </DemoGroup>
    </DemoGrid>
  );
}
