import type { Editor } from '@tiptap/core';
import {
  CANNOT_REDUCE_CODE,
  DEFAULT_IMAGE_LIMITS,
  IMAGE_TYPES,
  ImageRefusal,
  type ImageLimits,
  type ImageReductionResult,
} from './host';
import { countImages, dropSlot, findNode, insertPieces, slotPosition, type Piece } from './insert';
import type { ImageRuntime } from './runtime';

/** Where a file came from: a clipboard image has no name worth keeping (US-1: «Изображение»). */
export type FileSource = 'chooser' | 'drop' | 'clipboard';

/** A pasted image the clipboard's html names rather than carries (design § Paste). */
export type PastedImage =
  | { kind: 'own'; id: string; alt: string }
  | { kind: 'data'; src: string; alt: string }
  | { kind: 'foreign' };

export interface Offer {
  file: File;
  slot: string;
  alt: string;
  index: number;
  total: number;
}

/** What the reduction dialog shows (design § Dialog «Изображение больше 5 МБ»). */
export type DialogState =
  | { step: 'offer' | 'reducing' | 'cannot' | 'failed'; offer: Offer }
  | { step: 'result'; offer: Offer; result: ImageReductionResult };

interface Job {
  abort: AbortController;
}

const MAX_ALT = 255;

/** The file name without its extension, cut to the stored alternative's size. */
export function altOf(name: string, fallback: string): string {
  const base = name.replace(/\.[^./\\]*$/, '').trim();
  return Array.from(base === '' ? fallback : base)
    .slice(0, MAX_ALT)
    .join('');
}

let counter = 0;
const newToken = () => `${Date.now().toString(36)}-${(counter++).toString(36)}`;

/** Animated formats are told by their container: GIF frames, APNG `acTL`, WebP `ANIM`. */
export async function isAnimated(file: File): Promise<boolean> {
  const bytes = new Uint8Array(await file.arrayBuffer());
  if (file.type === 'image/gif') {
    let frames = 0;
    for (let i = 0; i + 3 < bytes.length && frames < 2; i++) {
      if (bytes[i] === 0x21 && bytes[i + 1] === 0xf9 && bytes[i + 2] === 0x04) frames++;
    }
    return frames > 1;
  }
  const find = (text: string, limit: number) => {
    const needle = Array.from(text, (c) => c.charCodeAt(0));
    const end = Math.min(bytes.length, limit) - needle.length;
    for (let i = 0; i <= end; i++) {
      if (needle.every((byte, k) => bytes[i + k] === byte)) return true;
    }
    return false;
  };
  if (file.type === 'image/png') return find('acTL', 4096);
  if (file.type === 'image/webp') return find('ANIM', 128);
  return false;
}

/**
 * The editor's image workflow (design § Placeholder, § Paste, drop and the file chooser, § Dialog):
 * the checks before any upload, the placeholders, the uploads and their cancel, the queue of
 * reductions. Plain TypeScript: the React parts read `state` through `subscribe`.
 */
export class ImageController {
  state: DialogState | null = null;
  private readonly listeners = new Set<() => void>();
  private readonly jobs = new Map<string, Job>();
  private queue: Offer[] = [];
  private reduction: AbortController | null = null;
  private idle: (() => void)[] = [];
  private readonly busyListeners = new Set<() => void>();

  private readonly runtime: ImageRuntime;
  private readonly editor: Editor;

  constructor(runtime: ImageRuntime, editor: Editor) {
    this.runtime = runtime;
    this.editor = editor;
    editor.on('update', this.reconcile);
  }

  destroy() {
    this.editor.off('update', this.reconcile);
    this.reduction?.abort();
    for (const job of this.jobs.values()) job.abort.abort();
    this.jobs.clear();
    this.queue = [];
    this.release();
  }

  // --- observable state ---

  subscribe = (listener: () => void) => {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  };

  private emit(state: DialogState | null) {
    this.state = state;
    this.listeners.forEach((listener) => listener());
  }

  /** Uploads still running: a save waits for them (BR-IM-14). */
  get pending(): number {
    return this.jobs.size;
  }

  onBusyChange(listener: () => void): () => void {
    this.busyListeners.add(listener);
    return () => this.busyListeners.delete(listener);
  }

  /** Resolves when no upload is running. */
  whenIdle(): Promise<void> {
    if (this.jobs.size === 0) return Promise.resolve();
    return new Promise((resolve) => this.idle.push(resolve));
  }

  private release() {
    this.busyListeners.forEach((listener) => listener());
    if (this.jobs.size === 0) {
      const waiting = this.idle;
      this.idle = [];
      waiting.forEach((resolve) => resolve());
    }
  }

