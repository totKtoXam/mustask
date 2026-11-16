# feat-060 — code review

## Round 1

Diff `origin/main...c2bdc4e9` (12 files), read whole against status.md Plan 1-4 and D-1.
**Verdict: approve** — 0 blocker, 0 major, 5 minor (debts, not a round).

Checked and sound: the stripe extraction leaves the team board's styles byte-identical
(`BoardColumns.tsx:355`, `:521`; pinned by `BoardPage.test.tsx:60`, `:189`); the `scope` place
changes nothing for `team` and `project` (every new branch is gated by `scopePlace`/`picker`,
and `picker` is exactly the old `teamPlace !== null` for those two kinds); the create command
exists only for `view === 'boards'` at organization/department level and only over projects in
`GET /projects?active` whose codes hold `workitems.workitem.write` (`useAggregateNewWorkItem.tsx:60-73`);
the server still decides on the project's own endpoint.

### Findings

**m1 — minor. The project-filter preselect is unreachable; the hook's doc claims it.**
`useAggregateNewWorkItem.tsx:34-35` ("The address's project filter preselects a single project") and
`:124` (`filterProjectIds: selectors.projectId ? [...] : []`): `inScope` (`:60`) needs level
organization or department, and `viewLevelOf` (`lib/grouping.ts:19`) returns `project` whenever
`projectId` is set; `AggregateFilter` has no project filter. So the list is always `[]` and Plan 2's
"preselected when the view's project filter names exactly one" has no case. (QA's minor.)
Fix: pass `filterProjectIds: []`, drop the sentence, and say in status.md that the preselect has no
case at these levels.

**m2 — minor. `contextGone` is dropped.** `useAggregateNewWorkItem.tsx:133`
`onClose={() => setCreatingTypeId(null)}` ignores the dialog's `contextGone`, which the team views
act on (`hooks/useNewWorkItem.tsx:162-166`, the page is read again). After a not-found project the
board keeps the stale lane. Fix: take `onContextGone` and call `v.readHeadersAgain()` from the page.

**m3 — minor. Creation remounts the whole board.** `AggregateViewPages.tsx:65`
`key={createdSerial}`: every open lane drops the pages its «Показать ещё» loaded and every open cell
is read again (under the cap of 4, but N lanes x columns reads for one card). A move already does it
narrowly (`AggregateBoardView.tsx:250-251`: `registry.get(to.key)?.reload()` + headers). Fix: reload
only the initial state's cell of the item's lane (or accept, and say so in the comment at `:44`).

**m4 — minor. Tests check less than they name.**
(a) `AggregateViews.boardCreate.test.tsx:534-536`: the card stripe asserts `solid` and a non-empty
colour, so any colour passes; the team board pins the value (`BoardPage.test.tsx:189`). Assert the
type's colour. (b) The per-project write filter is tested only all-or-nothing: `testAggregate.tsx:206`
serves the same codes on every project, so a bug offering a project without write next to one with
it passes. Add a case with write on Мастаск only, expecting `['Мастаск']`. (c) No assertion that the
aggregate «Рабочие элементы» has no command (QA's minor; structurally true — `AggregateWorkItemsPage`
does not use `AggregatePage`).

**m5 — minor. Direction 2: feat-048's spec still says the opposite.**
`docs/features/feat-048-aggregate-level-views/spec.md:95` "no create command on the aggregate views"
is untouched; D-1's reversal lives only in status.md notes (feat-048 `status.md:79`, feat-060 D-1).
A reader of the spec (a later QA writing "absent on every aggregate view") is misled, though such a
test would fail visibly. Fix: one clause at `:95` — "(the board: reversed by feat-060 D-1)".

Note, not a finding: a department board narrowed by a team (`?teamId=`) is level `team` by
`viewLevelOf`, so it offers no create — consistent with Plan 2's "department and organization levels".

### Claims (item 6)

- `boardStripes.ts:5` "one definition so they cannot drift apart" → both boards import it (`BoardColumns.tsx:53`, `AggregateBoardView.tsx:33`). Anchored.
- `boardStripes.ts:6` "nothing is restyled" → expressions identical to the removed lines of `BoardColumns.tsx`. Anchored.
- `useAggregateNewWorkItem.tsx:28-33` active projects of the scope with write → `:62-73` + `useProjectCodes` (`GET /projects` active). Anchored.
- `useAggregateNewWorkItem.tsx:32-33` project list from `selector-offers`, nothing requested → `context.offers` (`:61`); the codes read shares the board's own `useProjectCodes` cache (`AggregateBoardView.tsx:292`). Anchored.
- `useAggregateNewWorkItem.tsx:34-35` filter preselects → m1.
- `AggregateHeader.tsx:36` command absent on the other views → `AggregateViewPages.tsx:42` `enabled: view === 'boards'` + hook `:60`. Anchored.
- `AggregateViewPages.tsx:36` backlog and «Рабочие элементы» keep feat-048's rule → `:42`; workitems page separate. Anchored.
- `AggregateViewPages.tsx:44` "the new item stands in its first column" → board re-read (`:45`, `:65`); first column holds only if the initial state is in it — soft, see m3.
- `CreateWorkItemDialog.tsx:50-51` (scope doc) «Команда» starts as the chosen project's default → `:177-180`, reset at `:220-223`. Anchored.
- `CreateWorkItemDialog.tsx:160` board preselects on its filter only → `:161-166`. Anchored (but no filter reaches it, m1).
- `CreateWorkItemDialog.tsx:169` «Без команды», read-only until chosen → `:170`, `readOnly={noProjectChosen}` `:426`, choices `[]` `:308`. Anchored.
- feat-048 `status.md:79` note: board only, backlog and «Рабочие элементы» keep the rule → as above. Anchored.
- Direction 2: behaviour changed = create on the aggregate board; grepped "create command", "no create", «Создать», BR-AV-8 under `docs/features/feat-043-*` and `feat-048-*` (spec, design, stories). Hits: feat-048 `spec.md:95` → m5; feat-043 `spec.md:91` "No create command" sits in the backlog's part of BR-NV-20 (still true); feat-043 `spec.md:122` row's "no: derived from the items" is about the view, not items (still true); US-2/US-6 hits concern other pages. `.claude/` and `AGENTS.md` not swept (no identifier of this behaviour lives there).
