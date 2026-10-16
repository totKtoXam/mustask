# US-1: An administrator builds the organization's area tree

- story: US-1
- depends-on: -
- shippable: no — an analysis story; `mod-NNN-area-workspace` delivers it together with US-3's area links
- status: pending

As an **organization admin, or a holder of «Управление» on an area**, I want **to create, rename, describe, move and
delete the organization's areas as one tree**, so that **the organization's departments, programs and working groups
get workspaces that projects and teams can be linked to, with nothing stored inside them (BR-MX-2, BR-MX-3)**.

The tree belongs to the organization, not to a project. Links follow BR-MX-5..-8, what grants and «Наследовать доступ»
reach BR-MX-22, and a work item's «Область» OQ-1. Grants arrive after this story's line: a clause that needs one is
marked "owed by `mod-NNN-scoped-access`", and that line verifies it. The examples use the brief's tree (§ 3.2):
«Логистика» with the children «Складские системы» and «Транспортные системы», and the top-level «Финансы» and
«Инфраструктура». A top-level area is level 1; a path is joined by « / ». "Refused" means the quoted text is answered
and nothing changes.

## Acceptance criteria

```gherkin
Scenario: The organization admin builds the example tree (brief § 3.2)
  Given the organization «Компания» has no areas, and the user is its organization admin
  When they create «Логистика», «Финансы» and «Инфраструктура» with no parent,
    then «Складские системы» and «Транспортные системы» with the parent «Логистика»
  Then «Области» shows the three top-level areas, and «Логистика / Складские системы» and «Логистика / Транспортные системы»
  And each area has an empty description and «Наследовать доступ» on

Scenario: Creating an area creates nothing else (BR-MX-2)
  Given the organization's projects, teams, team memberships, grants, links and work items are counted
  When «Финансы» is created with only a name
  Then «Финансы» has no project, no team and no participant, the creator included, and every count is unchanged

Scenario: The create form takes the optional fields in one operation (brief § 11.5)
  When the organization admin opens the create form
  Then only the name is required; the parent, the description, projects, teams, participants and access settings are optional
  And the parent picker offers only areas the user may create children in, and no parent only to the organization admin
  When they create «Транспортные системы» under «Логистика» with a description, the project A, the team MARS
    and a participant holding «Изменение» (the participant: owed by `mod-NNN-scoped-access`)
  Then the area, its two links and the grant all exist after the one operation, each made by BR-MX-5, -6 and -20; a «Наследовать доступ» set off in the same form is applied last (BR-MX-3)
  And when any link or grant in the operation is refused by those rules, nothing is created, the area included

Scenario: A name is 1–100 characters and is saved trimmed; a description holds at most 2 000 (BR-MX-4)
  When an area is created or renamed with an empty name, only spaces, or 101 characters after trimming
  Then it is refused with «Название должно содержать от 1 до 100 символов»
  And the top-level area «  Аналитика  » is saved and shown as «Аналитика»
  And a description of 2 001 characters is refused with «Описание должно содержать не более 2000 символов», and one of 2 000 is kept as typed

Scenario: Sibling names are unique, and top-level areas are siblings of each other (BR-MX-4)
  Given the example tree
  When a top-level «логистика » is created, «Финансы» is renamed «ЛОГИСТИКА»,
    or «Транспортные системы» is renamed «складские системы»
  Then each is refused with «Область с таким названием уже есть на этом уровне»
  But «Складские системы» can be created under «Инфраструктура», whose children are other siblings
  And «Финансы» can be renamed «финансы», since its only match is itself

Scenario: Two writes of one name at the same moment keep one (NFR Concurrency)
  When two users create a top-level «Аналитика» at the same moment, or one creates «Аналитика» under «Логистика» while another renames «Транспортные системы» «Аналитика»
  Then exactly one «Аналитика» exists at that level, and the other request is refused as a taken name

Scenario: An area moves with its whole subtree (BR-MX-3)
  Given the example tree, «Логистика» linked to the project A and holding one participant (owed by `mod-NNN-scoped-access`)
  When «Логистика» is moved under «Финансы»
  Then the tree shows «Финансы / Логистика / Складские системы» and «Финансы / Логистика / Транспортные системы»,
    and no top-level «Логистика»
  And its link, participant, description and switch are unchanged, and no project, team or work item changed
  When «Финансы / Логистика» is moved to the top level
  Then the tree is as it was in the Given

Scenario: A move needs a free name at its destination (BR-MX-4)
  Given «Инфраструктура» has a child «Складские системы»
  When «Логистика / Складские системы» is moved under «Инфраструктура»
  Then it is refused with «Область с таким названием уже есть на этом уровне», and so is a move to the top level
    of an area named like an existing top-level area

Scenario: An area never moves under itself or a descendant (BR-MX-3, SC-08)
  When «Логистика» is moved under «Логистика», or under «Логистика / Складские системы»
  Then it is refused with «Нельзя переместить область внутрь неё самой или её вложенной области»
  And the move dialog offers neither the area itself nor any of its descendants as the destination

Scenario: Two moves that together would form a cycle: one is refused (BR-MX-3, SC-08)
  When «Финансы» is moved under «Инфраструктура» and «Инфраструктура» under «Финансы» at the same moment
  Then exactly one move is applied, the other is refused, and following parents from any area ends at a top-level area
  And of two moves or creations at the same moment that together would make the tree deeper than 10 levels, one is refused

Scenario: The tree is at most 10 levels deep (BR-MX-3)
  Given a chain from the top-level «Финансы» down to «Ф10» at level 10, with «Ф9» at level 9 and «Ф8» at level 8
  When a child is created under «Ф10», or «Логистика» with its children is moved under «Ф9»
  Then each is refused with «Дерево областей не может быть глубже 10 уровней»
  But «Логистика» moved under «Ф8» is applied: its children land on level 10

Scenario: Rename, description and the inheritance switch are edited
  When the organization admin renames «Логистика» «Логистика и доставка», sets and then clears its description,
    and turns «Наследовать доступ» off on «Складские системы»
  Then the tree shows «Логистика и доставка / Складские системы» and «Логистика и доставка / Транспортные системы»
  And the description is empty again, and «Складские системы» shows the switch off on every later read, with its links and participants unchanged; what the switch changes in access is BR-MX-22's

Scenario: A holder of «Управление» manages the area and the areas below it; the switch and a move away need the parent too (BR-MX-3; owed by `mod-NNN-scoped-access`)
  Given a user whose only grant is «Управление» on «Логистика», reaching its children by inheritance (BR-MX-22)
  When they create «Аналитика» under «Логистика», rename «Транспортные системы», delete «Аналитика», and turn «Наследовать доступ» off on «Складские системы»
  Then each succeeds, and the last ends their own «Управление» on «Складские системы», which only the admin, or a holder of «Управление» on it and on «Логистика», turns on again
  When they create a top-level area, create a child of «Финансы», rename «Финансы», switch «Наследовать доступ» off on «Логистика»,
    or move «Транспортные системы» under «Финансы» or to the top level
  Then each is answered 403, and nothing changes
  And a user whose only grant is «Управление» on «Транспортные системы» is answered 403 turning its switch off or moving it: both need «Логистика» too
  And with «Управление» on «Финансы» as well, the first user's move under «Финансы» succeeds; moving «Логистика», and the top level, stay the admin's

Scenario: Nobody else writes to an area, a linked project or team included (BR-MX-6, P-9)
  Given «Логистика» linked to the project A and the team MARS, and users with no area grant other than as listed:
    a member, a viewer, and (owed by `mod-NNN-scoped-access`) holders of «Изменение» on «Логистика», of project
    «Управление» on A and of team «Управление» on MARS
  When any of them creates, renames, describes, moves, switches or deletes an area, on the page or by a direct request
  Then it is answered 403, and nothing changes
  And «Области» shows them the tree with no create, rename, move or delete action

Scenario: Every member reads the whole tree, within 2 s at the stated volume (NFR Volumes)
  Given the example tree, with «Наследовать доступ» off on «Складские системы»
  When a member or a viewer with no grant opens «Области»
  Then they see all five areas with their paths, descriptions and switches
  And a user who is not a member of «Компания» gets «Организация не найдена» (BR-MX-24)
  And in an organization with 1 000 areas over 10 levels, the whole tree is shown within 2 s

Scenario: Deleting a leaf ends its links and grants and nothing else (BR-MX-8, BR-MX-2; NFR Concurrency)
  Given «Логистика / Складские системы» is linked to the project A and the team MARS, A and MARS are also linked
    to «Финансы», and a user holds «Изменение» on «Складские системы» (owed by `mod-NNN-scoped-access`)
  When the organization admin deletes «Логистика»
  Then it is refused with «Нельзя удалить область, в которой есть вложенные области»
  When they delete «Складские системы», after a confirmation that names its team links, its links to projects the user may see
    (a deactivated one marked «деактивирован»), and its grants, and says it cannot be undone
  Then it is gone from the tree, and its two links and the grant are gone
  And a holder of «Управление» on «Складские системы» alone, who may not see a «Ограниченный» project R linked to it (owed by `feat-NNN-project-visibility`),
    deletes it without R in the confirmation or its counts, and R's link ends too (BR-MX-21)
  And A, MARS, every other link of theirs, A's work items, «Логистика» and «Транспортные системы» are as before
  And no action restores it: «Складские системы» created again under «Логистика» has no link and no participant
  And a deletion that runs at once with a child created under the area, or with an area moved under it, refuses one of the two, and no area is left under a deleted parent

Scenario: An organization with no areas works fully (brief § 3.1.4, SC-07, BR-MX-2)
  Given the organization has no areas
  When the organization admin opens «Области»
  Then they see «В организации пока нет областей» with the create action, and a member sees it without the action
  And a project and a team are created, a work item is created in the project, and the project's board opens with it, with no area anywhere

Scenario: An id from another organization names nothing (BR-MX-1, BR-MX-24)
  Given the organization «Другая» has its own top-level «Логистика», which did not clash with «Компания»'s
  When an admin of «Компания» creates an area under «Другая»'s «Логистика», moves «Финансы» under it,
    or renames, moves or deletes it through «Компания»'s address
  Then each is answered «Область не найдена», exactly as for an id that names no area, and neither organization changes
  And a member with no grant gets «Область не найдена» for the creation under it and for its rename, move or delete,
    but 403 for moving «Финансы» under it, as for any destination: no answer tells whether that area exists
```
