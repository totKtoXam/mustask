import { textblockTypeInputRule } from '@tiptap/core';
import { CodeBlock, backtickInputRegex } from '@tiptap/extension-code-block';
import { CODE_LANGUAGES, firstToken, tokenByName } from '../../../lib/richText';

/**
 * The code block of the subset (BR-RT-16, ADR-0027 decision 7): text only, no
 * marks, isolating (default commands never merge across its boundary), the
 * language one of the closed list or none.
 *
 * Configured off the stock extension: its own exits (triple Enter, arrows) are
 * off because the product's exits are explicit (`behaviour/keyboard.ts`, slice
 * 5); its VS Code paste handler is dropped because it would take a language
 * name from the clipboard that is not in the closed list; the tilde input rule
 * is dropped (design D-4 lists backticks only).
 */
export const RtCodeBlock = CodeBlock.configure({
  exitOnTripleEnter: false,
  exitOnArrowDown: false,
  exitOnArrowUp: false,
  enableTabIndentation: false,
  defaultLanguage: null,
}).extend({
  isolating: true,

  addAttributes() {
    return {
      language: {
        default: null,
        parseHTML: (element: HTMLElement) => {
          const code = Array.from(element.children).find((child) => child.localName === 'code');
          return firstToken(CODE_LANGUAGES, code?.getAttribute('class'))?.name ?? null;
        },
        rendered: false,
      },
    };
  },

  renderHTML({ node }) {
    const token = tokenByName(CODE_LANGUAGES, node.attrs.language as string | null);
    return ['pre', ['code', token ? { class: token.class } : {}, 0]];
  },

  addInputRules() {
    return [
      textblockTypeInputRule({
        find: backtickInputRegex,
        type: this.type,
        getAttributes: (match) => ({
          language: tokenByName(CODE_LANGUAGES, match[1])?.name ?? null,
        }),
      }),
    ];
  },

  addProseMirrorPlugins() {
    return [];
  },
});
