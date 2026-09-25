# doc-010 — cross-module review, consolidated

This file consolidates the four parallel reviews in `cross-review/`: A data model (19 findings), B access and roles (12), C scope and queue (20), and D behaviour and rules (19). That is 70 source findings in total, grouped here into 34 root causes.

- Line references are the ones the source reports use: `doc-00N:L` is that spec's `spec.md`, and `queue:L` is `docs/backlog.md` on main.
- The merged specs were opened only to settle conflicts between reports: the delivery-plan actor (doc-004) and the purge wording (doc-005 BR-9).
- The default for the per-team default area was checked the same way (doc-006 BR-M1-14 and R-19, and doc-003's create paths).
- Main has moved on since this branch was taken. Commit 630e0d0 ("Guard mod-001-process-seed behind doc-010 (state names under review)") has already applied C-2's queue guard.

Q-n in the entries refers to § Decisions for the human.

## Findings by root cause

Order: blockers first, then majors, then minors. Within each severity, entries are ordered by how early in the queue they bite (mod-001 → mod-002 → mod-003 → later lines → releases 2/3).

### N-1 Seeded state names are defined twice, by conflicting human decisions
Severity: blocker
Sources: A-1, D-1, C-2
Affects: doc-002 § Types, levels and states (table, Errata), US-1, US-3, US-10, US-11; doc-007 § MVP: the seeded process, US-3, US-5; doc-001 US-3, US-4, US-6, US-10; doc-003 BR-2; decisions doc-002 W-2/W-7/W-9 vs doc-007 P-1/P-2; queue mod-001, mod-003
Problem: M2 owns the state definition (doc-002 US-3: "M2's seed owns their definition"). Yet doc-002 and doc-007 seed different names for four of the five types.
- Эпик/Фича/История, state #3: «Разрешена» (doc-002:62, W-9) vs «Решена» (doc-007:123, P-1).
- Баг: «Новая»/«Активна»/«Разрешена»/«Закрыта» (doc-002:65, W-7: "one label set for all five types … whatever their gender") vs «Новый»/«В работе»/«Исправлен»/«Закрыт» (doc-007:124, P-2).
- Задача: «Новая»/«Активна»/«Закрыта» (W-2) vs «Новая»/«Выполняется»/«Готово» (doc-007:125). No P-decision stands behind doc-007's Задача row.

mod-001 would seed doc-007's list while mod-003's acceptance asserts doc-002's. Before the guard, mod-001 was the one line that could be taken immediately (C-2).
Resolution:
- Keep one state table, in doc-007 (the owner). doc-002 § Types cites it and keeps only the Resolved-on-Баг consequence; its Errata paragraph is deleted.
- Задача takes W-2's «Новая»/«Активна»/«Закрыта»/«Удалена» under every option. A-1 and D-1 agree: W-2 is the only human decision on Задача.
- Apply the Q-1 winner to every example: doc-001 US-3/US-4/US-6/US-10, doc-002 US-1/US-3/US-10/US-11, doc-007 US-3/US-5. Log the superseded W- or P- decision in its task's status.md.
- Queue guard (C-2): already applied on main in 630e0d0. Only check that the mod-001 line reads `deps:doc-007,doc-010`.
Decision: human — Q-1.

### N-2 Several teams per project (E-5) was carried back into doc-006 only
Severity: blocker
Sources: A-2, B-1, C-1, D-6
Affects:
- doc-001: § MVP boundary, M1 row, Operation completeness Team/Area, US-1, US-2.
- doc-003: BR-5, BR-6, BR-11, BR-13, Operation completeness Board, decomposition row (mod-006), US-2, US-3.
- doc-004: E-5, E-13, § Amendments, US-2, US-12.
- doc-005: BR-6, BR-10.
- doc-006: § Roles (`organizations.team.write`), ER, BR-M1-9, BR-M1-11, BR-M1-14, Operation completeness Team, § MVP boundary, § Release 2, decomposition, US-5, US-6, US-7.
- doc-010 D-2.
- Queue: mod-002 ("Team CRUD"), mod-006 ("incl. teams added later").

Problem: doc-004 (E-5, kept by E-13) has "several [teams] in MVP". Three other specs still model one team:
- doc-001:238 "exactly one default team per project";
- doc-003 BR-13 "MVP ships exactly one team per project";
- doc-006 BR-M1-9 "no action to create a second team", and its ER "exactly one in MVP".

D-2 carries back only doc-006 BR-M1-9/11, the Team row and the M1 decomposition. No spec has a create-team operation or story, and `organizations.team.write` covers "(rename, membership)" only, yet queue mod-002 promises "Team CRUD".

With n teams, the following are undefined:
- the root-area owner (BR-M1-11: "the project's team");
- the default team;
- the default area of an item a member of team B creates (BR-M1-14 gives the root, which team A owns);
- team name uniqueness;
- team deletion. doc-004:202 says it "stays doc-006's to decide", doc-006 puts it in release 2, and doc-005 BR-10 says "release 2+" while doc-005 BR-6 already handles a deleted team.

Resolution: widen the carry-back to every spec listed, using B-1's wording as the base.
- doc-006:
  - BR-M1-9: creating a project creates its first team, named after the project, with the creating administrator as its only member. An org admin adds teams at any time. A team name is 1–100 characters, unique in the project, case- and space-insensitive.
  - `organizations.team.write` covers create, rename and membership (plus delete per Q-3).
  - ER: TEAM "unique inside its project"; PROJECT–TEAM "one or more; the first comes with the project".
  - Team row: Create = org admin.
  - US-5: "its first team".
  - US-6 is retitled "manages the project's teams" and gets two scenarios: "adds a second team" and "name unique in project". C-1 proposed a new story; B-1's amended US-6 is picked because US-6 already owns team management.
  - US-7: the owner picker offers every team, plus a "re-own a child area from A to B" scenario.
  - § MVP boundary, § Release 2 and the decomposition say "the project with its teams".
  - BR-M1-11 and BR-M1-14 per Q-3 and Q-4.
- doc-001:
  - MVP table and M1 row: "one or more teams (the first created with the project)".
  - Operation completeness Team with actors.
  - US-1/US-2: "first team".
- doc-003:
  - BR-13 is rewritten, not deleted. D-6 and D-19 would delete it; B-1's rewrite is kept because it carries the guarantee that every team, incl. one added later, gets its boards. It states that several teams are MVP and that BR-5/BR-6/BR-11 are MVP behaviour tested with two real teams.
  - The US-2 re-point scenario and the US-3 Givens are rewritten with two teams, plus a cross-team parent scenario (BR-11).
  - Board row: "team deletion per doc-006 BR-M1-9".
- doc-005 BR-6/BR-10: aligned with Q-3.
- Boards for teams that predate mod-006: see N-30.

Decision: human — Q-3 (team deletion and the root-area owner) and Q-4 (per-team default area). The rest is default.

### N-3 The identifier's project and type keys are missing from their owners
Severity: blocker
Sources: A-3, D-2
Affects: doc-002 BR-5, US-4, § Decomposition ("Two keys are required of upstream"); doc-006 ER PROJECT, BR-M1-1, Operation completeness Project, US-5, decomposition; doc-007 WORK_ITEM_TYPE, § MVP: the seeded process, BR-M2-9, US-1, US-2, decomposition; doc-001 ER, glossary; doc-005 US-1, US-4, BR-12; W-10, W-11; queue mod-001, mod-002
Problem: `{ProjectKey}-{WorkItemKey}-{Sequence}` (doc-002 BR-5) needs two keys:
- a project key (W-10: 2..10 Latin capitals and digits starting with a letter, unique in the organization incl. deactivated projects, immutable, set at creation);
- the type keys EPIC/FEAT/US/TASK/BUG (W-11).

doc-002:249 says "All four lines carry it". None does:
- doc-006 PROJECT is {name, status}, and US-5 creates a project from a name alone.
- doc-007 WORK_ITEM_TYPE is {icon, colour}.

mod-001 and mod-002 ship before mod-003, so keyless projects would force a backfill of a required, unique, immutable key. Two smaller gaps:
- An address must lead "to one item, never to a namesake in another organization" (doc-002 US-4), but ORGANIZATION has only a renamable name.
- doc-005 US-4 imports by "existing work item id", and its field set has no identifier.

Resolution:
- doc-006: PROJECT.key per W-10 in the ER, BR-M1-1, Operation completeness (Update: "rename; the key never") and the decomposition row. US-5 gets four scenarios:
  - the key is required;
  - a bad format is refused with a Russian message;
  - a duplicate is refused, incl. against a deactivated project;
  - a rename keeps the key.
- doc-007: a «Ключ» column in the seed table; WORK_ITEM_TYPE {name, key}; BR-M2-9 makes the key part of the stable identity; US-1/US-2 show it; decomposition row.
- doc-001: PROJECT.key in the ER; glossary entries «Идентификатор» and «Ключ проекта».
- doc-005: «ID» joins the release-2 field set and the BR-12 default columns. CSV import matches the full identifier within the target project.
- doc-002 US-4 per Q-5.
- The mod-001/mod-002 lines are reworded (N-20).
Decision: human for the address form only (Q-5). The keys are carry-over of W-10/W-11 — default.

### N-4 Board columns and the process screen need Russian category labels nobody owns
Severity: major
Sources: A-12, D-11
Affects: doc-007 § MVP: the seeded process, US-2; doc-003 BR-2, US-1; doc-001 glossary («Статус / категория статуса»); queue mod-001, mod-006
Problem: doc-003 BR-2 says "Column wording is owned by M2/doc-007, not invented here", but doc-007 defines none. Its read-only screen still shows "each state's category" (US-2).
- A column cannot borrow a state name: one level mixes types whose states differ. Under either Q-1 option, the Истории column shows История «Активна» next to a Баг state.
- As things stand, Proposed/InProgress/Resolved/Completed/Removed reach the UI in English.
Resolution: doc-007 gains a category table with Russian labels.
- doc-003 BR-2 takes its column headers from it, doc-007 US-2 shows it, and doc-001's glossary lists it.
- Removed is never shown as a column.
- The wording differs between the reports (Q-2):
  - A-12: «Новые»/«В работе»/«Решённые»/«Завершённые»/«Удалённые»;
  - D-11: «Предложено»/«Выполняется»/«На проверке»/«Завершено»/«Удалено».
Decision: human — Q-2 (wording only; the table itself is default).

### N-5 The organization-scoped authorization model stops at M1
Severity: major
Sources: B-3, B-9, B-12
Affects: doc-006 § Roles constraints 1–5, BR-M1-7, BR-M1-8; doc-001 § Roles, BR-5; doc-002 NFR Permissions, BPMN (L102-103); doc-007 US-2; doc-005 § Roles L31-32; ADR-0018 amendment; queue mod-002
Problem: doc-006's five constraints on mod-002's ADR-0018 amendment miss four decisions:
- (a) How the panel learns org-scoped codes. It reads rights only from the token (`tokenClaims.ts`: "not from a separate server response"), yet every "no … control is offered" criterion depends on those codes.
- (b) Resource resolution for M3–M6. BR-M1-7 resolves only Area/Team → Project → Organization.
- (c) The realm role inventory. doc-001 BR-5 allows "only … two instance-level roles", yet "create organizations" needs a grantable role or group, and users added later may belong to no group and so read no dictionary.
- (d) A shared name space for instance and org codes (`organizations.organization.create` vs `.read/.write`). The realm contract test cannot filter them by name.

The non-member rule ("not found", BR-M1-8) exists only in M1. doc-007 US-2 answers a non-member "refused", and doc-002's BPMN answers «Недостаточно прав».
Resolution: doc-006 § Roles gains three constraints.
- Constraint 6, a resource-based check:
  - resolution chains for WorkItem, Iteration, Board, BacklogEntry, Query and Dashboard;
  - membership read per request;
  - "not found" for non-members;
  - the bundle holds the code;
  - an optional ownership predicate (N-8).
- Constraint 7: the panel gets the user's codes for an organization from Mustask.Api, with BR-M1-6's freshness.
- Constraint 8: the amendment names every remaining realm role and group, and who gives `dictionaries.languages.read` to a user in no group. It also updates `keycloak-seed.sh`, the E2E fixture and `src/backend/AGENTS.md`.

Constraint 1 adds: "every catalogued code carries its scope (instance | organization), and the realm contract test filters on it". Whether to rename the instance code is mod-002's call.

Other amendments:
- doc-001 BR-5: "instance-level roles only (named by the ADR-0018 amendment)". It also adds that a non-member of the resource's organization is answered "not found" in every module.
- doc-007 US-2 → «Организация не найдена».
- doc-002's BPMN checks "in this project's organization"; «Недостаточно прав» is only for members who lack the code.
- doc-005 L31-32 drops "an ADR-0018 amendment".
- The mechanisms stay mod-002's design.
Decision: default.

### N-6 The Keycloak user-directory channel breaks accepted ADR-0023 guards
Severity: major
Sources: B-4
Affects: doc-006 § Roles constraint 5, BR-M1-17, NFR Identity refresh, US-3; ADR-0023 Decision 3 and guard 3; `KeycloakRealmRolesTests.ProductionRealmImport_NoClientHasServiceAccountEnabled`; queue mod-002
Problem: constraint 5 (H-6) offers "admin API with a service account, or events" for reading the user directory. ADR-0023 forbids both halves of the first option:
- "a no-service-account property over the realm-import files". The contract test fails on any production client with `serviceAccountsEnabled: true`.
- "no shipping project's source calls Keycloak's admin REST surface at all". User search is `GET /admin/realms/mustask/users`.

The second option is a Keycloak event-listener provider — a new deployable that neither ADR mentions. doc-006 R-20, which produced constraint 5, did not see ADR-0023.
Resolution: constraint 5 gains "…and states what it changes in ADR-0023's guards". The plan records that mod-002 owes either an ADR-0023 change or a Keycloak extension, sized into the mod-002 line (N-20). Which of the two: Q-6.
Decision: human — Q-6.

### N-7 "Instance administrator" names two actors, and granting "create organizations" has no MUSTASK actor
Severity: major
Sources: B-5
Affects: doc-001 § Roles (L35-38), BR-5; doc-006 BR-M1-2, Operation completeness "Instance right", US-1, US-4; ADR-0018 § Consequences; realm `mustask`
Problem: doc-001 gives the instance administrator "every instance-level permission, incl. granting 'create organizations'". doc-006 US-1 has them grant it "in the Keycloak console, through a role or group".
- A MUSTASK realm role is a composite of `mustask-api` codes only (ADR-0018; `KeycloakRealmRolesTests` L93-130), and it opens no Keycloak console.
- Today the only actor with realm-management rights is the bootstrap administrator, which AppHost holds as a secret.
- The US-1 persona therefore cannot be tested as written.
Resolution: per Q-7. Under the recommended option:
- doc-001 L36-37 → "instance administrator (every instance-level code: 'create organizations', M0's codes)".
- doc-001 § Roles and doc-006 BR-M1-2 → "granting and revoking 'create organizations' is Keycloak realm administration … not a MUSTASK code".
- doc-006 US-1's persona and the "Instance right" row → "a Keycloak realm administrator".
- doc-006 US-4 → "lets them do only one thing — create an organization".
Decision: human — Q-7.

### N-8 The ownership predicate (D-14 and MVP authorship checks) has no model and no firm owner
Severity: major
Sources: B-2, C-7, C-20
Affects: doc-001 § Roles, Roles table, BR-5; doc-006 § Roles L81-84; doc-005 § Roles L21-33, BR-2, US-2, § Decomposition L227-228; doc-002 BR-12, BR-13, Operation completeness Comment/Attachment; doc-010 D-2; ADR-0018; queue mod-002, mod-008, feat-002, mod-004
Problem: D-14 (a viewer writes their own private objects) must reach doc-001 (ex-doc-008).
- Ownership checks already exist in MVP: a comment is edited "only by its author" (doc-002 BR-12), and an attachment is removed by "member (own)". No spec says whether ownership narrows a role code or replaces it. For example, can an author downgraded to viewer still edit their comment?
- doc-001's Roles table has the viewer holding "read codes only" and gives restore to the admin only. doc-001 L297 and doc-002 L163 let a member restore.
- The owner is conditional: doc-005:227 says "mod-002 if that has not shipped yet, else a follow-up M1 task". mod-002's line says nothing about it, and no follow-up line exists.
- doc-005:31 and :228 still name the withdrawn `doc-NNN-personal-object-ownership`.
Resolution: doc-001 § Roles takes B-2's text.
- A code may carry an ownership predicate, checked after the code on the resolved object.
  - (1) A role code narrowed to one's own objects — editing or deleting one's own comment, removing one's own attachment — is in the member bundle. The admin bundle holds the unrestricted moderation code. The right goes with the role: an author downgraded to viewer no longer edits.
  - (2) A personal-object code is in every bundle, viewer included (D-14). It covers create, read, update, delete and restore of the user's own private objects; in release 2 these are personal queries and «Мои запросы».
  - No bundle reads another user's private object. The one exception is the org admin's purge (N-13).
- Roles table: admin "purge deleted work items"; member "delete and restore a work item"; viewer "read codes plus the personal-object code".
- doc-001 BR-5 appends the predicate and the condition "while they hold a membership".
- doc-006 § Roles: "outside M1 the viewer bundle is not read-only".
- doc-005:
  - § Roles: expressed through doc-001's personal-object code;
  - BR-2: "whatever the owner's role, while they are a member";
  - US-2: "the one write a viewer may perform in release 2";
  - decomposition row: "absorbed by doc-010 (D-2)" plus the code owner per Q-8 (C-20).
- doc-002 BR-12/BR-13 and Operation completeness: "author (member or admin role)" and "uploader (member or admin role)".
- The reports disagree on the owner:
  - B-2: mod-002's amendment defines the predicate, and mod-008 registers the personal-object code.
  - C-7: mod-008 does all of it (alternatively, mod-002 does all of it).

  This is Q-8.
Decision: human — Q-8. B-2 generalises D-14 to "any object a spec declares personal", and that wording is part of the default text; Q-8 lets the human narrow it to queries.

### N-9 There is no permission-code catalog beyond M1
Severity: major
Sources: B-7
Affects: doc-002 § Roles (L29-33), L251-255; doc-003, doc-004, doc-005, doc-007 (no codes); doc-006 L65-84; queue mod-001, mod-003..mod-008, feat-001, feat-002, feat-004, feat-008..feat-010
Problem: only doc-006 has a code table. doc-006 L81-82 assumes "the work item, board, backlog and iteration codes owned by M3/M4/M5", but no spec names them. Six operations each have two candidate codes owned by different tasks:
- card drag: the M3 state write or an M4 code;
- iteration pointer writes made from M5 surfaces;
- backlog parent mapping vs feat-001's link;
- team iteration selection: `organizations.team.write` or an M5 code;
- delete/restore: registered by mod-003 per doc-002 L30, but founded by mod-005;
- the Drive connection: doc-002 L252-255 says "doc-006 only notes that it will exist", and doc-006 is silent.
Resolution: doc-010's spec carries B's § Permission codes as the analysis-level catalog, each code with its registering task. Rules:
- One prefix per module: `dictionaries`, `organizations`, `process`, `workitems`, `boards`, `iterations`, `queries`/`dashboards`.
- A code guards the entity an operation writes. Card drag, pointer write, pane drop and bulk move are `workitems.workitem.write`; the backlog mapping is `workitems.link.write`.
- M4 adds only `boards.backlog.reorder`. M5 adds `iterations.iteration.*` and `iterations.team-settings.write`.
- mod-005 registers delete/restore.
- mod-004 registers the Drive connection as `workitems.attachment-storage.manage`, hosted on doc-006's organization settings (one line in doc-006 § Roles).
- mod-001 registers `process.definition.read`.
- Every founder's decomposition row lists the codes it registers. The exact strings stay the founder's.
Decision: default. The human may object to a single placement, e.g. a separate M4 code for card moves.

### N-10 Team membership is given an access meaning the model does not have
Severity: major
Sources: A-16, B-6
Affects: doc-006 § Roles (L83-84), US-6; doc-003 § Roles (L24-27); doc-004 BR-12, US-12; doc-005 Operation completeness (Dashboard team); doc-001 BR-3, BR-5
Problem: the specs give team membership different meanings.
- doc-006 L83-84: team membership "decides which board and backlog a user sees (BR-3)".
- doc-003 L26 lets any reader "open any of their organization's team boards and backlogs".
- doc-004 lets any member move any team's unfinished work, and doc-005 lets any member edit any team's dashboard.
- doc-001 BR-3 routes items by Area, not by people, and BR-5 forbids any scope finer than the organization.

With several teams (N-2), this decides whether a member of team A may drag cards on team B's board.
Resolution: doc-006 L83-84 → "Team membership carries no role and no access of its own: any organization member reads, and a member or administrator acts on, every team's boards, backlogs, planning pane and (release 3) dashboards (BR-5)." Team membership then:
- decides which team a user's board and backlog open on by default;
- from release 2, decides whose capacity is planned (doc-004 US-7) and feeds the "In Group" clause.

US-6's so-that → "…so that the team's areas and default views have the right people". Restricting a board to its team would reopen doc-001 D-9.
Decision: default.

### N-11 The "area with no owning team" branch is unreachable but specified and tested
Severity: major
Sources: A-4, D-5
Affects: doc-006 BR-M1-11 (H-9), US-7; doc-001 BR-3, US-2, US-8; doc-003 BR-5, BR-13, § Data model BACKLOG_ENTRY, US-2; doc-004 BR-7, BPMN node H, US-3, US-13
Problem: doc-006 BR-M1-11 (human H-9) says the root "is always owned by the project's team — its owner cannot be changed or cleared. So every area resolves to an owning team". Yet many rules and scenarios build on the opposite:
- doc-001: US-2:45-46 ("…not shown on any team's board") and US-8:37-41;
- doc-003: BR-5 ("…has no BACKLOG_ENTRY"), BR-13, BACKLOG_ENTRY ("an Area becoming unowned removes the entry") and two US-2 scenarios;
- doc-004: BR-7 («У области элемента нет команды-владельца»), BPMN node H, US-3 and US-13.

None of their Givens can be set up. doc-006 R-6 fixed the same dead branch in doc-006 US-7 only.
Resolution:
- Keep BR-M1-11: it stays true with several teams, with the root owner per Q-3.
- State once, in doc-001 BR-3: "every area resolves to exactly one owning team".
- Delete the unowned clauses and scenarios:
  - doc-001: US-2's last clause and US-8 sc.4;
  - doc-003: BR-5's second clause, the BACKLOG_ENTRY clause and US-2's two scenarios;
  - doc-004: BR-7's second refusal and its message, the BPMN node H branch, the US-3 scenario and the US-13 clause.
- Replace doc-003 US-2's re-point scenario with the reachable one: the item's Area is re-owned from team A to team B, so the item leaves A's backlog and is appended to the bottom of B's (BR-6).
- The reports differ on BR-5/BR-7. A-4 deletes the clauses; D-5 keeps a defensive one-line invariant without a UI message. A-4 is picked: a rule no acceptance test can reach is not a requirement, and the invariant lives in doc-001 BR-3 and M1.
- If Q-3 admits team deletion, the deletion re-owns the team's areas first.
Decision: default.

### N-12 The deletion lifecycle is stated as one principle that the modules contradict
Severity: major
Sources: A-6, D-7, C-8
Affects: doc-001 BR-6, US-1, Operation completeness, ADR list (:366-372); doc-006 BR-M1-1, lifecycle (:132-140), Operation completeness (Membership, Area), US-2, US-5, US-7; doc-004 ITERATION.isActive, BR-10; doc-005 BR-9, BR-10, data-model note; doc-002 BR-12, BR-13; queue mod-002, mod-005, mod-007, mod-008, mod-009
Problem: doc-001 BR-6 says "Deletion is always soft, never a hard delete … only an administrator purges a deleted object permanently, by hand". The modules implement six lifecycles:
- work items: the state «Удалена»;
- organizations and projects: "deactivated", with no purge (doc-006 BR-M1-1);
- iterations: "soft" but invisible and irreversible (doc-004 BR-10);
- M6 objects: a "deleted status";
- area leaves, ORG_MEMBERSHIP, comment text and attachments: removed outright;
- a single M6 status carries two causes. A query its owner deleted before its project was deactivated comes back when the project is restored (doc-005 BR-10).

One concept has five names: "archives", "deactivated", "deleted status", `isActive`/"removed" and «Удалена». The only soft-delete ADR is owed by mod-005, although mod-002 builds the status mechanism first and mod-007/008/009 reuse it.
Resolution: rewrite doc-001 BR-6 as a principle plus a per-entity table: who deletes, restores and purges, and what stays reserved.

| Entity | Deleted as | Restore | Purge | Reserved afterwards |
|---|---|---|---|---|
| WorkItem | «Удалена» | member, admin | org admin (N-13) | — |
| Organization, Project | deactivated | org admin | none in MVP | name |
| Query, Folder, Dashboard, Widget | deleted status | whoever may delete | per Q-9 | — |
| Iteration | soft, no restore | none | none | sprint number (name freed) |

Removed outright, with a history or who/when trace: ORG_MEMBERSHIP, team membership, Area leaf, WorkItemLink, comment text, attachment link, tag on an item.

Other amendments:
- doc-005 separates an object's own deleted status from the status inherited from its project (BR-10 and the data-model note).
- doc-001 US-1: "archives" → "deactivates". Russian verbs: see N-29.
- ADRs: mod-002 owes the ADR for status-based soft deactivation — hidden from lists, restore, names reserved, cascade to children. mod-005's ADR covers the state-as-recycle-bin and references it. doc-001's ADR list and both queue lines are updated.
Decision: default.

### N-13 Purge is promised where no operation, story or code exists
Severity: major
Sources: B-8, C-10
Affects: doc-001 BR-6, § Roles (L42), US-1 (L81-82); doc-006 BR-M1-1, § Out of scope (:314); doc-004 BR-10, Operation completeness Iteration; doc-005 BR-9, Operation completeness, D-13; doc-002 US-10; queue mod-005, mod-007, mod-008, mod-009
Problem: the specs promise purges that no operation delivers.
- Projects: doc-001 US-1:80-82 says a project "stays recoverable … until an administrator purges it by hand", while doc-006 BR-M1-1 says "MVP has no purge".
- Iterations: doc-004 BR-10 says "an administrator purges by hand", but its Operation completeness row says "n/a".
- M6 objects: doc-005 BR-9 (D-13) says "only an administrator purges it by hand" and makes purge "the one action an administrator may take on another user's soft-deleted personal query". Yet the mod-008/mod-009 rows list only soft delete and restore.
- "Administrator" is never qualified.

Only the work-item purge (doc-002 US-10, mod-005) is a specified operation.
Resolution: under every option:
- "administrator" means the organization administrator;
- MVP has one purge, the work item (mod-005);
- organizations, projects and iterations have no purge in MVP:
  - doc-001 BR-6;
  - doc-001 US-1: "stays recoverable indefinitely (no purge in MVP, doc-006 BR-M1-1)";
  - doc-004 BR-10: "no reactivation and no purge in MVP".

The reports disagree on M6 (Q-9):
- A-6, B-8 and D-7 keep an in-product purge by the org admin (release 2/3). Its purge list shows owner, folder and deletion date — never the name or clauses.
- C-10 makes every non-work-item purge an operator procedure outside the product.
Decision: human — Q-9.

### N-14 Nobody defines how an item in «Удалена» takes part in other modules' operations
Severity: major
Sources: A-8, D-3, D-8
Affects: doc-001 BR-6, BR-10; doc-002 BR-8, BR-9, BR-14, US-2, US-8, US-10, US-11; doc-003 BR-10, BR-16, BR-17, BR-19, US-1, US-2, US-3; doc-004 BR-10, BR-12, BR-13, US-14; doc-005 BR-4, BR-8, US-4; doc-006 BR-M1-13, US-7; doc-007 BR-M2-6
Problem: doc-002 BR-14 makes a deleted item read-only, and doc-001 BR-10 says «Удалена» is "left only via Restore". The other modules break or ignore this:
- doc-003 BR-19 has a drag "land immediately … with no check". A stale card dragged after a delete overwrites «Удалена» with a live state — a restore without Restore, and without history.
- doc-004 BR-10 clears "every pointer" of a removed iteration, deleted items included. That writes to a read-only item, while BR-12/BR-13 exclude deleted items and US-14's confirmation count says nothing about them.
- doc-006 BR-M1-13 deletes "a leaf with no work items" without saying whether deleted items count. If they do not, a Restore points at a missing area.

Also open:
- tag suggestions;
- visibility in queries and CSV, and CSV writes to deleted items;
- a live child of a deleted parent is deadlocked: it can neither take a new parent nor drop the old link;
- an area re-owned while its item is deleted;
- a release-3 state removal vs an item's restore target.
Resolution: add one participation rule to doc-001, next to BR-6 (from D-8):
- (a) Visibility: a deleted item is excluded from every list, board, backlog, pane count, bulk move, tag suggestion, query result and export, unless a filter or clause names «Удалена». doc-005 BR-4 is amended to match.
- (b) References: the item keeps its area, iteration, tags, state and restore-target state.
  - Deleting an area that holds it is refused: doc-006 BR-M1-13 reads "work items, deleted ones included", with its message and US-7 updated.
  - A release-3 state removal is refused while any item is in that state or remembers it (doc-007 BR-M2-6).
- (c) Writes: only Restore, Purge and system clean-up change a deleted item.
  - doc-001 BR-10: "no operation other than Restore changes the state of an item in «Удалена», whatever its concurrency policy".
  - doc-002 BR-14: "read-only for users; system reference clean-up still applies and is recorded in history".
  - doc-004 BR-10/US-14 clear its pointer and count it in the confirmation.
  - doc-003 BR-19: "the no-check rule covers live items only; a move or reorder of a deleted or purged item is refused with the stale-move notice and a refresh". doc-003 US-1 and US-2 get one scenario each.
  - A CSV row that targets a deleted item is rejected.
  - A child whose parent is deleted may remove the link from its own side, recorded in both histories. The mapping action treats that child as having no parent (N-18).
- (d) If an item's Area is re-owned while it is deleted, its retained entry is re-scoped on Restore and appended at the bottom of the new team's backlog (doc-003 BR-16/BR-17).
Decision: default.

### N-15 doc-002 has no logical data model, so M3 attributes other modules use have no owner
Severity: major
Sources: A-5
Affects: doc-002 (BR-5, BR-15, US-2, US-5..US-11, US-13); doc-001 decomposition row doc-002 and ER; WORK_ITEM, COMMENT, ATTACHMENT, TAG, HISTORY_REVISION, WORK_ITEM_LINK, Drive connection; queue mod-003 (history ADR)
Problem: doc-001 asked doc-002 for "WORK_ITEM/WORK_ITEM_LINK/COMMENT/ATTACHMENT/TAG/HISTORY_REVISION entities". Unlike doc-003..007, doc-002 has a field table only. These attributes are therefore unowned:
- Work item: the per-project sequence; created and changed (doc-002 US-11 sorts "most recently changed first"; doc-005 has «Дата изменения»); the restore target, stored or derived; the concurrency revision.
- Comment: author, edited-at, a deleted marker.
- Attachment: name, size, uploader, upload time, the Drive reference.
- Tag: stored or derived is undecided. US-8 keeps "its original spelling «Аудит»", yet an unused tag "stops being offered".
- HistoryRevision: its structure, which mod-003's history ADR has to design against.
- Drive connection: organization 1:0..1, connected by/at, the credential reference.

doc-001's ER has no USER edge for a comment's author, an attachment's uploader or a revision's author.
Resolution: doc-002 gains a "Logical data model (M3)" section at doc-003's depth: an ER plus an entity table (key attributes, stored vs derived, uniqueness) covering the above. Defaults:
- Tag is a stored project entity. Its canonical spelling is the first one, kept while the tag is unused.
- Created and changed are derived from revisions.
- The spec states whether the restore target is stored or derived.
- USER edges for author, uploader and revision author.

The model is reflected in doc-001's ER (N-19).
Decision: default.

### N-16 Concurrency policy is stated six times, differently, and its client token has no ADR owner
Severity: major
Sources: D-4, C-9, D-13
Affects: doc-001 BR-11, US-4, US-8; doc-002 BR-15, BPMN, US-3; doc-003 BR-12, BR-19 (DD-2), NFR Refresh model, US-1, US-2; doc-004 US-3, US-11, US-12, US-14, NFR Bulk writes; doc-005 BR-8, NFR Concurrency; doc-006 NFR Concurrency; ADR-0010; queue mod-003, mod-006
Problem: the specs contradict each other on concurrency, and no line owns the client token.
- State writes: doc-002 BR-15 checks Статус "exactly as for any other field", but doc-003 BR-19 (human DD-2) never checks a drag. mod-003 would build a state write that mod-006 cannot use.
- Drags: doc-001 BR-11 makes board drags last-write-wins, while doc-004 US-11 rejects a pane drop "as a conflict".
- No stated policy: the mapping action, CSV update rows, iteration administration and comment edits.
- The token: ADR-0010 says "xmin is never exposed externally … If an ETag/If-Match scenario is ever needed … a separate decision". The form save and the stale-move notice need exactly that scenario, and no line owns it.
- The notice cannot be tested. doc-001 US-8 and doc-003 US-2 give it to "the other member", doc-003's NFR only to the later mover, and it has no Russian text.
Resolution: doc-001 BR-11 becomes an operation → policy table (from D-4). doc-002 BR-15, doc-003 BR-12/BR-19 and doc-004 US-11 cite it.

| Operation | Policy |
|---|---|
| Form save (incl. Статус, Теги, type fields, Итерация) | revision check; the loser reloads |
| Card move | last write wins, no check; advances the revision; refused on «Удалена» (N-14) |
| Reorder | last write wins, plus the stale-move notice |
| Pane drop | revision check (it writes the pointer) |
| Mapping | link write; the one-parent invariant is enforced atomically; the loser is refused |
| Comment, attachment or link add | never blocked by a stale field revision |
| Bulk move, iteration removal | count-confirmed, all or nothing |
| CSV update row (release 3) | last write wins per row, with history |
| M1 check-then-act | one of two conflicting changes is refused |
| M6 shared objects | revision check |

Notice scenarios, rewritten per D-13:
- Different items: both moves apply in arrival order. The second mover gets «Порядок изменён другим участником — список обновлён» and a refresh.
- Same card: the second drag's column is final, and the second mover gets the notice.
- doc-003 records the text.

ADR: mod-003 owes "client-visible concurrency token", since its first use is the form save. mod-006 reuses it. doc-001's ADR list and both queue lines are updated.
Decision: default. For the CSV update row, the human may prefer an optional revision column in the file.

### N-17 doc-001 promises full attribution that the modules do not deliver
Severity: major
Sources: D-12, D-14
Affects: doc-001 BR-4, NFR Audit/retention, Operation completeness Comment, US-5; doc-002 BR-10, NFR Audit, Operation completeness Comment, US-6, US-10; doc-003 (drag, reorder); doc-004 NFR Audit; doc-005 NFR Audit; doc-006 BR-M1-15, BR-M1-16, NFR Audit; queue mod-003 (history ADR)
Problem: doc-001's NFR says "every change, delete and restore is attributable to a user and a timestamp". In MVP, the following go unrecorded:
- organization and project deactivation (doc-006 BR-M1-15: release 2);
- iteration administration and team selection;
- all of M6;
- the M4 order.

A purge leaves no tombstone, and doc-002 US-10 does not say that the surviving children's link removal is recorded. doc-001 BR-4 lacks create and tag add/remove.

Comment edits contradict each other twice:
- doc-001 Operation completeness says "edit history stands"; doc-002 says "the earlier text is not restorable".
- doc-001 US-5 says "the edit shows in history, not in the comment itself"; doc-002 US-6 says "marked as edited with the time of the edit".
Resolution:
- Scope doc-001 BR-4 and its NFR to work items. Merge in doc-002 BR-10's additions: create, tag add/remove, one save = one revision.
- Add a list of what MVP does not audit: M1 beyond membership who/when, M2, the M4 order, M5 iteration administration, M6. The purge itself stays unattributed until release 2's audit log; this is accepted and said.
- A card move is a state revision authored by the mover.
- A purge records "parent purged: <identifier>" in each surviving child's history.
- A comment edit records that the text changed, not the old text. The comment carries an "edited" marker with the edit time. doc-002 wins, and doc-001 US-5 and Operation completeness are amended.
- mod-003's history ADR is scoped to anticipate BR-M1-15 (release 2), mod-010's Was Ever and mod-012's metrics (C's ADR table).
Decision: default.

