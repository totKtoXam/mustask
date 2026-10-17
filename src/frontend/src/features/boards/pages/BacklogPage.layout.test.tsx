import { afterEach, describe, expect, it } from 'vitest';
import { act, waitFor, within } from '@testing-library/react';
import { focusManager } from '@tanstack/react-query';
import { theme } from '../../../app/theme';
import { CATEGORY_MARKER_CLASS } from '../../../shared/ui/WorkTrackingMarks';
import { serveBoards } from '../testApi';
import {
  CELL,
  addControl,
  cellOf,
  cellText,
  expandControl,
  findBacklogTable,
  orders,
  press,
  rowOf,
  rowsOf,
  titleLink,
} from '../testBacklog';
import { fullTreeDataset, mskRecord, stateId } from '../testFixtures';
import { backlogAddress, mskForm, renderBoards } from '../testRender';

// feat-027 US-1: the table laid out as the reference screen (design § 4). The
// data is US-1's (`fullTreeDataset`); rows are found by `data-identifier`.

afterEach(() => {
  focusManager.setFocused(undefined);
});

/** `#242424` → `rgb(36, 36, 36)`, the form jsdom's computed style reports. */
function rgb(hex: string): string {
  const digits = hex.replace('#', '');
  const [red, green, blue] = [0, 2, 4].map((offset) =>
    parseInt(digits.slice(offset, offset + 2), 16),
  );
  return `rgb(${red}, ${green}, ${blue})`;
}

const HEADERS = [
  'Иерархия',
  'Порядок',
  'Тип',
  'Заголовок',
  'Статус',
  'Команда',
  'Трудоёмкость',
  'Бизнес-ценность',
  'Вид ценности',
];

/** A header cell's name: its `aria-label` where it has one, else its text. */
function headerNames(table: HTMLElement): string[] {
  return within(table)
    .getAllByRole('columnheader')
    .map((header) => header.getAttribute('aria-label') ?? header.textContent ?? '');
}

describe('US-1 Scenario: The columns stand in the reference screen’s order', () => {
  it('reads «Иерархия», «Порядок», «Тип», «Заголовок», «Статус», «Команда», «Трудоёмкость», «Бизнес-ценность», «Вид ценности» — and no ID, «Родитель», «Исполнитель», «Приоритет», «Действия» or handle column', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));

    const table = await findBacklogTable();

    expect(headerNames(table)).toEqual(HEADERS);
    for (const gone of ['ID', 'Родитель', 'Исполнитель', 'Приоритет', 'Действия', 'Перемещение']) {
      expect(within(table).queryByRole('columnheader', { name: gone })).toBeNull();
    }
    expect(within(table).getAllByRole('columnheader')).toHaveLength(9);
  });

  it('«Фичи» and «Истории» have the same headers', async () => {
    serveBoards(fullTreeDataset());
    const view = renderBoards(backlogAddress('features'));
    expect(headerNames(await findBacklogTable())).toEqual(HEADERS);
    view.unmount();

    renderBoards(backlogAddress('stories'));
    expect(headerNames(await findBacklogTable())).toEqual(HEADERS);
  });

  it('draws «Трудоёмкость» and «Бизнес-ценность» cut with an ellipsis and still names them in full, the full label their tooltip — left-aligned, as the reference draws them (D-10)', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();

    for (const label of ['Трудоёмкость', 'Бизнес-ценность']) {
      const header = within(table).getByRole('columnheader', { name: label });
      expect(header).toHaveAttribute('title', label);
      expect(header).toHaveTextContent(label);
      const clipped = header.firstElementChild as HTMLElement;
      expect(getComputedStyle(clipped).textOverflow).toBe('ellipsis');
      expect(getComputedStyle(clipped).whiteSpace).toBe('nowrap');
      expect(getComputedStyle(header).textAlign).toBe('left');
    }
  });

  it('draws the header separators after «Тип», «Заголовок», «Статус», «Команда», «Трудоёмкость» and «Бизнес-ценность» only', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();

    const separated = within(table)
      .getAllByRole('columnheader')
      .filter((header) => header.classList.contains('BacklogHead-separated'))
      .map((header) => header.textContent);
    expect(separated).toEqual([
      'Тип',
      'Заголовок',
      'Статус',
      'Команда',
      'Трудоёмкость',
      'Бизнес-ценность',
    ]);
  });

  it('fixes the widths of the eight columns and lets «Заголовок» take the rest, the table never narrower than 1 180 px', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();

    const widths = Array.from(table.querySelectorAll('col')).map((col) => col.style.width);
    expect(widths).toEqual([
      '72px',
      '68px',
      '184px',
      '',
      '104px',
      '120px',
      '64px',
      '64px',
      '104px',
    ]);
    expect(getComputedStyle(table).tableLayout).toBe('fixed');
    expect(getComputedStyle(table).minWidth).toBe('1180px');
  });
});

