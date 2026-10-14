import { expect, test, type Locator, type Page } from '@playwright/test';
import { signIn } from '../support/auth';
import { env } from '../support/env';
import { deleteEphemeralUsers, type EphemeralUser } from '../support/keycloakAdmin';
import {
  addEphemeralMember,
  addMember,
  changeMemberRole,
  removeMember,
} from '../support/membersApi';
import {
  anyRowActions,
  areasAddress,
  awaitLoaded,
  backlogAddress,
  boardAddress,
  deactivatedProjectsAddress,
  escapeForRegExp,
  heading,
  main,
  membersAddress,
  membersTable,
  notice,
  organizationGeneralAddress,
  projectGeneralAddress,
  projectsAddress,
  switcher,
  teamAddress,
  teamsAddress,
  workItemAddress,
  workItemsAddress,
} from '../support/membersUi';
import {
  createContext,
  createOrganization,
  createProject,
  runKey,
  runName,
  send,
  teams,
} from '../support/organizationsApi';
import { refocus } from '../support/refocus';
import { DESKTOP, openSession } from '../support/sessions';
import { createWorkItem, setParent, typeId } from '../support/workItemsApi';

/**
 * 29. What each role sees and does (mod-016 US-4, US-2) against the real
 * backend, real Keycloak and the real user projection.
 *
 * Scenarios, mapped to stories:
 * 29.1 a member: «Новый рабочий элемент», create and save a work item; none of
 *      the administrator controls on «Общие», «Проекты», «Команды», a team's
 *      page, «Области», «Участники»; the empty hints for the others (US-4 "The
 *      controls follow the role", "An empty page names no control the user lacks")
 * 29.2 a reader: lists, backlog and board are read and carry no write control,
 *      no «Ещё действия», no grip, nothing draggable; the work-item card is read-only
 *      (feat-029 BR-C-19: every field a value, the notice says why), no «Сохранить»,
 *      the parent with its «Открыть» link; the empty hints (US-4)
 * 29.3 a role granted behind the open page shows up on refocus, with no reload,
 *      and creating works (US-4 "A granted role shows up on refocus")
 * 29.4 a 403 re-reads the codes: «Недостаточно прав» in the dialog with what was
 *      typed, the primary disabled, the control gone once the dialog is closed,
 *      the role-change notice shown once (US-4 "A 403 re-reads the codes";
 *      design § 2.3, § 2.4)
 * 29.5 a member removed behind the open backlog: their next action turns the
 *      page into «Организация не найдена», the switcher falls back, one organization
 *      list and one access read follow the call's 404 (US-4 "A removed member's
 *      page turns into «not found»"; design § 2.2)
 * 29.6 a page opened by address without its code: «Доступ ограничен» for a
 *      member, the tab «Деактивированные» for an administrator (US-4)
 * 29.7 a switch from an organization the user administers to one they only
 *      read: no «Создать проект» on the second, at no frame (US-4 "An
 *      organization switch re-reads the codes")
 * 29.8 a failed access read: the banner with «Повторить», never «Доступ
 *      ограничен»; after the retry the page and its controls (US-4 "A failed
 *      access read offers a retry")
 *
 * US-2's other rows (the order of the answers, the second code of a parent
 * write, the inventory) are the backend's integration tests, with no browser
 * surface.
 *
 * Every case creates what it needs the way specs 21 and 22 do — an organization
 * with a project through the API, created by the fixture user, who is therefore
 * its administrator (D-3) — and `afterAll` deactivates each organization it
 * created and deletes each throwaway user.
 *
 * One fault is injected on purpose (29.8): the access read is refused at the
 * network layer, as 19.8 and 20.3 do for theirs. Nothing on the session flow is
 * stubbed (`src/test/e2eSessionGuard.test.ts`).
 */

test.describe.configure({ timeout: 300_000 });

const created: string[] = [];
const throwaway: EphemeralUser[] = [];

/** An organization with «Пример проекта» created by the fixture user, who administers it. */
async function fixtureOrganization(page: Page, extraTeams: readonly string[] = []) {
  const context = await createContext(page.request, {
    organization: runName('Организация E2E'),
    project: 'Пример проекта',
    teams: extraTeams,
  });
  created.push(context.organization.id);
  const firstTeam = context.teams.find((team) => team.isFirstTeam);
  if (!firstTeam) throw new Error('The project has no first team.');
  return { ...context, firstTeam };
}

