import { describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { hierarchyDataset, ids } from '../../testFixtures';
import { held, openCard } from './testCard';

// feat-040 design § 7, D-2, BR-TS-3: «Проект» in block A of the band, row 2, under «Статус» — the name
// of the item's project as read-only text, for every role and in every state.

vi.setConfig({ testTimeout: 60_000 });

const project = held(() => screen.getByRole('textbox', { name: 'Проект' }));

describe('feat-040 D-2: the card shows the item’s project in the band', () => {
  it('stands in block A under «Статус», named «Проект» by its label, with the project’s name as its value', async () => {
    await openCard('MSK-US-2');

    expect(project()).toHaveValue('Мастаск');
    expect(project()).toHaveAccessibleName('Проект');
    const state = screen.getByRole('combobox', { name: 'Статус' });
    expect(
      state.compareDocumentPosition(project()) & Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();
    expect(state.closest('[data-band-block]')).toBe(project().closest('[data-band-block]'));
    // «Команда» is of block B, apart from it.
    expect(screen.getByRole('combobox', { name: 'Команда' }).closest('[data-band-block]')).not.toBe(
      project().closest('[data-band-block]'),
    );
  });

  it('is read-only: no picker opens, a typed key changes nothing, and the card is not dirty', async () => {
    await openCard('MSK-US-2');

    expect(project()).toHaveAttribute('readonly');
    await userEvent.click(project());
    await userEvent.keyboard('Бета');
    expect(project()).toHaveValue('Мастаск');
    expect(screen.queryByRole('listbox')).toBeNull();
    expect(screen.getByRole('button', { name: 'Сохранить' })).toBeDisabled();
  });

  it('names the project as it stands now: a renamed project reads at once', async () => {
    const data = hierarchyDataset();
    const msk = data.projects.find((candidate) => candidate.id === ids.msk);
    if (!msk) throw new Error('No project.');
    msk.name = 'Мастаск Плюс';

    await openCard('MSK-US-2', { data });

    expect(project()).toHaveValue('Мастаск Плюс');
  });

  it('is there for a reader too, and still read-only', async () => {
    await openCard('MSK-US-2', { card: { readOnly: true, parentReadOnly: true } });

    expect(project()).toHaveValue('Мастаск');
    expect(project()).toHaveAttribute('readonly');
  });
});
