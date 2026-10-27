# Review: mod-005-work-item-delete-restore

## Requirements review, round 1

Reviewer: requirements-reviewer (stage 02, clean context, not the spec's author), 2026-10-18, over `15c20a32`. The light route's one round. Read: `spec.md`, `stories/US-1..US-6`; doc-001 BR-4, -6, -10, -11, -13, -14, § Deletion lifecycle, NFR; doc-002 BR-M3-5, -9, -11, -21, -24, US-10; doc-010 D-13, PR-9; feat-001 spec BR-H-4..-8, US-7, D-8, api-contract § 3.3; mod-003 BR-M3-21, -24, api-contract § 0.4; mod-006 § The stack rank; mod-017 BR-OT-12, -13; feat-029 BR-C-19; feat-032 BR-C-1, -5, -7; doc-012 BR-MX-1, -12, -14; ADR-0026, ADR-0028 § 6 rule 8, ADR-0029 § 4. Code checked: `WorkItem.Save`, `WorkItemHierarchy.Write`, `SaveWorkItemHandler`, `WorkItemReadModel`, `IWorkItemTeamOwnership`, `ProjectBacklogRepository`, `WorkItemRepository` (number counter), the board's card move and checklist tick (`useSetWorkItemState`).

Every finding is minor and fixed in place by the reviewer; each is marked `resolved` with what changed. No blocker, no major.

### Rule → story map

| Rule | Where |
|---|---|
| doc-001 BR-6, § Deletion lifecycle (soft, indefinite, admin purge, number reserved) | BR-DR-1, -5, -12, -14; US-1, US-4, US-6 |
| doc-001 BR-10 (Delete in, Restore out, default to the held state) | BR-DR-1, -8, -10; US-3, US-4 |
| doc-001 BR-14 (a) hidden unless a filter names «Удалена» | BR-DR-6, § Surfaces; US-2; filters owed to feat-003, feat-013, mod-008, feat-005 |
| BR-14 (b) keeps area, links, state, restore target; area guard; state removal refused | BR-DR-3, -10; US-1, US-2; state removal owed (R-5) |
| BR-14 (c) only Restore, Purge, clean-up; card move and reorder refused; CSV row; child release | BR-DR-8, -9; US-3, US-5; CSV owed (R-11) |
| BR-14 (d) re-scoped backlog entry on Restore | BR-DR-10 via mod-006 and BR-OT-12; US-4 |
| doc-001 BR-4, NFR Audit (history of delete, restore, purge note) | BR-DR-2, OQ-3 (deferred to feat-015; R-9) |
| doc-001 BR-11 (concurrency is correctness) | BR-DR-11; US-1, US-4, US-6 (R-1) |
| doc-002 BR-M3-5 (number never reused) | BR-DR-14; US-6 (verified: `work_item_counters` survives a purge) |
| doc-002 BR-M3-9 (no cascade) | BR-DR-4, -13; US-5, US-6 |
| doc-002 US-10 (each scenario) | US-1..US-6; Drive refusal owed to mod-004; history to feat-015 |
| doc-010 D-13 (admin purge), PR-9 (later lines) | BR-DR-12, -16; § Owed by later lines |
| feat-001 US-7 re-run through Delete (D-8 OQ-1) | BR-DR-18; US-5 (R-13) |
| ADR-0026 rules 3, 4, 5 | BR-DR-17; § Decomposition (the recycle-bin ADR) |
| ADR-0029 § 4 and "What the delivery lines implement" | § Roles (R-7) |
| mod-017 BR-OT-12; doc-012 BR-MX-12 | BR-DR-9; US-2, US-3, US-4 (R-4) |
| tech-012 m1 | BR-DR-8; US-3 (confirmed in code: `Save` applies the state before `WorkItemHierarchy.Write` reads "deleted") |

### R-1
Severity: minor
Affects: BR-DR-11, NFR Concurrency, US-6
Problem: BR-DR-11 listed only pairs on the same item. The writes of other items that touch a deleted item were unnamed: a parent write naming it as the new parent, a creation beneath it, a child's release or mapping, a team's deletion releasing it and its children, a reorder. One of them is a real trap. A purge rewrites the item and then its children, while a team deletion locks the items it owns by id (`IWorkItemTeamOwnership`), so the two can deadlock and answer a server error. The ADR designs the locks only for the pairs the spec names.
Required resolution: name the cross-item pairs with their outcome (one order, no server error), and name the lock order the purge keeps.
Status: resolved
Resolution: BR-DR-11 adds the cross-item writes. Restore against a child's mapping that saw no parent answers `parent_changed` (BR-H-4). The purge locks its children in ADR-0028 § 6 rule 8's order (work items by id). NFR Concurrency covers these pairs, and US-6 adds purge against team deletion.

### R-2
Severity: minor
Affects: BR-DR-8, § Surfaces and writes, US-3, US-5
Problem: "parent write" covers the form's save as well as the parent route in feat-001's vocabulary (`WorkItemHierarchy.Write` serves both). US-3 «Link writes keep their own refusal» and US-5 said that "any parent on the deleted MSK-US-12" answers `link.item_deleted`. US-3's tech-012 m1 scenario and the table say that a form save of a deleted item answers `workitem.deleted`. The same request had two expected answers.
Required resolution: say which write answers which code.
Status: resolved
Resolution: in BR-DR-8 and the table, the parent route, a creation with a parent and a live item's save naming a deleted parent keep `link.item_deleted`, and any save of a deleted item answers `workitem.deleted`. US-3 and US-5 name the route, and US-3 adds the save case. BR-DR-8 also lists the checks it precedes: values, area, team, parent, state, assignee. The previous wording, "value checks", left the 404s of area and team unplaced.

### R-3
Severity: minor
Affects: § Decomposition
Problem: the line changes merged contracts without naming them. mod-003 api-contract § 0.4 and § 2.4 change, because the 409 now precedes the 400. feat-001 api-contract § 3.3 changes: today a save of a deleted item with a parent answers `link.item_deleted` at step 4. mod-003 § 2.1, the list, stops returning deleted items, and § 2.3 gains the deletion marks. AGENTS.md wants such a change noted. Without the list, stage 4 could miss one of them.
Required resolution: list them for this line's api-contract.
Status: resolved
Resolution: § Decomposition has the paragraph "Merged contracts this line changes".

### R-4
Severity: minor
Affects: BR-DR-8, BR-DR-9, US-4
Problem: BR-DR-8 is a closed list (Restore, Purge, clean-up), and BR-DR-9 named team deletion as the only clean-up. doc-012 BR-MX-12 says that a project–team unlink reassigns the items "in any state", and so does feat-028, which is in flight. As written, this spec forbids that unlink to touch a deleted item. The task also asked about a restore after an unlink.
Required resolution: make the unlink, and later clean-ups such as BR-MX-14's sprint removal, clean-ups of the same kind, and say who tests the unlink on a deleted item, without depending on feat-028's unmerged details.
Status: resolved
Resolution: BR-DR-9 now names the unlink and later clean-ups. Of this line and feat-028, the one that merges second carries the test. A restore then returns the item to the destination as stored (BR-DR-10 "as stored").

### R-5
Severity: minor
Affects: BR-DR-10, § Owed by later lines, US-4
Problem: three Restore cases were open. (1) The remembered state stops being a state of the type. BR-14 (b) and doc-007 BR-M2-6 forbid that, but no line owed it. (2) Whether Restore can be refused for a cycle or the depth bound. It cannot: no write adds a link to or from a deleted item, and `AncestryAsync` walks stored links whatever the state. The criterion was missing, though. (3) For a live item that names a bad state, the order of `not_deleted` and `state_not_allowed`.
Required resolution: state all three.
Status: resolved
Resolution: BR-DR-10 states (1), (2) and the order: address, then `not_deleted`, then `state_not_allowed`. The release-3 process-customization line gets an owed row, and US-4 adds the order case.

### R-6
Severity: minor
Affects: BR-DR-13, US-6
Problem: a purge of a parent whose child is itself deleted was unspecified. The purge then changes a deleted item, which BR-DR-8's list allows only implicitly, and nothing said what a later restore of that child gives.
Required resolution: state it.
Status: resolved
Resolution: BR-DR-13: deleted children also lose the link, stay deleted and are restored with no parent. US-6 adds MSK-BUG-4 under a purged MSK-FEAT-6.

### R-7
Severity: minor
Affects: § Roles, § Decomposition
Problem: ADR-0029 § 4 says that the line that ships removal places `workitems.workitem.delete` in the matrix. The spec said "proposed …, applied by mod-019", so nobody wrote the rows into an ADR, and an accepted ADR is not edited (docs/adr/README.md rule 3).
Required resolution: the recycle-bin ADR places both rows as an amendment, and mod-019 applies the scoped columns.
Status: resolved
Resolution: § Roles and the ADR's scope in § Decomposition both say so.

### R-8
Severity: minor
Affects: US-1, US-4, US-6
Problem: US-1 «A failed delete leaves the card as it was» covered "a refusal", and then the item was "still editable". For 409 `…deleted` the scenario before it says that the card turns read-only. The two contradicted each other, and a 404 or 403 matched neither. Restore and Purge had no failure criterion at all. A double activation would send a second request whose 409 or 404 shows an error after a success. Unsaved edits on the card were not addressed: there is no leave guard (mod-015 design § 9.7), so a deletion silently drops them.
Required resolution: scope the failure scenario, add the same for Restore and Purge, one request per confirmation, and a rule for unsaved edits.
Status: resolved
Resolution: US-1's failure scenario now covers no answer or a server error, and it routes 409, 404 and 403. US-1 adds «Unsaved edits do not survive a deletion», where the confirmation ends «Несохранённые изменения будут потеряны.»; design may reword it. US-1 also adds «One confirmation sends one request». US-4 and US-6 add failure and double activation.

### R-9
Severity: minor
Affects: OQ-2, OQ-3
Problem: OQ-3 (A) understated what the human accepts. Before feat-015 a restore records no who, and a restore clears the deletion's who and when. So neither act is ever attributable, against doc-001 NFR Audit and doc-002 US-10's "recorded in history". OQ-2 (A) named the consequence for Restore only: the admin can purge only what they can reach, so purge has the same limit.
Required resolution: put the full consequence in each option the human decides.
Status: resolved
Resolution: OQ-3 (A) and OQ-2's consequence now say it. Both stay "for the human at gate-1".

### R-10
Severity: minor
Affects: BR-DR-16, US-1, US-6
Problem: three criteria could not be tested as written. BR-DR-16's "data keyed to a work item" named no way to enumerate it. US-6's "no later filter finds it" is about a filter that does not exist. US-1's "nothing has run against it" is a negative with no observable.
Required resolution: make each one checkable.
Status: resolved
Resolution: BR-DR-16 says that the check enumerates the data model's references to a work item, removed, cleared or listed as kept; the recycle-bin ADR names how. US-6 checks "no stored data references it" (BR-DR-16's check). US-1 checks against the test's clock, the item unchanged, and no scheduled or background job registered.

