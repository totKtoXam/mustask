# US-1: A team member builds a query with the clause builder

- story: US-1
- depends-on: -
- shippable: no — release 2/3, M6 not founded in MVP (D-4)
- status: pending

As a **team member**, I want **to build a query using a clause builder over
a defined field/operator/macro subset**, so that **I can express reusable
filter criteria without hand-writing a query language**.

## Query language: fields, operators and macros (BR-5)

Release-2 field set — an explicit table, not a description of "columns
already on the list" (that ground would be doc-002's, and doc-002 has not
shipped); it is grounded instead in doc-001's own MVP field row for M3, and
it is broader than the MVP inline filter's four fields (state, assignee,
area, tag) — a deliberate new filterable surface, not a restatement of it:

| Field (UI label) | Source (doc-001 MVP row) | Release-2 operators | Release-2 macros |
|---|---|---|---|
| Заголовок | M3 (Title) | Contains | — |
| Тип рабочего элемента | M2/M3 (Work Item Type) | =, <>, In, Not In | — |
| Статус | M2/M3 (State) | =, <>, In, Not In | — |
| Исполнитель | M3 (Assigned To) | =, <>, In, Not In, Is Empty | @Me |
| Область | M1 (Area) | =, In | — |
| Итерация | M5 (Iteration) | = | @CurrentIteration (BR-6, D-16) |
| Приоритет | M3 (Priority) | =, <>, >, < | — |
| Тег | M3 (Tags) | In, Not In | — |

Release-3 extension, toward WIQL parity (`mod-NNN-query-hierarchy-operators`
in spec.md § Decomposition):

| Field | Release-3 adds |
|---|---|
| Заголовок | Contains Words |
| Статус | Was Ever (reads the work item's history, doc-001 BR-4) |
| Исполнитель | In Group — the group is a team of the query's own project (MUSTASK has no other groups) |
| Область | Under (over the Area hierarchy) |
| Дата изменения (new field: the time of the work item's latest history revision, doc-001 BR-4) | =, >, <, plus @Today±n |
| Query type | Дерево рабочих элементов / Рабочие элементы и прямые связи |

Every macro `research/ado-boards.md` § Queries lists, closed:

| Macro | Status |
|---|---|
| @Me | release 2 — Исполнитель only |
| @CurrentIteration | release 2 — Итерация only, team-bound to the query's own project (BR-6, D-16) |
| @Today±n | release 3 — any date field (Дата изменения) |
| @Project | never — a query is already scoped to one project (BR-1); the macro would be redundant |
| @Follows | never — MUSTASK has no "Follow" feature (not in doc-001's scope) |
| @RecentMentions | never — depends on @mentions in comments, itself out of scope (doc-001 "Out of scope") |

Release 3's "WIQL-subset query language" (doc-001 § Release 3) is the
clause builder's internal model only — MUSTASK never exposes a raw-text
query editor (spec.md § Out of scope); a query is always built and edited
through the clause builder UI, in release 2 and release 3 alike.

## Acceptance criteria

```gherkin
Scenario: Building a flat-list query with the release-2 field set
  Given a project with work items in different states, areas, assignees and
    tags (M3)
  When a team member adds a clause on "Статус" for a state of the
    InProgress category (doc-001 BR-2 — the concrete state names are M2's)
    and, combined with "И", the clause "Исполнитель = @Me"
  Then the query is valid and, when run, returns only work items matching
    both clauses

Scenario: The @CurrentIteration macro needs a team from the query's own project
  Given a team member building a query with the clause "Итерация =
    @CurrentIteration"
  When the query has no team selected
  Then the query editor blocks running or saving the query and asks to pick
    a team of the query's own project (BR-1, BR-6, D-16) — a team from any
    other project is not offered

Scenario: The release-2 clause builder does not offer a release-3-only operator
  Given a team member editing a query in release 2
  When they try to add a clause on Область (Area)
  Then only the release-2 operators listed for Область above are offered
    ("=", "In") — "Under" is not available until release 3 (BR-5)

Scenario: Choosing a query type
  Given a team member creating a new query
  When they open the query type picker
  Then "Плоский список" (flat list) is available in release 2; "Дерево
    рабочих элементов" and "Рабочие элементы и прямые связи" appear only
    once release 3 ships (BR-5)
```
