import Box from '@mui/material/Box';
import { FieldShape, LoadingRegion } from './skeletonParts';

export interface FieldsSkeletonProps {
  /** How many fields. */
  count: number;
  /** `top` (default) the label above its value; `start` the band's label-left rows. */
  labelPlacement?: 'top' | 'start';
}

/**
 * Loading fields (tech-015 design § 5.12): `count` shapes of a field's own size — label
 * 16, value 28, so 48 — in one `role="status"` «Загрузка данных». The skeleton mirrors
 * the loaded shape, so nothing moves when the values arrive.
 */
export function FieldsSkeleton({ count, labelPlacement = 'top' }: FieldsSkeletonProps) {
  return (
    <LoadingRegion>
      <Box
        sx={(theme) =>
          labelPlacement === 'start'
            ? {
                display: 'flex',
                flexDirection: 'column',
                rowGap: `${theme.layout.card.bandRowGap}px`,
              }
            : {}
        }
      >
        {Array.from({ length: count }, (_unused, index) => (
          <FieldShape key={index} placement={labelPlacement} />
        ))}
      </Box>
    </LoadingRegion>
  );
}
