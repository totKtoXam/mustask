// The emoji picker's data, built from `emojibase-data` by the Vite plugin in
// `vite.config.ts` (ADR-0037 decision 4, plan-frontend § 10.1). Loaded only
// through a dynamic `import()` so it is a chunk of its own.
declare module 'virtual:rich-text-emoji' {
  /** Russian group names; an emoji's last tuple member indexes this list. */
  export const groups: readonly string[];
  /** `[emoji, Russian label, Russian tags (space separated), group index]`. */
  export const emoji: readonly (readonly [string, string, string, number])[];
}
