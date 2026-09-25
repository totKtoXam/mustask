# doc-010 — requirements review

## Round 1 — product plan

Scope: `spec.md` (the product plan) and `stories/US-1..US-8`, against
`status.md` D-1..D-20, `cross-review.md` (§ Findings, § Defaults, § Queue
operations), `cross-review/C-scope-queue.md` § ADR debt, `docs/backlog.md` on
`main` (630e0d0, 25 lines), `scripts/queue.sh`, routing.md and numbering.md.
The amended doc-001..007 were read only where the plan cites them; the
amendments themselves are `review-amendments.md`'s subject.

### Decision coverage (D-4..D-20)

| Decision | Reflected in |
|---|---|
| D-4 | § Releases M2; E-1, E-10; US-1, US-3 |
| D-5 | § Releases M2; E-1; US-1 |
| D-6 | § Releases M1, M4, M5; E-2, E-10, E-12, E-15, E-20; ADR row; US-1 |
| D-7 | § Releases M1, M4; E-2, E-10; US-1, US-3 |
| D-8 | § Releases M3; E-3 |
| D-9 | A-1; E-2 deps; ADR rows; US-1 |
| D-10 | § Releases M1; E-2; ADR-0018 row; US-1 |
| D-11, D-12 | E-2, E-5, E-8, E-15; ADR-0018 row; codes table; US-5 |
| D-13 | § Releases M3, R2 M6, R3 M6; E-7, E-15, E-20; US-2, US-5 |
| D-14 | A-3; E-9, E-13; US-2, US-3 |
| D-15 | A-4; E-16; § Release 2 M3; US-6 |
| D-16 | E-15, E-21; US-5, US-8 |
| D-17 | § Amendments, doc-001 |
| D-18 | A-2; § Release 2; PR-5 goes beyond it (R-1) |
| D-19 | E-10 |
| D-20 | codes table (N-9); ADR rows (N-12, N-16); E-1..E-9 (N-20); E-8 (N-22); E-10 (N-30); E-3, E-13, E-15, E-16, E-21, E-23, E-24 and the README note (N-33) |

Every decision is reflected; none is missing.

### Checks that passed

- **Queue simulation.** Each of the 25 lines on `main` is edited by exactly one
  of E-1..E-22, and E-23/E-24 add the epic paragraph and § Next. The adds run
  before the edits that cite their numbers. On today's counters they get
  mod-014, doc-011, feat-013 and feat-014; the `<A-n>` placeholders cover a
  number taken meanwhile. Every commit subject is ≤ 72 characters, no
  description contains " · ", and every flag is one `queue.sh add` accepts.
  Applying the operations gives the target table's deps, epics and sizes,
  with A-2 now L (R-12).
- **Graph.** It is acyclic. No listed code dep is reachable through another
  one. Every non-founder reaches its module's founder (PR-1), and doc-010 is
  listed only where nothing else reaches it. The mermaid edges equal the
  table.
- **Waves and critical path (recomputed).**
  - Waves 1–10 are as tabled. The MVP depth is 7: 5 L + 2 M after doc-010,
    tied through board-backlog-filter → feat-008.
  - The path through mod-001 has 21 stops. MVP = 8 L + 9 M. MS-8's
    feat-012 is the deepest line (9).
  - Exceptions: R-5.
- **Milestones.** MS-1..MS-8 partition the 29 lines with none orphaned.
  Every story's "delivered by" list equals its MS column.
- **ADR debt.** Every row of C § ADR debt has exactly one owner. The
  predicate joins mod-002's ADR-0018 amendment (D-11). The projection's ADR
  is new (D-9). ADR-0023 is unchanged.
- **Deviations named in the task.**
  - feat-003 via mod-005, mod-012 via mod-011, mod-002 without
    doc-006/doc-010: each dropped dep is reachable, so all three are sound.
    D-2's "mod-002 and mod-008 depend on doc-010" still holds transitively.
  - mod-008's added doc-005 is sound: PR-3, since no M6 line precedes it.
  - mod-008's release-2-scope dep is not a capability dep: R-1.

