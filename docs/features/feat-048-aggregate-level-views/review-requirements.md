# Requirements review: feat-048-aggregate-level-views

## Round 1 — requirements

Scope: a delta review. F43's approved text (BR-NV-*, US-6, US-10, Q-7..Q-13) is not reopened; only the index's faithfulness, the Delta OQ-1..OQ-5 and anything new for gate-1.

**Verdict: `needs-human-decision`.** OQ-1..OQ-4 stay the human's, and R-1 adds one more question. R-2..R-5 are spec edits for the analyst, made before gate-1.

Counts: blocker 0, major 5, minor 6.

### R-1
Severity: major
Affects: index § Rules carried (BR-NV-20), § Out of scope; US-10 "Filters change the counts", budget scenario
Problem: the index puts "the views' filters (feat-013)" out of scope, and feat-013 is not merged (`docs/backlog.md`: open, deps feat-042). The approved text still has filter clauses: BR-NV-20 "*Filters* (feat-013's) apply to every group", the «3 из 40» headers, no move "while a filter … narrows", US-10's keyword scenario, and the NFR budget "with a team, assignee or state filter". The index does not say which filters the aggregate views have in this line: none (only the selectors narrow), or the keyword, or an API that takes feat-003's parameters with no UI yet. Without that, the US-10 filter scenarios and the assignee/state budgets cannot be tested, and the grouped read's parameters are left undefined.
Required resolution: add a Delta question (OQ-6) or an index statement. It names the filter set of the aggregate views in this line and says whether the grouped, group and cell reads accept feat-003's filter parameters now. It lists which US-10 clauses this line tests through the selectors only and which wait for feat-013.
Status: open
Resolution:

### R-2
Severity: major
Affects: index § Rules carried, row "BR-NV-17, BR-NV-13"
Problem: the row reads only "Narrowing by the selectors and the Q-9 no-reorder hint". It does not list the BR-NV-17 clauses marked "from the second line". One of them changes an existing view: a team chosen with a department narrows the team's own feat-040 «Бэклог»/«Канбан» to the projects placed in that department. US-10 ("A chosen team narrows…") then shows them as one group per project, A «1 из 3», B «2 из 2», with no reorder. A developer reading the index or the Summary ("organization and department level") would build only the org/department aggregates and miss the change to the team view.
Required resolution: list each owned BR-NV-17 clause in the row. These are the department and organization content of the three sections; team + department narrowing the team's views (grouped per project, no reorder, the «сужено: …» line); and the team-level «Рабочие элементы» across its projects.
Status: open
Resolution:

### R-3
Severity: major
Affects: index § Non-functional requirements; OQ-3
Problem: "must not make a single user with two tabs see refusals in ordinary use" cannot be tested, because "ordinary use" is undefined. The test of OQ-3 A depends on this sentence.
Required resolution: make it concrete. For example: "one user with two tabs, each restoring 10 groups (8 group reads in flight), receives no 429; a ninth concurrent read from that user may be refused". Then a limiter test can assert it.
Status: open
Resolution:

### R-4
Severity: major
Affects: OQ-1, OQ-3 (design § 12 limiter)
Problem: the options are incomplete in two ways. (a) None of them considers a short server-side wait queue. A limiter can hold a read for a bounded time before refusing it (the ASP.NET concurrency limiter has a queue limit). Two tabs would then work at a per-user limit of 4–6, and a 429 would stay an abnormal event, without raising the per-user number that OQ-3 A raises. (b) "Per user" has no defined key. It could be the Keycloak subject across every organization and device, or the session or token. The scope is also open: per API process or across replicas, given that the pool it protects is per process. Each choice changes what a test asserts.
Required resolution: add the queue option to OQ-3 (or OQ-1), and state the limiter's key and scope in the Delta: subject or session, and per process or shared. Both can be marked "the design's number" as long as the behaviour is fixed.
Status: open
Resolution:

### R-5
Severity: major
Affects: BR-NV-20 "A move inside a group", "Paging inside a group"; design § 12 keyboard path; Delta (missing)
Problem: the move is defined against a project's whole backlog, but a group holds only the pages loaded so far. The shipped move menu (`boards/lib/reorder.ts`: «В начало», «Выше», «Ниже», «В конец») and the drag target have no rule for a partly loaded group. Does «В конец» mean the end of the project's backlog (the row then leaves the held set and returns on a later page) or the end of the loaded rows? Is «Ниже» on the last loaded row disabled while more rows exist? A drop below the last loaded row is not defined either. A user's own move across the cursor could also show the row twice or lose it, and the "drop by id" rule covers only someone else's moves.
Required resolution: add a Delta question with options, for example: (A) «В конец» means the project's end, the row leaves the loaded set and appears once later; «Ниже» on the last loaded row is disabled until «Показать ещё»; a drop lands only between loaded rows. (B) The moves are limited to the loaded rows, and «В конец» is absent while more rows exist. Each option needs a scenario.
Status: open
Resolution:

