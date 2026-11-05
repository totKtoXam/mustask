import type { AriaAttributes, HTMLAttributes } from 'react';

/** Any `data-*` attribute — the E2E and the tests select by them. */
export type DataAttributes = { [name: `data-${string}`]: string | number | boolean | undefined };

/**
 * What a kit part passes through to its root control, besides its own props
 * (tech-015 design § 4.2, rule 4): every `aria-*` and `data-*` attribute, and
 * the few DOM props a host needs to drive the control — its `id`, `role`,
 * `tabIndex` and the key, focus and pointer handlers. A kit part takes no
 * `sx`, `className` or `style`: its look is its own.
 */
export type PassThrough<E extends HTMLElement = HTMLElement> = AriaAttributes &
  DataAttributes &
  Pick<
    HTMLAttributes<E>,
    | 'id'
    | 'role'
    | 'tabIndex'
    | 'onKeyDown'
    | 'onKeyUp'
    | 'onFocus'
    | 'onBlur'
    | 'onMouseDown'
    | 'onPointerDown'
  >;
