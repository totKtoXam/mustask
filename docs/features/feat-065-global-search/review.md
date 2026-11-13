# Review: feat-065-global-search

## Round 1 — requirements

Reviewer: requirements-reviewer, clean context, 2026-11-13. Input: `spec.md` and `stories/US-1..US-6`. Checked against doc-016 (spec BR-TD-9, -10, -12..-17, -21, -22; design § 3), the queue lines feat-041, feat-061, feat-062, feat-063, feat-065, ADR-0015, -0030, -0032, -0036, -0039, -0040, feat-003 (BR-WS-9, -10) and feat-043's reading of BR-WS-9 for the organization's list. Code read: `PermissionCatalog`, `OrganizationRoleBundles` (and its test), `MemberView`/`UserRefView`, `UserDirectoryReadModel`, the frontend `addressModel.ts` menu table, `RichTextEditor/behaviour/keyboard.ts`, `WorkItemEndpoints.cs`, `Program.cs` request logging.

### Rule → story map

| Rule | Stories | Note |
|---|---|---|
| BR-GS-1 opening | US-1 | editor and modal exceptions added (R-1, R-18) |
| BR-GS-2 scope | US-1, US-3, US-4, US-5 | chip lifetime added (R-19) |
| BR-GS-3 fields | US-2, US-3 | users' membership states added (R-9) |
| BR-GS-4 sight first | US-4 | |
| BR-GS-5 no disclosure | US-1 (live region), US-4 | timing clause not testable to the microsecond, kept as a design constraint |
| BR-GS-6 not found | US-2, US-3, US-4, US-6 | sub-areas of a deactivated project added (R-13) |
| BR-GS-7 item matching | US-3, US-4 | examples corrected (R-8) |
| BR-GS-8 ranking | US-2, US-3 | tie-break made total (R-8) |
| BR-GS-9 limits, "more", show all | US-2, US-3, US-4 | list keyword difference stated (R-5) |
| BR-GS-10 query rules | US-2, US-1 (IME) | logging clause made testable (R-14) |
| BR-GS-11 rows | US-2, US-3 | work item row made unambiguous (R-21) |
| BR-GS-12 choosing | US-5 | fit rule aligned with BR-TD-9 (R-6); user with no items (R-10) |
| BR-GS-13 recent | US-6, US-4 | "opened" defined (R-12); failed resolution (R-11) |
| BR-GS-14 neighbours | none (scope rule) | wording vs BR-TD-21 (R-20) |
| BR-GS-15 failure | US-2, US-4, US-6 | 404 mid-session, recent failure (R-11) |
| BR-GS-16 accessibility | US-1 | width wording (R-16) |
| NFR volumes, query shape, load | US-3 (speed), US-6 (resolve) | 2-character feasibility (R-22) |
| NFR non-disclosure, tenant | US-4, US-6 | |

### R-1
Severity: major
Affects: BR-GS-1, US-1 "Typing a shortcut inside a text field of the page"
Problem: The merged rich-text editor already binds Ctrl/Cmd+K to its link command (`src/frontend/src/shared/ui/RichTextEditor/behaviour/keyboard.ts`, case 'k' → `openLink()`). The scenario required the palette to open from the description editor, which either breaks the editor's link shortcut or is unimplementable as written.
Required resolution: Decide which wins inside the editor.
Status: resolved
Resolution: BR-GS-1 now keeps Ctrl/Cmd+K as the editor's link command inside the editor (the magnifier still opens the palette); US-1 split into a plain-field scenario and an editor scenario. The human may reverse this at gate-1 (then the editor's link shortcut must move, a change to ADR-0039's editor).

### R-2
Severity: major
Affects: § Roles cast, US-3, US-4, US-6
Problem: The cast said Max sees every project because «Видим всей организации» is on. Under BR-TD-12 (c) that switch makes the projects visible to every member, Olga included, so every US-4 scenario that hides BRV, CHR, DLT, JUPITER and «Финансы» from Olga was false.
Required resolution: Give Max sight by another route and state the switch is off.
Status: resolved
Resolution: Cast now says every «Видим всей организации» is off and Max holds department grants on «Логистика» and «Финансы» (BR-TD-12 (a), BR-TD-13); US-4's cast line updated.

### R-3
Severity: major
Affects: § Roles, US-2 "Users need the membership read code", US-4 "Users"
Problem: `organizations.membership.read` is in all three organization role bundles (`OrganizationRoleBundlesTests`: true, true, true). No real user lacks it, and US-4 named Olga («Участник») as lacking it, which is false; both scenarios were untestable through the API.
Required resolution: State that every member gets the group today and test the gate with a caller lacking the code.
Status: resolved
Resolution: § Roles notes all bundles hold the code; both scenarios now name a caller without the code in a handler test.

### R-4
Severity: major
Affects: BR-GS-9, BR-GS-12, § Decomposition (`feat-NNN-search-palette` deps)
Problem: The user rows and «Все рабочие элементы по запросу» open the organization's «Рабочие элементы» list, whose sight (BR-TD-17) is owed by feat-062. The palette line depended on feat-041 only through the read lines, so it could ship a new road onto a list that does not yet hold the sight rule, against BR-GS-4's promise.
Required resolution: Make the palette line wait for feat-062, or drop the two links until it merges.
Status: resolved
Resolution: `feat-062-sight-in-work-views` added to the palette line's deps with the reason. The human may prefer the other option (ship without the two links); see the questions.

