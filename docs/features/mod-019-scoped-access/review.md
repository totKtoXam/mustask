# Review — mod-019-scoped-access

## Round 1 — requirements

Reviewer: requirements-reviewer, clean context, 2026-11-06. Input: `spec.md`, `stories/US-1..US-6`, checked against
doc-012 (BR-MX-1, -3, -6, -20..-26; stories US-1, US-3, US-5, US-7, US-10), ADR-0029, ADR-0031, ADR-0033, mod-018
(spec BR-AW-5, gate-1 answers OQ-3/5/7 A, review m-2, m-3, TC-30, status Debt), mod-016 api-contract §§ 0.1, 0.2, 1.1,
1.5-1.6, mod-006 BR-M4-27, feat-039 BR-PB-5, feat-043 BR-NV-1/-3/-14/-18, feat-048 § Roles, `docs/backlog.md` (tech-026 OP-7).

Verdict: **needs-human-decision**. Three blockers and eight majors go back to the business analyst (too large for
reviewer edits); OQ-1..OQ-6 and the new OQ-7 are the human's at gate-1. Keycloak: the spec keeps grants in the MUSTASK
database and does not touch the realm (BR-SA-8, NFR Migration), as it should. E2E: no criterion needs E2E; every
scenario can be an API integration test or a component test.

### Rule → story mapping

| Rule / owed clause | Covered by | Gap |
|---|---|---|
| BR-MX-20 grants, lifecycle | BR-SA-1..-3; US-1 | concurrency edges (R-16) |
| BR-MX-22 inheritance (as ADR-0031 rule 7) | BR-SA-5, -6; US-2 | nested sub-areas wording (R-15) |
| BR-MX-23 effective permissions | BR-SA-8; US-3 | fixture makes outcomes wrong (R-1) |
| BR-MX-26 people move | BR-SA-3; US-1 | outcome wrong under the fixture (R-1) |
| BR-MX-3 who switches / who moves | BR-SA-7; US-2 | move by a department manager missing (R-7); scenario order wrong (R-2) |
| BR-MX-6 who links (`*.link`) | Roles text only | no rule, no scenario (R-6) |
| BR-MX-24 answer order | BR-SA-9; US-3 | the item case contradicts "today's order" (R-4) |
| ADR-0029 § 10 served per object | BR-SA-11; US-4 | the tree and pickers (R-9); panel consumers (R-8) |
| ADR-0029 § 12, tech-026 OP-7 | BR-SA-14; US-6 | matrix definition impossible under OQ-1 A (R-10); ADR-0033 mirror (R-19, OQ-7) |
| mod-006 BR-M4-27 / BR-SA-10 | US-3 | aggregate views of feat-048 (R-7) |
| mod-018 m-2, m-3 / DEF-4, TC-30 | BR-SA-12, -13 | m-2, m-3 already closed in mod-018; TC-30 fix dead under OQ-4 A (R-11) |
| doc-012 US-1 owed clauses (manager moves, "turned on again by…", nobody else writes) | US-2 in part | R-2, R-7 |
| doc-012 US-3 owed clauses (links under grants) | none | R-6 |
| doc-012 US-5 | US-1..US-3 | R-1 |
| doc-012 US-7 owed clauses (who reorders; shared board moves) | US-3 in part | R-7 |
| doc-012 US-10 owed clauses (sections by object codes, link multiselect) | US-4, US-5 | R-5, R-6 |

### R-1
Severity: blocker
Affects: US-1 fixture; US-1 "A role is changed, then removed", "People move without moving work"; US-2 "A department grant reaches…", "Switching a project node off…", "Switching back on…", "A move changes what reaches a project", "No link and no team carries access"; US-3 "A grant on a project nothing links", "A grant removal applies from the next check"; US-4 "Controls follow the grant", "The server decides"
Problem: the fixture makes Olga, Pavel, Irina and Kirill organization «Участник» "unless said". Every project is «Организация», so a «Участник» already holds `workitems.workitem.write`, `workitems.link.write`, `boards.projectbacklog.reorder` on every project and `boards.backlog.reorder` on every team (ADR-0029 § 4, BR-SA-10: "every `member` keeps it"). Codes are only added. So the listed scenarios assert 403s that the spec's own rules make impossible: Pavel's change to D (US-2) is kept; Olga's change after her grant goes to «Чтение» (US-1) is kept; Olga's reorder of MARS's backlog after leaving it (US-1) is kept; US-3's "Z" and "removal in flight" cases pass whatever the grant does, so they test nothing; US-4's Olga already has the controls. doc-012 US-5 avoided this by making U a «Читатель».
Required resolution: make the fixture default «Читатель» and name «Участник» only where a scenario needs it (US-3 "Codes are only added", "The project backlog's order"). Re-read every scenario's expected answer against ADR-0029 § 4 for the role it now has.
Status: closed
Resolution: round 2. The fixture defaults to «Читатель», with Mira the one «Участник». I re-checked every 403 in US-1..US-4 against the bundles, and all hold.

### R-2
Severity: blocker
Affects: US-2 "Who may switch, and the parent's code"; BR-SA-7
Problem: Pavel (only «Управление» on «Логистика») switches off A's node "and then «Платформа»", and "both are kept". Once A's node is off, nothing from «Логистика» reaches A's node or «Платформа» below it (BR-SA-5), so Pavel holds `organizations.area.write` on neither, and the second switch must answer 403. The scenario contradicts BR-SA-5 and BR-SA-7. It also leaves out doc-012 US-1's owed clause: after the switch, only the admin (or a holder of «Управление» on the node and on its parent) turns it on again.
Required resolution: switch «Платформа» first, then A's node, both kept; then add "Pavel's attempt to switch A's node back on answers 403; the admin's is kept".
Status: closed
Resolution: round 2. «Платформа» is switched first, then A's node. Pavel's switch back is 403 and Anna's is kept.

