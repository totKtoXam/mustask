# doc-017 review

## Round 1 — requirements

Reviewer: requirements-reviewer, clean context. Read: spec.md, stories US-1..US-10,
input-brief.md, status.md, ADR-0039, ADR-0040; checked against doc-005 BR-M6-1,
-8, -12; feat-022 BR-M3-30, -31, -35, -37, -40, D-1; mod-005 BR-DR-1, -8, -9,
-12..-17; doc-010 D-18, D-21, PR-5/PR-6; ADR-0029 § 4; docs/error-codes.md; the
queue lines of feat-005, feat-053, feat-015, feat-016, feat-002, mod-004,
mod-008, feat-047.

### Coverage: the ask and the brief

| Source | Covered by |
|---|---|
| Ask: mapping | BR-IE-4, -5; US-1 |
| Ask: conversion | BR-IE-7..-9; US-2, US-3 |
| Ask: the constructor (templates) | BR-IE-18; US-7 |
| Ask: everything else from the file | § Decomposition, § Out of scope, OQ-4..-11 |
| Outline 1–10 | BR-IE-2/-3, -1, -4, -7, -8/-9, -10, -13, -14..-17, -18, -19/-20 |
| Columns Id, Проект, Заголовок, TaskType, Changer, Статус, Priority, Оценка, Ссылка | BR-IE-10, -7, -8, -24; first transfer |
| Теги, Комментарии, Team, Вес A, budget line | owed by feat-016, feat-002, participants, custom fields (OQ-11) |
| Срок, Вес D | OQ-5 line |
| Баг репорт, TG_*, Логи, weekly sheets, Дашборд | § Out of scope |
| Git branches (0 filled), TestSheet | not moved (brief); no rule needed |

| Rule | Stories |
|---|---|
| BR-IE-1 | US-1 |
| BR-IE-2, -3, -4 | US-1 |
| BR-IE-5 | US-1, US-10 |
| BR-IE-6 | US-5 |
| BR-IE-7 | US-2, US-7 |
| BR-IE-8, -9 | US-3, US-2 |
| BR-IE-10 | US-4, US-6, US-7, US-9 |
| BR-IE-11 | US-4, US-5, US-8, US-9 |
| BR-IE-12 | US-2, US-4, US-5, US-6 |
| BR-IE-13 | US-1, US-4 |
| BR-IE-14 | US-4, US-5, US-6 |
| BR-IE-15 | US-5 |
| BR-IE-16, -17 | US-6 |
| BR-IE-18 | US-5, US-7 |
| BR-IE-19, -20, -22 | US-8 |
| BR-IE-21 | US-1, US-8 |
| BR-IE-23 | US-5 |
| BR-IE-24 | US-9 |
| BR-IE-25 | US-10 |

### R-1
Severity: major
Affects: BR-IE-16, US-6, ADR-0040, OQ-3; mod-005 BR-DR-12
Problem: Undo purges an untouched created item that is live. mod-005 BR-DR-12, approved, says "only of an item in «Удалена» … A live item is never purged". The spec contradicted it silently; the run line would have met it at its own gate.
Required resolution: State the amendment explicitly, name the purge path the undo uses, and let the human decide it with OQ-3.
Status: resolved
Resolution: BR-IE-16 amends BR-DR-12 for this one caller and keeps BR-DR-12..-16's purge path (log line included); OQ-3 A names the amendment; ADR-0040 decision 4 says it; the run line's L is justified by it; feat-047 gains the undo's purge record; US-6 has a scenario.

### R-2
Severity: major
Affects: BR-IE-12, ADR-0040 decision 1, US-5; feat-022 BR-M3-30, BR-M3-40
Problem: feat-022 says the create takes none of its fields and ignores them. An import that "writes as the form writes" would create an item with no «Вид работ», «Исходная оценка» or dates, then need a second import to set them.
Required resolution: Define the imported create as create plus save in one row transaction, leaving the dialog's rule unchanged.
Status: resolved
Resolution: BR-IE-12 and ADR-0040 decision 1 say so; US-5 has a scenario.