describe('US-1 Scenario: The hierarchy column’s header holds the two level buttons', () => {
  it('holds «Развернуть на один уровень» (a plus) and «Свернуть на один уровень» (a minus), 24 px squares with a thin dark outline', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();

    const header = within(table).getByRole('columnheader', { name: 'Иерархия' });
    const expand = within(header).getByRole('button', { name: 'Развернуть на один уровень' });
    const collapse = within(header).getByRole('button', { name: 'Свернуть на один уровень' });
    expect(expand.querySelector('[data-testid="AddIcon"]')).not.toBeNull();
    expect(collapse.querySelector('[data-testid="RemoveIcon"]')).not.toBeNull();
    for (const button of [expand, collapse]) {
      const style = getComputedStyle(button);
      expect(style.width).toBe('24px');
      expect(style.height).toBe('24px');
      expect(style.borderTopWidth).toBe('1px');
    }
    expect(getComputedStyle(collapse).borderTopColor).toBe(rgb(theme.palette.text.primary));
    // The first state is the whole tree: nothing more to open.
    expect(expand).toHaveAttribute('aria-disabled', 'true');
    expect(collapse).not.toHaveAttribute('aria-disabled');
  });
});

describe('US-1 Scenario: The title cell carries the chevron, the icon and the plain title', () => {
  it('MSK-EPIC-1 shows its chevron, the crown and «Вход и регистрация» as plain text that opens its form; the tooltip carries the identifier; no ID is drawn', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();
    const row = rowOf(table, 'MSK-EPIC-1');

    const link = titleLink(row);
    expect(link).toHaveTextContent('Вход и регистрация');
    expect(link).toHaveAttribute('title', 'MSK-EPIC-1: Вход и регистрация');
    expect(link).toHaveAttribute('href', mskForm('MSK-EPIC-1'));
    // Not blue and not underlined at rest.
    expect(getComputedStyle(link).color).toBe(rgb(theme.palette.text.primary));
    expect(link).toHaveClass('MuiLink-underlineHover');
    expect(link).toHaveAccessibleDescription('MSK-EPIC-1. Уровень вложенности: 1.');
    expect(expandControl('MSK-EPIC-1')).toBeInTheDocument();
    const cells = within(row).getAllByRole('cell');
    expect(cells[CELL.title]?.querySelector('[data-testid="CrownIcon"]')).not.toBeNull();
    // No identifier is drawn: no cell shows it, and the link's own text is the title alone —
    // the identifier stays in the tooltip and in the hidden description.
    expect(link.textContent).toBe('Вход и регистрация');
    expect(
      cells.filter((cell, index) => index !== CELL.title && cell.textContent?.includes('MSK-')),
    ).toEqual([]);
    // «Тип» is the type's name as plain text; «Статус» the dot and «Новая».
    expect(cellText(row, CELL.type)).toBe('Эпик');
    expect(cellOf(row, CELL.type).querySelector('svg')).toBeNull();
    expect(cellText(row, CELL.state)).toBe('Новая');
    expect(cellOf(row, CELL.state).querySelector(`.${CATEGORY_MARKER_CLASS}`)).not.toBeNull();

    // A plain click opens the card as a dialog (BacklogPage.card.test.tsx); the address stays
    // the item's own page, for a new tab (feat-029 BR-C-1).
  });

  it('describes a nested row’s depth for assistive technology: «<ID>. Уровень вложенности: <n>.»', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();

    expect(titleLink(rowOf(table, 'MSK-US-2'))).toHaveAccessibleDescription(
      'MSK-US-2. Уровень вложенности: 3.',
    );
    expect(titleLink(rowOf(table, 'MSK-TASK-3'))).toHaveAccessibleDescription(
      'MSK-TASK-3. Уровень вложенности: 4.',
    );
  });
});

