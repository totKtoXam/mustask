// Slice barrel: the route-level page components, plus what the shell's
// navigation model needs to register «Языки» — its permission code and its
// address (mod-015 plan § 2.4, D-20). `app/` imports this slice through this
// file only (frontend.md: "Slices do not import each other's internals —
// only features/x").
// `DefaultLanguageDialog` is deliberately not re-exported: it is internal to
// the languages registry, opened only from `LanguagesRegistryPage`'s own row
// action, never routed to directly.
export { LanguagesRegistryPage } from './pages/LanguagesRegistryPage';
export { LanguageFormPage } from './pages/LanguageFormPage';
export { dictionariesPermissions } from './permissions';
export { LANGUAGES_PATH } from './paths';