### R-5
Severity: major
Affects: BR-GS-9, BR-GS-7, US-3 "Show all"
Problem: The palette matches identifier prefixes and «ключ-номер» (BR-GS-7), while the list's keyword (feat-003 BR-WS-9 as feat-043 reads it) does neither and matches a bare number in every project. «Все рабочие элементы по запросу» could therefore show fewer items than the palette (e.g. «ALP-7» → none) or other ones («42»). Unstated, it reads as a bug at QA.
Required resolution: State the difference and its acceptance, or carry the palette's meaning to the list (a change to feat-003's line).
Status: resolved
Resolution: BR-GS-9 states the list keeps its own keyword meaning and may differ; US-3 gained the scenario "Show all uses the list's own keyword". Listed as a question for the human.

### R-6
Severity: major
Affects: BR-GS-12, BPMN node C1, US-5
Problem: BR-GS-12 said the levels below a chosen object "reset to «все»" and a work item leaves "the team untouched"; doc-016 BR-TD-9 and design § 3.3 reset only what no longer fits, and US-5's work item scenario resets an unfitting team. Choosing a team while a fitting project is chosen, or the department already chosen, had two answers.
Required resolution: Align with BR-TD-9.
Status: resolved
Resolution: BR-GS-12 and the BPMN now reset only unfitting values; a work item chooses like a project; a sub-area chooses its project and department (ADR-0032 § 5). US-5 gained "A choice that fits resets nothing". (doc-016's own BR-TD-21 and design § 3.1 carry the same "levels below reset" wording against its BR-TD-9; that is doc-016's, noted for feat-063, not fixed here.)

### R-7
Severity: minor
Affects: § Decomposition
Problem: `feat-NNN-cascading-context-selectors` already has its number: `feat-063-cascading-context-selectors` is queued.
Required resolution: Use the number.
Status: resolved
Resolution: Renamed in the table and the note below it.

### R-8
Severity: minor
Affects: BR-GS-7, BR-GS-8
Problem: (b) gave `msk-42` as an identifier prefix, although US-3 says the key-and-number form names one item only; the tier of the key-and-number form was unstated; the work-item tie-break "latest change then number" is not total across projects (numbers repeat).
Required resolution: Fix the example, assign tiers, make the tie-break total.
Status: resolved
Resolution: (b)'s examples are `MSK-BUG-4`, `msk-bug-4`, with "the key-and-number form has no prefix match"; (a) is tier 1, (b) tier 2; tie-break is latest change, then project key, then number descending.

### R-9
Severity: minor
Affects: BR-GS-3, US-2
Problem: Which people the users group finds was not stated: former members, disabled accounts (the directory keeps them, `UserRefView.State`).
Required resolution: State it.
Status: resolved
Resolution: Current members only; a disabled account is found and reads «(неактивен)» (feat-003 BR-WS-7). US-2 gained "Former and disabled members".

### R-10
Severity: minor
Affects: BR-GS-12, US-5
Problem: Choosing a person who is assigned no visible live item opens the list with an «Исполнитель» value that is none of its options; feat-003 BR-WS-10 then shows an empty list and a blank field with "a filter is on". The outcome was unstated and is a weak answer to "what is X working on".
Required resolution: State the outcome; the human may want something better (a question).
Status: resolved
Resolution: BR-GS-12 and US-5 "A user assigned nothing" state BR-WS-10's outcome. Raised as a downside of OQ-6 A.

### R-11
Severity: minor
Affects: BR-GS-13, BR-GS-15, US-4, US-6
Problem: Unstated: a failed recent-items resolution (would the client drop every id as "unresolved"?), and a 404 «Организация не найдена.» while the palette is open (a «Повторить» would loop).
Required resolution: State both.
Status: resolved
Resolution: Ids are dropped only after a successful resolution; a failed one shows BR-GS-15's message. A 404 shows its text without «Повторить». Scenarios added to US-4 and US-6.

### R-12
Severity: minor
Affects: BR-GS-13, US-6
Problem: "An object's own page" was undefined under ADR-0032's addresses: a project's «Канбан», the project's «Структура» node page (an area id), a card carrying `teamId`.
Required resolution: Define what each address records.
Status: resolved
Resolution: BR-GS-13 records the primary object of the address prefix; the project node page records the project; a card records the item only; `teamId`/`departmentId` record nothing. US-6 gained "Which page records what".

### R-13
Severity: minor
Affects: BR-GS-6, US-2
Problem: The sub-areas of a deactivated project were not listed among what is not found.
Required resolution: Add them.
Status: resolved
Resolution: Added to BR-GS-6 and to US-2's deactivated scenario.

