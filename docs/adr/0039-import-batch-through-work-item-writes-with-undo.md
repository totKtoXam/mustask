# ADR-0039: Write an import row by row through the work items module's own writes, and record each import as a batch that one undo can reverse

- Status: accepted (by the human in chat, at doc-017 gate-1, 2026-11-10)
- Date: 2026-11-10
- Context: `doc-017-import-export-constructor`, stage 1: [spec](../features/doc-017-import-export-constructor/spec.md) § Roles, BR-IE-12..-17, BR-IE-23, US-5, US-6, OQ-1, OQ-3. It supersedes doc-005 BR-M6-8's write rules for import and builds on ADR-0011 (locks), ADR-0013 (intent statuses and the outbox), ADR-0027 and ADR-0029 (codes), ADR-0030 (the purge) and ADR-0038 (the scoped «Менеджер»). It goes to the human at doc-017's gate-1; only the human sets `accepted`. It is implemented by `mod-024-import-constructor` (the codes, the dry run) and `feat-067-import-run-and-undo` (the batch and the undo).

## Problem

An import writes up to 5 000 work items in one go, owned by another module (M3), and must apply every rule the form applies: field rules, the owning team, the assignee's access, the project's write right. A wrong template must be reversible without touching what people did afterwards. The run is too long for one request, can be interrupted, and must not hold one transaction across 5 000 rows. Every later bulk write (a move, a bulk edit) will copy whatever pattern this sets.

## Options considered

### A. How rows are written

- **A1: bulk insert into the work items' storage from the import module.** Fast. It bypasses M3's rules, so every rule is written twice and drifts; it crosses a module boundary. Rejected.
- **A2: the whole file in one transaction through M3's writes.** All or nothing, but one bad row stops 5 000 good ones, locks are held for minutes (ADR-0011), and BR-M6-8 already chose one row per transaction. Rejected.
- **A3: one transaction per row through an in-process write contract of M3**, the same command the form's save and create use, with the import naming the batch. Chosen.

### B. How an import is undone

- **B1: no undo; the user deletes items by hand.** Rejected: 500 items.
- **B2: undo moves created items to «Удалена».** Restorable, but a wrong import of 500 rows leaves 500 wrong items in the recycle bin beside the corrected import's, each one a restore by mistake away from a duplicate (OQ-3 B).
- **B3: compensating writes from a record kept by the batch.** Created items that nobody changed are purged through ADR-0030's purge path; overwritten fields go back to their recorded value when they still hold the imported one; anything changed since is kept and listed. Chosen (OQ-3 A).

## Decision

A3 with B3.

1. **The write contract.** M3 offers the import an in-process create, or update of the item a MUSTASK identifier names (the only match, spec BR-IE-10), checked exactly as the form's writes (BR-IE-12), allowing any state of the type outside «Удалена» at creation (BR-M3-21, mod-005 BR-DR-1). A create is the create followed by the form's save in the row's one transaction, so it carries feat-022's fields, which the create dialog does not (BR-M3-40). A field holding several values is merged on update, never replaced (spec BR-IE-11). It returns, for an update, the fields it changed with their previous values. The import never touches M3's storage. A row is one transaction; rows run in file order.
2. **The batch.** The import module owns the batch: who, when, the file name, a snapshot of the template, the state («Выполняется», «Завершён», «Прерван», «Отменён», «Отменён частично»), the per-row report, and per written item whether it was created and which values it overwrote. Each row's batch record is written in the same transaction as the row, so the record and the item never disagree. The batch is never deleted; the uploaded file is (24 h).
3. **Running.** The run happens outside the request that starts it. At most one import or undo runs per organization, held by a lock on the organization's import state. A run that the service lost is marked «Прерван» at the next start; what it wrote stays undoable.
4. **Undo.** Allowed once, within 30 days, by an organization administrator, or by the «Менеджер» who started the batch while they still manage every project it wrote into, and refused while a later not-undone batch wrote any of the same items. "Changed after the import" is any write after the batch's own write to the item, a backlog or board reorder excepted (BR-IE-17); M3 answers it from the item's own record of its last write and, once feat-015 exists, from history. Undo runs one item per transaction, under the same organization lock. Purging a created item that is not in «Удалена» amends mod-005 BR-DR-12 for this one caller; the purge path is otherwise ADR-0030's, unchanged. An old value is restored only if it still passes the form's checks; an item in «Удалена», purged, or in a deactivated project is left and listed.
5. **Codes.** Three codes join ADR-0029 § 4. For templates, organization scope: `imports.template.read` (organization admin, member, viewer: a template's name and export columns) and `imports.template.write` (the organization administrator: create, copy, edit and delete any template; a project or department «Менеджер» anywhere in the organization: create and copy, and edit and delete the templates they authored, the author kept on the template; spec OQ-12 B). `imports.import.run` covers the dry run, the import, the batch list and the undo. The organization administrator holds it on the organization. The project role «Менеджер» holds it on its project, and the department role «Менеджер» on the projects its grant reaches, as ADR-0038 reaches them (spec OQ-1 B). Each row is checked against the row's project, so a row outside the importer's reach is that row's error. A holder of `imports.import.run` on any scope also reads a template's settings. Each row also needs `workitems.workitem.write` on its project. Export needs `imports.template.read` and `workitems.workitem.read` per project.
6. **Side effects.** An imported write is the importer's write. History, once it exists, names the batch; notifications, once they exist, are not sent per item for an import or an undo.

## Consequences

- Simpler: one set of rules for the form and the import; a later bulk operation reuses the contract and the batch-with-undo pattern.
- Harder: 5 000 single-row transactions take minutes, not seconds (spec NFR: 5 min); the batch's before-values duplicate data for 30 days; every module that later adds data to an item (comments, tags, participants, attachments) must say whether its write counts as "changed after the import" and must let undo remove what an import added.
- Locked in: imports never bypass M3; an undone batch cannot be redone.
- Revisit when: imports exceed 5 000 rows, or a scheduled or API import is asked for.
