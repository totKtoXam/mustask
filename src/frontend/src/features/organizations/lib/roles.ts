// The three organization roles and their words (mod-016 api-contract § 1.1,
// design § 3.6, § 2.4). The role is a closed set the server serves; the panel
// branches on codes, never on a role (frontend.md "Named values"), and uses a
// role only to name it: in «Роль», in a helper text and in the role-change
// notice.

export const ORGANIZATION_ROLES = ['admin', 'member', 'viewer'] as const;

export type OrganizationRole = (typeof ORGANIZATION_ROLES)[number];

/** The role's name in «Роль» and in the notices. */
export const ROLE_LABELS: Readonly<Record<OrganizationRole, string>> = {
  admin: 'Администратор',
  member: 'Участник',
  viewer: 'Читатель',
};

/** The helper text under «Роль» for the chosen role (design § 3.6). */
export const ROLE_DESCRIPTIONS: Readonly<Record<OrganizationRole, string>> = {
  admin:
    'Управляет организацией, участниками, проектами, командами и областями, работает с рабочими элементами.',
  member:
    'Просматривает всё и работает с рабочими элементами: создаёт, меняет, связывает и упорядочивает их.',
  viewer: 'Только просматривает, ничего не меняет.',
};

/**
 * The notice a re-read raises when it shows a role other than the one last
 * read for the organization (design § 2.4).
 */
export function roleChangeNotice(organizationName: string, role: OrganizationRole): string {
  return `Ваша роль в организации «${organizationName}» изменилась: «${ROLE_LABELS[role]}».`;
}
