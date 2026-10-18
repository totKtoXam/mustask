import { useState } from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import {
  FormDialog,
  InlineMultiPickerField,
  InlineTextArea,
  InlineTextField,
} from '../../../shared/ui';
import type { CreateTeamBody, ProjectListItem, Team } from '../api/organizationsApi';
import { useFocusInvalid } from '../hooks/useFocusInvalid';
import { useFocusOnMount } from '../hooks/useFocusOnMount';
import { useProjects } from '../hooks/queries';
import { useUserSearch } from '../hooks/useUserSearch';
import { placeRefusal, type RefusalField } from '../lib/refusals';
import { createTeamDialog, linkDialog } from '../lib/teamTexts';
import {
  optionKey,
  optionLabel,
  sameOption,
  userOptions,
  type UserOption,
} from '../lib/userSearch';
import { descriptionError, nameError } from '../lib/validation';

type FieldErrors = Partial<Record<RefusalField, string>>;

/** Two project options are one project when their ids are. */
function sameProject(first: ProjectListItem, second: ProjectListItem): boolean {
  return first.id === second.id;
}

/**
 * «Новая команда» (feat-028 US-1; design § 5.1): the team, its optional description, members and
 * projects in one request. «Название» is checked on «Создать» (1–100 after trimming) and so is
 * «Описание» (at most 2 000, as typed); nothing is sent while either is refused. «Участники» is
 * a searchable multiselect over the server's search of the organization's active members
 * (api-contract § 2.3a: two trimmed characters, at most 20, the same lines as the team page's
 * search), «Проекты» one over its active projects, drawn only for a user who holds both link codes
 * (`canLink`). A refusal lands on its field with the input kept; the lists are read again after a
 * project is refused, and a choice no longer offered drops. Mounted per opening.
 */
