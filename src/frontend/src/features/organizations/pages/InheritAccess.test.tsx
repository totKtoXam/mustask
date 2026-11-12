import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ids } from '../testFixtures';
import { people } from '../testMembersData';
import { openPage } from '../testMembersPage';

// mod-019 design §§ 4-5: «Наследовать доступ» on a project's node, its read-only fact and the confirmation.

const NODE_PATH = `/organizations/${ids.alfa}/areas/${ids.prmRoot}/overview`;
const TOP_LEVEL_PATH = `/organizations/${ids.alfa}/areas/${ids.anlRoot}/overview`;

describe('mod-019 «Наследовать доступ»', () => {
  it('shows the switch on for the manager, with what it means for this project', async () => {
    openPage({ path: NODE_PATH });

    const control = await screen.findByRole('switch', { name: 'Наследовать доступ' });

    expect(control).toBeChecked();
    expect(
      screen.getByText(
        'Доступ департамента «Платформа» действует в проекте «Пример проекта», его рабочих элементах и областях.',
      ),
    ).toBeVisible();
  });

  it('shows a reader the fact and no switch', async () => {
    openPage({ path: NODE_PATH, role: 'viewer' });

    expect(await screen.findByText('Наследовать доступ: Включено')).toBeVisible();
    expect(screen.queryByRole('switch')).toBeNull();
  });

  it('says that a top-level project has nothing to inherit from yet', async () => {
    openPage({ path: TOP_LEVEL_PATH });

    expect(
      await screen.findByText(
        'Над проектом нет департамента, поэтому выбор пока ни на что не влияет. Он начнёт действовать, когда проект переместят в департамент.',
      ),
    ).toBeVisible();
  });

  it('asks before turning it off: the count, the caller’s line and the access the users gave', async () => {
    const { data, organizations } = openPage({ path: NODE_PATH });
    data.switchImpact = {
      holderCount: 3,
      callerIncluded: true,
      givenGrants: [{ user: { ...people.maria }, role: 'reader', givenBy: { ...people.ivan } }],
      givenGrantsTotal: 1,
    };
    await userEvent.click(await screen.findByRole('switch', { name: 'Наследовать доступ' }));

    const dialog = await screen.findByRole('dialog', { name: 'Отключить наследование доступа?' });
    expect(
      await within(dialog).findByText('Потеряют доступ через департамент: 3 пользователя.'),
    ).toBeVisible();
    expect(within(dialog).getByText(/Вы входите в их число/)).toBeVisible();
    expect(
      within(dialog).getByText('Доступ в проекте, выданный этими пользователями (1)'),
    ).toBeVisible();
    expect(organizations.writes()).toHaveLength(0);

    await userEvent.click(within(dialog).getByRole('button', { name: 'Отключить' }));

    await waitFor(() =>
      expect(organizations.writes().map((write) => [write.method, write.body])).toEqual([
        ['PUT', { value: false }],
      ]),
    );
    expect(await screen.findByText('Наследование доступа отключено.')).toBeVisible();
    expect(await screen.findByRole('switch', { name: 'Наследовать доступ' })).not.toBeChecked();
  });

  it('says that nobody loses access when the count is zero, and drops the given-access table', async () => {
    openPage({ path: NODE_PATH });
    await userEvent.click(await screen.findByRole('switch', { name: 'Наследовать доступ' }));

    const dialog = await screen.findByRole('dialog', { name: 'Отключить наследование доступа?' });

    expect(await within(dialog).findByText('Это не отнимет доступ ни у кого.')).toBeVisible();
    expect(within(dialog).queryByRole('table')).toBeNull();
  });

  it('turns it on again at once, with no confirmation', async () => {
    const { organizations } = openPage({ path: NODE_PATH });
    organizations.inherits[ids.prmRoot] = false;
    await userEvent.click(await screen.findByRole('switch', { name: 'Наследовать доступ' }));

    await waitFor(() =>
      expect(organizations.writes().map((write) => write.body)).toEqual([{ value: true }]),
    );
    expect(screen.queryByRole('dialog')).toBeNull();
    expect(await screen.findByText('Наследование доступа включено.')).toBeVisible();
  });
});
