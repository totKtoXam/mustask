import { useState, type Ref, memo } from 'react';
import { InlinePickerField } from '../../../../shared/ui';
import { useProjectAreaTree } from '../../../organizations';
import type { WorkItemArea } from '../../api/workItemsApi';
import { projectRelativePath } from '../../lib/areaPaths';
import { cardTexts } from '../../lib/cardTexts';

/** The list's lines other than areas (mod-003 spec § Pickers). */
const areaTexts = {
  noMatch: 'Ничего не найдено',
  loading: 'Загрузка…',
  failed: 'Не удалось загрузить области. Попробуйте ещё раз.',
} as const;

/**
 * «Область» of the band (US-4; mod-003 spec § Pickers): every node of the item's
 * project by path, in tree order with the root first, from mod-002's tree read
 * (api-contract § 3). The tree is read when the list first opens; a failed read
 * says so in the list, and opening it again reads it again.
 */
function AreaFieldView({
  organizationId,
  projectId,
  value,
  error,
  busy,
  readOnly,
  onChange,
  inputRef,
}: {
  organizationId: string;
  projectId: string;
  value: WorkItemArea;
  error: string | undefined;
  busy: boolean;
  readOnly: boolean;
  onChange: (area: WorkItemArea) => void;
  inputRef?: Ref<HTMLInputElement>;
}) {
  const [opened, setOpened] = useState(false);
  const tree = useProjectAreaTree(organizationId, projectId, { enabled: opened });
  const failed = tree.isError && !tree.isFetching;
  const options: WorkItemArea[] = tree.isSuccess
    ? tree.data.map((node) => ({ id: node.id, path: projectRelativePath(tree.data, node) }))
    : [];

  return (
    <InlinePickerField<WorkItemArea>
      label={cardTexts.area.label}
      labelPlacement="start"
      options={options}
      value={value}
      onChange={(area) => {
        if (area !== null) {
          onChange(area);
        }
      }}
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
      loadingText={areaTexts.loading}
      noOptionsText={failed ? areaTexts.failed : areaTexts.noMatch}
      error={error}
      busy={busy}
      readOnly={readOnly}
      ref={inputRef}
    />
  );
}

export const AreaField = memo(AreaFieldView);
