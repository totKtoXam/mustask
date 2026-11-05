/**
 * Records every moment a button named `label` is put into the document, from
 * now until `stop()` — the unit-test twin of the E2E probe of spec 29.7
 * ("any frame that carries an administrator's control on a reader's page").
 *
 * Looking at the document after the fact misses a control that is drawn for one
 * commit and gone in the next; a `MutationObserver` keeps the record of every
 * node added, whatever happens to it after. Start it before the render under
 * test, so the first commit is in the record.
 */
export interface Appearances {
  /** How many times such a button was added to the document so far. */
  count(): number;
  /** Stops watching. */
  stop(): void;
}

export function recordAppearances(label: string): Appearances {
  let appeared = 0;
  const countIn = (records: readonly MutationRecord[]) => {
    for (const record of records) {
      for (const node of record.addedNodes) {
        if (!(node instanceof Element)) {
          continue;
        }
        const buttons = node.matches('button') ? [node] : [];
        buttons.push(...node.querySelectorAll('button'));
        appeared += buttons.filter((button) => button.textContent?.trim() === label).length;
      }
    }
  };
  const observer = new MutationObserver(countIn);
  observer.observe(document.body, { childList: true, subtree: true });
  return {
    count() {
      countIn(observer.takeRecords());
      return appeared;
    },
    stop() {
      countIn(observer.takeRecords());
      observer.disconnect();
    },
  };
}
