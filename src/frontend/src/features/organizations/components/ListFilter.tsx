import type { ReactNode } from 'react';
import { InlineTextField } from '../../../shared/ui';
import { FilterBar } from '../../../shared/ui/FilterBar';

/**
 * The filter band of the organization's lists (feat-043 design § 5.2-5.3): one search field, named
 * «Найти …», and the host's other fields. «Очистить фильтры» appears while the text is set.
 */
export function ListFilter({
  label,
  text,
  onText,
  children,
}: {
  label: string;
  text: string;
  onText: (text: string) => void;
  children?: ReactNode;
}) {
  return (
    <FilterBar active={text !== ''} onClear={() => onText('')}>
      <InlineTextField
        label={label}
        labelPlacement="hidden"
        placeholder={label}
        frame="always"
        value={text}
        onChange={onText}
      />
      {children}
    </FilterBar>
  );
}
