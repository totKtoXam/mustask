import { useState } from 'react';
import Autocomplete from '@mui/material/Autocomplete';
import Box from '@mui/material/Box';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import { FormDialog } from '../../../shared/ui/FormDialog';
import type { Area, AreaNode } from '../api/organizationsApi';
import { moveOptions, type MoveOption } from '../lib/areaTree';
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

/** «Новая область» (US-5; design § 11.5; mod-017 BR-OT-10): a child of `parent`, asking «Название» alone. */
export function CreateAreaDialog({
  parent,
  submit,
  onSucceeded,
  onClose,
}: DialogEnd<Area> & {
  parent: AreaNode;
  submit(body: { parentId: string; name: string }): Promise<Area>;
}) {
  const [name, setName] = useState('');
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
      onSucceeded(await submit({ parentId: parent.id, name }));
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
