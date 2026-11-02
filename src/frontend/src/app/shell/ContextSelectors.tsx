import { useMemo } from 'react';
import Box from '@mui/material/Box';
import { SearchMenu, type SearchMenuGroup, type SearchMenuOption } from '../../shared/ui';
import {
  SELECTOR_KINDS,
  applyChoice,
  offerFor,
  resetNotice,
  type FitData,
  type ObjectSelection,
  type SelectorKind,
  type SelectorOffer,
  type SelectorOption,
} from '../navigation/selectorFit';

/** The footer of the organization selector: it clears all four (ADR-0032 § 5). */
export const ALL_ORGANIZATIONS_ID = 'all-organizations';

const CAPTIONS: Record<SelectorKind | 'organization', string> = {
  organization: 'Организация',
  department: 'Департамент',
  team: 'Команда',
  project: 'Проект',
};

const SEARCH_LABEL = 'Найти…';
const EMPTY_TEXT = 'Ничего не найдено';
const ERROR_TEXT = 'Не удалось загрузить список. Попробуйте ещё раз.';
const RETRY_LABEL = 'Повторить';
const ALL_LABEL = 'все';
const OTHERS_HEADING = 'Остальные';
const OTHERS_NOTE = 'Выбор сбросит то, что не подойдёт.';

export interface ContextSelectorsProps {
  /** The user's active organizations, as the server orders them. */
  organizations: readonly { id: string; name: string }[];
  organizationId?: string;
  /** `selector-offers` of the chosen organization; absent while it loads or after it failed. */
  data?: FitData;
  dataStatus: 'ready' | 'loading' | 'error';
  onRetry: () => void;
  /** The organization list's own status; default `ready`. */
  organizationsStatus?: 'ready' | 'loading' | 'error';
  onRetryOrganizations?: () => void;
  selection: ObjectSelection;
  /** An organization was chosen: it opens its «Обзор» with «все» in the rest. */
  onOrganization: (organizationId: string) => void;
  /** «Все организации»: clears all four and opens the list. */
  onAllOrganizations: () => void;
  /** An object was chosen or reset; `notice` is «Выбор сброшен: …» or `null`. */
  onChange: (selection: ObjectSelection, notice: string | null) => void;
}

function toOption({ id, name, key }: SelectorOption): SearchMenuOption {
  return { id, label: name, hint: key };
}

function groupsOf(kind: SelectorKind, offer: SelectorOffer): SearchMenuGroup[] {
  const groups: SearchMenuGroup[] = [];
  if (kind === 'team') {
    const own = offer.fitting.filter((option) => option.isMember === true).map(toOption);
    const rest = offer.fitting.filter((option) => option.isMember !== true).map(toOption);
    if (own.length > 0) groups.push({ heading: 'Мои команды', options: own });
    if (rest.length > 0) {
      groups.push({ heading: own.length > 0 ? 'Другие команды' : undefined, options: rest });
    }
  } else {
    groups.push({ options: offer.fitting.map(toOption) });
  }
  if (offer.others.length > 0) {
    groups.push({
      heading: OTHERS_HEADING,
      note: OTHERS_NOTE,
      options: offer.others.map(toOption),
    });
  }
  return groups;
}

/**
 * The four header selectors (feat-043 design § 3, ADR-0032 § 5): organization, department,
 * team, project. Controlled: the host reads `selector-offers` once, owns the selection and
 * moves the address; this part judges fit and reset with `selectorFit` and words the notice.
 */
export function ContextSelectors({
  organizations,
  organizationId,
  data,
  dataStatus,
  onRetry,
  organizationsStatus = 'ready',
  onRetryOrganizations,
  selection,
  onOrganization,
  onAllOrganizations,
  onChange,
}: ContextSelectorsProps) {
  const organization = organizations.find(({ id }) => id === organizationId);
  const offers = useMemo(() => {
    if (!data) return null;
    return Object.fromEntries(
      SELECTOR_KINDS.map((kind) => [kind, offerFor(data, selection, kind)]),
    ) as Record<SelectorKind, SelectorOffer>;
  }, [data, selection]);

  function nameOf(kind: SelectorKind): string {
    const id = selection[kind];
    if (id === null || !data) return ALL_LABEL;
    const list: readonly { id: string; name: string }[] =
      kind === 'department' ? data.departments : kind === 'team' ? data.teams : data.projects;
    return list.find((item) => item.id === id)?.name ?? ALL_LABEL;
  }

  function choose(kind: SelectorKind, id: string | null) {
    if (!data) return;
    const result = applyChoice(data, selection, kind, id);
    onChange(result.selection, resetNotice(result.reset));
  }

  const organizationOptions: SearchMenuGroup[] = [
    { options: organizations.map(({ id, name }) => ({ id, label: name })) },
  ];

  return (
    <Box
      role="group"
      aria-label="Контекст работы"
      sx={{ display: 'flex', flexWrap: 'nowrap', alignItems: 'center', gap: 1.5, minWidth: 0 }}
    >
      <SearchMenu
        label={CAPTIONS.organization}
        value={organization?.name ?? 'Все организации'}
        chosen={organization !== undefined}
        currentId={organizationId ?? null}
        groups={organizationOptions}
        footer={{ id: ALL_ORGANIZATIONS_ID, label: 'Все организации' }}
        searchLabel={SEARCH_LABEL}
        emptyText={EMPTY_TEXT}
        status={organizationsStatus}
        errorText={ERROR_TEXT}
        retryLabel={RETRY_LABEL}
        onRetry={onRetryOrganizations}
        onSelect={(id) => {
          if (id === null) return;
          if (id === ALL_ORGANIZATIONS_ID) onAllOrganizations();
          else onOrganization(id);
        }}
      />
      {SELECTOR_KINDS.map((kind) => (
        <SearchMenu
          key={kind}
          label={CAPTIONS[kind]}
          value={nameOf(kind)}
          chosen={selection[kind] !== null}
          currentId={selection[kind]}
          allLabel={ALL_LABEL}
          groups={offers ? groupsOf(kind, offers[kind]) : []}
          searchLabel={SEARCH_LABEL}
          emptyText={EMPTY_TEXT}
          status={dataStatus}
          errorText={ERROR_TEXT}
          retryLabel={RETRY_LABEL}
          onRetry={onRetry}
          onSelect={(id) => choose(kind, id)}
        />
      ))}
    </Box>
  );
}
