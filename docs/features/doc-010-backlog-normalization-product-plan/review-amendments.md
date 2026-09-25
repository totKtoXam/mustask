# doc-010 — review of the amendments to the merged specs

Part B of stage 2. It covers the amendment pass over doc-001..doc-007 (spec.md, stories/, status.md), checked against cross-review.md (§ Findings by root cause, § Defaults, § Amendment map) and D-4..D-20.

The product plan itself (spec.md, stories/) is part A, in review.md. The diff was taken file by file against the main checkout, because this session had no shell for `git diff`.

## Round 1 — amendments

### RA-1
Severity: major
Affects: doc-006 BR-M1-9, BR-M1-14, BR-M1-18, § Operation completeness (Team, Area), § Out of scope, US-6; doc-001 § Deletion lifecycle, § Operation completeness (Team), US-2; doc-003 BR-M4-2, § Board columns, US-1, US-4; doc-007 § MVP: the seeded process (category table)
Problem: the Log names three calls the analysts made where D-6/D-7/D-4 are silent. Each is a product decision only the human can take. Each was checked everywhere it lands.
- (a) **A deleted team cannot be restored, its deletion is confirmed first, and its name becomes free.** Where it lands: doc-006 BR-M1-18, the BPMN confirmation node, Operation completeness Team, § Out of scope and US-6; doc-001 § Deletion lifecycle ("Restore: none", "the name is freed") and Operation completeness. It is consistent across doc-001/006, and doc-003/004/005 define no restore path, so they agree too. It contradicts the plan: doc-010 § ADR debt gives the team "restore, names reserved" (RA-11). If the human says no, mod-002 needs a restore that reverses the cascade over M4, M5 and M6.
- (b) **Every team after the first comes with its own child area of the root, named after it, owned by it and set as its default.** Where it lands: doc-006 BR-M1-9 and BR-M1-14, Operation completeness Area ("a later team's first area comes with the team"), US-6 sc.1 and sc.3; doc-001 US-2 sc.1. doc-003 US-4 and doc-002 § Fields agree with it. It has three effects:
  - creating a team fails while the root already has a child area of that name;
  - the admin cannot create a team without an area;
  - combined with (a), a deleted team's leftover area blocks re-using the freed name (RA-2).

  The alternative is to create the team with no area and make the admin pick a default among the areas it owns. That would need a rule for a team that owns no area yet.
- (c) **Every board gives «Разрешена» a column of its own, the Эпики and Фичи boards included.** It follows from doc-003 BR-M4-2's generic per-(type, state) rule: InProgress holds two states on those types. The boards are:
  - Эпики and Фичи: «Новые» · «В работе» · «Разрешена» · «Завершённые»;
  - Истории: those four plus «Решённые».

  Where it lands: doc-003 § Board columns and US-1 sc.1. doc-001 BR-2 and US-7 and doc-007 give only the Истории example and defer to doc-003, so they are consistent. It matches the reference product's Epic and Feature boards, which have a Resolved column. The alternative is to put Эпик/Фича «Разрешена» in the «В работе» column. BR-M4-2 would then need a per-level exception.
Required resolution: the human confirms or overturns (a), (b) and (c) at gate-1. If one is overturned, the analyst reworks the listed places.
Status: resolved
Resolution: D-23 — the human confirmed (a), (b) and (c) as written; no rework.

### RA-2
Severity: minor
Affects: doc-006 BR-M1-18, the team-deletion BPMN, US-6; doc-005 BR-M6-10, US-5
Problem: D-6's cascade in doc-006 had two defects.
- It named bound queries but not team dashboards. doc-005 BR-M6-10 and US-5 soft-delete them with the team (release 3), so the M1 rule and the M6 rule disagreed on the cascade's reach.
- BR-M1-18 said "the deleted team's name becomes free". BR-M1-9, however, refuses a new team while the root has a child area of that name, and the deleted team's area keeps its name. The freed name was therefore not usable. The behaviour was determined by the rules, but it was not stated or tested.
Required resolution: add team dashboards to the cascade, and state the leftover-area effect in the rule and in a scenario.
Status: resolved
Resolution: BR-M1-18 now includes "its team dashboards are soft-deleted (doc-005, from release 3)" and the sentence on a same-named new team, which is refused until the area is renamed. The team-deletion BPMN node now lists the dashboards. US-6's deletion scenario has a new "And" line that tests the refusal and then the success after the rename. No new behaviour: this is what BR-M1-9 already implied. It rides on RA-1 (a)/(b).

