import Box from '@mui/material/Box';
import Skeleton from '@mui/material/Skeleton';
import Stack from '@mui/material/Stack';

interface FormSkeletonProps {
  /** Число пар "подпись + поле" (вариант "карточка/форма"). */
  fields?: number;
}

/** Скелетон карточки/формы: подпись над полем, повторено per field. */
export function FormSkeleton({ fields = 3 }: FormSkeletonProps) {
  return (
    <Stack spacing={2} role="status" aria-label="Загрузка данных">
      {Array.from({ length: fields }, (_, index) => (
        <Box key={index}>
          <Skeleton width={140} height={20} />
          <Skeleton variant="rounded" height={40} />
        </Box>
      ))}
    </Stack>
  );
}
