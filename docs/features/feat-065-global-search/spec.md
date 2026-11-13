# Spec: Global search palette (feat-065)

## Context and goal

At doc-016 gate-2 (2026-11-12) the human asked for one place to jump to anything by typing: a palette opened with Ctrl+K (Cmd+K on macOS) or a magnifier button at the right end of the header, which finds departments, teams, projects, sub-areas, users and work items, shows the user's recent items when the field is empty, and is fully usable by keyboard. Everything it finds is inside the user's sight (doc-016 BR-TD-12..-17, delivered by feat-041; ADR-0040): a hidden object is not found, not counted, not hinted at. Today the panel can only filter one project's list by keyword (feat-003) and, after doc-016, jump between departments, teams and projects from the context panel (BR-TD-21); nothing finds a work item or a person from anywhere. This analysis decides what is searched and how it is ranked, how sight is enforced without disclosure, what "recent" means and where it lives, what choosing a row does, and the delivery lines. Implementation waits for feat-041; the design stage follows this one. Rule ids: BR-GS-n are this spec's; BR-TD-n are doc-016's; BR-WS-n are feat-003's.

## Roles

Organization roles «Администратор», «Участник», «Читатель» (ADR-0027) and the scoped grants (ADR-0038). The palette adds no permission code: each group is read under the gate its object already has, and sight (BR-TD-12..-15) comes first.

| Role | Finds |
|---|---|
| Any active member | the departments, teams, projects and sub-areas they see (BR-TD-12..-15); the work items of the projects they see, under `workitems.workitem.read`; the organization's members, under `organizations.membership.read` |
| Organization «Администратор» | everything of the organization (BR-TD-12), deleted and deactivated objects excepted |
| Not a member (an instance administrator included) | nothing: «Организация не найдена.» for the palette's organization (BR-GS-4) |

Common cast of every story: organization «Ромашка» (Ann «Администратор»; Max, Olga «Участник»; Vera «Читатель»); departments «Логистика» and «Финансы»; teams MARS and VENUS in «Логистика», JUPITER in «Финансы»; projects «Альфа» (key ALP) and «Браво» (BRV) in «Логистика», «Чарли» (CHR) and «Дельта» (DLT) in «Финансы»; links MARS–ALP, MARS–BRV, VENUS–ALP, JUPITER–CHR, JUPITER–DLT; Olga is a member of VENUS and holds no grant, so she sees ALP, VENUS, MARS (linked to ALP) and «Логистика»; Max sees every project («Видим всей организации» on). Items: ALP-US-7 «Оплата картой», BRV-BUG-3 «Оплата картой», CHR-TASK-5 «Оплата счёта». Organization «Другая» (Olga «Участник»): department «Склад», team ORION, project «Зет» (ZED), item ZED-US-1 «Оплата картой».

## Stories

| id | Title | depends-on | File |
|----|-------|------------|------|
| US-1 | The palette opens from Ctrl+K or the header button and works by keyboard | - | `stories/US-1-open-palette-keyboard.md` |
| US-2 | Typing finds departments, teams, projects, sub-areas and users, grouped and ranked | US-1 | `stories/US-2-find-structure-and-users.md` |
| US-3 | Typing finds work items by identifier or title | US-1 | `stories/US-3-find-work-items.md` |
| US-4 | Only what the user sees is found, and nothing reveals what is hidden | US-2, US-3 | `stories/US-4-sight-no-disclosure.md` |
| US-5 | Choosing a result opens it and sets the work context | US-2, US-3 | `stories/US-5-choose-result.md` |
| US-6 | An empty field shows the user's recent items | US-5 | `stories/US-6-recent-items.md` |

No story is `shippable` alone: the palette is one capability delivered by the lines in § Decomposition. A docs/design cut: no story is delivered by this task.

## Business process (BPMN)