### R-1
Severity: major
Affects: PR-5, A-2 note, A-4 deps, E-15 deps, MS-5, § Releases diagram; doc-002 estimation row, doc-005 mod-008 row
Problem: PR-5 hard-depends mod-008 and the estimation line on doc-NNN-release-2-scope. Through them it also gates mod-011, feat-011 and mod-012, and every release-3 line built on M6. PR-5 cites D-18 as its source, but D-18 says the release-2/3 lines "stay as they are". The human chose Q-14 (a), the status quo, over (b), which held those lines back until the scoping task.
- The dep is a process gate, not a capability, so PR-3 ("every capability a line uses comes from a line it reaches") does not ground it.
- It also cuts against the plan's own OQ-2 default ("priority is enough").
- The amended specs follow D-18 literally. doc-002's estimation row depends on mod-003 only, and doc-005's mod-008 row on feat-003, mod-007 and doc-010.

As written, the plan silently narrows a human choice, and on the day it is applied the queue and the spec rows disagree.
Required resolution: the human answers OQ-4, added to the spec in this round.
- Yes: PR-5 stays as the plan's own call, confirmed at gate-1, and the amendment pass adds doc-NNN-release-2-scope to doc-002's estimation row and doc-005's mod-008 row.
- No:
  - A-4 becomes `--deps mod-003`, and E-15's deps become `feat-003,mod-007,doc-005`;
  - the table and the graph drop the two edges;
  - PR-5 becomes a priority note under PR-6, and MS-5 lists mod-008 alone.

The reviewer leans to yes. The gate costs no depth, since the scoping line sits in wave 2. The scoping may also reshape what mod-008 and mod-011 build on: bulk edit over query results, and board configuration on the taskboard.
Status: resolved
Resolution: D-24 — the human kept the gate; PR-5 states it as decided (source D-18, D-24), OQ-4 is removed, and doc-002's estimation row and doc-005's mod-008 row already carry the dep (RA-4).

### R-2
Severity: major
Affects: § Queue operations E-1..E-8, E-10..E-15, E-17..E-20, E-22; PR-8; untouched descriptions of feat-004, feat-005, feat-009, feat-010, feat-011, mod-009, mod-012
Problem: after N-32, a bare BR-n is doc-001's rule. The line texts still cite module rules without a prefix:
- E-4, "with BR-8's refusals": that is now doc-001 BR-8, tags are project-scoped; BR-M3-8 was meant.
- E-7, "no cascade (BR-9)": that is now doc-001 BR-9, the current iteration; BR-M3-9 was meant.
- E-13 and E-19 carried "BR-9" and "BR-17".
- Seven lines the edits leave untouched keep ids from before the renumbering: feat-004 BR-9..BR-11, feat-005 BR-8, feat-009 BR-13, feat-010 BR-12, feat-011 BR-15, mod-012 BR-16, mod-009 BR-9.
- Decision ids were bare too (D-6, D-7, D-10, W-2, W-10 …). doc-001 has its own D-1..D-12; its D-6 is "no Reason". This goes against the N-32 default: "decision ids are cited with their task id outside their task".

`new-task.sh` copies a line into its task character by character, so the task would read the wrong rule.
Required resolution: qualify every id in the line text, and state the rule.
Status: resolved
Resolution:
- E-1..E-8, E-10, E-12, E-13, E-15 and E-19 are rewritten with qualified ids.
- E-11 now gives feat-004's whole description.
- E-14, E-17, E-18, E-20 and E-22 carry the id replacements for the untouched lines, so there are still 24 edits.
- A note above E-1, and PR-8, state the rule.

### R-3
Severity: major
Affects: US-1; doc-006 BR-M1-9, BR-M1-12, BR-M1-14
Problem: in US-1 the administrator adds the team «Платформа», then creates an area «Платформа» and makes it the default.
- Under amended BR-M1-9, a later team "comes with a child area of the root named after it, owned by it and set as its default area".
- Sibling names are unique (BR-M1-12), so the second «Платформа» is refused.
- The MS-1 demo therefore cannot pass as written (PR-12).
Required resolution: the demo follows BR-M1-9.
Status: resolved
Resolution: US-1 now checks the area each team comes with, and that the first team's default is the root. The behaviour itself is one of the amendment pass's calls that await confirmation (R-7, OQ-3 (b)).

### R-4
Severity: major
Affects: US-6; doc-002 BR-M3-18; doc-004 BR-M5-16, US-8
Problem: US-6 says that when a «Задача» moves to «Закрыта», "the burndown drops today by that task's Remaining Work". The specs say otherwise:
- doc-002 BR-M3-18: "closing a Задача leaves its «Оставшаяся работа» as entered".
- BR-M5-16 and doc-004 US-8 sc.1 define each point as the «Оставшаяся работа» of the team's items in the iteration.