describe('US-1 Scenario Outline: Each type has its icon in its colour', () => {
  it.each([
    ['MSK-EPIC-1', 'Эпик', 'CrownIcon', 'epic'],
    ['MSK-FEAT-8', 'Фича', 'EmojiEventsIcon', 'feature'],
    ['MSK-US-2', 'Пользовательская история', 'StoryIcon', 'story'],
    ['MSK-BUG-5', 'Баг', 'BugReportIcon', 'bug'],
    ['MSK-TASK-3', 'Задача', 'AssignmentTurnedInIcon', 'task'],
  ])(
    'the row of %s (%s) shows %s before its title in the %s colour',
    async (identifier, type, icon, token) => {
      serveBoards(fullTreeDataset());
      renderBoards(backlogAddress('epics'));
      const table = await findBacklogTable();
      const row = rowOf(table, identifier);

      expect(cellText(row, CELL.type)).toBe(type);
      const mark = cellOf(row, CELL.title).querySelector<SVGElement>(`[data-testid="${icon}"]`);
      expect(mark).not.toBeNull();
      expect(mark).toHaveAttribute('aria-hidden', 'true');
      expect(getComputedStyle(mark as Element).color).toBe(
        rgb(theme.workTracking.typeAppearance(token).colour),
      );
      // The icon stands before the title.
      expect(
        (mark as Element).compareDocumentPosition(titleLink(row)) &
          Node.DOCUMENT_POSITION_FOLLOWING,
      ).toBeTruthy();
    },
  );

  it('draws the icon at 16 px', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();

    const mark = cellOf(rowOf(table, 'MSK-EPIC-1'), CELL.title).querySelector(
      '[data-testid="CrownIcon"]',
    ) as Element;
    expect(getComputedStyle(mark).fontSize).toBe('16px');
  });
});

describe('US-1 Scenario: The state is a dot by its category and the name', () => {
  it('«Новые» shows a grey dot, «В работе» a blue one, «Завершённые» a green one, each with the state’s name', async () => {
    const data = fullTreeDataset();
    mskRecord(data, 2).stateId = stateId('US', 'Активна');
    mskRecord(data, 3).stateId = stateId('TASK', 'Закрыта');
    serveBoards(data);
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();

    const dot = (identifier: string) =>
      cellOf(rowOf(table, identifier), CELL.state).querySelector(
        `.${CATEGORY_MARKER_CLASS}`,
      ) as HTMLElement;
    expect(cellText(rowOf(table, 'MSK-EPIC-1'), CELL.state)).toBe('Новая');
    expect(getComputedStyle(dot('MSK-EPIC-1')).backgroundColor).toBe(
      rgb(theme.workTracking.categoryColour('Proposed')),
    );
    expect(cellText(rowOf(table, 'MSK-US-2'), CELL.state)).toBe('Активна');
    expect(getComputedStyle(dot('MSK-US-2')).backgroundColor).toBe(
      rgb(theme.workTracking.categoryColour('InProgress')),
    );
    expect(cellText(rowOf(table, 'MSK-TASK-3'), CELL.state)).toBe('Закрыта');
    expect(getComputedStyle(dot('MSK-TASK-3')).backgroundColor).toBe(
      rgb(theme.workTracking.categoryColour('Completed')),
    );
    // The dot is 8 px and stands 6 px from the name.
    expect(getComputedStyle(dot('MSK-EPIC-1')).width).toBe('8px');
    expect(getComputedStyle(dot('MSK-EPIC-1').parentElement as Element).gap).toBe('6px');
  });

  it('«Разрешена» shows the Resolved colour on a story, an epic and a bug alike — one name, one meaning (bug-004 D-3)', async () => {
    const data = fullTreeDataset();
    mskRecord(data, 1).stateId = stateId('EPIC', 'Разрешена');
    mskRecord(data, 2).stateId = stateId('US', 'Разрешена');
    mskRecord(data, 4).stateId = stateId('BUG', 'Разрешена');
    serveBoards(data);
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();

    const dot = (identifier: string) =>
      cellOf(rowOf(table, identifier), CELL.state).querySelector(
        `.${CATEGORY_MARKER_CLASS}`,
      ) as HTMLElement;
    const resolved = rgb(theme.workTracking.categoryColour('Resolved'));
    for (const identifier of ['MSK-EPIC-1', 'MSK-US-2', 'MSK-BUG-4']) {
      expect(cellText(rowOf(table, identifier), CELL.state)).toBe('Разрешена');
      expect(getComputedStyle(dot(identifier)).backgroundColor).toBe(resolved);
    }
    expect(resolved).not.toBe(rgb(theme.workTracking.categoryColour('InProgress')));
  });
});