```mermaid
flowchart TD
    O([Ctrl/Cmd+K or the magnifier]) --> M{Signed in, an organization in scope?}
    M -->|no organization at all| M0[Button hidden, shortcut does nothing]
    M -->|yes| P[Palette opens, focus in the field, scope chip shows the organization]
    P --> F{Field empty?}
    F -->|yes| R[Recent items: ids from the browser, resolved by the server under sight]
    R --> R1{Anything left after sight and deletion?}
    R1 -->|no| R2[Hint: nothing opened yet]
    R1 -->|yes| L
    F -->|no| Q{At least 2 characters?}
    Q -->|no| Q0[Hint: type at least 2 characters]
    Q -->|yes| D[Debounce 200 ms, stale request cancelled]
    D --> S[One search read in the organization: sight sets first, then match, rank, limit]
    S --> E{Error or 429?}
    E -->|yes| E0[Message with Retry; palette stays open]
    E -->|no| G{Any visible match?}
    G -->|no| G0[«Ничего не найдено»]
    G -->|yes| L[Groups ordered by best match; first row active]
    L --> K[Arrows move, Enter chooses, Esc closes]
    K --> C{Row type}
    C -->|department, team, project, sub-area| C1[Context chosen as BR-TD-9, parents follow, lower levels reset with the usual notice; its page opens]
    C -->|work item| C2[Its card opens; project and department chosen]
    C -->|user| C3[Organization list of work items by assignee opens]
    C1 --> Z[Recent list updated; palette closes]
    C2 --> Z
    C3 --> Z
```

## Business rules

