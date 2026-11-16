# Review: doc-016-teams-under-departments

## Round 1 — requirements

Reviewer: requirements-reviewer (clean context), 2026-11-12. Input: `spec.md`, US-1..US-6, ADR-0040 (proposed) at 15055ed4; checked against feat-028, mod-018/ADR-0031, mod-019/ADR-0038, ADR-0028/-0029/-0032, doc-012, feat-048, feat-049 and the queue lines feat-041/-042/-050/-052/-054. Code spot-checks under `src/`: departments do not nest (`Area.cs`: a department's parent must be null, a project node's parent a department); «Верхний уровень» project creation exists (`CreateProjectByPlace.test.tsx`, `areaTexts.ts`); the selector read is unfiltered by sight (`NavigationReadModel.ReadSelectorOffersAsync`: every department, every reachable project, every active team); «Остальные» lives in `selectorFit.ts` and `ContextSelectors.tsx`; the team-department links are read by `selectorFit.ts` (`departmentIds`) and the feat-049 diagram (`diagramModel.ts`); deleted teams are soft-deleted rows (`Team.Status`).

### Rule → story mapping

| Rule / request point | Story | Delivery line |
|---|---|---|
| Point 1; BR-TD-1, -2, -4, -5 | US-1 | mod-NNN-teams-in-departments (+ mod-NNN-department-managers-run-teams for OQ-6 A) |
| Point 1 (migration) | US-2 | mod-NNN-teams-in-departments |
| Point 2; BR-TD-7, -8, -11 | US-3 | feat-NNN-cascading-context-selectors, feat-NNN-cross-organization-selectors |
| Point 2; BR-TD-9, -10, -18 | US-4 (US-2 last scenario for the remembered selection) | same |
| Point 3; BR-TD-6 | US-5 | feat-052 (reworded) |
| Point 4; BR-TD-12..-17 | US-6 (US-3 for the offers) | feat-041 (reworded), feat-NNN-sight-in-structure-lists, feat-NNN-sight-in-work-views |
| BR-TD-3 | US-3 (C offered under «Логистика» through MARS) | - |

Every rule maps to a story; nothing of the four points is left only in Out of scope.

### R-1
Severity: major
Affects: BR-TD-13, BR-TD-14, US-6, ADR-0040 § 4
Problem: Sight was defined for teams and departments, but only the project has a gate in the evaluator (ADR-0029 § 6 step 3 is "for scope P"). Nothing said what a hidden team or department answers when addressed (`teams/:t/…`, `areas/:a/…`, the `teamId`/`departmentId` query of ADR-0032 § 1), whether the organization role's team-scope codes (every `member` holds `boards.backlog.reorder`) still apply on a team one does not see, or what a project node and its sub-areas (area rows) follow. US-6 expected «Команда не найдена.» with no rule behind it.
Required resolution: a gate for scope T and A, the answers, and the node rule.
Status: resolved
Resolution: BR-TD-13/-14 now end "otherwise it does not exist for them («Команда не найдена.» / «Область не найдена.»), at ADR-0029 § 8 item 4; the organization role's codes of that scope apply only on a visible one"; BR-TD-14 adds that a project node and its sub-areas are visible exactly when their project is. ADR-0040 § 4 gains the same gate. US-6 adds a deep link carrying a hidden team in the query.

### R-2
Severity: major
Affects: US-6 "Sight through a department grant", BR-TD-13, BR-TD-14
Problem: The chain of BR-TD-13/-14 (a visible project shows its linked teams; a visible team shows its department) gives Max, through C, the team MARS and therefore «Логистика». The scenario said he does not see «Логистика». A test written from the story would contradict the rule. The chain itself is needed: without it, choosing MARS could not choose its department (BR-TD-9).
Required resolution: align the scenario with the rule.
Status: resolved
Resolution: the scenario now reads that Max sees «Логистика» showing MARS alone, and does not see A, B or VENUS.

