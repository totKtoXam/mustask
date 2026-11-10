import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http } from 'msw';
import { server } from '../../../test/mocks/server';
import { AREA_URL, ids } from '../testFixtures';
import { ANNA, people } from '../testMembersData';
import { openPage } from '../testMembersPage';

// tech-065: the debts G-2..G-4 of the mod-019 QA (frontend pass), each with the test that failed before the fix.

const NODE_PATH = `/organizations/${ids.alfa}/areas/${ids.prmRoot}/overview`;
const AREA_PATH = `/organizations/${ids.alfa}/areas/${ids.platformArea}/members`;
const PROJECT_PATH = `/organizations/${ids.alfa}/projects/${ids.prm}/access`;
const TEAM_PATH = `/organizations/${ids.alfa}/teams/${ids.platformTeam}/members`;

const ONLY_GRANTED = /Здесь показан только выданный доступ\./;

/** Holds the answer of one area read until `release()` is called; the handler then falls through. */
function holdAreaRead(areaId: string): () => void {
  let release: () => void = () => undefined;
  const gate = new Promise<void>((resolve) => {
    release = resolve;
  });
  server.use(
    http.get(AREA_URL, async ({ params }) => {
      if (params.areaId !== areaId) return undefined;
      await gate;
      return undefined;
    }),
  );
  return release;
}

describe('tech-065 G-2: the grants notice and the codes notice of a team', () => {
  it('drops «Здесь показан только выданный доступ» on the team, keeps it on the project and the department', async () => {
    openPage({ path: TEAM_PATH });
    expect(
      await screen.findByRole('heading', { level: 2, name: 'Доступ к команде' }),
    ).toBeVisible();
    expect(screen.queryByText(ONLY_GRANTED)).toBeNull();
  });

  it.each([
    ['department', AREA_PATH],
    ['project', PROJECT_PATH],
  ])('keeps the notice on the %s', async (_scope, path) => {
    openPage({ path });
    expect(await screen.findByText(ONLY_GRANTED)).toBeVisible();
  });

  it('announces «Ваш доступ в команде «…» изменился.» once when a re-read serves other codes', async () => {
    const opened = openPage({ path: TEAM_PATH });
    await screen.findByRole('heading', { level: 2, name: 'Доступ к команде' });
    expect(screen.queryByText(/Ваш доступ в команде/)).toBeNull();

    const mine = opened.data.memberships?.find((membership) => membership.userId === ANNA);
    mine!.role = 'viewer';
    opened.update({ role: 'viewer' });

    expect(await screen.findByText('Ваш доступ в команде «Платформа» изменился.')).toBeVisible();
    opened.update({ role: 'viewer' });
    await waitFor(() =>
      expect(screen.getAllByText('Ваш доступ в команде «Платформа» изменился.')).toHaveLength(1),
    );
  });
});

describe('tech-065 G-3: the switch-off dialog', () => {
  function manyGrants(count: number) {
    return Array.from({ length: count }, (_unused, index) => ({
      user: {
        ...people.maria,
        id: `user-given-${index}`,
        displayName: `Пользователь ${index + 1}`,
        email: `given${index}@example.org`,
      },
      role: 'reader' as const,
      givenBy: { ...people.ivan },
    }));
  }

  it('shows at most 6 rows of the given access and scrolls the rest (D-12)', async () => {
    const { data } = openPage({ path: NODE_PATH });
    data.switchImpact = {
      holderCount: 2,
      callerIncluded: false,
      givenGrants: manyGrants(50),
      givenGrantsTotal: 50,
    };
    await userEvent.click(await screen.findByRole('switch', { name: 'Наследовать доступ' }));

    const dialog = await screen.findByRole('dialog', { name: 'Отключить наследование доступа?' });
    const table = await within(dialog).findByRole('table');

    expect(within(table).getAllByRole('row').length).toBe(51);
    const card = table.closest('[data-table-card]') as HTMLElement;
    // 40px header + 6 rows of 36px.
    expect(getComputedStyle(card).maxHeight).toBe('256px');
    expect(getComputedStyle(card).overflow).toBe('auto');
  });

  it('draws the field skeleton, not a spinner, while the impact is read', async () => {
    const { data } = openPage({ path: NODE_PATH });
    data.switchImpact = undefined;
    let release: () => void = () => undefined;
    const gate = new Promise<void>((resolve) => {
      release = resolve;
    });
    server.use(
      http.get(`${AREA_URL}/inherits-access/impact`, async () => {
        await gate;
        return undefined;
      }),
    );
    await userEvent.click(await screen.findByRole('switch', { name: 'Наследовать доступ' }));

    const dialog = await screen.findByRole('dialog', { name: 'Отключить наследование доступа?' });
    const status = within(dialog).getByRole('status', { name: 'Загрузка данных' });
    expect(status.querySelectorAll('.MuiSkeleton-rounded').length).toBeGreaterThan(0);
    expect(within(dialog).queryByRole('progressbar')).toBeNull();

    release();
    expect(await within(dialog).findByText('Это не отнимет доступ ни у кого.')).toBeVisible();
  });
});

describe('tech-065 G-4: the inherit switch while the parent’s read is pending', () => {
  it('shows a skeleton, never the read-only fact, until the parent’s codes land, then the switch', async () => {
    const release = holdAreaRead(ids.platformArea);
    openPage({ path: NODE_PATH });

    await screen.findByText('Вложенные области (2)');
    expect(screen.queryByText(/Наследовать доступ: /)).toBeNull();
    expect(screen.queryByRole('switch')).toBeNull();
    expect(screen.getAllByRole('status', { name: 'Загрузка данных' }).length).toBeGreaterThan(0);

    release();
    expect(await screen.findByRole('switch', { name: 'Наследовать доступ' })).toBeChecked();
    expect(screen.queryByText(/Наследовать доступ: /)).toBeNull();
  });

  it('still shows a reader the fact at once: the parent’s read decides nothing for them', async () => {
    const release = holdAreaRead(ids.platformArea);
    openPage({ path: NODE_PATH, role: 'viewer' });

    expect(await screen.findByText('Наследовать доступ: Включено')).toBeVisible();
    release();
  });
});