### R-3
Severity: major
Affects: BR-IE-7, BR-IE-12, US-2, ADR-0040
Problem: "A new item may start in any state of its type", and a state recoding offers every state of the type. That includes «Удалена», which mod-005 BR-DR-1 makes reachable only by Delete. The sheet's «11. Отменена» invites that mapping.
Required resolution: Exclude the Removed category from the recoding targets and from the creation state.
Status: resolved
Resolution: BR-IE-7 and BR-IE-12 exclude «Удалена»; ADR-0040 decision 1 likewise; US-2 has a scenario.

### R-4
Severity: major
Affects: BR-IE-16, US-6, ADR-0040 decision 4
Problem: Undo was undefined for several cases. One is an updated item that is now in «Удалена», since writes to it are refused (BR-DR-8). Others are an item purged since and an item in a deactivated project (BR-DR-17). The last is an old value that no longer passes the form's checks, such as an owning team unlinked or an assignee who lost access. The implementer would have to invent the behaviour.
Required resolution: One rule for each case.
Status: resolved
Resolution: BR-IE-16: such items are left and listed, and an old value is restored only while it passes BR-IE-12. ADR-0040 decision 4 gets the same rule, and US-6 gets two scenarios.

### R-5
Severity: major
Affects: BR-IE-11, BR-IE-16, US-5, US-10; the enrichment re-import plan (§ Decomposition)
Problem: Update semantics for fields that hold several values (tags, links, participants) were not stated, so replace and merge were both possible. In the same way, a mapped «Комментарии» would add the same comment on every re-import. The enrichment plan depends on re-importing the same sheet repeatedly. Undo removed added links and tags but said nothing of participants.
Required resolution: Choose one semantics, state how comments avoid duplicates, and include participants in undo.
Status: resolved
Resolution: BR-IE-11 now says these fields are merged (values added, none removed, so «Очистить» is not offered for them). A comment is added on update only when no comment of the same text exists. BR-IE-16 now removes the participants the batch added, ADR-0040 decision 1 records the merge, and US-5 has a scenario. If the human wants "replace" so that removals in the sheet carry over, that is a later option on the mapping.

### R-6
Severity: major
Affects: BR-IE-19, US-8
Problem: "The exported file comes back in by key: every unedited row is «Без изменений»" was false for items that carry no key of the template's source (created by hand, or from another source). Their key cell is empty, so on re-import they create duplicates under «Строки без ключа» = «Создавать».
Required resolution: Say what such rows export as, warn before export, and narrow the round-trip criterion.
Status: resolved
Resolution: BR-IE-19 now exports these rows with an empty key cell and the dialog counts them. US-8's round-trip scenario now covers keyed items only, and a new scenario covers the count.

### R-7
Severity: major
Affects: § Decomposition (`mod-NNN-process-custom-fields` "queued now"), OQ-4 A; doc-010 D-21
Problem: Queuing doc-007's custom fields row now, without custom types, re-decides the human's doc-010 D-21 ("doc-007's process-editor rows stay unqueued"). The spec did this in a table cell, not as a question.
Required resolution: A human decision.
Status: open
Resolution: Added as OQ-11 (A: queue it now, re-deciding D-21 for this row; B: keep D-21). The decomposition row now reads "queued now if OQ-11 A" and names `process.definition.write`, which doc-010 assigns to the first doc-007 row queued.

### R-8
Severity: minor
Affects: BR-IE-17, US-6
Problem: US-6 says a deleted created item "counts as changed (BR-IE-17)", but BR-IE-17's list did not include delete or restore. System clean-up (BR-DR-9: a team deleted or unlinked) was not addressed either.
Required resolution: Complete the list.
Status: resolved
Resolution: BR-IE-17 now includes deletion and restore and excludes system clean-up.

### R-9
Severity: minor
Affects: BR-IE-7, US-2
Problem: Only a deactivated project was covered for a saved recoding entry whose target stopped qualifying. A member who left and a team deleted or unlinked were not.
Required resolution: A general rule.
Status: resolved
Resolution: BR-IE-7 now covers every such entry: it is marked in the table and the rows that reach it are errors with the form's text. US-2's scenario now shows the mark.

### R-10
Severity: minor
Affects: BR-IE-14, US-5
Problem: The background run did not say what happens when the importer stops being the administrator mid-run, or when the organization is deactivated.
Required resolution: State it.
Status: resolved
Resolution: BR-IE-14 now writes each row under the importer's current rights and stops the run as «Прерван» in either case. US-5 has a scenario.

