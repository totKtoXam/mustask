// What the tests of «Участники» and of the team members share (mod-016
// plan-frontend § 3.8): the dataset served with its memberships, the page
// rendered as the caller in a given role, and the few queries every test makes
// of the table and its row menus.
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { serveOrganizations, type OrganizationsTestApi } from './testApi';
import { ids, type Dataset } from './testFixtures';
import { ANNA, membersDataset } from './testMembersData';
import { serveMemberships } from './testMembership';
import type { MembersApi, MembersOptions } from './testMembers';
import { contextSource, renderPage } from './testRender';
import type { TestRole } from '../../test/organizationRoles';
import type { WorkContextSource } from '../../shared/lib/workContext';

/** «Участники» of «Альфа». */
export const MEMBERS_PATH = `/organizations/${ids.alfa}/settings/members`;

/** The team page of «Платформа» in «Пример проекта» of «Альфа». */
export const PLATFORM_TEAM_PATH = `/organizations/${ids.alfa}/projects/${ids.prm}/settings/teams/${ids.platformTeam}`;

export interface OpenedPage extends ReturnType<typeof renderPage> {
  data: Dataset;
  organizations: OrganizationsTestApi;
  members: MembersApi;
}

/**
 * Serves the dataset and renders `path` as Анна Смирнова, whose role in
 * «Альфа» is `role`: the dataset's membership and the page's check agree. The
 * switcher lists «Альфа» and «Бета» and has «Альфа» selected.
 */
export function openPage({
  path = MEMBERS_PATH,
  role = 'admin',
  data = membersDataset(),
  clock,
  strict,
  context = contextSource({
    organizations: [
      { id: ids.alfa, name: 'Альфа', projects: [] },
      { id: ids.beta, name: 'Бета', projects: [] },
    ],
    selection: { organizationId: ids.alfa },
  }),
}: {
  path?: string;
  role?: TestRole;
  data?: Dataset;
  clock?: MembersOptions['now'];
  strict?: boolean;
  /** The switcher's state; default: «Альфа» selected, «Бета» listed beside it. */
  context?: WorkContextSource;
} = {}): OpenedPage {
  const mine = data.memberships?.find(
    (membership) => membership.organizationId === ids.alfa && membership.userId === ANNA,
  );
  if (mine) {
    mine.role = role;
  }
  const organizations = serveOrganizations(data);
  const members = serveMemberships(data, { now: clock });
  const page = renderPage({
    path,
    role,
    subject: ANNA,
    strict,
    context,
  });
  return { ...page, data, organizations, members };
}

/** The table of «Участники»; found even while an open dialog hides the page from the accessibility tree. */
export async function membersTable() {
  return screen.findByRole('table', { name: 'Участники организации', hidden: true });
}

/**
 * The body rows of a table, each as its cells' text. Read from the markup, so
 * it also works while a dialog hides the page from the accessibility tree.
 */
export function rowsOf(table: HTMLElement): string[][] {
  return Array.from(table.querySelectorAll('tbody tr')).map((row) =>
    Array.from(row.querySelectorAll('td')).map((cell) => cell.textContent ?? ''),
  );
}

export async function openRowMenu(name: string) {
  await userEvent.click(screen.getByRole('button', { name: `Действия: ${name}` }));
  return screen.findByRole('menu');
}

/** Chooses `item` in the open row menu. */
export async function chooseRowAction(name: string, item: string) {
  const menu = await openRowMenu(name);
  await userEvent.click(within(menu).getByRole('menuitem', { name: item }));
}

/** Types `text` into a user search; the list is left open on its lines. */
export async function searchUsers(dialog: HTMLElement, label: string, text: string) {
  const field = within(dialog).getByRole('combobox', { name: label });
  await userEvent.click(field);
  await userEvent.type(field, text);
  return field;
}

/** Searches `text` and chooses the candidate whose line matches `option`. */
export async function pickUser(dialog: HTMLElement, label: string, text: string, option: RegExp) {
  const field = await searchUsers(dialog, label, text);
  await userEvent.click(await screen.findByRole('option', { name: option }));
  return field;
}

/** Chooses `role` in the dialog's «Роль» select. */
export async function chooseRole(dialog: HTMLElement, role: string) {
  await userEvent.click(within(dialog).getByRole('combobox', { name: 'Роль' }));
  await userEvent.click(await screen.findByRole('option', { name: role }));
}

/** The lines the open candidate list shows, as text. */
export function listedLines(): string[] {
  return screen.getAllByRole('option').map((option) => option.textContent ?? '');
}
