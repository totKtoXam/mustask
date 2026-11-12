/**
 * The one place the editor's chunk is imported from (ADR-0039 decision 10):
 * ProseMirror, TipTap, the extensions and the toolbar live behind this dynamic
 * import, so nothing of them is in the entry chunk or the shared vendor chunks.
 * It is also the mock point of the wrapper's tests (loading and failure).
 */
export function loadEditorImpl() {
  return import('./EditorImpl');
}
