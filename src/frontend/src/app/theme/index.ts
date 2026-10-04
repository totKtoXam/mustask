// The theme barrel: `app/App.tsx` provides `theme`; the token tuples are
// exported for the tests. Slices never import this folder — they read the
// theme through `useTheme()` (design § 2.3).
export { theme } from './theme';
export {
  APPEARANCE_TOKENS,
  CATEGORY_CODES,
  type AppearanceToken,
  type CategoryCode,
} from './tokens';
export type { TypeAppearance, WorkTrackingLookups } from './workTracking';
