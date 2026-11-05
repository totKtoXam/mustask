import { expect, test, type Page } from '@playwright/test';
import { signIn } from '../support/auth';
import { env } from '../support/env';
import { deleteEphemeralUsers, setUserEnabled, type EphemeralUser } from '../support/keycloakAdmin';
import {
  addEphemeralMember,
  addMember,
  assigneeOf,
  awaitMemberState,
  createDirectoryUser,
  removeMember,
} from '../support/membersApi';
import {
  chooseUser,
  expectFieldRefusal,
  heading,
  main,
  notice,
  optionWith,
  searchAnswered,
  userField,
  workItemAddress,
} from '../support/membersUi';
import { createContext, createOrganization, runName, send } from '../support/organizationsApi';
import { DESKTOP, openSession } from '../support/sessions';
import { createWorkItem, saveWorkItem, typeId } from '../support/workItemsApi';

/**
 * 30. The assignee rule (mod-016 US-5) against the real backend, real Keycloak
 * and the real user projection.
 *
 * Scenarios, mapped to stories:
 * 30.1 the «Исполнитель» picker offers an active member of the organization and
 *      neither a disabled member nor a member of another organization only;
 *      searching for the latter shows «Никого не найдено» (US-5 "The picker
 *      offers only active members of the item's organization")
 * 30.2 a user removed after the search is picked: «Сохранить» shows «Этого
 *      пользователя нельзя назначить исполнителем.» on «Исполнитель», the draft
 *      kept (US-5 "A non-member … assignee is refused"; design § 7 "A refusal")
 * 30.3 an assignee who leaves the organization stays on the form with no mark,
 *      saving the title succeeds, «Не назначен» clears; re-added as a reader
 *      they are offered again (US-5 "An assignee who leaves … stays", "Clearing
 *      the assignee", "A member re-added … can be assigned again")
 *
 * US-5's API rows (a save that sets a non-member, an inactive or an unknown
 * assignee; `assignee.isMember` in the list, the item, the backlog and the
 * board) are the backend's integration tests; 27.4 checks the flag once.
 *
 * Every case creates what it needs the way specs 21 and 22 do — an organization
 * with a project and a work item through the API, created by the fixture user,
 * who is therefore its administrator (D-3) — and `afterAll` deactivates each
 * organization it created and deletes each throwaway user. The picker is
 * the fixture user's: it needs `workitems.workitem.write`, which an
 * administrator holds. Nothing is injected and nothing on the session flow is
 * stubbed (`src/test/e2eSessionGuard.test.ts`).
 */

test.describe.configure({ timeout: 300_000 });

const created: string[] = [];
const throwaway: EphemeralUser[] = [];

/** An organization with «Пример проекта» and one «Задача» in it, through the API. */
async function fixtureItem(page: Page, title: string) {
  const context = await createContext(page.request, {
    organization: runName('Организация E2E'),
    project: 'Пример проекта',
  });
  created.push(context.organization.id);
  const item = await createWorkItem(page.request, context.organization.id, context.project.id, {
    typeId: await typeId(page.request, context.organization.id, 'TASK'),
    title,
  });
  return { ...context, item };
}

/** The item's form, waited for. */
async function openForm(
  page: Page,
  organizationId: string,
  projectId: string,
  identifier: string,
): Promise<void> {
  await page.goto(workItemAddress(organizationId, projectId, identifier));
  await expect(heading(page, `Задача ${identifier}`)).toBeVisible();
  await expect(page.getByRole('textbox', { name: 'Заголовок' })).toBeVisible();
}

const assigneeField = (page: Page) => userField(main(page), 'Исполнитель');