const createButton = (target: Page) =>
  target.getByRole('button', { name: 'Новый рабочий элемент' });

/** The list's «Новый рабочий элемент» → a type → the dialog, filled and submitted. */
async function createFromList(target: Page, type: string, title: string): Promise<void> {
  await createButton(target).first().click();
  await target.getByRole('menuitem', { name: type, exact: true }).click();
  const dialog = target.getByRole('dialog', { name: 'Новый рабочий элемент' });
  await dialog.getByRole('textbox', { name: 'Заголовок' }).fill(title);
  await dialog.getByRole('button', { name: 'Создать' }).click();
}

const SENTENCE_FOR_READERS =
  'Рабочие элементы создают участники организации с ролью «Участник» или «Администратор».';

test.describe('29. role views (mod-016)', () => {
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
            // A 404: the organization is gone for the fixture user already (29.5 removes no
            // fixture membership, but a failed run may have left one half-done).
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

  test('29.1 a member creates and saves a work item and sees none of the administrator controls; the empty pages name no control they lack (US-4)', async ({
    page,
    browser,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const { organization, project } = await fixtureOrganization(page, ['Аналитика']);
    const [, extra] = await teams(page.request, organization.id, project.id);
    if (!extra) throw new Error('The extra team is missing.');
    // A second organization with no project, for «Пока нет проектов».
    const bare = await createOrganization(page.request, runName('Организация E2E пустая'));
    created.push(bare.id);
    const member = await addEphemeralMember(page.request, organization.id, 'Участник', 'member');
    throwaway.push(member);
    await addMember(page.request, bare.id, member.id, 'member');

    const session = await openSession(browser, {
      username: member.username,
      returnPath: workItemsAddress(organization.id, project.id),
    });
    try {
      const target = session.page;

      // The work-item writes are the member's (the spec's matrix): the empty
      // list offers the first one, the dialog creates, the form saves.
      await expect(heading(target, 'Рабочие элементы')).toBeVisible();
      await expect(
        main(target).getByRole('heading', { level: 2, name: 'Рабочих элементов пока нет' }),
      ).toBeVisible();
      await expect(
        target.getByText('Создайте первый рабочий элемент кнопкой «Новый рабочий элемент» выше.', {
          exact: true,
        }),
      ).toBeVisible();
      await createFromList(target, 'Задача', 'Задача участника');
      const identifier = `${project.key}-TASK-1`;
      await expect(notice(target, `Рабочий элемент ${identifier} создан.`)).toBeVisible();
      await target.getByRole('link', { name: 'Задача участника' }).click();
      await expect(heading(target, `Задача ${identifier}`)).toBeVisible();
      await target.getByRole('textbox', { name: 'Заголовок' }).fill('Задача участника, правка');
      await target.getByRole('button', { name: 'Сохранить' }).click();
      await expect(notice(target, 'Изменения сохранены.')).toBeVisible();

      // Each page the member may read, with something that proves it has loaded,
      // and the controls it must not draw.
      const pages: {
        name: string;
        address: string;
        loaded: (target: Page) => Locator;
        absent: (target: Page) => Locator[];
      }[] = [
        {
          name: 'организация, «Общие»',
          address: organizationGeneralAddress(organization.id),
          loaded: (t) => main(t).getByText('Название', { exact: true }),
          absent: (t) => [
            t.getByRole('button', { name: 'Переименовать' }),
            t.getByRole('button', { name: 'Деактивировать' }),
          ],
        },
        {
          name: '«Проекты»',
          address: projectsAddress(organization.id),
          loaded: (t) => t.getByRole('table', { name: 'Активные проекты' }),
          absent: (t) => [
            t.getByRole('button', { name: 'Создать проект' }),
            // One view left: no strip with a lone tab (DC-2).
            t.getByRole('tab'),
          ],
        },
        {
          name: 'проект, «Общие»',
          address: projectGeneralAddress(organization.id, project.id),
          loaded: (t) => main(t).getByRole('link', { name: 'Процесс' }),
          absent: (t) => [
            t.getByRole('button', { name: 'Переименовать' }),
            t.getByRole('button', { name: 'Деактивировать' }),
          ],
        },
        {
          name: '«Команды»',
          address: teamsAddress(organization.id, project.id),
          loaded: (t) => t.getByRole('table', { name: 'Команды' }),
          absent: (t) => [t.getByRole('button', { name: 'Добавить команду' }), anyRowActions(t)],
        },
        {
          name: 'страница команды',
          address: teamAddress(organization.id, project.id, extra.id),
          loaded: (t) => t.getByRole('heading', { level: 2, name: 'Участники команды' }),
          absent: (t) => [
            t.getByRole('button', { name: 'Переименовать' }),
            t.getByRole('button', { name: 'Удалить команду' }),
            t.getByRole('button', { name: 'Изменить область по умолчанию' }),
            t.getByRole('button', { name: 'Добавить в команду' }),
            anyRowActions(t),
          ],
        },
        {
          name: '«Области»',
          address: areasAddress(organization.id, project.id),
          loaded: (t) => t.getByRole('heading', { level: 2, name: 'Вложенных областей пока нет' }),
          absent: (t) => [
            t.getByRole('button', { name: 'Новая область' }),
            t.getByRole('button', { name: 'Новая дочерняя область' }),
          ],
        },
        {
          name: '«Участники»',
          address: membersAddress(organization.id),
          loaded: (t) => membersTable(t),
          absent: (t) => [t.getByRole('button', { name: 'Добавить участника' }), anyRowActions(t)],
        },
      ];
      for (const { name, address, loaded, absent } of pages) {
        await target.goto(address);
        await expect(loaded(target), name).toBeVisible();
        await awaitLoaded(target);
        for (const control of absent(target)) await expect(control, name).toHaveCount(0);
      }

      // The empty pages the member cannot fill name the one who can.
      await target.goto(areasAddress(organization.id, project.id));
      await expect(
        target.getByText('Области задаёт администратор организации.', { exact: true }),
      ).toBeVisible();
      await target.goto(projectsAddress(bare.id));
      await expect(
        target.getByRole('heading', { level: 2, name: 'Пока нет проектов' }),
      ).toBeVisible();
      await expect(
        target.getByText('Проекты создаёт администратор организации.', { exact: true }),
      ).toBeVisible();
      await expect(target.getByRole('button', { name: 'Создать проект' })).toHaveCount(0);
    } finally {
      await session.close();
    }
  });

  test('29.2 a reader reads the lists, the backlog and the board with no write control; the work-item form is text with the parent a link; the empty pages name the ones who can (US-4)', async ({
    page,
    browser,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const { organization, project, firstTeam } = await fixtureOrganization(page);
    const reader = await addEphemeralMember(page.request, organization.id, 'Читатель', 'viewer');
    throwaway.push(reader);
    // Two stories and a bug under the first one, in the first team's root area.
    const story = await typeId(page.request, organization.id, 'US');
    const bugType = await typeId(page.request, organization.id, 'BUG');
    const first = await createWorkItem(page.request, organization.id, project.id, {
      typeId: story,
      title: 'Вход по паролю',
    });
    await createWorkItem(page.request, organization.id, project.id, {
      typeId: story,
      title: 'Выход',
    });
    const bug = await createWorkItem(page.request, organization.id, project.id, {
      typeId: bugType,
      title: 'Падение при входе',
    });
    await setParent(page.request, organization.id, project.id, bug.identifier, first.id);
    // A project with nothing in it, for the empty pages.
    const empty = await createProject(page.request, organization.id, {
      name: 'Пустой проект',
      key: runKey('E'),
    });
    const [emptyTeam] = await teams(page.request, organization.id, empty.id);
    if (!emptyTeam) throw new Error('The empty project has no team.');

    const backlog = backlogAddress(organization.id, project.id, firstTeam.id);
    const board = boardAddress(organization.id, project.id, firstTeam.id);
    const backlogTable = (target: Page) => target.getByRole('table', { name: 'Бэклог' });
    const rows = (target: Page) => backlogTable(target).locator('tbody tr[data-identifier]');

    // The administrator's own view proves the locators below can find what
    // the reader must not have: a grip, a draggable row and card, a row menu.
    await page.goto(backlog);
    await expect(rows(page)).toHaveCount(3);
    await expect(main(page).locator('[data-grip]').first()).toBeAttached();
    await expect(main(page).locator('tr[draggable="true"]').first()).toBeAttached();
    await expect(
      main(page)
        .getByRole('button', { name: /^Ещё действия/ })
        .first(),
    ).toBeVisible();
    await expect(
      main(page)
        .getByRole('button', { name: /^Добавить/ })
        .first(),
    ).toBeVisible();
    await page.goto(board);
    await expect(main(page).locator('[data-card]')).toHaveCount(3);
    await expect(main(page).locator('[draggable="true"]').first()).toBeAttached();

    const session = await openSession(browser, {
      username: reader.username,
      returnPath: workItemsAddress(organization.id, project.id),
    });
    try {
      const target = session.page;

      await test.step('the list is read; no «Новый рабочий элемент»', async () => {
        await expect(heading(target, 'Рабочие элементы')).toBeVisible();
        await expect(target.getByRole('table', { name: 'Рабочие элементы' })).toContainText(
          'Вход по паролю',
        );
        await awaitLoaded(target);
        await expect(createButton(target)).toHaveCount(0);
      });

      await test.step('the backlog is read; the reader keeps what only reads', async () => {
        await target.goto(backlog);
        await expect(heading(target, firstTeam.name)).toBeVisible();
        await expect(rows(target)).toHaveCount(3);
        await awaitLoaded(target);
        await expect(createButton(target)).toHaveCount(0);
        await expect(main(target).getByRole('button', { name: /^Ещё действия/ })).toHaveCount(0);
        await expect(main(target).getByRole('button', { name: /^Добавить/ })).toHaveCount(0);
        await expect(main(target).locator('[data-grip]')).toHaveCount(0);
        await expect(main(target).locator('tr[draggable="true"]')).toHaveCount(0);
        // What only reads stays: the head's buttons, the title links, the board link.
        await expect(
          main(target).getByRole('button', { name: 'Развернуть на один уровень' }),
        ).toBeVisible();
        await expect(
          main(target).getByRole('button', { name: 'Свернуть на один уровень' }),
        ).toBeVisible();
        await expect(target.getByRole('link', { name: 'Вход по паролю' })).toBeVisible();
        await expect(target.getByRole('link', { name: 'Показать канбан' })).toBeVisible();
      });

      await test.step('the board is read; no «Новый элемент», no card menu, nothing draggable', async () => {
        await target.goto(board);
        await expect(main(target).locator('[data-card]')).toHaveCount(3);
        await awaitLoaded(target);
        await expect(target.getByRole('button', { name: 'Новый элемент' })).toHaveCount(0);
        await expect(createButton(target)).toHaveCount(0);
        await expect(target.getByRole('button', { name: /^Ещё действия/ })).toHaveCount(0);
        await expect(main(target).locator('[draggable="true"]')).toHaveCount(0);
        await expect(target.getByRole('link', { name: 'Падение при входе' })).toBeVisible();
      });

      await test.step('the work-item card is read-only: the notice says why, the fields are values, the parent has its link, no «Сохранить»', async () => {
        await target.goto(workItemAddress(organization.id, project.id, bug.identifier));
        await expect(heading(target, `Баг ${bug.identifier}`)).toBeVisible();
        await expect(target.getByText('Рабочий элемент доступен только для чтения.')).toBeVisible();
        await expect(target.getByRole('textbox', { name: 'Заголовок' })).toHaveAttribute(
          'readonly',
          '',
        );
        await expect(target.getByRole('combobox', { name: 'Исполнитель' })).toHaveAttribute(
          'aria-readonly',
          'true',
        );
        await expect(main(target).getByText('Не назначен', { exact: true })).toBeVisible();
        const parent = target.getByRole('combobox', { name: 'Родитель' });
        await expect(parent).toHaveAttribute('aria-readonly', 'true');
        await expect(parent).toHaveValue(new RegExp(escapeForRegExp(first.identifier)));
        await expect(
          target.getByRole('link', { name: `Открыть ${first.identifier}` }),
        ).toBeVisible();
        // What only reads stays; the Save group and the undo do not.
        await expect(target.getByRole('button', { name: 'Обновить' })).toBeVisible();
        await expect(target.getByRole('button', { name: 'Сохранить' })).toHaveCount(0);
        await expect(target.getByRole('button', { name: 'Отменить все изменения' })).toHaveCount(0);
      });

      await test.step('the empty pages: the same heading, the hint for readers, no action', async () => {
        await target.goto(workItemsAddress(organization.id, empty.id));
        await expect(
          main(target).getByRole('heading', { level: 2, name: 'Рабочих элементов пока нет' }),
        ).toBeVisible();
        await expect(target.getByText(SENTENCE_FOR_READERS, { exact: true })).toBeVisible();
        await expect(createButton(target)).toHaveCount(0);

        await target.goto(backlogAddress(organization.id, empty.id, emptyTeam.id));
        await expect(
          main(target).getByRole('heading', { level: 2, name: 'Бэклог пуст' }),
        ).toBeVisible();
        await expect(target.getByText(SENTENCE_FOR_READERS, { exact: true })).toBeVisible();
        await expect(createButton(target)).toHaveCount(0);

        // The board keeps its own hint for everyone; only the action goes.
        await target.goto(boardAddress(organization.id, empty.id, emptyTeam.id));
        await expect(main(target).getByRole('heading', { name: 'Канбан пуст' })).toBeVisible();
        await expect(
          target.getByText('Новые рабочие элементы этого уровня появятся в колонке «Новая».', {
            exact: true,
          }),
        ).toBeVisible();
        await expect(createButton(target)).toHaveCount(0);
        await expect(target.getByRole('button', { name: 'Новый элемент' })).toHaveCount(0);
      });
    } finally {
      await session.close();
    }
  });

  test('29.3 a role granted behind the open page shows up on refocus with no reload, and creating works (US-4)', async ({
    page,
    browser,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const { organization, project } = await fixtureOrganization(page);
    const reader = await addEphemeralMember(page.request, organization.id, 'Читатель', 'viewer');
    throwaway.push(reader);

    const session = await openSession(browser, {
      username: reader.username,
      returnPath: workItemsAddress(organization.id, project.id),
    });
    try {
      const target = session.page;
      await expect(target.getByText(SENTENCE_FOR_READERS, { exact: true })).toBeVisible();
      await expect(createButton(target)).toHaveCount(0);
      // A marker on `window` survives only while the document is not reloaded.
      await target.evaluate(() => {
        (window as unknown as { qaMarker?: string }).qaMarker = 'kept';
      });

      // An administrator makes the reader a member; the window returns to the reader.
      await changeMemberRole(page.request, organization.id, reader.id, 'member');
      await refocus(target);

      await expect(createButton(target).first()).toBeVisible();
      await expect(
        notice(target, `Ваша роль в организации «${organization.name}» изменилась: «Участник».`),
      ).toBeVisible();
      expect(
        await target.evaluate(() => (window as unknown as { qaMarker?: string }).qaMarker ?? null),
      ).toBe('kept');

      await createFromList(target, 'Задача', 'Задача после выдачи роли');
      await expect(notice(target, `Рабочий элемент ${project.key}-TASK-1 создан.`)).toBeVisible();
    } finally {
      await session.close();
    }
  });

  test('29.4 a refused write re-reads the codes: «Недостаточно прав» in the dialog with what was typed, the primary disabled, the control gone, the role-change notice once (US-4)', async ({
    page,
    browser,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const { organization, project } = await fixtureOrganization(page);
    const member = await addEphemeralMember(page.request, organization.id, 'Участник', 'member');
    throwaway.push(member);

    const session = await openSession(browser, {
      username: member.username,
      returnPath: workItemsAddress(organization.id, project.id),
    });
    try {
      const target = session.page;
      await expect(createButton(target).first()).toBeVisible();

      // An administrator makes the member a reader; the page is not told.
      await changeMemberRole(page.request, organization.id, member.id, 'viewer');

      await createButton(target).first().click();
      await target.getByRole('menuitem', { name: 'Задача', exact: true }).click();
      const dialog = target.getByRole('dialog', { name: 'Новый рабочий элемент' });
      const title = dialog.getByRole('textbox', { name: 'Заголовок' });
      await title.fill('Набранное до отказа');
      await dialog.getByRole('button', { name: 'Создать' }).click();

      // Where the dialog shows its refusals, with the input kept…
      await expect(dialog.getByRole('alert')).toContainText('Недостаточно прав');
      await expect(title).toHaveValue('Набранное до отказа');
      // …and, once the codes are read again, the primary has nothing it may do.
      await expect(dialog.getByRole('button', { name: 'Создать' })).toBeDisabled();
      const changed = `Ваша роль в организации «${organization.name}» изменилась: «Читатель».`;
      await expect(notice(target, changed)).toHaveCount(1);
      await expect(dialog).toBeVisible();

      await dialog.getByRole('button', { name: 'Отмена' }).click();
      await expect(dialog).toHaveCount(0);
      await expect(createButton(target)).toHaveCount(0);
      await expect(notice(target, changed)).toHaveCount(1);
    } finally {
      await session.close();
    }
  });

  test('29.5 a member removed behind the open backlog: their next action turns the page into «Организация не найдена», the switcher falls back, and one list read and one access read follow the 404 (US-4)', async ({
    page,
    browser,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const { organization, project, firstTeam } = await fixtureOrganization(page);
    const member = await addEphemeralMember(page.request, organization.id, 'Участник', 'member');
    throwaway.push(member);
    const story = await typeId(page.request, organization.id, 'US');
    await createWorkItem(page.request, organization.id, project.id, {
      typeId: story,
      title: 'Вход по паролю',
    });
    const last = await createWorkItem(page.request, organization.id, project.id, {
      typeId: story,
      title: 'Выход',
    });

    const session = await openSession(browser, {
      username: member.username,
      returnPath: backlogAddress(organization.id, project.id, firstTeam.id),
    });
    try {
      const target = session.page;
      const rows = target
        .getByRole('table', { name: 'Бэклог' })
        .locator('tbody tr[data-identifier]');
      await expect(rows).toHaveCount(2);
      await awaitLoaded(target);

      // An administrator removes the member; the page is not told.
      await removeMember(page.request, organization.id, member.id);

      const listReads: string[] = [];
      const accessReads: string[] = [];
      target.on('request', (request) => {
        const url = new URL(request.url());
        if (request.method() !== 'GET') return;
        if (url.pathname === '/api/organizations') listReads.push(request.url());
        if (url.pathname === `/api/organizations/${organization.id}/access`) {
          accessReads.push(request.url());
        }
      });

      // The next action that reaches the API: the last row to the top.
      await target.getByRole('button', { name: `Ещё действия: ${last.identifier}` }).click();
      await target.getByRole('menuitem', { name: 'В начало' }).click();

      await expect(heading(target, 'Организация не найдена')).toBeVisible();
      // No dialog, no alert of its own, no content of the page.
      await expect(rows).toHaveCount(0);
      await expect(switcher(target).getByRole('link', { name: 'Нет организаций' })).toBeVisible();
      // The window in which a second read of either would have started.
      await target.waitForTimeout(1500);
      expect(listReads, 'organization list reads after the 404').toHaveLength(1);
      expect(accessReads, 'access reads after the 404').toHaveLength(1);
      await expect(heading(target, 'Организация не найдена')).toBeVisible();
    } finally {
      await session.close();
    }
  });

  test('29.6 a page opened by address without its code: «Доступ ограничен» for a member, the tab «Деактивированные» for an administrator (US-4)', async ({
    page,
    browser,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const { organization } = await fixtureOrganization(page);
    const member = await addEphemeralMember(page.request, organization.id, 'Участник', 'member');
    throwaway.push(member);

    const session = await openSession(browser, {
      username: member.username,
      returnPath: deactivatedProjectsAddress(organization.id),
    });
    try {
      const target = session.page;
      await expect(heading(target, 'Доступ ограничен')).toBeVisible();
      await expect(
        target.getByText(
          'У вас нет прав для просмотра этого раздела. Обратитесь к администратору, чтобы запросить доступ.',
          { exact: true },
        ),
      ).toBeVisible();
      // The page's own content is not there, and nothing was asked to build it.
      await expect(target.getByRole('table')).toHaveCount(0);
    } finally {
      await session.close();
    }

    await page.goto(projectsAddress(organization.id));
    await expect(page.getByRole('table', { name: 'Активные проекты' })).toBeVisible();
    await page.getByRole('tab', { name: 'Деактивированные' }).click();
    await expect(page).toHaveURL(
      new RegExp(`/organizations/${organization.id}/settings/projects/deactivated$`),
    );
    await expect(
      main(page).getByRole('heading', { level: 2, name: 'Деактивированных проектов нет' }),
    ).toBeVisible();
  });

  test('29.7 a switch from an organization the user administers to one they only read: no «Создать проект» on the second, at no frame (US-4)', async ({
    page,
    browser,
  }) => {
    await signIn(page, { returnPath: '/home' });
    // «Альфа» sorts before «Бета», so it is the organization the switcher opens first.
    const alpha = await createContext(page.request, {
      organization: runName('Альфа E2E'),
      project: 'Первый проект',
    });
    const beta = await createContext(page.request, {
      organization: runName('Бета E2E'),
      project: 'Второй проект',
    });
    created.push(alpha.organization.id, beta.organization.id);
    const user = await addEphemeralMember(page.request, alpha.organization.id, 'Двуликий', 'admin');
    throwaway.push(user);
    await addMember(page.request, beta.organization.id, user.id, 'viewer');

    const session = await openSession(browser, {
      username: user.username,
      returnPath: projectsAddress(alpha.organization.id),
    });
    try {
      const target = session.page;
      await expect(target.getByRole('table', { name: 'Активные проекты' })).toContainText(
        alpha.project.name,
      );
      await expect(target.getByRole('button', { name: 'Создать проект' }).first()).toBeVisible();

      // From here on, any frame that carries the second organization's address
      // and an administrator's «Создать проект» is recorded.
      await target.evaluate((betaId) => {
        const seen = window as unknown as { qaMarker?: string; qaLeak?: boolean };
        seen.qaMarker = 'kept';
        seen.qaLeak = false;
        const look = () => {
          if (!location.pathname.includes(betaId)) return;
          for (const button of document.querySelectorAll('button')) {
            if (button.textContent?.trim() === 'Создать проект') seen.qaLeak = true;
          }
        };
        new MutationObserver(look).observe(document.body, {
          childList: true,
          subtree: true,
          characterData: true,
          attributes: true,
        });
      }, beta.organization.id);

      await switcher(target)
        .getByRole('button', { name: /^Организация:/ })
        .click();
      await target.getByRole('menuitemradio', { name: beta.organization.name }).click();

      // «Проекты» of the second organization, with its project and without the command.
      await expect(target).toHaveURL(
        new RegExp(`/organizations/${beta.organization.id}/settings/projects$`),
      );
      await expect(target.getByRole('table', { name: 'Активные проекты' })).toContainText(
        beta.project.name,
      );
      await awaitLoaded(target);
      await expect(target.getByRole('button', { name: 'Создать проект' })).toHaveCount(0);
      const seen = await target.evaluate(() => {
        const record = window as unknown as { qaMarker?: string; qaLeak?: boolean };
        return { marker: record.qaMarker ?? null, leak: record.qaLeak ?? null };
      });
      expect(seen.marker, 'no reload').toBe('kept');
      expect(seen.leak, 'a frame of the second organization with the first one’s control').toBe(
        false,
      );
    } finally {
      await session.close();
    }
  });

  test('29.8 a failed access read shows the banner with «Повторить», never «Доступ ограничен»; after the retry the page and its controls show (US-4)', async ({
    page,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const { organization } = await fixtureOrganization(page);

    const isAccessRead = (url: URL) =>
      url.origin === new URL(env().apiUrl).origin &&
      /^\/api\/organizations\/[^/]+\/access$/.test(url.pathname);
    await page.route(isAccessRead, (route) => route.abort('connectionrefused'));
    await page.goto(projectsAddress(organization.id));

    // Not denied, not found: the canon error under the page's own title.
    await expect(heading(page, 'Проекты')).toBeVisible();
    const banner = page
      .getByRole('alert')
      .filter({ hasText: 'Не удалось загрузить права доступа' });
    await expect(banner).toBeVisible();
    await expect(banner.getByRole('button', { name: 'Повторить' })).toBeVisible();
    await expect(page.getByText('Доступ ограничен')).toHaveCount(0);
    await expect(page.getByRole('table', { name: 'Активные проекты' })).toHaveCount(0);

    await page.unroute(isAccessRead);
    await banner.getByRole('button', { name: 'Повторить' }).click();

    await expect(page.getByRole('table', { name: 'Активные проекты' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Создать проект' }).first()).toBeVisible();
    await expect(page.getByText('Не удалось загрузить права доступа')).toHaveCount(0);
  });
});
