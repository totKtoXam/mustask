# Cross-review B — access, roles and permissions

Coverage: doc-010 status.md; AGENTS.md; spec.md + status.md of doc-001..007 (full); stories read in full — doc-001 US-1..US-10, doc-006 US-1..US-7, doc-002 US-6/US-10, doc-003 (role/refusal scenarios), doc-004 US-2/US-7/US-12 (+ role/refusal scenarios of US-1/3/9/10/11/13/14), doc-005 US-2/US-5 (+ US-3/US-4 role lines), doc-007 US-1..US-5 (role lines); doc-006 review.md R-20; `deploy/keycloak/realms/mustask-realm.json`; `PermissionCatalog.cs`; `KeycloakRealmRolesTests.cs`; `src/frontend/src/shared/api/tokenClaims.ts`; `scripts/keycloak-seed.sh` (group lines); ADR-0018, ADR-0022 (permission lines), ADR-0023; `docs/backlog.md` (branch copy, the mod-002/mod-008/doc-008/doc-009 lines).

Line numbers are those of the merged files on this branch. "IA" = instance administrator, "KO" = whoever administers realm `mustask` in the Keycloak console, "A/M/V" = membership roles admin/member/viewer, "NM" = a user with no membership in the resource's organization.

## Findings

### B-1
Severity: blocker
Affects: doc-006 BR-M1-9, BR-M1-11, BR-M1-14, § Roles (`organizations.team.write`), ER, Operation completeness (Team), § MVP boundary, § Release 2, § Decomposition, US-5, US-6, US-7; doc-001 § MVP boundary, MVP table (M1), Operation completeness (Team / Area), US-1, US-2; doc-003 BR-13, Operation completeness (Board); doc-005 BR-10; source: doc-004 E-5, E-13; binding: doc-006 H-9
Problem: the absorbed carry-back (ex-doc-009) was scoped to doc-006 only, but "one team per project" is stated in three specs, and several teams in MVP opens two actor questions no spec answers.
- doc-006: BR-M1-9 (L212) "MVP has no action to create a second team or to delete the one that exists"; Team row (L229) "no: created with the project, exactly one"; ER L168 `"one per project in MVP"`, L177 `"has, exactly one in MVP"`; BR-M1-11 (L214) "ownership decides nothing there yet"; US-5 L20 "exactly one team is created"; US-6 L43-48 "no action creates a second team or deletes this one"; US-7 L34-35 "in MVP the project has one team"; MVP boundary L269 "the project with its one team"; Release 2 L288 "several teams per project and team deletion"; decomposition L327 "Project with its one team".
- doc-001: L230-231 "each with one default team"; M1 row L238 "exactly one default team per project"; Operation completeness L295 (Team create "one default per project, MVP"; the Update and Delete cells name no actor at all); US-1 L57-58; US-2 L9-10, L16-19 "the default team".
- doc-003 BR-13 (L69): "MVP ships exactly one team per project … not end-to-end with two real teams" — contradicts E-5 and was not on the carry-back's list; doc-003 Board row (L95) points at an M1 "team deactivation" that no spec defines.
- No operation "create a team" exists in any spec, and `organizations.team.write` covers "(rename, membership)" only (doc-006 L77) — the MVP operation E-5 needs has no actor and no code.
- Team deletion is undecided: doc-004 L202 "team deletion stays doc-006's to decide"; doc-006 L288 release 2; doc-005 BR-10 (L143) "When a team is deleted (release 2+)"; doc-005 BR-6 (L139) already describes a deleted bound team.
- Root area owner: BR-M1-11 and H-9 ("the root area is not … re-owned on its own") pin it to "the project's team"; with several teams it is unsaid which one, and whether that team may ever be deleted.
Proposed resolution (the amendment doc-010 commits):
- doc-006 BR-M1-9 → "Creating a project creates its first team, named after the project, with the creating administrator as its only member. An organization administrator adds further teams to the project at any time; a team's name is 1–100 characters and unique inside its project, case- and space-insensitive. [T-1.] Several teams per project are MVP (doc-004 E-5)."
- doc-006 BR-M1-11 → "…and is always owned by a team of the project — initially its first team; [T-2]." Delete the sentence "ownership decides nothing there yet … start from areas that already carry an owner".
- doc-006 § Roles L77 → `organizations.team.write` "(create, rename, membership[, delete per T-1])".
- doc-006 ER → TEAM name "unique inside its project"; `PROJECT ||--o{ TEAM : "has, one or more; the first comes with the project"`.
- doc-006 Team row → Create "org admin (US-6; the first comes with the project, BR-M1-9)"; Delete per T-1.
- doc-006 stories: US-5 L20 "its first team"; US-6 retitled "…manages the project's teams", scenario L43-48 replaced by "An administrator adds a second team" and "A team's name is unique inside its project" (plus a delete scenario or an explicit "no deletion" per T-1); US-7 owner picker offers "every team of the project, or no explicit owner", plus one scenario re-owning a child area from team A to team B.
- doc-006 § MVP boundary / § Release 2 / § Decomposition → "the project with its teams (the first created with it)"; Release 2 keeps only "team deletion" if T-1 = (a).
- doc-001 → L230-231 and L238 "one or more teams (the first created with the project)"; L295 Team create "org admin (the first with the project, further ones any time, doc-004 E-5)", Update "org admin — rename, add/remove members (candidates: the organization's members)", Delete per T-1; US-1 L57-58 "its first team"; US-2 "a team's membership".
- doc-003 BR-13 → "A project has one or more teams in MVP (doc-004 E-5); every team, including one added later, gets its three boards and backlogs when it is created. The cross-team behaviours of BR-5/BR-6/BR-11 are MVP behaviour and are tested with two real teams." Board row L95 "team deletion per doc-006 BR-M1-9".
- Handed to dimension D (behaviour, not access): with several teams, BR-M1-14 (a new item without an area gets the root area) puts an item created from team B's board onto the root owner's board; whether a per-team default area moves into MVP is D's to rule.
Decision: human — T-1 team deletion: (a) not in MVP, release 2 [recommended: matches doc-006 L288 and doc-005 BR-10, and avoids cascades over areas, boards, backlog entries, iteration selections/defaults and bound queries]; (b) in MVP — then refused while the team owns any area explicitly or the root, and the cascade over boards, backlog entries and iteration settings needs its own rules. T-2 root area owner: (a) the first team, never changed (H-9 unchanged) [recommended with T-1 (a)]; (b) re-ownable to another team of the project, never cleared (amends H-9). Everything else — default.

