import { useState } from 'react';
import Typography from '@mui/material/Typography';
import { FormDialog, InlineSelectField } from '../../../shared/ui';
import type { Team } from '../api/organizationsApi';
import { placeRefusal } from '../lib/refusals';

/** The empty choice: no team is named, and the setting is cleared (api-contract § 2.8). */
export const NO_DEFAULT_TEAM_LABEL = 'Не выбрана';

/**
 * «Команда по умолчанию» (mod-017 US-3, BR-OT-4; design § 7.1): the team a new work
 * item of the project starts with, or «Не выбрана». The value is the team the
 * settings name (the inferred only team is shown by the list's mark, not here);
 * «Сохранить» stays unavailable until the choice changes. A team refused as
 * not found or not in the project is the field's error, the dialog stays open and
 * the list is read again (`onRefused`).
 */
export function DefaultTeamDialog({
  teams,
  submit,
  onSucceeded,
  onRefused,
  onClose,
}: {
  teams: readonly Team[];
  /** `null` is «Не выбрана». */
  submit(teamId: string | null): Promise<unknown>;
  onSucceeded(): void;
  /** The server refused the choice: the page reads the teams again. */
  onRefused(): void;
  /** `contextGone`: the project or organization answered not found; the page reads itself again. */
  onClose(contextGone: boolean): void;
}) {
  const named = teams.find((team) => team.defaultMark === 'named')?.id ?? '';
  const [teamId, setTeamId] = useState(named);
  const [fieldError, setFieldError] = useState<string | undefined>(undefined);
  const [alert, setAlert] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [contextGone, setContextGone] = useState(false);

  async function handleSubmit() {
    setBusy(true);
    setAlert(null);
    try {
      await submit(teamId === '' ? null : teamId);
      onSucceeded();
    } catch (error) {
      const placed = placeRefusal(error, ['team']);
      setFieldError(placed.fields.team);
      setAlert(placed.alert);
      setContextGone((gone) => gone || placed.contextGone);
      setBusy(false);
      if (placed.fields.team !== undefined) {
        onRefused();
      }
    }
  }

  return (
    <FormDialog
      open
      title="Команда по умолчанию"
      submitLabel="Сохранить"
      busy={busy}
      error={alert}
      submitDisabled={teamId === named}
      onSubmit={() => void handleSubmit()}
      onClose={() => onClose(contextGone)}
    >
      <Typography variant="body2" color="text.secondary">
        {`Новые рабочие элементы проекта начинаются с этой команды, если их создают не со страницы команды и не как дочерние. Существующие рабочие элементы не меняются.${
          teams.length === 1 ? ' Пока в проекте одна команда, она и есть команда по умолчанию.' : ''
        }`}
      </Typography>
      <InlineSelectField<string>
        label="Команда"
        frame="always"
        value={teamId}
        emptyLabel={NO_DEFAULT_TEAM_LABEL}
        options={teams.map((team) => ({ value: team.id, label: team.name }))}
        onChange={(next) => {
          setTeamId(next);
          setFieldError(undefined);
        }}
        error={fieldError}
        busy={busy}
      />
    </FormDialog>
  );
}
