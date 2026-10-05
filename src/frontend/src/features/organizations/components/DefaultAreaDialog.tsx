import { useState } from 'react';
import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import MenuItem from '@mui/material/MenuItem';
import Skeleton from '@mui/material/Skeleton';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import { apiErrorText } from '../../../shared/lib/apiErrorText';
import { FormDialog } from '../../../shared/ui/FormDialog';
import type { Team } from '../api/organizationsApi';
import { useAreaTree } from '../hooks/queries';
import { defaultAreaOptions } from '../lib/areaTree';
import { placeRefusal } from '../lib/refusals';

/**
 * «Область по умолчанию» (US-4; design § 9.6): the areas the team owns
 * explicitly, by path, in tree order — one it owns by inheritance is not
 * offered (BR-M1-14). The dialog reads the project's tree itself: a skeleton
 * in the field's place while it loads, an error `Alert` with «Повторить» if
 * it fails. «Сохранить» is disabled until the choice changes. The first team
 * has no such dialog: no action offers it.
 */
export function DefaultAreaDialog({
  organizationId,
  projectId,
  team,
  submit,
  onSucceeded,
  onClose,
}: {
  organizationId: string;
  projectId: string;
  team: Team;
  submit(areaId: string): Promise<unknown>;
  onSucceeded(): void;
  onClose(contextGone: boolean): void;
}) {
  const tree = useAreaTree(organizationId, projectId);
  const [areaId, setAreaId] = useState(team.defaultArea.id);
  const [fieldError, setFieldError] = useState<string | null>(null);
  const [alert, setAlert] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [contextGone, setContextGone] = useState(false);

  async function handleSubmit() {
    setBusy(true);
    setAlert(null);
    try {
      await submit(areaId);
      onSucceeded();
    } catch (error) {
      const placed = placeRefusal(error, ['area']);
      setFieldError(placed.fields.area ?? null);
      setAlert(placed.alert);
      setContextGone((gone) => gone || placed.contextGone);
      setBusy(false);
    }
  }

  const options = tree.data ? defaultAreaOptions(tree.data, team.id) : [];
  const treeLoading = tree.isPending || (tree.isError && tree.isFetching);

  return (
    <FormDialog
      open
      title="Область по умолчанию"
      submitLabel="Сохранить"
      busy={busy}
      error={alert}
      submitDisabled={areaId === team.defaultArea.id || !tree.isSuccess}
      onSubmit={() => void handleSubmit()}
      onClose={() => onClose(contextGone)}
    >
      <Typography variant="body2" color="text.secondary">
        {`В эту область попадают рабочие элементы, созданные с досок и бэклогов команды «${team.name}».`}
      </Typography>
      {treeLoading ? (
        <Skeleton
          variant="rounded"
          aria-label="Загрузка областей"
          sx={{ height: (theme) => theme.spacing(7) }}
        />
      ) : tree.isError ? (
        <Alert
          severity="error"
          action={
            <Button color="inherit" size="small" onClick={() => void tree.refetch()}>
              Повторить
            </Button>
          }
        >
          {apiErrorText(tree.error)}
        </Alert>
      ) : (
        <TextField
          select
          label="Область"
          autoFocus
          value={areaId}
          disabled={busy}
          error={fieldError !== null}
          helperText={fieldError ?? undefined}
          onChange={(event) => {
            setAreaId(event.target.value);
            setFieldError(null);
          }}
        >
          {options.map((area) => (
            <MenuItem key={area.id} value={area.id}>
              {area.path}
            </MenuItem>
          ))}
        </TextField>
      )}
    </FormDialog>
  );
}
