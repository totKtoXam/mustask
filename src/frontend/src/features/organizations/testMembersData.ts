// The people of the membership tests (mod-016 plan-frontend § 3.8): the
// stories' dataset with a projection of users, the memberships of «Альфа» and
// the teams' members, so «Участники» and the team page meet every kind of
// row — an administrator, a member the account of whom is disabled, a reader
// the projection does not know — and every kind of candidate.
import { exampleDataset, ids, type Dataset, type UserRecord } from './testFixtures';

/** The caller: an administrator of «Альфа», who the token's `sub` names. */
export const ANNA = 'user-acting';

export const people = {
  anna: { id: ANNA, displayName: 'Анна Смирнова', email: 'anna@example.org', state: 'active' },
  ivan: { id: 'user-ivan', displayName: 'Иван Петров', email: 'ivan@example.org', state: 'active' },
  oleg: {
    id: 'user-oleg',
    displayName: 'Олег Иванов',
    email: 'oleg@example.org',
    state: 'inactive',
  },
  maria: {
    id: 'user-maria',
    displayName: 'Мария Орлова',
    email: 'maria@example.org',
    state: 'active',
  },
  mark: { id: 'user-mark', displayName: 'Марк Орлов', email: '', state: 'active' },
  boris: {
    id: 'user-boris',
    displayName: 'Борис Отключённый',
    email: 'boris@example.org',
    state: 'inactive',
  },
} as const satisfies Record<string, UserRecord>;

/** The id of a member the projection has no record of. */
export const GHOST = 'user-ghost';

/**
 * «Альфа» as «Участники» shows it, in the server's order: Анна Смирнова
 * (administrator, the caller), Иван Петров (administrator), Олег Иванов
 * (member, inactive, whose role Анна changed), then the user the projection
 * does not know (reader). Мария Орлова, Марк Орлов (no e-mail) and
 * Борис Отключённый (inactive) are in the directory and in no organization.
 * «Платформа» of «Пример проекта» has Иван Петров and Олег Иванов as members.
 */
export function membersDataset(): Dataset {
  const data = exampleDataset();
  data.actingUserId = ANNA;
  data.users = Object.values(people).map((person) => ({ ...person }));
  data.memberships = [
    {
      organizationId: ids.alfa,
      userId: ANNA,
      role: 'admin',
      addedBy: people.ivan.id,
      addedAt: '2026-10-01T10:00:00+00:00',
    },
    {
      organizationId: ids.alfa,
      userId: people.ivan.id,
      role: 'admin',
      addedBy: ANNA,
      addedAt: '2026-10-01T10:05:00+00:00',
    },
    {
      organizationId: ids.alfa,
      userId: people.oleg.id,
      role: 'member',
      addedBy: ANNA,
      addedAt: '2026-10-02T09:15:00+00:00',
      roleChangedBy: ANNA,
      roleChangedAt: '2026-10-05T14:30:00+00:00',
    },
    {
      organizationId: ids.alfa,
      userId: GHOST,
      role: 'viewer',
      addedBy: ANNA,
      addedAt: '2026-10-03T11:00:00+00:00',
    },
    // «Бета»: the caller is a reader there.
    {
      organizationId: ids.beta,
      userId: ANNA,
      role: 'viewer',
      addedBy: people.ivan.id,
      addedAt: '2026-10-01T10:00:00+00:00',
    },
  ];
  data.teamMembers = [
    { teamId: ids.platformTeam, userId: people.ivan.id },
    { teamId: ids.platformTeam, userId: people.oleg.id },
  ];
  return data;
}
