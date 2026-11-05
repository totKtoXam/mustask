/** A callback ref the card hands to a control: it remembers the control's element. */
export type ElementRef = (element: HTMLElement | null) => void;

export interface FieldRegistry<Field extends string> {
  /** The ref for `field`'s control; the same function every time, so React does not re-attach it. */
  ref: (field: Field) => ElementRef;
  /** Moves focus to `field`'s control and brings it into view; nothing if it is not mounted. */
  focus: (field: Field) => void;
}

/**
 * Where the card's controls are, by the field they edit — so that a refused
 * save can take the user to the first field with an error (US-4). Plain
 * functions over a map: no state, nothing to render.
 */
export function createFieldRegistry<Field extends string>(): FieldRegistry<Field> {
  const elements = new Map<Field, HTMLElement>();
  const refs = new Map<Field, ElementRef>();
  return {
    ref(field) {
      let ref = refs.get(field);
      if (ref === undefined) {
        ref = (element) => {
          if (element) {
            elements.set(field, element);
          } else {
            elements.delete(field);
          }
        };
        refs.set(field, ref);
      }
      return ref;
    },
    focus(field) {
      const element = elements.get(field);
      if (element) {
        element.focus();
        // jsdom has no layout, so no scrolling either.
        element.scrollIntoView?.({ block: 'nearest' });
      }
    },
  };
}
