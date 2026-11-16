// The iterations module over HTTP, strictly per docs/features/mod-007-iterations/api-contract.md
// § 1–§ 2: one function per route, camelCase as all of Mustask.Api. Ids come from the address or
// from an answer and are sent as received, re-encoded in the path and never validated (§ 0): a
// malformed one answers its level's not-found code. The item's own `sprint` (§ 3) is the work items
// slice's write; this slice only reads the sprints it can be set to.
import { apiFetch } from '../../../shared/api/httpClient';

/** § 1: a sprint's state, the string the API uses. */
export type SprintState = 'Planned' | 'Active' | 'Closed';

/** § 1 `Sprint`. */
export interface Sprint {
  id: string;
  teamId: string;
  number: number;
  name: string;
  state: SprintState;
  startDate: string | null;
  endDate: string | null;
  createdAt: string;
  createdBy: string;
  closedAt: string | null;
  closedBy: string | null;
}

/** § 2.1 `SprintRow`: `closedItems` and `closedStoryPoints` are `null` for a Planned or Active sprint. */
export interface SprintRow {
  id: string;
  number: number;
  name: string;
  state: SprintState;
  startDate: string | null;
  endDate: string | null;
  itemCount: number;
  storyPoints: number;
  closedItems: number | null;
  closedStoryPoints: number | null;
}

/** § 2.1's answer: highest number first, `nextBefore` is the cursor of the next page. */
export interface SprintPage {
  items: SprintRow[];
  nextBefore: number | null;
}

/** § 2.2, § 2.3's body: the name and the two dates, replacing all three on an edit. */
export interface SprintBody {
  name: string | null;
  startDate: string | null;
  endDate: string | null;
}

/** § 2.6: a count of items and their story points. */
export interface ItemTotal {
  items: number;
  storyPoints: number;
}

/** § 2.6: a Planned sprint the unfinished items can move to. */
export interface CloseDestination {
  id: string;
  name: string;
  startDate: string | null;
  endDate: string | null;
}

/** § 2.6. */
export interface ClosePreview {
  moving: ItemTotal;
  staying: ItemTotal;
  destinations: CloseDestination[];
}

/** § 2.8. */
export interface ClosedSprint {
  sprint: Sprint;
  closedByName: string | null;
  itemCount: number;
  storyPoints: number;
}

/** § 2.9: the three levels the outcome's figures are read at. */
export type OutcomeLevel = 'epics' | 'features' | 'stories';

/** § 1 `Figure`: display-ready; `null` is «—». */
export interface Figure {
  min: number | null;
  average: number | null;
  max: number | null;
}

/** The four figures of a summary or of a person's dynamics (§ 2.9). */
export interface FigureSet {
  closedItems: Figure;
  closedItemsPercent: Figure;
  closedStoryPoints: Figure;
  closedStoryPointsPercent: Figure;
}

/** § 2.9 `Row`. A percent is `null` when held is 0. */
export interface DynamicsRow {
  id: string;
  number: number;
  name: string;
  endDate: string | null;
  items: number;
  storyPoints: number;
  closedItems: number;
  closedItemsPercent: number | null;
  closedStoryPoints: number;
  closedStoryPointsPercent: number | null;
}

/** § 2.9 `GET …/dynamics`. */
export interface Dynamics {
  level: OutcomeLevel;
  from: string;
  to: string;
  rangeSprints: number;
  shownSprints: number;
  truncated: boolean;
  current: DynamicsRow;
  sprints: DynamicsRow[];
  summary: FigureSet;
}

/** § 2.9 `Participant`; `userId` is `null` on «Без исполнителя» and on the total. */
export interface Participant {
  userId: string | null;
  displayName: string | null;
  isMember: boolean;
  items: number;
  storyPoints: number;
  closedStoryPoints: number;
  closedStoryPointsPercent: number | null;
  closedItems: number;
  closedItemsPercent: number | null;
  comparedSprints: number;
  dynamics: FigureSet;
}

/** § 2.9 `GET …/participants`: 200 rows a page; `total` is the same on every page. */
export interface ParticipantsPage {
  level: OutcomeLevel;
  from: string;
  to: string;
  rangeSprints: number;
  rows: Participant[];
  total: Participant;
  totalRows: number;
}

/** § 2.9 `GET …/items`: a purged item has `title: null` and `purged: true`. */
export interface OutcomeItem {
  identifier: string;
  title: string | null;
  project: { id: string; name: string };
  storyPoints: number;
  purged: boolean;
}

export interface OutcomeItemsPage {
  items: OutcomeItem[];
  totalItems: number;
  totalStoryPoints: number;
}

