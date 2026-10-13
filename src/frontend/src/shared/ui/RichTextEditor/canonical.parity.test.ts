import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { Editor, getSchema } from '@tiptap/core';
import { afterEach, describe, expect, it } from 'vitest';
import { normaliseRichText } from '../../lib/richText';
import { documentJson } from './behaviour/loadDocument';
import { createExtensions } from './schema/extensions';

/**
 * US-3 / US-7, the client half: the editor's schema and the shared fixtures
 * agree on the canonical form. Each canonical string is loaded into the real
 * editor (its schema, its serialisation), serialised, and normalised: the
 * result is the canonical string again (plan-frontend § 4.6). The hostile
 * corpus is loaded as the stored value itself, so what the editor holds and
 * renders is checked, not only what the normaliser returns (US-3 "stored markup
 * outside the subset is not rendered").
 */

function load<T>(name: string): T[] {
  const file = path.resolve(
    path.dirname(fileURLToPath(import.meta.url)),
    '../../../../../../contracts/rich-text/fixtures',
    name,
  );
  return (JSON.parse(readFileSync(file, 'utf8')) as { cases: T[] }).cases;
}

const created: Editor[] = [];

function open(html: string): Editor {
  const extensions = createExtensions('');
  const editor = new Editor({
    extensions,
    content: documentJson(getSchema(extensions), html),
  });
  created.push(editor);
  return editor;
}

afterEach(() => {
  for (const editor of created.splice(0)) editor.destroy();
});

interface CanonicalCase {
  id: string;
  input: string;
  canonical: string;
  editorOutput?: string;
}

describe('canonical.json through the real editor', () => {
  const cases = load<CanonicalCase>('canonical.json');

  it.each(cases.map((c) => [c.id, c] as const))(
    '%s: load canonical, serialise, normalise: canonical',
    (_id, c) => {
      const serialised = open(c.canonical).getHTML();

      expect(normaliseRichText(serialised).html).toBe(c.canonical);
    },
  );

  it.each(cases.map((c) => [c.id, c] as const))(
    '%s: the reduced input loads and serialises to the same canonical',
    (_id, c) => {
      const reduced = normaliseRichText(c.input).html;

      expect(normaliseRichText(open(reduced).getHTML()).html).toBe(c.canonical);
    },
  );
});

describe('the editor serialises the canonical form itself', () => {
  it('writes no style, class outside the vocabulary, id, colgroup or colspan for a table and a link', () => {
    const html = open(
      '<table><tbody><tr><th><p>h</p></th><td><p>d</p></td></tr></tbody></table>' +
        '<p><a href="https://example.org" rel="noopener noreferrer">l</a></p>',
    ).getHTML();

    expect(html).toBe(
      '<table><tbody><tr><th><p>h</p></th><td><p>d</p></td></tr></tbody></table>' +
        '<p><a href="https://example.org" rel="noopener noreferrer">l</a></p>',
    );
  });
});

interface HostileCase {
  id: string;
  input: string;
  mustNotContain: string[];
}

describe('hostile-corpus.json: the stored markup itself is not rendered', () => {
  const cases = load<HostileCase>('hostile-corpus.json');

  it.each(cases.map((c) => [c.id, c] as const))('%s', (_id, c) => {
    const editor = open(c.input);
    // What the editor serialises and what it puts on the page (the content, not its own root).
    const surfaces = [editor.getHTML(), editor.view.dom.innerHTML].map((surface) =>
      surface.toLowerCase(),
    );

    for (const surface of surfaces) {
      for (const needle of c.mustNotContain) {
        expect(surface).not.toContain(needle.toLowerCase());
      }
      expect(surface).not.toMatch(
        /<script|<style|<iframe|<img|<svg|<form|\son[a-z]+=|\sstyle=|\sid=|\sname=/,
      );
    }
  });
});
