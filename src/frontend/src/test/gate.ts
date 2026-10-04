/**
 * A promise the test resolves to let a held answer go.
 *
 * A handler that awaits `opened` before it answers keeps a read pending for as
 * long as the test wants. That is how a test sees the loading state of a
 * retry: in jsdom a read that answers at once settles its fetching state and
 * its result in one render, so the skeleton never shows and the button the
 * test clicked stays mounted. Call `open()` to release the answer — every
 * test that holds one must, or the request outlives it.
 */
export interface Gate {
  opened: Promise<void>;
  open: () => void;
}

export function gate(): Gate {
  let open: () => void = () => {};
  const opened = new Promise<void>((resolve) => {
    open = resolve;
  });
  return { opened, open };
}