### B-2
Severity: major
Affects: doc-001 § Roles (Roles table), BR-5; doc-006 § Roles (L81-84); doc-005 § Roles (L21-33), BR-2, BR-9, US-2, § Decomposition (doc-NNN row), mod-008 deps; doc-002 BR-12, Operation completeness (Comment, Attachment); D-14 (doc-005)
Problem: D-14 (a viewer writes their own private objects) must be carried into doc-001 (ex-doc-008). The specs frame it as a departure from the code model (doc-005 L25-29 "an ownership-based write is a check doc-001's permission model does not express yet"). But ownership checks already exist in MVP, unstated as a mechanism: a comment is edited "only by its author" (doc-002 BR-12), an attachment is removed by "member (own)" (doc-001 L299, doc-002 L166). Three gaps follow:
- no spec says whether ownership narrows a role code or replaces it. An author later downgraded to viewer — can they still edit or delete their comment? Nothing answers this.
- doc-001's Roles table contradicts D-14 and also misstates restore: L44 Reader "read codes only | read everything a member can write"; L42 gives "restore/purge the recycle bin" to admin, while L297 and doc-002 L163 let a member restore.
- doc-005 US-2 L25-27 "this is the one write a viewer may perform anywhere in MUSTASK" stops being true once any other personal object arrives (doc-006 L290-291 release-2 notification subscriptions; doc-002 L208-209 follow/subscribe).
Proposed resolution:
- doc-001 § Roles, after "…and nothing outside it" (L32-34), add: "A code may carry an ownership predicate, checked after the code on the resolved object. (1) A role code narrowed to one's own objects — editing or deleting one's own comment, removing one's own attachment — is in the member bundle; the admin bundle holds the unrestricted moderation code. It goes with the role: an author downgraded to viewer no longer edits or deletes their comments. (2) A personal-object code is in every bundle, viewer included (doc-005 D-14): create, read, update, delete and restore of the acting user's own private objects — in release 2 their personal queries and their «Мои запросы» folder, and later any object a spec declares personal. No bundle, admin included, reads or writes another user's private object; the one exception is the organization administrator's purge of a soft-deleted one, which grants no read (doc-005 BR-9)."
- doc-001 Roles table: admin "…purge deleted work items"; member "…delete and restore a work item (BR-6)"; viewer bundle "read codes plus the personal-object code (D-14)", can "read everything a member can write; write only their own private objects (release 2)".
- doc-001 BR-5, append: "A code may carry an ownership predicate (§ Roles); a private object is reachable only by its owner, whatever their role, while they hold a membership in its organization (doc-005 D-14)."
- doc-006 § Roles after L84: "Outside M1 the viewer bundle is not read-only: it holds the personal-object code (doc-001 § Roles, doc-005 D-14)." Its predicate mechanism is B-3's constraint 6.
- doc-005: L25-33 → "doc-001 § Roles expresses this as a personal-object code in every bundle with an ownership predicate (amended by doc-010)"; BR-2 "regardless of organization role" → "whatever the owner's role, while they are a member of the organization"; US-2 L26-27 → "the one write a viewer may perform in release 2"; decomposition row `doc-NNN-personal-object-ownership` → "absorbed by doc-010 (D-2)"; the phrase "or else a follow-up M1 task" goes — see the next bullet.
- doc-002 BR-12 / Operation completeness: "author (member or admin role)" for comment edit/delete and "uploader (member or admin role)" for removal, so a downgraded author is refused.
- Queue: the predicate mechanism is MVP (comments, attachments), so mod-002's ADR amendment defines it (B-3 constraint 6). mod-008 then only registers the personal-object code in all three bundles — no separate M1 follow-up task, and no M1 dependency beyond the transitive one through mod-003.
Decision: default — the text above. The generalisation from "personal queries" to "any object a spec declares personal" reads D-14's "own private objects" as a class; the human can narrow it back to queries only.

