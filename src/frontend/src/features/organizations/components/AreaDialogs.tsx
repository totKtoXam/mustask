import { useState } from 'react';
import Alert from '@mui/material/Alert';
import Autocomplete from '@mui/material/Autocomplete';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import MenuItem from '@mui/material/MenuItem';
import Skeleton from '@mui/material/Skeleton';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import type { UseQueryResult } from '@tanstack/react-query';
import { apiErrorText } from '../../../shared/lib/apiErrorText';
import { FormDialog } from '../../../shared/ui/FormDialog';
import type { Area, AreaNode, Team } from '../api/organizationsApi';
import {
  INHERIT_OWNER_VALUE,
  moveOptions,
  ownerOptions,
  ownerTeamIdOf,
  type MoveOption,
} from '../lib/areaTree';
import { placeRefusal } from '../lib/refusals';
import { nameError } from '../lib/validation';
import { ConfirmAction } from './ConfirmAction';
import { requiredField } from './requiredField';

/** What every area dialog shares: the outcome hand-back of design § 4.3. */
interface DialogEnd<R> {
  onSucceeded(result: R): void;
  /** `contextGone`: a not-found answer; the page reads itself again (design § 4.4). */
  onClose(contextGone: boolean): void;
}

/** The refusal state of a dialog: its fields' texts, its `Alert`, and whether the page is gone. */
function useRefusal() {
  const [fields, setFields] = useState<{ name?: string | null; parent?: string | null }>({});
  const [alert, setAlert] = useState<string | null>(null);
  const [contextGone, setContextGone] = useState(false);
  return {
    fields,
    alert,
    contextGone,
    clearField: (field: 'name' | 'parent') =>
      setFields((current) => ({ ...current, [field]: null })),
    refuseField: (field: 'name' | 'parent', text: string) =>
      setFields((current) => ({ ...current, [field]: text })),
    place(error: unknown, available: readonly ('name' | 'parent')[]) {
      const placed = placeRefusal(error, available);
      setFields({ name: placed.fields.name ?? null, parent: placed.fields.parent ?? null });
      setAlert(placed.alert);
      setContextGone((gone) => gone || placed.contextGone);
    },
    reset: () => setAlert(null),
  };
}

/**
 * The «Владелец» picker (US-6; design § 11.5): «Наследовать от родительской
 * области» first, then the project's active teams in list order — a deleted
 * team is never offered, because the list leaves it out. A skeleton while
 * the teams load, an error `Alert` with «Повторить» if they fail.
 */
function OwnerField({
  teams,
  value,
  onChange,
  disabled,
  helper,
  autoFocus = false,
}: {
  teams: UseQueryResult<Team[]>;
  value: string;
  onChange(value: string): void;
  disabled: boolean;
  helper?: string;
  autoFocus?: boolean;
}) {
  if (teams.isPending || (teams.isError && teams.isFetching)) {
    return (
      <Skeleton
        variant="rounded"
        aria-label="Загрузка команд"
        sx={{ height: (theme) => theme.spacing(7) }}
      />
    );
  }
  if (teams.isError) {
    return (
      <Alert
        severity="error"
        action={
          <Button color="inherit" size="small" onClick={() => void teams.refetch()}>
            Повторить
          </Button>
        }
      >
        {apiErrorText(teams.error)}
      </Alert>
    );
  }
  return (
    <TextField
      select
      label="Владелец"
      autoFocus={autoFocus}
      value={value}
      disabled={disabled}
      helperText={helper}
      onChange={(event) => onChange(event.target.value)}
      slotProps={{ select: { displayEmpty: true }, inputLabel: { shrink: true } }}
    >
      {ownerOptions(teams.data).map((option) => (
        <MenuItem key={option.value || 'inherit'} value={option.value}>
          {option.label}
        </MenuItem>
      ))}
    </TextField>
  );
}

/** «Новая область» (US-5, US-6; design § 11.5): a child of `parent`, with or without an owner. */
export function CreateAreaDialog({
  parent,
  teams,
  submit,
  onSucceeded,
  onClose,
}: DialogEnd<Area> & {
  parent: AreaNode;
  teams: UseQueryResult<Team[]>;
  submit(body: { parentId: string; name: string; ownerTeamId: string | null }): Promise<Area>;
}) {
  const [name, setName] = useState('');
  const [owner, setOwner] = useState(INHERIT_OWNER_VALUE);
  const [busy, setBusy] = useState(false);
  const refusal = useRefusal();

  async function handleSubmit() {
    const refused = nameError(name);
    if (refused) {
      refusal.refuseField('name', refused);
      return;
    }
    setBusy(true);
    refusal.reset();
    try {
      onSucceeded(await submit({ parentId: parent.id, name, ownerTeamId: ownerTeamIdOf(owner) }));
    } catch (error) {
      refusal.place(error, ['name']);
      setBusy(false);
    }
  }

  return (
    <FormDialog
      open
      title="Новая область"
      submitLabel="Создать"
      busy={busy}
      error={refusal.alert}
      onSubmit={() => void handleSubmit()}
      onClose={() => onClose(refusal.contextGone)}
    >
      <Typography variant="body2" color="text.secondary">
        {`Родительская область: ${parent.path}`}
      </Typography>
      <TextField
        label="Название"
        autoFocus
        value={name}
        disabled={busy}
        error={Boolean(refusal.fields.name)}
        helperText={refusal.fields.name ?? 'От 1 до 100 символов'}
        onChange={(event) => {
          setName(event.target.value);
          refusal.clearField('name');
        }}
        slotProps={requiredField(Boolean(refusal.fields.name))}
      />
      <OwnerField teams={teams} value={owner} onChange={setOwner} disabled={busy} />
    </FormDialog>
  );
}