### N-18 "Give an item a different parent" has opposite outcomes in M3 and M4
Severity: major
Sources: A-7, D-9
Affects: doc-002 BR-8, BR-10, W-1, US-5, Operation completeness WorkItemLink; doc-003 BR-10, BR-11, US-3, Operation completeness BacklogEntry; queue feat-001, feat-004
Problem: M3 and M4 give "a different parent" opposite outcomes.
- doc-002 US-5:33-39 refuses a different parent: "re-parenting is removing the old link and adding the new one". Links are never updated in place.
- doc-003 BR-10 "creates or replaces the same Parent/Child link", and US-3 says "Assigning a different parent replaces the existing link".
- feat-004 writes "through M3's Parent/Child link", which has no replace operation and no history rule for one.
- doc-003 offers only parents "one level up". That narrows W-1's "higher or equal" (e.g. a Баг under a История) without saying so.
Resolution:
- doc-002 BR-8 gains a named, atomic "replace parent" operation: remove and add in one write, refused under the same conditions as add. It is recorded as a removal on the old parent, an addition on the new parent and one revision on the child.
- doc-003 BR-10 cites it.
- doc-003 US-3 states that mapping targets only the level directly above; same-level links are made on the form.
- The reports differ on the form. A-7 offers the replace there as well; D-9 keeps doc-002 US-5's "refuse and name the existing parent". D-9 is picked:
  - it leaves an approved scenario untouched;
  - it matches the reference product (the form refuses a second parent, the mapping re-parents);
  - nothing in MVP needs re-parenting from the form.