### R-14
Severity: minor
Affects: BR-GS-10, NFR Privacy
Problem: "Never written to the server log" was not verifiable as worded: a query-string read lands in request lines and traces, and the edge proxy's access log is outside the code.
Required resolution: Bound the rule to what the application controls and give the design the transport choice.
Status: resolved
Resolution: The rule covers the application's logs and traces; the design keeps the text out of the logged request line; the edge proxy is named outside the rule.

### R-15
Severity: minor
Affects: US-2 "Group order follows the best match"
Problem: The scenario used department «Склад», which the common cast puts in «Другая», where Ann is no member.
Required resolution: Use objects of «Ромашка».
Status: resolved
Resolution: Now «Снабжение» (department) and «Снабжение-2» (team) in «Ромашка».

### R-16
Severity: minor
Affects: BR-GS-16
Problem: "Works from 640 CSS px (full width below 600)": the full-width case lies below the supported floor and could never be tested.
Required resolution: Make the narrow rule reachable.
Status: resolved
Resolution: Full width on a viewport narrower than the dialog; the breakpoint is the design's.

### R-17
Severity: minor
Affects: US-6 "Clear"
Problem: "Emptied after confirmation by Enter or a click" read both as a confirmation step and as the activation itself.
Required resolution: Say which.
Status: resolved
Resolution: No confirmation step; Enter or a click empties the list.

### R-18
Severity: minor
Affects: BR-GS-1, US-1
Problem: Ctrl+K while another modal dialog is open (a form) was unspecified: two stacked focus traps.
Required resolution: State it.
Status: resolved
Resolution: The shortcut does nothing while another modal dialog is open; scenario added.

### R-19
Severity: minor
Affects: BR-GS-2, US-1
Problem: How long the chip's choice lasts was unstated.
Required resolution: State it.
Status: resolved
Resolution: Until the palette closes; every opening starts from the default.

### R-20
Severity: minor
Affects: BR-GS-14
Problem: "Shares their name match" contradicted BR-TD-21, which matches from the first character and states no «ё» folding or literal-character rule.
Required resolution: Share the fields, not the rule.
Status: resolved
Resolution: BR-GS-14 now shares the sight sets and the fields; the palette's match rules are its own and change neither neighbour.

### R-21
Severity: minor
Affects: BR-GS-11
Problem: The work item row listed identifier and title in the grey secondary line, leaving the primary text undefined.
Required resolution: Say which is which.
Status: resolved
Resolution: Primary text is identifier and title; the secondary line is state and project.

### R-22
Severity: minor
Affects: NFR Volumes, US-3 "Speed", OQ-2
Problem: A trigram index cannot narrow a 2-character pattern, so a 2-character title query at 100 000 items is a scan of the visible projects' items on every pause; the p95 300 ms target has no fallback if the volume test fails.
Required resolution: The design (and the ADR of OQ-2) shows the plan, or proposes a 3-character floor for titles only.
Status: accepted-risk
Resolution: Left to the design and the volume test of `feat-NNN-search-work-items-read`; the line's gate decides the fallback.

### R-23
Severity: minor
Affects: § Decomposition `feat-NNN-search-palette`
Problem: The panel's kit-first rule (`src/frontend/AGENTS.md`) puts a search-dialog/combobox pattern into the UI kit before the screen; that was not in the line and presses its M size.
Required resolution: Name it in the line.
Status: resolved
Resolution: Added to the line's task; size kept M, to be re-checked by its own route.

### R-24
Severity: minor
Affects: US-4 "Not a member"
Problem: "Ann is no member of «Другая» (an instance administrator too)" read as Ann being an instance administrator.
Required resolution: Name two callers.
Status: resolved
Resolution: Ann and an instance administrator, neither a member, for the search and the recent-items read.

### Verified claims

- No per-user page: the menu table (`addressModel.ts`) has «Участники» only; no user route. Holds.
- The organization's «Рабочие элементы» list takes `assigneeId` and `search` (`WorkItemEndpoints.cs`), so BR-GS-12's and BR-GS-9's targets exist.
- «Участники» shows email to every member: `MemberView.User` carries `Email`, gated by `organizations.membership.read`, which every bundle holds. Holds.
- The user directory already stores folded `SearchName`/`SearchEmail` and matches them with `LIKE` (`UserDirectoryReadModel`): useful to the design of OQ-2 for the users group.
- ADR-0015 names full-text search as needing a new ADR; ADR-0036 has `http.too_many_requests`, `Retry-After`, 3 tries; ADR-0030 B2's entry points hold.

### Questions for the human (gate-1)

Besides OQ-1..OQ-6 as written:

- R-1: inside the description editor, Ctrl/Cmd+K stays the editor's link command (recommended, now in BR-GS-1), or the palette wins and the editor's link shortcut moves?
- R-4: the palette line waits for feat-062 (now in the decomposition), or ships before it without the user rows' target and «Все рабочие элементы по запросу»?
- R-5: accept that «Все рабочие элементы по запросу» applies the list's own keyword (may differ from the palette), or widen feat-003's keyword to the palette's identifier grammar in a separate line?
- R-10 (with OQ-6): choosing a person assigned nothing opens an empty list with a blank «Исполнитель» field; acceptable for this cut?
