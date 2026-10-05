import { useState } from 'react';
import Autocomplete from '@mui/material/Autocomplete';
import TextField from '@mui/material/TextField';
import { useAreaTree } from '../../organizations';
import type { WorkItemArea } from '../api/workItemsApi';

/** The list's lines other than areas (spec § Pickers). */
const areaPickerTexts = {
  noMatch: 'Ничего не найдено',
  loading: 'Загрузка…',
  failed: 'Не удалось загрузить области. Попробуйте ещё раз.',
} as const;

/**
 * «Область» (US-4; spec § Pickers): every area of the item's project by path,
 * in tree order with the root first, from mod-002's tree read (api-contract
 * § 3). The tree is read when the list first opens; a failed read says so in
 * the list, and opening it again reads it again.
 */
export function AreaPicker({
  organizationId,
  projectId,
  value,
  disabled,
  error,
  onChange,
}: {
  organizationId: string;
  projectId: string;
  value: WorkItemArea;
  disabled: boolean;
  error: string | undefined;
  onChange(area: WorkItemArea): void;
}) {
  const [opened, setOpened] = useState(false);
  const tree = useAreaTree(organizationId, projectId, { enabled: opened });
  const failed = tree.isError && !tree.isFetching;
  const options: WorkItemArea[] = tree.isSuccess
    ? tree.data.map((node) => ({ id: node.id, path: node.path }))
    : [];

  return (
    <Autocomplete
      options={options}
      value={value}
      disabled={disabled}
      disableClearable
      onChange={(_event, area) => onChange(area)}
      onOpen={() => {
        setOpened(true);
        if (tree.isError) {
          void tree.refetch();
        }
      }}
      getOptionLabel={(area) => area.path}
      getOptionKey={(area) => area.id}
      isOptionEqualToValue={(option, selected) => option.id === selected.id}
      loading={opened && !tree.isSuccess && !failed}
      loadingText={areaPickerTexts.loading}
      noOptionsText={failed ? areaPickerTexts.failed : areaPickerTexts.noMatch}
      sx={(theme) => ({ minWidth: theme.spacing(32) })}
      renderInput={(params) => (
        <TextField
          {...params}
          label="Область"
          size="small"
          error={error !== undefined}
          helperText={error}
        />
      )}
    />
  );
}
