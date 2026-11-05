// The options of the «Исполнитель» filter as the picker draws them (feat-003 design § 2.3, BR-WS-6,
// BR-WS-7, D-2). The server's order is kept: the panel never sorts.
import { NO_ASSIGNEE_FILTER_VALUE, type AssigneeFilterOption } from '../api/workItemsApi';
import { UNASSIGNED_LABEL, UNKNOWN_USER_LABEL } from './assignee';
import { userNameWithMarks } from './deletion';

/** One choice of the picker: a person by the user id, or «Не назначен» by `none`. */
export interface AssigneeChoice {
  /** The value the address and the read carry. */
  value: string;
  /** The text of the list and of the tag. */
  label: string;
}

/** «Не назначен», always the last choice (BR-WS-6). */
export const NOBODY_CHOICE: AssigneeChoice = {
  value: NO_ASSIGNEE_FILTER_VALUE,
  label: UNASSIGNED_LABEL,
};

/** The text that tells a person apart from an alike one by e-mail (D-2): « — <e-mail>». */
function withEmail(text: string, email: string): string {
  return `${text} — ${email}`;
}

/**
 * The same text with « № <n>» after the name and before the marks (D-2): «Неизвестный
 * пользователь № 2 (не в организации)». `name` is the part `userNameWithMarks` starts with.
 */
function withNumber(text: string, name: string, place: number): string {
  return `${name} № ${place}${text.slice(name.length)}`;
}

/**
 * The texts of the options, alike ones told apart (D-2): two options whose full text is equal get
 * a difference in the text itself — and only they. A known person whose e-mail differs from every
 * other alike option's is told by it (« — <e-mail>»); the rest — unknown users, no e-mail, e-mails
 * equal ignoring case — by « № <n>», n their place among the alike in the list's order, 1-based. A
 * pair that already reads differently (with and without «(неактивен)») gets nothing.
 */
export function tellApart(options: readonly AssigneeFilterOption[]): string[] {
  const texts = options.map((option) => userNameWithMarks(option));
  const alike = new Map<string, number[]>();
  texts.forEach((text, index) => {
    alike.set(text, [...(alike.get(text) ?? []), index]);
  });
  const told = [...texts];
  for (const group of alike.values()) {
    if (group.length < 2) {
      continue;
    }
    const emailOf = (index: number): string => (options[index]?.email ?? '').trim().toLowerCase();
    group.forEach((index, place) => {
      const option = options[index];
      const text = texts[index];
      if (option === undefined || text === undefined) {
        return;
      }
      const email = emailOf(index);
      const unique =
        option.displayName !== null &&
        email !== '' &&
        group.every((other) => other === index || emailOf(other) !== email);
      told[index] = unique
        ? withEmail(text, (option.email ?? '').trim())
        : withNumber(text, option.displayName ?? UNKNOWN_USER_LABEL, place + 1);
    });
  }
  return told;
}

/**
 * The picker's choices for the read's options: people in the read's order with `userNameWithMarks`
 * as their text (alike ones told apart), then «Не назначен».
 */
export function assigneeChoices(options: readonly AssigneeFilterOption[]): AssigneeChoice[] {
  const texts = tellApart(options);
  return [
    ...options.map((option, index) => ({ value: option.id, label: texts[index] ?? option.id })),
    NOBODY_CHOICE,
  ];
}
