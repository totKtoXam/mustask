import { useState } from 'react';
import { Link as RouterLink } from 'react-router-dom';
import Box from '@mui/material/Box';
import Link from '@mui/material/Link';
import Typography from '@mui/material/Typography';
import { ConfirmDialog, FormDialog, InlineNotice } from '../../../shared/ui';
import { useFocusInvalid } from '../hooks/useFocusInvalid';
import { useAreaTree, useOrganizationTeams, useProjects } from '../hooks/queries';
import { areaLinkDialog, areaUnlinkDialog, type AreaLinkEnd } from '../lib/areaTexts';
import { placeRefusal, type RefusalField } from '../lib/refusals';
import { organizationAreasPath, organizationTeamsPath } from '../paths';
import { LocalMultiPicker } from './AreaPickers';

/** One thing the dialog offers: an area (by its path), a team, or a project (with its key). */
export interface AreaLinkOption {
  id: string;
  name: string;
  /** A project's key, shown beside its name. */
  key?: string;
}

/** The field a chosen end's refusal lands on, and the section a «none exist» notice points at. */
const END = {
  areaProjects: { field: 'projects', section: null },
  areaTeams: { field: 'teams', section: 'teams' },
  projectAreas: { field: 'areas', section: 'areas' },
  teamAreas: { field: 'areas', section: 'areas' },
} as const satisfies Record<
  AreaLinkEnd,
  { field: RefusalField; section: 'teams' | 'areas' | null }
>;

/**
 * «Добавление … » (mod-018 design § 6.1): four ends, one component. The options are the organization's
 * active projects, active teams or areas (by path), read when the dialog opens, minus the ones the page
 * lists as linked; typed text narrows them locally (D-1). «Добавить» waits for a choice. Every state of
 * the list is named: loading, a failed read («Повторить»), all linked, nothing matches, none exist (an
 * information notice, «Добавить» unavailable). A chosen end that is gone lands under the field, the
 * options are read again and a choice no longer offered drops; the page's own end gone is the alert.
 */
