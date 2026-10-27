import { memo } from 'react';
import { InlineTextField } from '../../../../shared/ui';
import { cardTexts } from '../../lib/cardTexts';

/** A read-only field has nothing to change. */
function noop(): void {}

/**
 * «Проект» of the band (feat-040 design § 7, BR-TS-3, D-2): block A, row 2, under «Статус» — the
 * name of the item's project as text, read-only for every role and in every state: an item does not
 * move between projects, so there is no picker. It is the project as it stands now (a rename shows at
 * once, api-contract § 2.1). «Команда» is the item's project's, as before.
 */
function ProjectFieldView({ name }: { name: string }) {
  return (
    <InlineTextField
      label={cardTexts.project.label}
      labelPlacement="start"
      value={name}
      onChange={noop}
      readOnly
    />
  );
}

export const ProjectField = memo(ProjectFieldView);
