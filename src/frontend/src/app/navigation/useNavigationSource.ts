import { useCallback, useEffect, useMemo, useState } from 'react';
import { matchPath, useLocation } from 'react-router-dom';
import {
  ORGANIZATIONS_PATH,
  notFoundLevel,
  useArea,
  useOrganizations,
  useProjectAreaTree,
  useSelectorOffers,
} from '../../features/organizations';
import type {
  WorkContextObjectKind,
  WorkContextObjectLink,
  WorkContextFallback,
  WorkContextObjects,
  WorkContextOffers,
  WorkContextOrganization,
  WorkContextSelection,
  WorkContextSource,
  WorkContextStatus,
} from '../../shared/lib/workContext';
import { KIND_ROUTE_PREFIX, SELECTOR_QUERY, menuFor } from './addressModel';
import {
  menuHref,
  clearRemembered,
  isOrganizationCleared,
  namesObjects,
  readAddressSelection,
  readRemembered,
  readRememberedOrganization,
  resolveKnownId,
  writeRemembered,
  writeRememberedOrganization,
  type KeyValueStore,
} from './selection';
import {
  NO_SELECTION,
  SELECTOR_KINDS,
  openObject,
  pairFits,
  repairUnfit,
  resetNotice,
  resetUnfitted,
  type ObjectSelection,
  type SelectorKind,
} from './selectorFit';

function decodeId(value: string): string {
  try {
    return decodeURIComponent(value);
  } catch {
    return value;
  }
}

export interface NavigationSourceOptions {
  /** `false` while a test hands the shell its own source: nothing is read or stored. */
  enabled: boolean;
  /** The token's `sub`: the remembered selection's key (ADR-0032 § 6). */
  subject?: string;
}

const NOTHING: readonly never[] = [];

function storeOf(): KeyValueStore | null {
  try {
    return window.localStorage;
  } catch {
    return null;
  }
}

function statusOf(query: {
  isSuccess: boolean;
  isError: boolean;
  isFetching: boolean;
}): WorkContextStatus {
  if (query.isSuccess) return 'ready';
  return query.isError && !query.isFetching ? 'failed' : 'loading';
}

/** The ids the offers hold, per selector. */
function knownIds(data: WorkContextOffers): Record<(typeof SELECTOR_KINDS)[number], string[]> {
  return {
    department: data.departments.map(({ id }) => id),
    team: data.teams.map(({ id }) => id),
    project: data.projects.map(({ id }) => id),
  };
}

/** Each chosen id spelled as the offers spell it (tech-009 F4); the ones they do not hold are lost. */
function resolveAgainst(
  chosen: ObjectSelection,
  data: WorkContextOffers,
): { objects: ObjectSelection; lost: readonly (typeof SELECTOR_KINDS)[number][] } {
  const known = knownIds(data);
  const objects: ObjectSelection = { ...NO_SELECTION };
  const lost: (typeof SELECTOR_KINDS)[number][] = [];
  for (const kind of SELECTOR_KINDS) {
    const id = chosen[kind];
    if (id === null) continue;
    const resolved = resolveKnownId(id, known[kind]);
    if (resolved === null) lost.push(kind);
    else objects[kind] = resolved;
  }
  return { objects, lost };
}

/**
 * The shell's context source (feat-043: ADR-0032 §§ 1, 5, 6; US-8). It reads the user's active
 * organizations and the effective organization's `selector-offers` once each, takes the selection
 * from the address — the object of the prefix, the others from the query, matched as the router
 * matches — and, when the address names no object (a page with no organization, or an
 * organization's own page such as «Участники»), from the remembered selection (`localStorage`, per
 * user and organization). The remembered selection is written when an address names objects or
 * the user chooses (`remember`, «все» too), never by a visit to a page that names none. A
 * remembered object that is gone drops to «все» with the fallback notice, and so does an
 * organization the user left; a selector in the query that names nothing, or does not fit the
 * prefix's object, is `selectorNotFound`. Choosing a value is the shell's business: it moves the
 * address, and the address is what this reads.
 */
