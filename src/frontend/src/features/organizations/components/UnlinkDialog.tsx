import { useState } from 'react';
import Typography from '@mui/material/Typography';
import { apiErrorText } from '../../../shared/lib/apiErrorText';
import {
  ConfirmDialog,
  FieldsSkeleton,
  InlineNotice,
  InlineSelectField,
  type SelectOption,
} from '../../../shared/ui';
import { useFocusInvalid } from '../hooks/useFocusInvalid';
import { useTeams, useUnlinkCount } from '../hooks/queries';
import { placeRefusal } from '../lib/refusals';
import { linkDialog, unlinkDialog } from '../lib/teamTexts';

/**
 * The destination «Без команды» is no team: its select value is this, which no id can be.
 * The request names it as `teamId: null` (api-contract § 2.4).
 */
const NO_TEAM = 'none';

/**
 * «Отключение команды от проекта» (feat-028 US-4, BR-OL-8; design § 6.2): the same from a project's row
 * on a team's page and from a team's row on the project's page. Destructive, focus on «Отмена». What the
 * team owns **in that project** is counted when the dialog opens. If it owns items the actor chooses
 * where they go — «Без команды» or another team linked to the project — and **nothing is preselected**
 * (OQ-5 A): «Отключить» waits for the choice, since the choice moves every item. If it owns none there
 * is no field and the request names «Без команды». A refusal lands under the field (the choice
 * clears, the project's teams are read again) or in the alert. Mounted per opening.
 */
export function UnlinkDialog<R>({
  organizationId,
  projectId,
  projectName,
  teamId,
  teamName,
  submit,
  onSucceeded,
  onClose,
}: {
  organizationId: string;
  projectId: string;
  projectName: string;
  teamId: string;
  teamName: string;
  /** `destinationTeamId` is `null` for «Без команды». */
  submit(destinationTeamId: string | null): Promise<R>;
  onSucceeded(result: R): void;
  /** `contextGone`: the project (or the team) answered not found; the page reads itself again. */
  onClose(contextGone: boolean): void;
}) {
  const count = useUnlinkCount(organizationId, projectId, teamId);
  const teams = useTeams(organizationId, projectId, { fresh: true });
  const [destination, setDestination] = useState<string>('');
  const [fieldError, setFieldError] = useState<string | undefined>(undefined);
  const [alert, setAlert] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [contextGone, setContextGone] = useState(false);
  const focusInvalid = useFocusInvalid();

  const owned = count.data?.count ?? 0;
  const counting = count.isPending || (count.isError && count.isFetching);
  // The project's teams matter only when there is a choice to make.
  const teamsPending = owned > 0 && (teams.isPending || (teams.isError && teams.isFetching));
  const loading = counting || teamsPending;
  const failure = count.isError ? count.error : owned > 0 && teams.isError ? teams.error : null;

  const others = (teams.data ?? []).filter((team) => team.id !== teamId);
  const options: SelectOption<string>[] = [
    { value: NO_TEAM, label: unlinkDialog.noTeam },
    ...others.map((team) => ({ value: team.id, label: team.name })),
  ];
  const chosenTeam = others.find((team) => team.id === destination);
  const choiceLine =
    destination === NO_TEAM
      ? unlinkDialog.toNobody
      : chosenTeam
        ? unlinkDialog.toTeam(chosenTeam.name)
        : null;

  const ready = count.isSuccess && (owned === 0 || (teams.isSuccess && destination !== ''));

  async function confirm() {
    setBusy(true);
    setAlert(null);
    setFieldError(undefined);
    try {
      onSucceeded(await submit(owned === 0 || destination === NO_TEAM ? null : destination));
    } catch (error) {
      // The destination field is drawn only when the team owns items; with none, every refusal is the alert's
      // (a team deleted while the dialog is open must show, not land on a field that is not on the screen).
      const placed = placeRefusal(error, owned > 0 ? ['destination'] : []);
      setFieldError(placed.fields.destination);
      setAlert(placed.alert);
      setContextGone((gone) => gone || placed.contextGone);
      setBusy(false);
      focusInvalid();
      if (placed.fields.destination !== undefined) {
        // The destination is out of date: the choice clears and the project's teams are read again.
        setDestination('');
        void teams.refetch();
      }
    }
  }

  return (
    <ConfirmDialog
      open
      title={unlinkDialog.title}
      confirmLabel={unlinkDialog.confirm}
      destructive
      cancelAutoFocus
      busy={busy}
      error={alert}
      confirmDisabled={!ready}
      onConfirm={() => void confirm()}
      onClose={() => onClose(contextGone)}
    >
      {loading ? (
        <FieldsSkeleton count={2} />
      ) : failure ? (
        <InlineNotice
          severity="error"
          action={{
            label: linkDialog.retry,
            onClick: () => {
              void count.refetch();
              void teams.refetch();
            },
          }}
        >
          {apiErrorText(failure)}
        </InlineNotice>
      ) : owned > 0 ? (
        <>
          <Typography variant="body1" sx={{ mb: 2 }}>
            {unlinkDialog.owns(teamName, projectName, owned)}
          </Typography>
          <InlineSelectField<string>
            label={unlinkDialog.field}
            frame="always"
            required
            emptyText={unlinkDialog.fieldPlaceholder}
            value={destination}
            options={options}
            busy={busy}
            error={fieldError}
            onChange={(next) => {
              setDestination(next);
              setFieldError(undefined);
            }}
          />
          {choiceLine && !fieldError && (
            <Typography variant="body2" sx={{ color: 'text.secondary', mt: 1 }}>
              {choiceLine}
            </Typography>
          )}
          <Typography variant="caption" sx={{ display: 'block', color: 'text.secondary', mt: 1 }}>
            {unlinkDialog.caption(teamName)}
          </Typography>
        </>
      ) : (
        <>
          <Typography variant="body1">{unlinkDialog.ownsNone(teamName, projectName)}</Typography>
          <Typography variant="caption" sx={{ display: 'block', color: 'text.secondary', mt: 1 }}>
            {unlinkDialog.captionNone(teamName)}
          </Typography>
        </>
      )}
    </ConfirmDialog>
  );
}
