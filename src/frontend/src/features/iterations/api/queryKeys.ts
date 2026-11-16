// TanStack Query keys of the slice, `['iterations', <organization>, <team>, <entity>, …]`
// (frontend.md "Data and state"). Every write of a team's sprints reads the team's lists and
// options again by the team prefix.
import type { OutcomeQuery } from './iterationsApi';

const ROOT = 'iterations';

export const iterationsKeys = {
  all: [ROOT] as const,
  /** Everything of one team: the list, the options, the close preview, the outcome. */
  team: (organizationId: string, teamId: string) => [ROOT, organizationId, teamId] as const,
  list: (organizationId: string, teamId: string) => [ROOT, organizationId, teamId, 'list'] as const,
  /** The card's and the close dialog's options: the team's Active and Planned sprints. */
  open: (organizationId: string, teamId: string) => [ROOT, organizationId, teamId, 'open'] as const,
  preview: (organizationId: string, teamId: string, sprintId: string) =>
    [ROOT, organizationId, teamId, 'preview', sprintId] as const,
  closed: (organizationId: string, teamId: string, sprintId: string) =>
    [ROOT, organizationId, teamId, 'closed', sprintId] as const,
  dynamics: (organizationId: string, teamId: string, sprintId: string, query: OutcomeQuery) =>
    [ROOT, organizationId, teamId, 'dynamics', sprintId, query] as const,
  participants: (organizationId: string, teamId: string, sprintId: string, query: OutcomeQuery) =>
    [ROOT, organizationId, teamId, 'participants', sprintId, query] as const,
  items: (organizationId: string, teamId: string, sprintId: string) =>
    [ROOT, organizationId, teamId, 'items', sprintId] as const,
};
