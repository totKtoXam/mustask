import { textblockTypeInputRule } from '@tiptap/core';
import { CodeBlock, backtickInputRegex, type CodeBlockOptions } from '@tiptap/extension-code-block';
import {
  CODE_LANGUAGES,
  CODE_LANGUAGE_PLAIN,
  firstToken,
  tokenByName,
} from '../../../lib/richText';
import { bindingsOf } from '../behaviour/bindings';

/** The texts of the strip above a block; the strip is chrome and never stored. */
export interface CodeChrome {
  /** The label by vocabulary name, and `plain`. */
  languages: Record<string, string>;
  copyLabel: string;
}

const COPY_ICON_PATH =
  'M16 1H4c-1.1 0-2 .9-2 2v14h2V3h12V1zm3 4H8c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h11c1.1 0 2-.9 2-2V7c0-1.1-.9-2-2-2zm0 16H8V7h11v14z';

/**
 * The code block of the subset (BR-RT-16, ADR-0037 decision 7): text only, no
 * marks, isolating (default commands never merge across its boundary), the
 * language one of the closed list or none.
 *
 * Configured off the stock extension: its own exits (triple Enter, arrows) are
 * off because the product's exits are explicit (`behaviour/keyboard.ts`); its
 * VS Code paste handler is dropped because it would take a language name from
 * the clipboard that is not in the closed list; the tilde input rule is
 * dropped (design D-4 lists backticks only).
 *
 * The node view draws the strip — the language name and «Копировать код» for
 * the pointer (`tabIndex` -1: the same two actions are in the toolbar for the
 * keyboard, D-3) — around `pre > code`; the strip is never in `renderHTML`.
 */
export const RtCodeBlock = CodeBlock.extend<CodeBlockOptions & { chrome: CodeChrome }>({
  isolating: true,

  addOptions() {
    return {
      ...(this.parent?.() as CodeBlockOptions),
      exitOnTripleEnter: false,
      exitOnArrowDown: false,
      exitOnArrowUp: false,
      enableTabIndentation: false,
      defaultLanguage: null,
      chrome: { languages: { [CODE_LANGUAGE_PLAIN]: 'plain' }, copyLabel: 'Copy' },
    };
  },

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

  addNodeView() {
    const { editor } = this;
    const { chrome } = this.options;

    return ({ node: initial }) => {
      let node = initial;
      const labelOf = (language: unknown) =>
        chrome.languages[typeof language === 'string' ? language : CODE_LANGUAGE_PLAIN] ??
        chrome.languages[CODE_LANGUAGE_PLAIN] ??
        '';

      const dom = document.createElement('div');
      dom.className = 'rt-codeblock';

      const strip = document.createElement('div');
      strip.className = 'rt-codeblock-strip';
      strip.contentEditable = 'false';

      const label = document.createElement('span');
      label.className = 'rt-codeblock-label';
      label.textContent = labelOf(node.attrs.language);

      const copy = document.createElement('button');
      copy.type = 'button';
      copy.tabIndex = -1;
      copy.className = 'rt-codeblock-copy';
      copy.title = chrome.copyLabel;
      copy.setAttribute('aria-label', chrome.copyLabel);
      const icon = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
      icon.setAttribute('viewBox', '0 0 24 24');
      icon.setAttribute('aria-hidden', 'true');
      const path = document.createElementNS('http://www.w3.org/2000/svg', 'path');
      path.setAttribute('d', COPY_ICON_PATH);
      icon.appendChild(path);
      copy.appendChild(icon);
      // The pointer never takes the focus from the body.
      copy.addEventListener('mousedown', (event) => event.preventDefault());
      copy.addEventListener('click', () => {
        if (!navigator.clipboard) {
          bindingsOf(editor).notice('copyFailed');
          return;
        }
        void navigator.clipboard.writeText(node.textContent).then(
          () => bindingsOf(editor).notice('copied'),
          () => bindingsOf(editor).notice('copyFailed'),
        );
      });

      strip.append(label, copy);

      const pre = document.createElement('pre');
      const code = document.createElement('code');
      pre.appendChild(code);
      dom.append(strip, pre);

      return {
        dom,
        contentDOM: code,
        update: (updated) => {
          if (updated.type !== node.type) return false;
          node = updated;
          label.textContent = labelOf(node.attrs.language);
          return true;
        },
        ignoreMutation: (mutation) => strip.contains(mutation.target),
        stopEvent: (event) => strip.contains(event.target as Node),
      };
    };
  },
});