Decision: default.

### N-19 The umbrella doc-001 no longer reflects the module refinements
Severity: major
Sources: A-10, C-11
Affects: doc-001 § MVP boundary, release tables, cross-module ER, ownership table, ADR list, US-9; doc-002 errata; doc-004 § Amendments; doc-003..007 data models; doc-010 D-2
Problem: each module recorded its supersessions its own way:
- doc-004:196-198: "doc-001 is not edited here … this table is the record";
- doc-002: an errata note ("doc-001 is not reopened");
- doc-005 and doc-004 E-13: carry-back tasks.

As a result doc-001 is stale in several places.
- MVP table: it omits the planning pane (E-6), moving unfinished work (E-9), the iteration tree (E-7), type-specific fields (W-5), per-organization Drive (W-12), the read-only process screen and the project key (W-10).
- US-9 still says "flat iteration list (MVP: no tree)".
- Release plan: "the taskboard" and "the Задачи-level board" are listed as two items, though they are one.
- ER:
  - ITERATION {name, startDate, finishDate} differs from doc-004's model;
  - BOARD_COLUMN → STATE_DEFINITION differs from doc-003's (board, category);
  - PROJECT–PROCESS is drawn as stored, though it is derivable (doc-007 P-4).
- Ownership table: it lacks QueryFolder, the Dashboard levels, the Drive connection, the release-2/3 entities and team membership.
Resolution: doc-010 delivers one consolidation, in place (Q-13).
- Rewrite the MVP rows for M1/M3/M4/M5 and the release-2 M5 line. M5 owns the taskboard and reuses M4's BOARD.
- List the superseding decisions with their sources.
- Fix US-9's scope and its "delivered by" (feat-008).
- Amend the ER to the module shapes: PROJECT–PROCESS derived, PROJECT.key (N-3), the USER edges (N-15).
- Complete the ownership table with every entity and its release.
- doc-006's organization settings name the Drive-connection section that mod-004 adds.
- doc-004 § Amendments and doc-002's errata then point to doc-001.
Decision: human — Q-13 (in place, or superseded by the product plan). The content is default.

### N-20 Six queue lines state less scope than their spec rows
Severity: major
Sources: C-14
Affects: queue mod-001, mod-002, mod-003, mod-004, mod-005, feat-003; doc-006 decomposition row (:327); doc-007 decomposition row (:269); doc-002 decomposition (:242)
Problem: `new-task.sh` files a task "character by character" from the line, and route question 3.3 measures scope against the line. Six lines state less than their spec rows:

| Line | Missing from the line |
|---|---|
| mod-001 | the read-only process screen ("a full-stack slice"); the W-11 keys |
| mod-002 | the project key; the user-directory channel; the last-admin and inactive rules. It also says "Team CRUD" |
| mod-003 | the list surface; the identifier format. doc-002:242 says "the note records the growth", but it does not, because "queue.sh cannot edit an existing line's note" |
| mod-004 | per-organization Drive; US-13; the external counterparty; secrets |
| mod-005 | purge |
| feat-003 | the keyword filter; former members; deleted items |

