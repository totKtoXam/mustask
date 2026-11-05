/** What a click event says about itself, so a lib function needs no React types. */
interface ClickLike {
  button: number;
  ctrlKey: boolean;
  metaKey: boolean;
  shiftKey: boolean;
  altKey: boolean;
}

/**
 * A click that follows a link in place: the main button, no Ctrl, Cmd, Shift or
 * Alt. A modified click or a middle click is the browser's own — a new tab, the
 * full page (BR-C-1) — and a title that opens the dialog leaves it alone.
 */
export function isPlainClick(event: ClickLike): boolean {
  return event.button === 0 && !event.ctrlKey && !event.metaKey && !event.shiftKey && !event.altKey;
}