describe('US-1 Scenario: Only top-level rows are numbered', () => {
  it('MSK-EPIC-1 reads «1» and MSK-EPIC-20 «2»; the rows beneath MSK-EPIC-1 show no «Порядок»', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();

    expect(cellText(rowOf(table, 'MSK-EPIC-1'), CELL.order)).toBe('1');
    expect(cellText(rowOf(table, 'MSK-EPIC-20'), CELL.order)).toBe('2');
    expect(orders(table)).toEqual(['1', '', '', '', '', '', '', '', '', '2']);
    for (const nested of ['MSK-FEAT-8', 'MSK-FEAT-6', 'MSK-US-22']) {
      expect(cellText(rowOf(table, nested), CELL.order)).toBe('');
    }
  });
});

describe('US-1 Scenario Outline: The planning values, and blanks where a type has no field', () => {
  it.each([
    ['MSK-EPIC-1', '20', '300', 'Бизнес'],
    ['MSK-EPIC-20', '', '', 'Архитектура'],
    ['MSK-FEAT-8', '2,5', '', 'Бизнес'],
    ['MSK-US-2', '', '', 'Бизнес'],
    ['MSK-TASK-3', '', '', ''],
  ])(
    '%s reads «Трудоёмкость» «%s», «Бизнес-ценность» «%s», «Вид ценности» «%s»',
    async (identifier, effort, value, area) => {
      serveBoards(fullTreeDataset());
      renderBoards(backlogAddress('epics'));
      const table = await findBacklogTable();
      const row = rowOf(table, identifier);

      expect(cellText(row, CELL.effort)).toBe(effort);
      expect(cellText(row, CELL.businessValue)).toBe(value);
      expect(cellText(row, CELL.valueArea)).toBe(area);
      // Left-aligned, as the reference draws the numbers (D-10; D-7 6's «right-aligned» was a misreading).
      expect(getComputedStyle(cellOf(row, CELL.effort)).textAlign).toBe('left');
      expect(getComputedStyle(cellOf(row, CELL.businessValue)).textAlign).toBe('left');
    },
  );

  it('a value changed elsewhere shows at the next read: «Трудоёмкость» 13 on MSK-EPIC-1', async () => {
    const api = serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();
    expect(cellText(rowOf(table, 'MSK-EPIC-1'), CELL.effort)).toBe('20');

    mskRecord(api.data, 1).fields.effort = 13;
    await act(async () => {
      focusManager.setFocused(false);
    });
    await act(async () => {
      focusManager.setFocused(true);
    });

    await waitFor(() => expect(cellText(rowOf(table, 'MSK-EPIC-1'), CELL.effort)).toBe('13'));
  });
});

