// The tab's read slots (feat-048 BR-NV-20, Q-11): at most 4 group and cell reads in flight, the
// rest wait in order. A module-level queue, because the cap is per browser tab, whatever number of
// groups or lanes ask. A read that is waiting to retry after a 429 holds no slot (OQ-1).
export const MAX_IN_FLIGHT = 4;

let inFlight = 0;
const waiting: Array<() => void> = [];

function grant(): () => void {
  inFlight += 1;
  let released = false;
  return () => {
    if (released) return;
    released = true;
    inFlight -= 1;
    const next = waiting.shift();
    if (next) next();
  };
}

/** Resolves with the slot's release when one of the four is free; release is idempotent. */
export function acquireSlot(): Promise<() => void> {
  if (inFlight < MAX_IN_FLIGHT) return Promise.resolve(grant());
  return new Promise((resolve) => {
    waiting.push(() => resolve(grant()));
  });
}

/** For tests and diagnostics: reads now in flight, and reads waiting for a slot. */
export function readQueueState(): { inFlight: number; waiting: number } {
  return { inFlight, waiting: waiting.length };
}
