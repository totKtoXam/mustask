import { notFoundLevel } from '../../organizations';

/**
 * The frame an aggregate read answers with when its header selection names an object the caller
 * cannot see (feat-061 US-6, closing feat-041 R-4): the server answers a hidden department, team or
 * project with the object's not-found code, exactly as for a missing one, and a retry can never
 * succeed. `null` for any other failure, which keeps the generic error with «Повторить».
 */
export function goneSelectionLevel(error: unknown): ReturnType<typeof notFoundLevel> {
  return notFoundLevel(error);
}
