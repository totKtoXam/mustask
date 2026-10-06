import Box from '@mui/material/Box';
import Skeleton from '@mui/material/Skeleton';

/** Columns drawn while the board is read: its columns are not known yet. */
const SKELETON_COLUMNS = 4;
const SKELETON_CARDS = 3;

/**
 * The board's loading state (spec § Page «Доски»: column skeletons), in
 * `role="status"` «Загрузка данных» as every skeleton of the panel; header and
 * tabs stay above it.
 */
export function BoardSkeleton() {
  return (
    <Box
      role="status"
      aria-label="Загрузка данных"
      sx={{ display: 'flex', gap: 2, overflowX: 'auto', pb: 2 }}
    >
      {Array.from({ length: SKELETON_COLUMNS }, (_, columnIndex) => (
        <Box
          key={columnIndex}
          sx={(theme) => ({
            width: theme.layout.boardColumnWidth,
            flexShrink: 0,
            display: 'flex',
            flexDirection: 'column',
            gap: 1,
          })}
        >
          <Skeleton variant="text" sx={{ width: '50%' }} />
          {Array.from({ length: SKELETON_CARDS }, (_, cardIndex) => (
            <Skeleton
              key={cardIndex}
              variant="rounded"
              sx={(theme) => ({ height: theme.spacing(10) })}
            />
          ))}
        </Box>
      ))}
    </Box>
  );
}