| # | Rule | Source |
|---|------|--------|
| BR-GS-1 | **Opening.** Ctrl+K (Cmd+K on macOS) anywhere in the signed-in shell, and the magnifier button at the right end of the header, open one centred modal dialog (`role="dialog"`, labelled «Поиск»); the shortcut toggles it and cancels the browser's own binding. The button's tooltip and `aria-label` read «Поиск (Ctrl+K)», «Поиск (⌘K)» on macOS. Focus lands in the field; on close it returns to the opener. A user with no organization has neither the button nor the shortcut. | the human, doc-016 gate-2 |
| BR-GS-2 | **Scope: the current organization.** The palette searches one organization: the one the address carries; with none in the address (Главная, «Все организации»), the remembered one (ADR-0032 § 6), else the user's first by name. A chip in the palette names it and lets the user pick another of their organizations for the palette only; that changes no header selector. Nothing is searched across organizations (out of scope). A result never mixes organizations. | the human, 2026-11-13 (current organization); ADR-0032 §§ 5-6 |
| BR-GS-3 | **What is searched, and in which fields.** Work items: the identifier and the title (BR-GS-7). Projects: name and key. Departments, teams, sub-areas: name. Users: display name and email. Not searched: a work item's «Описание» (sanitized HTML, ADR-0039), comments, any other field. Matching ignores letter case and treats «ё» as «е» (feat-003 BR-WS-9); every character is literal («%», «_», «\» find only themselves); words are not reduced to stems and a wrong keyboard layout is not corrected. | analysis default (OQ-1, OQ-3); feat-003 BR-WS-9 |
| BR-GS-4 | **Sight first (BR-TD-17).** The palette is a surface of BR-TD-17: every read joins the visible departments, teams, projects and sub-areas (BR-TD-12..-15) and, for work items, the visible projects under `workitems.workitem.read`, before matching, ranking and limiting. A non-member of the palette's organization gets «Организация не найдена.»; a project, team or department the user does not see is found nowhere, in any group, on any query. | doc-016 BR-TD-12..-17; ADR-0040 § 4 |
| BR-GS-5 | **No disclosure of existence or counts.** No group shows a total; there is no «N скрытых», no empty group heading for a hidden match, and a result list never differs in length or order because of a hidden row. "More" (BR-GS-9) is known only from the visible rows (the read asks for one row past the limit after sight is applied). Response time must not reveal hidden matches beyond the noise of the visible work (not tested to the microsecond; no early exit on a hidden hit). | BR-TD-17; doc-012 BR-MX-21 |
| BR-GS-6 | **Not found: deleted and inactive.** A work item in the Removed state (ADR-0030), an item of a deactivated project, a deactivated or deleted project, team or department (ADR-0026) are not found. The palette is a new named entry point of the recycle bin's hiding (ADR-0030 B2): the entry-point test lists it. | ADR-0030; ADR-0026 |
| BR-GS-7 | **Work item matching.** An item matches when (a) the query is its full identifier (`MSK-BUG-42`) or the project key and the number (`MSK-42`; the number is unique in the project across types, so this names one item); (b) the query is a prefix of its identifier from its first character (`MSK-BUG-4`, `msk-42`); (c) its title contains the query as one piece of text. A bare number does not match by number (needs the key); it matches titles. Several words are one piece of text, not an AND of words. | analysis default; feat-003 BR-WS-9 widened for type-ahead |
| BR-GS-8 | **Ranking.** Within a group, rows are ordered by match tier, then a tie-break: (1) exact identifier or exact name or key; (2) identifier, name or key starting with the query; (3) a word of the name or title starting with the query; (4) contains. Tie-break: work items by latest change then number descending; projects, teams, departments, sub-areas and users by name (the Russian collation of the lists), then id. Groups appear in the order of their best row's tier, equal tiers in the fixed order «Рабочие элементы», «Проекты», «Команды», «Департаменты», «Области», «Пользователи»; the first row of the first group is active. | analysis default |
| BR-GS-9 | **Limits and "more".** At most 5 rows a group on the first read. A group with more visible matches ends with the row «Показать ещё», which expands that group in place to at most 20 rows (one more read, the same query); there is no further paging. The work items group also ends, when it has any row, with «Все рабочие элементы по запросу», which opens the organization's «Рабочие элементы» list with the query as its keyword (feat-003); sight on that list is feat-062's. | analysis default |
| BR-GS-10 | **Query rules.** Trimmed; at least 2 characters (shorter: «Введите не менее 2 символов»); at most 100 (the field stops there); 200 ms debounce after the last keystroke; a read still in flight when the text changes is cancelled and its answer is ignored; no read runs while an IME composition is open. The query text is never written to the server log or to any record (only its length and the timing). Contrast: the context panel's own field searches from the first character (BR-TD-21). | analysis default |
| BR-GS-11 | **Rows.** A row shows its type icon (the tree's, ADR-0024 tokens), the primary text with the matched part emphasised, and a grey secondary line: work item — identifier, title, state, project name; project — key, department; team — department; department — nothing; sub-area — project, department; user — email. A department, team and project row show the object's colour (BR-TD-20) as the context panel does. | doc-016 BR-TD-20, -21 |
| BR-GS-12 | **Choosing (see US-5).** A department, team, project or sub-area is chosen in the context as BR-TD-9 and -10 say (organization and department follow; levels below reset to «все» with «Выбор сброшен: …») and its own page opens (the bare object address, ADR-0032 § 1). A work item opens its card under its project, the project and its department being chosen, the team untouched. A user opens the organization's «Рабочие элементы» filtered by that person as «Исполнитель» (feat-003); there is no user page. Every row is a real link: a middle click or Ctrl/Cmd+Enter opens it in a new tab, which sets the context from its address. | analysis default (OQ-6); ADR-0032 §§ 1, 5; BR-TD-9 |
| BR-GS-13 | **Recent items (see US-6).** "Recent" is what the user opened — an object's own page or a work item's card, from the palette or by any other route — not what they typed. At most 10, newest first, one entry per object, per user and per organization, kept in the browser's `localStorage` as type, id and organization only, no names (`mustask.recent.v1.<sub>.<organizationId>`, like ADR-0032 § 6's key). The empty field sends the ids to one read that returns only the objects the user still sees today, with their current names; an object that is gone, deleted, deactivated or no longer visible is omitted without a reason and its id is dropped from storage on the next write. | analysis default (OQ-4, OQ-5); ADR-0032 § 6; ADR-0030 |
| BR-GS-14 | **Differences from the neighbours; no duplication.** The context panel's field (BR-TD-21) selects a department, team or project inside the selectors and stays; the «Структура» filter (BR-TD-21, feat-061) hides rows on a page and never navigates; feat-003's keyword filters one project's list. The palette finds across types and navigates. It shares their sight sets (BR-TD-12..-15) and their name match for structure objects (BR-GS-3); it does not replace any of them. feat-062 owns sight on the multi-project views' keyword filters and counts; the palette needs none of that line. | doc-016 BR-TD-21, -22; feat-061; feat-062 |
| BR-GS-15 | **Failure.** An error or a 429 (`http.too_many_requests`, ADR-0036's limiter wording) shows «Не удалось выполнить поиск. Попробуйте ещё раз.» with «Повторить» (a 429 is retried by `Retry-After`, up to 3 tries); the field keeps its text and the palette stays open. Offline is the same message. | ADR-0036; ADR-0007 |
| BR-GS-16 | **Accessibility.** The field is a combobox over a listbox with `aria-activedescendant`; groups are labelled; a polite live region says the number of rows shown («Найдено: 7») or the empty text, never a hidden total; the dialog traps focus; Esc closes at any time; it works from 640 CSS px (full width below 600) and the active row is visible without a mouse. | ADR-0024; doc-016 § 9 |

## Operation completeness

| Entity | Create | Read | Update | Delete/deactivate | Cancel/reverse |
|--------|--------|------|--------|-------------------|----------------|
| Search query (typed text) | the user, in the field (US-2, US-3) | the user, as results; never stored (BR-GS-10) | retype | clear the field (shows recent) | Esc closes, text discarded |
| Result row (department, team, project, sub-area, user, work item) | no: derived from objects | any member, within sight (US-4) | no | no: leaves the results when the object leaves sight, is deleted or deactivated (BR-GS-6) | no |
| Recent entry | by opening an object (US-6) | its user, only while the object is still visible | no: reorders on re-open | no: aged out beyond 10; dropped when unresolved; «Очистить недавние» clears the list (US-6) | no |
| Palette scope (organization chip) | no: defaults per BR-GS-2 | the user | the user picks another of their organizations | no | pick again |

## Non-functional requirements

- **Volumes.** One organization at doc-012's volumes: 100 000 work items, 1 000 areas, 200 projects, 200 teams, 2 000 members; a user seeing everything or a tenth of it. The server read has p95 ≤ 300 ms and p99 ≤ 800 ms at these volumes with the sight sets applied, and the result is on screen within 600 ms of the last keystroke at p95 (debounce included), measured by a volume test. A non-indexable pattern (a 2-character query) stays within the same targets.
- **Query shape.** A fixed number of statements per read, independent of rows, groups and the sight sets' sizes (sight sets read once, then at most one statement per group); a statement-count test pins it. Recent resolution is one statement per type, at most one request for ten ids.
- **Load.** A search is a hot, frequent read: it has its own per-user concurrency limit and a per-process one, refused with 429 and `Retry-After` as ADR-0036 does for group reads; the numbers are the design's. The debounce and the cancel of stale requests are required, since they are what keeps the load near one read per pause.
- **Non-disclosure.** BR-GS-4/-5: a test per object kind holds a hidden object out of the results, the "more" row, the live region and the recent list; the response bodies for a hidden object and for a name that exists nowhere are identical.
- **Privacy.** The query text is not logged (BR-GS-10). Email is matched and shown only for a holder of `organizations.membership.read`, the same people the «Участники» page already shows it to.
- **Tenant.** Every read is scoped to the organization of the route; an id of another organization in the recent list resolves to nothing.
- **Localization.** Interface copy is Russian, quoted in this spec and the stories; new error text (if any) goes to the catalogue at the backend stage.

## Out of scope

- Searching across all of the user's organizations at once (the palette follows one organization, BR-GS-2); a cross-organization mode is a later line.
- Searching a work item's «Описание», comments, tags, attachments, custom fields; stemmed word forms; typo or wrong-layout tolerance; synonyms.
- Commands in the palette («Создать…», «Перейти в настройки»), saved searches, filters inside the palette, search history of typed queries.
- Searching deleted items, the recycle bin, deactivated objects (BR-GS-6).
- A user profile page (none exists); the organization's «Участники» page gains nothing.
- Sight on the multi-project views, counts and the keyword filter on lists (feat-061, feat-062); the palette does not change them.
- Server-side recent items and recent items across devices (OQ-4).
- Full-text ranking by relevance score (BM25); a separate search engine (OpenSearch, etc.).

## Decomposition for the backlog

Proposals for the human; the orchestrator queues them with numbers after approval. Priority in the order given. The queue line `feat-065-global-search` (size:M, one line) is replaced by these four; ADR owed with the first.

| id | Task | size | deps |
|----|------|------|------|
| `feat-NNN-search-structure-users-read` | US-2 and US-4's read side for departments, teams, projects, sub-areas and users: the one search read in an organization (sight sets joined first, groups, ranking, limit+1 "more", expansion of one group), the trigram indexes on names and keys, the per-user and per-process limiter, the ADR for the match technique and the limiter accepted at its gate, error catalogue entries, hidden-object tests per kind, statement-count and volume tests | M | feat-041-project-visibility |
| `feat-NNN-search-work-items-read` | US-3 and US-4's read side for work items: identifier grammar (BR-GS-7), the title index, joining visible projects and the read gate, the Removed/deactivated hiding and its entry-point test entry, the group in the same read and its expansion, volume test at 100 000 items | M | `feat-NNN-search-structure-users-read` |
| `feat-NNN-search-palette` | US-1, US-2, US-3, US-5 in the panel: Ctrl/Cmd+K and the header magnifier, the dialog with its combobox, debounce and cancel, groups and rows, keyboard, scope chip, choosing (context per BR-TD-9, -10, card, the assignee-filtered list), states and failures | M | `feat-NNN-search-work-items-read`, `feat-NNN-cascading-context-selectors` |
| `feat-NNN-search-recent` | US-6: the recent list (route-level recording of opened objects, the browser key), the resolving read under sight, the empty state, «Очистить недавние» | S | `feat-NNN-search-palette` |

`feat-NNN-cascading-context-selectors` (doc-016) is the line whose choice rules (BR-TD-9, -10, -18) the palette reuses; it is required because choosing must not re-implement them. If the human prefers an earlier first release, `feat-NNN-search-palette` can ship with structure and users only (dropping the dependency on the work items read) and the work items group follows.

## Open questions

Each carries a recommendation and the analysis goes on with it.

- [ ] **OQ-1 Is a work item's «Описание» searched?** (A, recommended) No: title and identifier only (BR-GS-3). The description is sanitized HTML (ADR-0039), so searching it needs a stored plain-text copy, a second index and a rule for reindexing on edit; titles and identifiers cover "find that item". Downside: an item known only by a phrase in its description is not found; the project list's keyword has the same limit (feat-003). (B) Yes, through a derived plain-text column with its own trigram or full-text index: finds more, at the cost of a new column, an update path in every description write, and a larger index on the table that grows fastest. (C) Later: ship A now and queue B as its own line once usage shows the need.
- [ ] **OQ-2 The match technique: which database features.** (A, recommended) `pg_trgm` GIN indexes on the folded (lower, «ё»→«е») title, names, keys and display names, with a substring match: fits type-ahead on partial words and identifiers, mixes Russian and English without a language configuration, matches feat-003's no-stemming semantics. Needs `CREATE EXTENSION pg_trgm` in every database (a one-line additive migration; the company-wide Keycloak realm is not involved, but the DB host must allow it). (B) `tsvector` with Russian and English configurations: ranks by word and handles word forms, but breaks on partial words typed so far, on identifiers (`MSK-BUG-42`) and on mixed text, and needs a language column per item. (C) A plain scan with `LIKE` and no extension: nothing to install, acceptable for names (a few thousand rows), but 100 000 titles per organization scan on every keystroke and break the p95 target as organizations grow. Both A and B depart from ADR-0015's "full-text search … a new ADR" note; the ADR is owed by the first line (not drafted here, the number is issued when a line takes it; an accepted ADR needs the human's `accepted`).
- [ ] **OQ-3 Are users found by email as well as by name?** (A, recommended) Name and email: people type either, and the «Участники» page already shows the email to the same readers. (B) Name only: nothing about email is added to a surface; a user known by email is not found.
- [ ] **OQ-4 Where recent items live.** (A, recommended) The browser's `localStorage`, ids only, per user and organization (BR-GS-13): no table, no migration, the server stays authoritative for names and sight; recents do not follow the user to another browser. (B) On the server, per user and organization: follows the user across devices, at the cost of a table, a write on every opening (a write on a read path) and a retention rule. ADR-0032 § 6 took the same trade for the remembered selection.
- [ ] **OQ-5 What counts as "opened".** (A, recommended) Any display of an object's own page or a work item's card, whether reached from the palette or by a board, link or address (BR-GS-13), recorded by one route-level hook. (B) Only what was chosen in the palette: far simpler, but the empty field is mostly empty for a user who works from boards. 
- [ ] **OQ-6 What choosing a user does.** (A, recommended) Opens the organization's «Рабочие элементы» filtered by that person as «Исполнитель» (BR-GS-12): it needs no new page and answers the usual "what is X working on". (B) Opens «Участники» scrolled to the person: needs the members page to take a person in its address and highlight a row, a change to another line's page. (C) No users group in this cut: removes the question, loses "find a colleague".
