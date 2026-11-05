# feat-029-work-item-card-reference — review

## Round 1

Verdict: **changes-requested** — 4 major, 6 minor. Diff `tech/015-ui-kit...8fa4c28`: `src/frontend`
without `shared/ui` (72 files) and this folder; the kit is judged only by how the card uses it.
ESLint on the card's, the hosts' and E2E 23/25/27's files: clean. `tsc -b`: clean (`vite build` not
run: it needs `VITE_API_BASE_URL`). Vitest: `workitems/{components,hooks}`, `lib/edits`, the page's
host test, `BacklogPage.card`, `BoardPage.card`, `kitGuard`, `colourLiterals` — every card file green;
one untouched file timed out in the parallel run (R1-10). Not run: the six other
`WorkItemFormPage.*` files, E2E (no stand).

### Findings

**R1-1 · major · `src/frontend/src/features/workitems/hooks/useWorkItemDialog.ts:97,105-121`**
A dialog the address opened (depth 0) that switches to a related item pushes `cardDepth: 1`;
«Закрыть», Esc or a click outside then go back one entry — to the address-opened entry, which still
holds `?workitem=<first>`. The dialog stays open on the first item and needs a second close
(reproduced in a throwaway hook test: after `switchTo('MSK-FEAT-6')` and `close()` the address is
`?workitem=MSK-US-2`). So US-1 «Esc, «Закрыть» and Back close it» fails for every shared link on
which the user followed «Открыть <ID>». Nothing covers it: `useWorkItemDialog.test.tsx:87-120`,
`WorkItemCardDialog.test.tsx:277-296` and E2E 27.1 (`27-work-item-card.spec.ts:662`) stop at Back.
Fix: carry the base in the state the card pushes (e.g. `cardBase: 'address'`, kept by `open`), and
on close from such an entry go back `depth` entries and then replace the landed address without the
parameter (a pending strip consumed when that location arrives). Add the case to the hook test and 27.1.