### B-3
Severity: major
Affects: ADR-0018 amendment (owed by mod-002); doc-006 § Roles constraints 1-5; doc-001 L28-38, BR-5, L366-372; doc-005 L30-32, § Decomposition; doc-002 NFR "Permissions" (L190-191); queue line mod-002
Problem: every spec agrees the amendment is written by `mod-002-organizations-projects-teams`: doc-001 L28-29 and L366-369, doc-006 L30-32 and L315-317, doc-005 L227 ("mod-002 … if that has not shipped yet"), queue line mod-002. doc-010, like every analysis task, writes none. Two soft spots remain: doc-005 L31-32 labels the carry-back doc task "(§ Decomposition below, an ADR-0018 amendment)", and doc-006 L292 promises a second amendment for a release-2 rights screen. The real gap is scope. doc-006's five constraints miss four decisions the other specs and today's code force:
- (a) How the panel learns org-scoped codes. The panel reads rights only from the token (`tokenClaims.ts` L1-2 "not from a separate server response", L53 `resource_access` only), and ADR-0018 L144-147 keeps "permission-based navigation filtering". Every "no … control is offered" criterion needs it: doc-002 L190 "a reader never sees a write control", doc-006 US-2/3/5/6/7, doc-003 US-1/2/3, doc-004 US-1/2/3/12. Once org-scoped codes leave the token (constraint 1), nothing in the constraints tells the panel what a user may do in an organization.
- (b) Resource resolution for M3–M6. BR-M1-7 (L210) resolves only Area/Team → Project → Organization. WorkItem, Iteration, Board/BacklogEntry, Query and Dashboard reach their organization by paths no spec states, and each needs the same per-request lookup (BR-M1-6), the same "not found" for non-members (BR-M1-8, see B-9) and the ownership predicate (B-2).
- (c) The realm role inventory. doc-001 L35-38 and BR-5 L278 allow "only … two instance-level roles". But US-1 L25-26 grants "create organizations" "through a role or group that carries it" (a third role or group). Constraint 2 (L45-51) keeps `dictionaries.languages.read` only for users "who hold that code today", yet users an org admin adds later (D-10) may be in no Keycloak group and would read no dictionary. `keycloak-seed.sh` L25-32 and the E2E fixture put users into groups by the member/viewer roles being removed.
- (d) Catalog shape. Instance and org-scoped codes share one naming space (B-12), and the contract test must compare instance codes only (constraint 1). The catalog therefore needs a scope attribute, or two registries.
Proposed resolution: doc-006 § Roles gains constraints 6–8, and the mod-002 queue line reads "owes the ADR-0018 amendment bound by doc-006 § Roles constraints 1–8, and the ADR-0023 change of B-4":
- 6 — "An organization-scoped check is resource-based: the resource's organization is resolved (Area/Team → Project → Organization; WorkItem, Iteration, Board, BacklogEntry, Query, Dashboard → Project or Team → Organization), the acting user's membership there is read per request, a non-member gets 'not found', the role's bundle must hold the code, and an optional ownership predicate is then checked (doc-001 § Roles). Founders of M3–M6 plug into this; none re-derives it."
- 7 — "The panel obtains the acting user's codes for an organization from Mustask.Api (not from the token), with the same next-request freshness as BR-M1-6; navigation and write controls are filtered by them."
- 8 — "The amendment names every realm role and group that remains (at least: instance administrator, dictionaries administrator, a grantable 'create organizations' role or group) and states which role or group gives `dictionaries.languages.read` to a user who is in none today; it updates `scripts/keycloak-seed.sh`, the E2E fixture's group and `src/backend/AGENTS.md`." doc-001 BR-5's "the two instance-level roles" → "instance-level roles only (named by the ADR-0018 amendment)".
- doc-005 L31-32: drop "an ADR-0018 amendment".
Decision: default — constraints 6–8 as worded. The mechanisms themselves (endpoint shape, caching, registry) stay mod-002's design.

### B-4
Severity: major
Affects: doc-006 § Roles constraint 5 (L59-63), BR-M1-17, NFR "Identity refresh", US-3 (member search); ADR-0023 Decision 3; `KeycloakRealmRolesTests.ProductionRealmImport_NoClientHasServiceAccountEnabled`; `Mustask.Architecture.Tests` StandOnlyKeycloakClientRules check 3
Problem: constraint 5 (H-6) offers "admin API with a service account, or events" for reading Keycloak's user directory. The first option breaks two guards of an accepted ADR:
- ADR-0023 L201-205: "a no-service-account property over the realm-import files". The contract test at `KeycloakRealmRolesTests.cs` L369-393 fails on any production client with `serviceAccountsEnabled: true`.
- ADR-0023 L166-174 (guard 3): "no shipping project's source calls Keycloak's admin REST surface at all (`/admin/realms/...`)". User search is `GET /admin/realms/mustask/users`.
doc-006 R-20 (review.md L597-600), which produced constraint 5, did not see ADR-0023. So mod-002 cannot satisfy constraint 5 by its first option without superseding part of ADR-0023, and the second option (events) is a Keycloak event-listener provider — a new deployable artifact that neither ADR mentions.
Proposed resolution: constraint 5 gains "…and states what it changes in ADR-0023's guards"; the product plan records that mod-002 owes an ADR-0023 change or a Keycloak extension, and sizes it in.
Decision: human — (a) amend ADR-0023's guards to allow exactly one named production client with a service account holding only `realm-management` view-users/query-users, and shipping code calling only the read-only user endpoints [recommended: smallest change, keeps the guards meaningful as a closed allowlist]; (b) an event-listener provider in Keycloak pushes user create/update/disable/delete into MUSTASK's projection, and search runs on the projection — no admin API from shipping code, but a custom Keycloak artifact and a backfill of existing users; (c) no directory channel: members are added by exact e-mail among users who have signed in once — narrows D-10/US-3 and loses BR-M1-17 for users who never return, not recommended.