export function CreateTeamDialog({
  organizationId,
  canLink,
  submit,
  onSucceeded,
  onClose,
}: {
  organizationId: string;
  /** The user holds `team.link` and `project.link`: «Проекты» is drawn. */
  canLink: boolean;
  submit(body: CreateTeamBody): Promise<Team>;
  onSucceeded(team: Team): void;
  /** `contextGone`: the organization answered not found; the page reads itself again. */
  onClose(contextGone: boolean): void;
}) {
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [members, setMembers] = useState<UserOption[]>([]);
  const [memberText, setMemberText] = useState('');
  const [typed, setTyped] = useState('');
  const [projects, setProjects] = useState<ProjectListItem[]>([]);
  const [errors, setErrors] = useState<FieldErrors>({});
  const [alert, setAlert] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [contextGone, setContextGone] = useState(false);
  const focusFirst = useFocusOnMount<HTMLInputElement>();
  const focusInvalid = useFocusInvalid();

  const { search, retry } = useUserSearch({ kind: 'newTeam', organizationId }, typed);
  const projectsQuery = useProjects(organizationId, 'active', { enabled: canLink, fresh: true });

  const shown = userOptions(search);
  // The chosen stay among the options MUI knows even while the search lists others.
  const memberOptions = [
    ...shown,
    ...members.filter((chosen) => !shown.some((option) => sameOption(option, chosen))),
  ];
  const projectOptions = projectsQuery.data ?? [];

  async function handleSubmit() {
    const refused: FieldErrors = {};
    const nameRefusal = nameError(name);
    const descriptionRefusal = descriptionError(description);
    if (nameRefusal) refused.name = nameRefusal;
    if (descriptionRefusal) refused.description = descriptionRefusal;
    if (Object.keys(refused).length > 0) {
      setErrors(refused);
      focusInvalid();
      return;
    }
    const body: CreateTeamBody = { name };
    if (description !== '') {
      body.description = description;
    }
    const memberIds = members.flatMap((option) => (option.kind === 'user' ? [option.user.id] : []));
    if (memberIds.length > 0) {
      body.memberIds = memberIds;
    }
    if (canLink && projects.length > 0) {
      body.projectIds = projects.map((project) => project.id);
    }
    setBusy(true);
    setAlert(null);
    setErrors({});
    try {
      onSucceeded(await submit(body));
    } catch (error) {
      const fields: RefusalField[] = ['name', 'description', 'members'];
      if (canLink) {
        fields.push('projects');
      }
      const placed = placeRefusal(error, fields);
      setErrors(placed.fields);
      setAlert(placed.alert);
      setContextGone((gone) => gone || placed.contextGone);
      setBusy(false);
      focusInvalid();
      if (placed.fields.projects !== undefined) {
        // The list is read again, and a choice it no longer offers drops (design § 5.1).
        void projectsQuery.refetch().then((result) => {
          const offered = result.data;
          if (offered) {
            setProjects((current) =>
              current.filter((project) => offered.some((candidate) => candidate.id === project.id)),
            );
          }
        });
      }
    }
  }

  return (
    <FormDialog
      open
      title={createTeamDialog.title}
      submitLabel={createTeamDialog.submit}
      busy={busy}
      error={alert}
      onSubmit={() => void handleSubmit()}
      onClose={() => onClose(contextGone)}
    >
      <InlineTextField
        ref={focusFirst}
        label={createTeamDialog.name.label}
        frame="always"
        required
        hint={createTeamDialog.name.hint}
        value={name}
        busy={busy}
        error={errors.name}
        onChange={(next) => {
          setName(next);
          setErrors((current) => ({ ...current, name: undefined }));
        }}
      />
      <InlineTextArea
        label={createTeamDialog.description.label}
        frame="always"
        hint={createTeamDialog.description.hint}
        value={description}
        busy={busy}
        error={errors.description}
        onChange={(next) => {
          setDescription(next);
          setErrors((current) => ({ ...current, description: undefined }));
        }}
      />
      <InlineMultiPickerField<UserOption>
        label={createTeamDialog.members.label}
        frame="always"
        hint={createTeamDialog.members.hint}
        busy={busy}
        error={errors.members}
        value={members}
        options={memberOptions}
        inputValue={memberText}
        onInputChange={(text, reason) => {
          setMemberText(text);
          setTyped(reason === 'input' ? text : '');
        }}
        onOpen={retry}
        filterOptions={() => shown}
        onChange={(next) => {
          setMembers(next.filter((option) => option.kind === 'user'));
          setErrors((current) => ({ ...current, members: undefined }));
        }}
        getOptionLabel={optionLabel}
        getOptionKey={optionKey}
        getOptionDisabled={(option) => option.kind === 'hint'}
        isOptionEqualToValue={sameOption}
        renderOption={(option) =>
          option.kind === 'user' ? (
            <Box sx={{ minWidth: 0 }}>
              <Box>{option.user.displayName}</Box>
              {option.user.email && (
                <Typography variant="caption" sx={{ color: 'text.secondary' }}>
                  {option.user.email}
                </Typography>
              )}
            </Box>
          ) : (
            <Typography variant="body2" sx={{ color: 'text.secondary' }}>
              {option.text}
            </Typography>
          )
        }
      />
      {canLink && (
        <InlineMultiPickerField<ProjectListItem>
          label={createTeamDialog.projects.label}
          frame="always"
          hint={createTeamDialog.projects.hint}
          busy={busy}
          error={errors.projects}
          value={projects}
          options={projectOptions}
          loading={projectsQuery.isPending}
          noOptionsText={
            projectsQuery.isError
              ? linkDialog.team.loadFailed
              : projectOptions.length === 0
                ? createTeamDialog.projects.none
                : undefined
          }
          onChange={(next) => {
            setProjects(next);
            setErrors((current) => ({ ...current, projects: undefined }));
          }}
          getOptionLabel={(project) => project.name}
          getOptionKey={(project) => project.id}
          isOptionEqualToValue={sameProject}
          renderOption={(project) => (
            <Box sx={{ display: 'flex', gap: 1, minWidth: 0, alignItems: 'baseline' }}>
              <Box component="span">{project.name}</Box>
              <Typography variant="caption" sx={{ color: 'text.secondary' }}>
                {project.key}
              </Typography>
            </Box>
          )}
        />
      )}
    </FormDialog>
  );
}
