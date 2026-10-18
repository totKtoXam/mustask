import { TeamFilter } from '../../workitems';
import { readProjects, type BoardScope, type SurfaceRead } from '../lib/scope';
import { FilterRow } from './FilterRow';
import { ProjectFilter } from './ProjectFilter';

/**
 * The filter row under the head of a backlog or a board, whatever it is the view of (mod-017
 * design § 6.2; feat-040 design § 4): «Команда» on the project's views, «Проект» on a team's.
 *
 * A team's row is drawn from the first frame — its field works while the first read is pending or
 * has failed — and it is drawn while the address carries a value, an invalid one included, so the
 * filter can be seen and cleared. It is not drawn when the team works on no active project and
 * nothing is filtered: there is nothing to filter by (design § 4.2).
 */
export function ViewFilterRow({
  organizationId,
  scope,
  filterIds,
  read,
  onChange,
}: {
  organizationId: string;
  scope: BoardScope;
  /** The chosen values of this view's dimension, as the address carries them. */
  filterIds: readonly string[];
  /** The view's read, once there is one: its `projects` are a team's options. */
  read: {
    data: SurfaceRead | undefined;
    /** The first read failed and nothing is shown. */
    failed: boolean;
    /** Reads the page again. */
    refetch(): void;
  };
  onChange(values: string[]): void;
}) {
  const active = filterIds.length > 0;
  if (scope.kind === 'project') {
    return (
      <FilterRow active={active} onClear={() => onChange([])}>
        <TeamFilter
          organizationId={organizationId}
          projectId={scope.projectId}
          value={filterIds}
          onChange={onChange}
        />
      </FilterRow>
    );
  }
  const projects = readProjects(read.data);
  if (!active && projects !== undefined && projects.length === 0) {
    return null;
  }
  return (
    <FilterRow active={active} onClear={() => onChange([])}>
      <ProjectFilter
        projects={projects}
        failed={read.failed}
        value={filterIds}
        onOpen={() => {
          if (read.failed) {
            read.refetch();
          }
        }}
        onChange={onChange}
      />
    </FilterRow>
  );
}