/** «Перемещение области» (US-5, US-6; design § 11.5): the node moves with its whole subtree. */
export function MoveAreaDialog({
  area,
  tree,
  submit,
  onSucceeded,
  onClose,
}: DialogEnd<{ parentId: string }> & {
  area: AreaNode;
  tree: readonly AreaNode[];
  submit(parentId: string): Promise<unknown>;
}) {
  const [parent, setParent] = useState<MoveOption | null>(null);
  const [busy, setBusy] = useState(false);
  const refusal = useRefusal();
  const options = moveOptions(tree, area.id);

  async function handleSubmit() {
    if (!parent) {
      return;
    }
    setBusy(true);
    refusal.reset();
    try {
      await submit(parent.id);
      onSucceeded({ parentId: parent.id });
    } catch (error) {
      refusal.place(error, ['parent']);
      setBusy(false);
    }
  }

  return (
    <FormDialog
      open
      title="Перемещение области"
      submitLabel="Переместить"
      busy={busy}
      error={refusal.alert}
      submitDisabled={parent === null}
      onSubmit={() => void handleSubmit()}
      onClose={() => onClose(refusal.contextGone)}
    >
      <Typography variant="body2" color="text.secondary">
        {`Область «${area.name}» переместится вместе со всеми вложенными областями.`}
      </Typography>
      <Autocomplete
        options={options}
        value={parent}
        disabled={busy}
        onChange={(_event, value) => {
          setParent(value);
          refusal.clearField('parent');
        }}
        getOptionLabel={(option) => option.path}
        isOptionEqualToValue={(option, value) => option.id === value.id}
        noOptionsText="Ничего не найдено"
        renderOption={({ key, ...props }, option) => (
          <Box component="li" key={key} {...props} sx={{ display: 'block' }}>
            <Typography variant="body1">{option.path}</Typography>
            {option.isRoot && (
              <Typography variant="body2" color="text.secondary">
                Корневая область
              </Typography>
            )}
          </Box>
        )}
        renderInput={(params) => (
          <TextField
            {...params}
            label="Новая родительская область"
            autoFocus
            error={Boolean(refusal.fields.parent)}
            helperText={refusal.fields.parent ?? undefined}
          />
        )}
      />
    </FormDialog>
  );
}

/** «Владелец области» (US-6; design § 11.5): set or clear the explicit owner. */
export function AreaOwnerDialog({
  area,
  teams,
  submit,
  onSucceeded,
  onClose,
}: DialogEnd<null> & {
  area: AreaNode;
  teams: UseQueryResult<Team[]>;
  submit(ownerTeamId: string | null): Promise<unknown>;
}) {
  const initial = area.ownerTeamId ?? INHERIT_OWNER_VALUE;
  const [owner, setOwner] = useState(initial);
  const [busy, setBusy] = useState(false);
  const refusal = useRefusal();

  async function handleSubmit() {
    setBusy(true);
    refusal.reset();
    try {
      await submit(ownerTeamIdOf(owner));
      onSucceeded(null);
    } catch (error) {
      refusal.place(error, []);
      setBusy(false);
    }
  }

  const current = area.ownerInherited
    ? `Сейчас: ${area.resolvedOwner.name}, унаследовано`
    : `Сейчас: ${area.resolvedOwner.name}`;

  return (
    <FormDialog
      open
      title="Владелец области"
      submitLabel="Сохранить"
      busy={busy}
      error={refusal.alert}
      submitDisabled={owner === initial}
      onSubmit={() => void handleSubmit()}
      onClose={() => onClose(refusal.contextGone)}
    >
      <OwnerField
        teams={teams}
        value={owner}
        onChange={setOwner}
        disabled={busy}
        helper={current}
        autoFocus
      />
    </FormDialog>
  );
}

/** «Удаление области» (US-5; design § 11.5): an empty leaf, with no restore. */
export function DeleteAreaDialog({
  area,
  action,
  onSucceeded,
  onClose,
}: DialogEnd<unknown> & { area: AreaNode; action(): Promise<unknown> }) {
  return (
    <ConfirmAction
      title="Удаление области"
      text={`Удалить область «${area.name}»? Восстановить её будет нельзя.`}
      confirmLabel="Удалить область"
      destructive
      action={action}
      onSucceeded={onSucceeded}
      onClose={onClose}
    />
  );
}
