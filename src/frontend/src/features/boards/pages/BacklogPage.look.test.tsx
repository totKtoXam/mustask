import { afterEach, describe, expect, it } from 'vitest';
import { screen, within } from '@testing-library/react';
import { focusManager } from '@tanstack/react-query';
import { theme } from '../../../app/theme';
import { CATEGORY_MARKER_CLASS } from '../../../shared/ui/WorkTrackingMarks';
import { gripSx } from '../components/backlogRowStyle';
import { serveBoards } from '../testApi';
import {
  CELL,
  addControl,
  cellOf,
  collapseOneLevel,
  expandControl,
  expandOneLevel,
  findBacklogTable,
  grip,
  press,
  rowOf,
} from '../testBacklog';
import { cardOf, findBoard } from '../testBoard';
import { fullTreeDataset, mskRecord, stateId, storiesDataset } from '../testFixtures';
import { backlogAddress, boardAddress, renderBoards } from '../testRender';

// feat-027 review V-1..V-8 and D-10: the backlog against the reference
// screenshot (the look the human compares side by side; design § 4). Each test
// pins one measurement the review took, in the form jsdom can read: a computed
// style, a CSS rule, an SVG attribute.

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

const px = (value: string) => parseFloat(value);

/** The `::after` rules in the document that apply to `element`, as jsdom parsed what emotion wrote. */
function afterRules(element: Element): CSSStyleRule[] {
  return Array.from(document.styleSheets)
    .flatMap((sheet) => Array.from(sheet.cssRules))
    .filter(
      (rule): rule is CSSStyleRule =>
        rule instanceof CSSStyleRule &&
        rule.selectorText.endsWith('::after') &&
        element.matches(rule.selectorText.slice(0, -'::after'.length)),
    );
}

describe('V-1 Scenario: The header separators are thin lines, not a band', () => {
  it('draws each of the seven separators 1 px wide and 20 px tall — a length, because `sx` reads a bare `width: 1` as 100 % and drew one grey bar across the headers', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();

    const headers = within(table).getAllByRole('columnheader');
    const separated = headers.filter((header) =>
      header.classList.contains('BacklogHead-separated'),
    );
    expect(separated.map((header) => header.textContent)).toEqual([
      'Тип',
      'Заголовок',
      'Статус',
      'Команда',
      'Проект',
      'Трудоёмкость',
      'Бизнес-ценность',
    ]);
    for (const header of separated) {
      const rules = afterRules(header);
      expect(rules, `${header.textContent}: a separator rule`).not.toHaveLength(0);
      const style = (rules[rules.length - 1] as CSSStyleRule).style;
      expect(style.getPropertyValue('width'), `${header.textContent}: width`).toBe('1px');
      expect(style.getPropertyValue('height'), `${header.textContent}: height`).toBe(
        `${theme.layout.table.separatorHeight}px`,
      );
      expect(style.getPropertyValue('background-color')).toBe(theme.palette.divider);
    }
    for (const header of headers.filter((candidate) => !separated.includes(candidate))) {
      expect(afterRules(header), `${header.textContent}: no separator`).toHaveLength(0);
    }
  });

  it('starts the «Заголовок» label at the depth-0 icon’s left edge, a few px right of it, as the reference’s label stands over its icons', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();

    const title = cellOf(rowOf(table, 'MSK-EPIC-1'), CELL.title);
    const content = title.firstElementChild as HTMLElement;
    const icon = title.querySelector('[data-testid="CrownIcon"]') as Element;
    const edge =
      px(getComputedStyle(title).paddingLeft) +
      px(getComputedStyle(content).paddingLeft) +
      px(getComputedStyle(expandControl('MSK-EPIC-1')).width) +
      px(getComputedStyle(icon.parentElement as Element).marginLeft);
    const label = px(
      getComputedStyle(within(table).getByRole('columnheader', { name: 'Заголовок' })).paddingLeft,
    );

    expect(edge).toBe(32);
    expect(label - edge).toBeGreaterThanOrEqual(2);
    expect(label - edge).toBeLessThanOrEqual(6);
  });
});

describe('V-3 Scenario: The idle «+» and «−» read as a pair', () => {
  it('draws an idle button’s border and glyph in the secondary text colour, not the near-invisible `action.disabled`, and keeps it `aria-disabled`', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    await findBacklogTable();

    // The first state is the whole tree: nothing more to open, so «+» is idle and «−» is not.
    const expand = expandOneLevel();
    const collapse = collapseOneLevel();
    expect(expand).toHaveAttribute('aria-disabled', 'true');
    expect(collapse).not.toHaveAttribute('aria-disabled');
    expect(getComputedStyle(expand).borderTopColor).toBe(rgb(theme.palette.text.secondary));
    expect(getComputedStyle(expand).color).toBe(rgb(theme.palette.text.secondary));
    expect(getComputedStyle(collapse).borderTopColor).toBe(rgb(theme.palette.text.primary));
    expect(getComputedStyle(expand).borderTopColor).not.toBe(rgb(theme.palette.action.disabled));
    // The pair is still told apart by `aria-disabled`; it only reads less broken.
    expect(getComputedStyle(expand).borderTopWidth).toBe('1px');
  });
});

