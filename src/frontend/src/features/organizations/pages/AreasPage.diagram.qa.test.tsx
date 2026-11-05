// QA pass for feat-049: the gestures and states the developer's file does not reach — Ctrl + wheel
// and middle-button pan, search and jump, the hidden-node toasts, the vertical note, the live
// region, the level announcements and the deactivated node. Timings are not measurable in jsdom.
import { beforeEach, describe, expect, it } from 'vitest';
import { act, fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { department, exampleDataset, ids } from '../testFixtures';
import { serveOrganizations } from '../testApi';
import { ACTING_USER, renderPage } from '../testRender';
import { structureStorageKey } from '../hooks/useStructureSettings';

const AREAS = `/organizations/${ids.alfa}/areas`;

async function openDiagram() {
  await userEvent.click(await screen.findByRole('tab', { name: 'Диаграмма' }));
  return screen.findByRole('tree', { name: 'Диаграмма структуры' });
}

const zoomButton = () => screen.getByRole('button', { name: /^Масштаб: / });
const liveText = () => screen.getAllByRole('status').at(-1)!.textContent;
const search = () => screen.getByPlaceholderText('Найти узел');
const item = (tree: HTMLElement, name: RegExp) => within(tree).getByRole('treeitem', { name });
const chip = (level: number) => screen.getAllByRole('button', { name: `Уровень ${level}` })[0]!;
const frameOf = (tree: HTMLElement) => tree.parentElement!.parentElement!.parentElement!;

function wheel(target: HTMLElement, deltaY: number, ctrlKey: boolean) {
  const event = new WheelEvent('wheel', { deltaY, ctrlKey, bubbles: true, cancelable: true });
  act(() => void target.dispatchEvent(event));
  return event;
}

beforeEach(() => window.localStorage.clear());

describe('feat-049 BR-SD-20: Ctrl + wheel zoom and middle-button pan', () => {
  it('zooms with Ctrl + wheel, stops at both ends and does not zoom the page', async () => {
    serveOrganizations();
    renderPage({ path: AREAS });
    const tree = await openDiagram();

    expect(wheel(tree, -300, false).defaultPrevented).toBe(false);
    expect(zoomButton()).toHaveTextContent('100%');

    expect(wheel(tree, -200, true).defaultPrevented).toBe(true);
    await waitFor(() => expect(zoomButton()).not.toHaveTextContent('100%'));
    expect(Number.parseInt(zoomButton().textContent!, 10)).toBeGreaterThan(100);

    for (let i = 0; i < 12; i += 1) {
      wheel(tree, -500, true);
      await waitFor(() => expect(zoomButton()).toBeInTheDocument());
    }
    await waitFor(() => expect(zoomButton()).toHaveTextContent('200%'));
    expect(screen.getByRole('button', { name: /^Увеличить масштаб/ })).toHaveAttribute(
      'aria-disabled',
      'true',
    );

    for (let i = 0; i < 16; i += 1) {
      wheel(tree, 500, true);
      await waitFor(() => expect(zoomButton()).toBeInTheDocument());
    }
    await waitFor(() => expect(zoomButton()).toHaveTextContent('50%'));
  });

  it('adds up small touchpad deltas and a burst inside one render', async () => {
    serveOrganizations();
    renderPage({ path: AREAS });
    const tree = await openDiagram();

    // Each delta alone rounds back to the same zoom; together they are about +22 %.
    act(() => {
      for (let i = 0; i < 50; i += 1) {
        tree.dispatchEvent(
          new WheelEvent('wheel', { deltaY: -4, ctrlKey: true, bubbles: true, cancelable: true }),
        );
      }
    });

    await waitFor(() => expect(zoomButton()).toHaveTextContent('122%'));
  });

  it('returns to 100 % from the value button and announces each step', async () => {
    serveOrganizations();
    renderPage({ path: AREAS });
    await openDiagram();
    await userEvent.click(screen.getByRole('button', { name: /^Увеличить масштаб/ }));
    expect(liveText()).toBe('Масштаб: 110%');
    await userEvent.click(screen.getByRole('button', { name: 'Масштаб: 110%. Вернуть 100%' }));
    expect(zoomButton()).toHaveTextContent('100%');
    expect(zoomButton()).toBeDisabled();
    expect(liveText()).toBe('Масштаб: 100%');
  });

  it('pans on the middle button only, with a grabbing cursor while it is held', async () => {
    serveOrganizations();
    renderPage({ path: AREAS });
    const tree = await openDiagram();
    const frame = frameOf(tree);
    Object.defineProperty(frame, 'scrollLeft', { value: 100, writable: true });
    Object.defineProperty(frame, 'scrollTop', { value: 80, writable: true });

    const primary = new MouseEvent('mousedown', { button: 0, bubbles: true, cancelable: true });
    act(() => void frame.dispatchEvent(primary));
    expect(primary.defaultPrevented).toBe(false);
    expect(getComputedStyle(frame).cursor).not.toBe('grabbing');

    const middle = new MouseEvent('mousedown', {
      button: 1,
      buttons: 4,
      clientX: 200,
      clientY: 200,
      bubbles: true,
      cancelable: true,
    });
    act(() => void frame.dispatchEvent(middle));
    expect(middle.defaultPrevented).toBe(true);
    await waitFor(() => expect(getComputedStyle(frame).cursor).toBe('grabbing'));

    fireEvent.mouseMove(window, { buttons: 4, clientX: 150, clientY: 170 });
    expect(frame.scrollLeft).toBe(150);
    expect(frame.scrollTop).toBe(110);

    fireEvent.mouseUp(window, { button: 1 });
    await waitFor(() => expect(getComputedStyle(frame).cursor).not.toBe('grabbing'));
    fireEvent.mouseMove(window, { buttons: 4, clientX: 0, clientY: 0 });
    expect(frame.scrollLeft).toBe(150);
  });

  it('ends the pan when the button is released outside the page', async () => {
    serveOrganizations();
    renderPage({ path: AREAS });
    const tree = await openDiagram();
    const frame = frameOf(tree);
    act(() => {
      frame.dispatchEvent(
        new MouseEvent('mousedown', { button: 1, buttons: 4, bubbles: true, cancelable: true }),
      );
    });
    await waitFor(() => expect(getComputedStyle(frame).cursor).toBe('grabbing'));
    fireEvent.mouseMove(window, { buttons: 0 });
    await waitFor(() => expect(getComputedStyle(frame).cursor).not.toBe('grabbing'));
  });
});

describe('feat-049 US-2: find a node', () => {
  it('marks matches ignoring case and ё/е, never removes a card, and counts them', async () => {
    serveOrganizations();
    renderPage({ path: AREAS });
    const tree = await openDiagram();
    const before = within(tree).getAllByRole('treeitem').length;

    await userEvent.type(search(), 'ОТЧЕТЫ');

    expect(screen.getByText('Найдено: 1. Enter — к следующему')).toBeInTheDocument();
    expect(within(tree).getAllByRole('treeitem')).toHaveLength(before);
  });

  it('says nothing was found, does nothing on Enter and clears on emptying the field', async () => {
    serveOrganizations();
    renderPage({ path: AREAS });
    const tree = await openDiagram();
    await userEvent.type(search(), 'нет такого{Enter}');
    expect(screen.getByText('Ничего не найдено')).toBeInTheDocument();
    expect(tree.contains(document.activeElement)).toBe(false);
    await userEvent.clear(search());
    expect(screen.queryByText('Ничего не найдено')).not.toBeInTheDocument();
  });

  it('unfolds the branches hiding the match and focuses it, again on the next Enter', async () => {
    serveOrganizations();
    renderPage({ path: AREAS });
    const tree = await openDiagram();
    await userEvent.click(screen.getByRole('button', { name: 'Свернуть всё' }));
    expect(within(tree).queryByRole('treeitem', { name: /Область Шлюз/ })).not.toBeInTheDocument();

    await userEvent.type(search(), 'шлюз{Enter}');

    const gateway = await within(tree).findByRole('treeitem', { name: /Область Шлюз/ });
    await waitFor(() => expect(gateway).toHaveFocus());

    await userEvent.click(search());
    await userEvent.keyboard('{Enter}');
    await waitFor(() => expect(gateway).toHaveFocus());
  });

  it('tells a match on a hidden level by a toast, leaving focus and the level choice alone', async () => {
    serveOrganizations();
    renderPage({ path: AREAS });
    const tree = await openDiagram();
    await userEvent.click(chip(3));
    expect(liveText()).toBe('Показаны уровни: 3');

    await userEvent.type(search(), 'отчёты{Enter}');

    expect(await screen.findByText('Узел скрыт выбором уровней')).toBeInTheDocument();
    expect(tree.contains(document.activeElement)).toBe(false);
    expect(chip(3)).toHaveAttribute('aria-pressed', 'true');
    expect(chip(1)).toHaveAttribute('aria-pressed', 'false');
  });

  it('writes nothing to the expansion when «Только связанные» hides the match (QA F-3)', async () => {
    serveOrganizations();
    renderPage({ path: AREAS });
    const tree = await openDiagram();
    await userEvent.click(screen.getByRole('button', { name: 'Свернуть всё' }));
    await userEvent.click(screen.getByRole('button', { name: 'Только связанные' }));
    await userEvent.click(item(tree, /^Департамент Отчёты/));

    await userEvent.type(search(), 'шлюз{Enter}');
    await screen.findByText('Узел скрыт режимом «Только связанные»');
    await userEvent.click(screen.getByRole('button', { name: 'Затемнение' }));

    expect(within(tree).queryByRole('treeitem', { name: /Шлюз/ })).toBeNull();
  });

  it('unfolds a folded section and focuses a related match in «Только связанные» (CR-10)', async () => {
    serveOrganizations();
    renderPage({ path: AREAS });
    const tree = await openDiagram();
    await userEvent.click(screen.getByRole('button', { name: 'Только связанные' }));
    await userEvent.click(item(tree, /^Департамент Платформа/));
    const header = within(tree)
      .getAllByRole('treeitem')
      .find(
        (node) =>
          node.getAttribute('aria-level') === '1' &&
          /Департаменты/.test(node.getAttribute('aria-label') ?? ''),
      )!;
    header.focus();
    await userEvent.keyboard('{ArrowLeft}');
    expect(header).toHaveAttribute('aria-expanded', 'false');

    await userEvent.type(search(), 'платформа{Enter}');

    await waitFor(() => expect(header).toHaveAttribute('aria-expanded', 'true'));
    await waitFor(() => expect(item(tree, /^Департамент Платформа/)).toHaveFocus());
    expect(screen.queryByText('Узел скрыт режимом «Только связанные»')).not.toBeInTheDocument();
  });

  it('tells a match removed by «Только связанные» by a toast', async () => {
    serveOrganizations();
    renderPage({ path: AREAS });
    const tree = await openDiagram();
    await userEvent.click(screen.getByRole('button', { name: 'Только связанные' }));
    await userEvent.click(item(tree, /^Департамент Отчёты/));
    expect(within(tree).queryByRole('treeitem', { name: /Департамент Платформа/ })).toBeNull();

    await userEvent.type(search(), 'платформа{Enter}');

    expect(await screen.findByText('Узел скрыт режимом «Только связанные»')).toBeInTheDocument();
    expect(tree.contains(document.activeElement)).toBe(false);
  });
});

describe('feat-049 US-2: levels, sections and the deactivated node', () => {
  it('announces the levels for both modes and keeps «Все уровни» in step', async () => {
    serveOrganizations();
    renderPage({ path: AREAS });
    await openDiagram();
    const all = screen.getByRole('button', { name: 'Все уровни' });
    expect(all).toBeDisabled();

    await userEvent.click(chip(1));
    await userEvent.click(chip(3));
    expect(liveText()).toBe('Показаны уровни: 1, 3');
    expect(all).toBeEnabled();

    await userEvent.click(all);
    expect(liveText()).toBe('Показаны все уровни');
    expect(all).toBeDisabled();

    await userEvent.click(screen.getByRole('button', { name: 'Уровень и вложенные' }));
    await userEvent.click(chip(2));
    await userEvent.click(chip(3));
    expect(liveText()).toBe('Показаны уровни: 2 и более глубокие');
  });

  it('says «Нет узлов на выбранных уровнях» when the chosen levels hold no drawn card', async () => {
    serveOrganizations();
    renderPage({ path: AREAS });
    await openDiagram();
    await userEvent.click(screen.getByRole('button', { name: 'Свернуть всё' }));
    await userEvent.click(chip(4));
    expect(await screen.findByText('Нет узлов на выбранных уровнях')).toBeInTheDocument();
  });

  it('shows no empty text when both sections are folded (CR-3)', async () => {
    serveOrganizations();
    renderPage({ path: AREAS });
    const tree = await openDiagram();
    await userEvent.click(chip(2));
    for (const header of within(tree)
      .getAllByRole('treeitem')
      .filter((node) => node.getAttribute('aria-level') === '1')) {
      header.focus();
      await userEvent.keyboard('{ArrowLeft}');
    }
    expect(screen.queryByText('Нет узлов на выбранных уровнях')).not.toBeInTheDocument();
    expect(screen.queryByText('Нет связанных узлов')).not.toBeInTheDocument();
  });

  it('keeps focus in the diagram when «Все уровни» finds no chip (CR-4)', async () => {
    window.localStorage.setItem(structureStorageKey('layout', ACTING_USER, ids.alfa), 'v');
    serveOrganizations();
    renderPage({ path: AREAS });
    const tree = await openDiagram();
    await userEvent.click(chip(1));
    for (const header of within(tree)
      .getAllByRole('treeitem')
      .filter((node) => node.getAttribute('aria-level') === '1')) {
      header.focus();
      await userEvent.keyboard('{ArrowLeft}');
    }
    expect(screen.queryAllByRole('button', { name: 'Уровень 1' })).toHaveLength(0);

    await userEvent.click(screen.getByRole('button', { name: 'Все уровни' }));

    await waitFor(() => expect(frameOf(tree).contains(document.activeElement)).toBe(true));
  });

  it('folds a section from its header with the arrow keys and announces it', async () => {
    serveOrganizations();
    renderPage({ path: AREAS });
    const tree = await openDiagram();
    const header = within(tree)
      .getAllByRole('treeitem')
      .find((node) => node.getAttribute('aria-level') === '1')!;
    expect(header).toHaveAttribute('aria-expanded', 'true');
    header.focus();
    await userEvent.keyboard('{ArrowLeft}');
    expect(header).toHaveAttribute('aria-expanded', 'false');
    expect(liveText()).toMatch(/: свёрнуто$/);
    await userEvent.keyboard('{ArrowRight}');
    expect(header).toHaveAttribute('aria-expanded', 'true');
    expect(liveText()).toMatch(/: развёрнуто$/);
  });

  it('draws a deactivated project tagged, not choosable and opening nothing', async () => {
    serveOrganizations();
    renderPage({ path: AREAS });
    const tree = await openDiagram();
    const dead = item(tree, /Старый проект.*деактивирован/);
    expect(dead).not.toHaveAttribute('aria-selected');
    expect(within(dead).queryByRole('link')).not.toBeInTheDocument();
    expect(within(dead).getByText('деактивирован')).toBeInTheDocument();

    dead.focus();
    await userEvent.keyboard(' ');
    expect(liveText()).toBe('Этот узел выбрать нельзя');
    await userEvent.keyboard('{Enter}');
    expect(liveText()).toBe('Узел деактивированного проекта ничего не открывает');
  });

  it('chooses by the body of a department and announces the branch summary', async () => {
    serveOrganizations();
    renderPage({ path: AREAS });
    const tree = await openDiagram();
    const dept = item(tree, /^Департамент Платформа/);
    expect(within(dept).getByRole('link', { name: 'Платформа' })).toBeInTheDocument();
    await userEvent.click(dept);
    expect(dept).toHaveAttribute('aria-selected', 'true');
    expect(liveText()).toMatch(/^Ветка департамента:/);
  });

  it('announces the cleared choice', async () => {
    serveOrganizations();
    renderPage({ path: AREAS });
    const tree = await openDiagram();
    const dept = item(tree, /^Департамент Платформа/);
    await userEvent.click(dept);
    await userEvent.click(screen.getByRole('button', { name: 'Сбросить' }));
    expect(liveText()).toBe('Выбор сброшен');
    expect(dept).toHaveAttribute('aria-selected', 'false');
  });
});

describe('feat-049 BR-SD-15: the wide vertical picture', () => {
  async function wide(orientation: 'h' | 'v') {
    const data = exampleDataset();
    for (let i = 0; i < 125; i += 1) {
      data.areas.push(department(`dep-extra-${i}`, ids.alfa, `Отдел ${i}`));
    }
    window.localStorage.setItem(structureStorageKey('layout', ACTING_USER, ids.alfa), orientation);
    serveOrganizations(data);
    renderPage({ path: AREAS });
    await openDiagram();
  }

  it('shows the note above 120 visible cards in the vertical layout and blocks nothing', async () => {
    await wide('v');
    expect(
      await screen.findByText(/^Вертикальная раскладка широкая: показано узлов — \d+\./),
    ).toBeVisible();
    expect(screen.getByRole('button', { name: 'Горизонтально' })).toBeEnabled();
  });

  it('shows no note in the horizontal layout', async () => {
    await wide('h');
    expect(screen.queryByText(/^Вертикальная раскладка широкая/)).not.toBeInTheDocument();
  });
});

describe('feat-049 BR-SD-3: no read beyond the two', () => {
  it('sends no write and no new kind of read while choosing, switching mode, layout and view', async () => {
    const api = serveOrganizations();
    renderPage({ path: AREAS });
    const tree = await openDiagram();
    await userEvent.click(item(tree, /^Департамент Платформа/));
    await userEvent.click(screen.getByRole('button', { name: 'Подсветка' }));
    await userEvent.click(screen.getByRole('button', { name: 'Вертикально' }));
    await userEvent.click(chip(1));
    await userEvent.click(screen.getByRole('tab', { name: 'Дерево' }));
    await userEvent.click(screen.getByRole('tab', { name: 'Диаграмма' }));

    expect(api.writes()).toEqual([]);
    const reads = api.requests.filter((request) => request.method === 'GET');
    const offerReads = reads.filter((request) => /selector-offers/.test(request.path));
    expect(offerReads.length).toBeLessThanOrEqual(2);
  });
});
