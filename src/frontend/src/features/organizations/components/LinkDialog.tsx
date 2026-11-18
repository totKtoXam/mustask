import { useState } from 'react';
import { organizationErrorCodes } from '../../../shared/api/errorCodes';
import { ApiError } from '../../../shared/api/httpClient';
import { apiErrorText } from '../../../shared/lib/apiErrorText';
import { Link as RouterLink } from 'react-router-dom';
import Box from '@mui/material/Box';
import Link from '@mui/material/Link';
import Typography from '@mui/material/Typography';
import { FormDialog, InlineMultiPickerField, InlineNotice } from '../../../shared/ui';
import { useFocusInvalid } from '../hooks/useFocusInvalid';
import { useSelectorOffers } from '../hooks/navigationQueries';
import { useOrganizationTeams, useProjects } from '../hooks/queries';
import { placeRefusal, type RefusalField } from '../lib/refusals';
import { linkDialog } from '../lib/teamTexts';
import { organizationTeamsPath } from '../paths';

/** One thing the dialog offers: a team of the organization, or a project (with its key). */
export interface LinkOption {
  id: string;
  name: string;
  /** A project's key, shown under its name. */
  key?: string;
}

/**
 * «Добавление команд в проект» (`end="project"`, opened on a project's page) and «Добавление проектов
 * в команду» (`end="team"`, on a team's page) — one component, two ends (feat-028 design § 6.1, US-3).
 * The options are the organization's active teams (projects), read when the dialog opens, minus the
 * ones the page lists as linked. «Добавить» waits for a choice. Every state of the list is named: it
 * loads («Загрузка…») while the field works; a failed read replaces the field with «Повторить»; no
 * option because all are linked is said in the list; none existing at all replaces the field with an
 * information notice. With `department` (mod-022, BR-TD-3) only the other end's objects standing in
 * that department are offered, and a refusal `team.other_department` (a stale list) is the alert. A chosen end that is gone lands under the field, the options are read again and a
 * choice no longer offered drops; the page's own end gone is the dialog's alert.
 */
export function LinkDialog({
  end,
  organizationId,
  ownName,
  department,
  linkedIds,
  submit,
  onSucceeded,
  onClose,
}: {
  end: 'project' | 'team';
  organizationId: string;
  /** The page's own end: the project's name for `end="project"`, the team's for `end="team"`. */
  ownName: string;
  /** The department the page's own end stands in: the picker offers that department's objects only. */
  department?: { id: string; name: string };
  /** What the page lists as linked already. */
  linkedIds: readonly string[];
  submit(ids: string[]): Promise<unknown>;
  onSucceeded(chosen: LinkOption[]): void;
  /** `contextGone`: the page's own end answered not found; the page reads itself again. */
  onClose(contextGone: boolean): void;
}) {
  const forProject = end === 'project';
  const texts = forProject ? linkDialog.project : linkDialog.team;
  const teams = useOrganizationTeams(organizationId, {
    enabled: forProject,
    fresh: true,
    linkableFor: 'project',
  });
  const projects = useProjects(organizationId, 'active', {
    enabled: !forProject,
    fresh: true,
    linkableFor: 'team',
  });
  // The project list carries no department: the selector offers do (read when the dialog opens).
  const offers = useSelectorOffers(organizationId, {
    enabled: !forProject && department !== undefined,
  });
  const query = forProject ? teams : projects;
  const [chosen, setChosen] = useState<LinkOption[]>([]);
  const [typed, setTyped] = useState('');
  const [fieldError, setFieldError] = useState<string | undefined>(undefined);
  const [alert, setAlert] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [contextGone, setContextGone] = useState(false);
  const focusInvalid = useFocusInvalid();

  const departmentOf = new Map((offers.data?.projects ?? []).map((p) => [p.id, p.departmentId]));
  const everyone: LinkOption[] = forProject
    ? (teams.data ?? [])
        .filter((team) => department === undefined || team.departmentId === department.id)
        .map(({ id, name }) => ({ id, name }))
    : (projects.data ?? [])
        .filter(
          (project) => department === undefined || departmentOf.get(project.id) === department.id,
        )
        .map(({ id, name, key }) => ({ id, name, key }));
  const options = everyone.filter((option) => !linkedIds.includes(option.id));
  const failed = (query.isError && !query.isFetching) || (offers.isError && !offers.isFetching);
  const loadingOffers = !forProject && department !== undefined && offers.isPending;
  const noneExist = query.isSuccess && !loadingOffers && everyone.length === 0;

  async function handleSubmit() {
    setBusy(true);
    setAlert(null);
    setFieldError(undefined);
    try {
      await submit(chosen.map((option) => option.id));
      onSucceeded(chosen);
    } catch (error) {
      if (
        error instanceof ApiError &&
        error.errorCode === organizationErrorCodes.teamOtherDepartment
      ) {
        // A move or a link raced the picker (design § 5.3): the dialog's banner, and the lists are read again.
        setAlert(apiErrorText(error));
        setBusy(false);
        void offers.refetch();
        void query.refetch().then((result) => {
          const offered = result.data;
          if (offered) {
            setChosen((current) =>
              current.filter((option) => offered.some((candidate) => candidate.id === option.id)),
            );
          }
        });
        return;
      }
      const field: RefusalField = forProject ? 'teams' : 'projects';
      const placed = placeRefusal(error, [field]);
      setFieldError(placed.fields[field]);
      setAlert(placed.alert);
      setContextGone((gone) => gone || placed.contextGone);
      setBusy(false);
      focusInvalid();
      if (placed.fields[field] !== undefined) {
        // The options are read again, and a choice no longer offered drops (design § 6.1).
        void query.refetch().then((result) => {
          const offered = result.data;
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
      submitLabel={linkDialog.submit}
      busy={busy}
      error={alert}
      submitDisabled={chosen.length === 0 || failed || noneExist}
      onSubmit={() => void handleSubmit()}
      onClose={() => onClose(contextGone)}
    >
      <Typography variant="body2" sx={{ color: 'text.secondary' }}>
        {forProject ? linkDialog.project.text(ownName) : linkDialog.team.text(ownName)}
      </Typography>
      {failed ? (
        <InlineNotice
          severity="error"
          action={{
            label: linkDialog.retry,
            onClick: () => {
              void query.refetch();
              void offers.refetch();
            },
          }}
        >
          {texts.loadFailed}
        </InlineNotice>
      ) : noneExist ? (
        <InlineNotice severity="info">
          {department?.name ? (
            texts.noneInDepartment(department.name)
          ) : forProject ? (
            <>
              {linkDialog.project.noneExist}
              <Link component={RouterLink} to={organizationTeamsPath(organizationId)}>
                {linkDialog.project.sectionName}
              </Link>
              {'.'}
            </>
          ) : (
            linkDialog.team.noneExist
          )}
        </InlineNotice>
      ) : (
        <InlineMultiPickerField<LinkOption>
          label={texts.label}
          frame="always"
          required
          busy={busy}
          error={fieldError}
          value={chosen}
          options={options}
          loading={query.isPending || loadingOffers}
          inputValue={typed}
          onInputChange={(text, reason) => setTyped(reason === 'input' ? text : '')}
          noOptionsText={
            typed === '' ? (department ? texts.allLinkedInDepartment : texts.allLinked) : undefined
          }
          onChange={(next) => {
            setChosen(next);
            setFieldError(undefined);
          }}
          getOptionLabel={(option) => option.name}
          getOptionKey={(option) => option.id}
          isOptionEqualToValue={(first, second) => first.id === second.id}
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