### R-3
Severity: blocker
Affects: US-3 "Split codes"; US-4 "Pickers offer what the caller may do"; BR-SA-12; spec § Roles (department «Управление» "place", "sub-areas it reaches")
Problem: the human's mod-018 gate-1 answers (2026-10-28) and ADR-0031 rule 5 forbid what these scenarios expect. Departments do not nest (OQ-3 A). Sub-areas sit only below a project node (OQ-7 A). A project node is created and deleted only with its project (OQ-5 A, `root_is_fixed`). So nothing can be created under «Логистика» except a project, and creating a project needs `organizations.project.create`. Yet US-3 has Pavel "create a child area under «Логистика», rename it and delete it", each kept, and US-4 offers him "the create dialog for a child of «Логистика»". A department is never moved under anything either (mod-018 api-contract § 3: a department moved to the top is a no-op), so "the department's … place" in § Roles is not a power.
Required resolution: rewrite both scenarios on a sub-area. Pavel creates, renames and deletes a sub-area under A's node, each kept; he creates a department or a project, each 403. In US-4 Pavel is offered the sub-area create dialog on A's node and on «Платформа», and none on «Логистика». BR-SA-12: "for a sub-area, by `.area.write` on its parent (a project node or a sub-area)". § Roles: drop "place" for a department; "the sub-areas below the project nodes it reaches".
Status: closed
Resolution: round 2. The child-area cases are now sub-areas under a project node (US-8, US-4). "Place" is dropped for a department.

### R-4
Severity: major
Affects: US-3 "The answer order stays today's"; BR-SA-9
Problem: the last line, "the same «Читатель» changing an item id that names nothing in A gets «Рабочий элемент не найден.», not 403", is the new order of ADR-0029 § 8 and doc-012 US-5. It is not today's. In mod-016 api-contract § 0.1, row 3 (403) precedes row 4 (a nested id's 404), and `RetrofitAccessTests.Order_AViewersWriteToAMissingProject_Is403_…` pins that. ADR-0029 § 8's last paragraph says this line keeps today's order until feat-041. As written, the story makes the developer change the merged contract early, which BR-SA-9 forbids.
Required resolution: expect 403 «Недостаточно прав» for the «Читатель»'s write to a missing item in A. Add that a «Участник»'s write to the same id answers «Рабочий элемент не найден.». State that `RetrofitAccessTests` is unchanged until feat-041.
Status: closed
Resolution: round 2. The viewer gets 403 and the member gets «Рабочий элемент не найден.». BR-SA-9 cites mod-016 § 0.1.

### R-5
Severity: major
Affects: US-4 "A section is shown by the object's codes", "The menu is built from the object's codes"
Problem: the stories treat «Настройки» as a section of a project's page ("Kirill sees «Настройки»", "Olga opening A's «Настройки»", "Vera sees … no «Настройки»"). In the merged feat-043 (BR-NV-18), «Настройки» is the derived last entry, always present, with the «Организация» group. A holder of the write code gets one group per chosen kind, «Проект» / «Департамент» / «Команда» → «Параметры», and "No object has a «Настройки» tab". As written, the criteria fail against the merged panel or push the developer to rebuild it.
Required resolution: say "Kirill sees the «Проект» group («Параметры») in «Настройки»; Olga does not, and opening A's `…/settings` by address shows «Доступ ограничен»". For Vera: "the «Настройки» entry shows only the «Организация» group". Keep "no entry is shown disabled" (BR-NV-1).
Status: closed
Resolution: round 2. US-4 now uses the «Проект» group («Параметры») and the `…/settings` address. Vera sees the «Организация» group only.

### R-6
Severity: major
Affects: spec § Roles, § Business rules (missing rule); US-1/US-3/US-5 (missing scenarios); doc-012 US-3 and US-10 owed clauses
Problem: the link codes (`organizations.area.link`, `.project.link`, `.team.link`) change hands in this line. Until now only the admin held them, and from here on «Управление» on each end does. No rule or scenario says how. doc-012 marks six clauses "owed by `mod-019-scoped-access`":
- US-3: the multiselect offers only teams the caller manages;
- US-3: «Управление» on one end creates no link and removes no project–team link;
- US-3: a department–team link ends on either end;
- US-3: a link gives no power over its other end;
- US-3: the unlink destination VENUS is offered whether or not the manager holds a grant on it;
- US-10: the multiselect and unlink row commands.

Under the typed tree only two link kinds remain, project–team and department–team (ADR-0031 rule 4). Nothing pins either for a non-admin.
Required resolution: add BR-SA-15 "Links under grants". Creating either kind needs the end's `*.link` on both ends. Removing a project–team link needs both. Removing a department–team link needs either. The 403 on the addressed end comes before the other end's not-found (ADR-0029 § 7). Pickers offer only the objects whose other-end code the caller holds. Carry the six owed clauses as scenarios, rewritten for departments.
Status: closed
Resolution: round 2. BR-SA-15 and US-7 carry the six owed clauses for both link kinds.