Resolution: after gate-1 the orchestrator rewords the six lines by hand in queue-only commits (precedent: 486295a, 6d70708). The rewording folds in the ADR obligations these entries assign:
- mod-002: the ADR-0018 amendment under constraints 1–8, the ADR-0023 change per Q-6, the soft-deactivation ADR, and the team scope per Q-3/Q-4;
- mod-003: the history ADR scope and the concurrency-token ADR;
- mod-005: the recycle-bin ADR, referencing mod-002's.

The doc-006 and doc-007 decomposition rows carry W-10 and W-11.
Decision: default.

### N-21 The recycle bin and the backlog filter sit behind the lines that need them
Severity: major
Sources: C-3
Affects: queue feat-003, mod-005, mod-006, feat-008; doc-002 US-10, US-11, decomposition row (:238); doc-003 US-2, BR-7, decomposition; doc-001 US-10
Problem: two filter capabilities are queued after the lines that depend on them.
- Recycle bin: deleted items are reachable only through feat-003's state filter (doc-002 US-11:63). feat-003 depends on mod-006, which depends on mod-005, so mod-005's restore has no UI path until feat-003 ships. That is a capability cycle: mod-005 → feat-003 → mod-006 → mod-005.
- Backlog filter: "Given a team's backlog with a filter applied" is part of mod-006's acceptance (doc-003 US-2), but feat-003 filters only "the list and the board". feat-008 assumes "feat-003's filter bar" on the backlog. No line delivers the backlog filter.
Resolution: per Q-10. The recommended option splits the filters by surface:
- feat-003 keeps the list filters (state incl. «Удалена», assignee incl. former members, tag, area, keyword) and is re-depped to mod-005.
- A new M line, `feat-NNN-board-backlog-filter` (deps mod-006, feat-003), adds the filter bar on the board and the backlog. It takes doc-003 US-2's filtered-order scenario out of mod-006.
- feat-008 is re-depped onto the new line.
- Amended: doc-002 row :238, doc-003's decomposition and doc-001 US-10's "delivered by".
Decision: human — Q-10.

### N-22 Parallel lines take on parts of mod-005's behaviour with no ordering
Severity: major
Sources: C-13
Affects: queue mod-004, mod-005, feat-001, feat-002, feat-007; doc-002 rows :236-237, BR-14, US-10
Problem: mod-004's row "deletes a purged item's files from Drive (US-10)", but purge is mod-005's. US-10:73 requires that "a purge never ends with the item gone while its files remain in Drive".
- Both lines depend only on mod-003, so either may land first, with no partner to integrate with.
- BR-14 (a deleted item is read-only) must also hold for the comment, link, attachment and type-field writes of feat-001, feat-002, mod-004 and feat-007, which run in parallel with mod-005. No row says who enforces it.
Resolution:
- Re-dep mod-004 onto mod-005. mod-004 is off the critical path, so this costs no time.
- doc-002's decomposition gains the rule "BR-14 is enforced by mod-005 for the writes that exist when it ships, and by each later line for its own writes". The rows for feat-001, feat-002, mod-004 and feat-007 repeat it.
Decision: default.

### N-23 Iteration and area conditions mean different things on each surface
Severity: major
Sources: A-15, C-6, D-10, D-16
Affects: doc-001 BR-7, BR-9, release-2 plan (:249-250), US-9; doc-002 US-11; doc-003 § Out of scope (:130-134); doc-004 BR-6, BR-7, BR-8, BR-9, § Amendments, US-2, US-11, US-13; doc-005 BR-4, BR-5, BR-6, US-1; queue mod-008, mod-010
Problem: the same conditions mean different things on different surfaces.
- Subtrees:
  - The MVP inline filter matches subtrees for Область (doc-002 US-11) and Итерация (doc-004 BR-9 "or at any of its descendants").
  - The release-2 query offers Область "=, In" and Итерация "=" only. "Under" arrives in release 3, for Область alone.
  - doc-001 promises that inline filters "graduate into personal saved queries" in release 2, and its US-9 shows "only work items assigned to" the iteration.
- The current iteration:
  - Its derivation (UTC and four tie-breaks) lives only in doc-004 BR-8, while doc-005 BR-6 cites doc-001 BR-9.
  - doc-005 puts the current iteration into team-bound project queries and does not say whether their results narrow to the team.
- doc-003 says doc-001 BR-7 "is read by M5's planning surfaces, not by M4's MVP views". feat-008 and feat-009 sit on M4's surfaces.
- doc-001 US-9 sc.3 assigns an iteration without doc-004 BR-7's team-selection precondition.
- The team's default iteration is where the reference product lands new items. In MUSTASK it only decides the current iteration, and no spec says so.
Resolution: the default part:
- The derivation moves into doc-001 BR-9. doc-004 BR-8 keeps "derived on read; on team surfaces and in team-bound queries".
- doc-005 BR-4: a team-bound @CurrentIteration does not narrow results to the team's areas.
- doc-003's out-of-scope sentence → "M4's MVP views read the pointer only through the iteration filter (feat-008) and the planning pane (feat-009)".
- doc-001 US-9 sc.3-6 gain the team-selection Given and subtree wording, and doc-004 § Amendments lists them.
- doc-004 BR-6, plus a US-2 scenario: "the team's default iteration is never assigned to new work items; it only decides «Текущая итерация» when no dates do".

Operator semantics — the reports differ:
- D-10 makes every iteration condition match descendants, "=" included.
- A-15 and C-6 add an "Under" operator on Область and Итерация and keep "=" exact.

A-15's split is picked:
- "=" is still needed for items sitting on a parent node;
- it matches the reference product;
- "Under" reproduces the inline filter exactly;
- @CurrentIteration works with both operators.

Whether "Under" and the keyword clause ship in release 2 (mod-008) or release 3 (mod-010) is Q-12.
Decision: human — Q-12.

### N-24 Release-2 planning rests on estimation fields no spec owns
Severity: major
Sources: A-9, C-5
Affects: doc-002 § Out of scope (:199-201), § Decomposition; doc-004 E-8, BR-15, BR-16, US-7, US-8, US-9, rows :236/:238; doc-007 FIELD_DEFINITION note; queue mod-011, mod-012 (feat-011 and feat-012 inherit)
Problem: doc-004 E-8 gives the estimation fields to doc-002.
- doc-004 BR-16 "read[s] estimation fields owned by doc-002", and its rows depend on "doc-002's estimation-fields slice".
- doc-002 lists these fields only as out of scope for release 2 and has no row for them.
- The queue records the gap as a note ("not yet queued", queue:35/:37), not as a dependency.
- US-7:35, US-8:63 and US-9:31 need estimates. Capacity (BR-15) also needs an Activity list, which has no owner.
Resolution:
- doc-002 gains a release-2 section: the fields, the types carrying them, the rules and the surfaces.
- doc-002's decomposition gains a row `feat-NNN-work-item-estimation-fields`. It is M because the fields are additive on M3's own WorkItem; deps mod-003; epic work-items.
- Activity is a fixed M3 list with Russian labels, like Приоритет.
- mod-011 and mod-012 depend on the new line, and doc-004 rows :236 and :238 cite it.
- doc-007's FIELD_DEFINITION note adds "release 2 adds the estimation fields, still M3-fixed".
- Which fields: the reports differ (Q-11). A-9 proposes the ADO Agile set; C-5 proposes exactly what doc-004 US-7..US-9 read.
Decision: human — Q-11.

### N-25 CSV import/export is placed in two releases and two modules
Severity: major
Sources: C-4
Affects: doc-001 § Release 3 (:255); doc-002 § Out of scope (:204); doc-003 § Out of scope (:129); doc-005 decomposition (:230); queue feat-005
Problem: doc-001:255, doc-005:230 and the queue put CSV in release 3, under M6 (feat-005). Two specs place it elsewhere:
- doc-002:204 says "bulk edit, templates, CSV import/export — release 2 (doc-001 release plan)", citing a plan that says the opposite.
- doc-003:129 says "Bulk edit and CSV export from the backlog view — release 2, module M3".
Resolution: doc-002:204 and doc-003:129 → "CSV import/export — release 3, M6, feat-005". If export from the backlog view is wanted, it is a release-3 extension of feat-005, not a second M3 line.
Decision: default.

### N-26 No rule decides which release-2/3 work is queued
Severity: major
Sources: C-12
Affects: all release-2/3 queue lines; doc-001 § Release 2/3; doc-006 § Release 2; doc-007 decomposition; doc-002/doc-003 § Out of scope
Problem: the release-2/3 queue follows no rule.
- doc-004's and doc-005's release rows are queued.
- doc-007's eight release-3 rows are not queued, by the human's choice.
- doc-006:287 says its items are "not queued by this task".
- doc-001's release-2 items for M1, M3 and M4 have no rows at all.
- Other release-2 items exist only in module out-of-scope lists: other link types, change type, tag management, live push, the per-team level toggle, parent grouping rows and the instance-rights screen.
- Two items are claimed twice: notifications (doc-006:290 in M1 vs doc-002:208 in M3) and audit (doc-002:188 vs doc-006 BR-M1-15).

The queue cannot yield a release-2 milestone.
Resolution: adopt one rule (Q-14). doc-010's product plan then lists every release-2/3 item with one owning module.
Decision: human — Q-14.

### N-27 M2's ordering attributes and initial state are restated, not modelled
Severity: minor
Sources: A-11, D-17
Affects: doc-007 logical model, BR-M2-2, BR-M2-4; doc-002 § Fields (Статус), BR-8, BPMN node C, US-1, US-5; doc-003 BR-1, BR-2, BR-11; doc-005 BR-8, US-4
Problem: doc-007's ER shows attributes only for WORK_ITEM_TYPE {icon, colour}. Other modules compute with M2 attributes that M2 never models:
- a total order of backlog levels (doc-002 BR-8 "higher than or equal", doc-003 BR-11 "exactly one level below");
- a level kind that decides which levels get a board (doc-003 BR-1);
- the state order (doc-003 BR-2 "M2's seed order").

The initial state is defined two ways: "the type's Proposed state" in doc-002 and "the first … default initial state" in doc-007 BR-M2-2. The two agree today and diverge once release 3 allows custom states. Nothing says whether Статус can be set at create, though CSV import creates items "through the same validation path".
Resolution:
- doc-007's model:
  - BACKLOG_LEVEL {name, rank, kind: portfolio|requirement|task};
  - STATE_DEFINITION {name, category, order};
  - WORK_ITEM_TYPE {name, key, icon, colour}.
- doc-007 BR-M2-4: "levels are totally ordered by rank; M3 BR-8 and M4 BR-11 read the rank; M4 BR-1 reads the kind".
- doc-002 § Fields, Статус, cites BR-M2-2: an item is "created in its type's first state; the create form shows Статус read-only; a CSV row naming another state for a new item is rejected with the reason". doc-005 BR-8 and US-4 are aligned.
Decision: default.

### N-28 The assignee's inactive / former-member markings are unresolved
Severity: minor
Sources: A-13, D-15
Affects: doc-006 BR-M1-17, H-8, US-3; doc-002 § Fields (Исполнитель), W-3, US-2, US-11; doc-001 BR-13
Problem: doc-006 BR-M1-17 renders «неактивен» and hands over the question: "whether it remains an assignee candidate is for doc-002-work-items to state". doc-002 never answers; its "candidates = every current member" includes disabled accounts. W-3's "former member" marking has no Russian label, and a user who is both removed and disabled would carry two markings.
Resolution: doc-002 § Fields, Исполнитель:
- An inactive account is not offered for a new assignment, as H-8 rules for the pickers.
- An inactive account that is already assigned stays, marked «неактивен».
- A removed member is marked «не в организации»; this marking prevails over «неактивен».
- US-2 and US-11 gain the scenario.
Decision: default.

### N-29 Vocabulary drift: field set, labels, type names, actors and verbs are restated instead of cited
Severity: minor
Sources: A-14, A-19, B-10, D-18
Affects: doc-001 glossary, § Roles, US-1..US-10; doc-002 § Fields, US-1..US-12; doc-003 US-1..US-3; doc-004 § Terms, US-1/2/3/5/9/10/12/13/14; doc-005 § Glossary, US-1, US-4, US-5, BR-12; doc-006 US-2, US-5; doc-007 § MVP (field layout), US-1, US-2
Problem: the specs restate the model's vocabulary instead of citing it, and the copies drift.
- Field set:
  - doc-007 lists the MVP fields without Итерация and without the type-specific fields.
  - doc-005 US-1 bases its field set on doc-001 because "doc-002 has not shipped". It uses «Тег»/«Тип рабочего элемента» where doc-002 has «Теги»/«Тип», and has no identifier.
- Type names: stories use names outside D-12's ("a Feature with linked Stories", "team A История").
- Actors:
  - sign-in roles ("a user signed in with only the 'viewer' role");
  - project roles ("the 'member' role on a project");
  - personas the model lacks ("product owner", "manager");
  - doc-007 US-1 has "an organization administrator creates a new organization".
- Verbs: one act has four ("archives", "deactivated", "removed", «Удалить»).
- Glossary: «Корзина» is defined though no such screen exists, «Порядок» is unused, and the module glossaries are not in doc-001.
Resolution:
- doc-002 § Fields is the single field catalogue, with names equal to the UI labels. doc-007 cites it. doc-005 US-1 is rebased on doc-002: «Тип», «Теги», «ID».
- Stories use «Эпик»/«Фича»/«Пользовательская история»/«Задача»/«Баг» and «Эпики»/«Фичи»/«Истории»/«Задачи».
- doc-001 § Roles gets a wording rule, "a user whose role in the item's organization is X", applied by a mechanical sweep. The personas become "a member" or "an organization member". doc-007 US-1 → "a user permitted to create organizations".
- doc-001's glossary:
  - absorbs doc-004 § Terms and doc-005 § Glossary;
  - adds the keys (N-3) and the category labels (N-4);
  - adds «Удалить»/«Восстановить»/«Удалить навсегда» (work item), «Деактивировать»/«Восстановить» (organization, project) and «Удалить итерацию»;
  - marks «Корзина» "term only, never a UI label";
  - drops «Порядок».