describe('V-4 Scenario: The grip is the reference’s four dots, not a dotted line the row’s height', () => {
  it('draws a 1 px column of four 2 px dots 6 px apart — a mark 20 px tall, centred on the row, at the 8th pixel column — on a strip as high as the cell', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();

    const style = gripSx(theme, false);
    const dot = theme.palette.action.disabled;
    expect(style.backgroundImage).toBe(
      `repeating-linear-gradient(to bottom, ${dot} 0, ${dot} 2px, transparent 2px, transparent 6px)`,
    );
    expect(style.backgroundSize).toBe('1px 20px');
    expect(style.backgroundPosition).toBe('7px center');
    expect(style.backgroundRepeat).toBe('no-repeat');
    // The mark holds exactly four dots (2 px each, 6 px apart: 4 × 2 + 3 × 4 = 20 px).
    expect(Math.floor((theme.layout.table.gripMarkHeight + 4) / 6)).toBe(4);
    // The hit strip is not shrunk with it: the cell's full height, 16 px wide.
    expect(style.top).toBe(0);
    expect(style.bottom).toBe(0);
    expect(style.width).toBe(theme.layout.table.gripHitWidth);

    const drawn = getComputedStyle(grip(table, 'MSK-EPIC-1'));
    expect(drawn.backgroundSize).toBe('1px 20px');
    expect(drawn.backgroundPosition).toBe('7px center');
    expect(drawn.width).toBe('16px');
  });
});

describe('V-5 Scenario: The chevrons and the row «+» are hairlines', () => {
  /** The one path of an icon, whatever Material or this slice drew. */
  const pathOf = (element: Element) => element.querySelector('svg path') as SVGPathElement;

  it('draws the tree chevron, the team chevron and the row «+» as one stroked path 1.5 units thick with no fill — not Material’s filled ExpandMore, ChevronRight and Add', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    await findBacklogTable();

    const drawn = {
      'an expanded row’s chevron': expandControl('MSK-EPIC-1'),
      'the row «+»': await addControl('MSK-EPIC-1'),
      'the team chevron': screen.getByRole('button', { name: 'Сменить команду' }),
    };
    for (const [what, control] of Object.entries(drawn)) {
      const path = pathOf(control);
      expect(path.getAttribute('fill'), `${what}: no fill`).toBe('none');
      expect(path.getAttribute('stroke'), `${what}: stroked`).toBe('currentColor');
      expect(path.getAttribute('stroke-width'), `${what}: 1.5 units`).toBe('1.5');
      for (const material of ['ExpandMoreIcon', 'ChevronRightIcon', 'AddIcon']) {
        expect(control.querySelector(`[data-testid="${material}"]`), what).toBeNull();
      }
    }
    // A collapsed row points right with the same stroke.
    expect(
      drawn['an expanded row’s chevron'].querySelector('[data-testid="ThinChevronDownIcon"]'),
    ).not.toBeNull();
  });

  it('keeps the glyph sizes of the design — a 20 px chevron in its 24 px box, a 16 px «+» — so only the stroke changed', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    await findBacklogTable();

    const chevron = expandControl('MSK-EPIC-1').querySelector('svg') as Element;
    expect(getComputedStyle(chevron).fontSize).toBe(`${theme.layout.iconAction.chevron.glyph}px`);
    const add = (await addControl('MSK-EPIC-1')).querySelector('svg') as Element;
    expect(getComputedStyle(add).fontSize).toBe(`${theme.layout.iconAction.row.glyph}px`);
  });
});

describe('V-6 Scenario: The team chevron stands close to the name', () => {
  it('pulls the chevron’s box in by 4 px, so its ink — about 6 px inside the box — starts about 12 px after the name, as the reference’s does, not 19', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    await findBacklogTable();

    const button = screen.getByRole('button', { name: 'Сменить команду' });
    expect(getComputedStyle(button).marginLeft).toBe(theme.spacing(-0.5));
    expect(px(getComputedStyle(button).marginLeft)).toBe(-4);
    // 10 px of the title group's gap, 4 px pulled in, about 6 px of the glyph's own inset (2 px of
    // box and 4.5 of the path's 24 units, at 20 px): the ink starts 12 px after the name.
    const gap = px(getComputedStyle(button.parentElement as Element).gap);
    const inset = (24 - theme.layout.iconAction.chevron.glyph) / 2 + (4.5 * 20) / 24;
    expect(Math.round(gap + px(getComputedStyle(button).marginLeft) + inset)).toBe(12);
  });
});

