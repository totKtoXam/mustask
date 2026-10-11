import Box from '@mui/material/Box';

/**
 * The page's visually hidden `role="status"` region (spec § Page «Бэклоги»,
 * § Page «Доски»; BR-M4-8): a move is announced here — «<ID> перемещён:
 * позиция <n> из <m>.», «<ID> перемещён в колонку «<колонка>».». Rendered from
 * the page's first frame, so the region exists before its first message.
 *
 * A screen reader speaks what changes in the region, so a message equal to the
 * last one would be silent. `serial` counts the messages: the text is drawn in a
 * node of its own, replaced for each new message, repeated text included (F-11).
 */
export function StatusRegion({ text, serial = 0 }: { text: string; serial?: number }) {
  return (
    <Box role="status" sx={(theme) => theme.mixins.visuallyHidden}>
      <span key={serial}>{text}</span>
    </Box>
  );
}