  private get limits(): ImageLimits {
    return { ...DEFAULT_IMAGE_LIMITS, ...this.runtime.host?.limits };
  }

  // --- the checks and the insert ---

  /** Files from the chooser, a paste or a drop (US-1, US-2). Ignored without a host (design D-8). */
  async insertFiles(files: readonly File[], source: FileSource, pos?: number): Promise<void> {
    const host = this.runtime.host;
    if (!host || files.length === 0) return;
    const { copy } = this.runtime;
    const limits = this.limits;
    const refusals: string[] = [];
    const refuse = (text: string) => {
      if (!refusals.includes(text)) refusals.push(text);
    };

    let count = countImages(this.editor.state.doc) + this.queue.length + (this.state ? 1 : 0);
    const plan: { file: File; offer: boolean }[] = [];
    for (const file of files) {
      if (!(IMAGE_TYPES as readonly string[]).includes(file.type)) {
        refuse(copy.refusals.format);
        continue;
      }
      if (file.size > limits.maxIntakeBytes) {
        refuse(copy.refusals.tooLarge);
        continue;
      }
      const over = file.size > limits.maxBytes;
      if (over && (await isAnimated(file))) {
        refuse(copy.refusals.animation);
        continue;
      }
      if (count >= limits.maxCount) {
        refuse(copy.refusals.count);
        continue;
      }
      count++;
      plan.push({ file, offer: over });
    }

    if (plan.length > 0) {
      const total = plan.filter((item) => item.offer).length;
      let index = 0;
      const pieces: Piece[] = [];
      const started: (() => void)[] = [];
      for (const { file, offer } of plan) {
        const alt = source === 'clipboard' ? copy.defaultAlt : altOf(file.name, copy.defaultAlt);
        if (offer) {
          const slot = newToken();
          pieces.push({ slot });
          this.queue.push({ file, slot, alt, index: ++index, total });
        } else {
          const token = newToken();
          pieces.push({ node: this.placeholder(token) });
          const job: Job = { abort: new AbortController() };
          this.jobs.set(token, job);
          started.push(() => this.upload(token, job, async () => file, alt));
        }
      }
      insertPieces(this.editor, pieces, pos === undefined ? undefined : this.clamp(pos));
      started.forEach((start) => start());
      this.release();
      this.next();
    }

    if (refusals.length > 0) this.runtime.ui.notice(refusals.slice(0, 4).join('\n'));
  }

  /** Images the clipboard's html carried (design § Paste of rich content). */
  insertPasted(images: readonly PastedImage[]): void {
    const host = this.runtime.host;
    if (!host || images.length === 0) return;
    const { copy } = this.runtime;
    const limits = this.limits;
    const refusals: string[] = [];
    let count = countImages(this.editor.state.doc) + this.queue.length + (this.state ? 1 : 0);
    const pieces: Piece[] = [];
    const started: (() => void)[] = [];
    for (const image of images) {
      if (image.kind === 'foreign') {
        if (!refusals.includes(copy.refusals.foreign)) refusals.push(copy.refusals.foreign);
        continue;
      }
      if (count >= limits.maxCount) {
        if (!refusals.includes(copy.refusals.count)) refusals.push(copy.refusals.count);
        continue;
      }
      count++;
      const token = newToken();
      pieces.push({ node: this.placeholder(token) });
      const job: Job = { abort: new AbortController() };
      this.jobs.set(token, job);
      const alt = altOf(image.alt, copy.defaultAlt);
      started.push(() => this.upload(token, job, () => this.fetchPasted(image, job), alt));
    }
    if (pieces.length > 0) {
      insertPieces(this.editor, pieces);
      started.forEach((start) => start());
      this.release();
    }
    if (refusals.length > 0) this.runtime.ui.notice(refusals.join('\n'));
  }

  /** An image of the clipboard's html as a file: read from the store (re-upload, BR-IM-13) or decoded. */
  private async fetchPasted(image: Exclude<PastedImage, { kind: 'foreign' }>, job: Job) {
    const host = this.runtime.host;
    if (image.kind === 'data') {
      const blob = await (await fetch(image.src, { signal: job.abort.signal })).blob();
      return new File([blob], 'image', { type: blob.type });
    }
    if (!host) throw new Error('no host');
    const address = await host.address(image.id, job.abort.signal);
    try {
      const blob = await (await fetch(address, { signal: job.abort.signal })).blob();
      return new File([blob], 'image', { type: blob.type });
    } finally {
      host.release?.(address);
    }
  }

