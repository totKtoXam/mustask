// How a row's planning values read in the backlog table (feat-027 BR-T-3;
// design § 4.3). The labels are feat-022's, as its form shows them; the table
// keeps its own copy because a slice does not reach into another's internals.

/** The list's labels by key (feat-022 «Вид ценности»). */
const VALUE_AREA_LABELS: Readonly<Record<string, string>> = {
  Business: 'Бизнес',
  Architectural: 'Архитектура',
};

/**
 * «Трудоёмкость» or «Бизнес-ценность»: the stored number in its shortest form
 * with «,» as the decimal mark (`2.5` → «2,5», `20` → «20»); blank for `null` —
 * an empty value, or a type without the field.
 */
export function planningNumber(value: number | null): string {
  return value === null ? '' : String(value).replace('.', ',');
}

/** «Вид ценности»: the list's label, blank for `null`; a key the panel does not know shows as sent. */
export function valueAreaLabel(key: string | null): string {
  return key === null ? '' : (VALUE_AREA_LABELS[key] ?? key);
}
