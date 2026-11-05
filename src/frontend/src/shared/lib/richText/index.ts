/**
 * The pure rich-text library (feat-018, ADR-0037): no React, no TipTap, only
 * the DOM's `DOMParser`. Everything here runs on the form route without the
 * editor, so it stays small; the editor itself is a lazy chunk in
 * `shared/ui/RichTextEditor`.
 */
export {
  CODE_LANGUAGES,
  CODE_LANGUAGE_PLAIN,
  FONT_SIZES,
  HIGHLIGHTS,
  MARK_ORDER,
  TEXT_COLOURS,
  firstToken,
  tokenByName,
  type MarkKind,
  type VocabularyToken,
} from './vocabulary';
export { normaliseBody, normaliseRichText, type NormalisedRichText } from './normalise';
export { reducePastedHtml, type ReducedPaste } from './pasteReduce';
export { tabularRows } from './spreadsheetPaste';
export { legacyToHtml } from './legacy';
export { promptAddress, readLinkAddress } from './linkAddress';
export { findAutoLinks, type AutoLink } from './autolink';
export {
  DESCRIPTION_COUNTER_FROM,
  DESCRIPTION_MAX_MARKUP,
  DESCRIPTION_MAX_VISIBLE,
  DESCRIPTION_RAW_CEILING,
  checkDescription,
  type DescriptionVerdict,
} from './limits';
export { cleanText, codePointLength, isUnicodeWhiteSpace } from './whitespace';