### R-11
Severity: minor
Affects: § Owed by later lines, § Out of scope
Problem: three items were missing. BR-14 (c)'s "a CSV row targeting it is rejected" was owed to nobody (feat-005). feat-021 needed to know that Delete, Restore and Purge stay outside the token (BR-DR-11). Notifications were neither in scope nor out of it.
Required resolution: add them.
Status: resolved
Resolution: the feat-005 and feat-021 rows are extended. Out of scope adds notifications: none exist (doc-010 D-18).

### R-12
Severity: minor
Affects: BR-DR-7, US-1, US-2
Problem: four defects. (1) US-2 counts 9 items and 8 rows, but "Common data" holds 8 items in «Платформа», so the numbers are unverifiable. (2) The US-1 confirmation says that children «показываются без родителя» while the parent is deleted, but the child's form shows the deleted parent (BR-H-8). Only backlogs show them unparented. (3) BR-DR-7 shows "who deleted it", but not for a deleter who has left the organization or been disabled, nor for an item with no marks. (4) US-2 says «Восстановить» is "one click" away, but it opens a confirmation.
Required resolution: fix the data and the copy, and state the deleter's display.
Status: resolved
Resolution: US-2 now has 8 items and 7 rows. The copy is «…в бэклогах они показываются без родителя.». BR-DR-7 shows the deleter as «Исполнитель» names a user, with BR-13's markings, and no line for an item with no marks. US-2 says "offers «Восстановить»".

