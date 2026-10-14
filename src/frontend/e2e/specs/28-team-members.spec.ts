import { expect, test, type Page } from '@playwright/test';
import { signIn } from '../support/auth';
import { env } from '../support/env';
import { deleteEphemeralUsers, setUserEnabled, type EphemeralUser } from '../support/keycloakAdmin';
import {
  addEphemeralMember,
  addTeamMember,
  awaitMemberState,
  createDirectoryUser,
  fixtureMember,
  members,
  removeTeamMember,
  teamMembers,
  teamPath,
} from '../support/membersApi';
import {
  backlogAddress,
  boardAddress,
  chooseRowAction,
  chooseUser,
  expectFieldRefusal,
  heading,
  main,
  mainNavigation,
  notice,
  optionWith,
  rowOf,
  searchAnswered,
  switcher,
  teamAddress,
  teamMembersTable,
  userField,
} from '../support/membersUi';
import {
  createContext,
  createProject,
  deleteTeam,
  runKey,
  runName,
  send,
  teams,
} from '../support/organizationsApi';
import { DESKTOP, openSession } from '../support/sessions';

/**
 * 28. Team members (mod-016 US-3, US-7) against the real backend, real
 * Keycloak and the real user projection.
 *
 * Scenarios, mapped to stories:
 * 28.1 an organization member is added to a team and listed with the e-mail,
 *      the organization role unchanged; a disabled member and a non-member are
 *      not offered (US-3 add, only active members)
 * 28.2 a second add is refused «Пользователь уже состоит в команде» on the
 *      field; the user is listed once (US-3 once)
 * 28.3 «Исключить из команды» acts at once with its notice; the user still
 *      reads the project; a removal already made over the API shows «Участник
 *      команды не найден.» and the table is read again (US-3 remove, already
 *      gone)
 * 28.4 an empty team: the administrator's hint and action, the others' hint and
 *      no action; the empty frame is an `h3` under the section's `h2` (US-3 "A
 *      team with no members says so")
 * 28.5 a project created through the API lists its creator as the first team's
 *      only member; deleting a team ends its memberships and leaves the other
 *      team's (US-3 creator, deleted team)
 * 28.6 the default team: the user's own team after choosing a project; the
 *      first of several in the project's order; none → the project's first;
 *      a remembered team that exists is kept; leaving a team changes the
 *      default at the next read (US-7)
 *
 * US-3's races (an add against a removal from the organization, an add against a
 * team's deletion) are the backend's integration tests; they have no browser
 * surface.
 *
 * Every case creates what it needs the way specs 21 and 22 do — an organization
 * with a project through the API, created by the fixture user, who is therefore
 * its administrator (D-3) and the only member of the project's first team
 * (BR-M1-25) — and `afterAll` deactivates each organization it created and
 * deletes each throwaway user. The team page is at `teamAddress` of
 * `membersUi.ts` (feat-028 may move it, design § 15 item 11).
 *
 * Nothing is injected and nothing on the session flow is stubbed
 * (`src/test/e2eSessionGuard.test.ts`).
 */

test.describe.configure({ timeout: 300_000 });

const created: string[] = [];
const throwaway: EphemeralUser[] = [];

/** The fixture user's display name (`KeycloakStandProvisioner`: «E2E» «Fixture»). */
const FIXTURE_NAME = 'E2E Fixture';

/** An organization with «Пример проекта» and the extra teams named, through the API. */
async function fixtureOrganization(page: Page, extraTeams: readonly string[] = []) {
  const context = await createContext(page.request, {
    organization: runName('Организация E2E'),
    project: 'Пример проекта',
    teams: extraTeams,
  });
  created.push(context.organization.id);
  const firstTeam = context.teams.find((team) => team.isFirstTeam);
  if (!firstTeam) throw new Error('The project has no first team.');
  const named = (name: string) => {
    const found = context.teams.find((team) => team.name === name);
    if (!found) throw new Error(`The project has no team «${name}».`);
    return found;
  };
  return { ...context, firstTeam, named };
}