Closing a task alone therefore changes no point, and the demo step fails. US-6 also named the fields in English, although doc-002 § Release 2 gives the Russian UI labels.
Required resolution: a step that holds under both rules, using the UI labels.
Status: resolved
Resolution: US-6 now sets «Оставшаяся работа» to 0 before closing the task, and uses «Оценка в пунктах», «Исходная оценка», «Оставшаяся работа» and «Вид работ». Outside this artifact: doc-004 US-8 sc.2 ("closes a task today → today's point drops") conflicts with BR-M3-18 in the same way. That is for review-amendments.md.

### R-5
Severity: minor
Affects: § Critical path and parallelism
Problem: two numbers are off.
- "mod-007 follows mod-003 with two waves of slack" is wrong. mod-007 (wave 5) must merge before feat-009 (wave 7, after mod-006 in wave 6), so it has one wave of slack.
- "42 stops" assumes every L has a gate-2. An L with no screen has none, and the projection may be one.
Required resolution: correct both.
Status: resolved
Resolution: the text now reads "one wave of slack (it must merge before feat-009)" and "up to 42 stops".

### R-6
Severity: minor
Affects: OQ-1, OQ-2
Problem: neither question is well posed.
- **OQ-2** named only mod-008 and mod-013. But the estimation line is ready from wave 5, and mod-011, mod-009, feat-005 and mod-010 in wave 8, all before feat-008 and feat-010 merge. "The MVP's last lines" is not a set anyone can apply.
- **OQ-1** asked for a scoping line "before" the release-3 lines, while its default queues nothing and gates nothing. mod-013 is ready in wave 7, long before MS-6.
Required resolution: name the lines, and separate "scoping line" from "gate".
Status: resolved
Resolution:
- OQ-2 lists the lines that become ready early, the five lines that would take the dep and the six MVP terminal lines.
- OQ-1 separates the line from the gate and names the five release-3 lines a gate would touch.

### R-7
Severity: minor
Affects: § Open questions; US-1; § ADR debt
Problem: status.md's Log (2026-09-25) records three calls by the amendment pass that are "to confirm at gate-1":
- a deleted team cannot be restored and its name is freed;
- a later team comes with its own default area;
- «Разрешена» gets its own column on the Эпики and Фичи boards.

The plan is the gate-1 index, yet it listed only OQ-1 and OQ-2, while US-1 depends on the second call and § ADR debt on the first.
Required resolution: put the three calls to the human.
Status: resolved
Resolution: OQ-3 is added, with the default "keep". The answer is the human's.

### R-8
Severity: minor
Affects: § ADR debt, row "Soft deactivation by status"
Problem: the row requires "restore, names reserved" for organization, project and team. doc-006 BR-M1-18 and doc-001 § Deletion lifecycle give a team no restore and a freed name, and an iteration likewise. The owning line would be told to design a restore that the specs forbid.
Required resolution: state the variants per entity.
Status: resolved
Resolution: the row now gives each entity's variant and cites doc-001 § Deletion lifecycle.

### R-9
Severity: minor
Affects: § Amendments to the merged specs
Problem: the summary disagrees with the amended specs in three places:
- the doc-004 row says "working days and days off go to the org admin". doc-004 BR-M5-15, doc-001 § Roles and US-6 give days off to a member.
- the doc-006 row puts team deletion in BR-M1-9. It is the new BR-M1-18.
- the doc-001 row says "the ADR list = § ADR debt". doc-001's table lists only the founders' ADRs: no mod-012, and no release-2/3 rows.

The doc-005 row's claim that the "decomposition rows follow § Normalized queue" is also false for mod-008's deps (R-1).
Required resolution: correct them.
Status: resolved
Resolution: the three facts are corrected and the doc-005 claim is qualified. The section's future is R-15.

### R-10
Severity: minor
Affects: § Normalized queue; doc-002 feat-003 row, doc-004 mod-012 row, doc-006 mod-002 row
Problem: three amended rows list more deps than the target table:
- doc-002's feat-003 row: mod-003 and mod-005, D-14's literal text;
- doc-004's mod-012 row: the estimation line on top;
- doc-006's mod-002 row: doc-006 and doc-010 on top.

