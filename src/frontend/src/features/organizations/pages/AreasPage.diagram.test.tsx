import { beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SELECTOR_OFFERS_URL, ids, problem } from '../testFixtures';
import { serveOrganizations, serveProblem } from '../testApi';
import { ACTING_USER, renderPage } from '../testRender';
import { layoutDiagram } from '../lib/diagramLayout';
import { structureStorageKey } from '../hooks/useStructureSettings';

// A pass-through spy: the layout is a pure function, so counting its calls shows when it is rebuilt.
vi.mock('../lib/diagramLayout', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../lib/diagramLayout')>();
  return { ...actual, layoutDiagram: vi.fn(actual.layoutDiagram) };
});

const AREAS = `/organizations/${ids.alfa}/areas`;
const VIEW_KEY = structureStorageKey('view', ACTING_USER, ids.alfa);

async function openDiagram() {
  await userEvent.click(await screen.findByRole('tab', { name: 'Диаграмма' }));
  return screen.findByRole('tree', { name: 'Диаграмма структуры' });
}

beforeEach(() => {
  window.localStorage.clear();
  vi.mocked(layoutDiagram).mockClear();
});

describe('feat-049 US-1: the view switch', () => {
  it('opens on the tree and switches to the diagram, remembering the choice', async () => {
    serveOrganizations();
    renderPage({ path: AREAS });
    await screen.findByRole('treegrid', { name: 'Структура' });
    expect(screen.getByRole('tab', { name: 'Дерево' })).toHaveAttribute('aria-selected', 'true');
    expect(window.localStorage.getItem(VIEW_KEY)).toBeNull();

    await openDiagram();

    expect(screen.queryByRole('treegrid')).not.toBeInTheDocument();
    expect(window.localStorage.getItem(VIEW_KEY)).toBe('diagram');
  });

  it('opens on the remembered diagram next time', async () => {
    serveOrganizations();
    window.localStorage.setItem(VIEW_KEY, 'diagram');
    renderPage({ path: AREAS });
    expect(await screen.findByRole('tree', { name: 'Диаграмма структуры' })).toBeInTheDocument();
    expect(screen.queryByRole('treegrid')).not.toBeInTheDocument();
  });

  it('a damaged stored value means the tree', async () => {
    serveOrganizations();
    window.localStorage.setItem(VIEW_KEY, 'cube');
    renderPage({ path: AREAS });
    expect(await screen.findByRole('treegrid', { name: 'Структура' })).toBeInTheDocument();
  });

  it('keeps the memory per user and organization', async () => {
    serveOrganizations();
    window.localStorage.setItem(structureStorageKey('view', 'someone-else', ids.alfa), 'diagram');
    renderPage({ path: AREAS });
    expect(await screen.findByRole('treegrid', { name: 'Структура' })).toBeInTheDocument();
  });

  it('keeps the search and the zoom across a switch', async () => {
    serveOrganizations();
    renderPage({ path: AREAS });
    await openDiagram();
    await userEvent.click(screen.getByRole('button', { name: /^Увеличить масштаб/ }));
    await userEvent.click(screen.getByRole('tab', { name: 'Дерево' }));
    await screen.findByRole('treegrid', { name: 'Структура' });
    await userEvent.click(screen.getByRole('tab', { name: 'Диаграмма' }));
    expect(await screen.findByRole('button', { name: /^Масштаб: / })).toHaveTextContent('110%');
  });

  it('switches the view from the keyboard: arrow moves focus, Enter confirms', async () => {
    serveOrganizations();
    renderPage({ path: AREAS });
    const treeTab = await screen.findByRole('tab', { name: 'Дерево' });
    treeTab.focus();
    await userEvent.keyboard('{ArrowRight}');
    const diagramTab = screen.getByRole('tab', { name: 'Диаграмма' });
    expect(diagramTab).toHaveFocus();
    expect(screen.queryByRole('tree', { name: 'Диаграмма структуры' })).not.toBeInTheDocument();

    await userEvent.keyboard('{Enter}');

    expect(await screen.findByRole('tree', { name: 'Диаграмма структуры' })).toBeInTheDocument();
    expect(diagramTab).toHaveAttribute('aria-selected', 'true');
  });

  it('moves between the two views with the arrow keys', async () => {
    serveOrganizations();
    renderPage({ path: AREAS });
    const tree = await screen.findByRole('tab', { name: 'Дерево' });
    tree.focus();
    await userEvent.keyboard('{ArrowRight}');
    expect(screen.getByRole('tab', { name: 'Диаграмма' })).toHaveFocus();
  });
});