export function AreaLinkDialog({
  end,
  organizationId,
  ownName,
  linkedIds,
  submit,
  onSucceeded,
  onClose,
}: {
  end: AreaLinkEnd;
  organizationId: string;
  /** The page's own end: the area's, the project's or the team's name. */
  ownName: string;
  /** What the page lists as linked already. */
  linkedIds: readonly string[];
  submit(ids: string[]): Promise<unknown>;
  onSucceeded(chosen: AreaLinkOption[]): void;
  /** `contextGone`: the page's own end answered not found; the page reads itself again. */
  onClose(contextGone: boolean): void;
}) {
  const texts = areaLinkDialog[end];
  const projects = useProjects(organizationId, 'active', {
    enabled: end === 'areaProjects',
    fresh: true,
  });
  const teams = useOrganizationTeams(organizationId, { enabled: end === 'areaTeams', fresh: true });
  const areas = useAreaTree(organizationId, {
    enabled: end === 'projectAreas' || end === 'teamAreas',
    fresh: true,
  });
  const query = end === 'areaProjects' ? projects : end === 'areaTeams' ? teams : areas;
  const [chosen, setChosen] = useState<AreaLinkOption[]>([]);
  const [fieldError, setFieldError] = useState<string | undefined>(undefined);
  const [alert, setAlert] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [contextGone, setContextGone] = useState(false);
  const focusInvalid = useFocusInvalid();

  const everyone: AreaLinkOption[] =
    end === 'areaProjects'
      ? (projects.data ?? []).map(({ id, name, key }) => ({ id, name, key }))
      : end === 'areaTeams'
        ? (teams.data ?? []).map(({ id, name }) => ({ id, name }))
        : (areas.data ?? []).map(({ id, path }) => ({ id, name: path }));
  const options = everyone.filter((option) => !linkedIds.includes(option.id));
  const failed = query.isError && !query.isFetching;
  const noneExist = query.isSuccess && everyone.length === 0;
  const { field, section } = END[end];

  async function handleSubmit() {
    setBusy(true);
    setAlert(null);
    setFieldError(undefined);
    try {
      await submit(chosen.map((option) => option.id));
      onSucceeded(chosen);
    } catch (error) {
      const placed = placeRefusal(error, [field]);
      setFieldError(placed.fields[field]);
      setAlert(placed.alert);
      setContextGone((gone) => gone || placed.contextGone);
      setBusy(false);
      focusInvalid();
      if (placed.fields[field] !== undefined) {
        // The options are read again, and a choice no longer offered drops (design § 6.1).
        void query.refetch().then((result) => {
          const offered = result.data as { id: string }[] | undefined;
          if (offered) {
            setChosen((current) =>
              current.filter((option) => offered.some((candidate) => candidate.id === option.id)),
            );
          }
        });
      }
    }
  }

  return (
    <FormDialog
      open
      title={texts.title}
      submitLabel={areaLinkDialog.submit}
      busy={busy}
      error={alert}
      submitDisabled={chosen.length === 0 || failed || noneExist}
      onSubmit={() => void handleSubmit()}
      onClose={() => onClose(contextGone)}
    >
      <Typography variant="body2" sx={{ color: 'text.secondary' }}>
        {texts.text(ownName)}
      </Typography>
      {failed ? (
        <InlineNotice
          severity="error"
          action={{ label: areaLinkDialog.retry, onClick: () => void query.refetch() }}
        >
          {texts.loadFailed}
        </InlineNotice>
      ) : noneExist ? (
        <InlineNotice severity="info">
          {section === null ? (
            texts.noneExist
          ) : (
            <>
              {texts.noneExist}
              <Link
                component={RouterLink}
                to={
                  section === 'teams'
                    ? organizationTeamsPath(organizationId)
                    : organizationAreasPath(organizationId)
                }
              >
                {'sectionName' in texts ? texts.sectionName : ''}
              </Link>
              {'.'}
            </>
          )}
        </InlineNotice>
      ) : (
        <LocalMultiPicker<AreaLinkOption>
          label={texts.label}
          required
          busy={busy}
          error={fieldError}
          options={options}
          loading={query.isPending}
          emptyText={texts.allLinked}
          value={chosen}
          onChange={(next) => {
            setChosen(next);
            setFieldError(undefined);
          }}
          getKey={(option) => option.id}
          getLabel={(option) => option.name}
          matchTexts={(option) => (option.key ? [option.key] : [])}
          renderOption={(option) => (
            <Box sx={{ display: 'flex', gap: 1, minWidth: 0, alignItems: 'baseline' }}>
              <Box component="span">{option.name}</Box>
              {option.key && (
                <Typography variant="caption" sx={{ color: 'text.secondary' }}>
                  {option.key}
                </Typography>
              )}
            </Box>
          )}
        />
      )}
    </FormDialog>
  );
}

/** The four unlinks of design § 6.2, each from the end whose page it is. */
export type AreaUnlinkKind = 'areaProject' | 'areaTeam' | 'projectArea' | 'teamArea';

/**
 * «Отключение …» (design § 6.2): destructive, focus on «Отмена»; nothing is counted, an unlink changes
 * nothing else. An unlinked pair is a success. A refusal is the alert, and the page reads itself
 * again when the dialog closes. Mounted per opening.
 */
export function AreaUnlinkDialog({
  kind,
  name,
  ownName,
  action,
  onSucceeded,
  onClose,
}: {
  kind: AreaUnlinkKind;
  /** The row's name (the other end). */
  name: string;
  /** The page's own end. */
  ownName: string;
  action(): Promise<unknown>;
  onSucceeded(): void;
  onClose(contextGone: boolean): void;
}) {
  const texts = areaUnlinkDialog[kind];
  const [busy, setBusy] = useState(false);
  const [alert, setAlert] = useState<string | null>(null);
  const [stale, setStale] = useState(false);

  async function confirm() {
    setBusy(true);
    setAlert(null);
    try {
      await action();
      onSucceeded();
    } catch (error) {
      setAlert(placeRefusal(error, []).alert);
      setStale(true);
      setBusy(false);
    }
  }

  return (
    <ConfirmDialog
      open
      title={texts.title}
      confirmLabel={areaUnlinkDialog.confirm}
      destructive
      cancelAutoFocus
      busy={busy}
      error={alert}
      onConfirm={() => void confirm()}
      onClose={() => onClose(stale)}
    >
      <Typography variant="body1">{texts.text(name, ownName)}</Typography>
    </ConfirmDialog>
  );
}
