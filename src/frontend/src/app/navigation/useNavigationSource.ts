import { useCallback, useEffect, useMemo, useState } from 'react';
import { useLocation } from 'react-router-dom';
import {
  ORGANIZATIONS_PATH,
  useOrganizations,
  useSelectorOffers,
} from '../../features/organizations';
import type {
  WorkContextFallback,
  WorkContextOffers,
  WorkContextOrganization,
  WorkContextSelection,
  WorkContextSource,
  WorkContextStatus,
} from '../../shared/lib/workContext';
import {
  clearRemembered,
  isOrganizationCleared,
  readAddressSelection,
  readRemembered,
  readRememberedOrganization,
  resolveKnownId,
  writeRemembered,
  type KeyValueStore,
} from './selection';
import { NO_SELECTION, SELECTOR_KINDS, type ObjectSelection } from './selectorFit';

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
 * matches — and, on a page that carries no organization, from the remembered selection
 * (`localStorage`, per user and organization). An address that names an organization writes the
 * selection it carries; a remembered object that is gone drops to «все» with the fallback
 * notice, and so does an organization the user left. Choosing a value is the shell's business:
 * it moves the address, and the address is what this reads.
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
  });
  const offersStatus: WorkContextStatus =
    organizationId === undefined ? 'ready' : statusOf(offersQuery);
  const offers: WorkContextOffers | undefined =
    offersStatus === 'ready' && organizationId !== undefined ? offersQuery.data : undefined;

  // The chosen objects: the address's, or on a page with no organization the remembered ones.
  const carriesOrganization = address.organizationId !== undefined;
  const rememberedObjects =
    !carriesOrganization && store && organizationId !== undefined
      ? readRemembered(store, user, organizationId)
      : null;
  const chosen: ObjectSelection = carriesOrganization
    ? address.selection
    : (rememberedObjects ?? NO_SELECTION);
  const resolution = useMemo(
    () => (offers ? resolveAgainst(chosen, offers) : null),
    // `chosen` is a fresh object per render; its three ids are the dependencies.
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [offers, chosen.department, chosen.team, chosen.project],
  );
  const objects: ObjectSelection = resolution ? resolution.objects : chosen;
  const lost = resolution?.lost ?? NOTHING;
  const objectsKey = `${objects.department}|${objects.team}|${objects.project}`;
  const lostKey = lost.join(',');

  const [fallback, setFallback] = useState<WorkContextFallback | null>(null);
  const raise = useCallback((names: readonly string[]) => {
    setFallback((current) => ({ id: (current?.id ?? 0) + 1, names }));
  }, []);
  const organizationName = organizationList.find(({ id }) => id === organizationId)?.name;

  // Remembered: written when an address sets the selection, repaired when a page with none
  // reads a selection that no longer exists (ADR-0032 § 6).
  useEffect(() => {
    if (!store || organizationId === undefined) return;
    if (carriesOrganization) {
      // An address that names a remembered object that is gone drops it as well, with the notice.
      if (lostKey !== '') {
        const remembered = readRemembered(store, user, organizationId);
        const sameGone = SELECTOR_KINDS.some(
          (kind) =>
            lostKey.split(',').includes(kind) &&
            remembered?.[kind] !== null &&
            remembered?.[kind]?.toLowerCase() === address.selection[kind]?.toLowerCase(),
        );
        // The notice is an event of the external store (the remembered selection), not derived state.
        // eslint-disable-next-line react-hooks/set-state-in-effect
        if (sameGone) raise(organizationName ? [organizationName] : []);
      }
      if (offers || objectsKey === 'null|null|null') {
        writeRemembered(store, user, organizationId, objects);
      }
      return;
    }
    if (organizationGone) {
      raise(organizationName ? [organizationName] : []);
      writeRemembered(store, user, organizationId, NO_SELECTION);
      return;
    }
    if (lostKey !== '') {
      raise(organizationName ? [organizationName] : []);
      writeRemembered(store, user, organizationId, objects);
    }
    // `objects` and `address` are read through their keys.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [
    store,
    user,
    organizationId,
    carriesOrganization,
    objectsKey,
    lostKey,
    organizationGone,
    offers,
  ]);

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
      departmentId: objects.department ?? undefined,
      teamId: objects.team ?? undefined,
      projectId: objects.project ?? undefined,
    }),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [organizationId, objectsKey],
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
    ],
  );
}