describe('feat-049 US-2: the hierarchy diagram', () => {
  it('draws a card per node with the level as the tree item level, one Tab stop', async () => {
    serveOrganizations();
    renderPage({ path: AREAS });
    const tree = await openDiagram();

    const items = within(tree).getAllByRole('treeitem');
    expect(items.length).toBeGreaterThan(2);
    expect(items.filter((item) => item.tabIndex === 0)).toHaveLength(1);
    const department = within(tree).getAllByRole('treeitem', { name: /Платформа/ })[0]!;
    expect(department).toHaveAttribute('aria-level', '2');
  });

  it('reads only the area tree until the diagram is open, then the offers once, and nothing else', async () => {
    const api = serveOrganizations();
    renderPage({ path: AREAS });
    await screen.findByRole('treegrid', { name: 'Структура' });
    const isOffers = (path: string) => path.endsWith('/selector-offers');
    expect(api.requests.some((request) => isOffers(request.path))).toBe(false);

    const tree = await openDiagram();
    await waitFor(() => expect(within(tree).getAllByRole('treeitem').length).toBeGreaterThan(0));

    const gets = api.requests.filter((request) => request.method === 'GET');
    expect(gets.every((request) => request.path.endsWith('/areas') || isOffers(request.path))).toBe(
      true,
    );
    expect(gets.filter((request) => isOffers(request.path))).toHaveLength(1);
  });

  it('says the relations did not load and retries', async () => {
    serveOrganizations();
    serveProblem('get', SELECTOR_OFFERS_URL, problem('organizations.validation_failed'), {
      once: true,
    });
    renderPage({ path: AREAS });
    await openDiagram();

    await userEvent.click(await screen.findByRole('button', { name: 'Повторить' }));

    await waitFor(() =>
      expect(screen.queryByRole('button', { name: 'Повторить' })).not.toBeInTheDocument(),
    );
  });

  it('zoom buttons stay between 50 and 200 per cent', async () => {
    serveOrganizations();
    renderPage({ path: AREAS });
    await openDiagram();
    const out = screen.getByRole('button', { name: /^Уменьшить масштаб/ });
    for (let i = 0; i < 12; i += 1) await userEvent.click(out);
    expect(screen.getByRole('button', { name: /^Масштаб: / })).toHaveTextContent('50%');
  });

  it('moves with the arrow keys and Home', async () => {
    serveOrganizations();
    renderPage({ path: AREAS });
    const tree = await openDiagram();
    const first = within(tree)
      .getAllByRole('treeitem')
      .find((item) => item.tabIndex === 0)!;
    first.focus();
    await userEvent.keyboard('{ArrowDown}');
    const second = within(tree)
      .getAllByRole('treeitem')
      .find((item) => item.tabIndex === 0)!;
    expect(second).not.toBe(first);
    expect(second).toHaveFocus();
    await userEvent.keyboard('{Home}');
    expect(first).toHaveFocus();
  });
});

describe('feat-049 US-3: choosing a node', () => {
  it('Space chooses the focused card and Escape clears the choice', async () => {
    serveOrganizations();
    renderPage({ path: AREAS });
    const tree = await openDiagram();
    const card = within(tree)
      .getAllByRole('treeitem')
      .find((item) => item.hasAttribute('aria-selected'))!;
    card.focus();

    await userEvent.keyboard(' ');
    expect(card).toHaveAttribute('aria-selected', 'true');

    await userEvent.keyboard('{Escape}');
    expect(card).toHaveAttribute('aria-selected', 'false');
    expect(screen.queryByRole('button', { name: 'Сбросить' })).not.toBeInTheDocument();
  });

  it('rebuilds no layout when choosing in «Затемнение» and «Подсветка» with the branch expanded', async () => {
    serveOrganizations();
    renderPage({ path: AREAS });
    const tree = await openDiagram();
    const choosable = () =>
      within(tree)
        .getAllByRole('treeitem')
        .filter((item) => item.hasAttribute('aria-selected'));
    const built = vi.mocked(layoutDiagram).mock.calls.length;

    await userEvent.click(choosable()[0]!);
    await userEvent.click(choosable()[1]!);
    expect(vi.mocked(layoutDiagram).mock.calls.length).toBe(built);

    await userEvent.click(screen.getByRole('button', { name: 'Подсветка' }));
    await userEvent.click(choosable()[0]!);
    await userEvent.click(choosable()[1]!);
    expect(vi.mocked(layoutDiagram).mock.calls.length).toBe(built);
  });

  it('shows the summary with «Сбросить» and clears it on Escape', async () => {
    serveOrganizations();
    renderPage({ path: AREAS });
    const tree = await openDiagram();
    const card = within(tree)
      .getAllByRole('treeitem')
      .find((item) => item.hasAttribute('aria-selected'))!;

    await userEvent.click(card);
    expect(card).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByRole('button', { name: 'Сбросить' })).toBeInTheDocument();

    card.focus();
    await userEvent.keyboard('{Escape}');
    expect(screen.queryByRole('button', { name: 'Сбросить' })).not.toBeInTheDocument();
  });

  it('«Только связанные» removes the unrelated cards and the tree stays one Tab stop', async () => {
    serveOrganizations();
    renderPage({ path: AREAS });
    const tree = await openDiagram();
    await userEvent.click(screen.getByRole('button', { name: 'Только связанные' }));
    const card = within(tree)
      .getAllByRole('treeitem')
      .find((item) => item.hasAttribute('aria-selected'))!;
    const before = within(tree).getAllByRole('treeitem').length;
    await userEvent.click(card);

    expect(screen.getByRole('button', { name: 'Сбросить' })).toBeInTheDocument();
    expect(within(tree).getAllByRole('treeitem').length).toBeLessThan(before);
    expect(
      within(tree)
        .getAllByRole('treeitem')
        .filter((item) => item.tabIndex === 0),
    ).toHaveLength(1);
  });
});
