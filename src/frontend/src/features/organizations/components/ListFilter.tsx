import type { ReactNode } from 'react';
import Box from '@mui/material/Box';
import { InlineTextField } from '../../../shared/ui';
import { FilterBar } from '../../../shared/ui/FilterBar';

/**
 * The filter band of the organization's lists (feat-043 design § 5.2-5.3): one search field, named
 * «Найти …», and the host's other fields. «Очистить фильтры» appears while the text is set.
 * `fieldWidth` fixes the field's width in px (the «Структура» field, doc-016 design § 7.2: about 360);
 * `escapeClears` makes Esc in the field clear it.
 */
export function ListFilter({
  label,
  text,
  onText,
  fieldWidth,
  escapeClears = false,
  children,
}: {
  label: string;
  text: string;
  onText: (text: string) => void;
  fieldWidth?: number;
  escapeClears?: boolean;
  children?: ReactNode;
}) {
  const field = (
    <InlineTextField
      label={label}
      labelPlacement="hidden"
      placeholder={label}
      frame="always"
      value={text}
      onChange={onText}
    />
  );
  return (
    <FilterBar active={text !== ''} onClear={() => onText('')}>
      {fieldWidth === undefined && !escapeClears ? (
        field
      ) : (
        <Box
          sx={fieldWidth === undefined ? undefined : { width: fieldWidth, maxWidth: '100%' }}
          onKeyDown={(event) => {
            if (escapeClears && event.key === 'Escape' && text !== '') onText('');
          }}
        >
          {field}
        </Box>
      )}
      {children}
    </FilterBar>
  );
}
