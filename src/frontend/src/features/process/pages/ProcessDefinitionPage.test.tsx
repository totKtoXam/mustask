import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { Route, Routes } from 'react-router-dom';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { theme } from '../../../app/theme';
import { server } from '../../../test/mocks/server';
import { renderWithProviders } from '../../../test/renderWithProviders';
import type { ProcessDefinitionResponse } from '../api/processApi';
import { CATEGORY_MARKER_CLASS } from '../components/CategoryCell';
import { PROCESS_DEFINITION_ROUTE, processDefinitionPath } from '../paths';
import {
  DEMONSTRATION_ORGANIZATION_ID,
  PROCESS_DEFINITION_URL,
  TYPE_NAMES_IN_ORDER,
  cloneProcessDefinition,
  sampleProcessDefinition,
} from '../testFixtures';
import { ProcessDefinitionPage } from './ProcessDefinitionPage';

/** `#A4262C` → `rgb(164, 38, 44)`, the form jsdom's computed style reports. */
function rgb(hex: string): string {
  const digits = hex.replace('#', '');
  const [red, green, blue] = [0, 2, 4].map((offset) =>
    parseInt(digits.slice(offset, offset + 2), 16),
  );
  return `rgb(${red}, ${green}, ${blue})`;
}

/** The icon the theme draws per token (mod-015 design § 2.3), by MUI's own test id. */
const TOKEN_ICONS: Record<string, string> = {
  epic: 'LandscapeIcon',
  feature: 'EmojiEventsIcon',
  story: 'MenuBookIcon',
  bug: 'BugReportIcon',
  task: 'AssignmentIcon',
};

const CATEGORY_CODES = ['Proposed', 'InProgress', 'Resolved', 'Completed', 'Removed'];

const requests: { method: string; url: string }[] = [];
const onRequest = ({ request }: { request: Request }) => {
  if (!request.url.includes('/api/session/')) {
    requests.push({ method: request.method, url: request.url });
  }
};

beforeEach(() => {
  requests.length = 0;
  server.events.on('request:start', onRequest);
});

afterEach(() => {
  server.events.removeListener('request:start', onRequest);
});

function serve(definition: ProcessDefinitionResponse = sampleProcessDefinition) {
  server.use(http.get(PROCESS_DEFINITION_URL, () => HttpResponse.json(definition)));
}

/**
 * The page at its route, outside the shell: the shared access check fails
 * closed here, so content appearing is itself the proof that the page makes
 * no permission check (design DD-6).
 */
function renderPage(organizationId = DEMONSTRATION_ORGANIZATION_ID) {
  return renderWithProviders(
    <Routes>
      <Route path={PROCESS_DEFINITION_ROUTE} element={<ProcessDefinitionPage />} />
    </Routes>,
    { initialEntries: [processDefinitionPath(organizationId)] },
  );
}

interface RowFacts {
  order: string;
  name: string;
  initial: boolean;
  label: string;
  marker: HTMLElement;
}

function rowsOf(table: HTMLElement): RowFacts[] {
  return within(table)
    .getAllByRole('row')
    .slice(1)
    .map((row) => {
      const header = within(row).getByRole('rowheader');
      const [order, label] = within(row).getAllByRole('cell');
      return {
        order: order!.textContent ?? '',
        name: header.firstChild?.textContent ?? '',
        initial: within(header).queryByText('Начальный') !== null,
        label: label!.textContent ?? '',
        marker: row.querySelector(`.${CATEGORY_MARKER_CLASS}`) as HTMLElement,
      };
    });
}

async function cards(): Promise<HTMLElement[]> {
  await screen.findByRole('region', { name: 'Эпик' });
  return screen.getAllByRole('region');
}

function tableOf(typeName: string): HTMLElement {
  return screen.getByRole('table', { name: `Статусы типа «${typeName}»` });
}

