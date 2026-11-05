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
Status: open
Resolution:

### R-2
Severity: blocker
Affects: US-2 "Who may switch, and the parent's code"; BR-SA-7
Problem: Pavel (only «Управление» on «Логистика») switches off A's node "and then «Платформа»", and "both are kept". Once A's node is off, nothing from «Логистика» reaches A's node or «Платформа» below it (BR-SA-5), so Pavel holds `organizations.area.write` on neither, and the second switch must answer 403. The scenario contradicts BR-SA-5 and BR-SA-7. It also leaves out doc-012 US-1's owed clause: after the switch, only the admin (or a holder of «Управление» on the node and on its parent) turns it on again.
Required resolution: switch «Платформа» first, then A's node, both kept; then add "Pavel's attempt to switch A's node back on answers 403; the admin's is kept".
Status: open
Resolution:

### R-3
Severity: blocker
Affects: US-3 "Split codes"; US-4 "Pickers offer what the caller may do"; BR-SA-12; spec § Roles (department «Управление» "place", "sub-areas it reaches")
Problem: the human's mod-018 gate-1 answers (2026-10-28) and ADR-0031 rule 5 forbid what these scenarios expect. Departments do not nest (OQ-3 A). Sub-areas sit only below a project node (OQ-7 A). A project node is created and deleted only with its project (OQ-5 A, `root_is_fixed`). So nothing can be created under «Логистика» except a project, and creating a project needs `organizations.project.create`. Yet US-3 has Pavel "create a child area under «Логистика», rename it and delete it", each kept, and US-4 offers him "the create dialog for a child of «Логистика»". A department is never moved under anything either (mod-018 api-contract § 3: a department moved to the top is a no-op), so "the department's … place" in § Roles is not a power.
Required resolution: rewrite both scenarios on a sub-area. Pavel creates, renames and deletes a sub-area under A's node, each kept; he creates a department or a project, each 403. In US-4 Pavel is offered the sub-area create dialog on A's node and on «Платформа», and none on «Логистика». BR-SA-12: "for a sub-area, by `.area.write` on its parent (a project node or a sub-area)". § Roles: drop "place" for a department; "the sub-areas below the project nodes it reaches".
Status: open
Resolution:

### R-4
Severity: major
Affects: US-3 "The answer order stays today's"; BR-SA-9
Problem: the last line, "the same «Читатель» changing an item id that names nothing in A gets «Рабочий элемент не найден.», not 403", is the new order of ADR-0029 § 8 and doc-012 US-5. It is not today's. In mod-016 api-contract § 0.1, row 3 (403) precedes row 4 (a nested id's 404), and `RetrofitAccessTests.Order_AViewersWriteToAMissingProject_Is403_…` pins that. ADR-0029 § 8's last paragraph says this line keeps today's order until feat-041. As written, the story makes the developer change the merged contract early, which BR-SA-9 forbids.
Required resolution: expect 403 «Недостаточно прав» for the «Читатель»'s write to a missing item in A. Add that a «Участник»'s write to the same id answers «Рабочий элемент не найден.». State that `RetrofitAccessTests` is unchanged until feat-041.
Status: open
Resolution:

### R-5
Severity: major
Affects: US-4 "A section is shown by the object's codes", "The menu is built from the object's codes"
Problem: the stories treat «Настройки» as a section of a project's page ("Kirill sees «Настройки»", "Olga opening A's «Настройки»", "Vera sees … no «Настройки»"). In the merged feat-043 (BR-NV-18), «Настройки» is the derived last entry, always present, with the «Организация» group. A holder of the write code gets one group per chosen kind, «Проект» / «Департамент» / «Команда» → «Параметры», and "No object has a «Настройки» tab". As written, the criteria fail against the merged panel or push the developer to rebuild it.
Required resolution: say "Kirill sees the «Проект» group («Параметры») in «Настройки»; Olga does not, and opening A's `…/settings` by address shows «Доступ ограничен»". For Vera: "the «Настройки» entry shows only the «Организация» group". Keep "no entry is shown disabled" (BR-NV-1).
Status: open
Resolution:

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
Status: open
Resolution:

