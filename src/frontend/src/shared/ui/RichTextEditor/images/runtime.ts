import type { Editor } from '@tiptap/core';
import type { RichTextCopy } from '../copy';
import type { RichTextImages } from './host';

/** What reading a stored image gave (design § Image block, states). */
export type AddressState =
  { status: 'loading' } | { status: 'ready'; url: string } | { status: 'failed' };

const LOADING: AddressState = { status: 'loading' };
const FAILED: AddressState = { status: 'failed' };

interface Entry {
  state: AddressState;
  abort: AbortController | null;
  listeners: Set<() => void>;
}

/**
 * The editor's reads of stored images: one request per reference, shared by the image blocks, the
 * reduction's preview and the full-size view (design § Host seam, "the address rule"). The host's
 * address (an object URL, ADR-0045) is released when the editor goes: nothing is revoked while a
 * block may still show it, and nothing is left behind after.
 */
export class AddressCache {
  private readonly entries = new Map<string, Entry>();
  private disposed = false;

  private readonly host: () => RichTextImages | null;

  constructor(host: () => RichTextImages | null) {
    this.host = host;
  }

  snapshot(reference: string): AddressState {
    return this.entries.get(reference)?.state ?? LOADING;
  }

  /** Subscribes to a reference and starts its read on the first subscriber. */
  subscribe(reference: string, listener: () => void): () => void {
    let entry = this.entries.get(reference);
    if (!entry) {
      entry = { state: LOADING, abort: null, listeners: new Set() };
      this.entries.set(reference, entry);
      this.load(reference, entry);
    }
    entry.listeners.add(listener);
    return () => entry.listeners.delete(listener);
  }

  private load(reference: string, entry: Entry) {
    const host = this.host();
    if (!host || this.disposed) {
      this.set(entry, FAILED);
      return;
    }
    const abort = new AbortController();
    entry.abort = abort;
    host.address(reference, abort.signal).then(
      (url) => {
        if (this.disposed || abort.signal.aborted) {
          host.release?.(url);
          return;
        }
        this.set(entry, { status: 'ready', url });
      },
      () => {
        if (!abort.signal.aborted) this.set(entry, FAILED);
      },
    );
  }

  private set(entry: Entry, state: AddressState) {
    entry.state = state;
    entry.listeners.forEach((listener) => listener());
  }

  /** Aborts the reads in flight and releases every address handed out. */
  dispose() {
    this.disposed = true;
    const host = this.host();
    for (const entry of this.entries.values()) {
      entry.abort?.abort();
      if (entry.state.status === 'ready') host?.release?.(entry.state.url);
    }
    this.entries.clear();
  }
}

/** The image a full-size view or a menu was opened from. */
export interface ImageTarget {
  imageId: string;
  alt: string;
  /** Where focus returns: the node's position when it was opened. */
  pos: number;
}

/**
 * What the image nodes, their keys and the controller share with the React component around the
 * editor: the host, the texts, the address cache and the two surfaces that open from an image.
 * It is created with the component and handed to the extensions, whose node views read it.
 */
export class ImageRuntime {
  host: RichTextImages | null = null;
  copy!: RichTextCopy['images'];
  editor: Editor | null = null;
  readonly addresses = new AddressCache(() => this.host);
  /** The last selection came from the pointer: no keyboard hint (design § Announcements). */
  pointerSelection = false;
  ui: {
    openView: (target: ImageTarget) => void;
    openMenu: (target: ImageTarget, anchor: HTMLElement | null) => void;
    notice: (message: string) => void;
  } = { openView: () => {}, openMenu: () => {}, notice: () => {} };

  /** Sets what the editor around the runtime reads on every use: the host and the texts. */
  configure(host: RichTextImages | null, copy: RichTextCopy['images']) {
    this.host = host;
    this.copy = copy;
  }

  /** Joins the runtime to its editor and the surfaces that open from an image. */
  attach(editor: Editor, ui: ImageRuntime['ui']) {
    this.editor = editor;
    this.ui = ui;
  }

  get enabled(): boolean {
    return this.host !== null;
  }
}