- doc-001 US-1: "archives" → "deactivates".
Decision: default.

### N-30 M4 records are created by events that happen before M4 exists
Severity: minor
Sources: A-17
Affects: doc-003 Operation completeness Board/BacklogEntry, BR-6, BR-13, BR-16; doc-005 dashboard seeding (:229); queue mod-006
Problem: doc-003 seeds a team's boards "when the team itself is created (M1, doc-006)" and creates a BACKLOG_ENTRY when an item "becomes visible". But mod-002 and mod-003 create teams and items before mod-006 ships, and M1 cannot call M4. Three things are undefined:
- what triggers seeding for teams created after M4 ships;
- how teams that predate M4 get their boards;
- the initial rank of items that predate M4.

doc-005 answers the same question for dashboards ("for new teams and for teams existing when it ships"). D-6 raises it too (see N-2).
Resolution: doc-003 states that M4 guarantees one board and one backlog per (team, level) for every team, including teams that existed before M4 shipped. It reacts to team or item creation, or creates on first read; the choice is design's.
- When M4 ships, every existing item gets an entry, ordered by identifier number (creation order).
- Deleted items get hidden entries (BR-16).
- mod-006's line says so.
Decision: default.

### N-31 Release-2/3 settings and plans have no actor or owning scope
Severity: minor
Sources: A-18, B-11
Affects: doc-004 BR-15, BR-17, US-7, US-10, logical data model (DeliveryPlan), Operation completeness; doc-003 § Roles (L28); doc-001 § Roles
Problem: MVP team settings belong to the org admin only (doc-004 BR-14). Release-2/3 settings have no actor or a different one:
- In doc-004 US-7, a member sets capacity together with team days off and working days.
- Board column, swimlane and WIP administration and delivery-plan editing name no actor.
- DeliveryPlan spans the projects of one organization (US-10), yet its owning scope and permissions are unstated. The persona is "a manager".
Resolution:
- doc-001 § Roles: "every team-level setting (iterations, working days, board configuration, default area) is the organization administrator's; per-iteration values a team enters about itself (capacity, days off) are a member's". doc-004 US-7 is split accordingly, and doc-003 L28 names the org admin.
- DeliveryPlan is organization-owned, with an Operation completeness row: member and admin write, every role reads.
- The reports differ on who edits a plan: B-11 says the org admin, A-18 says member and admin. A-18 is picked:
  - a plan is a shared view, like a shared query or dashboard (member/admin write, doc-005 BR-3), not a team setting;
  - doc-004 names no actor (checked), so nothing approved is reversed.
Decision: default.

### N-32 Business-rule and decision ids collide across specs
Severity: minor
Sources: D-19
Affects: every BR table (doc-001 BR-1..13, doc-002 BR-1..17, doc-003 BR-1..19, doc-004 BR-1..17, doc-005 BR-1..12, doc-006 BR-M1-*, doc-007 BR-M2-*); decision ids doc-005 D-13..D-16, doc-010 D-1..D-3
Problem: doc-001..005 share one unprefixed BR-n namespace.
- "BR-3" means five different rules in five specs.
- doc-001 US-8 cites the stale-move rule as BR-11, while doc-003 US-2 cites the same rule as BR-12 — which is stack rank in doc-001.
- doc-005 continues the umbrella's D-series, and doc-010's D-1..D-3 repeat doc-001's ids.
Resolution:
- Unprefixed BR-n is reserved for doc-001. Module rules become BR-M<k>-n with n kept: doc-002 → M3, doc-003 → M4, doc-004 → M5, doc-005 → M6.
- Deleted rules leave gaps. Cross-module citations use the prefixed form. Decision ids are cited with their task id outside their task.
- Pure duplicates of doc-001 rules are deleted in favour of doc-001:
  - doc-002 BR-6/BR-7;
  - doc-003 BR-7 and BR-12;
  - doc-004 BR-14;
  - doc-006 BR-M1-7's first sentence;
  - doc-007 BR-M2-3.
- Merged into doc-001, then deleted:
  - doc-002 BR-10 → BR-4;
  - doc-004 BR-8 → BR-9;
  - doc-002 BR-15 and doc-003 BR-19 → the BR-11 table;
  - doc-002 BR-14 → BR-10;
  - doc-005 BR-9 → BR-6.
- doc-007 BR-M2-2 keeps only "states are ordered; the first is the initial state".
- Exception: doc-003 BR-13 is rewritten, not deleted (N-2).
- Applied last in the amendment pass, so every citation is rewritten once. If the diff is judged too wide, the fallback is to keep the ids and qualify every cross-spec citation with its task id.
Decision: default.

### N-33 The queue has no stated conventions for deps, prefixes, sizes and epics
Severity: minor
Sources: C-15, C-16, C-17, C-19
Affects: queue mod-003, mod-004, mod-005, mod-006, mod-008, mod-010, mod-011, mod-012, mod-013, feat-003, feat-008, feat-011; every line's `epic:`; `docs/backlog.md` § Line format, § Next; routing.md type table; `docs/features/README.md`; doc-004 row :236
Problem: the queue follows no stated conventions.
- Deps:
  - mod-011's deps do not follow its story's. US-5 depends on US-11 (feat-009) and expands to child Задачи (feat-004), but the line has "mod-007,mod-006,feat-001".
  - Redundant deps blur the critical path (mod-003's mod-001, feat-008's mod-006, mod-008's mod-003/mod-005).
- Prefixes and sizes: `mod-` means "creating a whole module" (routing.md:15), yet mod-004/005/010/011/012/013 found none. The L grounds of mod-010 and mod-011 are unstated.
- Epics:
  - The epic keys do not match the module names.
  - `product/analysis` is not in the documented set.
- Other metadata: MVP lines carry no release prefix, and § Next is empty.
Resolution:
- Deps rule: a line's deps are the minimal set of code lines plus the spec task(s) it is specified by.
  - mod-011 → feat-004, feat-009 and the estimation line (N-24); doc-004 row :236 matches.
  - Drop mod-003's mod-001, feat-008's mod-006, and mod-008's mod-003/mod-005.
  - Keep mod-006's mod-003 only under Q-15 (b).
- Sizes and ids: keep them all; renaming numbered ids breaks references across seven specs. Record the L reason in mod-010's and mod-011's notes. `docs/features/README.md` states that `mod-` also marks an L that founds a sub-slice or a pattern.
- Epics: `core/<slug of the module's analysis task>` (org-access, process-customization, work-items, backlogs-boards, iterations-planning, queries-dashboards). A line takes the epic of the module whose spec owns its stories. Add `product/analysis` to the documented set.
- Prefix MVP lines with "MVP.", and move mod-001 to § Next when doc-010 merges.
Decision: default. The alternative for epics is to keep the short keys and add a key → module table.

### N-34 mod-006 waits for mod-005 only for two deleted-item rules
Severity: minor
Sources: C-18
Affects: queue mod-006, mod-005; doc-003 BR-16, BR-17, decomposition row (:151)
Problem: the path to the first usable product is doc-010 → mod-001 → mod-002 → mod-003 → mod-005 → mod-006. That is five code L tasks in series, each with three gates. mod-006 depends on mod-005 only for BR-16/BR-17: hiding deleted items and restoring their backlog position (doc-003 R-23).
Resolution: per Q-15. The recommendation is to keep the dep.
Decision: human — Q-15.

## Decisions for the human

The recommended option comes first under each question. "Consequence" names the specs and queue lines each option changes.

### State names and Russian names

**Q-1 (N-1) Which state label set wins for Эпик, Фича, Пользовательская история and Баг?** Задача takes W-2's «Новая»/«Активна»/«Закрыта» under every option. D-1 recommends (a); A-1 poses the question without a recommendation.
- (a) **Recommended.** doc-007's P-1/P-2.
  - Labels: Эпик/Фича/История «Новая»/«Активна»/«Решена»/«Закрыта»; Баг «Новый»/«В работе»/«Исправлен»/«Закрыт».
  - Why: no label maps to two categories; it is the owning module's decision; the bug flow reads idiomatically.
  - Consequence:
    - doc-002: the § Types table becomes a reference, the Errata go, and the US-1/US-3/US-10/US-11 examples change.
    - doc-001: the US-3/US-4/US-6/US-10 examples.
    - doc-007: the Задача row and US-3.
    - W-7, and W-9's spelling, are logged as superseded.
    - The list filter, doc-005's Статус clause and the boards work per (type, state).
- (b) doc-002's uniform set (W-7/W-9): «Новая»/«Активна»/«Разрешена»/«Закрыта» for all four types.
  - «Разрешена» is InProgress on История but Resolved on Баг: two cards with the same label sit in two columns, and "Статус = Разрешена" mixes categories.
  - Consequence: doc-007's seed rows, US-3 and US-5; P-1/P-2 logged as superseded; the doc-001 examples.
- (c) Not recommended: doc-007 verbatim, including Задача «Выполняется»/«Готово». It overrides the explicit W-2 with no human decision behind it.
  - Consequence: doc-002's Задача row and Errata are reversed, and the InProgress category label cannot be «Выполняется» (Q-2).

**Q-2 (N-4) What are the Russian labels of the five state categories?** They are used for board column headers, the process screen and the glossary. The reports differ.
- (a) **Recommended — D-11.** «Предложено» / «Выполняется» / «На проверке» / «Завершено» / «Удалено».
  - Why: no header repeats a state of another category. «Решено» is avoided because История «Решена» sits in the InProgress column under Q-1 (a).
  - Consequence: doc-007 gains the category table; doc-003 BR-2 and the doc-001 glossary cite it.
- (b) A-12. «Новые» / «В работе» / «Решённые» / «Завершённые» / «Удалённые».
  - It reads naturally as a group of items, but «Решённые» would head the Resolved column while История «Решена» appears under InProgress.
  - Consequence: the same files, with other strings.
- (c) Headers taken from each level's main type, as the reference product does. Headers then differ per board, and the Resolved column on the Истории board has no История state to borrow.
  - Consequence: doc-003 BR-2 is rewritten per level; doc-007 needs category labels only for the process screen.

### Teams and areas

**Q-3 (N-2) What is the team lifecycle in MVP, and who owns the root area?** All four reports recommend (a).
- (a) **Recommended.** Teams are created, renamed and given members; deletion and its cascade move to release 2. The root area stays owned by the project's first team for good (H-9 unchanged).
  - Why: it matches doc-006 § Release 2 and doc-005 BR-10, and it keeps the cascade over areas, boards, entries, iteration settings and bound queries off the critical path. The cost: a team created by mistake can only be renamed or emptied.
  - Consequence:
    - doc-006: BR-M1-9, BR-M1-11, the Team row, and a "no deletion" scenario in US-6.
    - doc-001: Operation completeness, Team Delete "release 2".
    - Queue: mod-002 reads "teams: create, rename, membership".
- (b) As (a), but the root's owner can be reassigned to another team of the project, never cleared. This amends H-9.
  - Consequence: doc-006 BR-M1-11 plus one US-7 scenario, H-9 logged as superseded, and doc-003 BR-6 re-scoping applies to root-area items.
- (c) Deletion in MVP, as a soft delete:
  - refused while the team owns the root;
  - its other areas are re-owned to the first team;
  - its boards and entries are hidden;
  - its iteration settings are dropped;
  - its bound queries stop.
  - Consequence:
    - doc-006: a delete operation, code and story.
    - doc-003: deleted-team rules.
    - doc-004: team settings.
    - doc-005 BR-10: moves into MVP.
    - Queue: mod-002, mod-006 and mod-007 grow.

**Q-4 (N-2) Should MVP have a per-team default area?** A-2 recommends (b); the consolidation recommends (a).
- (a) **Recommended.** No. doc-006 BR-M1-14 and R-19 stand: a new item defaults to the root, which the first team owns, and a member of another team sets Область on the form. Design may pre-fill the creator's last-used area.
  - Why: MVP has no create action on a team's board or backlog (doc-003 defines none; items are created from M3's project-wide form). A per-team default would have no team context to apply to, and doc-006 R-19 deliberately placed it in release 2.
  - Consequence: one scenario each in doc-006 US-7 and doc-003 US-2 makes the landing explicit. No scope change.
- (b) Yes (A-2). Each team has a default area among its own areas, set by the org admin; the first team's default is the root. An item gets the default area of the team it is created from.
  - It needs a team context at create time: an "add item" on the backlog or board (new doc-003 scope), or a team selector on the form.
  - Consequence: doc-006 TEAM.defaultArea, BR-M1-14, US-6/US-7; doc-003 gets a new create rule and story; doc-002's Область default; mod-002 and mod-006 grow.

### Identifiers

**Q-5 (N-3) How does a work-item address name the organization?** The keys themselves are W-10/W-11 and are not in question.
- (a) **Recommended — A-3.** The address carries the organization's internal id, and the identifier is resolved inside that organization.
  - Why: no new attribute, and organization names stay renamable (doc-006 BR-M1-1).
  - Consequence: doc-002 US-4 wording only.
- (b) A readable organization slug: ORGANIZATION.key (Latin, unique across the instance, immutable, set at creation).
  - Consequence: doc-006 ER, BR-M1-1, US-2 scenarios and Operation completeness; doc-002 US-4; mod-002 grows.
- (c) Not recommended: project keys unique across the instance, so the bare identifier is global. This amends W-10, and a duplicate-key refusal would disclose another organization's project, against BR-M1-8.
  - Consequence: doc-002 W-10 and doc-006 BR-M1-1.

### Access

**Q-6 (N-6) Which channel reads the Keycloak user directory (member search, «неактивен»)?**
- (a) **Recommended — B-4.** Amend ADR-0023's guards to allow exactly one named production client with a service account. The account holds only realm-management view-users/query-users, and shipping code calls only the read-only user endpoints.
  - Why: it is the smallest change, and the guards stay a closed allowlist.
  - Consequence: doc-006 constraint 5; mod-002 owes the ADR-0023 amendment plus the contract-test and architecture-test changes; the queue line for mod-002 changes.