### R-7
Severity: major
Affects: US-2, US-3, US-4; doc-012 US-1, US-7 owed clauses; feat-048
Problem: the spec drops several owed clauses and changed surfaces.
- A move by a non-admin is never specified. BR-MX-3 requires «Управление» on the node, its current parent and its new parent. Under OQ-1 A, this means a department manager moves a project node between two departments they both manage. A project «Управление» holder never moves their own project (feat-043 BR-NV-11 places it from «Общие»).
- doc-012 US-7's "who reorders which backlog" clause (team planner versus project «Изменение» versus viewer) is not carried.
- Its "a shared board moves only the cards the user may change" clause is not carried either.
- The merged feat-048 grouped backlogs and per-project board lanes at organization and department level move rows and cards "where the project's own board or backlog allows" (feat-048 § Roles, the role bundle until mod-019). They now have to follow per-project codes, and no scenario says so.
Required resolution: in US-2, add a move scenario. Pavel holds «Управление» on «Логистика» and on «Финансы» and moves A's node between them: kept. With «Логистика» only: 403. A project «Управление» holder: 403, with no placement control in «Общие». In US-3, carry doc-012 US-7's two clauses. In US-4, add "on a department's grouped «Бэклог» and board, Olga («Читатель», «Изменение» on A) moves rows and cards in A's group and lane only; a direct move in B's answers 403".
Status: closed
Resolution: round 2. The moves are in US-8, doc-012 US-7's two clauses are in US-3, and the grouped views are in US-4.

### R-8
Severity: major
Affects: BR-SA-11; US-4; § Decomposition (feat-NNN-codes-served-per-object)
Problem: after BR-SA-11, the organization answer (mod-016 § 1.1) carries organization-scope codes only. It drops `workitems.*`, `boards.*`, `organizations.project.read/write`, `organizations.team.write` and `organizations.area.write`. Today every panel surface reads those from the organization answer: the kanban and backlog (mod-006, feat-039, feat-040, feat-048), card checklists (feat-032), parent links (feat-001), tree editing (feat-027), the item form, and «Структура». Any surface not switched to per-object codes silently loses its controls for every «Участник». That is a regression no scenario would catch. The spec lists neither the consumers nor a guard.
Required resolution: add to BR-SA-11 "every panel consumer of a non-organization code reads it from the object's served codes", with the list of surfaces handed to the design. Add a US-4 scenario: "a «Участник» with no grant is offered on every surface the controls offered today", run as component tests per surface. Add a unit or lint test that fails when a non-O code is read from the organization answer.
Status: closed
Resolution: round 2. BR-SA-18 is added, plus US-4's per-surface component tests and a unit guard.

### R-9
Severity: major
Affects: BR-SA-11, BR-SA-12; US-4 "Pickers"; US-5 "The switch control sits on the node's page"
Problem: BR-SA-11 serves codes on a read of one department, project or team, and per project on a multi-project view. «Структура» reads the whole tree (1 000 nodes, within 2 s, feat-043 BR-NV-9). The tree's row commands (create a sub-area, rename, move, switch, delete) and the move-destination pickers need `organizations.area.write` per node. ADR-0029 § 10 does not cover a tree, and the spec does not say how the tree carries them. The likely result is a per-node read or a full matrix, which § 10 forbids.
Required resolution: state that the tree read carries, once, the ids of the nodes where the caller holds `organizations.area.write`, from the set form, with the tree's 2 s and a pinned statement count still holding. The pickers offer only those ids. A node page carries its own codes as any object read.
Status: closed
Resolution: round 2. BR-SA-11 and US-4: the tree read carries, once, the ids of the nodes where the caller holds `area.write`.

