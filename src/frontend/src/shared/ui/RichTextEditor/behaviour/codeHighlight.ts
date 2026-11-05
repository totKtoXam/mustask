import { Extension } from '@tiptap/core';
import type { Node as ProseMirrorNode } from '@tiptap/pm/model';
import { Plugin, PluginKey } from '@tiptap/pm/state';
import { Decoration, DecorationSet, type EditorView } from '@tiptap/pm/view';
import bash from 'highlight.js/lib/languages/bash';
import csharp from 'highlight.js/lib/languages/csharp';
import css from 'highlight.js/lib/languages/css';
import javascript from 'highlight.js/lib/languages/javascript';
import json from 'highlight.js/lib/languages/json';
import markdown from 'highlight.js/lib/languages/markdown';
import python from 'highlight.js/lib/languages/python';
import sql from 'highlight.js/lib/languages/sql';
import typescript from 'highlight.js/lib/languages/typescript';
import xml from 'highlight.js/lib/languages/xml';
import yaml from 'highlight.js/lib/languages/yaml';
import { createLowlight } from 'lowlight';
import { codePointLength } from '../../../lib/richText';

/**
 * Code colouring (ADR-0037 decision 4, design § Syntax colouring, F-3):
 * `lowlight` over the twelve closed languages, applied as ProseMirror
 * **decorations** over the block's text — never inserted markup, never stored
 * (`getHTML()` and the normaliser never see it), so a colour-only repaint is
 * never a dirty form.
 *
 * Not `@tiptap/extension-code-block-lowlight`: its plugin recomputes every code
 * block synchronously on any change inside one, with no cap and no delay. Here
 * a block is recomputed 150 ms after the last change, for the edited blocks
 * only, and a block of more than 20 000 code points or 1 000 lines is not
 * coloured (D-9). `createLowlight` is imported by name with the grammars one by
 * one: the namespace import would bundle the `all` set.
 */

const lowlight = createLowlight({
  bash,
  csharp,
  css,
  javascript,
  json,
  markdown,
  python,
  sql,
  typescript,
  xml,
  yaml,
});
// `html` is the xml grammar's.
lowlight.registerAlias({ xml: ['html'] });

/** The grammar names of the closed list the highlighter holds (the vocabulary is checked against it). */
export function isRegisteredLanguage(name: string): boolean {
  return lowlight.registered(name);
}

/** The grammar names the highlighter holds, aliases not included. */
export function registeredLanguages(): string[] {
  return lowlight.listLanguages();
}

/** The idle delay before an edited block is coloured again. */
export const HIGHLIGHT_DELAY_MS = 150;
export const HIGHLIGHT_MAX_CODE_POINTS = 20_000;
export const HIGHLIGHT_MAX_LINES = 1_000;

/** The eight token classes of the design; the theme gives each its colour. */
export const CODE_TOKENS = [
  'keyword',
  'string',
  'number',
  'comment',
  'function',
  'type',
  'attribute',
  'punctuation',
] as const;

export type CodeToken = (typeof CODE_TOKENS)[number];

/** highlight.js scope (without `hljs-`) to the design's token. A scope not listed is not coloured. */
const SCOPE_TOKEN: Readonly<Record<string, CodeToken>> = {
  keyword: 'keyword',
  literal: 'keyword',
  name: 'keyword',
  'selector-tag': 'keyword',
  tag: 'keyword',
  built_in: 'keyword',
  string: 'string',
  regexp: 'string',
  char: 'string',
  number: 'number',
  comment: 'comment',
  doctag: 'comment',
  quote: 'comment',
  title: 'function',
  section: 'function',
  type: 'type',
  attr: 'attribute',
  attribute: 'attribute',
  property: 'attribute',
  'selector-class': 'attribute',
  'selector-id': 'attribute',
  'selector-attr': 'attribute',
  punctuation: 'punctuation',
  operator: 'punctuation',
};

/** The token of one element's class list (`['hljs-title', 'class_']` is a type name). */
export function tokenOf(classes: readonly string[]): CodeToken | null {
  if (classes.includes('class_')) return 'type';
  if (classes.includes('function_')) return 'function';
  for (const name of classes) {
    if (!name.startsWith('hljs-')) continue;
    const token = SCOPE_TOKEN[name.slice(5)];
    if (token) return token;
  }
  return null;
}

interface HastNode {
  type: string;
  value?: string;
  properties?: { className?: unknown };
  children?: HastNode[];
}