- (b) A Keycloak event-listener provider pushes user changes into a MUSTASK projection, and search runs on it. This needs a custom Keycloak artifact and a backfill of existing users.
  - Consequence: doc-006 constraint 5 and NFR Identity refresh; mod-002 likely outgrows L and may need a separate line.
- (c) Not recommended: no directory channel. Members are added by exact e-mail among users who have signed in once. This narrows D-10/US-3 and loses BR-M1-17 for users who never return.
  - Consequence: doc-006 US-3 and BR-M1-17.

**Q-7 (N-7) Who grants "create organizations"?**
- (a) **Recommended — B-5.** It is Keycloak realm administration, outside MUSTASK's role model.
  - Why: no MUSTASK role becomes a Keycloak administrator, and ADR-0018/0023 stay as they are on this point.
  - Consequence: doc-001 § Roles and BR-5; doc-006 BR-M1-2, the "Instance right" row, the US-1 persona and US-4. B-3's constraint 8 names the grantable role or group.
- (b) The instance-administrator realm role also carries realm-management rights.
  - Consequence: the ADR-0018 amendment composes non-`mustask-api` roles, `KeycloakRealmRolesTests` L93-130 change, and a MUSTASK role becomes a Keycloak administrator. doc-006 US-1 stays as written.

**Q-8 (N-8) Who owns the ownership predicate and D-14's personal-object code?** The reports differ.
- (a) **Recommended — B-2.** Split the work:
  - mod-002's ADR-0018 amendment states the predicate (the code is checked, then an optional ownership test on the resolved object), because MVP already needs it for own comments (feat-002) and own attachments (mod-004);
  - mod-008 registers the personal-object code in all three bundles and builds personal scope;
  - there is no separate M1 task.
  - Why: the predicate is MVP regardless of D-14. mod-002 carries one paragraph, not release-2 code, which answers C-7's concern about loading the critical path.
  - Consequence: doc-006 constraint 6; doc-005 § Roles and L227-228; the mod-002 and mod-008 lines.
- (b) C-7's recommendation: mod-008 writes the whole ownership amendment and builds the check. Until release 2, the MVP ownership checks are implemented line by line without a model statement.
  - Consequence: doc-005 L227 names mod-008; the mod-008 line changes; feat-002 and mod-004 carry ad-hoc checks, and a downgraded author's rights are decided per line.
- (c) Not recommended: mod-002 builds everything in MVP, including the personal-object code. That puts release-2 scope on the critical-path founder.
  - Consequence: the mod-002 line grows.

