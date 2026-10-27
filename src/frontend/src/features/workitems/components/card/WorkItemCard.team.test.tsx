import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { gate } from '../../../../test/gate';
import { server } from '../../../../test/mocks/server';
import { cardTexts } from '../../lib/cardTexts';
import type { WorkItemsTestApi } from '../../testApi';
import {
  PROBLEM_HEADERS,
  TEAMS_URL,
  WORK_ITEM_URL,
  hierarchyDataset,
  ids,
  problem,
  recordOf,
} from '../../testFixtures';
import { held, openCard } from './testCard';

// mod-017 US-1, design § 3: «Команда» in block B of the band, row 1 (mod-018 D-6).

vi.setConfig({ testTimeout: 60_000 });

const team = held(() => screen.getByRole('combobox', { name: 'Команда' }));
const save = held(() => screen.getByRole('button', { name: 'Сохранить' }));
const patches = (api: WorkItemsTestApi) => api.writes().filter((r) => r.method === 'PATCH');

/** «MSK-US-2» is on MARS. */
function marsDataset() {
  const data = hierarchyDataset();
  recordOf(data, 2).teamId = ids.marsTeam;
  return data;
}

async function pick(option: string) {
  await userEvent.click(team());
  await userEvent.click(
    within(await screen.findByRole('listbox')).getByRole('option', { name: option }),
  );
}

describe('mod-017 US-1 Scenario: the card shows and keeps the owning team apart from everything else', () => {
  it('stands in block B of the band, named «Команда» by its label only, with the stored team as its value', async () => {
    await openCard('MSK-US-2', { data: marsDataset() });

    expect(team()).toHaveValue('MARS');
    expect(team()).toHaveAccessibleName('Команда');
    const state = screen.getByRole('combobox', { name: 'Статус' });
    expect(state.closest('[data-band-block]')).not.toBe(team().closest('[data-band-block]'));
  });

  it('shows «Без команды» as a value for an item that has none, and offers it first in the list', async () => {
    await openCard('MSK-TASK-3');

    expect(team()).toHaveValue('Без команды');
    await userEvent.click(team());
    expect(
      within(await screen.findByRole('listbox'))
        .getAllByRole('option')
        .map((option) => option.textContent),
    ).toEqual(['Без команды', 'MARS', 'VENUS']);
  });

  it('reads the teams when the list first opens: «Загрузка…» meanwhile, and nothing is read before', async () => {
    const held = gate();
    const { api } = await openCard('MSK-US-2', {
      data: marsDataset(),
      prepare: () =>
        server.use(
          http.get(TEAMS_URL, async () => {
            await held.opened;
            return undefined;
          }),
        ),
    });
    expect(api.reads('/teams')).toHaveLength(0);

    await userEvent.click(team());

    expect(await screen.findByText('Загрузка…')).toBeInTheDocument();
    held.open();
    expect(await screen.findByRole('option', { name: 'VENUS' })).toBeInTheDocument();
    expect(api.reads('/teams')).toHaveLength(1);
  });

  it('says so in the list when the teams could not be read, and opening it again reads again', async () => {
    let failing = true;
    await openCard('MSK-US-2', {
      data: marsDataset(),
      prepare: () =>
        server.use(
          http.get(TEAMS_URL, () => {
            if (failing) {
              failing = false;
              return HttpResponse.json(problem('organizations.access.forbidden'), {
                status: 500,
                headers: PROBLEM_HEADERS,
              });
            }
            return undefined;
          }),
        ),
    });

    await userEvent.click(team());
    expect(
      await screen.findByText('Не удалось загрузить команды. Попробуйте ещё раз.'),
    ).toBeInTheDocument();
    await userEvent.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('listbox')).not.toBeInTheDocument());

    await userEvent.click(team());
    expect(await screen.findByRole('option', { name: 'VENUS' })).toBeInTheDocument();
  });

  it('an untouched team is never named by the save; a pick sends it in the wrapper and «Сохранить» stores it', async () => {
    const { api, notify } = await openCard('MSK-US-2', { data: marsDataset() });
    expect(save()).toBeDisabled();

    await pick('VENUS');
    expect(save()).toBeEnabled();
    await userEvent.click(save());

    await waitFor(() => expect(notify).toHaveBeenCalledWith(cardTexts.notices.saved));
    expect(patches(api)[0]?.body).toEqual({ team: { teamId: ids.venusTeam } });
    expect(team()).toHaveValue('VENUS');
    expect(save()).toBeDisabled();
  });

  it('«Без команды» sends `{ teamId: null }`; the undo restores the stored team', async () => {
    const { api, notify } = await openCard('MSK-US-2', { data: marsDataset() });

    await pick('Без команды');
    expect(team()).toHaveValue('Без команды');
    await userEvent.click(screen.getByRole('button', { name: 'Отменить все изменения' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Отменить изменения' }));
    await waitFor(() => expect(team()).toHaveValue('MARS'));
    expect(patches(api)).toHaveLength(0);

    await pick('Без команды');
    await userEvent.click(save());
    await waitFor(() => expect(notify).toHaveBeenCalledWith(cardTexts.notices.saved));
    expect(patches(api)[0]?.body).toEqual({ team: { teamId: null } });
    expect(recordOf(api.data, 2).teamId).toBeNull();
  });

  it('a save that touches another field never names the team, whoever changed it meanwhile', async () => {
    const { api } = await openCard('MSK-US-2', { data: marsDataset() });
    recordOf(api.data, 2).teamId = ids.venusTeam;

    await userEvent.click(screen.getByRole('combobox', { name: 'Приоритет' }));
    await userEvent.click(await screen.findByRole('option', { name: '1' }));
    await userEvent.click(save());

    await waitFor(() => expect(patches(api)).toHaveLength(1));
    expect(patches(api)[0]?.body).toEqual({ priority: { value: 1 } });
    expect(recordOf(api.data, 2).teamId).toBe(ids.venusTeam);
  });

  it.each([
    ['organizations.team.not_in_project', 409, 'Команда не участвует в проекте.'],
    ['organizations.team.not_found', 404, 'Команда не найдена.'],
  ])(
    '%s: «%s» under the field, the choice kept, the focus on it, no frame replaces the card',
    async (code, status, text) => {
      const { api, onGone } = await openCard('MSK-US-2', { data: marsDataset() });
      await pick('VENUS');
      server.use(
        http.patch(
          WORK_ITEM_URL,
          () => HttpResponse.json(problem(code), { status, headers: PROBLEM_HEADERS }),
          { once: true },
        ),
      );

      await userEvent.click(save());

      expect(await screen.findByText(text)).toBeInTheDocument();
      expect(team()).toHaveValue('VENUS');
      expect(team()).toHaveFocus();
      expect(team()).toHaveAttribute('aria-invalid', 'true');
      expect(onGone).not.toHaveBeenCalled();
      expect(recordOf(api.data, 2).teamId).toBe(ids.marsTeam);
    },
  );

  it('is read-only for the reader: the value as text, no list', async () => {
    await openCard('MSK-US-2', { data: marsDataset(), card: { readOnly: true } });

    expect(team()).toHaveValue('MARS');
    await userEvent.click(team());
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
  });
});