### R-7
Severity: major
Affects: US-2, US-3, US-4; doc-012 US-1, US-7 owed clauses; feat-048
Problem: the spec drops several owed clauses and changed surfaces.
- A move by a non-admin is never specified. BR-MX-3 requires «Управление» on the node, its current parent and its new parent. Under OQ-1 A, this means a department manager moves a project node between two departments they both manage. A project «Управление» holder never moves their own project (feat-043 BR-NV-11 places it from «Общие»).
- doc-012 US-7's "who reorders which backlog" clause (team planner versus project «Изменение» versus viewer) is not carried.
- Its "a shared board moves only the cards the user may change" clause is not carried either.
- The merged feat-048 grouped backlogs and per-project board lanes at organization and department level move rows and cards "where the project's own board or backlog allows" (feat-048 § Roles, the role bundle until mod-019). They now have to follow per-project codes, and no scenario says so.
Required resolution: in US-2, add a move scenario. Pavel holds «Управление» on «Логистика» and on «Финансы» and moves A's node between them: kept. With «Логистика» only: 403. A project «Управление» holder: 403, with no placement control in «Общие». In US-3, carry doc-012 US-7's two clauses. In US-4, add "on a department's grouped «Бэклог» and board, Olga («Читатель», «Изменение» on A) moves rows and cards in A's group and lane only; a direct move in B's answers 403".
Status: open
Resolution:

### R-8
Severity: major
Affects: BR-SA-11; US-4; § Decomposition (feat-NNN-codes-served-per-object)
Problem: after BR-SA-11, the organization answer (mod-016 § 1.1) carries organization-scope codes only. It drops `workitems.*`, `boards.*`, `organizations.project.read/write`, `organizations.team.write` and `organizations.area.write`. Today every panel surface reads those from the organization answer: the kanban and backlog (mod-006, feat-039, feat-040, feat-048), card checklists (feat-032), parent links (feat-001), tree editing (feat-027), the item form, and «Структура». Any surface not switched to per-object codes silently loses its controls for every «Участник». That is a regression no scenario would catch. The spec lists neither the consumers nor a guard.
Required resolution: add to BR-SA-11 "every panel consumer of a non-organization code reads it from the object's served codes", with the list of surfaces handed to the design. Add a US-4 scenario: "a «Участник» with no grant is offered on every surface the controls offered today", run as component tests per surface. Add a unit or lint test that fails when a non-O code is read from the organization answer.
Status: open
Resolution:

### R-9
Severity: major
Affects: BR-SA-11, BR-SA-12; US-4 "Pickers"; US-5 "The switch control sits on the node's page"
Problem: BR-SA-11 serves codes on a read of one department, project or team, and per project on a multi-project view. «Структура» reads the whole tree (1 000 nodes, within 2 s, feat-043 BR-NV-9). The tree's row commands (create a sub-area, rename, move, switch, delete) and the move-destination pickers need `organizations.area.write` per node. ADR-0029 § 10 does not cover a tree, and the spec does not say how the tree carries them. The likely result is a per-node read or a full matrix, which § 10 forbids.
Required resolution: state that the tree read carries, once, the ids of the nodes where the caller holds `organizations.area.write`, from the set form, with the tree's 2 s and a pinned statement count still holding. The pickers offer only those ids. A node page carries its own codes as any object read.
Status: open
Resolution:

