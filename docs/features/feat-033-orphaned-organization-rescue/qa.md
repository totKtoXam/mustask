# QA — feat-033-orphaned-organization-rescue

## Backend

Pass 1, 2026-11-03, qa-engineer (clean session, deep tier for L). Test plan
written from `spec.md`, US-1..US-3 and `api-contract.md` before the
implementation was read. Checkout: the task worktree, HEAD 0de1cd5b
(backend commits df5346c0..6fb75f09). E2E: not asked (standing answer no;
frontend QA owns the question on this route).

Routes: R1 `GET /rescue`, R2 `GET /rescue/appointee-candidates`, R3 `POST
/rescue/{id}/appointments`, R4 `GET /rescue/{id}/appointments`, O1 `GET
/{id}/appointments` (all under `/api/organizations`). Test classes, short
names: AAE `AppointAdministratorEndpointTests`, AAC `…ConcurrencyTests`, ARA
`AppointmentRecordAtomicityTests`, GRO `GetRescueOrganizationsEndpointTests`,
GAE `GetAppointmentsEndpointTests`, SAC `SearchAppointeeCandidatesEndpointTests`,
ARM `AppointmentReadModelTests` (Infrastructure), QA `RescueQaGapTests` and
TP `AppointmentReasonTrimParityTests` (added by this pass).

### Coverage matrix

