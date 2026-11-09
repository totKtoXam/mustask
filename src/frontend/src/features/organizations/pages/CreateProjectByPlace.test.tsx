import { afterEach, describe, expect, it } from 'vitest';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ADMIN_CODES, VIEWER_CODES } from '../../../test/organizationRoles';
import { servedObjectCodes } from '../testApi';
import { ids } from '../testFixtures';
import { openPage } from '../testMembersPage';

// mod-019 BR-SA-12, BR-SA-17, design § 7: «Создать проект» is offered where the caller may write — the
// places come from the served writable ids and `area.create`; with no place the command is absent.

const PROJECTS = `/organizations/${ids.alfa}/projects`;
const DEPARTMENT_PROJECTS = `/organizations/${ids.alfa}/areas/${ids.platformArea}/projects`;

/** A department manager: an organization reader who holds the area codes on the department. */
function asDepartmentManager(path: string, writable: string[]) {
  const opened = openPage({ path, role: 'viewer' });
  opened.data.writableAreaIds = writable;
  servedObjectCodes.current = [
    ...VIEWER_CODES,
    'organizations.area.write',
    'organizations.area.grant',
  ];
  servedObjectCodes.pinned = true;
  return opened;
}

afterEach(() => {
  servedObjectCodes.current = ADMIN_CODES;
  servedObjectCodes.pinned = false;
});

describe('mod-019 «Создать проект» by place', () => {
  it('is offered to a department manager on org «Проекты», offering their department only', async () => {
    asDepartmentManager(PROJECTS, [ids.platformArea]);

    await userEvent.click(await screen.findByRole('button', { name: 'Создать проект' }));
    const dialog = await screen.findByRole('dialog', { name: 'Новый проект' });

    expect(await within(dialog).findByRole('combobox', { name: /Расположение/ })).toHaveValue(
      'Платформа',
    );
  });

  it('is absent when the caller has no place at all', async () => {
    openPage({ path: PROJECTS, role: 'viewer' });

    await screen.findByRole('table', { name: 'Проекты' });

    expect(screen.queryByRole('button', { name: 'Создать проект' })).toBeNull();
  });

  it('stands on the department’s «Проекты» with the department preselected', async () => {
    asDepartmentManager(DEPARTMENT_PROJECTS, [ids.platformArea]);

    await userEvent.click(await screen.findByRole('button', { name: 'Создать проект' }));
    const dialog = await screen.findByRole('dialog', { name: 'Новый проект' });

    expect(await within(dialog).findByRole('combobox', { name: /Расположение/ })).toHaveValue(
      'Платформа',
    );
  });

  it('keeps the administrator’s «Верхний уровень» and every department', async () => {
    openPage({ path: PROJECTS });

    await userEvent.click(await screen.findByRole('button', { name: 'Создать проект' }));
    const dialog = await screen.findByRole('dialog', { name: 'Новый проект' });
    await userEvent.click(await within(dialog).findByRole('combobox', { name: /Расположение/ }));

    expect((await screen.findAllByRole('option')).map((option) => option.textContent)).toContain(
      'Платформа',
    );
  });
});
