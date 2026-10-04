import Box from '@mui/material/Box';
import Skeleton from '@mui/material/Skeleton';
import Stack from '@mui/material/Stack';

interface FormSkeletonProps {
  /** The number of "label + field" pairs (the card/form variant). */
  fields?: number;
}

/**
 * The card/form skeleton: a label above a field, repeated per field.
 *
 * Shared since mod-015 (design § 9.8), sized by the spacing unit: a label
 * line and a field-high block per field.
 */
export function FormSkeleton({ fields = 3 }: FormSkeletonProps) {
  return (
    <Stack
      spacing={2}
      role="status"
      aria-label="Загрузка данных"
      sx={{ maxWidth: (theme) => theme.layout.formColumnWidth }}
    >
      {Array.from({ length: fields }, (_, index) => (
        <Box key={index}>
          <Skeleton
            sx={{ width: (theme) => theme.spacing(17.5), height: (theme) => theme.spacing(2.5) }}
          />
          <Skeleton variant="rounded" sx={{ height: (theme) => theme.spacing(5) }} />
        </Box>
      ))}
    </Stack>
  );
}