### RA-3
Severity: minor
Affects: doc-006 BR-M1-14, US-6 sc. "An administrator sets a team's default area"
Problem: BR-M1-14 read "the first team's is the root area; a later team's is the area created with it, and an organization administrator may set it to any other area that team owns explicitly". The "it" could cover the first team, while US-6 and Operation completeness say the first team's default stays the root. D-7 ("the first team's default is the root") supports the stories.
Required resolution: make the rule say which team's default can change.
Status: resolved
Resolution: BR-M1-14 now reads "the first team's is the root area and never changes; a later team's is at first the area created with it (BR-M1-9), and an organization administrator may set a later team's default to any other area that team owns explicitly."

### RA-4
Severity: minor
Affects: doc-005 § Decomposition (mod-008); doc-002 § Decomposition (feat-NNN-work-item-estimation-fields); doc-010 § Normalized queue, PR-5
Problem: the module rows were written in parallel with the plan and do not carry its release-2 gate. PR-5 says mod-008 and the estimation line list doc-NNN-release-2-scope, and the plan's § Amendments says doc-005's rows "follow § Normalized queue". But:
- doc-005's mod-008 row had deps `feat-003, mod-007, doc-010`, with no release-2-scope and no doc-005;
- doc-002's estimation row had `mod-003` only.

A reader of either spec would queue a release-2 root that the plan forbids starting.
Required resolution: align the two rows with the plan's target deps.
Status: resolved
Resolution: mod-008 deps are now `feat-003-work-item-search, mod-007-iterations, doc-005-queries-dashboards, doc-NNN-release-2-scope`, and the estimation line's deps are now `mod-003-work-item-core, doc-NNN-release-2-scope`. Three rows are left as they were: they differ from the plan only by redundant deps, which reach the same lines transitively. They are doc-002 feat-003 (`mod-003, mod-005`, the literal D-14), doc-004 mod-012 (`mod-011`, plus the estimation line per N-24) and doc-006 mod-002 (`doc-006, mod-001, projection, doc-010`). The queue is the plan's.

### RA-5
Severity: minor
Affects: doc-001 § Concurrency policy (card move); doc-003 NFR Refresh model, US-1 "Two members drag the same card at once"; cross-review N-16
Problem: N-16 settled the same-card case: "the second drag's column is final, and the second mover gets the notice". doc-003 implements that, but doc-001's table gave the notice only to reorders. The table the modules cite was narrower than the module.
Required resolution: the card-move row states the same-card outcome.
Status: resolved
Resolution: the card-move row now adds "of two moves of the same card the one arriving second decides the column, and its mover gets the stale-move notice below and a refreshed view (doc-003)".

### RA-6
Severity: minor
Affects: doc-001 BR-9; doc-004 US-4 "A full tie prefers a sprint, then the lower number"
Problem: the current-iteration derivation moved from doc-004 BR-8 to doc-001 BR-9 without its last clause, "(between sprints, the lower number)". doc-004 US-4 still expects sprint 5 to beat sprint 8 on "doc-001 BR-9". "Created first" and "lower number" coincide only by inference from BR-M5-2. They are not provably equal for two sprints created at the same moment, and BR-M5-2 is exactly the rule that gives those two distinct numbers.
Required resolution: restore the deterministic tie-break.
Status: resolved
Resolution: doc-001 BR-9 now reads "…then the one created first (between sprints, the lower number)".

### RA-7
Severity: minor
Affects: doc-003 § Data model (BOARD_COLUMN)
Problem: doc-003 still said it refines doc-001's ER edge `BOARD_COLUMN }o--|| STATE_DEFINITION : "maps to"`. The amended doc-001 ER has no such edge; it now reads `BOARD_COLUMN }o..o{ STATE_DEFINITION : "maps per (type, state), derived (BR-2)"`. This is a cross-reference to a statement that no longer exists.
Required resolution: quote the current edge.
Status: resolved
Resolution: the row now cites doc-001's derived edge `BOARD_COLUMN }o..o{ STATE_DEFINITION : "maps per (type, state)"`, one state per (column, type) at most.

