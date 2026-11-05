import { randomUUID } from 'node:crypto';
import { expect, test, type Page } from '@playwright/test';
import { signIn } from '../support/auth';
import { env } from '../support/env';
import {
  createEphemeralUserWithoutGroup,
  deleteEphemeralUsers,
  setUserEnabled,
  type EphemeralUser,
} from '../support/keycloakAdmin';
import {
  accessOf,
  addEphemeralMember,
  addMember,
  addTeamMember,
  assigneeOf,
  awaitMemberState,
  awaitNotOffered,
  changeMemberRole,
  createDirectoryUser,
  fixtureMember,
  members,
  removeMember,
  teamMembers,
  workItemsPath,
} from '../support/membersApi';
import {
  anyRowActions,
  awaitLoaded,
  cellOf,
  chooseRowAction,
  chooseUser,
  escapeForRegExp,
  expectFieldRefusal,
  heading,
  mainNavigation,
  membersAddress,
  membersTable,
  notice,
  optionWith,
  organizationGeneralAddress,
  pageScrollsSideways,
  projectGeneralAddress,
  roleField,
  rowActions,
  rowOf,
  searchFor,
  settingsNavigation,
  todayText,
  userField,
  workItemsAddress,
} from '../support/membersUi';
import {
  activeOrganizations,
  createContext,
  deactivateOrganization,
  runName,
  send,
  teams,
} from '../support/organizationsApi';
import { refocus } from '../support/refocus';
import { DESKTOP, openSession } from '../support/sessions';
import { createWorkItem, saveWorkItem, typeId } from '../support/workItemsApi';

/**
 * 27. Organization membership (mod-016 US-1, US-2, US-4, US-5, US-6, US-8)
 * against the real backend, real Keycloak and the real user projection.
 *
 * Scenarios, mapped to stories:
 * 27.1 the administrator adds a found user as «Участник»; the added user sees
 *      the organization after a refocus, no re-login (US-1 add; US-2)
 * 27.2 the search: one character, no match, a disabled user, a current member
 *      refused on the field; a disabled or unknown user is refused by the API
 *      (US-1 search, duplicate, inactive)
 * 27.3 a role change from the row menu; the user's next write is 403
 *      «Недостаточно прав» (US-1 role change)
 * 27.4 the removal: the confirmation verbatim, «Отмена», «Удалить»; the user
 *      leaves the team and keeps the assignment; their next request is 404 and
 *      their open page turns into «Организация не найдена» (US-1 removal;
 *      US-4 removed member; US-5 an assignee who leaves stays)
 * 27.5 the last administrator: refused alone; stepping down with a second
 *      administrator hides the controls with no reload; removing oneself lands
 *      on «Организации» (US-1 last admin, self-removal; US-4 stepping down)
 * 27.6 a membership already gone: «Участник не найден.», the list is read again
 *      (US-1 "Removing a membership that is already gone")
 * 27.7 a disabled member is listed «неактивен», in «Имя» and in the «Добавлен»
 *      of the rows that name them, and keeps the role (US-1 inactive)
 * 27.8 a user in no group: «Организация не найдена» at every depth, the empty
 *      list, «Настройки» opens «Языки» (US-2; US-6 the non-members of the
 *      existing data; US-8 a user in no group reads the dictionaries)
 * 27.9 the three roles see «Общие», «Проекты», «Участники», «Процесс» in that
 *      order; the reader sees the table without the controls; the served
 *      bundles hold what the contract's matrix says (US-4)
 * 27.10 the keyboard and the 640 px width of «Участники» (US-1; design § 10, § 11)
 *
 * US-6's migration seeding and US-8's live-realm command have no browser
 * surface; the orchestrator runs them (status.md D-19).
 *
 * Every case creates what it needs the way specs 21 and 22 do — an
 * organization with a project through the API; the fixture user creates it, so
 * it is the organization's administrator (D-3) — and `afterAll` deactivates each
 * organization it created and deletes each throwaway user: nothing is ever
 * purged (BR-M1-1), so the stand is left as it was found. The throwaway users
 * (`keycloakAdmin.ts`) are created in Keycloak, waited for in the user
 * projection (it is fed by Keycloak's event listener, ADR-0025, so up to a
 * minute) and added through the API, except where the case adds them in the
 * panel.
 *
 * Nothing is injected and nothing on the session flow is stubbed
 * (`src/test/e2eSessionGuard.test.ts`).
 */

test.describe.configure({ timeout: 240_000 });

const created: string[] = [];
const throwaway: EphemeralUser[] = [];

const ROLE_LABELS = { admin: 'Администратор', member: 'Участник', viewer: 'Читатель' } as const;

