import type { WorkContextSource } from '../../../shared/lib/workContext';

/**
 * Whether taking away the link between a department and a team resets what is chosen (BR-NV-17,
 * ADR-0032 § 5; D-4): both are chosen, and no project placed in the department and linked to the
 * team keeps them fitting. The shell then says «Выбор сброшен: …» and moves the page; the page that
 * took the link away says nothing of its own, as a project's «Команды» does for its own unlink.
 */
export function unlinkResetsDepartmentTeam(
  context: Pick<WorkContextSource, 'selection' | 'offers'>,
  organizationId: string,
  departmentId: string,
  teamId: string,
): boolean {
  const { selection, offers } = context;
  if (
    offers === undefined ||
    selection.organizationId !== organizationId ||
    selection.departmentId !== departmentId ||
    selection.teamId !== teamId
  ) {
    return false;
  }
  return !offers.projects.some(
    (project) => project.departmentId === departmentId && project.teamIds.includes(teamId),
  );
}
