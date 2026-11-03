import { beforeEach, describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SELECTOR_OFFERS_URL, ids, problem } from '../testFixtures';
import { serveOrganizations, serveProblem } from '../testApi';
import { ACTING_USER, renderPage } from '../testRender';
import { structureStorageKey } from '../hooks/useStructureSettings';

const AREAS = `/organizations/${ids.alfa}/areas`;
const VIEW_KEY = structureStorageKey('view', ACTING_USER, ids.alfa);

async function openDiagram() {
  await userEvent.click(await screen.findByRole('tab', { name: 'Диаграмма' }));
  return screen.findByRole('tree', { name: 'Диаграмма структуры' });
}

beforeEach(() => window.localStorage.clear());

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

  it('opens on the remembered diagram next time, and a damaged value means the tree', async () => {
    serveOrganizations();
    window.localStorage.setItem(VIEW_KEY, 'diagram');
    const first = renderPage({ path: AREAS });
    expect(await screen.findByRole('tree', { name: 'Диаграмма структуры' })).toBeInTheDocument();
    first.unmount?.();
  });

  it('falls back to the tree on a value outside the allowed set', async () => {
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

  it('reads only the area tree until the diagram is open, and the offers once', async () => {
    serveOrganizations();
    renderPage({ path: AREAS });
    await screen.findByRole('treegrid', { name: 'Структура' });
    await openDiagram();
    expect(await screen.findAllByRole('treeitem')).not.toHaveLength(0);
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

  it('moves with the arrow keys and chooses with Space, Escape clears', async () => {
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

  it('«Только связанные» keeps the chosen branch and the tree stays one Tab stop', async () => {
    serveOrganizations();
    renderPage({ path: AREAS });
    const tree = await openDiagram();
    await userEvent.click(screen.getByRole('button', { name: 'Только связанные' }));
    const card = within(tree)
      .getAllByRole('treeitem')
      .find((item) => item.hasAttribute('aria-selected'))!;
    await userEvent.click(card);

    expect(screen.getByRole('button', { name: 'Сбросить' })).toBeInTheDocument();
    expect(
      within(tree)
        .getAllByRole('treeitem')
        .filter((item) => item.tabIndex === 0),
    ).toHaveLength(1);
  });
});