### R-13
Severity: minor
Affects: BR-DR-18, US-5
Problem: the re-run of feat-001 US-7 had two gaps. US-7 «A deleted child takes no link write» covers set, replace and clear; US-5 covered set only. US-7 «…unparented on the backlog» checks that the «Родитель» cells read «Нет»; US-5 did not. BR-DR-18 did not say whether the SQL set-up tests are replaced or kept.
Required resolution: full coverage, and the set-up rule.
Status: resolved
Resolution: US-5 adds replace and clear and the «Нет» cell. BR-DR-18: the set-up becomes a Delete, and only the tests of an item with no marks keep the direct write.

### Checked, no finding

- Hidden surfaces against the code. The list reads every state today (`GetPageAsync`), and this line adds the filter. Parent candidates, «Дочерние элементы», backlogs, boards and checklists already exclude the Removed category. A team's owned count and release take any state.
- Answer order. The save resolves the address (404) before the values (400), so "after the address's not-found, before the body" fits the handler. The 403 for `workitems.link.write` stays first, so Rita's 403 (US-3) holds.
- A reorder racing a delete or a restore serializes as reorder-first. A place held by a departed item is tolerated and dropped by mod-006's next reorder, so neither order breaks.
- Out of scope, or owed to later lines and named: sprints, search, notifications and bulk actions. Covered by the stories: another organization's item, deactivated containers, a repeated delete and number reuse.
- Size: an L of six stories, 186 lines in the spec and at most 107 in a story after this round, within the budgets. No story is spun out.

### Open questions for the package

OQ-1 (who deletes and restores), OQ-2 (the way into the bin before feat-003), OQ-3 (history before feat-015) and OQ-4 (the purge confirmation) each read "decided: A (recommended) — for the human at gate-1". They travel with the gate-1 + gate-2 package. No new question arose: every finding had a resolution that the rules already fix.

### Verdict

**approved-by-ai.** All 13 findings are minor and resolved in place, and none is left for the analyst. `status.md` is the orchestrator's (task rules), so this run leaves it unedited. The verdict and the four open questions go to the package.