  private clamp(pos: number) {
    return Math.max(0, Math.min(pos, this.editor.state.doc.content.size));
  }

  private placeholder(token: string) {
    return this.editor.schema.nodes.rtImagePlaceholder!.create({ token });
  }

  private upload(token: string, job: Job, produce: () => Promise<File>, alt: string) {
    const host = this.runtime.host;
    if (!host) return;
    const { signal } = job.abort;
    produce()
      .then((file) => host.upload(file, signal))
      .then(
        (stored) => {
          if (signal.aborted) return;
          this.replacePlaceholder(token, stored.id, alt);
          this.runtime.ui.notice(this.runtime.copy.inserted);
        },
        (error: unknown) => {
          if (signal.aborted) return;
          this.removePlaceholder(token);
          this.runtime.ui.notice(
            error instanceof ImageRefusal ? error.message : this.runtime.copy.refusals.uploadFailed,
          );
        },
      )
      .finally(() => {
        this.jobs.delete(token);
        this.release();
      });
  }

  private replacePlaceholder(token: string, id: string, alt: string) {
    const { state, view, schema } = this.editor;
    const pos = findNode(
      state.doc,
      (node) => node.type.name === 'rtImagePlaceholder' && node.attrs.token === token,
    );
    if (pos < 0) return;
    const image = schema.nodes.rtImage!.create({ imageId: id, alt });
    view.dispatch(state.tr.replaceWith(pos, pos + 1, image));
  }

  private removePlaceholder(token: string) {
    const { state, view } = this.editor;
    const pos = findNode(
      state.doc,
      (node) => node.type.name === 'rtImagePlaceholder' && node.attrs.token === token,
    );
    if (pos >= 0) view.dispatch(state.tr.delete(pos, pos + 1));
  }

  /** A placeholder that left the document cancels its upload; one that came back (undo) goes again. */
  private reconcile = () => {
    const present = new Set<string>();
    this.editor.state.doc.descendants((node) => {
      if (node.type.name === 'rtImagePlaceholder') present.add(node.attrs.token as string);
      return true;
    });
    for (const [token, job] of this.jobs) {
      if (!present.has(token)) {
        job.abort.abort();
        this.jobs.delete(token);
      }
    }
    for (const token of present) {
      if (!this.jobs.has(token)) this.removePlaceholder(token);
    }
    this.release();
  };

  // --- the reduction dialog and its queue (US-6) ---

  /** Opens the next waiting offer; the dialogs are one at a time. */
  private next() {
    if (this.state) return;
    const offer = this.queue.shift();
    if (offer) this.emit({ step: 'offer', offer });
  }

  reduce() {
    const state = this.state;
    const host = this.runtime.host;
    if (!state || !host || (state.step !== 'offer' && state.step !== 'failed')) return;
    const { offer } = state;
    const abort = new AbortController();
    this.reduction = abort;
    this.emit({ step: 'reducing', offer });
    host.reduce(offer.file, abort.signal).then(
      (result) => {
        if (abort.signal.aborted) return;
        this.emit({ step: 'result', offer, result });
      },
      (error: unknown) => {
        if (abort.signal.aborted) return;
        if (error instanceof ImageRefusal && error.code === CANNOT_REDUCE_CODE) {
          this.emit({ step: 'cannot', offer });
        } else if (error instanceof ImageRefusal) {
          this.finish(offer);
          this.runtime.ui.notice(error.message);
        } else {
          this.emit({ step: 'failed', offer });
        }
      },
    );
  }

  /** «Вставить»: the confirmed image takes its own place in the chosen order (D-9). */
  insert() {
    const state = this.state;
    if (!state || state.step !== 'result') return;
    const { offer, result } = state;
    const pos = slotPosition(this.editor, offer.slot);
    const image = this.editor.schema.nodes.rtImage!.create({
      imageId: result.image.id,
      alt: offer.alt,
    });
    insertPieces(this.editor, [{ node: image }], pos ?? undefined);
    this.finish(offer);
    this.runtime.ui.notice(this.runtime.copy.inserted);
  }

  /** «Отмена», Esc, the scrim and «Закрыть»: the same at every step; nothing is inserted. */
  cancel() {
    const state = this.state;
    if (!state) return;
    this.finish(state.offer);
  }

  private finish(offer: Offer) {
    this.reduction?.abort();
    this.reduction = null;
    dropSlot(this.editor, offer.slot);
    this.emit(null);
    if (this.queue.length > 0) this.next();
    else this.editor.commands.focus();
  }
}
