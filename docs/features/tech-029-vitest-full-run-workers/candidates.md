# tech-029 — heavy page tests: candidates to fold or move down

Generated 2026-10-13 from a shuffled full run (`node scripts/heavy-candidates.mjs <vitest-json-report>`, the thin-and-heavy
filter in the session). **A list of candidates, not a change**: none of these tests was
edited or removed. Each one is a scenario of a spec (`US-… Scenario …`); folding or moving
it must keep every assertion and its place in the traceability of the spec.

Why these are expensive: every test mounts the whole shell (`AppLayout`, navigation, work
context) through `renderShell` / `renderWorkItems`, and a single mount costs ~5 s in jsdom.
A test with one or two `expect`s pays the same mount as one with ten. So the lever is the
number of mounts, not the number of assertions.

How to read the list: **≥ 5 s and ≤ 3 `expect`s**, per file. Two ways to cut a mount:

1. *Fold*: two thin tests that start from the same `openForm()` and differ only in the
   step after it become one test with two `describe`-free sections (assert, act, assert).
2. *Move down*: an assertion about one field (an option list, a refusal under a field, a
   disabled control) belongs in the field's own component test, which mounts the field
   alone (~0.3 s), not the shell.

The fast tier (`npm run test:fast`) already skips these files in the development loop;
this is about their cost in the full run.

### src/features/workitems/pages/WorkItemFormPage.related.test.tsx — 199 s, 32 tests
Heavy and thin (≥ 5 s, ≤ 3 `expect`s) — candidates to fold into a sibling test or move down to a component test:
- 9.3 s, 1 expect, 1 user steps — a save of the parent alone keeps «Статус», «Исполнитель» and «Область»
- 8.2 s, 3 expect, 1 user steps — is enabled after MSK-FEAT-6 is picked and disabled once «Нет родителя» is picked again
- 5.6 s, 1 expect, 0 user steps — the picker on MSK-FEAT-6’s form never offers MSK-FEAT-6
- 5.5 s, 2 expect, 0 user steps — «feat-8» offers «MSK-FEAT-8 Вход через SSO» with its state «Новая» as secondary text

### src/features/workitems/pages/WorkItemFormPage.sections.test.tsx — 187 s, 39 tests
Heavy and thin (≥ 5 s, ≤ 3 `expect`s) — candidates to fold into a sibling test or move down to a component test:
- 9.1 s, 2 expect, 1 user steps — choosing «1» and saving shows «1» on reopening
- 7.1 s, 2 expect, 2 user steps — the server’s `text_too_long` lands under «Шаги воспроизведения»
- 6.3 s, 3 expect, 1 user steps — keeps «<b>важно</b>» literally, with «шаг 2» on the next line
- 5.7 s, 3 expect, 1 user steps — `field_not_on_type` is the error Alert above the band, no field marked

### src/features/workitems/pages/WorkItemFormPage.planning.test.tsx — 172 s, 22 tests
Heavy and thin (≥ 5 s, ≤ 3 `expect`s) — candidates to fold into a sibling test or move down to a component test:
- 6.5 s, 3 expect, 1 user steps — a state change sends the state alone; «Оставшаяся работа» still reads 3
- 5.8 s, 2 expect, 1 user steps — the server’s reason with the same text lands under the field
- 5.6 s, 2 expect, 0 user steps — «Серьёзность» has no empty choice; «Вид работ» starts with «Не указан»
- 5.2 s, 3 expect, 1 user steps — either date alone may be cleared

### src/features/boards/pages/BacklogPage.add.test.tsx — 103 s, 33 tests
Heavy and thin (≥ 5 s, ≤ 3 `expect`s) — candidates to fold into a sibling test or move down to a component test:
- 7.9 s, 3 expect, 4 user steps — the same two header buttons: «Свернуть на один уровень» twice folds it away, «Развернуть на один уровень» brin

### src/features/workitems/pages/WorkItemFormPage.test.tsx — 96 s, 25 tests
Heavy and thin (≥ 5 s, ≤ 3 `expect`s) — candidates to fold into a sibling test or move down to a component test:
- 14.4 s, 3 expect, 2 user steps — reopening the form shows the stored values
- 8.6 s, 3 expect, 2 user steps — the server’s refusal lands under the field with the server’s text, the input kept
- 7.6 s, 3 expect, 1 user steps — keeps «<b>важно</b>» literally, with «шаг 2» on the next line
- 7.4 s, 3 expect, 2 user steps — any other refusal is an error Alert above the band, the input kept

### src/features/workitems/pages/WorkItemFormPage.assignee.test.tsx — 92 s, 11 tests
Heavy and thin (≥ 5 s, ≤ 3 `expect`s) — candidates to fold into a sibling test or move down to a component test:
- 11.0 s, 3 expect, 2 user steps — a refused save puts «Этого пользователя нельзя назначить исполнителем.» under «Исполнитель»
- 9.3 s, 3 expect, 3 user steps — shows «Поиск…» while the search runs, and a failed search says so
- 6.5 s, 2 expect, 0 user steps — offers «Анна Смирнова» for «smirnova@»
- 5.6 s, 2 expect, 0 user steps — does not offer «Пётр Иванов»
- 5.4 s, 2 expect, 0 user steps — «Ёжиков» (nobody matches): «Не назначен» and «Никого не найдено»

