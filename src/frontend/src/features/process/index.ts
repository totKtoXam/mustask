// Slice barrel: the page for the router, plus what the shell's navigation
// model needs to register «Процесс» — its permission code and its address
// (as the dictionaries barrel does, mod-015 plan D-20). `app/` imports this
// slice through this file only (frontend.md "Slice rules").
export { ProcessDefinitionPage } from './pages/ProcessDefinitionPage';
export { processPermissions } from './permissions';
export { PROCESS_DEFINITION_ROUTE, processDefinitionPath } from './paths';
