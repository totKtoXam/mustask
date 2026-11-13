/**
 * The spreadsheet rule (BR-RT-17, plan § 7.6): plain text pasted from a
 * spreadsheet is a table when it has at least three lines (one final empty
 * line from a trailing line feed is ignored) and every line has the same
 * number n >= 1 of tab characters; the cells are the text between the tabs.
 * Anything else — a ragged or short block, a single tab-separated line — is
 * ordinary text. The table has no header row (every cell is a `td`).
 */
export function tabularRows(text: string): string[][] | null {
  const lines = text.split(/\r\n|\r|\n/);
  if (lines.length > 1 && lines[lines.length - 1] === '') lines.pop();
  if (lines.length < 3) return null;

  const tabs = (lines[0] as string).split('\t').length - 1;
  if (tabs < 1) return null;
  for (const line of lines) {
    if (line.split('\t').length - 1 !== tabs) return null;
  }
  return lines.map((line) => line.split('\t'));
}