The reductions are sound (see § Checks), but nothing said which side wins.
Required resolution: say which one is normative.
Status: resolved
Resolution: a reading note under § Normalized queue now says that the table's deps are normative for the queue, and PR-3's minimal set beats an unreduced row. The PR-5 mismatches belong to R-1.

### R-11
Severity: minor
Affects: doc-001 § MVP boundary and release plan; spec § Releases and milestones
Problem: a spot check of doc-001 against the plan shows that the MVP rows M0–M6 and the release-3 list match in substance. Release 2 does not.
- doc-001 lists notifications under M1, in the very sentence that leaves their owner to doc-NNN-release-2-scope.
- The plan lists notifications as cross-module and unsettled (D-18).
- doc-001 is the umbrella every analysis starts from (D-17), so the release-2 analyst would read the M1 owner as settled.

Separately, the plan's "doc-001 must state this content" did not say whether the MS-n milestones belong in doc-001.
Required resolution: the amendment pass moves notifications out of doc-001's M1 release-2 list and into its cross-module clause. The M1 audit log, BR-M1-15, may stay under M1, as in the plan. This cannot be edited from here.
Status: resolved
Resolution: doc-001's release-2 paragraph drops notifications from M1 and its cross-module clause says they belong to no module until doc-NNN-release-2-scope settles them; the M1 audit log stays under M1, and the milestones live only in the plan.

### R-12
Severity: minor
Affects: A-2, § Normalized queue row, Totals, PR-2
Problem: A-2's size M rests on a clause of PR-2: "an analysis line is M unless it spans every module". Nothing in routing.md grounds that clause, and it contradicts the precedents:
- doc-001 spans every module and is M;
- doc-002, doc-004 and doc-006 each cover one module and are L.

doc-NNN-release-2-scope decides per-project and per-area permission overrides. That is an authorization threshold, the one that made doc-006 L. It must also name the owner of the instance-rights ADR (§ ADR debt), so there is doubt, and doubt means L. Its description also omitted that ADR obligation (PR-4) and the per-area overrides.
Required resolution: size it by routing.md and state the obligation.
Status: resolved
Resolution:
- A-2 is now `--size L`, with the reason in its note; the table row and the Totals sentence follow.
- PR-2's clause is replaced.
- A-2's description names the ADR obligation and the per-area overrides.
- The human still confirms the size when the line is taken.

### R-13
Severity: minor
Affects: US-2, US-3, US-4, US-8
Problem: four demos do not check what their milestones claim.
- **US-2.** MS-2 claims purge (D-13), but the demo stops at the offer. It does not show the purge's effects on other lines: the Drive files (N-22) and "parent purged" in the child's history (BR-4).
- **US-3.** The filter step filtered, then cleared, an unfiltered order. D-14's filtered-order scenario, a reorder while filtered, was never exercised.
- **US-4 and US-8.** "Every MVP / release-3 capability … has now been shown" is false: project deactivation, «Шаги воспроизведения», iteration removal and In Group appear in no demo. It is also not testable, since milestones are smoke demos (PR-12).
- **US-8.** "A tree query of work items and their direct links" mixes two query types.
Required resolution: the demos exercise what each milestone claims.
Status: resolved
Resolution:
- US-2's guard scenario now performs the purge and checks the list, the child's history and Drive.
- US-3 reorders inside a filtered view.
- US-4 and US-8 now end with "the earlier milestones' demos still pass".
- US-8 uses a tree query.

### R-14
Severity: minor
Affects: E-2, E-10, E-16, A-4, § Releases M4, table rows feat-007 and doc-NNN-release-2-scope
Problem: some line texts state less than their specs (PR-8), and two table cells are off.
- **E-2** left out two things. One is the organization settings screen, which doc-006's row gives mod-002 and E-8's mod-004 extends. The other is membership who/when (BR-M1-16).
- **E-10 and § Releases M4** hid only a deleted team's boards. D-6 hides its backlog entries too.
- **E-16** grounded mod-011's L in "founds the sprint-hub sub-slice". C-17 rejected that ground because mod-011 has no entity of its own. doc-004's row gives the real one: it changes M4's BR-M4-1.
- **feat-007's title** lacked the `*` although E-6 rewords it.
- **The release-2-scope row's MS cell**, "R2 gate", disagreed with MS-5's "(entry)".
- **A-4's Activity** lacked its UI label «Вид работ».
Required resolution: make the line texts complete and the cells consistent.
Status: resolved
Resolution: all six points are corrected.

