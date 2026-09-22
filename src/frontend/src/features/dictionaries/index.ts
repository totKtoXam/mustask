// Slice barrel: re-exports only the route-level page components.
// `app/routes.tsx` imports this slice through this file (frontend.md:
// "Slices do not import each other's internals — only features/x").
// `DefaultLanguageDialog` is deliberately not re-exported: it is internal to
// the languages registry, opened only from `LanguagesRegistryPage`'s own row
// action, never routed to directly.
export { LanguagesRegistryPage } from './pages/LanguagesRegistryPage';
export { LanguageFormPage } from './pages/LanguageFormPage';
