/** `Mod+Shift+Z` as the tooltip shows it: «Ctrl+Shift+Z», or «⌘⇧Z» on macOS. */
export function formatShortcut(shortcut: string, mac: boolean): string {
  const parts = shortcut.split('+');
  if (!mac) return parts.map((part) => (part === 'Mod' ? 'Ctrl' : part)).join('+');
  const glyphs: Record<string, string> = { Mod: '⌘', Shift: '⇧', Alt: '⌥' };
  return parts.map((part) => glyphs[part] ?? part).join('');
}
