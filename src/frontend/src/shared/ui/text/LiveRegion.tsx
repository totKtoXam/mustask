import Box from '@mui/material/Box';

export interface LiveRegionProps {
  /** What is announced now. */
  text: string;
  /**
   * Counts the messages: a text equal to the last one would be silent, so each
   * message is drawn in a node of its own, replaced when `serial` changes — a repeat
   * is spoken again (feat-027 F-11).
   */
  serial?: number;
}

/**
 * A page's visually hidden `role="status"` region (tech-015 design § 5.5; the
 * backlog's `StatusRegion`, which it replaces). Render it from the page's first
 * frame, so the region exists before its first message.
 */
export function LiveRegion({ text, serial = 0 }: LiveRegionProps) {
  return (
    <Box role="status" sx={(theme) => theme.mixins.visuallyHidden}>
      <span key={serial}>{text}</span>
    </Box>
  );
}