/** The decorations of one block's text; `start` is the position of its first character. */
export function highlightRanges(
  language: string,
  text: string,
): { from: number; to: number; token: CodeToken }[] {
  const ranges: { from: number; to: number; token: CodeToken }[] = [];
  const tree = lowlight.highlight(language, text) as unknown as HastNode;
  let offset = 0;

  const visit = (node: HastNode, inherited: CodeToken | null) => {
    if (node.type === 'text') {
      const length = node.value?.length ?? 0;
      if (inherited && length > 0)
        ranges.push({ from: offset, to: offset + length, token: inherited });
      offset += length;
      return;
    }
    const raw = node.properties?.className;
    const classes = Array.isArray(raw) ? (raw as string[]) : [];
    const own = node.type === 'element' ? tokenOf(classes) : null;
    for (const child of node.children ?? []) visit(child, own ?? inherited);
  };
  visit(tree, null);
  return ranges;
}

function isColourable(node: ProseMirrorNode): string | null {
  const language = node.attrs.language as string | null;
  if (!language || !lowlight.registered(language)) return null;
  const text = node.textContent;
  if (codePointLength(text) > HIGHLIGHT_MAX_CODE_POINTS) return null;
  let lines = 1;
  for (let index = 0; index < text.length; index++) if (text.charCodeAt(index) === 10) lines++;
  if (lines > HIGHLIGHT_MAX_LINES) return null;
  return language;
}

interface HighlightState {
  decorations: DecorationSet;
  /** Positions of the code blocks whose colouring is out of date. */
  dirty: number[];
}

const key = new PluginKey<HighlightState>('rtCodeHighlight');

function codeBlockPositions(doc: ProseMirrorNode): number[] {
  const positions: number[] = [];
  doc.descendants((node, position) => {
    if (node.type.name === 'codeBlock') positions.push(position);
    return node.type.name !== 'codeBlock';
  });
  return positions;
}

function recompute(view: EditorView): void {
  const state = key.getState(view.state);
  if (!state || state.dirty.length === 0) return;

  const { doc } = view.state;
  let decorations = state.decorations;
  for (const position of state.dirty) {
    const node = position <= doc.content.size ? doc.nodeAt(position) : null;
    if (!node || node.type.name !== 'codeBlock') continue;
    const end = position + node.nodeSize;
    decorations = decorations.remove(decorations.find(position, end));
    const language = isColourable(node);
    if (!language) continue;
    const created = highlightRanges(language, node.textContent).map((range) =>
      Decoration.inline(position + 1 + range.from, position + 1 + range.to, {
        class: `rt-code-${range.token}`,
      }),
    );
    decorations = decorations.add(doc, created);
  }
  view.dispatch(view.state.tr.setMeta(key, { decorations, dirty: [] } satisfies HighlightState));
}

export const RtCodeHighlight = Extension.create({
  name: 'rtCodeHighlight',

  addProseMirrorPlugins() {
    return [
      new Plugin<HighlightState>({
        key,
        state: {
          init: (_config, instance) => ({
            decorations: DecorationSet.empty,
            dirty: codeBlockPositions(instance.doc),
          }),
          apply: (transaction, value) => {
            const meta = transaction.getMeta(key) as HighlightState | undefined;
            if (meta) return meta;
            if (!transaction.docChanged) return value;

            const dirty = new Set(value.dirty.map((position) => transaction.mapping.map(position)));
            // Blocks the steps touched, in the new document.
            transaction.mapping.maps.forEach((map, index) => {
              const rest = transaction.mapping.slice(index + 1);
              map.forEach((_oldStart, _oldEnd, newStart, newEnd) => {
                const from = Math.max(0, rest.map(newStart, -1));
                const to = Math.min(transaction.doc.content.size, rest.map(newEnd, 1));
                transaction.doc.nodesBetween(from, to, (node, position) => {
                  if (node.type.name === 'codeBlock') dirty.add(position);
                  return node.type.name !== 'codeBlock';
                });
              });
            });

            return {
              decorations: value.decorations.map(transaction.mapping, transaction.doc),
              dirty: [...dirty].sort((a, b) => a - b),
            };
          },
        },
        props: {
          decorations: (state) => key.getState(state)?.decorations ?? DecorationSet.empty,
        },
        view: (editorView) => {
          let timer: ReturnType<typeof setTimeout> | null = null;
          let destroyed = false;

          const schedule = () => {
            if (timer !== null) clearTimeout(timer);
            timer = setTimeout(() => {
              timer = null;
              if (!destroyed) recompute(editorView);
            }, HIGHLIGHT_DELAY_MS);
          };

          if ((key.getState(editorView.state)?.dirty.length ?? 0) > 0) schedule();

          return {
            update: (view, previous) => {
              const now = key.getState(view.state);
              if (now && now.dirty.length > 0 && now !== key.getState(previous)) schedule();
            },
            destroy: () => {
              destroyed = true;
              if (timer !== null) clearTimeout(timer);
            },
          };
        },
      }),
    ];
  },
});
