import { Mark } from '@tiptap/core';
import {
  FONT_SIZES,
  HIGHLIGHTS,
  TEXT_COLOURS,
  firstToken,
  tokenByName,
  type VocabularyToken,
} from '../../../lib/richText';

/**
 * The three palette marks (ADR-0037 decision 4): in-house, because the shipped
 * highlight, text-style and colour extensions write an inline `style`. Each
 * holds the token's *name* (`red`, `lg`), reads its element by the exact class
 * token (`firstToken`) and writes the class from the vocabulary table only —
 * never `style`, never a value taken from the input (BR-RT-14).
 */
function createTokenMark(
  name: string,
  priority: number,
  tag: 'mark' | 'span',
  family: readonly VocabularyToken[],
) {
  return Mark.create({
    name,
    priority,
    addAttributes() {
      return { token: { default: null, rendered: false } };
    },
    parseHTML() {
      return [
        {
          tag,
          getAttrs: (element) => {
            const token = firstToken(family, (element as HTMLElement).getAttribute('class'));
            return token ? { token: token.name } : false;
          },
        },
      ];
    },
    renderHTML({ mark }) {
      const token = tokenByName(family, mark.attrs.token as string | null);
      return [tag, token ? { class: token.class } : {}, 0];
    },
  });
}

// Priorities fix the canonical nesting: link 1000 > bold 900 > italic 800 >
// underline 700 > strike 600 > highlight 500 > colour 400 > size 300 > code 200
// (BR-RT-15; `extensions.ts`).
export const RtHighlight = createTokenMark('rtHighlight', 500, 'mark', HIGHLIGHTS);
export const RtColor = createTokenMark('rtColor', 400, 'span', TEXT_COLOURS);
export const RtSize = createTokenMark('rtSize', 300, 'span', FONT_SIZES);
