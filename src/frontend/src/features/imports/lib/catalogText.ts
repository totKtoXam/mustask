// The text of an error-catalogue code, for a code that comes in a body and not as an HTTP answer
// (a dry run's `notes`, a field reason of a refusal). The catalogue is the repository's single copy:
// `contracts/errors/imports.json`, imported at build time as `roleBundles` imports its contract.
import catalogue from '../../../../../../contracts/errors/imports.json';

interface CatalogueEntry {
  ru: string;
}

const entries = (catalogue as { codes: Record<string, CatalogueEntry> }).codes;

/** The Russian text of `code` with its `{param}`s filled; the code itself when it is unknown. */
export function catalogText(code: string, params: Record<string, unknown> = {}): string {
  const entry = entries[code];
  if (!entry) return code;
  return entry.ru.replace(/\{(\w+)\}/g, (match, name: string) => {
    const value = params[name];
    return value === undefined || value === null ? match : String(value);
  });
}