export function useNavigationSource({
  enabled,
  subject,
}: NavigationSourceOptions): WorkContextSource {
  const { pathname, search } = useLocation();
  const store = enabled && subject ? storeOf() : null;
  const user = subject ?? '';

  const organizationsQuery = useOrganizations('active', { enabled, refetchOnWindowFocus: true });
  const organizationsStatus = statusOf(organizationsQuery);
  const organizationList =
    organizationsStatus === 'ready' ? (organizationsQuery.data ?? NOTHING) : NOTHING;

  const address = useMemo(() => readAddressSelection(pathname, search), [pathname, search]);

  // The effective organization: the address's, else the remembered one, else the first.
  let organizationId: string | undefined;
  let organizationGone = false;
  if (organizationsStatus === 'ready') {
    const ids = organizationList.map(({ id }) => id);
    if (address.organizationId !== undefined) {
      organizationId = resolveKnownId(address.organizationId, ids) ?? undefined;
    } else {
      const remembered = store ? readRememberedOrganization(store, user) : null;
      const kept = remembered === null ? null : resolveKnownId(remembered, ids);
      if (kept !== null) {
        organizationId = kept;
      } else if (!(store && isOrganizationCleared(store, user))) {
        organizationId = organizationList[0]?.id;
        organizationGone = remembered !== null;
      }
    }
  }

  const offersQuery = useSelectorOffers(organizationId ?? '', {
    enabled: enabled && organizationId !== undefined,
    // Another user's link change reaches an open tab on its next focus (ADR-0032 § 5).
    refetchOnWindowFocus: true,
  });
  const offersStatus: WorkContextStatus =
    organizationId === undefined ? 'ready' : statusOf(offersQuery);
  const offers: WorkContextOffers | undefined =
    offersStatus === 'ready' && organizationId !== undefined ? offersQuery.data : undefined;

  // The offers read before these ones, for the same organization: a link change (a team linked or
  // unlinked, a project moved) is a change from them (ADR-0032 § 5). `undefined` is the first read.
  const [offerTrack, setOfferTrack] = useState<{
    organizationId: string | undefined;
    offers: WorkContextOffers | undefined;
    previous: WorkContextOffers | undefined;
  }>({ organizationId: undefined, offers: undefined, previous: undefined });
  if (offerTrack.offers !== offers || offerTrack.organizationId !== organizationId) {
    setOfferTrack({
      organizationId,
      offers,
      previous:
        offerTrack.organizationId === organizationId && offerTrack.offers && offers
          ? offerTrack.offers
          : undefined,
    });
  }
  const previousOffers =
    offerTrack.offers === offers && offerTrack.organizationId === organizationId
      ? offerTrack.previous
      : undefined;

  // Bumped when the shell writes a choice (`remember`), so a page that reads the remembered
  // selection reads it again.
  const [, setReads] = useState(0);

  // `areas/:areaId` is a department, or a project's node or a sub-area (ADR-0032 § 2): the
  // offers hold the departments only, so any other id is read as the node it is.
  const areaPrefixId = useMemo(() => {
    const params: Record<string, string | undefined> | undefined = matchPath(
      { path: `${KIND_ROUTE_PREFIX.department}/*`, end: false },
      pathname,
    )?.params;
    const value = params?.areaId;
    return value === undefined || value === '' ? undefined : decodeId(value);
  }, [pathname]);
  const isDepartmentId =
    offers !== undefined &&
    areaPrefixId !== undefined &&
    resolveKnownId(
      areaPrefixId,
      offers.departments.map(({ id }) => id),
    ) !== null;
  const nodePrefix = offers !== undefined && areaPrefixId !== undefined && !isDepartmentId;
  const nodeQuery = useArea(organizationId ?? '', areaPrefixId ?? '', {
    enabled: enabled && nodePrefix,
  });
  const nodeProjectId = nodeQuery.isSuccess ? nodeQuery.data.projectId : null;
  const nodeId = nodeQuery.isSuccess && nodeProjectId !== null ? areaPrefixId : undefined;
  // Until the node is read, nothing is known of it (it may be a department that is gone).
  const nodePending = nodePrefix && !nodeQuery.isSuccess && !nodeQuery.isError;
  const nodeGone = nodePrefix && nodeQuery.isError && notFoundLevel(nodeQuery.error) === 'area';
  const placed =
    nodePrefix && nodeProjectId !== null
      ? offers.projects.find(({ id }) => id.toLowerCase() === nodeProjectId.toLowerCase())
      : undefined;
  // A node that is read but not offered (its project is deactivated) or failed to read: the
  // page answers for it, and the visit changes no context.
  const nodeUnoffered =
    nodePrefix && !nodeGone && (nodeQuery.isError || nodeQuery.isSuccess) && !placed;

  // The chosen objects: the address's; an organization's page with no object in its address
  // reads the remembered ones (ADR-0032 § 6; US-8), and so does a page with no organization.
  const carriesOrganization = address.organizationId !== undefined;
  const addressNamesObjects = carriesOrganization && namesObjects(address.selection);
  // Read in render, not once: a tab on a page that names no object follows another tab's last
  // write on its next render — harmless, the address still wins wherever it names an object.
  const storedObjects =
    store && organizationId !== undefined ? readRemembered(store, user, organizationId) : null;
  const rememberedObjects = addressNamesObjects ? null : storedObjects;
  const chosen: ObjectSelection = useMemo(() => {
    const base = addressNamesObjects ? address.selection : (rememberedObjects ?? NO_SELECTION);
    if (!addressNamesObjects || areaPrefixId === undefined || isDepartmentId || !offers) {
      return base;
    }
    // A node or sub-area: its project (and the project's department) is the selection; until
    // the node is read, or when it is gone, the department of the prefix is no department.
    return {
      ...base,
      department: placed?.departmentId ?? null,
      project: placed ? placed.id : null,
    };
  }, [
    addressNamesObjects,
    address,
    areaPrefixId,
    isDepartmentId,
    offers,
    placed,
    rememberedObjects,
  ]);

  const resolution = useMemo(
    () => (offers ? resolveAgainst(chosen, offers) : null),
    // `chosen` is a fresh object per render; its three ids are the dependencies.
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [offers, chosen.department, chosen.team, chosen.project],
  );
  const objects: ObjectSelection = resolution ? resolution.objects : chosen;
  // A department prefix that names no department and no readable node is a department that is gone.
  const lost: readonly SelectorKind[] = nodeGone
    ? [...(resolution?.lost ?? NOTHING), 'department']
    : (resolution?.lost ?? NOTHING);
  const lostKey = lost.join(',');

  // An address that names an object that is gone, or a node that is gone, not offered or still
  // being read, changes no context (BR-NV-7; US-3): the selectors keep the remembered selection
  // while the page answers for the address (D-8).
  const addressHeldBack =
    offers !== undefined &&
    addressNamesObjects &&
    (lost.length > 0 || nodePending || nodeUnoffered);
  const heldBackObjects = useMemo(
    () =>
      offers && addressHeldBack
        ? resolveAgainst(storedObjects ?? NO_SELECTION, offers).objects
        : null,
    // The stored selection is read afresh each render; its three ids are the dependencies.
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [
      offers,
      addressHeldBack,
      storedObjects?.department,
      storedObjects?.team,
      storedObjects?.project,
    ],
  );
  // A remembered selection met with offers for the first time (the tab was closed over a link
  // change): the more specific value of a pair that no longer fits drops, with the fallback
  // notice, so no menu entry carries a pair its address answers not found for (D-5).
  const repair = useMemo(
    () =>
      offers && resolution && !addressNamesObjects && previousOffers === undefined
        ? repairUnfit(offers, resolution.objects)
        : null,
    [offers, resolution, addressNamesObjects, previousOffers],
  );
  const unfitKey = repair ? repair.reset.map(({ kind }) => kind).join(',') : '';
  // What the selectors show and the menu is built from.
  const shown: ObjectSelection =
    heldBackObjects ?? (repair && repair.reset.length > 0 ? repair.selection : objects);
  const shownKey = `${shown.department}|${shown.team}|${shown.project}`;

  // A link change that unfits what is shown is the shell's to reset, not an address that never
  // fitted: the pair is not `selectorNotFound` for the offers that changed it (D-4).
  const linkReset = useMemo(
    () => (previousOffers && offers ? resetUnfitted(previousOffers, offers, shown) : null),
    // `shown` is read through its key.
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [previousOffers, offers, shownKey],
  );
  const linkResetKinds: readonly SelectorKind[] = linkReset
    ? linkReset.reset.map(({ kind }) => kind)
    : NOTHING;
  const linkResetKey = linkResetKinds.join(',');

  // «Структура» of the chosen project: its node, from the project's own subtree (an existing
  // read, shared with the project's pages), because the offers carry no node id.
  const structureProjectId = shown.project;
  const treeQuery = useProjectAreaTree(organizationId ?? '', structureProjectId ?? '', {
    enabled: enabled && organizationId !== undefined && structureProjectId !== null,
  });
  const structureId = treeQuery.data?.find((node) => node.type === 'project')?.id;

  // A selector in the query that names nothing, or does not fit the object of the prefix, answers
  // not found for its level, the menu kept (ADR-0032 §§ 1, 3). The object of the prefix answers
  // first, on its own page.
  const selectorNotFound: SelectorKind | null = (() => {
    if (!offers || !addressNamesObjects || nodePending || nodeGone) return null;
    const prefix = address.prefixKind;
    if (prefix !== null && !nodePrefix && lost.includes(prefix)) return null;
    let primary: { kind: SelectorKind; id: string } | null = null;
    if (nodePrefix) {
      primary = placed ? { kind: 'project', id: placed.id } : null;
    } else if (prefix !== null && objects[prefix] !== null) {
      primary = { kind: prefix, id: objects[prefix] };
    }
    for (const kind of SELECTOR_KINDS) {
      if (kind === prefix || address.selection[kind] === null) continue;
      if (lost.includes(kind)) return kind;
      const id = objects[kind];
      if (primary && id !== null && !pairFits(offers, primary.kind, primary.id, kind, id)) {
        // A pair that fitted the offers before these and not these is a link change, which the
        // shell resets with its notice; it is no address that never fitted (D-4).
        if (linkResetKinds.includes(kind) || linkResetKinds.includes(primary.kind)) continue;
        return kind;
      }
    }
    return null;
  })();

  const [fallback, setFallback] = useState<WorkContextFallback | null>(null);
  const raise = useCallback((names: readonly string[]) => {
    setFallback((current) => ({ id: (current?.id ?? 0) + 1, names }));
  }, []);
  const organizationName = organizationList.find(({ id }) => id === organizationId)?.name;

  // Remembered (ADR-0032 § 6): written when an address names objects or the user chooses (see
  // `remember`), never by a visit to a page that names none; repaired when the remembered
  // selection holds an object that no longer exists or a pair that no longer fits (D-5).
  useEffect(() => {
    if (!store || organizationId === undefined) return;
    if (carriesOrganization) {
      if (!addressNamesObjects) {
        // An organization's page with no object: it reads the remembered selection and leaves it.
        writeRememberedOrganization(store, user, organizationId);
        if (lostKey !== '' || unfitKey !== '') {
          // The notice is an event of the external store (the remembered selection), not derived state.
          // eslint-disable-next-line react-hooks/set-state-in-effect
          raise(organizationName ? [organizationName] : []);
          writeRemembered(store, user, organizationId, shown);
        }
        return;
      }
      // A node that is still being read, or answers for itself: the visit changes nothing.
      if (nodePending || nodeUnoffered) return;
      // An address that names a remembered object that is gone drops that one, with the notice,
      // and keeps the other remembered values. An address that names some other object that is
      // gone writes nothing: one bad link must not change the context the user keeps (QA F-1).
      if (lostKey !== '') {
        const remembered = readRemembered(store, user, organizationId);
        const gone = SELECTOR_KINDS.filter(
          (kind) =>
            lost.includes(kind) &&
            remembered?.[kind] != null &&
            remembered[kind].toLowerCase() === address.selection[kind]?.toLowerCase(),
        );
        if (remembered && gone.length > 0) {
          const kept: ObjectSelection = { ...remembered };
          for (const kind of gone) kept[kind] = null;
          raise(organizationName ? [organizationName] : []);
          writeRemembered(store, user, organizationId, kept);
        }
        return;
      }
      if (selectorNotFound !== null) return;
      // A link change that unfits the pair is reset by the shell, which writes the result.
      if (linkResetKey !== '') return;
      if (offers) writeRemembered(store, user, organizationId, shown);
      return;
    }
    if (organizationGone) {
      raise(organizationName ? [organizationName] : []);
      writeRemembered(store, user, organizationId, NO_SELECTION);
      return;
    }
    if (lostKey !== '' || unfitKey !== '') {
      raise(organizationName ? [organizationName] : []);
      writeRemembered(store, user, organizationId, shown);
    }
    // `shown` and `address` are read through their keys.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [
    store,
    user,
    organizationId,
    carriesOrganization,
    addressNamesObjects,
    shownKey,
    lostKey,
    unfitKey,
    linkResetKey,
    organizationGone,
    nodePending,
    nodeUnoffered,
    selectorNotFound,
    offers,
  ]);

  // The user chose (a selector, «все», another organization): the choice is remembered, so an
  // address with no object reads it back.
  const remember = useCallback(
    (forOrganization: string, chosenObjects: WorkContextObjects) => {
      if (!store) return;
      writeRemembered(store, user, forOrganization, chosenObjects);
      setReads((count) => count + 1);
    },
    [store, user, setReads],
  );

  // No organization left to fall back to: the notice says only that the context is gone.
  const noneLeft =
    enabled && organizationsStatus === 'ready' && organizationList.length === 0 && store !== null;
  const hadRemembered =
    noneLeft && !carriesOrganization && readRememberedOrganization(store, user) !== null;
  useEffect(() => {
    if (hadRemembered && store) {
      // The notice is an event of the external store (the remembered selection), not derived state.
      // eslint-disable-next-line react-hooks/set-state-in-effect
      raise([]);
      clearRemembered(store, user, null);
    }
  }, [hadRemembered, store, user, raise]);

  const clearOrganizations = useCallback(() => {
    if (store) clearRemembered(store, user, organizationId ?? null);
  }, [store, user, organizationId]);

  const dismissFallback = useCallback(() => setFallback(null), []);
  const refetchOrganizations = organizationsQuery.refetch;
  const refetchOffers = offersQuery.refetch;
  const retry = useCallback(() => void refetchOrganizations(), [refetchOrganizations]);
  const retryOffers = useCallback(() => void refetchOffers(), [refetchOffers]);

  const selection = useMemo<WorkContextSelection>(
    () => ({
      organizationId,
      departmentId: shown.department ?? undefined,
      teamId: shown.team ?? undefined,
      projectId: shown.project ?? undefined,
      structureId: shown.project !== null ? structureId : undefined,
      nodeId,
    }),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [organizationId, shownKey, structureId, nodeId],
  );

  // Where a row or a link to an object leads (D-3): the object becomes the most specific one.
  const objectLink = useCallback(
    (kind: WorkContextObjectKind, id: string, nodeId?: string): WorkContextObjectLink | null => {
      if (organizationId === undefined || !offers) return null;
      const opened = openObject(offers, shown, kind, id);
      const first = menuFor(kind)[0];
      if (!first) return null;
      // A node or sub-area of the project: its own page, on the area prefix, which the source
      // reads as the project and its place; the fitting team rides in the query (BR-NV-19, D-6).
      if (nodeId !== undefined) {
        const node = menuHref(
          organizationId,
          { ...NO_SELECTION, department: nodeId },
          { segment: 'overview' },
        );
        const team = opened.selection.team;
        return {
          to: team ? `${node}?${SELECTOR_QUERY.team}=${encodeURIComponent(team)}` : node,
          notice: resetNotice(opened.reset),
        };
      }
      return {
        to: menuHref(organizationId, opened.selection, first),
        notice: resetNotice(opened.reset),
      };
    },
    // `shown` is read through its key.
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [organizationId, offers, shownKey],
  );

  const tree = useMemo<WorkContextOrganization[]>(
    () =>
      organizationList.map(({ id, name }) => ({
        id,
        name,
        projects:
          id === organizationId && offers
            ? offers.projects.map((project) => ({ id: project.id, name: project.name }))
            : [],
        teams:
          id === organizationId && offers
            ? offers.teams.map((team) => ({
                id: team.id,
                name: team.name,
                isMember: team.isMember,
              }))
            : [],
      })),
    [organizationList, organizationId, offers],
  );

  return useMemo<WorkContextSource>(
    () => ({
      status: organizationsStatus,
      levelStatus: { project: offersStatus, team: offersStatus },
      organizations: tree,
      selection,
      offers,
      isReal: true,
      organizationListPath: ORGANIZATIONS_PATH,
      retry,
      retryOffers,
      clearOrganizations,
      fallback,
      dismissFallback,
      objectLink,
      selectorNotFound,
      remember,
    }),
    [
      organizationsStatus,
      offersStatus,
      tree,
      selection,
      offers,
      retry,
      retryOffers,
      clearOrganizations,
      fallback,
      dismissFallback,
      objectLink,
      selectorNotFound,
      remember,
    ],
  );
}