Under any option: should D-14 cover any object a spec declares personal (B-2's default), or only personal queries and «Мои запросы»?

### Lifecycle

**Q-9 (N-13) Purge beyond work items.** For MVP, (a) and (b) are identical: only work items are purged, by the org admin.
- (a) **Recommended — A-6, B-8, D-7.**
  - Purge is in-product wherever a spec defines one, and only the organization administrator performs it.
  - Organizations, projects and iterations have no purge; retention is indefinite.
  - M6 objects get an in-product purge in release 2/3, as doc-005 BR-9/D-13 already state. The purge list shows owner, folder and deletion date, never the name or clauses.
  - Why: it keeps the approved BR-9, whose "grants no read of its content" only makes sense for a product action.
  - Consequence: doc-001 BR-6 and US-1; doc-004 BR-10; doc-005 Operation completeness and BR-9; the mod-008 and mod-009 lines gain purge.
- (b) C-10's recommendation: work items only in the product; every other purge is an operator procedure outside the product.
  - Consequence: doc-001 BR-6; doc-004 BR-10; doc-005 BR-9 is reworded, and D-13's reading is logged as superseded. No queue change.
- (c) Not recommended: an in-product purge for every object, organizations and projects included. It reverses doc-006 BR-M1-1.
  - Consequence: doc-006 BR-M1-1 and § Out of scope; the mod-002, mod-007, mod-008 and mod-009 lines.

Concurrency (N-16) needs no decision. Every cell derives from existing text or from DD-2, and only the CSV update-row cell is an open default.

### Scope and queue

**Q-10 (N-21) Where do the recycle-bin filter and the backlog filter go?**
- (a) **Recommended — C-3.** Split by surface:
  - feat-003 becomes the list filters (incl. «Удалена», former members, keyword), depending on mod-005;
  - a new M line, `feat-NNN-board-backlog-filter` (deps mod-006, feat-003), takes doc-003 US-2's filtered-order scenario;
  - feat-008 depends on the new line.
  - Why: it removes the cycle, gives the backlog filter an owner, and makes restore usable right after mod-005.
  - Consequence: doc-002 row :238; doc-003's decomposition and US-2; doc-001 US-10; doc-004's feat-008 row. Queue: add one line, re-dep feat-003 and feat-008.
- (b) Keep one feat-003 behind mod-006, extend it to the backlog, and add a minimal "show deleted" toggle to mod-005.
  - Consequence: doc-002 rows :237/:238, US-10 and US-11; doc-003's decomposition. Queue: reword feat-003 and mod-005.

**Q-11 (N-24) Which estimation fields ship in release 2?** The reports differ.
- (a) **Recommended — C-5.** Exactly what doc-004 US-7..US-9 read: Story Points on Пользовательская история and Баг; Remaining Work and Activity on Задача.
  - Why: every field has a consuming story and acceptance, and adding a field later is additive.
  - Consequence: doc-002 gains a release-2 section and row. Queue: add `feat-NNN-work-item-estimation-fields` (M).
- (b) A-9's recommendation, the ADO Agile set: Story Points on История and Баг; Original Estimate, Remaining Work, Completed Work and Activity on Задача and Баг. It gives parity with the reference product, but Original and Completed Work have no consumer yet.
  - Consequence: the same files, with a larger row.
- (c) doc-002's whole release-2 list: Effort, Business Value, Risk, Value Area, Severity, dates.
  - Consequence: the row likely grows to L.

**Q-12 (N-23) Do release-2 saved queries match the MVP inline filter?** C-6 raises the question; A-15 and D-10 proposed the same direction as defaults.
- (a) **Recommended.** Release 2 covers everything the MVP filter can do:
  - "Under" on Область and Итерация, with subtree matching; "=" stays exact;
  - a keyword clause on Заголовок and the identifier;
  - mod-010 keeps Was Ever, @Today, Contains Words, In Group and the tree/linked query types.
  - Why: doc-001 promises that inline filters "graduate into personal saved queries" in release 2.
  - Consequence: doc-005 BR-5 and the US-1 tables; doc-001's cross-module rule. Queue: reword mod-008 (grows) and mod-010 (shrinks).
- (b) Keep the current cut, and state that saved queries cannot express the MVP filter until release 3.
  - Consequence: doc-001's "graduate" sentence is reworded, doc-005 US-1 gets a note, and mod-010 adds "Under" on Итерация.

**Q-13 (N-19) Is doc-001 amended in place, or superseded?**
- (a) **Recommended — C-11.** Amend doc-001 in place: its MVP and release tables, ER, ownership table, glossary and rules.
  - Why: every future analysis starts from the umbrella. The § Amendment map assumes this option.
  - Consequence: doc-001 becomes the largest amended file; doc-004 § Amendments and doc-002's errata point to it.
- (b) doc-010's product plan supersedes doc-001 § MVP boundary, the ER and the ownership table, and doc-001 gets a one-line pointer.
  - Consequence: the scope and ER rows of the doc-001 table move into doc-010's spec.md, and there are two sources of scope until the release-2 analysis.

**Q-14 (N-26) What rule decides which release-2/3 work is queued?**
- (a) **Recommended — C-12.** Keep the status quo, and add one analysis line `doc-NNN-release-2-scope`. It gives rows to the orphan release-2 items and settles the notification and audit double claims.
  - Consequence: add one docs line; the product plan lists every release-2/3 item with one owner.
- (b) Withdraw every release-2/3 implementation line until that scoping task is done.
  - Consequence: withdraw mod-008..mod-013 and feat-005/006/011/012 (numbers are never reused).
- (c) Queue everything, doc-007's release-3 rows included.
  - Consequence: add about eight doc-007 lines. The orphan items would need specs first, and this reverses doc-007's human choice.

**Q-15 (N-34) Should mod-006 keep depending on mod-005?**
- (a) **Recommended — C-18.** Keep the dependency.
  - Why: D-3 sets no dates, and decoupling saves one L of latency at the cost of an extra line and its gates.
  - Consequence: none.
- (b) Decouple: mod-006 depends on mod-003 (plus doc-003), and a new M line, `feat-NNN-backlog-deleted-items` (deps mod-005, mod-006), takes BR-16/BR-17.
  - Consequence: doc-003's decomposition changes. Queue: add one line, re-dep mod-006. Between mod-005 and the new line, backlogs show deleted items.

## Defaults

Applied unless the human objects at gate-1.

- N-5 — doc-006 § Roles gains constraints 6–8, plus a scope attribute on every code (constraint 1). doc-001 BR-5 reads "instance-level roles only" and answers "not found" to non-members in every module; doc-007 US-2 and doc-002's BPMN align.
- N-9 — B's permission-code catalog goes into the product plan. Rules: one prefix per module; a code guards the entity written; mod-005 registers delete/restore; mod-004 registers the Drive connection. Each founder's row lists its codes.
- N-10 — Team membership carries no access. Every member reads, and members and admins act on, every team's surfaces. Membership only sets the default team view and, from release 2, capacity.
- N-11 — BR-M1-11 stands, and doc-001 BR-3 says every area resolves to one owning team. Every unowned clause and scenario is deleted, and doc-003 US-2 gets the "re-owned from A to B" scenario.
- N-12 — doc-001 BR-6 becomes a per-entity lifecycle table. M6 separates its own deleted status from the one inherited from the project. mod-002 owes the soft-deactivation ADR, and mod-005's ADR references it.
- N-14 — One «Удалена» participation rule goes into doc-001. Drags and reorders of a deleted item are refused. Area deletion counts deleted items. Iteration removal clears their pointers and counts them.
- N-15 — doc-002 gains a "Logical data model (M3)" section. Tags are stored entities; created/changed are derived; the restore target is stated; USER edges are drawn.
- N-16 — doc-001 BR-11 becomes the operation → policy table. The notice goes to the second mover, with the default text. mod-003 owes the client-visible concurrency-token ADR, and mod-006 reuses it. CSV update rows are last write wins.
- N-17 — Audit is scoped to work items, with an explicit "not audited in MVP" list. A purge leaves "parent purged" on the surviving children. A comment edit gets a marker plus a history entry; the old text is not kept.
- N-18 — M3 gains an atomic "replace parent", used by the backlog mapping. The form keeps its refusal. The mapping offers only the level directly above; same-level links are made on the form.
- N-20 — The six lines that understate scope are reworded by hand after gate-1. The doc-006 and doc-007 rows carry W-10/W-11.
- N-22 — mod-004 depends on mod-005. mod-005 enforces BR-14 for the writes that exist when it ships, and each later line enforces it for its own.
- N-25 — CSV import/export is release 3, M6, and feat-005 is its only owner.
- N-27 — doc-007 models level rank and kind, and state order. An item is created in its type's first state, with Статус read-only at create.
- N-28 — Inactive accounts are not offered as assignees. The markings are «неактивен» and «не в организации»; the latter prevails.
- N-29 — doc-002 § Fields is the one field catalogue. Stories use D-12's names and actors scoped to the organization. The glossary absorbs the module glossaries and the new terms, «Корзина» becomes term-only, and «Порядок» is dropped.
- N-30 — M4 guarantees boards for every team, including pre-M4 teams, and backfills entries for pre-M4 items by identifier number.
- N-31 — Team-level settings belong to the org admin; per-iteration team values belong to a member. DeliveryPlan is organization-owned: members and admins write, every role reads.
- N-32 — Module rule ids get BR-M<k>- prefixes, and duplicates move into doc-001. This runs last in the pass; the fallback is to qualify cross-spec citations only.
- N-33 — Deps are the minimal code lines plus the specifying spec task. Sizes and ids are kept, and L reasons are noted. Epics become `core/<module slug>`, `product/analysis` is documented, and MVP lines get the "MVP." prefix.

Default parts of the entries marked human:

- N-1 — Задача takes W-2's «Новая»/«Активна»/«Закрыта»/«Удалена». doc-007 holds the only state table, and doc-002 cites it.
- N-2 — The carry-back body: a create-team operation, team names unique in the project, US-6 amended, doc-001 and doc-003 aligned, and doc-003 BR-13 rewritten.
- N-3 — The W-10 project key and the W-11 type keys are written into doc-006, doc-007 and doc-001, and «ID» into doc-005.
- N-4 — doc-007 owns a category-label table, and Removed is never a column.
- N-8 — doc-001's predicate text and the downgraded-author rule. doc-005's references are re-pointed to doc-010.
- N-13 — "Administrator" means the organization administrator. MVP purges work items only; organizations, projects and iterations have no purge.
- N-19 — The content of the doc-001 consolidation: MVP rows, ER, ownership table, US-9.
- N-23 — The derivation moves to doc-001 BR-9. "=" is exact and "Under" matches the subtree. A team-bound @CurrentIteration does not narrow results to the team. The default iteration is never assigned to new items.
- N-24 — The estimation row lives in doc-002 as an M line on mod-003, and Activity is a fixed M3 list.

## Amendment map

This is the work list for the amendment pass. It assumes Q-13 (a), amending doc-001 in place. Rows marked "per Q-n" wait for that answer. N-32's renumbering runs last.

### doc-001-domain-map

| Section / rule / story | Change | N |
|---|---|---|
| § Roles (L28-44) | Ownership-predicate paragraph: role-narrowed codes, the personal-object code in every bundle, no reading another user's private objects, the purge exception | N-8 |
| § Roles, Roles table | Admin "purge deleted work items"; member "delete and restore a work item"; viewer "read codes plus the personal-object code" | N-8 |
| § Roles (L35-38) | Instance administrator = instance-level codes only; granting "create organizations" per Q-7 | N-7 |
| § Roles | Actor wording rule; team-level settings belong to the org admin, per-iteration team values to a member | N-29, N-31 |
| § MVP boundary, MVP table | M1: "one or more teams (first created with the project)", project key; M3/M4/M5 rows add the planning pane, move of unfinished work, iteration tree, type-specific fields, per-org Drive, read-only process screen; superseding decisions listed with sources | N-2, N-3, N-19 |
| § Release 2/3 plan | Taskboard is one item, owned by M5, on M4's BOARD; CSV in release 3; "graduate" sentence per Q-12; orphan items per Q-14 | N-19, N-25, N-23, N-26 |
| Cross-module ER | ITERATION per doc-004; BOARD_COLUMN → category; PROJECT–PROCESS derived; PROJECT.key; USER edges for author, uploader and revision author; PROJECT–TEAM one-or-more | N-19, N-3, N-15, N-2 |
| Ownership table | Add QueryFolder, Dashboard team/project levels, the Drive connection (M3, hosted in M1 settings), release-3 FieldDefinition/FormLayout/TransitionRule, release-2/3 capacity/snapshot/plan entities and team membership, each with its release | N-19 |
| Glossary | Absorb doc-004 § Terms and doc-005 § Glossary; add «Идентификатор», «Ключ проекта», the category labels, the verbs and the assignee markings; «Корзина» term-only; drop «Порядок» | N-3, N-4, N-29, N-28 |
| BR-3 | Add "every area resolves to exactly one owning team" | N-11 |
| BR-4, NFR Audit/retention | Scoped to work items; doc-002 BR-10's additions merged; "not audited in MVP" list; card move = state revision; child entry on purge; comment edit gets a marker, old text not kept | N-17 |
| BR-5 | Ownership predicate and the membership condition; "not found" for non-members in every module; "instance-level roles only (named by the ADR-0018 amendment)" | N-8, N-5 |
| BR-6 | Principle plus per-entity lifecycle table; purge per Q-9 | N-12, N-13 |
| New rule next to BR-6 | «Удалена» participation: visibility, references, writes, re-scope on restore | N-14 |
| New rule next to BR-7 | The inline filter and "Under" match the subtree; "=" is exact | N-23 |
| BR-9 | Full current-iteration derivation moved in from doc-004 BR-8; the default iteration only decides the current iteration | N-23 |
| BR-10 | Add "no operation other than Restore changes the state of an item in «Удалена»" (absorbs doc-002 BR-14 and doc-007 BR-M2-3) | N-14, N-32 |
| BR-11 | Replaced by the operation → policy table | N-16 |
| Operation completeness | Team: create/update actors, delete per Q-3; Comment: edited marker, old text not kept; purge cells per Q-9; removal traces for Area leaf, attachment, tag | N-2, N-17, N-13, N-12 |
| ADR list (:366-372) | Soft-deactivation ADR → mod-002; concurrency-token ADR → mod-003; ADR-0023 change → mod-002 (Q-6); predicate per Q-8; history ADR scope anticipates BR-M1-15, Was Ever and metrics | N-12, N-16, N-6, N-8, N-17 |
| US-1 | "archives" → "deactivates"; drop "until an administrator purges it"; "its first team" | N-29, N-13, N-2 |
| US-2 | "first team" / "a team's membership"; delete the "no owner up to the root" clause | N-2, N-11 |
| US-3, US-4, US-6, US-10 | State names per Q-1 | N-1 |
| US-4 and every story that names a sign-in or project role | Actor wording sweep | N-29 |
| US-5 | Comment edit: "edited" marker with the edit time, plus a history entry | N-17 |
| US-8 | Delete sc.4 (no owning team); rewrite sc.3 (notice to the second mover) | N-11, N-16 |
| US-9 | Iteration tree; "delivered by" feat-008/mod-007; sc.3-6 get the team-selection Given and subtree wording | N-19, N-23 |
| US-10 | "Delivered by" per Q-10 | N-21 |

### doc-002-work-items

| Section / rule / story | Change | N |
|---|---|---|
| § Roles (L29-33) | Code registration per the catalog: delete/restore → mod-005; Drive → mod-004 | N-9 |
| BPMN (L102-103) | Permission checked "in this project's organization"; a non-member gets "not found"; «Недостаточно прав» only for members | N-5 |
| § Types, levels and states | Table replaced by a reference to doc-007, keeping the Resolved-on-Баг consequence; Errata deleted | N-1 |
| § Fields | Becomes the single field catalogue; names = UI labels | N-29 |
| § Fields, Статус | Created in the type's first state and read-only on create; a CSV row naming another state is rejected | N-27 |
| § Fields, Исполнитель | Inactive accounts not offered; «неактивен» / «не в организации» (the latter prevails) | N-28 |
| New § Logical data model (M3) | ER and entity table: WorkItem, Comment, Attachment, Tag (stored), HistoryRevision, WorkItemLink, Drive connection; USER edges | N-15 |
| New § Release 2: estimation fields | Fields per Q-11; Activity = fixed M3 list with Russian labels | N-24 |
| BR-6, BR-7 | Deleted as duplicates of doc-001 BR-10 | N-32 |
| BR-8 | Named atomic "replace parent" operation; the form keeps its refusal | N-18 |
| BR-9 | A child whose parent is deleted may drop the link from its side (recorded in both histories) | N-14 |
| BR-10 | Merged into doc-001 BR-4 | N-17, N-32 |
| BR-12, BR-13 | "Author (member or admin role)" / "uploader (member or admin role)"; comment edit records the change, not the old text | N-8, N-17 |
| BR-14 | "Read-only for users; system clean-up still applies and is recorded"; then merged into doc-001 BR-10 | N-14, N-32 |
| BR-15 | Cites the doc-001 BR-11 table (card move exception) | N-16 |
| NFR Permissions | Controls filtered by the codes from constraint 7 | N-5 |
| Operation completeness | Comment/Attachment actors with the predicate; WorkItemLink "replace parent"; Drive connection code | N-8, N-18, N-9 |
| § Out of scope (:199-209) | CSV → release 3, feat-005; estimation fields → the new section; notifications and audit per Q-14 | N-25, N-24, N-26 |
| § Decomposition | Row :238 per Q-10; new estimation row; BR-14 enforcement rule, repeated in rows feat-001/feat-002/mod-004/feat-007; "Two keys are required of upstream" → "carried by doc-006/doc-007"; codes registered per row | N-21, N-24, N-22, N-3, N-9 |
| US-1, US-3, US-10, US-11 | State names per Q-1 | N-1 |
| US-2 | Inactive-assignee scenario | N-28 |
| US-4 | The address names the organization per Q-5 | N-3 |
| US-5 | The refusal stays; a note that the backlog mapping replaces the parent (doc-003 BR-10) | N-18 |
| US-8 | A tag used only by deleted items is not suggested; canonical spelling per the model | N-14, N-15 |
| US-10 | A purge records "parent purged: <identifier>" on the surviving children | N-17 |
| US-11 | Former-member label; «Удалена» filter delivered per Q-10 | N-28, N-21 |
| US-1..US-12 | Actor wording sweep | N-29 |
| All BR | Renumbered BR-M3-n | N-32 |

### doc-003-backlogs-boards

| Section / rule / story | Change | N |
|---|---|---|
| § Roles (L24-28) | Organization-wide read and act on every team's board and backlog; board configuration (release 2) belongs to the org admin | N-10, N-31 |
| BR-2 | Column headers = doc-007 category labels (Q-2) | N-4 |
| BR-5 | Delete the "no owning team" clause | N-11 |
| BR-7, BR-12 | Deleted as duplicates of doc-001 BR-12/BR-11; the notice text moves to NFR Refresh model | N-32, N-16 |
| BR-10 | Cites M3 "replace parent"; the one-level-up offer is stated as M4's own narrowing; a child of a deleted parent is treated as parentless | N-18, N-14 |
| BR-13 | Rewritten: several teams in MVP; every team, including later ones, gets boards and backlogs; cross-team behaviour tested with two teams | N-2 |
| BR-16, BR-17 | A retained entry is re-scoped on Restore if its Area was re-owned meanwhile | N-14 |
| BR-19 | No-check applies to live items only; moves and reorders of deleted or purged items are refused with the notice; cites doc-001 BR-11 | N-14, N-16 |
| NFR Refresh model | The notice goes to the second mover; text «Порядок изменён другим участником — список обновлён» | N-16 |
| § Data model BACKLOG_ENTRY | Delete "an Area becoming unowned removes the entry" | N-11 |
| Operation completeness Board / BacklogEntry | Boards for every team, including pre-M4 ones; entries for pre-M4 items ordered by identifier number; hidden entries for deleted items; Board delete "team deletion per doc-006 BR-M1-9" | N-30, N-2 |
| § Out of scope (:129, :130-134, :135-141) | CSV → release 3, feat-005; the iteration sentence is replaced; live push, the level toggle and grouping rows per Q-14 | N-25, N-23, N-26 |
| Decomposition row :151 (mod-006) | Several teams plus the backfill; filters per Q-10; the mod-005 dep per Q-15; the codes it registers | N-2, N-30, N-21, N-34, N-9 |
| Decomposition row :152 (feat-004) | Uses M3 "replace parent" and `workitems.link.write` | N-18, N-9 |
| US-1 | Drag of a deleted item is refused; concurrent drag per the notice rule; actor wording | N-14, N-16, N-29 |
| US-2 | Unowned scenarios → "re-owned from team A to team B"; reorder of a deleted item is refused; concurrent-reorder notice; filtered-order scenario per Q-10 | N-11, N-2, N-14, N-16, N-21 |
| US-3 | Russian type names; mapping one level up, same-level links on the form; cross-team parent scenario; drop the "no second team" Givens | N-29, N-18, N-2 |
| All BR | Renumbered BR-M4-n | N-32 |

### doc-004-iterations-planning

| Section / rule / story | Change | N |
|---|---|---|
| § Terms | Moved into the doc-001 glossary; a pointer remains | N-29 |
| § Amendments | Add doc-001 US-9 sc.3-6; point to doc-001 once it is amended | N-23, N-19 |
| BR-6 | Add: the default iteration is never assigned to new items; it only decides «Текущая итерация» | N-23 |
| BR-7 | Delete the "no owning team" refusal and its message | N-11 |
| BR-8 | Keeps "derived on read; team surfaces and team-bound queries"; the derivation moves to doc-001 BR-9 | N-23, N-32 |
| BR-9 | Cites doc-001's subtree rule | N-23 |
| BR-10 | Deleted items' pointers are cleared and counted; "no reactivation and no purge in MVP" | N-14, N-13, N-12 |
| BR-14 | Deleted (duplicate of § Roles); the team-settings code is `iterations.team-settings.write` | N-32, N-9 |
| BR-15, US-7 | Working days, days off and settings belong to the org admin; per-iteration capacity belongs to a member | N-31 |
| BR-16 | Reads the estimation fields from doc-002's release-2 row | N-24 |
| BPMN node H | Delete the no-owner branch | N-11 |
| Logical data model, Operation completeness | DeliveryPlan is organization-owned (members and admins write, every role reads); Iteration has no purge | N-31, N-13 |
| NFR Audit | Iteration administration is unaudited, as listed in doc-001 | N-17 |
| Decomposition rows :236, :238 | Deps on the estimation line; :236 deps feat-004 and feat-009; the L reason for mod-011 | N-24, N-33 |
| US-2 | Default-iteration scenario | N-23 |
| US-3 | Delete the "no owning team" scenario | N-11 |
| US-5, US-12 | Russian type and level names | N-29 |
| US-9, US-10 | Personas → "a member" / "an organization member"; the plan stays within one organization | N-29, N-31 |
| US-11 | Cites the concurrency table | N-16 |
| US-13 | Delete "items with no owning team" | N-11 |
| US-14 | The confirmation counts deleted items | N-14 |
| All BR | Renumbered BR-M5-n | N-32 |

### doc-005-queries-dashboards

| Section / rule / story | Change | N |
|---|---|---|
| § Roles (L21-33) | D-14 goes through doc-001's personal-object code; drop "an ADR-0018 amendment"; name doc-010 and the code owner (Q-8) | N-8, N-5 |
| § Glossary | Moved into the doc-001 glossary; a pointer remains | N-29 |
| BR-2 | "Whatever the owner's role, while they are a member of the organization" | N-8 |
| BR-4 | «Удалена» items appear only through an explicit clause; a team-bound @CurrentIteration does not narrow results to the team's areas | N-14, N-23 |
| BR-5, US-1 query tables | "Under" on Область/Итерация and the keyword clause per Q-12 | N-23 |
| BR-6 | Cites doc-001 BR-9 (the full derivation) | N-23 |
| BR-8, US-4 | Rows matched by the full identifier within the target project; a row targeting a deleted item is rejected; update rows are last write wins; a new-item row with a non-initial state is rejected | N-3, N-14, N-16, N-27 |
| BR-9 | Purge per Q-9 (org admin; the purge list shows no name or clauses); then merged into doc-001 BR-6 | N-13, N-32 |
| BR-10, data-model note | An object's own deleted status is separate from the status inherited from its project; the team-deletion clause per Q-3 | N-12, N-2 |
| BR-12 | Default columns add «ID» | N-3, N-29 |
| US-1 | Field set rebased on doc-002 («Тип», «Теги», «ID») | N-29 |
| US-2 | "The one write a viewer may perform in release 2" | N-8 |
| US-5 | Persona → "an organization member" | N-29 |
| Operation completeness | Purge cells per Q-9 | N-13 |
| Decomposition (L227-228, rows mod-008/009/010, feat-005) | "Absorbed by doc-010"; owner per Q-8; purge per Q-9; "Under" and keyword per Q-12; minimal deps for mod-008; feat-005 is the only CSV owner | N-8, N-13, N-23, N-33, N-25 |
| All BR, D-13..D-16 | Renumbered BR-M6-n; decisions cited as "doc-005 D-n" | N-32 |

### doc-006-org-access

| Section / rule / story | Change | N |
|---|---|---|
| § Roles table (L65-79) | `organizations.team.write` = create, rename, membership (delete per Q-3); every code carries its scope; the Drive connection is hosted on the organization settings | N-2, N-5, N-9 |
| § Roles (L81-84) | Team membership carries no access; outside M1 the viewer bundle is not read-only | N-10, N-8 |
| § Roles constraints | 1 adds the scope attribute; 5 adds the ADR-0023 change per Q-6; new 6 (resource-based check with the predicate), 7 (codes from the API), 8 (realm role inventory) | N-5, N-6, N-8 |
| ER | PROJECT.key; TEAM name unique in the project; PROJECT–TEAM one-or-more; TEAM.defaultArea only under Q-4 (b) | N-3, N-2 |
| BR-M1-1 | Project key: format, unique in the organization incl. deactivated projects, immutable, survives a rename | N-3 |
| BR-M1-2 | Granting "create organizations" per Q-7 | N-7 |
| BR-M1-7 | First sentence moves to doc-001 BR-5; the resolution chain stays and is extended in constraint 6 | N-32, N-5 |
| BR-M1-9 | Rewritten: the first team comes with the project, further teams can be added any time, the name rule, deletion per Q-3 | N-2 |
| BR-M1-11 | The root is owned by the first team (Q-3); drop "ownership decides nothing there yet" | N-2, N-11 |
| BR-M1-13 | "Work items in it" includes «Удалена» items | N-14 |
| BR-M1-14 | Per Q-4 | N-2 |
| BR-M1-17 | Points to the doc-002 Исполнитель rule | N-28 |
| Lifecycle (:132-140) | Soft deactivation is designed and recorded in an ADR by mod-002 | N-12 |
| Operation completeness | Team: create by the org admin, delete per Q-3; Project update: "rename; the key never"; "Instance right" per Q-7 | N-2, N-3, N-7 |
| § MVP boundary, § Release 2, Decomposition (:327) | "The project with its teams"; project key; directory channel and the ADR-0023 change; soft-deactivation ADR; ADR-0018 constraints 1–8 | N-2, N-3, N-6, N-12, N-5, N-20 |
| US-1 | Persona per Q-7 | N-7 |
| US-2, US-5 | "Deactivates" wording | N-29 |
| US-3 | Member search per Q-6 | N-6 |
| US-4 (L74-76) | "Lets them do only one thing — create an organization" | N-7 |
| US-5 | Four key scenarios; "its first team" | N-3, N-2 |
| US-6 | Retitled "manages the project's teams"; adds a second team; name unique in the project; deletion per Q-3; so-that clause | N-2, N-10 |
| US-7 | Owner picker offers every team; "re-own from A to B" scenario; a leaf holding deleted items cannot be deleted | N-2, N-14 |

### doc-007-process-customization

| Section / rule / story | Change | N |
|---|---|---|
| § MVP: the seeded process | State table per Q-1 (Задача per W-2); «Ключ» column; category table with Russian labels (Q-2); the field layout cites doc-002 § Fields | N-1, N-3, N-4, N-29 |
| Logical model | BACKLOG_LEVEL {name, rank, kind}; STATE_DEFINITION {name, category, order}; WORK_ITEM_TYPE {name, key, icon, colour}; FIELD_DEFINITION note on the release-2 estimation fields | N-27, N-3, N-24 |
| BR-M2-2 | "States are ordered; the first is the initial state" | N-27, N-32 |
| BR-M2-3 | Moves to doc-001 BR-10 | N-32 |
| BR-M2-4 | Levels are totally ordered by rank; consumers named | N-27 |
| BR-M2-6 (release 3) | A state cannot be removed while an item is in it or remembers it as its restore target | N-14 |
| BR-M2-9 | The type key is part of the stable identity | N-3 |
| § Roles, Operation completeness | `process.definition.read` is registered by mod-001 | N-9 |
| Decomposition row :269 (mod-001) | Type keys; read-only process screen; category labels | N-3, N-20, N-4 |
| US-1 | Actor → "a user permitted to create organizations"; keys shown | N-29, N-3 |
| US-2 | Keys; Russian category labels; a non-member gets «Организация не найдена» | N-3, N-4, N-5 |
| US-3, US-5 | State names per Q-1 | N-1 |

Outside the specs, on this branch: `docs/features/README.md` gets a note on the `mod-` prefix (N-33). The widened scope of doc-010 D-2 (N-2, N-8) goes into doc-010's status.md; that file is the orchestrator's to edit. ADR texts are not touched here: they are owed by the queue lines named in N-5, N-6, N-8, N-12 and N-16.

## Queue operations

All of these are queue-only commits in main. `queue.sh` cannot edit an existing line's note, so rewording is done by hand (precedent: 486295a, 6d70708). Nothing is resized or renamed: ids and sizes are kept (N-33), and every new line is M.

Already on main — verify only:
1. doc-008 and doc-009 are withdrawn, and mod-002/mod-008 depend on doc-010 (34ee78d, 8164120, c7e2f5b).
2. mod-001 depends on doc-007 and doc-010 (630e0d0). This closes C-2 (N-1).

After gate-1 — defaults, no decision needed:

3. **Re-dep** mod-004 → `deps:mod-005`. mod-003 is implied (N-22).
4. **Re-dep** to prune redundant deps: mod-003 drops mod-001; mod-008 drops mod-003 and mod-005 (N-33).
5. **Reword** these lines (N-20):
   - mod-001: read-only process screen, W-11 keys, category labels, `process.definition.read`.
   - mod-002: project key; ADR-0018 constraints 1–8; soft-deactivation ADR; last-admin and inactive rules.
   - mod-003: list surface, identifier format, data model, history ADR scope, concurrency-token ADR.
   - mod-004: per-organization Drive, US-13, external counterparty, secrets, `workitems.attachment-storage.manage`.
   - mod-005: purge; a recycle-bin ADR that references mod-002's; BR-14 enforcement.
   - mod-006: every team, including the backfill of pre-existing teams and items; reuses the concurrency ADR.

   Related entries: N-3, N-5, N-12, N-16, N-22, N-30.
6. **Notes**: the L reasons for mod-010 and mod-011 (N-33).
7. **Re-epic** every line to `core/<module slug>`, document `product/analysis` in backlog.md's epic list, and prefix MVP lines with "MVP." (N-33).

After the human's answers — each step waits for the named question:

8. Q-3, Q-4, Q-6, Q-7, Q-8 → finish rewording mod-002: team scope, ADR-0023 change, predicate paragraph. Under Q-8, also reword mod-008 (personal-object code). N-2, N-6, N-7, N-8.
9. Q-11 → **add** `feat-NNN-work-item-estimation-fields` (Release 2, M, deps mod-003, epic core/work-items). Then **re-dep** mod-011 → feat-004, feat-009 and the new line, and mod-012 → the new line. N-24, N-33.
10. Q-10 (a) → **add** `feat-NNN-board-backlog-filter` (M, deps mod-006 and feat-003, epic core/backlogs-boards). **Re-dep** feat-003 → mod-005 and feat-008 → the new line (plus mod-007). **Reword** feat-003 as list-only: keyword, former members, deleted items. Under (b), reword feat-003 and mod-005 only. N-21, N-20.
11. Q-15 (b) → **add** `feat-NNN-backlog-deleted-items` (M, deps mod-005 and mod-006) and **re-dep** mod-006 → mod-003 and doc-003. Under (a), drop mod-006's redundant mod-003. N-34, N-33.
12. Q-12 (a) → **reword** mod-008 (adds "Under" and keyword) and mod-010 (drops "Under"). N-23.
13. Q-9 (a) → **reword** mod-008 and mod-009: org-admin purge. N-13.
14. Q-14 → one of:
    - (a): **add** `doc-NNN-release-2-scope` (docs, epic product/analysis, deps doc-010).
    - (b): **withdraw** mod-008..mod-013 and feat-005/006/011/012.
    - (c): **add** doc-007's release-3 rows.

    N-26.

15. When doc-010 merges: move mod-001 to § Next (N-33).

If Q-6 (b) is chosen, mod-002 may outgrow L. In that case the event-listener provider becomes its own line, added before step 8.

## Summary

**Counts.** 34 root causes: 3 blockers, 23 majors, 8 minors. 14 are marked human (15 questions); 20 are default. The 70 source findings by dimension:

| Dimension | Findings | Blocker | Major | Minor |
|---|---|---|---|---|
| A | 19 | 3 | 7 | 9 |
| B | 12 | 1 | 7 | 4 |
| C | 20 | 2 | 12 | 6 |
| D | 19 | 2 | 10 | 7 |
| Total | 70 | 8 | 36 | 26 |

**Most consequential:**
1. N-1 — Two approved state-name sets conflict for four of the five types. mod-001's seed and mod-003's tests would disagree. mod-001 is now guarded on main.
2. N-2 — Several teams per project is MVP, but doc-001/003/006 still model one team. No operation creates a team, and the root owner and team deletion are open, while mod-002 and mod-006 promise them.
3. N-3 — The W-10/W-11 keys are missing from doc-006/doc-007 and from the mod-001/mod-002 lines, which ship before the identifier's consumer. Leaving them out means backfilling a unique, immutable key.
4. N-6 — Constraint 5's service account breaks two accepted ADR-0023 guards. mod-002 owes an unsized ADR change or Keycloak extension.
5. N-5 — The ADR-0018 amendment's constraints stop at M1. There is no M3–M6 resource resolution, no path for the panel to learn org-scoped codes, and no realm role inventory.

**Where the reports disagree, and what was picked here:**

| Topic | Positions | Picked |
|---|---|---|
| Category wording | A-12 vs D-11 | Q-2 recommends D-11 |
| D-14 owner | B-2 vs C-7 | Q-8 recommends B-2's split |
| M6 purge | A-6/B-8/D-7 in-product vs C-10 operator procedure | Q-9 recommends in-product |
| Estimation fields | A-9 vs C-5 | Q-11 recommends C-5 |
| Re-parenting on the form | A-7 vs D-9 | D-9 |
| "=" on Итерация | D-10 (subtree) vs A-15/C-6 (exact, plus "Under") | A-15 |
| Delivery-plan editor | A-18 (member/admin) vs B-11 (org admin) | A-18 |
| Unowned-area clauses | A-4 (delete) vs D-5 (defensive invariant) | A-4 |
| doc-003 BR-13 | D-6/D-19 (delete) vs A-2/B-1 (rewrite) | rewrite |
| Second-team story | C-1 (new story) vs B-1 (amend US-6) | amend US-6 |
| Per-team default area | A-2 recommends one in MVP | Q-4 recommends against, because MVP creates items from no team surface |
| Decision class | A-15/D-10, B-2 and B-8 marked default where C-6, C-7 and C-10 asked the human | kept as human questions |

**Assessment.** mod-001..mod-003 can start once the amendments are applied and a subset of questions is answered.
- Before the pass: Q-13, which decides how the pass treats doc-001.
- For mod-001: Q-1, and Q-2 for its category labels.
- For mod-002: Q-3, Q-4, Q-6, Q-7 and Q-8. Q-5 matters only if (b) is chosen.

mod-003 then has everything it needs from defaults: a data model (N-15), a concurrency policy with an ADR owner (N-16), an audit scope (N-17), a code catalog (N-9) and «Удалена» rules (N-14).

Q-9, Q-10 and Q-15 shape the MVP after mod-003 (mod-005, mod-006, feat-003) and can be answered with the others at gate-1. Q-11, Q-12 and Q-14 concern releases 2 and 3 only.

What stays open after this task:
- The ADR contents, owed by their lines: the ADR-0018 and ADR-0023 amendments, soft deactivation, history, the concurrency token.
- The release-2/3 scope itself, which needs the scoping line under Q-14.
- The size of the amendment pass: it touches all seven merged specs, and N-32's renumbering must run last so citations are rewritten once.
- doc-010's own D-2 needs widening (N-2, N-8) in its status.md.

## Source index

Each cell gives the N-entry that consumes that source id; row n holds A-n, B-n, C-n and D-n. — = no such id.

| n | A-n | B-n | C-n | D-n |
|---|---|---|---|---|
| 1 | N-1 | N-2 | N-2 | N-1 |
| 2 | N-2 | N-8 | N-1 | N-3 |
| 3 | N-3 | N-5 | N-21 | N-14 |
| 4 | N-11 | N-6 | N-25 | N-16 |
| 5 | N-15 | N-7 | N-24 | N-11 |
| 6 | N-12 | N-10 | N-23 | N-2 |
| 7 | N-18 | N-9 | N-8 | N-12 |
| 8 | N-14 | N-13 | N-12 | N-14 |
| 9 | N-24 | N-5 | N-16 | N-18 |
| 10 | N-19 | N-29 | N-13 | N-23 |
| 11 | N-27 | N-31 | N-19 | N-4 |
| 12 | N-4 | N-5 | N-26 | N-17 |
| 13 | N-28 | — | N-22 | N-16 |
| 14 | N-29 | — | N-20 | N-17 |
| 15 | N-23 | — | N-33 | N-28 |
| 16 | N-10 | — | N-33 | N-23 |
| 17 | N-30 | — | N-33 | N-27 |
| 18 | N-31 | — | N-34 | N-29 |
| 19 | N-29 | — | N-33 | N-32 |
| 20 | — | — | N-8 | — |

70 ids (A 19, B 12, C 20, D 19); each appears in exactly one entry's `Sources:` line.
