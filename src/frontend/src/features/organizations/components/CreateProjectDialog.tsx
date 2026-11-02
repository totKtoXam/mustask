import { useState } from 'react';
import TextField from '@mui/material/TextField';
import { FormDialog, InlineMultiPickerField, InlineTextArea } from '../../../shared/ui';
import type { CreateProjectBody, Project, TeamListItem } from '../api/organizationsApi';
import { useFocusInvalid } from '../hooks/useFocusInvalid';
import { useAreaTree, useOrganizationTeams } from '../hooks/queries';
import { placementField } from '../lib/areaTexts';
import { placementOptionsFor, type PlaceOption } from '../lib/areaTree';
import { LocalPicker } from './AreaPickers';
import { placeRefusal, type RefusalField } from '../lib/refusals';
import { linkDialog, projectTexts } from '../lib/teamTexts';
import { descriptionError, keyError, nameError } from '../lib/validation';
import { requiredField } from './requiredField';

type Errors = Partial<Record<'name' | 'key' | 'description' | 'teams' | 'location', string | null>>;

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
  canPlaceInDepartment = false,
  canPlaceAtTop = false,
  submit,
  onSucceeded,
  onClose,
}: {
  organizationId: string;
  /** The user holds `team.link` and `project.link`: «Команды» is drawn. */
  canLink: boolean;
  /** The user holds `area.write`: a department can be the parent (api-contract § 5, POST /projects). */
  canPlaceInDepartment?: boolean;
  /** The user holds `area.create`: «Организация», the top level, is offered; without it a department is required. */
  canPlaceAtTop?: boolean;
  submit(body: CreateProjectBody): Promise<Project>;
  onSucceeded(project: Project): void;
  onClose(contextGone: boolean): void;
}) {
  const [name, setName] = useState('');
  const [key, setKey] = useState('');
  const [description, setDescription] = useState('');
  const [teams, setTeams] = useState<TeamListItem[]>([]);
  const canPlace = canPlaceInDepartment || canPlaceAtTop;
  const [chosenPlace, setPlace] = useState<PlaceOption | null>(null);
  const [errors, setErrors] = useState<Errors>({});
  const [alert, setAlert] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [contextGone, setContextGone] = useState(false);
  const focusInvalid = useFocusInvalid();
  const teamsQuery = useOrganizationTeams(organizationId, { enabled: canLink, fresh: true });
  const teamOptions = teamsQuery.data ?? [];
  const areasQuery = useAreaTree(organizationId, { enabled: canPlaceInDepartment, fresh: true });
  const placeOptions = placementOptionsFor(
    areasQuery.data ?? [],
    canPlaceAtTop,
    canPlaceInDepartment,
  );
  // The first offered place stands until one is chosen; one no longer offered falls back to it.
  const place = placeOptions.find((option) => option.id === chosenPlace?.id) ?? placeOptions[0];

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
    if (canPlace && place && place.id !== null) {
      body.parentAreaId = place.id;
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
      if (canPlace) {
        fields.push('location');
      }
      const placed = placeRefusal(error, fields);
      setErrors({
        name: placed.fields.name ?? null,
        key: placed.fields.key ?? null,
        description: placed.fields.description ?? null,
        teams: placed.fields.teams ?? null,
        location: placed.fields.location ?? null,
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
      if (placed.fields.location !== undefined) {
        // The departments are read again, and a place no longer offered drops (design § 7).
        void areasQuery.refetch().then((result) => {
          const offered = placementOptionsFor(
            result.data ?? [],
            canPlaceAtTop,
            canPlaceInDepartment,
          );
          setPlace((current) =>
            offered.some((candidate) => candidate.id === current?.id) ? current : null,
          );
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
      {canPlace && (
        <LocalPicker<PlaceOption>
          label={placementField.label}
          hint={placementField.hint}
          error={errors.location ?? undefined}
          busy={busy}
          options={placeOptions}
          loading={areasQuery.isPending}
          value={place ?? null}
          onChange={(next) => {
            setPlace(next);
            setErrors((current) => ({ ...current, location: null }));
          }}
          getKey={(option) => option.id ?? '__top__'}
          getLabel={(option) => option.label}
        />
      )}
    </FormDialog>
  );
}
