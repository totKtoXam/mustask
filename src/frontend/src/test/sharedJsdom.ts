import { populateGlobal, type Environment } from 'vitest/environments';

/**
 * Vitest's own `jsdom` environment, except that the window outlives the file (tech-029).
 *
 * With `isolate: false` the files one worker runs share a module cache, but the built-in
 * environment builds a new window per file and closes it afterwards: Testing Library's
 * `screen` and React DOM keep pointing at the first, closed window and every later file
 * sees an empty `<body />`. Here one window serves the whole worker; the document is
 * emptied between files and `setup.ts` resets what a test itself leaves behind.
 */
const WINDOW = Symbol.for('mustask.sharedJsdom');

/** The part of jsdom this file touches; the package ships no types and none are installed. */
interface JsdomLike {
  window: Window & typeof globalThis;
}
type JsdomConstructor = new (html: string, options: Record<string, unknown>) => JsdomLike;

type Shared = typeof globalThis & { [WINDOW]?: JsdomLike };

const environment: Environment = {
  name: 'shared-jsdom',
  transformMode: 'web',
  async setup(global, options) {
    const holder = globalThis as Shared;
    const { url = 'http://localhost/' } = (options as { jsdom?: { url?: string } }).jsdom ?? {};
    if (!holder[WINDOW]) {
      // A variable specifier: TypeScript would otherwise look for jsdom's declarations.
      const specifier = 'jsdom';
      const { JSDOM } = (await import(/* @vite-ignore */ specifier)) as { JSDOM: JsdomConstructor };
      holder[WINDOW] = new JSDOM('<!DOCTYPE html>', {
        pretendToBeVisual: true,
        runScripts: 'dangerously',
        url,
      });
    }
    const dom = holder[WINDOW];
    const window = dom.window;
    // What Vitest's environment does for the same reason.
    const target = window as unknown as Record<string, unknown>;
    target.Buffer = Buffer;
    for (const name of ['structuredClone', 'fetch', 'Request', 'Response', 'TextEncoder', 'TextDecoder']) {
      const value = (globalThis as unknown as Record<string, unknown>)[name];
      if (value !== undefined && target[name] === undefined) target[name] = value;
    }
    // A fresh document for the file: the previous file's nodes and head are gone.
    window.document.body.innerHTML = '';
    window.document.head.innerHTML = '';
    for (const element of [window.document.documentElement, window.document.body]) {
      for (const attribute of Array.from(element.attributes)) {
        element.removeAttribute(attribute.name);
      }
    }

    populateGlobal(global, window, { bindFunctions: true });
    (global as unknown as Record<string, unknown>).jsdom = dom;
    // The globals stay in place after the file: the window is the same for the next
    // one, and a callback of this file that fires late (a pending `waitFor`, a timer)
    // must still find `MutationObserver` and friends.
    return { teardown() {} };
  },
};

export default environment;
