// What a recoding table or a constant may point at: only what already exists in MUSTASK (BR-IE-7) —
// the active projects, the process's types and states, the active members, the organization's teams
// and the priority list. Read through the owning slices' public hooks, never their internals.
import { useMembers, useOrganizationTeams, useProjects } from '../../organizations';
import { useProcessDefinition } from '../../process';
import type { RecodingKind } from '../api/importsApi';

export interface ReferenceOption {
  /** The target id the template stores (a priority's number as text). */
  value: string;
  label: string;
  /** For a state: the type it belongs to; the option is that type's. */
  typeId?: string;
  /** For a state: the name of its type, so equal names of different types can be told apart. */
  group?: string;
}

export interface References {
  loading: boolean;
  failed: boolean;
  retry: () => void;
  options: (kind: RecodingKind, typeId?: string | null) => ReferenceOption[];
  types: ReferenceOption[];
}

const PRIORITIES: ReferenceOption[] = ['1', '2', '3', '4'].map((value) => ({
  value,
  label: value,
}));

export function useReferences(organizationId: string, enabled: boolean): References {
  const projects = useProjects(organizationId, 'active', { enabled });
  const process = useProcessDefinition(organizationId);
  const members = useMembers(organizationId, { enabled });
  const teams = useOrganizationTeams(organizationId);

  const reads = [projects, process, members, teams];
  const types: ReferenceOption[] = (process.data?.workItemTypes ?? []).map((type) => ({
    value: type.id,
    label: type.name,
  }));
  const lists: Record<RecodingKind, ReferenceOption[]> = {
    none: [],
    project: (projects.data ?? []).map((project) => ({ value: project.id, label: project.name })),
    type: types,
    state: (process.data?.workItemTypes ?? []).flatMap((type) =>
      type.states
        .filter((state) => state.category.code !== 'Removed')
        .map((state) => ({
          value: state.id,
          label: state.name,
          typeId: type.id,
          group: type.name,
        })),
    ),
    priority: PRIORITIES,
    assignee: (members.data ?? [])
      .filter((member) => member.user.state !== 'unknown')
      .map((member) => ({
        value: member.user.id,
        label: [member.user.displayName, member.user.email].filter(Boolean).join(' · '),
      })),
    team: (teams.data ?? []).map((team) => ({ value: team.id, label: team.name })),
  };
  return {
    loading: reads.some((read) => read.isPending && read.fetchStatus !== 'idle'),
    failed: reads.some((read) => read.isError),
    retry: () => reads.filter((read) => read.isError).forEach((read) => void read.refetch()),
    options: (kind, typeId) =>
      kind === 'state' && typeId
        ? lists.state.filter((option) => option.typeId === typeId)
        : lists[kind],
    types,
  };
}