### R-15
Severity: minor
Affects: spec.md, § Amendments to the merged specs
Problem: spec.md is about 420 lines, against the index budget of about 250 (01-business-analysis.md). Most of it is load-bearing for a single artifact: the target table, the queue operations, the graph and the ADR debt. § Amendments (about 80 lines) is not. It restates changes already committed to doc-001..007, each with its own Log line (PR-7), and it had already drifted in four places (R-9, R-1).
Required resolution: reduce § Amendments to one row per spec, giving the sections touched and the Log line, and leave the content to the specs. This goes back to the analyst.
Status: resolved
Resolution: § Amendments is one row per spec (summary → the task's status.md Log line and cross-review.md § Amendment map), § Releases points to doc-001 for module scope instead of restating it, and the codes moved to their own § Permission codes; spec.md 421 → 329 lines.

### R-16
Severity: minor
Affects: PR-1; A-1; E-2
Problem: mod-002 "founds M1", but mod-NNN-keycloak-user-projection lands first, because mod-002 depends on it. By doc-001's module map it puts the USER projection in M1, so it opens M1's code slice, and its design fixes the skeleton that mod-002 then extends. PR-1 called it only a sub-slice founder.
Required resolution: say so, so that the projection's design stage knows it sets M1's skeleton.
Status: resolved
Resolution: PR-1 now states it.

### Summary

4 majors: 1 open, 3 resolved. 12 minors: 2 open, 10 resolved.

For the human: OQ-1..OQ-4, of which OQ-4 is R-1. Returned to the analyst: R-15. For the amendment pass: R-11, plus the note in R-4 on doc-004 US-8 sc.2.

Verdict: needs-human-decision. R-1 turns on a human decision (OQ-4, the release-2 gate against D-18's status quo). OQ-1..OQ-3 are also questions only the human can answer. R-11 and R-15 stay open with the amendment pass and the analyst.

## Delta check (after D-21..D-24)

Scope: the resolutions of R-1, R-11 and R-15, D-21..D-24, and the current `spec.md` (329 lines) and stories, as far as the fix pass touched them. This session had no shell, so `git diff 66982c5` was not run. The delta was checked against the current text, section by section, where the resolutions say it changed.

- **R-1 / D-24: holds.** The gate is stated in six places, and they agree:
  - PR-5 states it as decided (D-18, D-24);
  - A-2's note says it gates every release-2 implementation line;
  - A-4 lists `doc-<A-2>` and E-15 lists `doc-<A-2>`;
  - the table rows and the graph edges (r2s → est, mod-008) carry it;
  - MS-5 lists doc-NNN-release-2-scope as its entry;
  - doc-002's estimation row and doc-005's mod-008 row both list doc-NNN-release-2-scope.

  OQ-4 is gone.
- **D-21: holds.** No release-3 scoping line appears in the table or the operations. It is covered in several places:
  - PR-6, § Releases R3 and § Out of scope name the MS-6 trigger;
  - MS-8 is "complete, process customization aside";
  - the ADR row and the codes row name doc-007's rows as unqueued. doc-007 § Decomposition has those eight release-3 rows and says nothing about queueing them.

  PR-6's wording is fixed under R-17.
- **D-22: holds.** No release-2 or release-3 row depends on an MVP line only to wait for the MVP's end. That covers the table, doc-004's mod-011..mod-013 rows and doc-005's rows. mod-011's feat-004 and feat-009 are capabilities.
- **D-23: holds.** The § ADR debt soft-deactivation row matches doc-001 § Deletion lifecycle and doc-006 BR-M1-18:
  - organization and project: restore, with name and key reserved;
  - team: cascade, no restore, name freed, deletion confirmed first.

  US-1 follows BR-M1-9. The specs' source cells now cite D-23 (review-amendments.md, RA-16).
- **R-11: holds.**
  - doc-001's release-2 paragraph lists notifications only among the unowned items, and says they are cross-module and belong to no module until doc-NNN-release-2-scope settles them.
  - The M1 audit log (BR-M1-15) stays under M1.
  - This agrees with doc-006's release-2 paragraph, which names the M1/M3 double claim, and with doc-002 § Out of scope.
- **R-15: nothing normative was lost.**
  - The table has 29 rows: 17 MVP, 6 R2 and 6 R3. They are partitioned by MS-1..MS-8 with no orphan.
  - Each of the 25 existing lines is edited by exactly one of E-1..E-22. The count is 4 adds, 0 withdrawals and 24 edits. The deps in E-2, E-3, E-8..E-10, E-13, E-15 and E-16 equal the table's.
  - The graph edges equal the table. Waves 1–10 recompute. There are 21 stops to MVP, and the MVP is 8 L and 9 M. § ADR debt has 10 rows, each with one owner. PR-1..PR-12 are intact.
  - § Amendments' seven status.md links resolve, and so does cross-review.md § Amendment map.
  - Of the "Scope:" pointers, the release-2 and release-3 paragraphs exist (doc-001 :274, :294). The MVP pointer is fixed under R-20.
- **§ Permission codes: consistent.**
  - Every code of [B § Permission codes](cross-review/B-access-roles.md#permission-codes) is present with B's owner or with N-9's settled one: delete and purge go to mod-005, and `attachment-storage.manage` to mod-004.
  - The two additions are D-13's (`queries.query.purge`, `dashboards.dashboard.purge`).
  - B's `queries.personal.write` is left as "the personal-object code". This is deliberate, because D-12 widens it beyond queries.
  - The M1 codes and the `organizations.team.write` scope equal doc-006's code table.
  - The citations in doc-002 :31 and doc-004 :28, and the B anchor, all resolve.
  - The `process.definition.write` owner is fixed under R-19.
- **doc-004 US-8 vs doc-002 BR-M3-18: holds.** The scenario sets «Оставшаяся работа» to 0 before closing. A verifiability gap in the added line is covered in review-amendments.md (RA-17).
- **Stories US-1..US-8: unchanged in substance.** US-5 and US-6 agree with PR-5, and US-8 with D-21.

### R-17
Severity: minor
Affects: PR-6
Problem: PR-6 said "no release-3 line is gated". Read literally, this contradicts PR-5 and D-24. Five of the six release-3 lines (mod-009, feat-005, feat-006, mod-010, feat-012) reach doc-NNN-release-2-scope through mod-008 or mod-011, and R-1 had named that effect before the human chose D-24. D-21 only means that there is no release-3 gate of its own.
Required resolution: say what is and is not gated.
Status: resolved
Resolution: PR-6 now says "no release-3 line has a gate of its own". It also says that the lines built on mod-008 or mod-011 still wait for doc-NNN-release-2-scope through those deps, and mod-013 does not. The source adds D-24.

### R-18
Severity: minor
Affects: § Critical path, Waves
Problem: the waves note said that a release-2 or release-3 line runs only when no MVP line is ready. PR-6 also puts release 2 before release 3: in wave 7, mod-013 yields to mod-008.
Required resolution: state PR-6's full rule.
Status: resolved
Resolution: the note now reads "a line in a wave runs only when no line of an earlier release is ready".

### R-19
Severity: minor
Affects: § Permission codes, `process.definition.write`
Problem: the owner was "doc-007's unqueued process-customization rows", which is eight rows. The section's own rule is one owning line per code.
Required resolution: name one owner.
Status: resolved
Resolution: the owner is now "the first of doc-007's unqueued process-customization rows to be queued (D-21)".

### R-20
Severity: minor
Affects: § Releases, MVP "Scope:"; doc-001 § MVP boundary and release plan, preamble and M1 row
Problem: the plan pointed to "doc-001 § MVP boundary … as amended per D-4..D-14", but doc-001's preamble listed the rows' doc-010 sources as "D-4..D-9, D-14". The MVP rows also carry D-10 (the Keycloak realm administrator, M1) and D-13 (the organization administrator's purge, M3). Neither citation matched the rows. In the M1 row, a bare "(D-10)" inside doc-001 is doc-001's own D-10, which does not say "Keycloak realm administrator".
Required resolution: align the pointer, the preamble and the M1 citation.
Status: resolved
Resolution: the pointer now names "§ MVP boundary and release plan, the MVP table's rows M0–M6, as amended per D-4..D-10, D-13 and D-14". doc-001's preamble reads "doc-010 D-4..D-10, D-13, D-14", and its M1 row cites "(D-10, doc-010 D-10)".

### Summary

R-1..R-16 are resolved. R-17..R-20 are minors, all fixed in place by the reviewer. No question for the human is open: OQ-1..OQ-4 are answered as D-21..D-24.

Verdict: approved-by-ai. Each decision D-21..D-24 holds in the plan, and in the rows of the amended specs it touches. The R-15 trim lost no normative content, and the four new minors are fixed.
