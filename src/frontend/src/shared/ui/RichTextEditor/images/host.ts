/**
 * The host seam of the editor's images (feat-078 design § Host seam, BR-IM-19). The editor names no
 * route: a host form passes an optional `images` group and the editor shows the «Изображение»
 * control, accepts a paste or a drop of an image file and draws stored images. Without the group it
 * does none of these.
 */

/** What the upload and the reduction answer for the stored picture (api-contract § 1). */
export interface StoredImage {
  /** The opaque reference the description stores. */
  id: string;
  format: 'png' | 'jpeg' | 'gif' | 'webp';
  /** The stored size in bytes. */
  byteLength: number;
  width: number;
  height: number;
}

/** api-contract § 2: the stored result and the file as it was sent. */
export interface ImageReductionResult {
  image: StoredImage;
  original: { format: StoredImage['format']; byteLength: number; width: number; height: number };
}

/**
 * A refusal the member is told in words: the text is the server's reason (the catalog text of its
 * error code) and is announced as it is. Any other error a host operation throws is the
 * generic «Не удалось загрузить изображение…» / «Не удалось уменьшить изображение…».
 */
export class ImageRefusal extends Error {
  readonly code: string | undefined;

  constructor(text: string, code?: string) {
    super(text);
    this.name = 'ImageRefusal';
    this.code = code;
  }
}

/** The error code of the reduction that opens the dialog's «Cannot reduce» body. */
export const CANNOT_REDUCE_CODE = 'images.reduction.cannot_reduce';

export interface ImageLimits {
  /** The stored cap, 5 MB: a still image above it is offered a reduction. */
  maxBytes: number;
  /** The reduction's intake cap, 20 MB. */
  maxIntakeBytes: number;
  /** Images, placeholders and queued offers in one description. */
  maxCount: number;
}

export const DEFAULT_IMAGE_LIMITS: ImageLimits = {
  maxBytes: 5_242_880,
  maxIntakeBytes: 20_971_520,
  maxCount: 50,
};

export interface RichTextImages {
  /** Stores a file; the answer's `id` is what the description keeps. Abortable. */
  upload: (file: File, signal: AbortSignal) => Promise<StoredImage>;
  /** The US-6 request: stores the first result within the cap and answers before and after. */
  reduce: (file: File, signal: AbortSignal) => Promise<ImageReductionResult>;
  /**
   * The address rule: a stored reference in, the string an `img` loads from out. The host reads the
   * bytes with its token and answers an object URL (ADR-0045); the editor releases it.
   */
  address: (reference: string, signal: AbortSignal) => Promise<string>;
  /** Called once per address the editor is done with (an object URL is revoked here). */
  release?: (address: string) => void;
  limits?: Partial<ImageLimits>;
}

/** The ids, in the textual form the stored element uses (lower case, hyphenated). */
export const IMAGE_ID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;

/** The formats the chooser and the client check accept; the server decides by content. */
export const IMAGE_TYPES = ['image/png', 'image/jpeg', 'image/gif', 'image/webp'] as const;
