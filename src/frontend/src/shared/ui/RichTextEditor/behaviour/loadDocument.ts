import type { JSONContent } from '@tiptap/core';
import { DOMParser as ProseMirrorDOMParser, type Schema } from '@tiptap/pm/model';
import { cleanText } from '../../../lib/richText';

const WRAPPER = '<!DOCTYPE html><html><head></head><body>';

/**
 * The editor's document for a stored value (plan-frontend § 5.3, F-2): built
 * here as ProseMirror JSON, never handed to TipTap as a string. TipTap's
 * string path strips some whitespace-only text nodes (a code block of one line
 * feed would be lost) and has its own parse options; JSON is exact, and the
 * schema stays the single judge of what the editor can hold — whatever the
 * markup carries that the schema cannot (a script, an event attribute, a
 * `style`, a class outside the vocabulary, a refused address) is not in the
 * result.
 *
 * `preserveWhitespace: 'full'` is required: canonical text keeps leading
 * spaces, tabs and runs of spaces (`indentation-kept`), which the default parse
 * would collapse. The parse runs on an inert document (no script, no fetch).
 * An empty value is one empty paragraph.
 */
export function documentJson(schema: Schema, html: string): JSONContent {
  const parsed = new window.DOMParser().parseFromString(WRAPPER + cleanText(html), 'text/html');
  const doc = ProseMirrorDOMParser.fromSchema(schema).parse(parsed.body, {
    preserveWhitespace: 'full',
  });
  return doc.toJSON() as JSONContent;
}
