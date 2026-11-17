import { ApiError } from '../../../shared/api/httpClient';
import { importErrorCodes } from '../../../shared/api/errorCodes';
import { apiErrorText } from '../../../shared/lib/apiErrorText';

/**
 * The reason of a refused file (api-contract § 3.1): the server answers `imports.file.refused` with the
 * reason's own code and text under `errors.file`; other answers (`header_empty`, an expired file) carry
 * their text in `detail`. The text is always the server's, rendered from the catalogue.
 */
export function refusalText(error: unknown): string {
  if (error instanceof ApiError) {
    const reasons = error.body?.errors?.file ?? [];
    const first = reasons[0];
    if (first?.detail) return first.detail;
    const header = Object.values(error.body?.errors ?? {})[0]?.[0];
    if (error.errorCode === importErrorCodes.validationFailed && header?.detail)
      return header.detail;
  }
  return apiErrorText(error);
}

/** Whether the answer is the refusal of a whole file (a notice «Файл не принят», not a banner). */
export function isFileRefusal(error: unknown): boolean {
  return (
    error instanceof ApiError &&
    (error.errorCode === importErrorCodes.fileRefused ||
      error.errorCode === importErrorCodes.fileNotFound)
  );
}
