// What a backlog row can be given as a child (feat-027 BR-T-9; design § 4.5).

/** One child type a row's «+» offers, named by the process's own name for it. */
export interface AddEntry {
  typeId: string;
  /** The process's name of the type: «Фича», «Пользовательская история»… */
  name: string;
  /** The appearance token, which draws the type's icon. */
  appearance: string;
}

/**
 * The appearances a row of an appearance can be given, in the order BR-T-9
 * lists them: `epic` → `feature`; `feature` → `story`, `bug`; `story` →
 * `task`, `bug`; a `bug` and a `task` none (D-6 OQ-6 (b)). Each pair is one
 * BR-H-2 allows. Types are recognised by appearance at both ends, never by
 * name.
 */
const CHILD_APPEARANCES: Readonly<Record<string, readonly string[]>> = {
  epic: ['feature'],
  feature: ['story', 'bug'],
  story: ['task', 'bug'],
};

interface ProcessType {
  id: string;
  name: string;
  appearance: string;
}

/**
 * The entries of a row of `appearance`, resolved against the process's types.
 * A token no type of the process has gives no entry; with no types read yet
 * there is none, as a name is needed.
 */
export function addEntriesOf(appearance: string, types: readonly ProcessType[]): AddEntry[] {
  return (CHILD_APPEARANCES[appearance] ?? []).flatMap((child) => {
    const type = types.find((candidate) => candidate.appearance === child);
    return type ? [{ typeId: type.id, name: type.name, appearance: type.appearance }] : [];
  });
}

/**
 * Whether a card of `appearance` offers a task as a child (feat-032 BR-C-9, by
 * feat-027 BR-T-9): a story. By appearance, never by name.
 */
export function offersTask(appearance: string): boolean {
  return CHILD_APPEARANCES[appearance]?.includes('task') ?? false;
}

/**
 * The accessible name of a row's «+»: one entry names the type (`Добавить:
 * Фича (MSK-EPIC-1)`), several open a menu (`Добавить дочерний элемент
 * (MSK-FEAT-6)`).
 */
export function addControlLabel(entries: readonly AddEntry[], identifier: string): string {
  const [only] = entries;
  return entries.length === 1 && only
    ? `Добавить: ${only.name} (${identifier})`
    : `Добавить дочерний элемент (${identifier})`;
}

/** The label of one entry, on the single control's tooltip and in the menu. */
export function addEntryLabel(entry: AddEntry): string {
  return `Добавить: ${entry.name}`;
}