/** api-contract § 0.2: the codes every role holds. */
const READ_CODES = [
  'organizations.organization.read',
  'organizations.membership.read',
  'organizations.project.read',
  'organizations.team.read',
  'organizations.area.read',
  'process.definition.read',
  'workitems.workitem.read',
];
/** api-contract § 0.2: what a member holds beyond the read codes. */
const MEMBER_CODES = ['workitems.workitem.write', 'workitems.link.write', 'boards.backlog.reorder'];
/** api-contract § 0.2: what only an administrator holds. */
const ADMIN_CODES = [
  'organizations.organization.write',
  'organizations.organization.deactivate',
  'organizations.membership.write',
  'organizations.project.write',
  'organizations.project.deactivate',
  'organizations.team.write',
  'organizations.area.write',
];

/** The fixture user's display name (`KeycloakStandProvisioner`: «E2E» «Fixture»). */
const FIXTURE_NAME = 'E2E Fixture';

/** An organization with «Пример проекта» created by the fixture user, who administers it. */
async function fixtureOrganization(page: Page) {
  const context = await createContext(page.request, {
    organization: runName('Организация E2E'),
    project: 'Пример проекта',
  });
  created.push(context.organization.id);
  return context;
}

/** Opens «Участники» of an organization as whoever `page` is, and waits for its rows. */
async function openMembers(page: Page, organizationId: string): Promise<void> {
  await page.goto(membersAddress(organizationId));
  await expect(heading(page, 'Участники')).toBeVisible();
  await expect(membersTable(page)).toBeVisible();
  await expect(rowOf(membersTable(page), FIXTURE_NAME)).toBeVisible();
}

