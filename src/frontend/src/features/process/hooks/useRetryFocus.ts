import { useCallback, useEffect, useRef } from 'react';

/** What the hook reads of the query: whether a fetch runs and how the last one ended. */
export interface RetryFocusQuery {
  isFetching: boolean;
  isSuccess: boolean;
  isError: boolean;
  /** The last failure is «Организация не найдена»: the not-found frame replaced the page. */
  isNotFound: boolean;
}

/**
 * Focus after «Повторить» (design § Focus and announcements). The button
 * unmounts under the skeleton while the retry runs, which would drop focus
 * to the document. Once the retry settles, focus moves to the page title on
 * success, to the not-found frame's heading when the retry finds no
 * organization (review round 1 F4: the frame is then the whole page and its
 * heading the page's `h1`), or to the new alert's «Повторить» on another
 * failure. Nothing happens on a first load or on any fetch that was not a
 * retry.
 */
export function useRetryFocus({ isFetching, isSuccess, isError, isNotFound }: RetryFocusQuery) {
  const titleRef = useRef<HTMLHeadingElement>(null);
  const notFoundHeadingRef = useRef<HTMLHeadingElement>(null);
  const retryRef = useRef<HTMLButtonElement>(null);
  const retryAsked = useRef(false);

  const requestRetryFocus = useCallback(() => {
    retryAsked.current = true;
  }, []);

  useEffect(() => {
    if (!retryAsked.current || isFetching) {
      return;
    }
    retryAsked.current = false;
    if (isSuccess) {
      titleRef.current?.focus();
    } else if (isNotFound) {
      notFoundHeadingRef.current?.focus();
    } else if (isError) {
      retryRef.current?.focus();
    }
  }, [isFetching, isSuccess, isError, isNotFound]);

  return { titleRef, notFoundHeadingRef, retryRef, requestRetryFocus };
}