**R1-2 · major · `src/frontend/src/test/kitGuard.allow.ts:781-794`**
Two entries still read `until: 'feat-029'`: `Marks.tsx` KIT-11 (the MUI avatar the list and the
boards still draw) and `ParentPicker.tsx` KIT-1, which the Log says "stays for boards (tech-018)".
Stage 07 sets `stage: complete` on the branch before the push; `kitGuard.test.ts:626-627` (through
`isTaskComplete`, :670-677) then fails with "feat-029 is complete — its entries must already be
gone", so `npm test`, `verify.sh` and `finish-task.sh` go red at the very last step.
Fix: point each entry at the task that removes it (tech-018 for `ParentPicker`, the list's or the
boards' migration for `Marks.tsx`) with its reason reworded, or migrate both here.

**R1-3 · major · `src/frontend/src/features/workitems/components/WorkItemCardDialog.tsx:97-157`; `components/card/KitFrames.tsx:47`**
The loading frame is a `DialogShell` that `CardDialog` renders; the loaded card's is another
`DialogShell` inside `WorkItemCard → DialogFrame`. The two stand at different places in the tree, so
on every open the first is unmounted and the second mounted: the dialog fades in, vanishes and fades
in again (the Log's "Open"), MUI's trap hands focus back to the backlog title in between, and a
screen reader meets two dialogs («Рабочий элемент MSK-US-2», then «Пользовательская история
MSK-US-2»). Against the spec's NFR ("frame and skeleton in the same frame as the click; the item
fills in without moving what the skeleton drew") and design § 6 ("keeps its frame in every state");
the same happens from the card to the not-found frame after a save.
Fix: one `DialogShell` element at one place for all four states — e.g. `CardDialog` owns it and the
loaded card hands its `header`, `band`, `alert` and body up (a setter through context, set in a
layout effect), switching `label` to `labelledBy`. A test then holds the dialog element across the read.

**R1-4 · major · `docs/features/feat-029-work-item-card-reference/stories/US-3-three-columns-borderless-fields.md:48-52`; `design.md:391,447`; `spec.md:111`**
US-3, design § 4.6 and § 4.7 and BR-C-15 say an empty optional list is blank at rest («Риск» shows
"nothing under it"). The kit's `InlineSelectField` draws its `emptyLabel` «Не указан» at rest, and
this diff says so itself: E2E 27 asserts «Не указан» (`27-work-item-card.spec.ts:1144-1148`) and
E2E 25's header (`25-work-item-fields.spec.ts:24-30`) notes "US-3 and design § 4.6 say «blank at
rest»: the kit wins". The difference is on no KD list, so BR-C-22 makes the side-by-side call it a
defect, and QA acting on US-3 files one.
Fix: record it as KD-13 in design § 2 (D-4: the kit wins), amend US-3's scenario, § 4.6's row,
§ 4.7's "an empty field blank" and BR-C-15, and add it to the Log's deviations.

**R1-5 · minor · `design.md:84-86,239,244,282,396,710-720`**
The design still has the card as one `<form noValidate>` ("Enter in a single-line field submits",
«Сохранить» `type="submit"`, § 12 "settle before pass A"), a `title` on the area's path and
`TypeLabel` in «Родитель». The code has no form — Enter saves from the title only
(`WorkItemCard.tsx:158-163,312`); Enter in «Оценка в пунктах» does nothing — no title on the area,
and `TypeMark` in the parent. The Log (deviations line) records them, the design does not, and
§ 12:751-754 tells feat-030 to find the section by the design.
Fix: amend § 1, § 4.2, § 4.3, § 4.6 and § 12 to the shipped shape.

**R1-6 · minor · `src/frontend/src/features/workitems/components/card/CardActions.tsx:53-62`**
After a save by the button, the clean main button is natively `disabled` and focus leaves it: on the
page to `body` (the next Tab starts at the top of the document), in the dialog to the frame after up
to 50 ms (MUI's trap), and an Esc in between goes to the page. The busy state is `aria-disabled` so
"a user who pressed Enter on «Сохранить» keeps their place" (design § 4.2) — lost one step later.
Three tests close by «Закрыть» instead of Esc because of it (`27-work-item-card.spec.ts:748-751,1754`,
`BacklogPage.priority.test.tsx:94`), so US-6 «A saved card closes at once» (Esc) is unchecked.
Fix: after an accepted save, when focus was in the Save group, move it to the title (or ask the kit
to keep a clean main button focusable); then press Esc in those three places.

**R1-7 · minor · `src/frontend/src/features/workitems/hooks/useWorkItemEdits.ts:204-206,258-262,278-280`; `card/CardActions.tsx:59-60,75-76`**
While «Обновить» reads, «Сохранить», Enter in the title and «Отменить изменения» stay enabled but
`working` absorbs them with no feedback: an undo confirmed during a slow read closes the question,
keeps every edit and says nothing. The refresh's notice also reads `edits` as they were when the read
began, so text typed during the read gets «Данные обновлены.» without «…остались на месте.».
Fix: make the Save group and the undo `inactive` while `refreshing` (as «Обновить» is while saving),
or let `undo` run during a read (the read already rebases through a functional update); word the
notice from the edits rebased at the read's end.

**R1-8 · minor · `src/frontend/src/features/workitems/components/card/oneColumn.ts:7`**
`'kit-columns'` copies a constant `ColumnLayout.tsx:19` does not export. A kit rename drops the
480 px one-column cap silently: jsdom evaluates no container query, so only E2E 25.1 at 640 px, on a
stand, would notice.
Fix: export the name (or a query helper) from the kit and import it; failing that, a Vitest that
renders `ColumnLayout` and pins its computed `container-name` to the card's constant.

**R1-9 · minor · `src/frontend/src/features/workitems/components/card/WorkItemCard.test.tsx:131-137`**
"shows the time of the latest change, and «—» when it cannot be read" checks only the first half;
nothing gives the item an unreadable `changedAt`.
Fix: add that case («Изменён: —»), or drop the clause from the name.

**R1-10 · minor · the card's Vitest files (`vi.setConfig({ testTimeout: 60_000 | 120_000 })`)**
In one run of the 12 files under `workitems/{components,hooks}` and `lib/edits` (4 workers), the
untouched `CreateWorkItemDialog.test.tsx` «creates an «Эпик» and then a «Баг» in a row» timed out at
20.3 s against the default 20 s; alone it takes 9.9 s. `verify.sh` runs the whole suite that way, so
load alone can turn it red. Measured with a throwaway probe: `getComputedStyle` is 75–80 % of an
interaction (1.5–1.8 s of a 2.1 s select choice; 793 → 838 `<style>` elements): about a third from
the text areas' autosize, two thirds from `getByRole(…, { name })` name computation (`isHidden`);
`pointerEventsCheck: Never` saves 4 % (`WorkItemCard.edit`: 212 → 203 s).
Fix (cheap, test-side): hold elements instead of re-querying by role and name after every step
(`save()`, `undo()`, `title()`), scope the queries left with `within(...)` of the header or the
section; optionally stub `getComputedStyle` for `HTMLTextAreaElement` in `testCard.tsx`. Not worth
it: the pointer-events switch.

### Claims checked (direction 1)

- status Log, pass B: "four states, keyed by identifier" → `WorkItemCardDialog.tsx:59,110-157`. OK.
- "focus back to the opening title" → `WorkItemCardDialog.tsx:44-66,171-183`; tested for Esc and after a switch. OK.
- "plain-click title links on the backlog and board, `href` kept" → `BacklogRow.tsx:231-236`, `BoardColumns.tsx:424-429`. OK.
- "BR-C-21 refresh after a save or a refresh" → `WorkItemCardDialog.tsx:145-146`, `BacklogPage.tsx:72,151`, `BoardPage.tsx:74,140`. OK.
- "27 closes by «Закрыть» after a save (Esc is lost on the disabled button)" → `27-work-item-card.spec.ts:748-751`. True of the test; the behaviour is R1-6.
- "Open: a first open swaps the loading shell for the card's, a second fade" → true; it is R1-3.
- "Slow jsdom tests: `getComputedStyle`" → measured, R1-10. OK.
- Pass A: "17 guard entries gone" → 17 removed in `kitGuard.allow.ts`; two `until: 'feat-029'` left → R1-2.
- Deviations: no form → `WorkItemCard.tsx:158-163,312`; `TypeMark` → `ParentField.tsx:132,205`, `ChildItems.tsx:43`; no area title → `AreaField.tsx:47-77`; "ParentPicker stays for boards" → `ChangeParentDialog.tsx:62`. OK; the design not amended → R1-5; the guard → R1-2.
- `WorkItemCard.tsx:92-93` "typing in one field draws that field, not the card's twenty" → the memoised fields hold; the title (new `inputProps`, :312) and the shells redraw. Overstated, nothing acts on it; not raised.
- `useWorkItemDialog.ts:46-52` close: "back by the entries the card pushed, or a replace" → R1-1.
- `useWorkItemDialog.ts:68-70` "leaving the page's address drops the parameter by itself" → the new address has none (:74-75). OK.
- `useWorkItemEdits.ts:116-118` "one piece of work at a time; a read cannot land over a save" → :204-206, :247-249; tested. OK (the silence is R1-7).
- `useCarriedEdits.ts:9-16` "read once, cleared with a replace" → :20, :23-34; tested. OK.
- `KitFrames.tsx:23-28` "none of them closes it while a save runs" → `busy` :55. OK.
- design § 4.8 slots draw nothing and stand in their rows → `WorkItemCard.slots.test.tsx:48-118`. OK.
- E2E 25:24-30 and 27:1144-1147 "the kit's `InlineSelectField` shows its `emptyLabel`" → true; contradicts US-3, § 4.6, § 4.7, BR-C-15 → R1-4.
- design § 1, § 4.2, § 4.3, § 4.6, § 12 (form, area title, `TypeLabel`) → R1-5.
- spec NFR "frame at once, the item fills in without moving" and design § 6 "keeps its frame" → R1-3.
- `WorkItemCard.test.tsx:131` "«—» when it cannot be read" → R1-9.

### Direction 2

Behaviours the diff changes: (1) a backlog row's and a kanban card's title opens a dialog on a plain
click — grepped "title … opens / link to the item's form / opens its form" in `docs/features/`
feat-027, mod-006, mod-003, feat-022, `docs/conventions`, `src/frontend/AGENTS.md`: mod-006
`spec.md:267` (a link to the item's form — still the `href`), feat-027 US-1:58 ("opens its form" —
the card), mod-003 US-2:32 (the list, unchanged); none stale. (2) the old form's parts removed —
grepped `FieldControl`, `FormSection`, `HeaderChrome`, `AssigneePicker`, `RelatedWorkSection`,
`StatePicker`, `AreaPicker`, `components/Notice` in `docs`, `.claude`, the `AGENTS.md` files: hits
only in logs and reviews of feat-001, feat-022, mod-003 (history) and tech-015 design § 13 (the
rename map); none stale. (3) `?workitem`, `cardDepth`, `WorkItemCardDialog`, `useWorkItemDialog`,
`27-work-item-card`: no hit outside this folder. (4) the guard's list → R1-2. Not swept: the kit's
own prose (tech-015's review).

### Resolution (fix pass after round 1)

Every finding was fixed except the parts declined below. Commits are on `feat/029-work-item-card-reference`.

- **R1-1 — fixed in 7dbcecd.** The card now marks the entries it pushes over a dialog the address opened with `cardBase: 'address'` (kept by `open`). A close from such an entry goes back `cardDepth` entries onto the address-opened entry and replaces it without `?workitem`; the landing reads as closed while the replace is on its way, so the dialog fades out where it stands instead of remounting on the first item. Hook test (six cases, among them "never shows the first item again on the way out"), `WorkItemCardDialog.test.tsx` (a shared link, a switch, Esc, Back leaves the page) and E2E 27.1 (a new step) hold it; design § 4.1 states the rule.
- **R1-2 — fixed in 9afc0d5.** `Marks.tsx` KIT-11 now reads `until: 'tech-016'` (the board tile and the list draw it through `AssigneeName`; the first of the two migrations swaps it for the kit's `UserAvatar`), `ParentPicker.tsx` KIT-1 reads `until: 'tech-018'` (the board's «Изменить родителя» dialog). `'feat-029'` is gone from `UNTIL`, so `stage: complete` cannot trip the guard. Kit ask: `docs/conventions/ui-kit.md:345` still names feat-029 as an `until`.
- **R1-3 — fixed in 80213da.** One `DialogShell` for all four states: `DialogFrame` draws it from the first frame of the click, named by `label` and holding the state's own frame (skeleton, banner, not-found) until the card has published its frame into a small store (`PublishFrame`, `frameStore.ts`), then taking the card's header, band, notice and body and the type line as its name (`labelledBy`). A store, not state, so a card that publishes does not redraw its host. `WorkItemCardDialog.test.tsx` holds the same dialog element across the read and across the not-found frame a save finds; design § 6 says so.
- **R1-4 — fixed in c94eba1.** KD-13 in design § 2 (D-4: the kit wins); US-3's scenario split in two (an empty text, number or date is blank at rest; an empty optional list says «Не указан»), § 4.6's row, § 4.7's "Rest, hover, focus" and BR-C-15 amended.
- **R1-5 — fixed in f9559fe.** Design § 1, § 4.2, § 4.3, § 4.6, § 9 and § 12 describe the card as shipped: no `<form>`, Enter in the title saves by the field's key handler and Enter elsewhere does nothing, «Сохранить» a plain button, no `title` on the area's path, `TypeMark` in «Родитель» and the children, the card's `ParentField` / `ChildItems` (feat-030 finds the section by them).
- **R1-6 — fixed in b35ab0a.** When the Save button held focus at the click, the card takes focus to the title once the save is accepted (`WorkItemCard.saveByButton`). The three tests that closed by «Закрыть» (E2E 27.1 and 27.6, `BacklogPage.priority.test.tsx`) press Esc; a card test, a dialog test (Esc closes a saved dialog at once) and E2E `toBeFocused` hold it. Kit ask, not required: a clean main button that stays focusable would keep the place natively.
- **R1-7 — fixed in bd0f620, with one part declined.** The undo works during a read (the read rebases through a functional update; only a save absorbs it), the notice is worded from the draft as it stands when the read ends (`latestEdits`), and the Save group over unsaved edits is `busy` while a read runs, so a click is visibly waiting. Declined: "inactive" for the Save group — the kit's `SplitButton` has no inert state without its spinner (kit ask: an `inactive` prop like `IconAction`'s); and Enter in the title during a read stays absorbed (queueing a save behind the read is more than this finding asks; the busy button says why nothing happens). Four tests in `WorkItemCard.actions.test.tsx`.
- **R1-8 — fixed in 1a4eac3, by the fallback the review names.** The kit does not export the container's name and the kit's fix pass runs in parallel, so `oneColumn.test.tsx` renders `ColumnLayout` and pins its declared `container-name` and the cap's `@container` rule to the card's constant. Kit ask: export the name (or a query helper) from `ColumnLayout`, then delete the copy and the test.
- **R1-9 — fixed in e0930bb.** Split in two: the latest change is shown, and an unreadable `changedAt` reads «Изменён: —».
- **R1-10 — fixed in 89aa0c3, in part.** The card's tests hold their elements (`held()` in `testCard.tsx`) instead of re-querying by role and name after every step: `title()`, `save()`, `undo()`, `description()` in the card, dialog and page host tests. Measured: one `getByRole` for a button costs 50 ms in the card's page, the held element 0.003 ms. A latent race this exposed in "asks first, keeps the edits…" (a role query while the question's modal still hides the page) is fixed there. Declined: the `getComputedStyle` stub for text areas — measured 3.5 ms a call against 50 ms for one by-role query, and it would change what jest-dom and Testing Library see on every text area; `within(...)` scoping of the queries left — the held elements took the repeated ones, the rest are single queries per test; and the pointer-events switch, which the review itself calls not worth it. The full suite under load is `verify.sh`'s to judge.