### R-6
Severity: minor
Affects: OQ-5
Problem: the claim matches the code. `TeamFilter` is an `InlineMultiSelectField` labelled «Команда» over the project's teams, then «Без команды» (`WorkItemsPage.tsx:214`), and its first value that is not `none` already acts as the shell's team selector (`useTeamFilter.ts`). But the question is already answered: BR-NV-12, Q-6 A and US-6 ("«Без команды» … is exclusive with a chosen team") fix option A, and B contradicts the approved text. OQ-5 also leaves out that the same multi-select is on the project's backlog and board (`boards/components/ViewFilterRow.tsx:50`). BR-NV-12 is about the list only, so those keep it until feat-013, and a project's list and its backlog would then differ. Nor does OQ-5 define what "exclusive" does. When «Без команды» is chosen with a team selected, the team selector could reset to «все» with the «Выбор сброшен» notice, or the toggle could be disabled.
Required resolution: turn OQ-5 into an index statement of the approved rule rather than a human question. Name what "exclusive" does, and state that the project's backlog and board keep the shipped «Команда» until feat-013.
Status: open
Resolution:

### R-7
Severity: minor
Affects: OQ-1
Problem: (a) `docs/error-codes.md` has no 429 code, so a new catalogue entry is owed (Russian and Kazakh text in `contracts/errors/*.json`), and it is not mentioned. (b) The message «Не удалось загрузить проекты группы» is wrong, because a group is one project. Something like «Не удалось загрузить элементы проекта. Сервис занят, попробуйте ещё раз.» fits. (c) Option A does not say whether a group waiting to retry holds one of the tab's 4 slots. If it does, other groups block behind its backoff.
Required resolution: name the catalogue entry, fix the copy, and state that a waiting retry releases its slot.
Status: open
Resolution:

### R-8
Severity: minor
Affects: OQ-2
Problem: option A leaves the cross-project list outside the limiter as "a single read". Its keyword contains-match is budgeted at up to 5 s, which makes it the longest-held connection in this line, so the global limit's pool argument applies to it most.
Required resolution: record it in OQ-2: either the list's keyword read is accepted outside the limiter (accepted risk), or it falls under the global limit only.
Status: open
Resolution:

### R-9
Severity: minor
Affects: US-10 "A deactivated project leaves and returns"; OQ-4
Problem: US-10 covers deactivation "at the next read" only. A project can be deactivated while its group or lane is open. «Показать ещё», a row move or a card move then answers «Проект не найден», and the text does not say what the panel shows (drop the group and re-read the headers, or an inline message).
Required resolution: add the case to OQ-4's recovery: for example, the group is removed and the headers are re-read, with or without a notice.
Status: open
Resolution:

### R-10
Severity: minor
Affects: OQ-4 A
Problem: OQ-4 A's automatic reload needs the panel to tell a cursor 400 apart from any other 400 of the same read, such as a bad level or a bad filter. "The existing validation error" does not say how.
Required resolution: state that the validation error names the field (`cursor`), so the recovery can be tested and does not fire on other 400s.
Status: open
Resolution:

### R-11
Severity: minor
Affects: index § Rules carried, § Dependencies, "Design input"
Problem: these are index details. "Resolved" omits Q-6 A and H-1 A, which BR-NV-12 and OQ-5 rely on. "Design input already set" lists layout and moves only, not the owed limiter and cursor ADR of design § 12 (they appear only in the Delta and the Log). § Dependencies says five dependencies "were not re-verified", but status.md's Log records them as checked merged. F43 `spec.md` still shows Q-12 and Q-13 unticked; the index rightly cites `status.md` § Decisions for them, and it could say so.
Required resolution: add Q-6 A and H-1 A, add the § 12 owed items, align the Dependencies sentence with the Log, and note that Q-12 A and Q-13 A are taken from F43 `status.md`.
Status: open
Resolution:

### Faithfulness check (no finding)

BR-NV-12, BR-NV-20, BR-NV-1/-2/-3 (addresses and menu rows), BR-NV-15, BR-NV-13, the two NFR budgets and the out-of-scope items are pointed at, not reworded. Nothing in the index contradicts F43. Two Delta items add text of their own: the copy in OQ-1 and the NFR sentence (R-3, R-7).