### R-10
Severity: major
Affects: BR-SA-4 (OQ-1 A); US-1 "A grant of the wrong scope is refused"; US-6 "The matrix is generated, not written"
Problem: (a) Under OQ-1 A, a department grant inherited onto a project node or a sub-area still brings `organizations.area.grant` there. ADR-0031 rule 7 itself names grants "held on the node". Yet no scenario says what a request to give an area role on a project node or a sub-area answers. (b) US-6 takes "each of the 11 roles held on the addressed object". That is impossible for area roles on a project, team or sub-area route, and for project or team roles on an area route.
Required resolution: (a) add to US-1: an area role given on a project node or a sub-area id answers the catalogue's type refusal, the exact code being the contract's (as mod-018's `type_not_allowed`), and stores nothing. (b) Rewrite US-6. Organization roles come through the membership. A role of the route's own scope is held on the addressed object. Area roles are held on the department above (switch on, switch off) and on another department. A project or team role is held on another object of its scope.
Status: open
Resolution:

### R-11
Severity: major
Affects: BR-SA-12, BR-SA-13; US-4 "Pickers" last line; § Context ("takes over m-2, m-3 / DEF-4")
Problem: (a) Under OQ-4 A, only the admin holds `organizations.project.create`, and the admin always has the top level as a place. So "«Новый проект» … disabled with a reason without a place" can never fire, and US-4 cannot be tested. Under feat-043 (§ Roles, BR-NV-14), a non-admin has no create command at all. (b) m-2 and m-3 are already closed in mod-018. Per mod-018 review.md, round 3 "Old debt re-checked": `MoveAreaHandler.cs:91` re-decides on the locked row, and the contract sentence was fixed as TC-15. `AreasPage`/`AreaPage` read `areaCreate`. The spec presents them as work to do.
Required resolution: (a) the TC-30 fix becomes "«Новый проект» is offered only with `organizations.project.create`, and absent otherwise; its «Расположение» offers the top level and the departments where the caller holds `organizations.area.write`, never empty". Drop "disabled with a reason". (b) Reword m-2 and m-3 as "kept under the split bundles", proved by regression tests (US-2's locked-row scenario stays, now for the switch route, which is new).
Status: open
Resolution:

### R-12
Severity: minor
Affects: OQ-6; US-2 "A switch off asks first"
Problem: "names how many users lose access" cannot be checked as written. Every member keeps the organization role's access to a «Организация» project, so nobody loses access. What they lose is the extra codes. Users with their own grant on the project, and admins, lose nothing.
Required resolution: define the count as "the distinct holders of grants held on an ancestor that reach the node now and will not after, organization admins excluded". Add an extra line when the actor is among them («Вы потеряете управление этим узлом»; copy is the design's).
Status: open
Resolution:

### R-13
Severity: minor
Affects: US-4 "Re-reads happen on the triggers"
Problem: "When Olga's own grant changes" cannot be tested when someone else changes it: the panel has no push channel.
Required resolution: "when the caller writes a grant on the object (their own included), the panel re-reads the object's codes; a change by someone else shows at the next trigger (refocus, a 403 or 404, a page load)".
Status: open
Resolution:

### R-14
Severity: minor
Affects: § Context; author's flagged points 1 and 2
Problem: point 1 is correct. ADR-0031 rule 7 supersedes ADR-0029 § 6's link-based reach. But ADR-0031's header supersedes only § 6, while three other sentences still say "linked": ADR-0029 § 4's note, § 7 "Cost" ("the ancestors of its linked areas") and § 12's matrix ("an area linked directly"). An implementer following § 12 literally builds a case that cannot exist. Point 2 is correct with a caveat. Under OQ-1 A, the matrix's "Area" roles are department-held grants. Their area-scope codes still apply on every node they reach (the project node and the sub-areas), so the resource scope must stay `Area` and not become `Department`.
Required resolution: one line in § Context: "ADR-0029 §§ 4, 7 and 12 are read through ADR-0031 rule 7: 'linked area' means the project's node and its ancestors". One line in § Roles: "«Департамент» is the UI name; the stored scope and `ResourceScope` stay `Area`".
Status: open
Resolution:

### R-15
Severity: minor
Affects: BR-SA-6
Problem: (a) "On a sub-area it decides whether the grants reaching its project node reach it" is wrong for a sub-area under a sub-area (depth up to 10). It is the parent that matters. (b) "On a department … the stored value matters once the node is moved under a department" cannot happen to a department, which never nests (mod-018 OQ-3 A).
Required resolution: (a) say "its parent". (b) Say a department's stored value never takes effect (see OQ-2).
Status: open
Resolution:

### R-16
Severity: minor
Affects: BR-SA-2, BR-SA-3
Problem: four edge cases are unstated.
- Two managers removing each other's grants at once both succeed (ADR-0029 § 7). This differs from mod-016 §§ 1.5-1.6, where the caller is re-judged under the lock, and an implementer may copy that pattern.
- A manager may remove or downgrade their own grant.
- A grant to an organization admin is allowed, and it is kept when the admin is demoted.
- A grant write is judged at check time.
Required resolution: add these four sentences to BR-SA-2/-3, plus one US-1 scenario for the mutual removal.
Status: open
Resolution:

### R-17
Severity: minor
Affects: US-1 fixture and "Only an active member…"; US-2 "Switching a project node off…"; US-5 first scenario
Problem: "D" names both a top-level project node and a removed user. "Pavel is shown no grant control on B" passes whatever the switch does, because a department bundle never holds `organizations.project.grant`. The US-5 title "direct and inherited grants" does not fit a department, which has nothing above it.
Required resolution: rename the user, for example Denis. Replace the vacuous line with "Pavel no longer appears as inherited in B's «Участники и доступ»". Retitle the US-5 scenario "A department's «Участники» lists its grants".
Status: open
Resolution:

### R-18
Severity: minor
Affects: spec (no "Breaks" section)
Problem: the line breaks several merged contracts: mod-016 api-contract § 0.2 and § 1.1, mod-006 BR-M4-27, feat-039 BR-PB-5, feat-048 § Roles, feat-043 BR-NV-3 / § Roles ("until mod-019"), and mod-018 BR-AW-5. AGENTS.md and ADR-0029 Consequences (P-10) require a note in each owner's status.md. The spec does not list them, so the notes will be missed.
Required resolution: add § Breaks, one row per owner, with the note each gets.
Status: open
Resolution:

### R-19
Severity: minor
Affects: US-6 "The bundles are a contract"; BR-SA-14
Problem: "the realm contract test still compares instance codes only" predates ADR-0033. The realm now holds four client roles, mapped to codes by a table in code. That table's hand mirror in `src/frontend/src/shared/api/tokenClaims.ts` waits for "the machine-readable contract that `mod-019` takes over" (ADR-0033 Consequences; tech-024 D-6). The spec does not say whether that mirror is in or out.
Required resolution: reword the sentence to ADR-0033's terms, and state the scope per OQ-7.
Status: open
Resolution:

### R-20
Severity: minor
Affects: NFR Performance; US-3 "A single-item read stays fast"; US-5 "The page works at a long list"
Problem: "within 500 ms" and "within 2 s" lack a test form, so CI timing noise makes them flaky or unchecked. "A number of statements that does not grow" needs a pinned count.
Required resolution: "an integration test on a seeded database pins the statement count of the check (equal at 10 and at 10 000 grants, at depth 1 and 10) and asserts the time on that seed". The 2 s list is a component test on a 200-row fake plus the read's pinned count. No E2E.
Status: open
Resolution:

### R-21
Severity: minor
Affects: US-1, US-5 (grant lists and picker)
Problem: the grant list's order, the inactive and unknown users, and the picker's limits are not stated. mod-016 §§ 0.4-0.5 define them for members (UserRef, the `ru` collation order, 2-character minimum, 20 results, `moreMatched`).
Required resolution: "grant lists and the grantee picker follow mod-016 §§ 0.4-0.5", plus one US-5 line for an inactive grantee («неактивен»).
Status: open
Resolution:

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