### R-11
Severity: minor
Affects: BR-IE-14, BR-IE-18
Problem: "The same template version" was used without saying what a version is. Concurrent saves by two administrators were not addressed.
Required resolution: Define the version.
Status: resolved
Resolution: BR-IE-18 now says each save makes a version, the later save wins, and an earlier dry run goes stale.

### R-12
Severity: minor
Affects: § Stories ("owed by"), § Decomposition deps of the gap lines
Problem: The gap lines (participants, tags, comments, custom fields) depend only on their own modules, so they may merge before the constructor. "That line adds it to the mapping catalogue" then has nothing to add to.
Required resolution: Use the merge-second rule (as mod-005 BR-DR-9 does).
Status: resolved
Resolution: § Stories now says that, of a gap line and the import line, whichever merges second adds the field to the catalogue, to undo or to export.

### R-13
Severity: minor
Affects: § Decomposition queue changes; BR-IE-1; doc-005 BR-M6-12
Problem: The reworded feat-005 exports by a template. doc-005 BR-M6-12, which was not superseded, says CSV export uses the query's column set. mod-004 and feat-047 also needed clauses (attachments count as a change after an import; the undo's purge record). feat-016 and feat-002 needed the merge and change-after-import clauses that ADR-0040's consequences require.
Required resolution: Supersede BR-M6-12's sentence and complete the queue changes.
Status: resolved
Resolution: BR-IE-1 now supersedes BR-M6-12's last sentence. The queue changes now include mod-004, feat-047 and the extended feat-016 and feat-002 clauses.

### R-14
Severity: minor
Affects: § Decomposition sizes
Problem: The key line's "the ADR is accepted at this task's gate-1" could be read as meaning the feat's own gate, which is an L threshold. The run line's L gave no routing threshold.
Required resolution: Make both sizes precise.
Status: resolved
Resolution: The key line now says ADR-0039 is decided at doc-017's gate-1. The run line's L now cites the change to BR-DR-12's meaning ("breaking" threshold).

### R-15
Severity: minor
Affects: § Decomposition, first-transfer set (OQ-7)
Problem: Is the first-transfer set minimal? feat-053 is a convenience: its own line says a user can be found after signing in once. The set is sufficient: projects come first by hand (OQ-6 A), and states are recoded (OQ-4 A). The brief's tags (feat-016) are rightly left out: they come by an enrichment re-import, and feat-016 waits on feat-013 and feat-042.
Required resolution: Name the alternative.
Status: resolved
Resolution: The feat-053 row now names "each of them signs in once" as the alternative. The "yes" mark stays.

### R-16
Severity: minor
Affects: § Roles, US-7, ADR-0040 decision 5
Problem: Any «Читатель» could read a template's settings, which include the people recoding table (names and emails from the source). Export needs only the name and the columns.
Required resolution: Narrow the read.
Status: resolved
Resolution: § Roles, US-7 and ADR-0040 decision 5 now limit `imports.template.read` to the name and the export columns. The settings are read under `imports.template.write`.

### R-17
Severity: minor
Affects: BR-IE-15, NFR privacy
Problem: The batch kept the values it overwrote forever, although they are needed only for the 30-day undo.
Required resolution: A retention rule.
Status: resolved
Resolution: BR-IE-15 now drops the overwritten values once undo is no longer possible. The report stays.

### R-18
Severity: minor
Affects: US-8, § Stories, OQ-10, OQ-7
Problem: Four small points. US-8's visibility clause depends on feat-041, which is not built yet. § Stories decided OQ-9 in advance ("an analysis line of their own"). The brief's "HotFix → priority" cannot work under BR-IE-4's one-source rule, and OQ-10 did not say so. The new lines' release placement under doc-010 PR-6 was not stated.
Required resolution: Mark, defer or state each.
Status: resolved
Resolution: US-8 now marks the clause as owed by feat-041. § Stories now defers to OQ-9. OQ-10 now explains why. The queue changes and OQ-7 A now give the lines no release label and put them in Next.

### Verdict

needs-human-decision: OQ-1..OQ-11 are human decisions, and R-7 stays open as OQ-11. No blocker. Every other finding is fixed in spec.md, the stories and ADR-0040. Stories stay within ~150 lines, and spec.md within ~250.