### src/features/workitems/components/card/WorkItemCard.edit.test.tsx — 80 s, 17 tests
No thin-and-heavy tests found.

### src/features/workitems/pages/WorkItemFormPage.areaState.test.tsx — 74 s, 15 tests
Heavy and thin (≥ 5 s, ≤ 3 `expect`s) — candidates to fold into a sibling test or move down to a component test:
- 7.7 s, 3 expect, 1 user steps — a «Пользовательская история» in «Закрыта» goes back to «Новая» with no question asked
- 6.8 s, 3 expect, 1 user steps — puts «Этот статус недоступен для рабочего элемента этого типа.» under «Статус»
- 6.6 s, 3 expect, 1 user steps — a «Задача» in «Новая» goes straight to «Закрыта»
- 6.0 s, 1 expect, 3 user steps — a text matching no area: «Ничего не найдено»

### src/features/boards/pages/BacklogPage.mapping.test.tsx — 66 s, 22 tests
No thin-and-heavy tests found.

### src/features/boards/pages/BacklogPage.reparent.test.tsx — 64 s, 15 tests
Heavy and thin (≥ 5 s, ≤ 3 `expect`s) — candidates to fold into a sibling test or move down to a component test:
- 5.4 s, 3 expect, 0 user steps — every menu holds «Изменить родителя…» and «Убрать родителя»; the second is disabled on MSK-EPIC-1 and MSK-EPIC

### src/features/workitems/components/card/WorkItemCard.actions.test.tsx — 47 s, 15 tests
No thin-and-heavy tests found.

### src/features/boards/pages/BacklogPage.drop.test.tsx — 44 s, 29 tests
No thin-and-heavy tests found.

### src/features/boards/pages/BacklogPage.tree.test.tsx — 42 s, 17 tests
Heavy and thin (≥ 5 s, ≤ 3 `expect`s) — candidates to fold into a sibling test or move down to a component test:
- 5.3 s, 3 expect, 0 user steps — another level and back starts at the first state

### src/features/boards/pages/BacklogPage.reorder.test.tsx — 40 s, 19 tests
No thin-and-heavy tests found.

### src/features/workitems/components/WorkItemCardDialog.test.tsx — 39 s, 13 tests
No thin-and-heavy tests found.

### src/features/boards/pages/BacklogPage.expand.test.tsx — 38 s, 12 tests
Heavy and thin (≥ 5 s, ≤ 3 `expect`s) — candidates to fold into a sibling test or move down to a component test:
- 6.4 s, 3 expect, 0 user steps — another level and back start at the first state

### src/features/workitems/components/card/WorkItemCard.test.tsx — 35 s, 21 tests
No thin-and-heavy tests found.

### src/features/workitems/components/CreateWorkItemDialog.test.tsx — 27 s, 16 tests
No thin-and-heavy tests found.

### src/app/routes.boards.test.tsx — 27 s, 16 tests
No thin-and-heavy tests found.

### src/features/organizations/components/AreaDialogs.test.tsx — 24 s, 14 tests
No thin-and-heavy tests found.

### src/features/workitems/pages/WorkItemFormPage.host.test.tsx — 20 s, 9 tests
No thin-and-heavy tests found.

### src/features/boards/pages/BacklogPage.layout.test.tsx — 20 s, 28 tests
No thin-and-heavy tests found.

### src/app/AppLayout.organizations.test.tsx — 19 s, 13 tests
No thin-and-heavy tests found.

### src/features/boards/pages/BacklogPage.look.test.tsx — 19 s, 17 tests
No thin-and-heavy tests found.

### src/features/boards/pages/BacklogPage.create.test.tsx — 17 s, 10 tests
No thin-and-heavy tests found.

### src/shared/ui/fields/FieldFrame.shared.test.tsx — 17 s, 342 tests
No thin-and-heavy tests found.

### src/features/boards/pages/BacklogPage.card.test.tsx — 16 s, 4 tests
Heavy and thin (≥ 5 s, ≤ 3 `expect`s) — candidates to fold into a sibling test or move down to a component test:
- 10.9 s, 3 expect, 2 user steps — shows the row’s new title once the card has saved it, the dialog staying open

### src/features/uikit/pages/UiKitShowcasePage.test.tsx — 16 s, 7 tests
No thin-and-heavy tests found.

### src/features/boards/pages/BacklogPage.head.test.tsx — 16 s, 13 tests
No thin-and-heavy tests found.

### src/features/boards/pages/BacklogPage.test.tsx — 15 s, 17 tests
No thin-and-heavy tests found.