describe('US-1 Scenario: Rows are compact, white and quiet', () => {
  it('every row is 36 px high on a white ground with no line between rows, in the card’s soft glow; the header stays', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();

    for (const row of rowsOf(table)) {
      expect(getComputedStyle(row).height).toBe('36px');
      expect(getComputedStyle(row).backgroundColor).toBe(rgb(theme.palette.background.paper));
      for (const cell of Array.from(row.querySelectorAll('td'))) {
        expect(getComputedStyle(cell).borderBottomStyle).not.toBe('solid');
      }
    }
    const card = table.parentElement as HTMLElement;
    expect(getComputedStyle(card).boxShadow.replace(/\s+/g, '')).toBe(
      theme.cardShadow.replace(/\s+/g, ''),
    );
    // The header is the table's sticky one: it stays while the rows scroll in the card.
    expect(within(table).getAllByRole('columnheader')[0]).toHaveClass('MuiTableCell-stickyHeader');
    expect(getComputedStyle(card).overflowX).toBe('auto');
  });

  it('a row’s grip, «+» and «⋮» are transparent at rest and shown on selection, on focus within the row and while their menu is open; «+» and «⋮» stay in the Tab order, the grip is no stop (F-5)', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();
    const row = rowOf(table, 'MSK-EPIC-1');
    // The row's «+» is there once the process has named the type it adds.
    await addControl('MSK-EPIC-1');
    const controls = () =>
      Array.from(row.querySelectorAll<HTMLElement>('.BacklogRow-control')).map(
        (control) => getComputedStyle(control).opacity,
      );

    expect(controls()).toEqual(['0', '0', '0']);
    expect(row.querySelector('[data-grip]')).toHaveAttribute('aria-hidden', 'true');
    expect(row.querySelector('[data-grip]')).toHaveAttribute('tabindex', '-1');
    // «+» and «⋮» are real buttons left in the Tab order: transparent, not removed.
    for (const button of row.querySelectorAll<HTMLElement>('button.BacklogRow-control')) {
      expect(button.tabIndex).toBe(0);
    }
    expect(row.querySelectorAll('button.BacklogRow-control')).toHaveLength(2);

    // A click selects the row: light blue, framed, its controls shown, its icon dark blue.
    press(cellOf(row, CELL.type));
    expect(row).toHaveAttribute('data-selected', 'true');
    expect(controls()).toEqual(['1', '1', '1']);
    expect(getComputedStyle(row).backgroundColor).toBe(rgb(theme.palette.shell.rowSelected));
    expect(getComputedStyle(cellOf(row, CELL.hierarchy)).boxShadow).toContain(
      `inset 2px 0 0 ${theme.palette.primary.main}`,
    );
    expect(
      getComputedStyle(
        cellOf(row, CELL.title).querySelector('[data-testid="CrownIcon"]') as Element,
      ).color,
    ).toBe(rgb(theme.palette.primary.dark));
    // One row at a time.
    press(cellOf(rowOf(table, 'MSK-EPIC-20'), CELL.type));
    expect(row).not.toHaveAttribute('data-selected');
    expect(rowOf(table, 'MSK-EPIC-20')).toHaveAttribute('data-selected', 'true');
    expect(controls()).toEqual(['0', '0', '0']);

    // Focus inside the row shows its controls too (jsdom has no `:focus-within` to match, so the rule is read).
    const rules = Array.from(document.styleSheets).flatMap((sheet) =>
      Array.from(sheet.cssRules).map((rule) => rule.cssText),
    );
    expect(
      rules.some(
        (rule) => rule.includes(':focus-within .BacklogRow-control') && rule.includes('opacity: 1'),
      ),
    ).toBe(true);
    expect(
      rules.some(
        (rule) => rule.includes(':hover .BacklogRow-control') && rule.includes('opacity: 1'),
      ),
    ).toBe(true);
  });
});

describe('US-1 Scenario: A narrow window scrolls the table, not the page', () => {
  it('keeps its width in the card, which scrolls sideways on its own', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();

    const card = table.parentElement as HTMLElement;
    expect(getComputedStyle(card).overflowX).not.toBe('visible');
    expect(getComputedStyle(table).minWidth).toBe('1180px');
    expect(getComputedStyle(card).maxHeight).toContain('204px');
  });

  it('holds the rows’ hidden descriptions in its scroll box: the card is their containing block, so the page does not scroll for the table’s height (F-3)', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();
    const card = table.parentElement as HTMLElement;

    const describedBy = titleLink(rowOf(table, 'MSK-FEAT-6')).getAttribute('aria-describedby');
    const description = document.getElementById(describedBy as string) as HTMLElement;
    expect(description).toHaveTextContent('MSK-FEAT-6. Уровень вложенности: 2.');
    // Absolutely positioned and clipped to nothing…
    expect(getComputedStyle(description).position).toBe('absolute');
    // …and nothing between it and the card is positioned, so the card must be.
    for (
      let ancestor: HTMLElement | null = description.parentElement;
      ancestor !== null && ancestor !== card;
      ancestor = ancestor.parentElement
    ) {
      expect(getComputedStyle(ancestor).position).not.toMatch(/relative|absolute|fixed|sticky/);
    }
    expect(getComputedStyle(card).position).toBe('relative');
  });

  it('keeps a row focused or scrolled to clear of the sticky header: the card’s scroll padding is the header’s height (F-10)', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();
    const card = table.parentElement as HTMLElement;

    expect(theme.density.tabHeight).toBe(40);
    expect(getComputedStyle(card).getPropertyValue('scroll-padding-top')).toBe(
      `${theme.density.tabHeight}px`,
    );
  });
});