/** The range the outcome's reads take; both or neither (§ 2.9). */
export interface OutcomeQuery {
  level: OutcomeLevel;
  from?: string;
  to?: string;
}

/** The page size of the list, the participants and the items (§ 2.1, § 2.9). */
export const SPRINT_PAGE_SIZE = 200;

const segment = encodeURIComponent;

/** `…/teams/{teamId}/sprints`, the base of every route. */
export function sprintsApiPath(organizationId: string, teamId: string): string {
  return `/api/organizations/${segment(organizationId)}/teams/${segment(teamId)}/sprints`;
}

function sprintApiPath(organizationId: string, teamId: string, sprintId: string): string {
  return `${sprintsApiPath(organizationId, teamId)}/${segment(sprintId)}`;
}

/** The query the outcome's page reads share (§ 2.9). */
export function outcomeQueryString(query: OutcomeQuery, offset?: number): string {
  const params = new URLSearchParams({ level: query.level });
  if (query.from && query.to) {
    params.set('from', query.from);
    params.set('to', query.to);
  }
  if (offset !== undefined) {
    params.set('offset', String(offset));
  }
  return params.toString();
}

/** § 2.1: one page of the list; `open` keeps Planned and Active. */
export function getSprints(
  organizationId: string,
  teamId: string,
  options: { before?: number | null; open?: boolean } = {},
): Promise<SprintPage> {
  const params = new URLSearchParams();
  if (options.before !== undefined && options.before !== null) {
    params.set('before', String(options.before));
  }
  if (options.open) {
    params.set('open', 'true');
  }
  const query = params.toString();
  return apiFetch(`${sprintsApiPath(organizationId, teamId)}${query ? `?${query}` : ''}`);
}

/** § 2.2. */
export function createSprint(
  organizationId: string,
  teamId: string,
  body: SprintBody,
): Promise<Sprint> {
  return apiFetch(sprintsApiPath(organizationId, teamId), { method: 'POST', body });
}

/** § 2.3. */
export function updateSprint(
  organizationId: string,
  teamId: string,
  sprintId: string,
  body: SprintBody,
): Promise<Sprint> {
  return apiFetch(sprintApiPath(organizationId, teamId, sprintId), { method: 'PUT', body });
}

/** § 2.4. */
export function startSprint(
  organizationId: string,
  teamId: string,
  sprintId: string,
): Promise<Sprint> {
  return apiFetch(`${sprintApiPath(organizationId, teamId, sprintId)}/start`, { method: 'POST' });
}

/** § 2.5. */
export function removeSprint(
  organizationId: string,
  teamId: string,
  sprintId: string,
): Promise<void> {
  return apiFetch(sprintApiPath(organizationId, teamId, sprintId), { method: 'DELETE' });
}

/** § 2.6. */
export function getClosePreview(
  organizationId: string,
  teamId: string,
  sprintId: string,
): Promise<ClosePreview> {
  return apiFetch(`${sprintApiPath(organizationId, teamId, sprintId)}/close-preview`);
}

/** § 2.7: `destinationSprintId: null` is «Без спринта». */
export function closeSprint(
  organizationId: string,
  teamId: string,
  sprintId: string,
  destinationSprintId: string | null,
): Promise<Sprint> {
  return apiFetch(`${sprintApiPath(organizationId, teamId, sprintId)}/close`, {
    method: 'POST',
    body: { destinationSprintId },
  });
}

/** § 2.8. */
export function getClosedSprint(
  organizationId: string,
  teamId: string,
  sprintId: string,
): Promise<ClosedSprint> {
  return apiFetch(sprintApiPath(organizationId, teamId, sprintId));
}

/** § 2.9. */
export function getDynamics(
  organizationId: string,
  teamId: string,
  sprintId: string,
  query: OutcomeQuery,
): Promise<Dynamics> {
  return apiFetch(
    `${sprintApiPath(organizationId, teamId, sprintId)}/dynamics?${outcomeQueryString(query)}`,
  );
}

/** § 2.9. */
export function getParticipants(
  organizationId: string,
  teamId: string,
  sprintId: string,
  query: OutcomeQuery,
  offset: number,
): Promise<ParticipantsPage> {
  return apiFetch(
    `${sprintApiPath(organizationId, teamId, sprintId)}/participants?${outcomeQueryString(query, offset)}`,
  );
}

/** § 2.9: the items read has no level or range of its own (the outcome stays whole, OQ-14). */
export function getOutcomeItems(
  organizationId: string,
  teamId: string,
  sprintId: string,
  offset: number,
): Promise<OutcomeItemsPage> {
  return apiFetch(`${sprintApiPath(organizationId, teamId, sprintId)}/items?offset=${offset}`);
}