test.describe('27. organization members (mod-016)', () => {
  test.use({ viewport: DESKTOP });

  test.afterAll(async ({ browser }) => {
    const { keycloakUrl, realm } = env();
    try {
      if (created.length > 0) {
        const session = await openSession(browser);
        try {
          for (const id of created) {
            // A 404 is not a failure: 27.5 ends with the fixture user out of an
            // organization its second administrator has already deactivated.
            const response = await send(
              session.page.request,
              'POST',
              `/api/organizations/${id}/deactivate`,
            );
            if (!response.ok() && response.status() !== 404) {
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

  test('27.1 happy path: an administrator adds a found user as «Участник»; the user sees the organization after a refocus, with no re-login (US-1, US-2)', async ({
    page,
    browser,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const { organization, project } = await fixtureOrganization(page);
    const anna = await createDirectoryUser(page.request, organization.id, 'Анна');
    throwaway.push(anna);

    // The user is already signed in, on the organization list, and belongs to
    // nothing yet (a user in no group has no create right either).
    const annaSession = await openSession(browser, {
      username: anna.username,
      returnPath: '/organizations',
    });
    try {
      const annaPage = annaSession.page;
      await expect(heading(annaPage, 'Организации')).toBeVisible();
      await expect(
        annaPage.getByRole('heading', {
          level: 2,
          name: 'Вы пока не состоите ни в одной организации',
        }),
      ).toBeVisible();

      // «Добавить участника»: the search, the chosen user, «Роль» left at «Участник».
      await openMembers(page, organization.id);
      await page.getByRole('button', { name: 'Добавить участника' }).click();
      const dialog = page.getByRole('dialog', { name: 'Добавление участника' });
      await chooseUser(page, userField(dialog, 'Пользователь'), anna);
      await expect(roleField(dialog)).toContainText(ROLE_LABELS.member);
      await dialog.getByRole('button', { name: 'Добавить', exact: true }).click();

      await expect(dialog).toHaveCount(0);
      await expect(
        notice(page, `Пользователь «${anna.displayName}» добавлен в организацию.`),
      ).toBeVisible();
      const table = membersTable(page);
      const row = rowOf(table, anna.displayName);
      await expect(row).toHaveCount(1);
      await expect(await cellOf(table, row, 'Роль')).toHaveText(ROLE_LABELS.member);
      await expect(await cellOf(table, row, 'Почта')).toHaveText(anna.email);
      const added = await cellOf(table, row, 'Добавлен');
      await expect(added).toContainText(FIXTURE_NAME);
      await expect(added).toContainText(await todayText(page));
      await expect(await cellOf(table, row, 'Роль изменена')).toContainText('—');
      await expect(page.getByRole('button', { name: 'Добавить участника' })).toBeFocused();

      // Her next request, no re-login: the window returns to her, the list is
      // read again, the organization is in it and its projects open (BR-M1-6).
      await refocus(annaPage);
      const link = annaPage.getByRole('link', { name: organization.name });
      await expect(link).toBeVisible();
      await link.click();
      await expect(annaPage.getByRole('table', { name: 'Активные проекты' })).toContainText(
        project.name,
      );
      // A «Участник» does not create projects (US-4): the table is there, the command is not.
      await expect(annaPage.getByRole('button', { name: 'Создать проект' })).toHaveCount(0);
    } finally {
      await annaSession.close();
    }
  });

  test('27.2 the search: one character sends nothing, no match, a disabled user is not offered, a current member is offered and refused; the API refuses a disabled or unknown user (US-1)', async ({
    page,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const { organization } = await fixtureOrganization(page);
    const { keycloakUrl, realm } = env();
    const disabled = await createDirectoryUser(page.request, organization.id, 'Отключённый');
    throwaway.push(disabled);
    await setUserEnabled(keycloakUrl, realm, disabled.id, false);
    await awaitNotOffered(page.request, organization.id, disabled);
    const present = await addEphemeralMember(page.request, organization.id, 'Участник', 'member');
    throwaway.push(present);

    await openMembers(page, organization.id);
    await page.getByRole('button', { name: 'Добавить участника' }).click();
    const dialog = page.getByRole('dialog', { name: 'Добавление участника' });
    const field = userField(dialog, 'Пользователь');
    await expect(dialog.getByRole('button', { name: 'Добавить', exact: true })).toBeDisabled();

    // One character: the hint, and no search request is sent. The wait runs
    // past the 250 ms debounce of design § 3.5, so a request that was going to
    // be sent has been.
    const searches: string[] = [];
    page.on('request', (request) => {
      if (new URL(request.url()).pathname.endsWith('/member-candidates')) {
        searches.push(request.url());
      }
    });
    await searchFor(field, 'я');
    await expect(optionWith(page, 'Введите не менее 2 символов имени или почты')).toBeVisible();
    await page.waitForTimeout(600);
    expect(searches).toEqual([]);

    // Nobody matches; a disabled account is not offered either.
    await field.fill('яяяяяяяя');
    await expect(optionWith(page, 'Пользователи не найдены')).toBeVisible();
    await field.fill(disabled.token);
    // The search for the disabled user has gone out and been answered before
    // "nobody" counts: the line of the earlier search is not taken for it.
    await expect
      .poll(() => searches.some((url) => url.includes(encodeURIComponent(disabled.token))))
      .toBe(true);
    await expect(optionWith(page, 'Пользователи не найдены')).toBeVisible();
    await expect(optionWith(page, disabled.displayName)).toHaveCount(0);

    // A current member is offered (DC-9) and refused on «Добавить», on the field.
    await chooseUser(page, field, present);
    await dialog.getByRole('button', { name: 'Добавить', exact: true }).click();
    await expectFieldRefusal(dialog, field, 'Пользователь уже состоит в организации');
    await expect(dialog).toBeVisible();
    await dialog.getByRole('button', { name: 'Отмена' }).click();
    await expect(dialog).toHaveCount(0);

    // Directly through the API (BR-M1-32): a disabled user and an id the
    // projection does not know are refused with the same text, nothing is created.
    for (const userId of [disabled.id, `unknown-${randomUUID()}`]) {
      const response = await send(
        page.request,
        'POST',
        `/api/organizations/${organization.id}/members`,
        { userId, role: 'member' },
      );
      expect(response.status(), userId).toBe(409);
      const body = (await response.json()) as { errorCode: string; detail: string };
      expect(body.errorCode).toBe('organizations.membership.user_unavailable');
      expect(body.detail).toBe('Этого пользователя нельзя добавить в организацию');
    }
    const ids = (await members(page.request, organization.id)).map((member) => member.user.id);
    expect(ids).not.toContain(disabled.id);
    expect(ids).toContain(present.id);
  });

  test('27.3 a role change from the row menu: the row and «Роль изменена»; the user’s next write is 403 «Недостаточно прав» (US-1)', async ({
    page,
    browser,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const { organization, project } = await fixtureOrganization(page);
    const member = await addEphemeralMember(page.request, organization.id, 'Участник', 'member');
    throwaway.push(member);
    const type = await typeId(page.request, organization.id, 'TASK');
    const memberSession = await openSession(browser, { username: member.username });
    try {
      // A member writes work items (the member bundle of the spec's matrix): this one is created.
      await createWorkItem(memberSession.page.request, organization.id, project.id, {
        typeId: type,
        title: 'До смены роли',
      });

      await openMembers(page, organization.id);
      await chooseRowAction(page, membersTable(page), member.displayName, 'Изменить роль');
      const dialog = page.getByRole('dialog', { name: 'Изменение роли' });
      await expect(dialog.getByText(`Пользователь: ${member.displayName}`)).toBeVisible();
      await expect(roleField(dialog)).toContainText(ROLE_LABELS.member);
      await expect(
        dialog.getByText(
          'Просматривает всё и работает с рабочими элементами: создаёт, меняет, связывает и упорядочивает их.',
        ),
      ).toBeVisible();
      const save = dialog.getByRole('button', { name: 'Сохранить' });
      await expect(save).toBeDisabled();
      await roleField(dialog).click();
      await page.getByRole('option', { name: ROLE_LABELS.viewer, exact: true }).click();
      await expect(dialog.getByText('Только просматривает, ничего не меняет.')).toBeVisible();
      await expect(save).toBeEnabled();
      await save.click();

      await expect(dialog).toHaveCount(0);
      await expect(
        notice(page, `Роль пользователя «${member.displayName}» изменена.`),
      ).toBeVisible();
      const table = membersTable(page);
      const row = rowOf(table, member.displayName);
      await expect(await cellOf(table, row, 'Роль')).toHaveText(ROLE_LABELS.viewer);
      const changed = await cellOf(table, row, 'Роль изменена');
      await expect(changed).toContainText(FIXTURE_NAME);
      await expect(changed).toContainText(await todayText(page));

      // The next request of the user, no re-login: refused, in the contract's words.
      const refused = await send(
        memberSession.page.request,
        'POST',
        workItemsPath(organization.id, project.id),
        { requestId: randomUUID(), typeId: type, title: 'После смены роли' },
      );
      expect(refused.status()).toBe(403);
      const body = (await refused.json()) as { errorCode: string; detail: string };
      expect(body.errorCode).toBe('organizations.access.forbidden');
      expect(body.detail).toBe('Недостаточно прав');
      // …and what the reader's bundle holds is still read.
      const served = await accessOf(memberSession.page.request, organization.id);
      expect(served.role).toBe('viewer');
    } finally {
      await memberSession.close();
    }
  });

  test('27.4 the removal: the text verbatim, «Отмена» changes nothing, «Удалить» removes the row and the team place; the assignment stays; the user meets «Организация не найдена» (US-1, US-4, US-5)', async ({
    page,
    browser,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const { organization, project } = await fixtureOrganization(page);
    const [firstTeam] = await teams(page.request, organization.id, project.id);
    if (!firstTeam) throw new Error('The project has no team.');
    const member = await addEphemeralMember(page.request, organization.id, 'Участник', 'member');
    throwaway.push(member);
    await addTeamMember(page.request, organization.id, project.id, firstTeam.id, member.id);
    const item = await createWorkItem(page.request, organization.id, project.id, {
      typeId: await typeId(page.request, organization.id, 'TASK'),
      title: 'Назначенная задача',
    });
    await saveWorkItem(page.request, organization.id, project.id, item.identifier, {
      assignee: { userId: member.id },
    });

    // The member is on a page of the organization when it happens.
    const memberSession = await openSession(browser, {
      username: member.username,
      returnPath: workItemsAddress(organization.id, project.id),
    });
    try {
      await expect(heading(memberSession.page, 'Рабочие элементы')).toBeVisible();
      await expect(memberSession.page.getByText(item.identifier).first()).toBeVisible();

      await openMembers(page, organization.id);
      const table = membersTable(page);
      await chooseRowAction(page, table, member.displayName, 'Удалить из организации');
      const dialog = page.getByRole('dialog', { name: 'Удалить участника?' });
      await expect(dialog).toContainText(
        `Пользователь ${member.displayName} потеряет доступ к организации «${organization.name}» и будет исключён из всех её команд. Назначенные ему рабочие элементы сохранят исполнителя.`,
      );
      await expect(dialog.getByRole('button', { name: 'Отмена' })).toBeFocused();

      // «Отмена» changes nothing.
      await dialog.getByRole('button', { name: 'Отмена' }).click();
      await expect(dialog).toHaveCount(0);
      await expect(rowOf(table, member.displayName)).toHaveCount(1);
      expect((await members(page.request, organization.id)).map((m) => m.user.id)).toContain(
        member.id,
      );

      // «Удалить»: the row goes, the notice names the user without a mark.
      await chooseRowAction(page, table, member.displayName, 'Удалить из организации');
      await dialog.getByRole('button', { name: 'Удалить', exact: true }).click();
      await expect(dialog).toHaveCount(0);
      await expect(rowOf(table, member.displayName)).toHaveCount(0);
      await expect(
        notice(page, `Пользователь «${member.displayName}» удалён из организации.`),
      ).toBeVisible();

      // BR-M1-36 and BR-M1-26: out of the team, still the item's assignee.
      expect(
        (await teamMembers(page.request, organization.id, project.id, firstTeam.id)).map(
          (user) => user.id,
        ),
      ).not.toContain(member.id);
      const assignee = await assigneeOf(page.request, organization.id, project.id, item.identifier);
      expect(assignee?.id).toBe(member.id);
      expect(assignee?.isMember).toBe(false);
      await page.goto(workItemsAddress(organization.id, project.id));
      const itemRow = page.getByRole('row').filter({ hasText: item.identifier });
      await expect(itemRow).toContainText(member.displayName);
      await expect(itemRow).not.toContainText('неактивен');

      // Their next request answers 404 «Организация не найдена»; the page they
      // had open turns into the frame when the window returns to them.
      const gone = await send(
        memberSession.page.request,
        'GET',
        `/api/organizations/${organization.id}/projects`,
      );
      expect(gone.status()).toBe(404);
      expect(((await gone.json()) as { errorCode: string }).errorCode).toBe(
        'organizations.organization.not_found',
      );
      await refocus(memberSession.page);
      await expect(heading(memberSession.page, 'Организация не найдена')).toBeVisible();
    } finally {
      await memberSession.close();
    }
  });

  test('27.5 the last administrator: refused alone; stepping down beside a second administrator hides the controls with no reload; removing oneself lands on «Организации» (US-1, US-4)', async ({
    page,
    browser,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const alone = await fixtureOrganization(page);
    const shared = await fixtureOrganization(page);
    // The fixture user ends this one as a non-member: the second administrator
    // deactivates it in `finally`, and `afterAll` takes the 404 that follows.
    const second = await addEphemeralMember(
      page.request,
      shared.organization.id,
      'Второй админ',
      'admin',
    );
    throwaway.push(second);
    const fixtureId = (await fixtureMember(page.request, shared.organization.id)).user.id;
    const secondSession = await openSession(browser, { username: second.username });
    try {
      await test.step('alone: the role is refused on «Роль», the removal in its confirmation', async () => {
        await openMembers(page, alone.organization.id);
        const table = membersTable(page);

        await chooseRowAction(page, table, FIXTURE_NAME, 'Изменить роль');
        const dialog = page.getByRole('dialog', { name: 'Изменение роли' });
        await roleField(dialog).click();
        await page.getByRole('option', { name: ROLE_LABELS.member, exact: true }).click();
        await expect(
          dialog.getByText(
            'Вы потеряете права администратора. Вернуть их сможет только другой администратор.',
          ),
        ).toBeVisible();
        await dialog.getByRole('button', { name: 'Сохранить' }).click();
        await expect(
          dialog.getByText('В организации должен остаться хотя бы один администратор', {
            exact: true,
          }),
        ).toBeVisible();
        await expect(dialog).toBeVisible();
        await dialog.getByRole('button', { name: 'Отмена' }).click();
        await expect(dialog).toHaveCount(0);

        await chooseRowAction(page, table, FIXTURE_NAME, 'Удалить из организации');
        const confirmation = page.getByRole('dialog', { name: 'Удалить себя из организации?' });
        await expect(confirmation).toContainText(
          `Вы потеряете доступ к организации «${alone.organization.name}».`,
        );
        await confirmation.getByRole('button', { name: 'Удалить', exact: true }).click();
        await expect(
          confirmation.getByText('В организации должен остаться хотя бы один администратор'),
        ).toBeVisible();
        await confirmation.getByRole('button', { name: 'Отмена' }).click();
        await expect(confirmation).toHaveCount(0);
        const left = await members(page.request, alone.organization.id);
        expect(left.map((member) => [member.user.displayName, member.role])).toEqual([
          [FIXTURE_NAME, 'admin'],
        ]);
      });

      await test.step('beside a second administrator: stepping down hides the controls with no reload', async () => {
        await openMembers(page, shared.organization.id);
        // A marker on `window` survives only while the document is not reloaded.
        await page.evaluate(() => {
          (window as unknown as { qaMarker?: string }).qaMarker = 'kept';
        });
        const table = membersTable(page);
        await expect(page.getByRole('button', { name: 'Добавить участника' })).toBeVisible();
        await chooseRowAction(page, table, FIXTURE_NAME, 'Изменить роль');
        const dialog = page.getByRole('dialog', { name: 'Изменение роли' });
        await roleField(dialog).click();
        await page.getByRole('option', { name: ROLE_LABELS.member, exact: true }).click();
        await expect(
          dialog.getByText(
            'Вы потеряете права администратора. Вернуть их сможет только другой администратор.',
          ),
        ).toBeVisible();
        await dialog.getByRole('button', { name: 'Сохранить' }).click();

        await expect(dialog).toHaveCount(0);
        await expect(page.getByRole('button', { name: 'Добавить участника' })).toHaveCount(0);
        await expect(anyRowActions(page)).toHaveCount(0);
        const own = rowOf(table, FIXTURE_NAME);
        await expect(await cellOf(table, own, 'Роль')).toHaveText(ROLE_LABELS.member);
        await expect(page.locator('#main-content')).toBeFocused();
        expect(
          await page.evaluate(() => (window as unknown as { qaMarker?: string }).qaMarker ?? null),
        ).toBe('kept');

        // The second administrator gives the role back.
        await changeMemberRole(
          secondSession.page.request,
          shared.organization.id,
          fixtureId,
          'admin',
        );
      });

      await test.step('beside a second administrator: removing oneself lands on «Организации»', async () => {
        await openMembers(page, shared.organization.id);
        await chooseRowAction(page, membersTable(page), FIXTURE_NAME, 'Удалить из организации');
        const confirmation = page.getByRole('dialog', { name: 'Удалить себя из организации?' });
        await expect(confirmation).toContainText(
          `Вы потеряете доступ к организации «${shared.organization.name}».`,
        );
        await confirmation.getByRole('button', { name: 'Удалить', exact: true }).click();

        await expect(page).toHaveURL(/\/organizations$/);
        await expect(heading(page, 'Организации')).toBeVisible();
        expect((await activeOrganizations(page.request)).map((o) => o.id)).not.toContain(
          shared.organization.id,
        );
        // The fixture user is out: the organization answers it as it answers a stranger.
        await page.goto(membersAddress(shared.organization.id));
        await expect(heading(page, 'Организация не найдена')).toBeVisible();
      });
    } finally {
      // The fixture user cannot deactivate it any more; the administrator that
      // remains does (best effort: a failed step above is the failure to report).
      await deactivateOrganization(secondSession.page.request, shared.organization.id).catch(
        () => undefined,
      );
      await secondSession.close();
    }
  });

  test('27.6 a membership already gone: «Изменить роль» then «Сохранить» shows «Участник не найден.» and the list is read again (US-1)', async ({
    page,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const { organization } = await fixtureOrganization(page);
    const member = await addEphemeralMember(page.request, organization.id, 'Участник', 'member');
    throwaway.push(member);

    await openMembers(page, organization.id);
    const table = membersTable(page);
    await expect(rowOf(table, member.displayName)).toHaveCount(1);

    // Another administrator removes the user behind the open page (the same
    // administrator through the API does the same: the page is not told).
    await removeMember(page.request, organization.id, member.id);

    await chooseRowAction(page, table, member.displayName, 'Изменить роль');
    const dialog = page.getByRole('dialog', { name: 'Изменение роли' });
    await roleField(dialog).click();
    await page.getByRole('option', { name: ROLE_LABELS.viewer, exact: true }).click();
    await dialog.getByRole('button', { name: 'Сохранить' }).click();

    await expect(dialog.getByRole('alert')).toContainText('Участник не найден.');
    await expect(dialog).toBeVisible();
    // The list is read again at once: the row is gone while the dialog stays.
    await expect(rowOf(table, member.displayName)).toHaveCount(0);
    await dialog.getByRole('button', { name: 'Отмена' }).click();
    await expect(dialog).toHaveCount(0);
  });

  test('27.7 a disabled member is listed «неактивен» after the name, in «Имя» and in the «Добавлен» of the rows that name them, and keeps the role (US-1)', async ({
    page,
    browser,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const { organization } = await fixtureOrganization(page);
    const { keycloakUrl, realm } = env();
    // An administrator who adds another member, so a second row names them.
    const adder = await addEphemeralMember(page.request, organization.id, 'Администратор', 'admin');
    throwaway.push(adder);
    const added = await createDirectoryUser(page.request, organization.id, 'Читатель');
    throwaway.push(added);
    const adderSession = await openSession(browser, { username: adder.username });
    try {
      await addMember(adderSession.page.request, organization.id, added.id, 'viewer');
    } finally {
      await adderSession.close();
    }
    // Then their account is disabled; the projection learns it a moment later.
    await setUserEnabled(keycloakUrl, realm, adder.id, false);
    await awaitMemberState(page.request, organization.id, adder.id, 'inactive');

    await openMembers(page, organization.id);
    const table = membersTable(page);
    const adderRow = rowOf(table, adder.displayName);
    const mark = new RegExp(`${escapeForRegExp(adder.displayName)}\\s*неактивен`);
    await expect(await cellOf(table, adderRow, 'Имя')).toContainText(mark);
    await expect(await cellOf(table, adderRow, 'Роль')).toHaveText(ROLE_LABELS.admin);
    // The row that names them keeps the mark attached to the name, the comma after it.
    const addedRow = rowOf(table, added.displayName);
    await expect(await cellOf(table, addedRow, 'Добавлен')).toContainText(mark);
    // The viewer who is active carries no mark.
    await expect(await cellOf(table, addedRow, 'Имя')).not.toContainText('неактивен');
  });

  test('27.8 a user in no group: «Организация не найдена» at every depth, the empty list without «Создать организацию», «Настройки» opens «Языки» (US-2, US-6, US-8)', async ({
    page,
    browser,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const { organization, project } = await fixtureOrganization(page);
    const { keycloakUrl, realm } = env();
    const stranger = await createEphemeralUserWithoutGroup(keycloakUrl, realm);
    throwaway.push(stranger);

    const strangerSession = await openSession(browser, {
      username: stranger.username,
      returnPath: organizationGeneralAddress(organization.id),
    });
    try {
      const strangerPage = strangerSession.page;
      // The organization, its members and a project beneath it: the same frame each time.
      for (const address of [
        organizationGeneralAddress(organization.id),
        membersAddress(organization.id),
        projectGeneralAddress(organization.id, project.id),
        workItemsAddress(organization.id, project.id),
      ]) {
        if (new URL(strangerPage.url()).pathname !== address) await strangerPage.goto(address);
        await expect(heading(strangerPage, 'Организация не найдена'), address).toBeVisible();
        await expect(strangerPage.getByRole('table')).toHaveCount(0);
        expect(new URL(strangerPage.url()).pathname).toBe(address);
      }

      // The API says the same, byte for byte, as for an organization that never existed (BR-M1-8).
      const real = await send(strangerPage.request, 'GET', `/api/organizations/${organization.id}`);
      const never = await send(strangerPage.request, 'GET', `/api/organizations/${randomUUID()}`);
      expect(real.status()).toBe(404);
      expect(never.status()).toBe(404);
      const realBody = (await real.json()) as { errorCode: string; detail: string };
      const neverBody = (await never.json()) as { errorCode: string; detail: string };
      expect(realBody.errorCode).toBe('organizations.organization.not_found');
      expect(realBody.errorCode).toBe(neverBody.errorCode);
      expect(realBody.detail).toBe(neverBody.detail);

      // The list says they belong to nothing and offers no create (no create right).
      await strangerPage.goto('/organizations');
      await expect(
        strangerPage.getByRole('heading', {
          level: 2,
          name: 'Вы пока не состоите ни в одной организации',
        }),
      ).toBeVisible();
      await expect(
        strangerPage.getByText('Попросите администратора организации добавить вас.', {
          exact: true,
        }),
      ).toBeVisible();
      await expect(strangerPage.getByRole('button', { name: 'Создать организацию' })).toHaveCount(
        0,
      );

      // «Настройки» still opens «Языки»: every signed-in user reads the dictionaries (D-7).
      await mainNavigation(strangerPage).getByRole('link', { name: 'Настройки' }).click();
      await expect(heading(strangerPage, 'Языки')).toBeVisible();
      await expect(strangerPage.getByRole('table', { name: 'Реестр языков' })).toBeVisible();
      await expect(strangerPage.getByRole('button', { name: 'Добавить язык' })).toHaveCount(0);
    } finally {
      await strangerSession.close();
    }
  });

  test('27.9 the three roles see «Общие», «Проекты», «Участники», «Процесс» in that order; the reader sees the table without the controls; the served bundles are the contract’s (US-4)', async ({
    page,
    browser,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const { organization } = await fixtureOrganization(page);
    const member = await addEphemeralMember(page.request, organization.id, 'Участник', 'member');
    const viewer = await addEphemeralMember(page.request, organization.id, 'Читатель', 'viewer');
    throwaway.push(member, viewer);
    const memberSession = await openSession(browser, { username: member.username });
    const viewerSession = await openSession(browser, { username: viewer.username });
    try {
      const sorted = (codes: readonly string[]) => [...codes].sort();
      const bundles = [
        { role: 'admin', page, codes: [...READ_CODES, ...MEMBER_CODES, ...ADMIN_CODES] },
        { role: 'member', page: memberSession.page, codes: [...READ_CODES, ...MEMBER_CODES] },
        { role: 'viewer', page: viewerSession.page, codes: READ_CODES },
      ] as const;

      for (const { role, page: rolePage, codes } of bundles) {
        // The bundle as served (api-contract § 1.1, § 0.2): exactly the role's
        // codes, organization codes only, in ordinal order.
        const served = await accessOf(rolePage.request, organization.id);
        expect(served.role, role).toBe(role);
        expect([...served.permissions].sort(), `${role}: the served codes`).toEqual(sorted(codes));
        expect(served.permissions, `${role}: the served order`).toEqual(sorted(served.permissions));

        // The navigation: the same four entries in the same order for every role.
        await openMembers(rolePage, organization.id);
        await expect(
          settingsNavigation(rolePage).getByRole('link', { name: 'Участники' }),
        ).toBeVisible();
        const entries = (await settingsNavigation(rolePage).getByRole('link').allInnerTexts()).map(
          (text) => text.trim(),
        );
        expect(entries.slice(0, 4), `${role}: the organization's entries`).toEqual([
          'Общие',
          'Проекты',
          'Участники',
          'Процесс',
        ]);

        // The table is read by everyone; the commands are the administrator's.
        await expect(rowOf(membersTable(rolePage), member.displayName)).toBeVisible();
        await awaitLoaded(rolePage);
        if (role === 'admin') {
          await expect(rolePage.getByRole('button', { name: 'Добавить участника' })).toBeVisible();
          await expect(rowActions(rolePage, member.displayName)).toBeVisible();
        } else {
          await expect(rolePage.getByRole('button', { name: 'Добавить участника' })).toHaveCount(0);
          await expect(anyRowActions(rolePage)).toHaveCount(0);
          // No empty column left behind either (DC-2): the header cells are the five.
          await expect(membersTable(rolePage).getByRole('columnheader')).toHaveText([
            'Имя',
            'Почта',
            'Роль',
            'Добавлен',
            'Роль изменена',
          ]);
        }
      }
    } finally {
      await memberSession.close();
      await viewerSession.close();
    }
  });

  test('27.10 keyboard and width: after the header’s controls Tab reaches «Добавить участника», then one «Действия: <имя>» per row; Escape closes the list before the dialog; at 640 px the table scrolls inside its own paper (US-1; design § 10, § 11)', async ({
    page,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const { organization } = await fixtureOrganization(page);
    const other = await addEphemeralMember(page.request, organization.id, 'Участник', 'member');
    throwaway.push(other);
    await openMembers(page, organization.id);
    await expect(rowOf(membersTable(page), other.displayName)).toBeVisible();
    await awaitLoaded(page);

    // The skip link moves focus to the main region; from there the first stops
    // are the page's own: the command, then a button per row, in row order.
    await page.getByRole('link', { name: 'Перейти к содержимому' }).focus();
    await page.keyboard.press('Enter');
    await expect(page.locator('#main-content')).toBeFocused();
    const names = await membersTable(page)
      .locator('tbody tr')
      .evaluateAll((rows) =>
        rows.map((row) => (row.querySelector('td, th')?.textContent ?? '').trim()),
      );
    expect(names).toContain(FIXTURE_NAME);
    expect(names).toContain(other.displayName);
    const stops: string[] = [];
    for (let index = 0; index < names.length + 1; index += 1) {
      await page.keyboard.press('Tab');
      stops.push(
        await page.evaluate(() => {
          const element = document.activeElement as HTMLElement | null;
          return (element?.getAttribute('aria-label') ?? element?.textContent ?? '').trim();
        }),
      );
    }
    expect(stops).toEqual(['Добавить участника', ...names.map((name) => `Действия: ${name}`)]);

    // The dialog: the first Escape closes the candidate list, the next the dialog.
    await page.getByRole('button', { name: 'Добавить участника' }).click();
    const dialog = page.getByRole('dialog', { name: 'Добавление участника' });
    const field = userField(dialog, 'Пользователь');
    await searchFor(field, other.token);
    await expect(optionWith(page, other.displayName)).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(page.getByRole('listbox')).toHaveCount(0);
    await expect(dialog).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(dialog).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Добавить участника' })).toBeFocused();

    // 640 px (the floor, D-8): the table scrolls inside its own container, the page does not.
    await page.setViewportSize({ width: 640, height: 800 });
    await expect(membersTable(page)).toBeVisible();
    const container = page.locator('.MuiTableContainer-root').filter({ has: membersTable(page) });
    await expect
      .poll(() =>
        container.evaluate((element) => ({
          wider: element.scrollWidth > element.clientWidth,
          overflowX: getComputedStyle(element).overflowX,
        })),
      )
      .toEqual({ wider: true, overflowX: expect.stringMatching(/^(auto|scroll)$/) });
    expect(await pageScrollsSideways(page)).toBe(false);
    // The dates do not wrap (design § 11): each stamp's date stands on one line.
    // Design § 11 says no more than that — the name before the comma in
    // «Добавлен» is running text, not a truncated one — so the height of a row
    // is not asserted.
    const stampLines = await membersTable(page)
      .locator('tbody tr')
      .first()
      .getByText(/\d{2}\.\d{2}\.\d{4} \d{2}:\d{2}/)
      .evaluateAll((dates) => dates.map((date) => date.getClientRects().length));
    expect(stampLines.length).toBeGreaterThan(0);
    expect(stampLines.every((lines) => lines === 1)).toBe(true);
  });
});