### B-5
Severity: major
Affects: doc-001 § Roles (L35-38), BR-5; doc-006 BR-M1-2, § Operation completeness ("Instance right"), US-1, US-4 (last scenario); realm `mustask` roles; ADR-0018 § Consequences
Problem: "instance administrator" names two different actors. doc-001 L36-37 calls it a Keycloak-kept role with "every instance-level permission, incl. granting "create organizations"". doc-006 US-1 L24-26 has the instance administrator grant "in the Keycloak console, through a role or group", and US-4 L74-76 says the role lets them "create an organization and grant that right".
- A MUSTASK realm role is a composite of `mustask-api` codes only (ADR-0018 L80-84; realm L47-60; `KeycloakRealmRolesTests` L93-130). It gives nobody access to the Keycloak admin console. Granting happens there (BR-M1-2, ADR-0018 L140-142), which takes Keycloak's own realm-management rights.
- No realm role in the import carries those rights. Today the only such actor is the Keycloak bootstrap administrator, which AppHost holds as a secret.
- "Granting" also has no code in doc-006's catalog (L65-79), and cannot have one, since MUSTASK has no screen for it.
The persona in US-1 therefore cannot be tested as written.
Proposed resolution: doc-001 L36-37 → "**instance administrator** (every instance-level code: 'create organizations', M0's codes)", dropping "incl. granting". doc-001 § Roles and doc-006 BR-M1-2 add: "Granting and revoking 'create organizations' is Keycloak realm administration — whoever administers realm `mustask` in the Keycloak console does it; it is not a MUSTASK code." doc-006 US-1 persona and Operation completeness row "Instance right" → "a Keycloak realm administrator"; US-4 L74-76 → "lets them do only one thing — create an organization".
Decision: human — (a) granting stays a Keycloak-administration act, outside MUSTASK's role model [recommended: no MUSTASK role becomes a Keycloak admin, and ADR-0018/0023 stay as they are on this point]; (b) the instance administrator realm role also carries `realm-management` rights (manage-users or similar), so one person holds both — the ADR-0018 amendment must then compose non-`mustask-api` roles, change the contract test at L93-130, and accept a MUSTASK role that is a Keycloak administrator.

### B-6
Severity: major
Affects: doc-006 § Roles (L83-84), US-6 (story line L9-10); doc-003 § Roles (L24-27); doc-004 BR-12, US-12, BR-14; doc-005 Operation completeness (Dashboard team); doc-001 BR-5, BR-3
Problem: doc-006 L83-84 says team membership "decides which board and backlog a user sees (BR-3), not what they may do". doc-003 L26 says a reader may "Open any of their organization's team boards and backlogs". doc-004 lets any member move a team's unfinished work from that team's pane (BR-12, US-12 L44-48). doc-005 lets any member or admin edit any team's dashboard (L159). BR-3 is about which board a work item appears on, not which board a user may open. BR-5 forbids any scope finer than the organization. With one team the clash was invisible. With several teams (B-1) it decides whether a member of team A may drag cards on team B's board, and doc-006 and doc-003 answer differently.
Proposed resolution: doc-006 L83-84 → "Team membership carries no role and no access of its own: any organization member reads, and a member or administrator acts on, every team's boards, backlogs, planning pane and (release 3) dashboards (BR-5). It decides which team a user's board and backlog open on by default and, from release 2, whose capacity is planned (doc-004 US-7)." US-6 L9-10 so-that clause → "…so that the team's areas and default views have the right people".
Decision: default — organization-wide access, as above. Restricting a team's board to its members would be a per-team permission scope that D-9/BR-5 rule out, and needs the human to reopen D-9.

### B-7
Severity: major
Affects: doc-002 § Roles (L29-33), L251-255; doc-003 (no codes); doc-004 (no codes); doc-005 (no codes); doc-007 (no codes); doc-006 L65-84; queue lines mod-001, mod-003..mod-008, feat-001, feat-002, feat-004, feat-008..feat-010
Problem: only M1 has a code table (doc-006 L65-79, "analysis-level"). doc-006 L81-82 assumes "the work item, board, backlog and iteration codes owned by M3/M4/M5" exist, but none of doc-002, doc-003, doc-004 or doc-007 names one: doc-002 L29-33 only says which slice registers "read/write/delete", "moderation", "attachment removal and the Drive connection", "purge". Without one list, six operations are each guarded by two candidate codes owned by different tasks (§ Permission codes, "Duplicates"):
- a card drag writes `WORK_ITEM.state` (doc-003 BR-3) — the M3 write code, or an M4 code?
- the iteration pointer (doc-004 BR-7) is an M3 field written by M5's form, pane and bulk move;
- the backlog parent mapping (doc-003 BR-10, feat-004) is "the same Parent/Child link" as feat-001's;
- team iteration selection/default (doc-004) is a team setting — `organizations.team.write` (M1) or an M5 code?
- the delete code is registered by mod-003 (doc-002 L30), but delete/restore is founded by mod-005;
- the Drive connection's code belongs to no namespace. doc-002 L252-255 says "doc-006 only notes that it will exist", and doc-006 does not mention it.
Proposed resolution: the product plan (doc-010 spec) carries § Permission codes below as the one analysis-level catalog, each code with the task that registers it. The rules:
- one prefix per module: `dictionaries` M0, `organizations` M1, `process` M2, `workitems` M3, `boards` M4, `iterations` M5, `queries`/`dashboards` M6;
- a code guards the entity an operation writes, so a card drag, a pointer write, a pane drop and a bulk move are `workitems.workitem.write`, and a backlog parent mapping is `workitems.link.write`; M4 adds only `boards.backlog.reorder`, M5 adds `iterations.iteration.*` and `iterations.team-settings.write`;
- delete/restore is registered by mod-005; the Drive connection is registered by mod-004 as `workitems.attachment-storage.manage`, with one line in doc-006 § Roles saying the organization settings screen hosts it;
- every founder's Definition of Done lists the codes it registers. Exact strings stay each founder's to fix.
Decision: default — the catalog and rules below. The human can object to any single placement, for example wanting a separate M4 code for card moves.