### R-3
Severity: major
Affects: BR-TD-8, BR-TD-17, § Changes to prior lines, merged feat-048 and feat-049
Problem: Two merged lines were missing from the change table. feat-048 (BR-AV, F43 US-6) scopes a department's «Рабочие элементы», «Канбан» and «Бэклог» to "the projects placed in it" and narrows "a team chosen together with a department" to that department's projects. BR-TD-8 now offers, under «Логистика», the project C through MARS, and US-3 lists C in «Логистика»'s «Проекты». So the selector and the list would show C while the department's views leave out C's items. With a team standing in one department, "a team together with a department" also changes meaning: MARS + «Логистика» would hide MARS's own items in C. feat-049's diagram draws the department–team links that BR-TD-2 retires. This is a change to shipped behaviour, so the human has to decide it.
Required resolution: an OQ on the department scope of the work views, and rows for feat-048 and feat-049 in the change table.
Status: open (OQ-8)
Resolution: OQ-8 added (A: the department's views span the same set as its «Проекты», BR-TD-8; B: placed projects only, the lists then show the same). Rows for feat-048 and feat-049, and a row for the lines that cite doc-012 BR-MX-21's "project visibility", were added to § Changes to prior lines.

### R-4
Severity: major
Affects: § Decomposition
Problem: Two lines were larger than M. `mod-NNN-teams-in-departments` held the model change, the guessing upgrade, move with locks, the deletion guard, retiring routes, the panel's team form, page, department lists and diagram, and also OQ-6's change to the permission matrix (team.create struck, department «Менеджер» team codes, generated matrix, bundle contract). mod-018 was L for a smaller model change. `feat-NNN-sight-on-every-surface` held what the old L feat-041 held for surfaces, now for three object kinds. With those two, "each ≤ M" was not met.
Required resolution: split them.
Status: resolved
Resolution: the matrix change of OQ-6 A moved to a new S line `mod-NNN-department-managers-run-teams`, which only exists if OQ-6 A is chosen; until it lands, team creation in a department is the administrator's. The surfaces line is split into `feat-NNN-sight-in-structure-lists` (selector offers, «Структура», «Проекты», «Команды», «Обзор», pickers) and `feat-NNN-sight-in-work-views` (multi-project item views, search, counts, the sprint clauses). US-1 and US-6 name the new lines. The human should still check the sizes at gate-1: `mod-NNN-teams-in-departments` is the M at greatest risk.

### R-5
Severity: major
Affects: BR-TD-7, BR-TD-9, US-2 last scenario, US-4 last scenario
Problem: Two scenarios repaired a selection by moving a parent: VENUS moved, so the department follows the team. No rule said this. BR-TD-9 covered only choices, and ADR-0032 § 6 together with today's `repairUnfit` resets the more specific value instead. A developer following the ADR would reset VENUS, while the stories keep it. The rule also left two things open: whether a parent being replaced (not reset to «все») is named in the notice, and the order of repair when a team and a project both stop fitting.
Required resolution: one repair rule.
Status: resolved
Resolution: BR-TD-18 added. A chosen team's department becomes the team's own. Then, least specific first, each lower chosen value that no longer fits resets to «все», with the notice of the occasion («Выбор сброшен: …» for a change seen while open, ADR-0032 § 6's for a remembered one). A parent replaced by the value's own parent is not a reset and is not named. BR-TD-9 says the same for choices. ADR-0040 § 3 points to it.

### R-6
Severity: major
Affects: BR-TD-3, OQ-4, mod-019 BR-SA-5/-15, doc-012 BR-MX-7
Problem: BR-TD-3 said the link rules, mod-019 BR-SA-15 included, are "unchanged". But under OQ-4 A, BR-SA-15's "a link gives … no access" and BR-SA-5's "no link, team or shared project passes a grant" stop holding for sight. A project–team link, and adding a person to a team, now disclose every project linked to that team. Whoever staffs a team then controls who sees projects they do not manage. OQ-4's downside named only the link.
Required resolution: say so in the rule and in OQ-4's downside.
Status: resolved
Resolution: BR-TD-3 now states the exception (a link still gives no code; under OQ-4 A it gives the team's members sight). OQ-4 A's downside adds that whoever adds a team member discloses the team's linked projects to them. The choice stays with the human in OQ-4.

### R-7
Severity: major
Affects: BR-TD-1, BR-TD-5, US-2, ADR-0040 § 1
Problem: Deleted teams stay as rows (`Team.Status`), and US-2 gives SATURN no department, so "every team has exactly one department, required" was false as written. A team deleted after the upgrade would keep its reference, and with "Restrict" it would block its department's deletion through a team nobody can see.
Required resolution: the requirement applies to active teams, and a deleted team never blocks a deletion.
Status: resolved
Resolution: BR-TD-1 now applies "required" to an active team. A deleted team keeps no department: deletion clears it, and the upgrade gives none. BR-TD-5 counts active teams. ADR-0040 § 1 is reworded to match.

### R-8
Severity: minor
Affects: US-1, ADR-0040 §§ 1-2
Problem: Three gaps. (1) The race between creating a team and deleting its department was not covered; only move against deletion was. (2) The lock taken by creation was not stated. (3) The declaration and answer order of team creation, whose place is in the body, were not stated: ADR-0038 § 3 decides project creation at step 0 so that a foreign or unknown place is not disclosed. Under OQ-6 A, a team code given by the department is also decided against a team that may be moving.
Required resolution: state them.
Status: resolved
Resolution: ADR-0040 § 1 locks the tree lock and the department `FOR SHARE` on creation too. § 2 decides creation like ADR-0038 § 3 (step 0; a non-admin gets 403 for any place lacking the code, an unknown, deleted or foreign one included) and decides department-given team codes on the locked team row. US-1 gains the creation-against-deletion scenario.

### R-9
Severity: minor
Affects: BR-TD-5, US-1
Problem: BR-TD-5 brought new copy, while a department holding a project already answers `organizations.area.not_empty` with its own catalogue text. The spec did not say whether this is a new code or a changed text, so the error catalogue and `contracts/errors` could not be planned.
Required resolution: name the code.
Status: resolved
Resolution: a department holding an active team answers a new 409 `organizations.area.has_teams` «Нельзя удалить департамент, в котором есть команды». A department holding a project keeps `organizations.area.not_empty` and its text. US-1 is updated.

### R-10
Severity: minor
Affects: US-2, OQ-1 A
Problem: Two corner cases. (1) Under OQ-1 A, a name clash failed the whole upgrade even when the clashing node was already a department of that name and could be reused. (2) Whether "most projects" counts deactivated projects was not said, so two implementations could place the same team differently.
Required resolution: make both deterministic.
Status: resolved
Resolution: US-2 now reuses an existing department named «Команды без департамента» and fails only when a top-level project node holds the name. Linked projects count whether active or deactivated. OQ-1 A says the same.

### R-11
Severity: minor
Affects: ADR-0040 § 4
Problem: The ADR said «Ограниченный» is "the project node's «Наследовать доступ» off plus the value off". Under OQ-4 A, a linked team's members still see such a project, so the two are not equal.
Required resolution: correct the sentence.
Status: resolved
Resolution: ADR-0040 § 4 now says that no value hides a project from its linked teams' members; to restrict one, switch inheritance off, turn the value off and unlink the teams.

### R-12
Severity: minor
Affects: BR-TD-8, US-3
Problem: The human's wording reads "projects = all projects of all its teams and departments". Taken literally, it leaves out a top-level project with no team, yet such a project could then be chosen nowhere. In US-3, D is linked to JUPITER, so the case was never exercised. The team grouping was also named only as "own teams first", while the panel already shows «Мои команды» / «Другие команды».
Required resolution: state the reading and test it; name the grouping.
Status: resolved
Resolution: BR-TD-8 says "every project of the chosen organization, a top-level project linked to no team included", and US-3 adds the top-level project E with no team. BR-TD-8 keeps feat-043's «Мои команды» / «Другие команды» groups.

### R-13
Severity: minor
Affects: US-4 "Opening an object from a row, a link or an address", US-6
Problem: ADR-0032 § 1 says that an address whose query value does not fit the primary object answers not found. It does not reset it. US-4 applied the row rule to "an address", which contradicts that section, and deep links to an object the user cannot see were covered only for the path object.
Required resolution: keep the address rule, and add a deep link that carries a hidden object in the query.
Status: resolved
Resolution: US-4 now reads "from a list row or a link". US-6 adds the address of A with the hidden JUPITER as `teamId`, which answers «Команда не найдена.».

### R-14
Severity: minor
Affects: § Decomposition, `feat-NNN-cross-organization-selectors`
Problem: The offers with organization «все» cannot be read through `GET /organizations/{organizationId}/selector-offers`. They need a route outside any organization, which the inventory test (every `{organizationId}` route declares an organization code) does not cover. The line did not say so.
Required resolution: name it in the line.
Status: resolved
Resolution: the line now says it adds a caller-scoped route outside the organization prefix, with its entry in the inventory, applying each organization's sight set.

### R-15
Severity: minor
Affects: BR-TD-9, the human's point 2 ("choosing a nested object selects its parents")
Problem: Choosing a project selects no team. Read literally, the request might expect the team to be selected too.
Required resolution: record the reading.
Status: resolved
Resolution: BR-TD-9 says why. A project's parent is its place (a department or the organization), while a team is linked to it, not above it. Choosing a team would also narrow the project to that team's items (ADR-0032 § 5). The human can still overrule this at gate-1.

### R-16
Severity: minor
Affects: US-6 "The upgrade's effect is named"
Problem: "The MR lists …" is a process step, not something a test can check.
Required resolution: make the upgrade produce the list.
Status: resolved
Resolution: the upgrade logs, per project, the members who lose sight, and the MR quotes the log.

### Verdict

`needs-human-decision`: OQ-1..OQ-8 are the human's. Every finding except R-3 is fixed in the spec, the stories or ADR-0040, and R-3 waits on OQ-8.
