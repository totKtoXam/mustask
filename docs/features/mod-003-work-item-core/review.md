# Code review: mod-003-work-item-core

## Round 1 — whole task

Reviewed at `d428cb9` against `main` (8953abf), per D-1: one round over
backend and frontend, authorization included, no separate security pass, no
delta round. D-2's scope cut (no history, tags, rich text, concurrency token,
deletion, Приоритет; BR-M3-25 last-write-wins), D-3 (every signed-in user may
do everything inside an active organization) and D-5 (project-level form
address) are treated as binding.

**Verdict: approve.** No blocker, no major. Five minors below, each a debt
with its proposed fix.

### What was checked and holds

- **Conformance.** The five routes, their DTOs, statuses and error codes
  match api-contract.md § 0–§ 5; the answer order of § 0.4 holds in
  `SaveWorkItemHandler.cs:49-97` and `CreateWorkItemHandler.cs:49-98`.
  Nothing beyond D-2's cut: no type change, no Removed state, no deletion,
  no priority. Screen copy matches spec § Screens and copy (list, dialog,
  form, pickers, not-found frames, notices).
- **Numbers.** The counter row is created `ON CONFLICT DO NOTHING` and locked
  `FOR UPDATE` before the number is taken (`WorkItemRepository.cs:16-38`),
  all inside one READ COMMITTED transaction (`UnitOfWork.cs:30-35`); a failed
  creation rolls the counter back. Real-overlap tests exist
  (`CreateWorkItemEndpointTests.cs:87-123`).
- **Idempotency.** The replay is looked up after the counter lock and scoped
  to the project (`WorkItemRepository.cs:40-45`, unique index
  `(project_id, creation_request_id)`), so a key first used in another
  project or organization creates a new item and never returns the other's;
  a replay with a different body returns the first item, as § 2.2 says.
  Another user replaying a key in the same project gets an item they may read
  anyway (D-3).
- **PATCH.** Absent or `null` fields are untouched; `"assignee": null`
  keeps, `{ "userId": null }` / `{}` clears (`WorkItemEndpoints.cs:132`,
  `SaveWorkItemHandler.cs:167-183`); the state must be one of the item's own
  type and never `Removed` (`WorkItemTypeStates.cs:43-44`); the area must be
  of the same project, locked `FOR SHARE` (`WorkItemRepository.cs:56-64`);
  `changedAt` moves only on a real change (`WorkItem.cs:151-188`); the item is
  locked before it is loaded, so concurrent saves of different fields both
  survive.
- **Identifier.** Parsed by an anchored regex with `\z`
  (`WorkItemIdentifier.cs:20`), the project key compared with the addressed
  project's key, the number looked up inside the addressed project only, the
  type key compared after the read (`GetWorkItemHandler.cs:36-47`,
  `SaveWorkItemHandler.cs:62-76`). `ABC-BUG-4` under MSK answers not-found and
  is tested.
- **Area rule.** `IAreaUsage` is asked after mod-002's `FOR UPDATE`
  (`DeleteAreaHandler.cs:75,87`); the save's `FOR SHARE` serializes with it;
  the composite FK `(project_id, area_id)` → `ak_areas_project_id_id` with
  `Restrict` is the backstop (`AddWorkItems.cs:59-64`). Both race orders are
  tested with a commit gate (`DeleteAreaWithWorkItemsEndpointTests.cs:105-138`).
- **List and search.** Ordered by `changed_at DESC, number DESC` (total order),
  50 a page, a long offset guarded (`WorkItemReadModel.cs:35-80`). Assignee
  search: active users only, at most 20, text over 100 refused by mod-014.
- **Security.** All five routes carry `AuthenticatedOnly` and
  `RequiresOrganizationContext` (`WorkItemEndpoints.cs:42-86`) and are in the
  route inventory; every handler re-checks that the project is reachable and
  of the addressed organization, so no item is reachable by another
  organization's or project's address. Every SQL is parameterized
  (`ExecuteSqlInterpolatedAsync`). No new logging; error bodies carry catalog
  texts only.
- **Architecture.** `IAreaUsage` is declared on M1's side and implemented in
  `Infrastructure/WorkItems`, so M1 never reads work items; the dependency
  points from M3 to M1. One folder per use case. Frontend slice imports other
  slices only through their `index.ts`. `TypeMark`/`CategoryMarker` and
  `initials` moved to `shared/` with two consumers each, and nothing in
  `shared/` imports `features/`.
- **Frontend.** The PATCH body holds only the fields that differ
  (`draft.ts:35-55`); the `requestId` is made at the first «Создать», kept on
  retries, and new for each dialog (`CreateWorkItemDialog.tsx:60-86`); create
  invalidates the project's list, save writes the item cache and invalidates
  the list (`mutations.ts`); refusals are placed by `errorCode`
  (`refusals.ts`, `notFoundLevel.ts`); the nav entry and the switcher follow
  the address (`addressContext.ts`, `routes.workitems.test.tsx`).
- **Tests.** For each risky rule a test would fail if the rule broke: number
  races, same-key overlap, area race in both orders, two concurrent saves,
  answer order, foreign project, wrong project key, Removed state, inactive
  assignee. One gap is m5.

### Findings