### RA-8
Severity: minor
Affects: doc-002 § Decomposition (mod-003 row, preamble); doc-001 § MVP boundary (M3 row)
Problem: three small defects.
- BR-M1-13 refuses to delete an area that holds work items, «Удалена» ones included. mod-002 ships before any item exists, so it cannot count them. The plan's E-3 gives this check to mod-003, and PR-9 makes the later line enforce it, but doc-002's mod-003 row did not carry it.
- doc-002 said "Eight lines are queued", but only seven of its eight rows are queued; the estimation line is new.
- doc-001's M3 row left the purge actor as "the administrator's purge", where N-13 requires "organization administrator".
Required resolution: carry the check in the mod-003 row, fix the count, and qualify the actor.
Status: resolved
Resolution: the mod-003 row now "enforces doc-006 BR-M1-13's refusal to delete an area that holds any of its items, «Удалена» included (M1 ships before the items exist)". The preamble now reads "Seven lines are queued". doc-001's M3 row now reads "the organization administrator's purge".

### RA-9
Severity: minor
Affects: story titles and "As a …" lines of doc-002 (US-1..US-11), doc-003 (US-1..US-4), doc-004 (US-3, US-4, US-5, US-7, US-11..US-13) and doc-005 (US-1..US-4), plus those specs' story tables; doc-001 § Roles actor rule; N-10, N-29
Problem: doc-001's amended rule makes stories name the actor by role in the resource's organization. N-10 says team membership carries no access. doc-001 renamed its own stories from "A team member …" to "A member …", and the module Givens were swept to "a user whose role in the organization is …". The personas, however, still say "team member" — roughly 200 occurrences across the four module specs. A title like "A team member works the Kanban board" suggests that team membership gates the board, which doc-001 US-7 now explicitly denies. The sweep row "US-1..US-12 actor wording" is therefore only half applied.
Required resolution: sweep "team member" to "member" (or "a user whose role … is member") in the titles, "As a" lines and When-steps of doc-002..005 and their story tables. Keep "team" wherever it names the team whose surface is meant ("team A's board"). Alternatively, the human accepts the wording as a known inconsistency.
Status: resolved
Resolution: titles and "As a" personas of doc-002 US-1..US-12, doc-003 US-1..US-4, doc-004 US-3..US-8, US-11..US-13 and doc-005 US-1..US-4 (29 files) and the four story tables now name "a member" / "an organization member", the team only for a team surface; Gherkin step wording is unchanged.

### RA-10
Severity: minor
Affects: doc-002 § Roles ("doc-010 § Permission codes"); doc-004 § Roles (same); doc-010 spec.md § ADR debt
Problem: both module specs cite a doc-010 section named "Permission codes", which does not exist. The catalog is a bold paragraph, "Permission codes each line registers", inside doc-010 § ADR debt. N-9 named the section "§ Permission codes".
Required resolution: preferably, part A gives the catalog its own heading ("### Permission codes") in doc-010 spec.md, which makes both citations resolve. Otherwise the two citations are re-pointed to § ADR debt. Owner: part A's author or the orchestrator. doc-010's spec is outside this review's write zone.
Status: resolved
Resolution: doc-010 spec.md has its own `## Permission codes` (code → owning line → release, incl. `queries.query.purge` and `dashboards.dashboard.purge`); doc-002's citation now reads "doc-010 § Permission codes" unbroken, doc-004's already did.

### RA-11
Severity: minor
Affects: doc-010 spec.md § ADR debt, row "Soft deactivation by status"; doc-006 BR-M1-18; doc-001 § Deletion lifecycle
Problem: the plan tells mod-002's soft-deactivation ADR to cover "Organization, project and team (D-6): hidden from lists, restore, names reserved, cascade". The amended specs give the team no restore and a freed name (RA-1 (a)). The ADR's author would be briefed against the requirement.
Required resolution: after RA-1 (a) is decided, part A splits the row. Organizations and projects get restore and reserved names; teams get a cascade with no restore and a freed name (or whatever the human decides).
Status: resolved
Resolution: D-23 — the § ADR debt soft-deactivation row gives organization and project restore with reserved names, and the team a cascade with no restore, its name freed and deletion confirmed first (doc-006 BR-M1-18).

