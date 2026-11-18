const MAX_ALT_CODE_POINTS = 255;

/** At most 255 code points of a text alternative; longer is cut (api-contract § 4.1). */
export function cutAlt(alt: string): string {
  let count = 0;
  for (let index = 0; index < alt.length;) {
    const unit = alt.charCodeAt(index);
    const next = alt.charCodeAt(index + 1);
    const pair = unit >= 0xd800 && unit <= 0xdbff && next >= 0xdc00 && next <= 0xdfff;
    if (++count > MAX_ALT_CODE_POINTS) return alt.slice(0, index);
    index += pair ? 2 : 1;
  }
  return alt;
}

/**
 * The stored form of a text alternative, as the server's writer keeps it: cut to 255 code points,
 * `<` and `>` not kept (an attribute that never holds markup is a fixed point under any serialiser),
 * a CR written as LF, NUL dropped. The fixture case `image-alt-angles` pins it on both sides.
 */
export function storedAlt(raw: string): string {
  return cutAlt(raw)
    .replace(/[<>\0]/g, '')
    .replace(/\r/g, '\n');
}