| # | Severity | Where | Finding | Proposed fix |
|---|---|---|---|---|
| m1 | minor | `src/frontend/src/features/workitems/pages/WorkItemFormPage.tsx:167-170`, `src/frontend/src/app/App.tsx:10-17` | The first edit copies the **whole** loaded item into `edited`. If the item query refetches during an edit (TanStack's `refetchOnReconnect` is on by default, for example after a laptop sleeps), `changesOf` compares the old copy with the new item. Fields the user never touched then go into the PATCH with their old values, silently reverting another user's change, which goes against BR-M3-25. The trigger is narrow, so this is minor. | Keep only the touched fields (`Partial<WorkItemDraft>`) and build the draft as `{ ...draftOf(item), ...edited }` at render. Add one component test that changes the cached item during an edit. |
| m2 | minor | `src/frontend/src/features/workitems/components/CreateWorkItemDialog.tsx:72` | The `requestId` is kept even when the user changes «Тип» or «Заголовок» after a failure. After a lost answer whose creation did land, the resend replays the first item: the notice reads «Рабочий элемент X создан.», but the stored item has the old title or type. This is legal under § 2.2 (a replay's body is not compared), but the user is not told. | Keep the key while the body is unchanged and make a new one when the user edits after a definite refusal; or compare the replay's title and type with what was sent and say so in the notice. |
| m3 | minor | `src/backend/Mustask.Domain/WorkItems/WorkItemText.cs:23-46` | A U+0000 in `title` or `description` passes validation, and PostgreSQL rejects it (22021), so a direct API call gets a 500 instead of a field reason. | Refuse control characters in the title (with `title_multiline` or a new reason), and refuse or strip U+0000 in the description. Add a theory row to `WorkItemTextTests`. |
| m4 | minor | `src/backend/Mustask.Infrastructure/WorkItems/WorkItemRepository.cs:56-64` | M3's repository takes a raw `FOR SHARE` lock on M1's `areas` row, so the deletion-versus-save lock protocol is split between two modules: M1 owns the `FOR UPDATE` side and M3 the `FOR SHARE` side. `IAreaUsage` keeps M1 out of M3's data, but nothing keeps M3 out of M1's locks. | Expose the lock from M1, for example `IOrganizationStructure.LockAreaForShareAsync(projectId, areaId)`, so M1 owns both sides of its row lock. |
| m5 | minor | `src/backend/tests/Mustask.Api.IntegrationTests/Features/WorkItems/CreateWorkItemEndpointTests.cs:128-168` | No test pins the project scope of the idempotency key. If `item.ProjectId == projectId` were dropped from `FindCreatedByRequestAsync` (`WorkItemRepository.cs:43`), a create in project B with a key first used in project A (or in another organization) would answer A's item, and no test would fail. | Add `Create_SameRequestIdInAnotherProject_CreatesItsOwnItem`: one key, two projects (one in another organization), two items, each answer from its own project. |

### Behavioural claims in prose (reduced item 6)

Direction 1 — claims in prose the diff adds, read against the code:

- api-contract § 2.2 "a replay … no number is consumed; two at the same
  moment leave one item, 201 and 200" → `CreateWorkItemHandler.cs:85-90`,
  test `CreateWorkItemEndpointTests.cs:148-168`. Holds.
- api-contract § 2.4 "`changedAt` moves only when at least one value
  changes; `{}` answers 200" → `WorkItem.cs:151-188`,
  `SaveWorkItemHandler.cs:92-95`. Holds.
- api-contract § 2.4 "re-sending the current assignee even when inactive is
  accepted" → `WorkItem.cs:146`. Holds.
- api-contract § 0.4 order of answers → `SaveWorkItemHandler.cs:49-90`,
  test `SaveWorkItemEndpointTests.cs:424-438`. Holds.
- api-contract § 6 race and order of refusals → `DeleteAreaHandler.cs:75-92`,
  `Area.cs` `EnsureDeletable`, `WorkItemRepository.cs:56-64`. Holds.
- api-contract § 7 port signature and "M1 never reads work items" →
  `IAreaUsage.cs`, `WorkItemAreaUsage.cs:13-14`. Holds.
- status.md Plan 7 "save puts its answer in the item's cache and invalidates
  the list" → `mutations.ts` `useSaveWorkItem`. Holds.
- status.md Log (stage 4) "real races for numbers, replays, concurrent saves
  and area deletion vs save (both orders)" → the four gated tests named above.
  Holds.
- No `AGENTS.md` file is in the diff.

Direction 2 — prose outside the diff about behaviour the diff changes:

- Changed behaviour: mod-002's area deletion also refuses an area that holds
  work items. Grepped `not_empty` and `IAreaUsage` under
  `docs/features/mod-002-organizations-projects-teams/`. Hits:
  `api-contract.md:467` (already says "From mod-003, also work items"),
  `:542` (the text, unchanged), `plan-backend.md:507`, `plan-frontend.md:649`
  and `qa.md:80,262` (M1's own children race and dialog, not contradicted).
  None is stale.
- Changed behaviour: `TypeMark` and `initials` moved to `shared/`. Grepped
  the old paths under `docs/` and the `AGENTS.md` files. Hits are only in
  merged mod-001 and mod-015 plans, reviews and QA reports, as historical
  records. No live prose.
- Not swept: the e2e spec 22 and whether any runbook lists it.

### Not read

The e2e spec `22-work-items.spec.ts` and `e2e/support/*`; the frontend test
doubles `testApi.ts`, `testFixtures.ts` and `testRender.tsx`; the bodies of
most frontend component tests (names read, scenarios matched to the stories);
the domain, contract and unit test bodies (names read); the generated
migration Designer and model snapshot; the Kazakh texts in
`contracts/errors/workitems.json`; the stories US-1..US-6 beyond what
spec.md restates.
