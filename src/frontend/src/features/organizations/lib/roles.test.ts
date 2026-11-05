import { describe, expect, it } from 'vitest';
import {
  ORGANIZATION_ROLES,
  ROLE_DESCRIPTIONS,
  ROLE_LABELS,
  roleChangeNotice,
  type OrganizationRole,
} from './roles';

describe('the organization roles (mod-016 design § 3.6, § 2.4)', () => {
  it('names the three roles as the design does', () => {
    expect(ORGANIZATION_ROLES).toEqual(['admin', 'member', 'viewer']);
    expect(ROLE_LABELS).toEqual({
      admin: 'Администратор',
      member: 'Участник',
      viewer: 'Читатель',
    });
  });

  it('describes each role in the words of the design, ending with a full stop', () => {
    expect(ROLE_DESCRIPTIONS.admin).toBe(
      'Управляет организацией, участниками, проектами, командами и областями, работает с рабочими элементами.',
    );
    expect(ROLE_DESCRIPTIONS.member).toBe(
      'Просматривает всё и работает с рабочими элементами: создаёт, меняет, связывает и упорядочивает их.',
    );
    expect(ROLE_DESCRIPTIONS.viewer).toBe('Только просматривает, ничего не меняет.');
  });

  it.each<[OrganizationRole, string]>([
    ['admin', 'Ваша роль в организации «Альфа» изменилась: «Администратор».'],
    ['member', 'Ваша роль в организации «Альфа» изменилась: «Участник».'],
    ['viewer', 'Ваша роль в организации «Альфа» изменилась: «Читатель».'],
  ])('says a change to %s', (role, text) => {
    expect(roleChangeNotice('Альфа', role)).toBe(text);
  });
});