### B-8
Severity: major
Affects: doc-001 BR-6 (L279), § Roles (L42), US-1 (L81-82), US-6; doc-006 BR-M1-1, US-2, US-5; doc-004 BR-10 (L142), Operation completeness (Iteration); doc-005 BR-9 (L142), Operation completeness; doc-002 US-10
Problem: which purges exist, and who performs them, differs across specs:
- doc-001 BR-6: "only an administrator purges a deleted object permanently, by hand", covering work items, organizations and projects. doc-001 US-1 L81-82: a deleted project "stays recoverable indefinitely until an administrator purges it by hand".
- doc-006 BR-M1-1 (L204): "MVP has no purge" for organizations and projects.
- doc-004 BR-10 (L142): "an administrator purges by hand" for iterations — no operation row, no story, no code.
- doc-005 BR-9: "only an administrator purges" M6 objects, including another user's soft-deleted personal query, "grants no read of its content". Its Operation completeness has no purge cell, and the admin has no way to pick a private query to purge without seeing it.
- "administrator" is never qualified. Instance administrators hold no organization content (doc-006 US-4), so only the organization administrator fits.
The only purge that exists as a specified MVP operation is the work item purge (doc-002 US-10, mod-005).
Proposed resolution: doc-001 BR-6 → "…retention is indefinite; where a spec defines a purge operation, only the organization administrator performs it. MVP defines one: purging a deleted work item (doc-002 US-10). Organizations, projects and iterations have no purge in MVP." doc-001 US-1 L81-82 → "stays recoverable indefinitely (no purge in MVP, doc-006 BR-M1-1)". doc-004 BR-10 last clause → "There is no reactivation and no purge in MVP." doc-005 Operation completeness gains an org-admin purge cell per entity (release 2/3), and BR-9 adds "the purge list of another user's private objects shows owner, folder and deletion date, never name or clauses".
Decision: default — as above. Adding organization/project purge to MVP would reverse doc-006 BR-M1-1 (gate-1-approved) and needs the human.

### B-9
Severity: minor
Affects: doc-007 US-2 (L23-27); doc-002 BPMN (L102-103); doc-001 BR-5; doc-006 BR-M1-8
Problem: BR-M1-8 answers a non-member "not found", never "forbidden". doc-007 US-2 L26 has a non-member's process request "refused", and doc-002 L102-103 checks "the write permission in this project" and answers «Недостаточно прав» on every refusal. A 403 to a non-member confirms the organization exists, which BR-M1-8 is written to prevent. The rule lives only in M1, so each module restates or contradicts it on its own.
Proposed resolution: doc-001 BR-5 appends "A user with no membership in the resource's organization is answered 'not found' in every module (doc-006 BR-M1-8)". doc-007 US-2 L26-27 → "Then the answer is «Организация не найдена» and no type, state or level is disclosed". doc-002 L102 "in this project" → "in this project's organization", with «Недостаточно прав» only for members lacking the code.
Decision: default.

### B-10
Severity: minor
Affects: doc-001 US-2/3/4/5/6/7/8/9; doc-002 US-1..US-12 role scenarios; doc-003 US-1/2/3; doc-004 US-1/2/3/9/10/12/13/14; doc-005 US-5; doc-007 US-1
Problem: many stories name roles or actors that do not exist in M1's model:
- a sign-in role: "a user signed in with only the "viewer" role" (doc-001 US-4 L72, doc-002 US-1 L75, doc-003 US-1 L118 and others), which is ambiguous for a user who is admin in organization A and viewer in B (D-2);
- a project-scoped role: "the "member" role on a project" (doc-001 US-4 L28, doc-002 US-1 L16);
- personas M1 does not define: "product owner" (doc-004 US-9 L8), "manager" (doc-004 US-10 L8), "team or project member" (doc-005 US-5 L8);
- the wrong creator: doc-007 US-1 L8/L16 "an organization administrator creates a new organization", whereas under D-10 the creator is an IA or a grantee and becomes admin only as a result.
Proposed resolution: one wording rule in doc-001 § Roles, "stories name the actor as 'a user whose role in the item's organization is X'", and a mechanical sweep of the listed lines. doc-004 US-9/US-10 personas → "a member" / "an organization member"; doc-005 US-5 → "an organization member"; doc-007 US-1 → "a user permitted to create organizations (doc-006 US-2)".
Decision: default.

### B-11
Severity: minor
Affects: doc-004 US-7 (L18), BR-15; doc-003 § Roles (L28), Out of scope; doc-004 US-10 / BR-17 (plan editing); doc-006 Out of scope ("Custom roles … fixed by D-9")
Problem: MVP team settings are organization-admin only (iteration selection/default, doc-004 BR-14). Release-2/3 team-level settings have no actor, or a different one. In doc-004 US-7 L18 a member sets capacity "for each of them", including team days off and working days (BR-15). Board column, swimlane and WIP administration (doc-003 L28) and delivery plan editing (doc-004 US-10) name no actor. Azure DevOps uses team administrators, which MUSTASK has ruled out (fixed roles, D-9).
Proposed resolution: doc-001 § Roles rule — "every team-level setting (iterations, working days, board configuration, default area) is the organization administrator's; per-iteration values a team enters about itself (capacity, days off) are a member's". doc-004 US-7 split accordingly; doc-003 L28 and doc-004 US-10 name the organization administrator.
Decision: default — a team-administrator role would reopen D-9 and is left to release-2 analysis.