### RA-12
Severity: minor
Affects: status.md of doc-001, doc-003, doc-006 (the owner is the orchestrator)
Problem: every amended task carries the `[doc-010] amended` Log line, and W-2, DD-1, E-4, E-13, doc-005 D-14, H-5, H-6, P-1 and P-2 are marked superseded. A few supersessions are unmarked, and one statement went stale:
- doc-001 D-5 ("an administrator purges permanently by hand") is narrowed by doc-010 D-13: organization administrator only, in the product, work items only in MVP, never organizations, projects, teams or iterations.
- doc-001 D-9's closed list "(instance administrator, dictionaries administrator)" became "at least …, named by the ADR-0018 amendment", with a grantable create-organizations role or group (doc-010 D-10, N-5 constraint 8). doc-006's first Decisions bullet repeats "the two instance-level roles only".
- doc-003 DD-2 says "doc-001 BR-11 unchanged". BR-11 is now the operation → policy table, and a drag of an item in «Удалена» is refused (N-14, N-16).
- The Log lines of doc-001, doc-006 and doc-007 are dated 2026-09-24, while doc-010's Log dates the pass 2026-09-25.

Nothing else in the status files changed: stage and gates are intact, and the Log order follows each file's convention.
Required resolution: the orchestrator appends "— superseded in part by doc-010 D-13 (…)" to doc-001 D-5, and "— superseded in part by doc-010 D-10" to doc-001 D-9 and doc-006's bullet. DD-2 gets "— amended in part by doc-010 D-20 (N-14, N-16)". The three dates are corrected, or left with a note.
Status: resolved
Resolution: done by the orchestrator (feac3e5), verified in the delta check: doc-001 D-5 and D-9, doc-006's first Decisions bullet and doc-003 DD-2 carry the notes, and all seven `[doc-010] amended` Log lines are dated 2026-09-25.

### RA-13
Severity: minor
Affects: docs/features/README.md; cross-review § Amendment map (the "Outside the specs" line); doc-010 spec.md § Amendments ("docs/features/README.md gets a note on the `mod-` prefix (N-33)")
Problem: the README note is not on the branch. The file lists `mod` only as a prefix and has no sentence saying that `mod-` also marks an L line that founds a sub-slice or a pattern.
Required resolution: add the one-line note before gate-3. Owner: orchestrator or analyst. The file is outside the amended specs.
Status: resolved
Resolution: done by the orchestrator (feac3e5), verified in the delta check: `docs/features/README.md`'s prefix row says that `mod` also marks an L line founding a sub-slice or a shared pattern inside an existing module.

### RA-14
Severity: minor
Affects: doc-001 spec.md (434 lines), doc-006 spec.md (403); also doc-007 (342) and doc-002 (303)
Problem: all four indexes are over the ~250-line guide (01-business-analysis.md § Rules). The question is whether story content leaked into them.
- **doc-001:** no Gherkin or scenario-level content is in the index. The growth is index material that grows with the number of modules, not with any one story:
  - the ownership-predicate paragraph and team-settings rule in § Roles;
  - the glossary, which absorbed doc-004 § Terms and doc-005 § Glossary;
  - the ER and the ownership table with releases;
  - BR-14 and BR-15;
  - § Deletion lifecycle and § Concurrency policy, which the modules cite.

  The one soft spot is § Decomposition's "ADRs it owes" column, a second copy of doc-010 § ADR debt. It is consistent today, but it is a copy that can drift.
