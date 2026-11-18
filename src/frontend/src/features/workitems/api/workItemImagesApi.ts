// The description's images over HTTP, strictly per
// docs/features/feat-078-description-images/api-contract.md § 1–§ 3: the upload, the reduction
// and the read with the bearer token (ADR-0045: an `img` cannot carry it, so the bytes are
// fetched and shown through an object URL).
import { ApiError, apiFetch, apiFetchBlob } from '../../../shared/api/httpClient';
import {
  ImageRefusal,
  type ImageReductionResult,
  type RichTextImages,
  type StoredImage,
} from '../../../shared/ui/RichTextEditor/images/host';
import { workItemsApiPath } from './workItemsApi';

const segment = encodeURIComponent;

function imagesPath(organizationId: string, projectId: string, identifier: string): string {
  return `${workItemsApiPath(organizationId, projectId)}/${segment(identifier)}/images`;
}

/** § 1: the file's bytes, nothing else. */
export function uploadImage(
  organizationId: string,
  projectId: string,
  identifier: string,
  file: Blob,
  signal?: AbortSignal,
): Promise<StoredImage> {
  return apiFetch(imagesPath(organizationId, projectId, identifier), {
    method: 'POST',
    body: file,
    signal,
  });
}

/** § 2: the same request shape, the answer holds the stored result and the original's figures. */
export function reduceImage(
  organizationId: string,
  projectId: string,
  identifier: string,
  file: Blob,
  signal?: AbortSignal,
): Promise<ImageReductionResult> {
  return apiFetch(`${imagesPath(organizationId, projectId, identifier)}/reductions`, {
    method: 'POST',
    body: file,
    signal,
  });
}

/** § 3: the stored bytes; one 404 answers every refusal. */
export function readImage(reference: string, signal?: AbortSignal): Promise<Blob> {
  return apiFetchBlob(`/api/images/${segment(reference)}`, { signal }).then((file) => file.blob);
}

/** A 400 of the images routes (and the busy 503) carries the catalog text: the member is told it. */
function refusalOf(error: unknown): unknown {
  if (
    error instanceof ApiError &&
    (error.status === 400 || error.status === 503) &&
    error.errorCode?.startsWith('images.') &&
    error.body?.detail
  ) {
    return new ImageRefusal(error.body.detail, error.errorCode);
  }
  return error;
}

/** The host group of one item's «Описание» (design § Host seam). */
export function itemImagesHost(
  organizationId: string,
  projectId: string,
  identifier: string,
): RichTextImages {
  return {
    upload: (file, signal) =>
      uploadImage(organizationId, projectId, identifier, file, signal).catch((error: unknown) => {
        throw refusalOf(error);
      }),
    reduce: (file, signal) =>
      reduceImage(organizationId, projectId, identifier, file, signal).catch((error: unknown) => {
        throw refusalOf(error);
      }),
    address: async (reference, signal) => URL.createObjectURL(await readImage(reference, signal)),
    release: (address) => URL.revokeObjectURL(address),
  };
}