### B-12
Severity: minor
Affects: doc-006 § Roles table (L67 vs L68-70), constraint 1; PermissionCatalog
Problem: `organizations.organization.create` is instance-scoped (token-carried, in the realm import), while its siblings `organizations.organization.read/write/deactivate` are organization-scoped and must never appear in the realm (constraint 1). The pattern `<module>.<resource>.<action>` then does not say where a code lives. The realm-import contract test (L32-62 today: "client roles equal `PermissionCatalog.AllCodes`") has to tell them apart by something other than the name.
Proposed resolution: constraint 1 adds "every catalogued code carries its scope (instance | organization), and the realm contract test filters on it". Either rename the instance code off the org-scoped resource (e.g. `organizations.instance.create-organization`) or keep the name and rely on the scope attribute — mod-002's call.
Decision: default.

## Matrix

Legend: Y allowed · N refused (a member of the organization gets «Недостаточно прав») · own = only on objects the actor authored or owns · 404 = "not found" (BR-M1-8) · ? = specs disagree or say nothing · r2/r3 = release 2/3. "Flag" names the finding that resolves the cell.

**Instance level.** IA and grantees hold no org-scoped code: in the organization table they act only through their own membership (doc-006 US-4 L40-46, L70-78).

| Operation | IA (realm role) | "Create organizations" grantee | Keycloak realm administrator | Dictionaries administrator | Any other signed-in user | Sources | Flag |
|---|---|---|---|---|---|---|---|
| Read languages dictionary (M0) | Y | only via a group carrying the code | — | Y | Y today (member/viewer realm roles); after the amendment ? for users in no group | realm L47-80; doc-006 constraints 2, 4 | B-3 |
| Write languages dictionary (M0) | Y | N | — | Y | N | realm; `PermissionCatalog` | — |
| Grant / revoke "create organizations" | doc-001 L36-37 Y; doc-006 US-1/US-4 Y "in the console" | N | the console is where it happens (BR-M1-2); no spec names this actor | N | N | doc-001 § Roles; doc-006 BR-M1-2, US-1 | B-5 |
| Create an organization (becomes its admin) | Y | Y | — | N | N | D-10; doc-006 US-1/US-2; doc-007 US-1 says "organization administrator" | B-10 |
| Open an organization without a membership | 404 | 404 | — | 404 | 404 | doc-006 US-1 L49-55, US-4 | — |
| Rescue an organization with no usable admin | N (accepted risk H-1) | N | outside MUSTASK only | N | N | doc-006 § MVP boundary | — |

**Organization level** (roles in the resource's organization).

| # | Operation | Rel | A | M | V | NM | Sources | Flag |
|---|---|---|---|---|---|---|---|---|
| 1 | Read organization, projects, teams, areas, member list (who/when) | MVP | Y | Y | Y | 404 | doc-006 | — |
| 2 | Rename / deactivate / restore organization | MVP | Y | N | N | 404 | doc-006 US-2 | — |
| 3 | Add member, change role, remove member | MVP | Y | N | N | 404 | doc-006 US-3 | — |
| 4 | Leave the organization (own membership) | MVP | Y if another active admin | N | N | — | BR-M1-4 | — |
| 5 | Create / rename project (sets project key at creation) | MVP | Y | N | N | 404 | doc-006 US-5; doc-002 W-10 | (project key: dimension A) |
| 6 | Deactivate / restore project; see the deactivated list | MVP | Y | N | N | 404 | doc-006 US-5 | — |
| 7 | Purge organization / project | ? | doc-001 BR-6, US-1: Y; doc-006: none in MVP | N | N | — | | B-8 |
| 8 | Create a further team in a project | MVP (E-5) | ? — no spec has the operation | N | N | 404 | doc-004 E-5; doc-006 BR-M1-9 | B-1 |
| 9 | Delete a team | ? | doc-001/006 r2; doc-004 "doc-006 to decide"; doc-005 "r2+" | N | N | — | | B-1 |
| 10 | Rename team; add/remove team member | MVP | Y (doc-001 L295: no actor named) | N | N | 404 | doc-006 US-6 | B-1 |
| 11 | Area create/rename/move/re-own/delete (non-root) | MVP | Y | N | N | 404 | doc-006 US-7 | — |
| 12 | Re-own the root area | MVP | never (BR-M1-11, H-9); open with several teams | N | N | — | | B-1 |
| 13 | Open / act on a team's board or backlog without being in the team | MVP | doc-003: Y; doc-006 L83-84: team membership "decides which board … a user sees" | same split | read: same split | 404 | | B-6 |
| 14 | Read the process definition | MVP | Y | Y | Y | doc-007 US-2 "refused" vs 404 | doc-007 US-2 | B-9 |
| 15 | Customize the process | r3 | Y | N | N | — | doc-007 | — |
| 16 | Connect / disconnect the organization's Drive | MVP | Y | N (sees availability) | N (same) | 404 | doc-002 US-13 | B-7 (no code) |
| 17 | Create WI; edit fields/state/tags; link; add comment; attach | MVP | Y | Y | N | 404 | doc-001, doc-002 | — |
| 18 | Edit own comment | MVP | own | own | ? (author downgraded to viewer) | — | doc-002 BR-12 | B-2 |
| 19 | Delete a comment | MVP | any (moderation) | own | ? | — | doc-002 BR-12 | B-2 |
| 20 | Remove an attachment | MVP | any | own | ? | — | doc-002 L166 | B-2 |
| 21 | Delete WI (→ «Удалена»), restore | MVP | Y | Y (doc-001 Roles table L42 gives restore to admin only) | N | 404 | doc-002 US-10 | B-2 |
| 22 | Purge a deleted WI | MVP | Y | N | N | — | doc-002 US-10 | — |
| 23 | Drag a card (writes state) | MVP | Y | Y | N | 404 | doc-003 BR-3 | B-7 (code) |
| 24 | Reorder a backlog | MVP | Y | Y | N | 404 | doc-003 US-2 | B-7 (code) |
| 25 | Assign/clear a parent from the backlog | MVP | Y | Y | N | 404 | doc-003 BR-10 | B-7 (same code as link write) |
| 26 | Iteration create/rename/date/move/remove | MVP | Y | N | N | 404 | doc-004 BR-14 | — |
| 27 | Purge an iteration | ? | doc-004 BR-10 "an administrator purges by hand"; no operation | N | N | — | | B-8 |
| 28 | Team iteration selection / default | MVP | Y | N | N | 404 | doc-004 US-2 | B-7 (M1 or M5 code) |
| 29 | Set/clear a WI's iteration (form, pane) | MVP | Y | Y | N | 404 | doc-004 US-3, US-11 | B-7 |
| 30 | Move a team's unfinished work (any team's pane) | MVP | Y | Y | N | 404 | doc-004 BR-12 | B-6 |
| 31 | Capacity, days off, team working days | r2 | ? | Y (US-7, incl. team settings) | N | — | doc-004 US-7 | B-11 |
| 32 | Board columns/swimlanes/WIP settings | r2 | ? (no actor) | ? | N | — | doc-003 L28 | B-11 |
| 33 | Delivery plan create/edit / read | r3 | ? ("manager") / Y | ? / Y | N / Y | — | doc-004 US-10 | B-10, B-11 |
| 34 | Own personal query and «Мои запросы»: CRUD, restore | r2 | own | own | own (D-14) | — | doc-005 BR-2 | B-2 |
| 35 | Purge another user's soft-deleted personal query | r2 | Y, no read (no operation row) | N | N | — | doc-005 BR-9 | B-8 |
| 36 | Shared query/folder CRUD, restore; promote own | r2 | Y | Y | N (read, run) | 404 | doc-005 BR-3, BR-11 | — |
| 37 | Dashboards/widgets CRUD (any team's, any project's) | r3 | Y | Y | N (read) | 404 | doc-005 | B-6 |
| 38 | CSV import / export | r3 | Y / Y | Y / Y | N / Y | 404 | doc-005 BR-8 | — |
| 39 | Purge M6 objects | r2/3 | Y (BR-9; no operation row) | N | N | — | doc-005 BR-9 | B-8 |

