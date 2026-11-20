# Spec: Cascading context selectors (one organization)

## Context and goal

feat-043 put four independent selectors in the header (organization, department, team, project); each hides nothing and parks non-fitting objects under «Остальные». doc-016 (accepted with ADR-0040 § 3) replaces that with one context path button opening a panel of four cascading columns and a search, where each column offers only what lies under the chosen levels above it and a choice fills in its parents. This task is the one-organization half of doc-016 US-3 and US-4 (the queue line `feat-NNN-cascading-context-selectors` of doc-016 § Decomposition); organization «все» is feat-064.

## Roles

- **Organization member** (any role, administrator included): sees the button and the panel; offers are limited to what they see (doc-016 BR-TD-12..-15, delivered before this task, deps mod-022 and feat-061 per `status.md`). No new permission, no new role.

## Stories

| id | Title | depends-on | File |
|----|-------|------------|------|
| US-1 | The context path button and four cascading columns | - | `stories/US-1-path-button-and-columns.md` |
| US-2 | A choice selects its parents, resets what no longer fits, and a stale selection is repaired | US-1 | `stories/US-2-choice-parents-reset-repair.md` |
| US-3 | The panel search finds and chooses an object of the chosen organization | US-1, US-2 | `stories/US-3-panel-search.md` |

All three ship together (the columns are unusable without the choice rules, and the search reuses them); none is its own queue line.

## Business process (BPMN)

Not drawn: the flow is linear (open, choose or search, selection updated, notice shown); the only branch, the rule that a choice resets values, is a table in doc-016 BR-TD-9, -10.

## Business rules

Cited by id, not restated; the text lives in `docs/features/doc-016-teams-under-departments/spec.md` and `design.md` § 3.

| # | Rule | Source |
|---|------|--------|
| BR-1 | The selection, its invariant and the four levels: doc-016 BR-TD-7. | doc-016 |
| BR-2 | Offers cascade, «Остальные» retired: BR-TD-8, in one organization (the department, team and project branches under a chosen organization; the organization-«все» branches stay out). | doc-016, ADR-0040 § 3 |
| BR-3 | A choice selects its organization and department; a project selects no team; unfitting values reset with «Выбор сброшен: …»: BR-TD-9, -10. | doc-016 |
| BR-4 | Empty offers and the lists «Проекты», «Команды» narrowed by the selection: BR-TD-11. | doc-016 |
| BR-5 | Repair of a remembered selection and of one seen stale while the panel is open: BR-TD-18, with ADR-0032 § 6's notice for a remembered one. | doc-016, ADR-0032 § 6 |
| BR-6 | The button and the panel: BR-TD-22(a), design § 3.0, minus the «Создать …» line (feat-052) and the organization-«все» columns by heading (feat-064). Colour stripes and dots show the stored colour of BR-TD-20 as it is already delivered; this task assigns none. | doc-016 |
| BR-7 | The panel search: BR-TD-21 limited to the chosen organization's objects; the grouped answer, at most 5 a group, colour mark and grey parent path as in design § 3.1 (the organization does not lead the path: one organization). | doc-016 |
| BR-8 | Product copy of the panel is quoted from doc-016 and ADR-0032 § 6 and is Russian; a state this spec adds no copy for uses the doc-016 text. | AGENTS.md |

## Operation completeness

| Entity | Create | Read | Update | Delete/deactivate | Cancel/reverse |
|--------|--------|------|--------|-------------------|----------------|
| Selection (organization, department, team, project) | by a choice or a search result (US-1..3) | the user, in the button | a choice; repaired by BR-TD-18 (US-2) | «все» at a level (US-2) | choose again; no undo of a notice |
| Context path button / panel | no: always present | every signed-in member | no | no | Esc / click outside closes (US-1) |
| Remembered selection | stored as ADR-0032 § 6 stores it; unchanged | read at start, repaired (US-2) | on each choice, as today | cleared when no organization is left | no |
| Offers, search results | no: derived | member, sight-scoped | no | no | no |

## Non-functional requirements

- Offers and search answer within the doc-016 volumes: a narrowed answer within 2 s (doc-016 NFR "Volumes", one organization at 1 000 areas, 200 projects, 200 teams).
- Non-disclosure: a hidden object appears in no column, search result or notice (BR-TD-17).
- Keyboard: ↑ ↓ inside a column, ← → between columns, Enter chooses, Esc closes; the button is reachable by Tab and labelled with the full path; at 640 CSS px the columns scroll sideways inside the panel, not the page (feat-043 criteria kept).
- No migration, no new permission code. Any server addition (offers or search parameters) is additive.

## Out of scope

- feat-052: the «Создать …» line in the columns and the guarantee «все» is first.
- feat-064: organization «все» with headings per organization, the search across organizations, the cross-organization offers route and a choice that opens another organization from «все».
- feat-074: the global search palette (Ctrl+K, users, work items).
- The «Структура» page's filter field of BR-TD-21: doc-016 § Decomposition does not put it in this line (the line names only "the search on one organization's objects" and the panel); it belongs with the «Структура» surfaces (`feat-NNN-sight-in-structure-lists`). Decided here; see OQ-2.
- Editing colours; changes to sight rules (BR-TD-12..-17); the objects' pages and the address shape (ADR-0032 §§ 1, 2 stand).

## Decomposition for the backlog

| id | Task | size | deps |
|----|------|------|------|
| `feat-NNN-cascading-context-selectors` | This feature: US-1..US-3 as one task (already queued as feat-063; no further lines) | M | mod-022, feat-061 |

## Open questions

- [ ] **OQ-1 Organization «все» in this task.** feat-063 handles one organization, but «Все организации» is still a state of the first column. Recommended: while it is chosen, the department, team and project columns show only «все» and the panel search is disabled until an organization is chosen; feat-064 lifts both. Alternative: keep feat-043's current behaviour for «все» as is (to be confirmed from the code at design).
- [ ] **OQ-2 «Структура» filter field.** Recommended: out of this task (above), owned by the «Структура» sight line; confirm the queue line `feat-NNN-sight-in-structure-lists` or a separate S line carries it, since doc-016's decomposition names it nowhere explicitly.