describe('V-7 Scenario: «Трудоёмкость» and «Бизнес-ценность» read from the left', () => {
  it('left-aligns the two headers and the numbers beneath them', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();

    for (const label of ['Трудоёмкость', 'Бизнес-ценность']) {
      expect(
        getComputedStyle(within(table).getByRole('columnheader', { name: label })).textAlign,
      ).toBe('left');
    }
    const row = rowOf(table, 'MSK-EPIC-1');
    expect(getComputedStyle(cellOf(row, CELL.effort)).textAlign).toBe('left');
    expect(getComputedStyle(cellOf(row, CELL.businessValue)).textAlign).toBe('left');
  });
});

describe('V-8 Scenario: The user story is two solid pages', () => {
  it('draws the story row with the StoryIcon, not Material’s MenuBook', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();

    const title = cellOf(rowOf(table, 'MSK-US-2'), CELL.title);
    expect(title.querySelector('[data-testid="StoryIcon"]')).not.toBeNull();
    expect(title.querySelector('[data-testid="MenuBookIcon"]')).toBeNull();
  });
});

describe('D-10 Scenario: The reference’s colours, on the backlog and on the board', () => {
  it.each([
    ['MSK-EPIC-1', 'CrownIcon', 'rgb(224, 108, 0)'],
    ['MSK-FEAT-8', 'EmojiEventsIcon', 'rgb(119, 59, 147)'],
    ['MSK-US-2', 'StoryIcon', 'rgb(0, 152, 199)'],
    ['MSK-BUG-5', 'BugReportIcon', 'rgb(204, 41, 61)'],
    ['MSK-TASK-3', 'AssignmentTurnedInIcon', 'rgb(164, 136, 10)'],
  ])('draws the icon of %s (%s) in %s', async (identifier, icon, colour) => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();

    const mark = cellOf(rowOf(table, identifier), CELL.title).querySelector(
      `[data-testid="${icon}"]`,
    ) as Element;
    expect(getComputedStyle(mark).color).toBe(colour);
  });

  it('draws the «Новые» dot #B2B2B2 and the «Завершённые» dot #339933, and keeps «В работе» blue', async () => {
    const data = fullTreeDataset();
    mskRecord(data, 2).stateId = stateId('US', 'Активна');
    mskRecord(data, 3).stateId = stateId('TASK', 'Закрыта');
    serveBoards(data);
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();

    const dot = (identifier: string) =>
      getComputedStyle(
        cellOf(rowOf(table, identifier), CELL.state).querySelector(
          `.${CATEGORY_MARKER_CLASS}`,
        ) as Element,
      ).backgroundColor;
    expect(dot('MSK-EPIC-1')).toBe('rgb(178, 178, 178)');
    expect(dot('MSK-TASK-3')).toBe('rgb(51, 153, 51)');
    expect(dot('MSK-US-2')).toBe(rgb('#005A9E'));
  });

  it('draws the row «+» in the pale grey #8A8886, not orange, and hands the accent #0078D4 to the primary button, the tab’s underline and the selected row’s frame', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();

    expect(getComputedStyle(await addControl('MSK-EPIC-1')).color).toBe('rgb(138, 136, 134)');
    const primary = screen.getByRole('button', { name: 'Новый рабочий элемент' });
    expect(getComputedStyle(primary).getPropertyValue('--variant-containedBg')).toBe('#0078D4');
    expect(
      getComputedStyle(document.querySelector('.MuiTabs-indicator') as Element).backgroundColor,
    ).toBe('rgb(0, 120, 212)');
    const row = rowOf(table, 'MSK-EPIC-20');
    press(cellOf(row, CELL.type));
    expect(getComputedStyle(cellOf(row, CELL.hierarchy)).boxShadow).toContain(
      'inset 2px 0 0 #0078D4',
    );
  });

  it('carries the type colours to the board’s cards through the same lookup: MSK-BUG-5’s bug is #CC293D', async () => {
    serveBoards(storiesDataset());
    renderBoards(boardAddress('stories'));
    const board = await findBoard();

    const mark = cardOf(board, 'MSK-BUG-5').querySelector(
      '[data-testid="BugReportIcon"]',
    ) as Element;
    expect(getComputedStyle(mark).color).toBe('rgb(204, 41, 61)');
  });
});
