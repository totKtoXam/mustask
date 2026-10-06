import Box from '@mui/material/Box';

/**
 * The page's visually hidden `role="status"` region (spec § Page «Бэклоги»,
 * § Page «Доски»; BR-M4-8): a move is announced here — «<ID> перемещён:
 * позиция <n> из <m>.», «<ID> перемещён в колонку «<колонка>».». Rendered from
 * the page's first frame, so the region exists before its first message.
 */
export function StatusRegion({ text }: { text: string }) {
  return (
    <Box role="status" sx={(theme) => theme.mixins.visuallyHidden}>
      {text}
    </Box>
  );
}