- **doc-006:** it grew by § Roles constraints 5–8 (binding requirements on mod-002's ADR, not stories), the code table's scope column, the team-deletion flow, and BR-M1-9, BR-M1-14 and BR-M1-18. It stays entity-driven, as rounds 1–2 already ruled at 331–335 lines.
- **doc-007 and doc-002:** they grew by model and rule sections (N-15 data model, the release-2 estimation section, the category table).
- **Stories:** all are within budget; the largest is doc-004 US-1 at 149 lines.
Required resolution: none. The size is legitimate. Optionally, doc-001's ADR column could shrink to a pointer to doc-010 § ADR debt once doc-010 merges.
Status: accepted-risk
Resolution: sizes accepted as legitimate; no split proposed.

## Amendment-map checklist (summary)

The work list, § Amendment map, has 146 spec rows plus one row outside the specs (the README note). Each row was checked against the amended text. Anchors are to the amended files; the stories were checked file by file.

| Spec | Rows | Applied | Partial | Missing | Where (amended spec.md line anchors) / notes |
|---|---|---|---|---|---|
| doc-001 | 28 | 28 | 0 | 0 | § Roles :18-69 (predicate :38-52, table :54-58, actor rule and team settings :60-66); Glossary :138-168; ER :172-241; ownership :245-253; MVP and releases :255-298; BR-2..BR-15 :318-331; § Deletion lifecycle :333-347; § Concurrency policy :349-362; Operation completeness :364-381; NFR Audit :390-395; ADRs :412-425; US-1..US-10 |
| doc-002 | 28 | 27 | 1 | 0 | § Roles :18-33; § Types :53-70; § Fields :72-91; data model :93-126; BPMN :132-136; BR-M3-5/8/9/11/12/13/18 :180-188; § Release 2 :190-202; Operation completeness :204-214; NFR :216-246; Out of scope :248-272; Decomposition :274-298. Partial: the US-1..US-12 actor sweep (RA-9) |
| doc-003 | 18 | 17 | 1 | 0 | § Roles :19-34; BR-M4-2 :73 and § Board columns :92-104; BR-M4-5/6/10/13/16/17 :76-86; BR-M4-20/21/22 :88-90; data model :113-118; Operation completeness :122-127; NFR :129-147; Out of scope :149-178; Decomposition :180-188. Partial: US-1's actor wording (RA-9) |
| doc-004 | 22 | 22 | 0 | 0 | § Roles :17-34; § Terms :58-64; model :66-94; BPMN :98-130; BR-M5-5..BR-M5-17 :143-154; Operation completeness :156-166; NFR Audit :182-185; § Relation to doc-001 :205-213; Decomposition :215-235; US-2, US-3, US-7, US-10, US-11, US-13, US-14 |
| doc-005 | 16 | 15 | 1 | 0 | § Roles :20-40; model note :99-104; § Glossary :106-117; BR-M6-2..BR-M6-12 :124-134; Operation completeness :136-148; NFR :170-177; Decomposition :209-222; US-1, US-2, US-4, US-5. Partial: BR-M6-9 was not "merged into doc-001 BR-6 and deleted". doc-001 BR-6 took the principle, and BR-M6-9 keeps the module detail (who restores, the purge list); no contradiction |
| doc-006 | 22 | 22 | 0 | 0 | § Roles constraints :38-91, code table :93-107, team membership :109-117; BPMN and lifecycle :136-189; ER :196-236; BR-M1-1..BR-M1-18 :257-273; Operation completeness :275-286; NFR :288-321; MVP, Release 2, Out of scope, Decomposition :323-398; US-1..US-7 |
| doc-007 | 12 | 12 | 0 | 0 | § Roles :30-38; model :50-88; seeded process with keys, rank and kind, category table, state table :97-186; BR-M2-2/4/6/9 :267-273, BR-M2-3 retired :259-262; Decomposition :321; US-1, US-2, US-3, US-5 |
| Outside the specs | 1 | 0 | 0 | 1 | README `mod-` note (RA-13) |
| **Total** | **147** | **143** | **3** | **1** | |

Every root-cause Resolution line that names one of doc-001..doc-007 is reflected in the amended text. There are two exceptions: N-29's persona half (RA-9) and N-33's README note (RA-13).

The ten highest-ranked root causes were spot-checked against the amended text for the contradictions the A–D reports found: N-1..N-6, N-8, N-11, N-14, N-16. None survives. The only gaps were a narrower citation target (RA-5) and a lost tie-break (RA-6), both now fixed.

## Decisions check (D-4..D-20)

| D | Where it lands | Verdict |
|---|---|---|
| D-4 | The one state table in doc-007, cited by doc-002 § Types. Board columns per (type, state): doc-003 BR-M4-2 and § Board columns, doc-001 BR-2 and US-7. «Статус = Разрешена» spans two categories: doc-001 BR-2 and US-10, doc-002 § Types, US-3 and US-11, doc-005 BR-M6-5 and US-1, doc-007. Задача per W-2 | consistent; doc-007, doc-002 and doc-003 are identical in substance |
| D-5 | doc-007 category table; doc-001 glossary; doc-003 BR-M4-2 headers; doc-007 US-2 | consistent |
| D-6 | doc-006 BR-M1-18 and its flow; doc-003 BR-M4-20; doc-004 BR-M5-5 and US-2; doc-005 BR-M6-6 and BR-M6-10; doc-001 lifecycle and Operation completeness | consistent after RA-2; the calls in RA-1 (a) |
| D-7 | doc-006 BR-M1-9 and BR-M1-14, ER; doc-003 BR-M4-21 and US-4; doc-002 § Fields (Область) and BPMN; doc-001 US-2, US-4, US-7, ER, M4 row | consistent after RA-3; the call in RA-1 (b) |
| D-8 | doc-002 BR-M3-5 and US-4 | consistent |
| D-9 | doc-006 constraint 5, NFR Identity refresh, US-3, projection row; doc-001 § Roles, M1 row, ownership, decomposition | consistent |
| D-10 | doc-001 § Roles; doc-006 BR-M1-2, US-1, Operation completeness "Instance right" | consistent |
| D-11, D-12 | doc-001 § Roles and BR-5; doc-006 constraint 6; doc-002 BR-M3-12/13 and the feat-002/mod-004 rows; doc-005 § Roles and decomposition | consistent |
| D-13 | doc-001 BR-6 and lifecycle; doc-002 (org-admin purge); doc-004 BR-M5-10; doc-005 BR-M6-9; doc-006 BR-M1-1 | consistent |
| D-14 | doc-002 feat-003 row; doc-003 BR-M4-22 and its filter row; doc-004 feat-008 deps; doc-001 US-8, US-10, M4 row | consistent |
| D-15 | doc-002 § Release 2 and BR-M3-18; doc-004 BR-M5-15/16; doc-007 field note; doc-001 release 2 | consistent |
| D-16 | doc-005 BR-M6-5, US-1, mod-008/mod-010 rows; doc-001 BR-15 and release 2 | consistent |
| D-17 | doc-001 in place; doc-002 errata removed; doc-004 § Relation to doc-001 | consistent |
| D-18 | Out-of-scope sections of doc-001 (release 2), doc-002, doc-003, doc-005 and doc-006 | consistent |
| D-19 | doc-003 decomposition | consistent |
| D-20 | § Defaults N-5..N-34 as applied above; the N-16 table is doc-001 § Concurrency policy | consistent after RA-5 |

## Verdict

needs-human-decision. The analysts' three calls need the human's confirmation at gate-1 (RA-1):
- no team restore, with the team's name freed;
- an automatic area of its own as a later team's default;
- a separate «Разрешена» column on every board.

The five open minors need no decision from the human, except that RA-9 may instead be accepted as a risk. They belong to:
- the analyst: RA-9;
- part A's author: RA-10, RA-11;
- the orchestrator: RA-12, RA-13.

Seven minors were fixed in place (RA-2..RA-8), and one was accepted as a risk (RA-14).

## Delta check (after D-21..D-24)

Scope: the resolutions of RA-1 and RA-9..RA-13, the R-4 note and R-11 carried over from review.md, and D-21..D-24 where they land in doc-001..007. This session had no shell, so `git diff 66982c5` was not run. The touched files were checked against the resolutions.

- **RA-1 / D-23: holds.** The confirmed calls stand as written:
  - (a) doc-006 BR-M1-18 and doc-001 § Deletion lifecycle;
  - (b) BR-M1-9 and BR-M1-14;
  - (c) doc-003 BR-M4-2 and § Board columns.

  No rework was needed. The rules now cite D-23 as their source (RA-16).
- **RA-9: holds for what it now claims.** Titles and "As a" personas in doc-002..005 say "an organization member" or "an organization administrator". No "As a … team member" is left. All four story tables match the file headings: doc-002 13/13, doc-003 4/4, doc-004 14/14 and doc-005 5/5. Gherkin was not touched, as the resolution says. The step-level remainder is RA-15.
- **RA-10: holds.** doc-010 § Permission codes exists, and the citations in doc-002 :31 and doc-004 :28 resolve to it.
- **RA-11: holds.** The § ADR debt row gives each entity doc-001 § Deletion lifecycle's variant.
- **RA-12, RA-13: hold.** Both are marked resolved above.
- **R-4 note (doc-004 US-8 sc.2): holds.** «Оставшаяся работа» is set to 0 before closing, so the scenario agrees with doc-002 BR-M3-18. The added closing-alone line is RA-17.
- **R-11 (doc-001 release-2 paragraph): correct.** Notifications are cross-module and belong to no module until doc-NNN-release-2-scope. doc-006's release-2 paragraph names the M1/M3 double claim, and doc-002 § Out of scope defers the owner. They agree.
- **D-24: holds.** doc-002's estimation row lists `mod-003-work-item-core, doc-NNN-release-2-scope`. doc-005's mod-008 row lists `feat-003, mod-007, doc-005, doc-NNN-release-2-scope`.
- **D-22: holds.** doc-004's mod-011..mod-013 rows and doc-005's release-2/3 rows list capability deps only.
- **D-21: holds.** doc-007 § Decomposition keeps its eight release-3 rows with no queueing statement.
- **No new contradiction** was found across doc-001..007 in the touched places.

### RA-15
Severity: minor
Affects: Gherkin steps and scenario titles of doc-002 US-1..US-12, doc-003 US-1..US-3 and doc-005 US-1..US-4 (about 150 lines of "a/the team member"), and doc-002's BPMN node A
Problem: this is the step-level half of RA-9's required resolution, which the fix pass left out. A step like "When a team member opens team A's boards, then team B's" (doc-003 US-1) still reads as if team membership mattered. No step asserts that a non-team member is refused. The actor is a permitted subset, and the access rule is stated and tested elsewhere: doc-001 § Roles and US-7, doc-003 § Roles, doc-006 § Roles. Nothing is wrong in behaviour, only in wording.
Required resolution: either a mechanical sweep ("a/the team member" → "a/the member" in doc-002..005 steps, keeping "team A's …" and doc-004 US-7's capacity per team member), or accept the wording.
Status: resolved
Resolution: mechanical sweep, 147 steps in 20 files.

### RA-16
Severity: minor
Affects: doc-006 BR-M1-9, BR-M1-14, BR-M1-18, the Context preamble and Open questions; doc-003 BR-M4-2; the preambles of doc-001, doc-002, doc-004 and doc-005
Problem: the three calls were human-confirmed by D-23, but their rules still cited only D-6, D-7 or D-4/D-5, which are silent on restore, the automatic area and the Эпики/Фичи column. The specs' "binding input" preambles also stopped at doc-010 D-20, although D-23 and D-24 now land in them.
Required resolution: cite D-23, and extend the preambles.
Status: resolved
Resolution: the source cells now read "doc-010 D-6, D-7, D-23 (b)" (BR-M1-9), "D-7, D-23 (b)" (BR-M1-14), "D-6, D-23 (a)" (BR-M1-18) and "D-4, D-5, D-23 (c)" (BR-M4-2). The preambles of doc-001, doc-002, doc-004, doc-005 and doc-006, and the Open questions of doc-001 and doc-006, now read D-4..D-24 (doc-006: "D-6..D-13 and D-23").

### RA-17
Severity: minor
Affects: doc-004 US-8, scenario "A change made today moves today's point only"
Problem: the fix pass added "And closing a «Задача» alone moves no point" as a Then of a scenario whose When had already set «Оставшаяся работа» to 0. No action in that scenario exercises the claim, so it cannot be tested as written.
Required resolution: give the claim its own action.
Status: resolved
Resolution: the line becomes its own scenario, "Closing a «Задача» alone moves no point". A «Задача» with «Оставшаяся работа» 4 is moved to «Закрыта» without touching the field, and no burndown point changes (doc-002 BR-M3-18). This change is covered by doc-004's existing `[doc-010] amended` Log line ("stories US-1..US-14").

### RA-18
Severity: minor
Affects: doc-001 § MVP boundary and release plan (preamble, M1 row)
Problem: the preamble named the doc-010 sources of the MVP rows as "D-4..D-9, D-14", although the rows also carry D-10 and D-13. The M1 row's bare "(D-10)" is doc-001's own D-10, which does not mention the Keycloak realm administrator. This is the doc-001 side of review.md R-20.
Required resolution: align the citations.
Status: resolved
Resolution: the preamble reads "doc-010 D-4..D-10, D-13, D-14", and the M1 row reads "(D-10, doc-010 D-10)".

### Summary and verdict

RA-1..RA-13 are resolved. RA-14 and RA-15 are accepted risks: RA-14 is size, RA-15 is step wording, with no behavioural effect. RA-16..RA-18 are fixed in place.

Verdict: approved-by-ai. D-21..D-24 hold in doc-001..007, and every open item of round 1 is resolved. The new minors are fixed, except RA-15, which is wording only and put to the human as an accepted risk (or a sweep before gate-3). The edits made in this check fall under each amended task's existing `[doc-010] amended` Log line, with two exceptions:
- doc-002's Log line does not list its context paragraph;
- the Log lines of doc-001 and doc-006 do not list § Open questions.

Their "doc-010 D-…" sentences were already unlisted before this round. The orchestrator adds "context" to doc-002's line and "open questions" to doc-001's and doc-006's; status.md is outside this review's write zone.