| Criterion (spec) | Check | Test type | Status |
|-----------------|----------|-----------|--------|
| BR-RS-1 | `organizations.organization.rescue` Instance scope, `system-admin` only; panel mirror | unit (`InstanceRolesTests`, `PermissionCatalogTests`, `tokenClaims.test.ts`) | ✅ |
| BR-RS-1, "Without the right" ×3 | R1..R4 → 403 `organizations.rescue.forbidden` «Недостаточно прав» for: no role, `user`, `user`+`organization-creator`, `user`+`dictionaries-admin`, both, `system-admin` without `user` (401/403), `system-admin` of a foreign client, `system-admin` as a realm role, the organization's own admin; nothing written | integration (QA, AAE, GRO, SAC, GAE) | ✅ |
| BR-RS-6a row 1 | no right + missing organization → 403 on R3 and R4 | integration (QA) | ✅ |
| BR-RS-1 | 401 on R1..R4 and O1 | integration | ✅ |
| US-1 list | active + deactivated, exactly id/name/status/lastAppointedAt, name order | integration (GRO), ARM | ✅ |
| US-1 filter | substring, case, ё=е both ways, trimmed, blank = all, `%` `_` `\` literal, >100 chars → count 0 | integration (GRO, QA), ARM | ✅ |
| US-1 paging | 25 a page, true count, page past last `[]`, `page` 0/-1/abc/1.5/overflow/empty → 400 `invalid_page` | integration (GRO, QA) | ✅ |
| US-1/US-2 content hidden | rescuer after appointing: 404 on the organization, `/access`, `/members`, `/projects`, `/teams`, `/overview`, O1, `/member-candidates` | integration (QA, AAE) | ✅ |
| BR-RS-6 search | active projected only, inactive hidden, <2 chars none, 3 fields, no membership, >100 → 400 | integration (SAC) | ✅ (20-cap and `moreMatched` belong to mod-014's own tests) |
| US-2 non-member / member / viewer in a team | add or raise, addedBy kept, roleChangedBy = caller, teams kept, others identical | integration (AAE) | ✅ |
| BR-RS-4 add/raise only | every other membership row identical, refusals included | integration (AAE, QA) | ✅ |
| US-2 deactivated / repeatable / next request | stays deactivated, appointee restores; second appointee leaves first; appointee reads O1 (membership.write) with the same token | integration (AAE, QA) | ✅ |
| US-2 refusals | already admin, never signed in, pre-tech-025 inactive, self (and another rescuer may), unknown/malformed/zero org | integration (AAE) | ✅ |
| BR-RS-5 reason | empty/spaces/tab, 500 and 501 chars and emoji, trimmed store, 502 → 500 after trim, NUL, noncharacter, Unicode spaces, lone surrogate → 400 | unit + integration (AAE, QA, TP) | ⚠ D-1 |
| BR-RS-6a order | all five rows of the outline + inactive-admin → unavailable, self-not-member-no-reason → self | integration (AAE, QA) | ✅ |
| BR-RS-10 record / atomic / refusal writes none | 201 body = row; FK failure in the same save → 500, nothing kept | integration (AAE, ARA) | ✅ |
| BR-RS-10 readers | R4 any org, deactivated incl., newest first, `[]`, 404; O1 admin 200 same bytes, member/viewer 403 `organizations.access.forbidden`, stranger / other org's admin / deactivated org's admin 404 | integration (GAE, QA) | ✅ |
| US-3 record outlives membership | role change and removal after appointment | integration (GAE, AAC) | ✅ |
| BR-RS-11 | same user ×2, different users, vs removal both orders, vs role change both orders, vs raise-to-admin first → `already_admin` | integration (AAC, QA) | ✅ |
| BR-M1-33 inventory | two rescue routes are exact instance exceptions; O1 in the list | integration (`OrganizationAccessInventoryTests`) | ✅ — but three neighbouring inventory tests are red: D-2 |
| Migration | applies on a clean database, rollback drops only the table, re-apply restores | integration (`MigrationsTests`) | ✅ |
| Error codes | 8 codes in catalog + `contracts/errors` | contracts (`OrganizationErrorCatalogTests`) | ✅ |
| NFR volumes / query budget | 1000 orgs: 2 statements < 2 s; 100 records: 2 statements | integration (ARM) | ✅ |

### Additional scenarios (beyond the spec)

| # | Scenario | Expectation | Status |
|---|----------|----------|--------|
| A-1 | malformed JSON from a caller without the right | 403, not 400 | ✅ QA |
| A-2 | unknown organization and an invalid body | 404 before 400 | ✅ by code (handler step 1–2 before 3) and AAE "unknown user + missing org" |
| A-3 | `userId` missing / blank / 256 chars | 400 `membership.user_invalid` | ✅ AAE, QA |
| A-4 | `userId` with spaces around a known id | not trimmed → `user_unavailable` | ✅ QA |
| A-5 | reason of NBSP / U+3000 / U+2003 only | `reason_required` | ✅ QA, TP; U+FEFF ✗ D-1 |
| A-6 | 502 code points trimming to 500 | 201 | ✅ QA |
| A-7 | lone surrogate `\ud800` | 400, no 500 | ✅ QA |
| A-8 | `lastAppointedAt` = newest record | max | ✅ ARM |
| A-9 | `page` 0, -1, abc, 1.5, overflow, empty | 400 `invalid_page` | ✅ GRO, QA |
| A-10 | O1 for a deactivated organization by its admin | 404 | ✅ QA |
| A-11 | system-admin who is also a member reads O1 by role, R4 regardless | as contract | by code only (O1 is the plain scoped check, R4 asks no membership); not tested |
| A-12 | pre-tech-025 inactive row already admin | `user_unavailable` | ✅ QA |

### Runs

Filtered only (shared lane). The two whole-assembly reruns were the
coordinator's, not this session's.

| Date | What was run | Result |
|------|-----------------|-----------|
| 2026-11-03 | coordinator: whole `Mustask.Infrastructure.Tests` (incl. `MigrationsTests`, ARM) | 459/459 green |
| 2026-11-03 | coordinator: whole `Mustask.Api.IntegrationTests`, compiling this pass's uncommitted QA file | 5 failed: 2 × QA `Appoint_AReasonOfUnicodeSpacesOnly_IsRequired` (U+FEFF → 201, D-1) and the three tests of D-2; every other feat-033 and QA test green |
| 2026-11-03 | `dotnet test Mustask.Domain.Tests --filter FullyQualifiedName~AppointmentReasonTrimParityTests` (before the skips) | 3 passed, 3 failed: the U+FEFF cases (D-1) |
| 2026-11-03 | same, after marking the U+FEFF cases `Skip = D-1` | 3 passed, 3 skipped |
| 2026-11-03 | `dotnet test Mustask.Api.IntegrationTests --filter FullyQualifiedName~RescueQaGapTests.Appoint_AReasonOfUnicodeSpacesOnly_IsRequired` (cases as code points, U+FEFF skipped) | 1 passed, 2 skipped |

### Defects

#### D-1: «Причина» keeps U+FEFF, which the panel trims

- Severity: minor
- Steps: as a system-admin, `POST /api/organizations/rescue/{id}/appointments`
  with `{"userId": "<known>", "reason": "\uFEFF"}` (the JSON escape) (or a reason pasted with a
  leading byte-order mark).
- Expected / Actual: BR-RS-5 says the dialog and the server count the same way;
  the panel trims with `String.prototype.trim`, which removes U+FEFF, so
  expected 400 `reason_required` and a stored reason without the mark. Actual:
  201 and a record whose reason is an invisible character; `.NET string.Trim()`
  does not treat U+FEFF as white space. Also off by one at the limit: U+FEFF +
  500 characters passes the dialog and gets `reason_too_long` from the server.
  Fix: trim U+FEFF too in `AppointmentReason.TryCreate` (Domain). Tests ready,
  skipped with this id: TP `OnlyCharactersThePanelTrims_IsRequired` (2 cases),
  `ALeadingByteOrderMark_IsNotKept`, QA `Appoint_AReasonOfUnicodeSpacesOnly_IsRequired` (2 cases).
- Status: open — carried to gate-3 as a debt

#### D-2: three existing Api integration tests were not updated for the new routes and code

- Severity: major (the branch's `Mustask.Api.IntegrationTests` is red; no MR
  with failing tests). The production behaviour is right; the fixes are in
  test code.
- Steps: run the three tests below (they failed in the coordinator's whole
  rerun of the assembly).
- Expected / Actual:
  1. `TeamViewContainmentTests.TheRouteInventory_ListsTheThreeTeamAddressedRoutes_AndNotTheProjectNestedOnes`
     asserts `OrganizationAddressedRoutes.All.Count == 73`; actual 74 (O1 added).
     The same count was raised to 74 in `OrganizationAccessInventoryTests` but
     not here.
  2. `OrganizationsOpenApiTests.OpenApiDocument_OrganizationOperations_AreExactlyTheContracts`
     asserts `ContractOperations.Length == 58`; actual 59 (O1 enters through
     `OrganizationAddressedRoutes`). The four rescue operations are tagged
     `Organizations` too and are not in `ContractOperations`, so after the
     count the set comparison is likely to fail next: the list needs the rescue
     operations.
  3. `CreateOrganizationEndpointTests.Create_EveryCodeButTheCreateRight_Returns403CreateForbidden_CreatesNothing`:
     `KeycloakTestRealm.RolesFor` finds no role set that gives every code but
     `organizations.organization.create`. The only role holding the new rescue
     code is `system-admin`, which also holds create, so "every other code
     together" cannot be expressed in roles any more. The test needs restating
     (for example, every role but `system-admin` and `organization-creator`,
     or dropping instance codes no role-set can carry without create).
- Status: open — back to backend-developer

#### D-3: api-contract § 3 gives `invalid_page` the wrong field

- Severity: minor
- Steps: read `api-contract.md` § 3, row `organizations.rescue.invalid_page`.
- Expected / Actual: "400 (field page)", as § 1.1 and the code have it; the
  table says "400 (field reason)". Documentation only; a change of
  `api-contract.md` goes with a note in `status.md` (not edited by QA).
- Status: open — carried to gate-3 as a debt

### Verdict

blockers: 0 · majors: 1 (D-2) · minors: 2 (D-1, D-3) · result: fail —
D-2 returns to the backend-developer; the re-check covers the three tests and
nothing else.

## Frontend

Pass 1, 2026-11-03, qa-engineer (clean session). Test plan (the matrix below,
statuses ☐) written from `spec.md`, US-1..US-3, `design.md` and
`api-contract.md` and saved here before the implementation (4535774d) was read.
E2E: skipped by the human's decision (standing answer no, 2026-10-12); not asked
again. Tests added by this pass: `AllOrganizationsPage.qa.test.tsx` (short:
QA). Developer's files: AOP `AllOrganizationsPage.test.tsx`, AOA
`AllOrganizationsPage.appoint.test.tsx`, MPH `MembersPage.history.test.tsx`,
`appointmentReason.test.ts`, `rescueRefusals.test.ts`,
`AppLayout.administration.test.tsx`.

### Coverage matrix

| # | Criterion (spec / design) | Check | Test type | Status |
|---|---|---|---|---|
| F-1 | BR-RS-1, design § 2: «Администрирование» → «Все организации» only with `organizations.organization.rescue`; `system-admin` alone carries it in the mirror | entry listed, placed, absent without the code; `INSTANCE_ROLE_CODES` | component (`AppLayout.administration`), unit (`tokenClaims.test.ts`, backend pass) | ✅ |
| F-2 | US-1 "Without the right": by address → «Доступ ограничен», no request | page without the code | component (AOP) | ✅ |
| F-3 | US-1 rows, DC-3: plain name (no link), «Активна» / «Деактивирована» as words, «ДД.ММ.ГГГГ», «—» + hidden «Назначений не было», no administrator wording | render | component (AOP) | ✅ |
| F-4 | BR-RS-2 paging: 25, «1–25 из 120», `page=2` | pager | component (AOP) | ✅ |
| F-5 | BR-RS-2 filter: trimmed after the pause, page 1 on change, spaces = no filter, «Организации не найдены» + «Сбросить фильтр», «Организаций пока нет», «Найдено: N» | filter | component (AOP, QA) | ✅ (D-10) |
| F-6 | § 3.3 states: skeleton, `aria-busy` + old rows while a page loads, error + «Повторить», 403 → `AccessDenied` (list, appoint, history), past-last page → last page re-read | states | component (AOP, AOA, QA) | ✅ (D-8, D-9) |
| F-7 | US-2 dialog: lead with the name, Keycloak warning from the first frame, focus in «Пользователь», «Назначить» disabled until a user, enabled with an empty «Причина» | dialog | component (AOA) | ✅ |
| F-8 | BR-RS-5, DC-6: «Укажите причину» for empty / spaces / U+FEFF only; 501 letters and 501 emoji refused, 500 emoji sent; leading U+FEFF not counted; trimmed text sent; nothing sent while invalid; focus to «Причина» | validation | unit + component (AOA, QA) | ✅ |
| F-9 | § 4.2 refusals by code with the catalog text: `rescue.forbidden` → page; `not_found` → notice, «Назначить» disabled, list re-read; `user_unavailable` / `self_appointment` / `already_admin` → «Пользователь» helper, `aria-invalid`, cleared on change; `reason_required` / `reason_too_long` / `reason_invalid` → «Причина»; 500 → notice; the server's `detail` not shown for a known code | refusals | unit + component (`rescueRefusals`, AOA) | ✅ (D-6) |
| F-10 | § 4.3 success (closes, «Администратор назначен: <имя>», list re-read, focus to «Действия: <название>»); «Отмена» sends nothing | success / cancel | component (AOA) | ✅ |
| F-11 | US-2 search: appointee route, no organization id, from 2 characters, name + e-mail only | search | component (AOA) | ✅ |
| F-12 | US-3 / § 5: title, 4 columns, rows as served, «ДД.ММ.ГГГГ ЧЧ:мм», full reason, only «Закрыть», focus on «Закрыть», empty, error + «Повторить», 403 / 404 notices | history | component (AOP, MPH, QA) | ✅ (D-7) |
| F-13 | § 5 hosts: list (rescue route), «Участники» only with `membership.write` (organization route, never `/rescue/`), not for member / viewer, focus back to the opener on both | hosts | component (AOP, MPH, QA) | ✅ |
| F-14 | § 12 point 2 (gate-2): history dialog sized to its content, not 90 %; a long reason wraps in its column | layout arithmetic from the code and kit sizes | review | ✗ D-4, D-5 |

### Additional scenarios (beyond the spec)

| # | Scenario | Expectation | Status |
|---|---|---|---|
| A-1 | `organizations.membership.already_member` (contract row 8) or `user_invalid` | the dialog's notice with the server's catalog text | by code (falls through to the alert); not tested |
| A-2 | 403 on the «Участники» host's history read | notice in the dialog; codes re-read | ✅ MPH (notice); the re-read is the shared access watcher's, not the host's |
| A-3 | Esc / ✕ while «Назначить» runs | ignored (`busy`) | by code (FormDialog → DialogShell `busy`); not tested |
| A-4 | the list itself answers 403 | no «Повторить», no retry | ✅ AOP |

### Deviations reported by the developer

1. **History dialog `size="form"` (600 px).** Not accepted: D-4. Gate-2 decided
   "the kit's ordinary wide dialog sized to its content (DialogShell md/lg)";
   `form` is MUI `sm`. Inside it (600 − 2 × 24 px = 552 px) the table's
   `minWidth` 560 means a sideways scroll at every width; the fixed columns
   130 / 150 / 150 leave «Причина» ≈ 130 px (≈ 114 px of text, about 16
   characters a line, so a 500-character reason is ~30 lines), and «Кто
   назначил» / «Кого назначили» ≈ 134 px truncate a name like «Системный
   Администратор» (a «неактивен» chip leaves almost nothing). The date fits.
   The columns do not fit readably, and the one the record exists for is the
   narrowest. Judged from the code and the kit's sizes: jsdom has no layout, and
   the panel was not opened (it needs the stand to sign in).
2. **Pager not disabled while the next page loads.** Accepted as a minor debt
   (D-9): `TablePager` has no such prop; harmless (the count is known), but it
   is a § 3.3 state, and the range «26–50 из 120» shows before the rows change.
3. **«Причина» hint only on focus.** Accepted, not a defect: the kit's field
   rule (`FieldFrame` shows the hint on focus and keeps it in the DOM as the
   accessible description); the limit matters only while typing, and the error
   replaces it on the press.
4. **A filter change may send one request with the old filter.** Accepted as a
   minor debt (D-10): `changeText` sets page 1 at once while the settled text is
   still the old one, so from page > 1 one `?page=1&name=<old>` goes out and its
   rows can flash before the filtered answer.

### Runs

Single files only; the full Vitest suite is the shared lane's and was not run.
The developer's files were reported green at stage 6 and not re-run.

| Date | What was run | Result |
|---|---|---|
| 2026-11-03 | `npx vitest run src/features/organizations/pages/AllOrganizationsPage.qa.test.tsx` (first version) | 6 passed, 4 failed: D-6, D-7, D-8, and one test of mine (the `aria-busy` test waited on the pager range, which changes before the rows; fixed) |
| 2026-11-03 | same file again, to read the failing names (no new information; against run economy, noted) | same 4 |
| 2026-11-03 | same file, `-t "aria-busy"` after fixing my test | 1 passed |
| 2026-11-03 | same file, final (D-6/7/8 tests skipped with their id) | 7 passed, 3 skipped |
| 2026-11-03 | `eslint`, `prettier --write`, `tsc -p tsconfig.app.json --noEmit` | clean |

### Defects

#### D-4: «История назначений» is 600 px, not sized to its content (gate-2)

- Severity: major (contradicts a human gate-2 decision; the reason column is unreadably narrow)
- Steps: «Все организации» → «Действия» → «История назначений» on an organization with a long reason; or read `AppointmentHistoryDialog.tsx` (`size="form"`, `COLUMN_WIDTHS` 130/150/150, `TABLE_MIN_WIDTH` 560).
- Expected / Actual: design § 12 point 2: a wider kit dialog sized to its content, the reason wrapping in its own column. Actual: MUI `sm` (600 px), a constant sideways scroll (560 > 552), «Причина» ≈ 114 px of text, names truncated (deviation 1 above).
- Fix needs a call: a `md` size in `DialogShell` is a kit change (component, test, showcase entry), the orchestrator's decision, or the human's if the kit is not to grow.
- Status: fixed in b7faeac7 (pass 2, by code; see Pass 2)

#### D-5: a long unbroken «Причина» is not wrapped

- Severity: minor
- Steps: a record whose reason is one long token (a URL, a run of ticket keys) → «История назначений».
- Expected / Actual: design § 5: `overflowWrap: anywhere`, never truncated. Actual: plain `Cell` text in a `table-layout: fixed` table; a word wider than the column spills out of it.
- Status: fixed in b7faeac7 (pass 2, by code; see Pass 2)

#### D-6: focus does not move to «Пользователь» after a user refusal

- Severity: minor (accessibility)
- Steps: dialog → pick a user → reason → «Назначить» → 409 `already_admin` (or `user_unavailable`, `self_appointment`).
- Expected / Actual: design § 4.2 "Focus goes to the field that carries the error". Actual: the helper and `aria-invalid` are set; focus stays where it was. Test: QA `…puts focus on «Пользователь»` (skipped, D-6).
- Status: fixed in b7faeac7 (pass 2, by code; see Pass 2)

#### D-7: the history dialog takes focus back to «Закрыть» on every render

- Severity: minor (accessibility)
- Steps: open «История назначений» while the read is slow; move focus to the title-bar ✕ (or to «Повторить»); the answer arrives.
- Expected / Actual: focus starts on «Закрыть» once (§ 5). Actual: `ref={(button) => button?.focus()}` is a new callback on each render, so React re-runs it and focus jumps back to the footer «Закрыть» whenever the dialog re-renders. Test: QA `leaves focus where the user moved it…` (skipped, D-7).
- Status: fixed in b7faeac7 (pass 2, by code; see Pass 2)

#### D-8: a list read goes out after the page has turned «Доступ ограничен»

- Severity: minor
- Steps: an appointment answered 403 `organizations.rescue.forbidden`.
- Expected / Actual: DC-8 / § 11: the page's queries are dropped and nothing more is asked. Actual: `removeQueries` runs while `useRescueOrganizations` is still mounted above the `AccessDenied` return, the observer re-creates the query, and one more `GET /rescue` goes out (a sure 403). Test: QA `asks for no list after…403` (skipped, D-8).
- Status: fixed in b7faeac7 (pass 2, by code; see Pass 2)

#### D-9: the pager stays active while a page loads

- Severity: minor (deviation 2)
- Expected / Actual: § 3.3 "the pager is disabled meanwhile". Actual: active; the range text changes before the rows.
- Status: fixed in b7faeac7 (pass 2, by code; see Pass 2)

#### D-10: a filter change sends one request with the old filter

- Severity: minor (deviation 4)
- Expected / Actual: one request per settled filter. Actual: from page > 1 a `?page=1&name=<old>` goes out first; its rows may flash.
- Status: fixed in b7faeac7 (pass 2, by code; see Pass 2)

### Verdict

blockers: 0 · majors: 1 (D-4) · minors: 6 (D-5..D-10) · result: fail — D-4
goes back through the orchestrator (the kit size is a decision); the re-check
covers the history dialog's layout (and D-5 / D-7 if fixed with it) and what the
change could break.

### Pass 2 (re-check of b7faeac7)

2026-11-03, same QA session, HEAD b7faeac7. Scope: D-4..D-10 and what the three
kit additions (`DialogShell size="wide"`, `TablePager disabled`, `Cell wrap`)
could break. **No Vitest was run in this pass:** the coordinator paused every
run, single files included, while the lane runs the full suite. The checks below
are by code; the test evidence is the lane's full run on b7faeac7, which covers
the files named in "Runs owed".

| Defect | Fix | Re-check |
|---|---|---|
| D-4 | `size="wide"`: MUI `maxWidth={false}` + paper `maxWidth: 900`, `fullWidth`; columns 130 / 190 / 190 / rest, `minWidth` 720; users `UserLine truncate={false}` | ✅ The content area is 900 − 48 = 852 px. «Причина» gets 852 − 510 ≈ 342 px (≈ 326 px of text, about 45 characters a line, so a 500-character reason is ~11 lines). The users get 174 px of text, enough for «Системный Администратор» (≈ 155 px), and a longer name or the «неактивен» chip wraps instead of being cut. The date fits on one line. No sideways scroll while the window is at least ≈ 832 px (720 + 48 + 64 margin); below that the table scrolls, which is design § 3.4's rule for narrow widths. Matches gate-2. |
| D-5 | `Cell wrap` → `overflow-wrap: anywhere` on the reason cell | ✅ |
| D-6 | `useFocusInvalid()` after a user or reason refusal: first `[role="dialog"] [aria-invalid="true"]`, after the render that re-enables the fields | ✅ The dialog has one invalid field at a time, so "first" is the right one. The QA test is un-skipped. |
| D-7 | `useCallback` ref on «Закрыть» | ✅ Runs once on mount. QA test un-skipped. |
| D-8 | `RescueGate` unmounts the list on revoke, then `removeQueries` in an effect | ✅ No observer is left to re-create the read. QA test un-skipped. |
| D-9 | `TablePager disabled={refreshing}`; the range follows `shownPage` (the rows' page) | ✅ for the § 3.3 state. It introduces D-11 (below). |
| D-10 | page reset on the *settled* filter, as state adjusted during render | ✅ React throws that render away before commit, and TanStack fetches from the effect, so no `(old page, new filter)` or `(1, old filter)` request leaves. The developer's test asserts the exact request list. |

**Regressions of the kit additions (by code).**
- `DialogShell`: the map gained a key, and the `sx` branch for every other size still yields `undefined`. No code switches exhaustively on `size` (grep). `confirm` / `form` / `card` are unchanged.
- `TablePager` and `Cell`: both props default to `false` and add nothing when off. Every existing caller is unchanged.
- Showcase: `DialogsDemo` gained a wide window that uses `TableCard` / `Cell` / `TablePager`. `registry.test.ts` ("a component in one section only") reads the registry's metadata, not demo imports, so it should hold; the lane's run confirms.
- `ui-kit.md` gained the guide lines.

#### D-11: a disabled pager drops keyboard focus on a page change

- Severity: minor (accessibility; brought in by the D-9 fix)
- Steps: in a browser, focus «следующая страница» with the keyboard and press Enter.
- Expected / Actual: design § 3.3, "After a page change focus stays on the pager button". Actual: `disabled` puts the `disabled` attribute on the focused arrow while the page loads. The browser's focus fixup then moves focus to `body`, and nothing returns it, so the next Tab starts from the top of the page. jsdom does no focus fixup, so a component test cannot show this; judged from the code and the HTML focus rules. Fix options: `aria-disabled` and ignore the click while loading, or refocus the arrow once the rows arrive.
- Status: open — carried to gate-3

#### Runs owed (paused by the coordinator)

`src/features/organizations/pages/AllOrganizationsPage.qa.test.tsx` (now 10 + 5
of the developer's, none skipped), `src/shared/ui/dialog/DialogShell.test.tsx`,
`src/shared/ui/table/Cell.test.tsx`, `src/shared/ui/table/TablePager.test.tsx`,
`src/features/uikit/showcase/registry.test.ts`. The lane's full run on b7faeac7
covers them all; this verdict holds only if that run is green.

#### Verdict (pass 2)

blockers: 0 · majors: 0 · minors open: 1 (D-11, new) · result: pass,
conditional on the lane's full Vitest run on b7faeac7 being green. D-4..D-10
are fixed; D-11 is carried to gate-3 as a debt.