Noted, not flagged: a viewer is an assignee candidate (doc-001 BR-13 "every member of the … organization") and can change nothing on the item assigned to them. Every spec is consistent on this, but the plan's demo scenarios should not assume an assignee can act on their own item.

## Permission codes

Status: **stated** = named in a spec or code; **implied** = the operation is specified but no code is named (the string here is the proposed analysis-level name under B-7's rules); **conflict** = see the flag.

| Code | Scope | Registered by | A / M / V | Source | Status |
|---|---|---|---|---|---|
| `dictionaries.languages.read` | instance | M0 (exists) | realm roles admin, member, viewer, dictionaries-admin | `PermissionCatalog.cs` L11 | stated; holders after the amendment → B-3 |
| `dictionaries.languages.write` | instance | M0 (exists) | realm roles admin, dictionaries-admin | L14 | stated |
| `organizations.organization.create` | instance | mod-002 | IA and grantees (Keycloak) | doc-006 L67 | stated; shares the resource with org-scoped codes → B-12 |
| `organizations.organization.read` | org | mod-002 | Y / Y / Y | doc-006 L68 | stated |
| `organizations.organization.write` (rename) | org | mod-002 | Y / N / N | L69 | stated |
| `organizations.organization.deactivate` (+ restore) | org | mod-002 | Y / N / N | L70 | stated |
| `organizations.membership.read` | org | mod-002 | Y / Y / Y | L71 | stated |
| `organizations.membership.write` | org | mod-002 | Y / N / N | L72 | stated |
| `organizations.project.read` | org | mod-002 | Y / Y / Y | L73 | stated |
| `organizations.project.write` (create, rename; key at creation) | org | mod-002 | Y / N / N | L74; doc-002 W-10 | stated |
| `organizations.project.deactivate` (+ restore, deactivated list) | org | mod-002 | Y / N / N | L75 | stated |
| `organizations.team.read` | org | mod-002 | Y / Y / Y | L76 | stated |
| `organizations.team.write` (rename, membership **+ create**, + delete per T-1) | org | mod-002 | Y / N / N | L77 | stated; missing create → B-1 |
| `organizations.area.read` / `.write` | org | mod-002 | Y/Y/Y · Y/N/N | L78-79 | stated |
| `process.definition.read` | org | mod-001 | Y / Y / Y | doc-007 § Roles, US-2 | implied → B-7 |
| `process.definition.write` (types, fields, states, rules, layout, levels, Reason) | org | release-3 `feat-NNN-process-*` | Y / N / N | doc-007 Operation completeness | implied |
| `workitems.workitem.read` | org | mod-003 | Y / Y / Y | doc-002 L29-30 | implied |
| `workitems.workitem.write` (create, fields, state, tags; card drag; iteration pointer, pane drop, bulk move; CSV import) | org | mod-003 | Y / Y / N | doc-002, doc-003 BR-3, doc-004 BR-7/12, doc-005 BR-8 | implied; duplicates below → B-7 |
| `workitems.workitem.delete` (+ restore) | org | mod-005 (doc-002 L30 says mod-003) | Y / Y / N | doc-002 US-10 | implied; conflict → B-7 |
| `workitems.workitem.purge` | org | mod-005 | Y / N / N | doc-002 L32 | implied |
| `workitems.link.write` (also the backlog parent mapping) | org | feat-001; reused by feat-004 | Y / Y / N | doc-002 US-5; doc-003 BR-10 | implied → B-7 |
| `workitems.comment.write` (add; edit/delete own — ownership predicate) | org | feat-002 | Y / own / N | doc-002 BR-12 | implied → B-2 |
| `workitems.comment.moderate` | org | feat-002 | Y / N / N | doc-002 L31 | implied |
| `workitems.attachment.write` (add; remove own — ownership predicate) | org | mod-004 | Y / own / N | doc-002 L166 | implied → B-2 |
| `workitems.attachment.moderate` (remove any) | org | mod-004 | Y / N / N | doc-002 L31 | implied |
| `workitems.attachment-storage.manage` (Drive connect/disconnect) | org | mod-004 | Y / N / N | doc-002 US-13, L252-255 | implied; no namespace owner → B-7 |
| `boards.backlog.reorder` | org | mod-006 | Y / Y / N | doc-003 US-2 | implied → B-7 |
| (board/backlog read = `workitems.workitem.read` + `organizations.team.read`) | org | — | Y / Y / Y | doc-003 § Roles | implied; no own code proposed |
| `iterations.iteration.read` | org | mod-007 | Y / Y / Y | doc-004 | implied |
| `iterations.iteration.write` (create, rename, date, move, remove) | org | mod-007 | Y / N / N | doc-004 BR-14 | implied |
| `iterations.team-settings.write` (selection, default) | org | mod-007 | Y / N / N | doc-004 US-2 | implied; M1-vs-M5 conflict → B-7 |
| `iterations.capacity.write` | org | r2 feat-011 | ? / Y / N | doc-004 US-7 | implied → B-11 |
| `iterations.delivery-plan.write` | org | r3 mod-013 | ? | doc-004 US-10 | implied; no actor → B-11 |
| `queries.query.read` (shared; run, CSV export) | org | mod-008 | Y / Y / Y | doc-005 BR-3, BR-8 | implied |
| `queries.query.write` (shared queries, folders, promote own) | org | mod-008 | Y / Y / N | doc-005 BR-3, BR-11 | implied |
| `queries.personal.write` (own queries and «Мои запросы»; ownership predicate) | org | mod-008 | own / own / own | doc-005 D-14, BR-2 | implied; the D-14 code → B-2 |
| `queries.query.purge` (incl. others' private, no read) | org | mod-008 | Y / N / N | doc-005 BR-9 | implied → B-8 |
| `dashboards.dashboard.read` / `.write` (+ widgets) | org | r3 mod-009 | Y/Y/Y · Y/Y/N | doc-005 | implied |
| purge of organization / project / iteration | — | nobody | — | doc-001 BR-6; doc-004 BR-10 | no operation in MVP → B-8 |

**Duplicates under different names (one operation, two candidate codes):**
1. Card drag (M4) vs `workitems.workitem.write` (M3 state field).
2. Iteration pointer set/clear, pane drop and bulk move (M5 stories) vs `workitems.workitem.write` (the pointer is an M3 field).
3. Backlog parent mapping (M4, feat-004) vs `workitems.link.write` (M3, feat-001) — doc-003 BR-10 says it is the same link.
4. Team iteration selection/default (M5) vs `organizations.team.write` (M1 team settings).
5. Work item delete/restore registered by mod-003 (doc-002 L30) vs the soft-delete pattern founded by mod-005.
6. `organizations.organization.create` (instance) vs `organizations.organization.*` (org) — one resource, two scopes (B-12).

**Codes no module owns:** the Drive connection (doc-002 defers to doc-006, which is silent); the personal-object code (ex-doc-008 put it in "M1's permission catalog", but doc-006's catalog is M1-only); process read (mod-001's scope lists no code); purge for organizations, projects and iterations (promised in doc-001 BR-6 and doc-004 BR-10, with no operation).

**MVP operations with no code named in any spec:** every M2–M5 row above — process read; work item read/write/delete/purge; link, comment and attachment writes and moderation; Drive connection; backlog reorder; iteration management; team iteration settings; iteration pointer writes.

## Summary

Blocker 1 (B-1) · major 7 (B-2..B-8) · minor 4 (B-9..B-12) · 12 findings. Human decisions: B-1 (T-1 team deletion, T-2 root area owner), B-4 (Keycloak user-directory channel vs ADR-0023), B-5 (who grants "create organizations"); the rest are defaults.

Most consequential:
1. B-1: several teams per project in MVP contradicts doc-006, doc-001 and doc-003 BR-13. No operation or code creates a team, and team deletion and root-area ownership (H-9) are undecided — mod-002 cannot start without them.
2. B-3: the ADR-0018 amendment's constraints miss how the panel learns org-scoped codes (it reads rights only from the token), resource resolution for M3–M6, the realm role inventory and the dictionary-read path for new users.
3. B-4: constraint 5's "admin API with a service account" breaks two accepted ADR-0023 guards (no production service account, no `/admin/realms` calls from shipping code). mod-002 therefore owes an ADR-0023 change or a Keycloak extension.