describe('mod-001 US-2 Scenario: A seeded process is shown in full', () => {
  it('shows the five types as sections named by their level-2 headings, in the order received', async () => {
    serve();
    renderPage();

    const sections = await cards();

    expect(sections).toHaveLength(5);
    sections.forEach((section) => expect(section.tagName).toBe('SECTION'));
    expect(
      sections.map((section) => within(section).getByRole('heading', { level: 2 }).textContent),
    ).toEqual(['Эпик', 'Фича', 'Пользовательская история', 'Баг', 'Задача']);
    expect(TYPE_NAMES_IN_ORDER).toEqual([
      'Эпик',
      'Фича',
      'Пользовательская история',
      'Баг',
      'Задача',
    ]);
  });

  it('gives each type its key, its backlog level and its states in order, the initial one marked', async () => {
    serve();
    renderPage();
    await cards();

    for (const type of sampleProcessDefinition.workItemTypes) {
      const section = screen.getByRole('region', { name: type.name });
      const facts = section.querySelector('dl') as HTMLElement;
      expect(facts).toHaveTextContent(`Ключ${type.key}`);
      expect(facts).toHaveTextContent(`Уровень бэклога${type.backlogLevel.name}`);

      const rows = rowsOf(
        within(section).getByRole('table', { name: `Статусы типа «${type.name}»` }),
      );
      expect(rows.map((row) => row.name)).toEqual(type.states.map((state) => state.name));
      expect(rows.map((row) => row.order)).toEqual(
        type.states.map((_, index) => String(index + 1)),
      );
      expect(rows.map((row) => row.initial)).toEqual(type.states.map((_, index) => index === 0));
      expect(rows.map((row) => row.label)).toEqual(
        type.states.map((state) => state.category.label),
      );
    }
    expect(screen.getAllByText('Начальный')).toHaveLength(5);
  });

  it('draws each type’s token through the theme — its icon in its colour — and never prints the token', async () => {
    serve();
    renderPage();
    await cards();

    for (const type of sampleProcessDefinition.workItemTypes) {
      const heading = screen.getByRole('heading', { level: 2, name: type.name });
      const icon = within(heading).getByTestId(TOKEN_ICONS[type.appearance]!);
      expect(icon).toHaveAttribute('aria-hidden', 'true');
      expect(getComputedStyle(icon).color).toBe(
        rgb(theme.workTracking.typeAppearance(type.appearance).colour),
      );
    }
    for (const token of Object.keys(TOKEN_ICONS)) {
      expect(screen.queryByText(token)).not.toBeInTheDocument();
    }
  });

  it('puts each state’s category colour beside its label, «Удалённые» as a ring', async () => {
    serve();
    renderPage();
    await cards();

    for (const type of sampleProcessDefinition.workItemTypes) {
      const rows = rowsOf(tableOf(type.name));
      rows.forEach((row, index) => {
        const code = type.states[index]!.category.code;
        const colour = rgb(theme.workTracking.categoryColour(code));
        expect(row.marker).toHaveAttribute('aria-hidden', 'true');
        if (code === 'Removed') {
          expect(getComputedStyle(row.marker).borderTopColor).toBe(colour);
          expect(getComputedStyle(row.marker).backgroundColor).not.toBe(colour);
        } else {
          expect(getComputedStyle(row.marker).backgroundColor).toBe(colour);
        }
      });
    }
  });

  it('renders the title and the two description sentences above the cards', async () => {
    serve();
    renderPage();
    await cards();

    expect(screen.getByRole('heading', { level: 1, name: 'Процесс' })).toBeInTheDocument();
    expect(
      screen.getByText(
        'Типы рабочих элементов организации, их статусы и уровни бэклога. Процесс задан системой и доступен только для просмотра.',
      ),
    ).toBeInTheDocument();
    expect(
      screen.getByText(
        'Категория показывает, к какой стадии работы относится статус. У разных типов один и тот же статус может относиться к разным категориям.',
      ),
    ).toBeInTheDocument();
  });
});

describe('mod-001 US-2 Scenario: Every signed-in user sees the same definition (page half)', () => {
  it('renders the full content outside the shell, where the shared access check refuses every code', async () => {
    serve();
    renderPage();

    expect(await cards()).toHaveLength(5);
    expect(screen.queryByText('Доступ ограничен')).not.toBeInTheDocument();
  });
});

describe('mod-001 US-2 Scenario: Categories are shown by their Russian labels', () => {
  it('shows «Задача»’s states with «Новые», «В работе», «Завершённые» and «Удалённые»', async () => {
    serve();
    renderPage();
    await cards();

    expect(rowsOf(tableOf('Задача')).map((row) => [row.name, row.label])).toEqual([
      ['Новая', 'Новые'],
      ['Активна', 'В работе'],
      ['Закрыта', 'Завершённые'],
      ['Удалена', 'Удалённые'],
    ]);
  });

  it('shows no category by its code anywhere on the screen', async () => {
    serve();
    const { container } = renderPage();
    await cards();

    for (const code of CATEGORY_CODES) {
      expect(container).not.toHaveTextContent(code);
    }
  });

  it('holds no map of its own: a label the server words differently is shown as sent', async () => {
    const definition = cloneProcessDefinition();
    const task = definition.workItemTypes.find((type) => type.key === 'TASK')!;
    task.states[0]!.category.label = 'Предложенные';
    serve(definition);
    renderPage();
    await cards();

    expect(rowsOf(tableOf('Задача'))[0]!.label).toBe('Предложенные');
  });
});

describe('mod-001 US-2 Scenario: One state label belongs to two categories', () => {
  it('reads «Разрешена» as «В работе» on «Пользовательская история» and «Решённые» on «Баг», in two colours', async () => {
    serve();
    renderPage();
    await cards();

    const story = rowsOf(tableOf('Пользовательская история')).find(
      (row) => row.name === 'Разрешена',
    )!;
    const bug = rowsOf(tableOf('Баг')).find((row) => row.name === 'Разрешена')!;

    expect(story.label).toBe('В работе');
    expect(bug.label).toBe('Решённые');
    expect(getComputedStyle(story.marker).backgroundColor).not.toBe(
      getComputedStyle(bug.marker).backgroundColor,
    );
  });
});

describe('mod-001 US-2 Scenario: Nothing on the screen changes the process', () => {
  it('holds no button, link, field, checkbox, combobox or menu, and the chips never take focus', async () => {
    serve();
    const { container } = renderPage();
    await cards();

    for (const role of ['button', 'link', 'textbox', 'checkbox', 'combobox', 'menu', 'menuitem']) {
      expect(screen.queryAllByRole(role)).toEqual([]);
    }
    for (const chip of Array.from(container.querySelectorAll('.MuiChip-root'))) {
      expect(chip).not.toHaveAttribute('tabindex');
      expect(chip).not.toHaveAttribute('role', 'button');
    }
  });

  it('gives Tab nothing to reach inside the content after the title', async () => {
    const user = userEvent.setup();
    serve();
    const { container } = renderPage();
    await cards();

    screen.getByRole('heading', { level: 1, name: 'Процесс' }).focus();
    await user.tab();

    expect(container.contains(document.activeElement)).toBe(false);
  });

  it('sends exactly one GET to the read, and nothing else', async () => {
    serve();
    renderPage();
    await cards();

    expect(requests).toEqual([
      {
        method: 'GET',
        url: `http://localhost:5000/api/organizations/${DEMONSTRATION_ORGANIZATION_ID}/process`,
      },
    ]);
  });
});