/** The team page, waited for: its `h1`, the section's `h2` and a loaded section. */
async function openTeam(
  page: Page,
  organizationId: string,
  projectId: string,
  team: { id: string; name: string },
): Promise<void> {
  await page.goto(teamAddress(organizationId, projectId, team.id));
  await expect(heading(page, `Команда: ${team.name}`)).toBeVisible();
  await expect(page.getByRole('heading', { level: 2, name: 'Участники команды' })).toBeVisible();
}

/** The section's command, the first of two when an empty team draws it in its frame too. */
const addToTeam = (page: Page) => page.getByRole('button', { name: 'Добавить в команду' });

test.describe('28. team members (mod-016)', () => {
  test.use({ viewport: DESKTOP });

  test.afterAll(async ({ browser }) => {
    const { keycloakUrl, realm } = env();
    try {
      if (created.length > 0) {
        const session = await openSession(browser);
        try {
          for (const id of created) {
            const response = await send(
              session.page.request,
              'POST',
              `/api/organizations/${id}/deactivate`,
            );
            if (!response.ok()) {
              throw new Error(`Could not deactivate ${id}: HTTP ${response.status()}`);
            }
          }
        } finally {
          await session.close();
        }
      }
    } finally {
      await deleteEphemeralUsers(keycloakUrl, realm, throwaway);
    }
  });

  test('28.1 an organization member is added to a team and listed with the e-mail, the organization role unchanged; a disabled member and a non-member are not offered (US-3)', async ({
    page,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const { organization, project, firstTeam } = await fixtureOrganization(page);
    const { keycloakUrl, realm } = env();
    const active = await addEphemeralMember(page.request, organization.id, 'Анна', 'member');
    const disabled = await addEphemeralMember(
      page.request,
      organization.id,
      'Отключённый',
      'member',
    );
    const outsider = await createDirectoryUser(page.request, organization.id, 'Посторонний');
    throwaway.push(active, disabled, outsider);
    await setUserEnabled(keycloakUrl, realm, disabled.id, false);
    await awaitMemberState(page.request, organization.id, disabled.id, 'inactive');

    await openTeam(page, organization.id, project.id, firstTeam);
    // The creator is the first team's only member (BR-M1-25).
    await expect(teamMembersTable(page)).toBeVisible();
    await expect(rowOf(teamMembersTable(page), FIXTURE_NAME)).toHaveCount(1);

    await addToTeam(page).first().click();
    const dialog = page.getByRole('dialog', { name: 'Добавление в команду' });
    const field = userField(dialog, 'Участник организации');
    await expect(dialog.getByText('Поиск среди активных участников организации')).toBeVisible();
    await expect(dialog.getByRole('button', { name: 'Добавить', exact: true })).toBeDisabled();

    // A member whose account is disabled, and a user of no organization here: nobody.
    await searchAnswered(page, field, disabled.token);
    await expect(optionWith(page, 'Пользователи не найдены')).toBeVisible();
    await expect(optionWith(page, disabled.displayName)).toHaveCount(0);
    await searchAnswered(page, field, outsider.token);
    await expect(optionWith(page, 'Пользователи не найдены')).toBeVisible();
    await expect(optionWith(page, outsider.displayName)).toHaveCount(0);

    // An active member of the organization: offered, chosen, added.
    await chooseUser(page, field, active);
    await dialog.getByRole('button', { name: 'Добавить', exact: true }).click();
    await expect(dialog).toHaveCount(0);
    await expect(
      notice(page, `Пользователь «${active.displayName}» добавлен в команду «${firstTeam.name}».`),
    ).toBeVisible();
    const table = teamMembersTable(page);
    const row = rowOf(table, active.displayName);
    await expect(row).toHaveCount(1);
    await expect(row).toContainText(active.email);
    await expect(addToTeam(page).first()).toBeFocused();

    // Team membership grants no access and changes no role (doc-006 § Roles).
    const roles = (await members(page.request, organization.id)).find(
      (member) => member.user.id === active.id,
    );
    expect(roles?.role).toBe('member');
  });

  test('28.2 a second add is refused «Пользователь уже состоит в команде» on the field; the user stays listed once (US-3)', async ({
    page,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const { organization, project, firstTeam } = await fixtureOrganization(page);
    const member = await addEphemeralMember(page.request, organization.id, 'Анна', 'member');
    throwaway.push(member);
    await addTeamMember(page.request, organization.id, project.id, firstTeam.id, member.id);

    await openTeam(page, organization.id, project.id, firstTeam);
    await expect(rowOf(teamMembersTable(page), member.displayName)).toHaveCount(1);

    // The user is offered again (the refusal comes on «Добавить», design § 4.4)…
    await addToTeam(page).first().click();
    const dialog = page.getByRole('dialog', { name: 'Добавление в команду' });
    const field = userField(dialog, 'Участник организации');
    await chooseUser(page, field, member);
    await dialog.getByRole('button', { name: 'Добавить', exact: true }).click();

    // …and refused on the field, which keeps the chosen user; the dialog stays.
    await expectFieldRefusal(dialog, field, 'Пользователь уже состоит в команде');
    await expect(field).toHaveValue(member.displayName);
    await expect(dialog).toBeVisible();
    await dialog.getByRole('button', { name: 'Отмена' }).click();
    await expect(dialog).toHaveCount(0);

    await expect(rowOf(teamMembersTable(page), member.displayName)).toHaveCount(1);
    const listed = await teamMembers(page.request, organization.id, project.id, firstTeam.id);
    expect(listed.filter((user) => user.id === member.id)).toHaveLength(1);
  });

  test('28.3 «Исключить из команды» acts at once with its notice, the user still reads the project; a removal already made over the API shows «Участник команды не найден.» and the table is read again (US-3)', async ({
    page,
    browser,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const { organization, project, firstTeam } = await fixtureOrganization(page);
    const kept = await addEphemeralMember(page.request, organization.id, 'Анна', 'member');
    const gone = await addEphemeralMember(page.request, organization.id, 'Олег', 'member');
    throwaway.push(kept, gone);
    await addTeamMember(page.request, organization.id, project.id, firstTeam.id, kept.id);
    await addTeamMember(page.request, organization.id, project.id, firstTeam.id, gone.id);

    await openTeam(page, organization.id, project.id, firstTeam);
    const table = teamMembersTable(page);
    await expect(rowOf(table, kept.displayName)).toHaveCount(1);
    await expect(rowOf(table, gone.displayName)).toHaveCount(1);

    // At once: no dialog, the row goes, the notice names user and team.
    await chooseRowAction(page, table, kept.displayName, 'Исключить из команды');
    await expect(page.getByRole('dialog')).toHaveCount(0);
    await expect(rowOf(table, kept.displayName)).toHaveCount(0);
    await expect(
      notice(page, `Пользователь «${kept.displayName}» исключён из команды «${firstTeam.name}».`),
    ).toBeVisible();

    // Out of the team, not out of the project: the backlog and the board still open (US-3).
    const keptSession = await openSession(browser, {
      username: kept.username,
      returnPath: backlogAddress(organization.id, project.id, firstTeam.id),
    });
    try {
      await expect(heading(keptSession.page, firstTeam.name)).toBeVisible();
      await expect(
        main(keptSession.page).getByRole('heading', { level: 2, name: 'Бэклог пуст' }),
      ).toBeVisible();
      await keptSession.page.goto(boardAddress(organization.id, project.id, firstTeam.id));
      await expect(
        main(keptSession.page).getByRole('heading', { name: 'Канбан пуст' }),
      ).toBeVisible();
    } finally {
      await keptSession.close();
    }
    expect(
      (await members(page.request, organization.id)).find((member) => member.user.id === kept.id)
        ?.role,
    ).toBe('member');

    // Another administrator removed the other user behind the open page.
    await removeTeamMember(page.request, organization.id, project.id, firstTeam.id, gone.id);
    await chooseRowAction(page, table, gone.displayName, 'Исключить из команды');
    await expect(page.getByText('Участник команды не найден.', { exact: true })).toBeVisible();
    // The table is read again: the row is not there.
    await expect(rowOf(table, gone.displayName)).toHaveCount(0);
  });

  test('28.4 an empty team: the administrator sees the hint and the action, a reader the other hint and no action; the empty frame is an h3 under the section’s h2 (US-3)', async ({
    page,
    browser,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const { organization, project, named } = await fixtureOrganization(page, ['Аналитика']);
    const empty = named('Аналитика');
    const reader = await addEphemeralMember(page.request, organization.id, 'Читатель', 'viewer');
    throwaway.push(reader);

    await openTeam(page, organization.id, project.id, empty);
    const emptyFrame = page.getByRole('heading', {
      level: 3,
      name: 'В команде пока нет участников',
    });
    await expect(emptyFrame).toBeVisible();
    await expect(
      page.getByText('Добавьте участников организации кнопкой «Добавить в команду».', {
        exact: true,
      }),
    ).toBeVisible();
    // The command in the section's header, and the same action in the frame (design § 6.3).
    await expect(addToTeam(page)).toHaveCount(2);

    const readerSession = await openSession(browser, {
      username: reader.username,
      returnPath: teamAddress(organization.id, project.id, empty.id),
    });
    try {
      const readerPage = readerSession.page;
      await expect(
        readerPage.getByRole('heading', { level: 2, name: 'Участники команды' }),
      ).toBeVisible();
      await expect(
        readerPage.getByRole('heading', { level: 3, name: 'В команде пока нет участников' }),
      ).toBeVisible();
      await expect(
        readerPage.getByText('Состав команды задаёт администратор организации.', { exact: true }),
      ).toBeVisible();
      await expect(addToTeam(readerPage)).toHaveCount(0);
    } finally {
      await readerSession.close();
    }
  });

  test('28.5 a project created through the API lists its creator as the first team’s only member; deleting a team ends its memberships and leaves the other team’s (US-3)', async ({
    page,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const { organization, project, firstTeam, named } = await fixtureOrganization(page, [
      'Аналитика',
      'Платформа',
    ]);
    const analytics = named('Аналитика');
    const platform = named('Платформа');
    const fixtureId = (await fixtureMember(page.request, organization.id)).user.id;

    // BR-M1-25: the creator, whatever the projection says, and nobody else.
    const first = await teamMembers(page.request, organization.id, project.id, firstTeam.id);
    expect(first.map((user) => user.id)).toEqual([fixtureId]);
    // The list read says which teams the caller is in (api-contract § 3.3).
    const listed = await teams(page.request, organization.id, project.id);
    expect(listed.map((team) => [team.name, team.isMember])).toEqual([
      [firstTeam.name, true],
      [analytics.name, false],
      [platform.name, false],
    ]);

    // A member of two teams: the deletion of one ends that membership alone.
    const member = await addEphemeralMember(page.request, organization.id, 'Анна', 'member');
    throwaway.push(member);
    await addTeamMember(page.request, organization.id, project.id, analytics.id, member.id);
    await addTeamMember(page.request, organization.id, project.id, platform.id, member.id);
    await deleteTeam(page.request, organization.id, project.id, analytics.id);

    expect(
      (await teamMembers(page.request, organization.id, project.id, platform.id)).map(
        (user) => user.id,
      ),
    ).toEqual([member.id]);
    const gone = await send(
      page.request,
      'GET',
      `${teamPath(organization.id, project.id, analytics.id)}/members`,
    );
    expect(gone.status()).toBe(404);
    expect(((await gone.json()) as { errorCode: string }).errorCode).toBe(
      'organizations.team.not_found',
    );
  });

  test('28.6 the default team: the user’s own after choosing a project, the first of several, none → the project’s first, a remembered team kept, leaving a team changes the default at the next read (US-7)', async ({
    page,
    browser,
  }) => {
    await signIn(page, { returnPath: '/home' });
    // «Пример проекта» sorts before «Ярлыки», so it is the first project the
    // switcher takes; its teams read «Пример проекта» (the first), «Аналитика»,
    // «Платформа» in the project's order (mod-002 § 3.1).
    const { organization, project, firstTeam, named } = await fixtureOrganization(page, [
      'Аналитика',
      'Платформа',
    ]);
    const analytics = named('Аналитика');
    const platform = named('Платформа');
    const second = await createProject(page.request, organization.id, {
      name: 'Ярлыки',
      key: runKey('Y'),
    });
    const [secondFirst] = await teams(page.request, organization.id, second.id);
    if (!secondFirst) throw new Error('The second project has no team.');

    const solo = await addEphemeralMember(page.request, organization.id, 'Платформенный', 'member');
    const double = await addEphemeralMember(page.request, organization.id, 'Двойной', 'member');
    const nobody = await addEphemeralMember(page.request, organization.id, 'Без команды', 'member');
    throwaway.push(solo, double, nobody);
    await addTeamMember(page.request, organization.id, project.id, platform.id, solo.id);
    await addTeamMember(page.request, organization.id, project.id, analytics.id, double.id);
    await addTeamMember(page.request, organization.id, project.id, platform.id, double.id);

    const teamLevel = (target: Page) => switcher(target).getByRole('button', { name: /^Команда:/ });
    const projectLevel = (target: Page) =>
      switcher(target).getByRole('button', { name: /^Проект:/ });
    const chooseProject = async (target: Page, name: string) => {
      await projectLevel(target).click();
      await target.getByRole('menuitemradio', { name, exact: true }).click();
    };

    await test.step('the user’s own team: the first load, then a project chosen again; «Бэклоги» opens it', async () => {
      const session = await openSession(browser, { username: solo.username });
      try {
        const target = session.page;
        await expect(teamLevel(target)).toHaveAttribute('aria-label', `Команда: ${platform.name}`);
        await chooseProject(target, second.name);
        await expect(teamLevel(target)).toHaveAttribute(
          'aria-label',
          `Команда: ${secondFirst.name}`,
        );
        await chooseProject(target, project.name);
        await expect(teamLevel(target)).toHaveAttribute('aria-label', `Команда: ${platform.name}`);
        await mainNavigation(target).getByRole('link', { name: 'Бэклоги' }).click();
        await expect(target).toHaveURL(new RegExp(`/teams/${platform.id}/backlogs/`));
        await expect(heading(target, platform.name)).toBeVisible();
      } finally {
        await session.close();
      }
    });

    await test.step('several teams: the first of them in the project’s order', async () => {
      const session = await openSession(browser, { username: double.username });
      try {
        await expect(teamLevel(session.page)).toHaveAttribute(
          'aria-label',
          `Команда: ${analytics.name}`,
        );
      } finally {
        await session.close();
      }
    });

    await test.step('no team of the project: the project’s first team, as before', async () => {
      const session = await openSession(browser, { username: nobody.username });
      try {
        await expect(teamLevel(session.page)).toHaveAttribute(
          'aria-label',
          `Команда: ${firstTeam.name}`,
        );
      } finally {
        await session.close();
      }
    });

    await test.step('a remembered team that exists is kept, whoever the user is a member of', async () => {
      const session = await openSession(browser, { username: solo.username });
      try {
        const target = session.page;
        await expect(teamLevel(target)).toHaveAttribute('aria-label', `Команда: ${platform.name}`);
        await teamLevel(target).click();
        await target.getByRole('menuitemradio', { name: analytics.name, exact: true }).click();
        await expect(teamLevel(target)).toHaveAttribute('aria-label', `Команда: ${analytics.name}`);
        await target.reload();
        await expect(teamLevel(target)).toHaveAttribute('aria-label', `Команда: ${analytics.name}`);
      } finally {
        await session.close();
      }
    });

    await test.step('leaving a team changes the default at the next read of the list', async () => {
      const session = await openSession(browser, { username: solo.username });
      try {
        const target = session.page;
        await expect(teamLevel(target)).toHaveAttribute('aria-label', `Команда: ${platform.name}`);
        // An administrator takes the user out of «Платформа»; the page is not told.
        await removeTeamMember(page.request, organization.id, project.id, platform.id, solo.id);
        // A default is not a choice: this context chose nothing and so remembers
        // nothing (the switcher writes a selection on a choice, an address or a
        // fallback only), and a reload reads the project's teams again. The
        // default comes from that list — the user is in none of its teams now,
        // so the project's first (the previous step's team is kept because the
        // user chose it, BR-M1-24)…
        await target.reload();
        await expect(teamLevel(target)).toHaveAttribute('aria-label', `Команда: ${firstTeam.name}`);
        // …and so it is for a project chosen again, from the list read for it.
        await chooseProject(target, second.name);
        await expect(teamLevel(target)).toHaveAttribute(
          'aria-label',
          `Команда: ${secondFirst.name}`,
        );
        await chooseProject(target, project.name);
        await expect(teamLevel(target)).toHaveAttribute('aria-label', `Команда: ${firstTeam.name}`);
      } finally {
        await session.close();
      }
    });
  });
});