### R-10
Severity: major
Affects: BR-SA-4 (OQ-1 A); US-1 "A grant of the wrong scope is refused"; US-6 "The matrix is generated, not written"
Problem: (a) Under OQ-1 A, a department grant inherited onto a project node or a sub-area still brings `organizations.area.grant` there. ADR-0031 rule 7 itself names grants "held on the node". Yet no scenario says what a request to give an area role on a project node or a sub-area answers. (b) US-6 takes "each of the 11 roles held on the addressed object". That is impossible for area roles on a project, team or sub-area route, and for project or team roles on an area route.
Required resolution: (a) add to US-1: an area role given on a project node or a sub-area id answers the catalogue's type refusal, the exact code being the contract's (as mod-018's `type_not_allowed`), and stores nothing. (b) Rewrite US-6. Organization roles come through the membership. A role of the route's own scope is held on the addressed object. Area roles are held on the department above (switch on, switch off) and on another department. A project or team role is held on another object of its scope.
Status: closed (round 3: US-6 holds project roles on the node and sub-area routes, and keeps only the exclusions for grants that cannot exist)
Resolution: round 2. (a) is closed by US-1 and BR-SA-4. (b) is half fixed: US-6 now says "no cell holds … a project or team role on an area route". But OQ-3 B makes the project role «Управление» hold `area.write` on its node and sub-areas, so the matrix would leave out the very cells the human just added. See R-23.

### R-11
Severity: major
Affects: BR-SA-12, BR-SA-13; US-4 "Pickers" last line; § Context ("takes over m-2, m-3 / DEF-4")
Problem: (a) Under OQ-4 A, only the admin holds `organizations.project.create`, and the admin always has the top level as a place. So "«Новый проект» … disabled with a reason without a place" can never fire, and US-4 cannot be tested. Under feat-043 (§ Roles, BR-NV-14), a non-admin has no create command at all. (b) m-2 and m-3 are already closed in mod-018. Per mod-018 review.md, round 3 "Old debt re-checked": `MoveAreaHandler.cs:91` re-decides on the locked row, and the contract sentence was fixed as TC-15. `AreasPage`/`AreaPage` read `areaCreate`. The spec presents them as work to do.
Required resolution: (a) the TC-30 fix becomes "«Новый проект» is offered only with `organizations.project.create`, and absent otherwise; its «Расположение» offers the top level and the departments where the caller holds `organizations.area.write`, never empty". Drop "disabled with a reason". (b) Reword m-2 and m-3 as "kept under the split bundles", proved by regression tests (US-2's locked-row scenario stays, now for the switch route, which is new).
Status: closed
Resolution: round 2. Under OQ-4 B the place list now decides the command, and m-2 and m-3 are reworded as regression tests. Two leftovers are tracked separately: the BR-NV-1 citation (R-25) and the deleted-place answer (R-22).

### R-12
Severity: minor
Affects: OQ-6; US-2 "A switch off asks first"
Problem: "names how many users lose access" cannot be checked as written. Every member keeps the organization role's access to a «Организация» project, so nobody loses access. What they lose is the extra codes. Users with their own grant on the project, and admins, lose nothing.
Required resolution: define the count as "the distinct holders of grants held on an ancestor that reach the node now and will not after, organization admins excluded". Add an extra line when the actor is among them («Вы потеряете управление этим узлом»; copy is the design's).
Status: closed
Resolution: round 2. BR-SA-7 and US-2 define the count as asked, add the lockout line, and give the example count of 2.

### R-13
Severity: minor
Affects: US-4 "Re-reads happen on the triggers"
Problem: "When Olga's own grant changes" cannot be tested when someone else changes it: the panel has no push channel.
Required resolution: "when the caller writes a grant on the object (their own included), the panel re-reads the object's codes; a change by someone else shows at the next trigger (refocus, a 403 or 404, a page load)".
Status: closed
Resolution: round 2. The US-4 triggers are reworded as asked.

### R-14
Severity: minor
Affects: § Context; author's flagged points 1 and 2
Problem: point 1 is correct. ADR-0031 rule 7 supersedes ADR-0029 § 6's link-based reach. But ADR-0031's header supersedes only § 6, while three other sentences still say "linked": ADR-0029 § 4's note, § 7 "Cost" ("the ancestors of its linked areas") and § 12's matrix ("an area linked directly"). An implementer following § 12 literally builds a case that cannot exist. Point 2 is correct with a caveat. Under OQ-1 A, the matrix's "Area" roles are department-held grants. Their area-scope codes still apply on every node they reach (the project node and the sub-areas), so the resource scope must stay `Area` and not become `Department`.
Required resolution: one line in § Context: "ADR-0029 §§ 4, 7 and 12 are read through ADR-0031 rule 7: 'linked area' means the project's node and its ancestors". One line in § Roles: "«Департамент» is the UI name; the stored scope and `ResourceScope` stay `Area`".
Status: closed
Resolution: round 2. The Context line and the Roles line are both added.

### R-15
Severity: minor
Affects: BR-SA-6
Problem: (a) "On a sub-area it decides whether the grants reaching its project node reach it" is wrong for a sub-area under a sub-area (depth up to 10). It is the parent that matters. (b) "On a department … the stored value matters once the node is moved under a department" cannot happen to a department, which never nests (mod-018 OQ-3 A).
Required resolution: (a) say "its parent". (b) Say a department's stored value never takes effect (see OQ-2).
Status: closed
Resolution: round 2. BR-SA-5 and -6 now say "its parent", and a department's value is stored, never shown and never effective. BR-SA-5 still lacks OQ-3 B's project-grant path (R-23).

### R-16
Severity: minor
Affects: BR-SA-2, BR-SA-3
Problem: four edge cases are unstated.
- Two managers removing each other's grants at once both succeed (ADR-0029 § 7). This differs from mod-016 §§ 1.5-1.6, where the caller is re-judged under the lock, and an implementer may copy that pattern.
- A manager may remove or downgrade their own grant.
- A grant to an organization admin is allowed, and it is kept when the admin is demoted.
- A grant write is judged at check time.
Required resolution: add these four sentences to BR-SA-2/-3, plus one US-1 scenario for the mutual removal.
Status: closed
Resolution: round 2. BR-SA-2 has the four sentences and US-1 has the edge-case scenario. Self-grant is not covered; see R-24.

### R-17
Severity: minor
Affects: US-1 fixture and "Only an active member…"; US-2 "Switching a project node off…"; US-5 first scenario
Problem: "D" names both a top-level project node and a removed user. "Pavel is shown no grant control on B" passes whatever the switch does, because a department bundle never holds `organizations.project.grant`. The US-5 title "direct and inherited grants" does not fit a department, which has nothing above it.
Required resolution: rename the user, for example Denis. Replace the vacuous line with "Pavel no longer appears as inherited in B's «Участники и доступ»". Retitle the US-5 scenario "A department's «Участники» lists its grants".
Status: closed
Resolution: round 2. The removed user is now Denis, B gets an inherited line, and the US-5 title is changed.

### R-18
Severity: minor
Affects: spec (no "Breaks" section)
Problem: the line breaks several merged contracts: mod-016 api-contract § 0.2 and § 1.1, mod-006 BR-M4-27, feat-039 BR-PB-5, feat-048 § Roles, feat-043 BR-NV-3 / § Roles ("until mod-019"), and mod-018 BR-AW-5. AGENTS.md and ADR-0029 Consequences (P-10) require a note in each owner's status.md. The spec does not list them, so the notes will be missed.
Required resolution: add § Breaks, one row per owner, with the note each gets.
Status: closed
Resolution: round 2. § Breaks lists every owner, mod-018 api-contract § 4 included.

### R-19
Severity: minor
Affects: US-6 "The bundles are a contract"; BR-SA-14
Problem: "the realm contract test still compares instance codes only" predates ADR-0033. The realm now holds four client roles, mapped to codes by a table in code. That table's hand mirror in `src/frontend/src/shared/api/tokenClaims.ts` waits for "the machine-readable contract that `mod-019` takes over" (ADR-0033 Consequences; tech-024 D-6). The spec does not say whether that mirror is in or out.
Required resolution: reword the sentence to ADR-0033's terms, and state the scope per OQ-7.
Status: closed (round 3: BR-SA-14 and US-6 say the realm holds four client roles and no code; the panel imports the contract at build time)
Resolution: round 2. The OQ-7 A scope is now in BR-SA-14 and US-6. But US-6 still says "the realm contract test compares instance codes only", while under ADR-0033 the realm holds four client roles and no codes. Reword it to "the realm contract test compares the four client roles; no code, organization or instance, is in the realm or the token". Also state that `tokenClaims.ts` is runtime code: the panel imports the contract at build time, not only in tests.

### R-20
Severity: minor
Affects: NFR Performance; US-3 "A single-item read stays fast"; US-5 "The page works at a long list"
Problem: "within 500 ms" and "within 2 s" lack a test form, so CI timing noise makes them flaky or unchecked. "A number of statements that does not grow" needs a pinned count.
Required resolution: "an integration test on a seeded database pins the statement count of the check (equal at 10 and at 10 000 grants, at depth 1 and 10) and asserts the time on that seed". The 2 s list is a component test on a 200-row fake plus the read's pinned count. No E2E.
Status: closed
Resolution: round 2. The NFR, US-3 and US-5 now specify pinned counts, a timing on the seed and component tests.

### R-21
Severity: minor
Affects: US-1, US-5 (grant lists and picker)
Problem: the grant list's order, the inactive and unknown users, and the picker's limits are not stated. mod-016 §§ 0.4-0.5 define them for members (UserRef, the `ru` collation order, 2-character minimum, 20 results, `moreMatched`).
Required resolution: "grant lists and the grantee picker follow mod-016 §§ 0.4-0.5", plus one US-5 line for an inactive grantee («неактивен»).
Status: closed
Resolution: round 2. US-1 and US-5 cite mod-016 §§ 0.4-0.5, and US-1 has the inactive and unknown lines.

### Opinion on the open questions

- **OQ-1: A, agree.** It matches feat-043's menus and avoids two lists that widen the same items. Widening later (B) needs no migration, since the grant is keyed on the area id and the type rule sits in the domain. Conditions: R-10 (the answer on a non-department node, the US-6 matrix) and R-14 (the scope stays `Area`).
- **OQ-2: A for project nodes and sub-areas, but no control on a department** (a variant of A and C: the column stays, nothing is migrated, the department page shows no switch). A department never nests (mod-018 OQ-3 A), so its value can never take effect, and a control "with a hint" is permanent noise. A top-level project node keeps its control and hint for the admin: it can be moved under a department, and this keeps doc-012 US-1's top-level clause.
- **OQ-3: A for this line, with a follow-up line queued for B.** A keeps the ADR as accepted. It has a real cost, though: a top-level project's sub-areas, and its item «Область» classification, are the admin's alone. The human should see that cost. B is an ADR amendment and belongs in its own line.
- **OQ-4: A, agree.** It also makes R-11's fix simple: «Новый проект» is offered only with `project.create`.
- **OQ-5: A, agree.** The human confirmed L. Under B, the panel's consumer switch (R-8) would land lines after the codes leave the organization answer, and members would lose their controls in between.
- **OQ-6: A, with R-12's definition.** Confirm with the count of grant holders who stop being reached, plus the self-lockout line. "Users who lose access" would be untrue for «Организация» projects.

### New open question for gate-1

- [ ] **OQ-7 Does the bundle contract also carry the instance role → code table (ADR-0033, tech-024 D-6)?**
  - (A, recommended) Yes. One more section in the same `contracts/` file, and the panel test reads it in place of the hand mirror in `tokenClaims.ts`. It is the same mechanism and closes D-6 for about one test. Cost: slightly wider scope.
  - (B) No. Only organization bundles (OP-7 as worded), and the instance table goes to a follow-up line. The scope stays minimal, but the mirror stays hand-kept, and ADR-0033's "mod-019 takes over" stays unmet.

## Author's response — round 1 fix (business-analyst, 2026-11-06)

The human's answers (status.md Log): OQ-1 A, OQ-2 reviewer's variant, OQ-3 B, OQ-4 B, OQ-5 A, OQ-6 reviewer's A, OQ-7 A. All 21 findings are accepted and fixed in `spec.md` and the stories; none is rejected. Status fields above stay `open` for the reviewer to verify.

| R | Fix |
|---|---|
| R-1 | US-1 fixture: every user a «Читатель», Mira the one «Участник»; every scenario's answer re-read against the bundles (US-2, US-3, US-4) |
| R-2 | US-2 "Who may switch": «Платформа» first, then A's node; Pavel's switch back is 403, Anna's kept; Kirill's 403 on the parent |
| R-3 | Child-area scenarios rewritten on sub-areas under a project node (new US-8, US-4); BR-SA-12; Roles drop "place" for a department |
| R-4 | US-3 "answer order": viewer 403 for a missing item, member «Рабочий элемент не найден.»; `RetrofitAccessTests` unchanged; BR-SA-9 |
| R-5 | US-4 uses the «Проект» group («Параметры») of «Настройки»; Vera sees the «Организация» group only |
| R-6 | BR-SA-15 and new US-7 carry the six owed clauses for the two link kinds that remain |
| R-7 | Moves by managers in US-8; doc-012 US-7's two clauses in US-3; the grouped views in US-4 |
| R-8 | BR-SA-18, US-4 "Every panel consumer" with a unit guard |
| R-9 | BR-SA-11 and US-4: the tree read carries once the ids with `area.write` |
| R-10 | US-1 type-refusal scenario and BR-SA-4; US-6 matrix cells made realizable |
| R-11 | BR-SA-12: absent, never disabled; "empty" only by a race (re-read on 403); m-2 and m-3 reworded as kept under the split bundles, regression tests, new for the switch route. With OQ-4 B the place list is non-empty for a department manager; with no place the command is absent |
| R-12 | BR-SA-7 and US-2: the count defined as the reviewer asks, plus the lockout line |
| R-13 | US-4 re-read triggers reworded (own write, 403/404, refocus, load) |
| R-14 | Context line and Roles line added |
| R-15 | BR-SA-5/-6: "its parent"; a department's value never takes effect |
| R-16 | The four sentences in BR-SA-2, the mutual-removal scenario in US-1 |
| R-17 | Denis for the removed user; B's inherited line instead of the vacuous one; US-5 titles |
| R-18 | § Breaks added to the spec |
| R-19 | Reworded to ADR-0033 terms; instance table in the contract (OQ-7 A) in US-6 and BR-SA-14 |
| R-20 | NFR and US-3, US-5: pinned statement counts, time asserted on the seed, component tests, no E2E |
| R-21 | Lists and picker follow mod-016 §§ 0.4-0.5; «неактивен» line in US-1 |

**New for the human:** OQ-8 (what a department manager holds on a project it creates). OQ-3 B and OQ-4 B each need a new ADR amending ADR-0029 § 4 (BR-SA-16, -17), written at the backend stage and accepted at gate-3.

### Author's response — round 2 fix (business-analyst, 2026-11-06)

OQ-8 A and OQ-9 B are the human's answers (spec § Open questions). All round-2 findings are accepted and fixed; none rejected.

| R | What changed |
|---|---|
| R-10 | BR-SA-5 states the project-grant path; US-6 gains the cells for project roles on the node and sub-area routes and keeps only the impossible exclusions (area role on a non-department, team role off its scope) |
| R-19 | BR-SA-14 and US-6: the realm test compares the four client roles, no code is in the realm or the token; `tokenClaims.ts` is runtime code imported at build time |
| R-22 | The recommended fix, which keeps mod-018's path: `POST /projects` is an inventory exception ("place in the body, step 0"); a non-admin gets 403 for any lacking, unknown, deleted, foreign or non-department place, the admin «Область не найдена.»; BR-SA-17, US-4, US-6, US-8 and § Breaks aligned |
| R-23 | BR-SA-5 gains the reach rule; BR-SA-16 names ADR-0029 §§ 4, 6, 7, 12 and the delivery bullet and requires the ADR `proposed` in plan-backend before any code, accepted at gate-3; § Breaks row updated |
| R-24 | OQ-9 B as BR-SA-19, replacing BR-SA-2's "downgrade" clause; US-1 scenario; US-2 confirmation line. It closes the escalation: no self-grant. Residual, accepted: two managers granting each other, visible in the giver column |
| R-25 | BR-SA-12 cites feat-043 § Roles and BR-NV-14; the submit answer is R-22's |
| R-26 | US-8 fixture: "created, renamed and deleted with its project, moved by the area move"; Kirill moves «Тесты» under «Платформа» |

## Round 2 — requirements (delta)

Reviewer: requirements-reviewer, 2026-11-06. Delta read of the open findings and the changed sections: spec.md as it now
stands, US-1..US-6 as changed, and the new US-7 and US-8. No Bash in this session, so I read the files rather than
`git diff 9b1cf430`. I re-read no unchanged source, apart from feat-043 BR-NV-1/-14 and the ADR README's Process section.

Gate-1 answers applied, as checked: OQ-1 A, OQ-2 (the reviewer's variant), OQ-3 B, OQ-4 B, OQ-5 A, OQ-6 A (with R-12),
OQ-7 A and OQ-8 A. All are recorded in spec § Open questions.

**Statuses:**
- R-1..R-9, R-11..R-18, R-20 and R-21 are closed, each with a one-line reason above.
- R-10 is reopened (major, with R-23).
- R-19 is reopened (minor).
- R-22..R-26 are new.

### R-22
Severity: major
Affects: BR-SA-17, BR-SA-14, BR-SA-9; US-8 "The place decides"; US-4 "The create commands…"; mod-018 api-contract § 4 (`POST /projects`)
Problem: under BR-SA-17, `POST …/organizations/{o}/projects` loses its route code, because `project.write` no longer covers creation and `project.create` is struck. The place, a body id, now decides alone. That breaks three things.
- The inventory: one code per route, with the code's scope either O or the route's. The place's code is `area.write`, of scope A, but the path has no area id. Declaring an O-scope read code instead is a "write declares `*.read`" fault (BR-SA-14). ADR-0029 § 7 handles the same case for a new child area by putting the parent in the path, and this route does not.
- The answer order: BR-SA-9 says body ids are checked after the route code, "each with its own not-found and then its second code". TC-2's "step 0, before any lookup" says the opposite.
- The stories contradict each other. US-4 says "a place deleted meanwhile answers 403 on submit", while US-8 says "a place that names no department answers the department's not-found", and neither says for whom.

Required resolution: decide the declaration and the order in BR-SA-17. Recommended: `POST /projects` goes on the inventory's exception list, with the reason "the addressed object is the place, carried in the body (mod-018 api-contract § 4); decided at step 0, before any lookup". A non-admin then gets 403 for any place they lack the code on, unknown, deleted and foreign places included, so nothing is disclosed. The admin gets «Область не найдена» for an unknown place. Align US-4 and US-8 on that answer. The alternative is a path route, `…/areas/{departmentId}/projects`, which is a larger contract change. The new ADR (R-23) records whichever is chosen.
Status: closed
Resolution: round 3. BR-SA-17, US-4, US-6 and US-8 put the route on the exception list, decided at step 0. A non-admin gets 403 for any place; the admin gets «Область не найдена.» for an unknown place, and in this round's edit (R-29) 409 `type_not_allowed` for a non-department place.

### R-23
Severity: major
Affects: BR-SA-16, BR-SA-17, BR-SA-5; US-6 "The matrix is generated…"; § Breaks (ADR-0029 row); R-10 (b)
Problem: the deferred ADR is scoped as "amends ADR-0029 § 4", but OQ-3 B and OQ-4 B change more than the matrix.
- **§ 6 reach.** The area-scope codes on a node reach it only through area grants held or inherited. OQ-3 B adds a new path: a project grant gives area codes on its node and on every sub-area below it, whatever their switches. The single, set and inverse forms and their parity test must all carry this path. BR-SA-5, the normative reach rule, does not state it; only the BPMN box and BR-SA-16 do.
- **§ 7.** The `POST /projects` declaration (R-22).
- **§ 12.** The matrix needs project roles on area routes for the project's node and sub-areas. US-6 excludes them ("no cell holds … a project or team role on an area route").
- **Delivery bullets.** The mod-019 delivery bullet names "`organizations.project.create` split from `.write`".

The ADR is also deferred to "the backend stage". The README's Process allows that, but code would then be written on an unwritten amendment, and gate-3 reviews it after the fact.

Required resolution:
- BR-SA-5 gains: "A project grant reaches its project, and, for area-scope codes, the project's node and every sub-area below it, whatever their switches".
- BR-SA-16 names the ADR's sections: §§ 4, 6 (reach), 7 (the creation route), 12 (matrix cells), and the delivery bullet.
- US-6 adds the cells for project roles on the node and on sub-area routes, and drops the exclusion.
- The ADR is written `proposed` in `plan-backend.md` before any code and accepted at gate-3, as with ADR-0031.
Status: closed
Resolution: round 3. BR-SA-5 states the project-grant path for all three forms. BR-SA-16 names §§ 4, 6, 7 and 12 and the delivery bullet, and requires the ADR as `proposed` in plan-backend before any code.

### R-24
Severity: major
Affects: BR-SA-2, BR-SA-16 (OQ-8 A, OQ-3 B), BR-SA-7 (the confirmation count); US-2 "Switching a project node off…", "A switch off counts…"; US-8 "A department manager runs the projects it reaches"
Problem: OQ-8 A gives department «Управление» `project.grant` on every project it reaches, and BR-SA-2 lets a holder grant "any active member", the holder included. Pavel can therefore give himself project «Управление» on A. That grant is held on the project, so turning A's node off does not cut it, which defeats the switch's purpose ("a project that must stand apart"). Through OQ-3 B it also gives him the area code on A's subtree, and from feat-041 on `organizations.visibility.write`, which US-8 says "stays the project role's". The switch-off confirmation excludes "holders of a project grant", so it hides exactly these grants from the admin. Granting a colleague is delegation, which is intended. Granting oneself is the escalation.
Required resolution: the human answers OQ-9. Then add one US-1 scenario and, depending on the answer, one US-2 line on what the confirmation shows.
Status: closed
Resolution: round 3. OQ-9 B is BR-SA-19, with the US-1 scenario. The mutual-grant residual is R-27.

### R-25
Severity: minor
Affects: BR-SA-12; US-4 "The create commands…"
Problem: "absent, never disabled (feat-043 BR-NV-1)" cites a rule about left-menu entries for a page command. The outcome is right: feat-043 § Roles and BR-NV-14 already make create commands absent for those without the code. With OQ-4 B, an empty place list can only come from a race, as the author says.
Required resolution: cite feat-043 § Roles and BR-NV-14 instead. Keep the reading. Replace "answers 403 on submit" with R-22's answer.
Status: closed
Resolution: round 3. BR-SA-12 now cites feat-043 § Roles and BR-NV-14, and the submit answer is BR-SA-17's.

### R-26
Severity: minor
Affects: US-8 fixture and "A project manager manages the sub-areas of its project"
Problem: the fixture says a project node is "moved … with its project", but US-8 moves it on its own, by the area move (feat-043 BR-NV-11), and ADR-0031 rule 5 ties only create, rename and delete to the project. Kirill "moves «Платформа» under A's node", where it already is.
Required resolution: say "created, renamed and deleted with its project; moved by the area move". Make the move "moves «Тесты» under «Платформа»".
Status: closed
Resolution: round 3. The US-8 fixture and Kirill's move are reworded as asked.

### On the widenings the coordinator asked about

- **OQ-3 B.** Bounded, provided R-23's reach rule is stated. A project manager neither moves nor switches its own node, which needs the parent's code, and US-8 pins that.
- **OQ-4 B.** Sound, but it creates R-22. The creator reaches the new project through the department grant only while the node's switch is on, which is the default at creation.
- **OQ-8 A.** The note "widens grants already given" changes nothing in practice: no grant exists before this task merges. The real risk is the self-grant (R-24).
- **The deferred ADR.** Acceptable timing if it is written before code (R-23). Its scope as listed is incomplete.
- **The mod-018 `POST /projects` change.** It is in § Breaks, but the route's declaration and answer order are unresolved (R-22).
- **R-11 "absent, not disabled".** The outcome is correct and the citation is wrong (R-25).

### New open question for the human

- [ ] **OQ-9 May a manager give a grant to themselves?**
  - (A) Yes. Simplest. A department manager can make themselves project «Управление» on any project they reach, and keep it after the admin cuts the node off; the confirmation stays silent about it.
  - (B, recommended) No: a grant's grantee is never its giver, on every object, and the request is refused with the contract's validation code. It closes the escalation, while delegation to others and the admin's own powers are unchanged. Cost: one rule and one test.
  - (C) Yes, but the switch-off confirmation also lists the project grants given by the users who stop being reached, and offers to end them. This keeps self-service but needs more UI, and still depends on the admin noticing.

### Verdict (round 2)

**needs-human-decision.** OQ-9 is the human's. R-10, R-22 and R-23 are majors the business analyst can fix without a
decision. This was the second and last review iteration (stage file: at most 2), so they go to the human with the
gate-1 summary, together with the three minors R-19, R-25 and R-26. No blocker is open.

## Round 3 — requirements (delta and final pass)

Reviewer: requirements-reviewer, 2026-11-06. **This round goes past the stage file's limit of two iterations, by the
human's decision in chat before approving gate-1.** I read commit 3c755082 as the files now stand (no Bash), and made a
final pass over BR-SA-1..-19 and US-1..US-8 for contradictions left by three rounds.

**Statuses:**
- R-10, R-19 and R-22..R-26 are closed, each with a reason above.
- R-27..R-29 are new. All three are minor, and the reviewer fixed R-28 and R-29 with small edits, as the stage file allows.

### R-27
Severity: minor
Affects: BR-SA-19, BR-SA-7; US-2 "A switch off counts…"; US-1 "A grantee is never the grant's giver"
Problem: the residual the coordinator asked about. Under OQ-8 A, Pavel gives Irina project «Управление» on A, and she gives him the same (US-1 pins that both are kept). Each grant is held on the project, so turning A's node off cuts neither. OQ-9 B closes only the one-person path. The switch-off confirmation leaves out project-grant holders, and BR-SA-19 gave a false reason for that ("no one can have made themselves one").
Required resolution: the reviewer reworded the reason in BR-SA-19 and US-2 to the true one: a project grant is held on the project and is not cut by the switch. BR-SA-19 now records the residual as accepted at gate-1 unless the human reopens it. If the human wants it closed, the cheap fix is one line in the switch-off confirmation naming the project grants on this project given by the users who stop being reached.
Status: closed (the human, 2026-11-06: close it by making it visible)
Resolution: round 3, reviewer edit; then the human's decision, applied by the business analyst: the switch-off confirmation also lists the project grants on this project given by the users who stop being reached (BR-SA-19, US-2); it only lists them, ending them stays manual; US-5 describes no dialog, so it is unchanged. Earlier: BR-SA-19 and US-2 are reworded, and the US-5 picker never offers the actor (BR-SA-19).

### R-28
Severity: minor
Affects: US-8 "A department manager manages the sub-areas…", "A project manager cannot place or switch its own project"
Problem: renaming A's node through the area route was expected to answer 403. But Pavel (by inheritance) and Kirill (OQ-3 B) both hold `area.write` on the node, so the code passes, and mod-018's contract answers 409 `area.root_is_fixed`.
Required resolution: expect 409 `area.root_is_fixed`. Kirill's move and switch stay 403, because he lacks the parent's code.
Status: closed
Resolution: round 3, reviewer edit in US-8.

### R-29
Severity: minor
Affects: BR-SA-17; US-8 "The place decides"
Problem: the admin's answer for a place that is a project node or a sub-area was not stated.
Required resolution: mod-018's 409 `type_not_allowed`. A non-admin keeps the 403.
Status: closed
Resolution: round 3, reviewer edit in BR-SA-17 and US-8.

### Final pass

- **Rules, BR-SA-1..-19:** no contradiction left.
  - BR-SA-2 and BR-SA-19 agree: no self-grant, and removing one's own grant is allowed.
  - BR-SA-5, -6 and -16 agree on the project-grant reach.
  - BR-SA-10 does not give OQ-8 A's department «Управление» `boards.projectbacklog.reorder`, and US-3 expects 403 for an area role, which is consistent.
  - BR-SA-12 and BR-SA-17 agree on the places.
  - Cosmetic: BR-SA-19 is listed before BR-SA-18.
- **Stories, US-1..US-8:** the outcomes match the bundles under the «Читатель» fixture; I spot-checked US-2, US-3, US-7 and US-8 against BR-SA-15..-17.
- **Constraints:** no Keycloak change. No criterion needs E2E. The ADR is `proposed` before code, and the human accepts it at gate-3.

### Verdict (round 3)

**approved-by-ai: ready for gate-1.** No blocker or major is open. The one item the human must see is R-27 (accepted-risk): two
managers who grant each other project «Управление» keep it after a switch-off, and the giver column is the only trace.
The human accepts that at gate-1, or asks for the one-line confirmation fix.
