// Slice barrel: the showcase page, plus what the shell's navigation model needs to
// register «Интерфейс» → «Витрина компонентов» — its permission code and its address
// (tech-015 design § 10.1). `app/` imports this slice through this file only.
export { UiKitShowcasePage } from './pages/UiKitShowcasePage';
export { uiKitPermissions } from './permissions';
export { UI_KIT_PATH } from './paths';
// The registry, for the coverage test that tech-016 adds (tech-015 design § 9.6).
export { showcaseRegistry as showcaseSections, type ShowcaseSection } from './showcase/registry';
