import { afterEach, describe, expect, it } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { header, mskRow, orgAddress, renderAggregate, serveAggregate } from '../testAggregate';
import { ids } from '../testFixtures';

// feat-048 QA D-1..D-3 (design § 2, § 3): the announcement of a grouping, the narrowed empty
// copy and the board's count line.

afterEach(() => localStorage.clear());

const AREA_BACKLOG = `/organizations/${ids.example}/areas/d-1/backlogs/stories?teamId=t-1`;

describe('QA D-1: a grouping choice is announced', () => {
  it('speaks «Группировка: …» through the status region on a change, and not before', async () => {
    serveAggregate({
      groups: { project: [header('project', 'p-msk', 'Мастаск', 1, { key: 'MSK' })], none: [] },
      rowsOf: () => [mskRow(1)],
    });
    renderAggregate(orgAddress('backlogs'));
    await screen.findByText('Мастаск');
    expect(screen.getAllByRole('status').map((region) => region.textContent)).not.toContain(
      'Группировка: По проекту',
    );

    await userEvent.click(screen.getByRole('button', { name: 'Группировка: По проекту' }));
    await userEvent.click(screen.getByRole('menuitemradio', { name: 'Без группировки' }));

    await waitFor(() =>
      expect(screen.getAllByRole('status').map((region) => region.textContent)).toContain(
        'Группировка: Без группировки',
      ),
    );
  });
});

describe('QA D-2: a narrowed empty view says «Ничего не найдено»', () => {
  it('on the backlog', async () => {
    serveAggregate({ groups: { project: [] } });
    renderAggregate(AREA_BACKLOG);
    expect(await screen.findByText('Ничего не найдено')).toBeInTheDocument();
    expect(screen.queryByText('В бэклоге пока нет элементов')).not.toBeInTheDocument();
  });
});

describe('QA D-3: the board without grouping shows the count line', () => {
  it('draws «Элементов: N»', async () => {
    serveAggregate({ groups: { none: [header('none', null, null, 4)] }, cardsOf: () => [] });
    renderAggregate(orgAddress('boards'));
    await userEvent.click(await screen.findByRole('button', { name: /Группировка/ }));
    await userEvent.click(screen.getByRole('menuitemradio', { name: 'Без группировки' }));
    expect(await screen.findByText('Элементов: 4')).toBeInTheDocument();
  });
});
