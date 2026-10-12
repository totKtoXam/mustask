# US-1: A member builds a query with the clause builder

- story: US-1
- depends-on: -
- shippable: no — release 2/3, M6 not founded in MVP (D-4); founded by `mod-008-queries-dashboards`, extended by `mod-010-query-hierarchy-operators`
- status: pending

As an **organization member**, I want **to build a query using a clause builder over
a defined field/operator/macro subset**, so that **I can express reusable
filter criteria without hand-writing a query language**.

## Query language: fields, operators and macros (BR-M6-5)

Release-2 field set. Field names are the UI labels of `doc-002` § Fields,
the single field catalogue. The set covers everything the MVP inline filter
can do — state, assignee, tag, area and iteration with their subtrees, and
the keyword — so an inline filter can be saved as a query without losing
anything:

| Field (UI label) | Release-2 operators | Release-2 macros |
|---|---|---|
| ID | =, In | — |
| Заголовок | Contains | — |
| Тип | =, <>, In, Not In | — |
| Статус | =, <>, In, Not In | — |
| Исполнитель | =, <>, In, Not In, Is Empty | @Me |
| Область | =, In, Under | — |
| Итерация | =, Under | @CurrentIteration (BR-M6-6, D-16) |
| Приоритет | =, <>, >, < | — |
| Теги | In, Not In | — |
| Ключевое слово | Contains — matches like the list's keyword filter (`doc-002` US-11): a word of Заголовок, the full identifier, or the number alone | — |

Semantics:

- "=" on Область and Итерация matches exactly that node; "Under" matches
  the node and all its descendants — the subtree the inline filter uses
  (doc-001 BR-15). @CurrentIteration works with both.
- A Статус clause compares the state's label on every type:
  «Статус = Разрешена» returns Эпик, Фича and Пользовательская история
  items in InProgress together with Баг items in Resolved (`doc-002`
  § Types, levels and states).
- A work item in «Удалена» is returned only by a clause that names
  «Удалена» (BR-M6-4).

Release-3 extension, toward WIQL parity (`mod-010-query-hierarchy-operators`):

| Field | Release-3 adds |
|---|---|
| Заголовок | Contains Words |
| Статус | Was Ever (reads the work item's history, doc-001 BR-4) |
| Исполнитель | In Group — the group is a team of the query's own project (MUSTASK has no other groups) |
| Дата изменения (`doc-002` § Fields: the time of the work item's latest history revision) | =, >, <, plus @Today±n |
| Query type | Дерево рабочих элементов / Рабочие элементы и прямые связи |

Every macro `research/ado-boards.md` § Queries lists, closed:

| Macro | Status |
|---|---|
| @Me | release 2 — Исполнитель only |
| @CurrentIteration | release 2 — Итерация only, with "=" or "Under", team-bound to the query's own project (BR-M6-6, D-16) |
| @Today±n | release 3 — any date field (Дата изменения) |
| @Project | never — a query is already scoped to one project (BR-M6-1); the macro would be redundant |
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
  When a member adds the clause «Статус = Активна» and, combined with
    «И», the clause «Исполнитель = @Me»
  Then the query is valid and, when run, returns only work items matching
    both clauses

Scenario: "Under" reaches sub-nodes, "=" does not
  Given an area «Платформа» with the child «Платформа/API», and a work item
    in each
  When a member runs «Область Under Платформа», then «Область =
    Платформа»
  Then the first returns both items and the second only the one in
    «Платформа» (BR-M6-5)
  And «Итерация Under @CurrentIteration» likewise returns items of the bound
    team's current iteration and of its descendants

Scenario: «Статус = Разрешена» spans two categories
  # Superseded by bug-004 D-3 (2026-10-12): «Разрешена» is Resolved on the
  # story as on the bug, so the two categories are one. Both are still
  # returned; write the check against the seeded process, not this wording.
  Given a «Пользовательская история» and a «Баг», both in «Разрешена»
  When a member runs «Статус = Разрешена»
  Then both are returned, although the story's state is InProgress and the
    bug's is Resolved (BR-M6-5)

Scenario: The keyword clause finds what the inline keyword filter finds
  Given a work item MSK-BUG-42 whose title contains «оплата»
  When a member runs «Ключевое слово Contains оплата», then
    «MSK-BUG-42», then «42»
  Then the item is returned all three times (BR-M6-5, `doc-002` US-11)

Scenario: Deleted items appear only when a clause names «Удалена»
  Given a project with an item in «Удалена» that otherwise matches a query
  When a member runs the query without, then with, «Статус = Удалена»
  Then the item is absent the first time and returned the second (BR-M6-4)

Scenario: The @CurrentIteration macro needs a team from the query's own project
  Given a member building a query with the clause «Итерация =
    @CurrentIteration»
  When the query has no team selected
  Then the query editor blocks running or saving the query and asks to pick
    a team of the query's own project (BR-M6-1, BR-M6-6, D-16) — a team from
    any other project is not offered

Scenario: A release-3 operator is not offered in release 2
  Given a member editing a query in release 2
  When they add a clause on Статус
  Then "Was Ever" is not offered until release 3 (BR-M6-5)

Scenario: Choosing a query type
  Given a member creating a new query
  When they open the query type picker
  Then «Плоский список» (flat list) is available in release 2; «Дерево
    рабочих элементов» and «Рабочие элементы и прямые связи» appear only
    once release 3 ships (BR-M6-5)
```