test.describe('30. the assignee rule (mod-016)', () => {
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

  test('30.1 the picker offers an active member and neither a disabled member nor a member of another organization only (US-5)', async ({
    page,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const { organization, project, item } = await fixtureItem(page, 'Настроить сборку');
    const { keycloakUrl, realm } = env();
    const active = await addEphemeralMember(page.request, organization.id, 'Анна', 'member');
    const disabled = await addEphemeralMember(page.request, organization.id, 'Олег', 'member');
    // «Мария»: an active user of the directory who is a member of another organization only.
    const elsewhere = await createDirectoryUser(page.request, organization.id, 'Мария');
    throwaway.push(active, disabled, elsewhere);
    const other = await createOrganization(page.request, runName('Другая организация E2E'));
    created.push(other.id);
    await addMember(page.request, other.id, elsewhere.id, 'member');
    await setUserEnabled(keycloakUrl, realm, disabled.id, false);
    await awaitMemberState(page.request, organization.id, disabled.id, 'inactive');

    await openForm(page, organization.id, project.id, item.identifier);
    const field = assigneeField(page);

    await searchAnswered(page, field, active.token, 'assignee-candidates');
    await expect(optionWith(page, active.displayName)).toBeVisible();

    await searchAnswered(page, field, disabled.token, 'assignee-candidates');
    await expect(optionWith(page, 'Никого не найдено')).toBeVisible();
    await expect(optionWith(page, disabled.displayName)).toHaveCount(0);

    await searchAnswered(page, field, elsewhere.token, 'assignee-candidates');
    await expect(optionWith(page, 'Никого не найдено')).toBeVisible();
    await expect(optionWith(page, elsewhere.displayName)).toHaveCount(0);
  });

  test('30.2 a user removed after the search is picked: «Сохранить» shows «Этого пользователя нельзя назначить исполнителем.» on «Исполнитель», the draft kept (US-5)', async ({
    page,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const { organization, project, item } = await fixtureItem(page, 'Настроить сборку');
    const leaving = await addEphemeralMember(page.request, organization.id, 'Анна', 'member');
    throwaway.push(leaving);

    await openForm(page, organization.id, project.id, item.identifier);
    const field = assigneeField(page);
    await chooseUser(page, field, leaving);
    const save = page.getByRole('button', { name: 'Сохранить' });
    await expect(save).toBeEnabled();

    // The user leaves the organization after the search offered them.
    await removeMember(page.request, organization.id, leaving.id);
    await save.click();

    await expectFieldRefusal(
      main(page),
      field,
      'Этого пользователя нельзя назначить исполнителем.',
    );
    // The draft is kept: the field still holds the user, «Сохранить» can try again.
    await expect(field).toHaveValue(leaving.displayName);
    await expect(save).toBeEnabled();
    expect(await assigneeOf(page.request, organization.id, project.id, item.identifier)).toBeNull();
  });

  test('30.3 an assignee who leaves stays with no mark, saving the title succeeds, «Не назначен» clears; re-added as a reader they are offered again (US-5)', async ({
    page,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const { organization, project, item } = await fixtureItem(page, 'Настроить сборку');
    const assignee = await addEphemeralMember(page.request, organization.id, 'Анна', 'member');
    throwaway.push(assignee);
    await saveWorkItem(page.request, organization.id, project.id, item.identifier, {
      assignee: { userId: assignee.id },
    });
    await removeMember(page.request, organization.id, assignee.id);

    await openForm(page, organization.id, project.id, item.identifier);
    const field = assigneeField(page);
    // Still the assignee, by name, and no mark: the marks are feat-017's (D-4).
    await expect(field).toHaveValue(assignee.displayName);
    await expect(main(page)).not.toContainText('неактивен');
    await expect(main(page)).not.toContainText('не в организации');
    const readBack = await assigneeOf(page.request, organization.id, project.id, item.identifier);
    expect(readBack?.id).toBe(assignee.id);
    expect(readBack?.isMember).toBe(false);

    // Saving another field is never refused for it, and keeps the assignee.
    const save = page.getByRole('button', { name: 'Сохранить' });
    await page.getByRole('textbox', { name: 'Заголовок' }).fill('Настроить сборку и деплой');
    await save.click();
    await expect(notice(page, 'Изменения сохранены.')).toBeVisible();
    // The form is clean again once the save has been answered.
    await expect(save).toBeDisabled();
    expect((await assigneeOf(page.request, organization.id, project.id, item.identifier))?.id).toBe(
      assignee.id,
    );

    // «Не назначен» is always allowed.
    await field.click();
    await page.getByRole('option', { name: 'Не назначен', exact: true }).click();
    await expect(save).toBeEnabled();
    await save.click();
    await expect(save).toBeDisabled();
    expect(await assigneeOf(page.request, organization.id, project.id, item.identifier)).toBeNull();

    // Added back as a reader (any role may be an assignee), they are offered again.
    await addMember(page.request, organization.id, assignee.id, 'viewer');
    await searchAnswered(page, field, assignee.token, 'assignee-candidates');
    await expect(optionWith(page, assignee.displayName)).toBeVisible();
  });
});
