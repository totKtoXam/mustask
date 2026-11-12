/**
 * jsdom lacks the layout and selection geometry ProseMirror reads while it
 * scrolls the caret into view and places widgets (plan-frontend § 12.2,
 * spike S2). The stubs are additive and answer "nothing laid out"; no other
 * test reads these members.
 */

function emptyRects(): DOMRectList {
  const list = {
    length: 0,
    item: () => null,
    [Symbol.iterator]: function* () {},
  };
  return list as unknown as DOMRectList;
}

function zeroRect(): DOMRect {
  return {
    x: 0,
    y: 0,
    top: 0,
    left: 0,
    right: 0,
    bottom: 0,
    width: 0,
    height: 0,
    toJSON: () => ({}),
  } as DOMRect;
}

export function installRichTextDom(): void {
  const rangeProto = Range.prototype as Partial<Range>;
  if (!rangeProto.getClientRects) {
    rangeProto.getClientRects = emptyRects;
  }
  if (!rangeProto.getBoundingClientRect) {
    rangeProto.getBoundingClientRect = zeroRect;
  }

  const documentProto = document as unknown as { elementFromPoint?: unknown };
  if (!documentProto.elementFromPoint) {
    documentProto.elementFromPoint = () => null;
  }

  if (!HTMLElement.prototype.scrollIntoView) {
    HTMLElement.prototype.scrollIntoView = () => {};
  }
}

installRichTextDom();
