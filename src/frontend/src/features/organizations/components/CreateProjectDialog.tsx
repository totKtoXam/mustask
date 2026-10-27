import { useState } from 'react';
import TextField from '@mui/material/TextField';
import { FormDialog, InlineMultiPickerField, InlineTextArea } from '../../../shared/ui';
import type { CreateProjectBody, Project, TeamListItem } from '../api/organizationsApi';
import { useFocusInvalid } from '../hooks/useFocusInvalid';
import { useAreaTree, useOrganizationTeams } from '../hooks/queries';
import { areasField } from '../lib/areaTexts';
import { AreasField, type AreaOption } from './AreaPickers';
import { placeRefusal, type RefusalField } from '../lib/refusals';
import { linkDialog, projectTexts } from '../lib/teamTexts';
import { descriptionError, keyError, nameError } from '../lib/validation';
import { requiredField } from './requiredField';

type Errors = Partial<Record<'name' | 'key' | 'description' | 'teams' | 'areas', string | null>>;

/**
 * «Новый проект» (US-3; design § 7.4; feat-028 design § 8): «Название» and «Ключ», both checked
 * on submit — the key as typed, never trimmed or upper-cased — and nothing sent while either is
 * refused; then an optional «Описание» (at most 2 000 characters, as typed) and «Команды», a
 * searchable multiselect over the organization's active teams, drawn only for a user who holds both
 * link codes. **A project is created with no team** (BR-OL-2): left empty, it has none, the normal case.
 * A refusal lands on its field or in the dialog's `Alert` by its code; the input is kept.
 */
export function CreateProjectDialog({
  organizationId,
  canLink,
  canLinkAreas = false,
  submit,
  onSucceeded,
  onClose,
}: {
  organizationId: string;
  /** The user holds `team.link` and `project.link`: «Команды» is drawn. */
  canLink: boolean;
  /** The user holds `area.link` and `project.link`: «Области» is drawn. */
  canLinkAreas?: boolean;
  submit(body: CreateProjectBody): Promise<Project>;
  onSucceeded(project: Project): void;
  onClose(contextGone: boolean): void;
}) {
  const [name, setName] = useState('');
  const [key, setKey] = useState('');
  const [description, setDescription] = useState('');
  const [teams, setTeams] = useState<TeamListItem[]>([]);
  const [areas, setAreas] = useState<AreaOption[]>([]);
  const [errors, setErrors] = useState<Errors>({});
  const [alert, setAlert] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [contextGone, setContextGone] = useState(false);
  const focusInvalid = useFocusInvalid();
  const teamsQuery = useOrganizationTeams(organizationId, { enabled: canLink, fresh: true });
  const teamOptions = teamsQuery.data ?? [];
  const areasQuery = useAreaTree(organizationId, { enabled: canLinkAreas, fresh: true });

  async function handleSubmit() {
    const refused: Errors = {
      name: nameError(name),
      key: keyError(key),
      description: descriptionError(description),
    };
    if (refused.name || refused.key || refused.description) {
      setErrors(refused);
      focusInvalid();
      return;
    }
    const body: CreateProjectBody = { name, key };
    if (description !== '') {
      body.description = description;
    }
    if (canLink && teams.length > 0) {
      body.teamIds = teams.map((team) => team.id);
    }
    if (canLinkAreas && areas.length > 0) {
      body.areaIds = areas.map((area) => area.id);
    }
    setBusy(true);
    setAlert(null);
    setErrors({});
    try {
      onSucceeded(await submit(body));
    } catch (error) {
      const fields: RefusalField[] = ['name', 'key', 'description'];
      if (canLink) {
        fields.push('teams');
      }
      if (canLinkAreas) {
        fields.push('areas');
      }
      const placed = placeRefusal(error, fields);
      setErrors({
        name: placed.fields.name ?? null,
        key: placed.fields.key ?? null,
        description: placed.fields.description ?? null,
        teams: placed.fields.teams ?? null,
        areas: placed.fields.areas ?? null,
      });
      setAlert(placed.alert);
      setContextGone((gone) => gone || placed.contextGone);
      setBusy(false);
      focusInvalid();
      if (placed.fields.teams !== undefined) {
        // The list is read again, and a choice no longer offered drops (design § 8).
        void teamsQuery.refetch().then((result) => {
          const offered = result.data;
          if (offered) {
            setTeams((current) =>
              current.filter((team) => offered.some((candidate) => candidate.id === team.id)),
            );
          }
        });
      }
      if (placed.fields.areas !== undefined) {
        // The areas are read again, and a choice no longer offered drops (design § 5.3).
        void areasQuery.refetch().then((result) => {
          const offered = result.data;
          if (offered) {
            setAreas((current) =>
              current.filter((area) => offered.some((candidate) => candidate.id === area.id)),
            );
          }
        });
      }
    }
  }

  return (
    <FormDialog
      open
      title="Новый проект"
      submitLabel="Создать"
      busy={busy}
      error={alert}
      onSubmit={() => void handleSubmit()}
      onClose={() => onClose(contextGone)}
    >
      <TextField
        label="Название"
        autoFocus
        value={name}
        disabled={busy}
        error={Boolean(errors.name)}
        helperText={errors.name ?? projectTexts.create.nameHint}
        onChange={(event) => {
          setName(event.target.value);
          setErrors((current) => ({ ...current, name: null }));
        }}
        slotProps={requiredField(Boolean(errors.name))}
      />
      <TextField
        label="Ключ"
        value={key}
        disabled={busy}
        error={Boolean(errors.key)}
        helperText={
          errors.key ??
          'От 2 до 10 латинских заглавных букв и цифр, начиная с буквы. С ключа начинаются номера рабочих элементов, после создания он не меняется.'
        }
        onChange={(event) => {
          setKey(event.target.value);
          setErrors((current) => ({ ...current, key: null }));
        }}
        slotProps={{
          inputLabel: { required: true },
          htmlInput: {
            autoComplete: 'off',
            spellCheck: false,
            autoCapitalize: 'characters',
            'aria-required': true,
            'aria-invalid': Boolean(errors.key),
          },
        }}
      />
      <InlineTextArea
        label={projectTexts.create.description.label}
        frame="always"
        hint={projectTexts.create.description.hint}
        value={description}
        busy={busy}
        error={errors.description ?? undefined}
        onChange={(next) => {
          setDescription(next);
          setErrors((current) => ({ ...current, description: null }));
        }}
      />
      {canLink && (
        <InlineMultiPickerField<TeamListItem>
          label={projectTexts.create.teams.label}
          frame="always"
          hint={projectTexts.create.teams.hint}
          busy={busy}
          error={errors.teams ?? undefined}
          value={teams}
          options={teamOptions}
          loading={teamsQuery.isPending}
          noOptionsText={
            teamsQuery.isError
              ? linkDialog.project.loadFailed
              : teamOptions.length === 0
                ? projectTexts.create.teams.none
                : undefined
          }
          onChange={(next) => {
            setTeams(next);
            setErrors((current) => ({ ...current, teams: null }));
          }}
          getOptionLabel={(team) => team.name}
          getOptionKey={(team) => team.id}
          isOptionEqualToValue={(first, second) => first.id === second.id}
        />
      )}
      {canLinkAreas && (
        <AreasField
          hint={areasField.projectHint}
          busy={busy}
          error={errors.areas ?? undefined}
          value={areas}
          options={areasQuery.data ?? []}
          loading={areasQuery.isPending}
          failed={areasQuery.isError}
          onChange={(next) => {
            setAreas(next);
            setErrors((current) => ({ ...current, areas: null }));
          }}
        />
      )}
    </FormDialog>
  );
}
