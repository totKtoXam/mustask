# QA: feat-027-backlog-tree-editing

By D-1 (the short route) there are no QA sessions for this task: the
developers' tests, `scripts/verify.sh` and the E2E run below stand in for
them. This section records commands and facts, not a test plan, so it has no
coverage matrix and no verdict. The scope is `spec.md` § E2E, `design.md` and
the stories; the file under test is `26-backlog-tree-editing.spec.ts` (new,
26.1..26.6, never run before this run); files 23, 24 and 25 are the ones
aligned with the reference backlog (23.1, 23.3, 24.3..24.5, 25.6).

## Frontend — E2E results, 2026-10-11

A task-executor run on branch tip `b0a90d9` (the orchestrator's last commit;
nothing in the product changed during the run — the only commit made by the run
is the spec fix below, `061ec4b`), worktree of its own, database
`mustask_feat027` on the external PostgreSQL (port 5432), with the stand lock
taken before the build and released after the stop. Times are UTC. The stand was on **panel 3100,
API 5100, Keycloak 5010** (AppHost's user secrets), the Aspire dashboard on
17125; 3000 and 5000 belong to the human's other project and were not touched.
The suite ran with `E2E_PANEL_URL=http://localhost:3100`,
`E2E_API_URL=http://localhost:5100` and `E2E_ALLOW_DOCKER=1` (specs 5 and 6
stop and start the stand's own Keycloak container by the name in the stand
file; as in the earlier runs).

| Step | Command | Result |
|------|---------|--------|
| 1 | `bash scripts/warm-worktree.sh` | node_modules copied from the cache, no `npm ci` |
| 2 | `dotnet build src/backend/Mustask.AppHost` | 0 warnings, 0 errors |
| 3 | helper `reset` (`bash scripts/stand-reset.sh --yes`, `ConnectionStrings__mustask` naming `mustask_feat027`) | exit 0. Database "not present", created empty; Keycloak store `mustask-keycloak-dbe9dedd12b91b69` "not present", so the pair started from nothing. The reset also printed that it could not delete the stale E2E stand file (`DirectoryNotFoundException`: the `.e2e-stand` folder did not exist yet), which is harmless on a first start |
| 4 | helper `migrate` (`dotnet ef database update --project src/backend/Mustask.Infrastructure`) | exit 0: the eight migrations, `InitialCreate` through `AddWorkItemFormFields` (20261009055512), applied |
| 5 | helper `start` (`dotnet run --project src/backend/Mustask.AppHost`, default launch profile, detached), a minute or so before 11:27:33 | log: "Database: external PostgreSQL", "Keycloak store: the live realm matches the realm import", `mustask-e2e` and `e2e-fixture` provisioned. The API (`/health`) and the panel answered 200; the listeners were 3100 (panel), 5100 (API) and 5010 (Keycloak) |
| 6 | helper `seed` (`bash scripts/keycloak-seed.sh`, the three passwords from AppHost's user secrets), 11:27:33–11:27:46 | exit 0: `mustask` → «Администраторы», `member` → «Сотрудники / Участники», both created |
| 7 | `cd src/frontend && E2E_ALLOW_DOCKER=1 npx playwright test` (env as above), 11:27:50–11:38:27 | 69 tests in 25 files, **67 passed, 2 failed (25.1, 26.6), 0 skipped, 0 flaky**, 10.5 min |
| 8 | `npx playwright test e2e/specs/25-work-item-fields.spec.ts` after the spec fix (rerun cycle 1 of 2) | **6 passed**, 57 s |
| 9 | a throwaway copy of file 26 with the separators assertion of 26.6 made soft (`expect.soft`), run with `-g "26.6"` (which, as written, matches every case of the file), then deleted | 26.1..26.5 passed again; 26.6 failed on that one assertion and on nothing else — see "Defects" |
| 10 | a throwaway spec seeding a tree through the API and saving seven screenshots, run alone, then deleted | 1 passed, 17 s; seven PNGs written — see "Screenshots" |
| 11 | stand stopped: `taskkill //PID <Mustask.AppHost.exe> //T //F`, `docker rm -f mustask-e2e-keycloak-5010` | 3000, 3100, 5000, 5010, 5100 and 17125 free; no `Mustask.*`, `dcp` or Vite `node` process left; the Keycloak container gone; lock released. `mustask_feat027` and the Keycloak volume left as they are; `postgres-5432` untouched and running |

**How the secrets reached the run.** As in feat-026's runs: AppHost's user
secrets were read with exactly `dotnet user-secrets list --project
src/backend/Mustask.AppHost`, inside a command substitution of a small helper
script kept outside the repository (the session's scratchpad), run as `cd
<worktree> && bash <helper> reset|migrate|start|seed`. The helper holds no value
and derives `ConnectionStrings__mustask` (the user secret's connection string
with `Database=mustask` replaced by `Database=mustask_feat027`) and the three
passwords into its own process environment; nothing was printed or written to a
file. The permission layer accepted the read.

**Totals (first full run):** 69 tests in 25 files, 67 passed, 2 failed, 0
skipped. Tests per file: one each in 01–11, 13, 14, 16, 17 and 18; 3 in 15; 13
in 19; 6 in 20; 8 in 21; 3 in 22; 3 in 23; 5 in 24; **6 in 25**; **6 in 26**
(the new file). Against feat-026's run (57 tests in 23 files) the difference is
files 25 and 26 (+6 and +6). Specs 5 and 6 ran because Docker control was
allowed.

**Totals (after the one rerun cycle):** 68 passed, 1 failed, 0 skipped. The
failure is 26.6, a product defect (P-1 below); no rerun can change it.

| File | Test | Result |
|---|---|---|
| 26 (new) | 26.1 «Эпики»: the head, the eight columns in their order and none of the removed ones, «Порядок» on top-level rows only, the values and blanks per type, the title cell, the state dot; «Канбан» has the same head (US-1) | passed, 13.0 s |
| 26 (new) | 26.2 «Эпики» opens expanded as far as the data go; «Свернуть на один уровень» three times, «Развернуть на один уровень» three times, no request; «Истории»: a bug beneath its story only (US-2, US-3) | passed, 6.7 s |
| 26 (new) | 26.3 «Добавить: Фича» on an epic, Escape, an empty Enter; «Добавить дочерний элемент» → «Добавить: Задача», over 255 characters refused, one create with the parent, the task last, selected, in the story's area (US-4) | passed, 7.8 s |
| 26 (new) | 26.4 a story dragged by its grip onto a feature; a refused drop with its notice (US-5) | passed, 6.6 s |
| 26 (new) | 26.5 «Ещё действия» on every row, a top-level row's four moves, a nested bug given a parent by keyboard (US-6) | passed, 7.5 s |
| 26 (new) | 26.6 the look: Segoe UI, 36 px rows, the header with its separators, hover, selection, keyboard focus, the sticky header, 1 440 and 1 280 px | **failed**, 5.6 s — P-1 |
| 23 (aligned) | 23.1 happy path | passed, 11.2 s |
| 23 (aligned) | 23.3 switcher on a team-level page | passed, 3.5 s |
| 24 (aligned) | 24.3 «Эпики»: expand and collapse, a reorder keeps the children | passed, 6.9 s |
| 24 (aligned) | 24.4 «Истории»: «Изменить родителя…», «Убрать родителя» | passed, 6.4 s |
| 24 (aligned) | 24.5 the level drop-down by keyboard | passed, 5.2 s |
| 25 (aligned) | 25.6 «Приоритет» on the kanban card and on no backlog column | passed, 8.6 s |
| 25 | 25.1 «Эпик»: two columns at 1 440 px, one at 1 279 px, none sideways at 640 px | **failed** in the full run (8.9 s, a timing defect of the spec), **passed** after the fix (rerun of the file) |

The other tests of 23 (23.2), 24 (24.1, 24.2) and 25 (25.2..25.5), and every
test of files 01–22, passed in the full run (none of them changed).

**Stand record.** The run printed `e2e global setup: warm stand —
http://127.0.0.1:5100/health was green at the first check`. **The run was
warm.** The stand was brought up from nothing (step 3), but `Mustask.Api` had
already reached the realm before the suite's first check (the seed ran in
between: the stand answered at 11:27:33, the suite began at 11:27:50), so this
run did not exercise the cold-start window of 05-frontend § 7. The brief asked
for one run; whether to repeat it on a stand whose first check is red is the
orchestrator's call.

### Failures and their classification

**1. 25.1 — spec defect (timing); fixed in `061ec4b`, rerun green.** At 1 279
px the case read `Описание` and `Планирование` right after
`page.setViewportSize` and asserted they stand on one left edge
(`Math.abs(below.x - folded.x) < 2`): received 27.2 px in the full run and 15.5
px in the single rerun, so it moves between runs. Cause, by reading
`AppLayout.tsx` and by a probe: the permanent navigation is expanded at 1 440 px
and a rail at 1 279 px, and its width is animated (`widthTransition`, mod-015
design § 2.6), so for a moment after the resize the page's content is still
sliding. A throwaway probe read the two sections' `x` every 40 ms after the
resize (deleted): 81.9 and 73.3 at the first sample, 72.0 and 72.0 from the
second on (the drawer's own width was not measured). The failure screenshot,
taken a moment later, shows both sections at the same edge. The shell is not changed on this branch (no diff in
`src/frontend/src/app/AppLayout.tsx` or `app/shell`), so this is not a feat-027
regression; why the case passed in feat-022's two runs (16.5 s, 27.8 s) is not
established — most likely the read happened to land after the animation. The fix
polls (`expect.poll`) until the two sections' left edges agree before reading the
boxes, and does the same for the 640 px "no sideways scroll" read below it,
which would race the same way and never ran because the case stopped at the
first one. Rerun of file 25: 6 passed.

**2. 26.6 — product defect P-1 (below), not fixable by the spec.** It failed at
the first assertion of the step "rows are compact, white and quiet; the header
is small grey labels over one line", the static header separators. Everything
the case asserts after that line was checked in a throwaway copy with that one
assertion made soft: the Segoe UI stack and the weights, the hierarchy buttons
(24 px, 4 px apart), the transparent-but-present row controls and the hover that
shows them, the «+» as orange and 24 px, the grip, the selection (light blue,
framed, controls shown), the keyboard stops and the focus ring, the sticky
header and the 1 440 / 1 280 px behaviour all passed. So there is **no colour-only
failure to put under "pending Q-1/Q-2"**: the assertions on the «+» (orange) and
on the dot colours hold at the values design.md § 2 names today; if the human
answers Q-1 or Q-2 otherwise, those lines change with the answer.

### Defects

**P-1 (major, visual) — the header separators are drawn as wide as their
cells.**

- Test: 26.6, step "rows are compact, white and quiet; the header is small
  grey labels over one line" (design § 4.2, § 4.3).
- Steps: open «Эпики» of any team at any width.
- Expected: the `::after` of the header cells «Тип», «Заголовок», «Статус»,
  «Трудоёмкость» and «Бизнес-ценность» is a thin vertical line, `1px x 20px`;
  none after the hierarchy column, «Порядок» or the last header.
- Actual: `184px x 20px`, `492px x 20px`, `104px x 20px`, `64px x 20px`,
  `64px x 20px` — the width of each cell. Each is painted in the divider colour
  above the label, so in every screenshot a grey bar covers the five labels
  (the screenshots `backlog-*.png`: only «Порядок» and «Вид ценности» can be
  read). The labels stay in the DOM, which is why 26.1's accessible-name
  assertions pass.
- Suspected cause: `BacklogHeadRow.tsx`, `headCellSx`, the `::after` rule's
  `width: theme.layout.dividerWidth` — the number 1 inside an `sx` object is a
  fraction, 100 %, not a pixel. The same value is already known from the Log of
  2026-10-11. The expected fix is one line (`${theme.layout.dividerWidth}px`);
  not made, by the brief.

No other product defect was found by the suite.

### Specs changed

- `src/frontend/e2e/specs/25-work-item-fields.spec.ts`, commit `061ec4b`
  (`test(e2e): 25.1 waits for the navigation's width animation`): 25.1's two
  layout reads at 1 279 and 640 px poll until the layout has settled. No other
  spec file was edited; file 26 is as the E2E branch committed it.

### Screenshots (for the human's side-by-side comparison with the reference screenshot)

Not an assertion: the pictures exist so that the human can lay them next to the
reference screenshot, which the run did not have. They were made by a throwaway
spec (deleted, never committed) that signed in the way file 26 does and seeded,
through the API, its own organization «Организация снимков …» with the project
«Снимки бэклога» and the team «Команда снимков» (deactivated at the end of the
spec; the rows stay in `mustask_feat027`). The tree is shaped like the reference:
epic 1 (a long title) → feature A → story 1 → a bug; feature A → two stories with
long titles (they truncate), the story «us1» in «Закрыта» with the tasks «be»,
«fe», «qa», the bug «bg1» and the task «release» all «Закрыта» and the task
«qa1» «Новая», the story «US2»; epic 1 → feature B → one story; epic 2 and its
feature, both with «Бизнес-ценность» 100. The PNGs are kept outside the
repository, in the run's session scratchpad folder `shots-027/`:

| File | Shows |
|---|---|
| `backlog-1366.png` | «Эпики» at 1366×868, the first epic row hovered: light grey, the orange «+», the grip, «⋮» |
| `backlog-selected.png` | the same, the nested bug row selected by a click: light blue, framed, grip and «⋮» shown, the pointer away |
| `backlog-add-row.png` | the inline add row open beneath story 1 after «Добавить: Задача»: the type «Задача», its icon, the field «Введите заголовок» focused, the row selected |
| `backlog-plus-menu.png` | feature A's «+» menu open: «Добавить: Пользовательская история», «Добавить: Баг» |
| `backlog-row-menu.png` | the «⋮» menu of a nested story open: «Изменить родителя…», «Убрать родителя» (the four moves are on top-level rows only) |
| `backlog-1920.png` | «Эпики» at 1920×1080, nothing hovered or selected: every row of the tree fits, including epic 2's feature with 100 |
| `kanban-head.png` | «Канбан» at 1366×868: the same head — the team's name with its drop-down, the tab «Канбан», «Эпики» at the right, «Показать бэклог» as the grey secondary button, no primary button |

What the executor saw in them, for the comparison: P-1 in every backlog picture;
at 1366×868 the card does not hold all 19 rows (it scrolls inside itself, the
sticky header stays; feature C with 100 is below the fold, and shows at 1920);
long titles end in an ellipsis; each type has an icon of its own and the state
dots are grey («Новая») and green («Закрыта»). In `kanban-head.png` a long epic title wraps into a column
about 70 px wide beside the identifier and breaks words mid-word («уведомлен /
ий:»); that is the board card's layout (mod-006 and feat-022), outside this
task's scope, noted only because it shows in the picture.

### What was not checked

- **The reference screenshot itself.** The run had no copy of it; whether the
  backlog looks "exactly like" it is the human's reading of the PNGs above, and
  the assertions of 26.6 stand in for the measurable part of design.md.
- **Design § 9, Q-1 and Q-2** (the type and «Новые» colours, the row «+»'s
  colour): the assertions pass at the values written in design.md; the human may
  still change them.
- **The 64-item depth bound** (409 `workitems.link.depth_exceeded`): no E2E case
  by the spec; the backend's integration tests only. The panel's refusal tables
  lack the code (the Log's re-check minor), so what a browser shows for it is
  not seen.
- **What the spec leaves to the component tests**: the add row's pending state
  and a double Enter, a create refused because the parent has gone (nothing can
  be deleted through the panel before mod-005), the look of the drag's allowed
  and refused frames (26.4 asserts the `data-drop-target` marks and the writes,
  not the outline), and touch.
- **The expanded set under the 2 000-drawn-rows budget** and the 5 000-item
  volumes: measured by the backend's volume tests, not in a browser (26.2 reads
  the first state on a ten-row tree).
- **Widths other than 1440×900 and 1280×900** for the backlog as assertions; 1366
  and 1920 are in the pictures only.
- **Access**: mod-002 D-a (everyone inside an active organization); no denied
  path to test before `mod-016`.
- **A cold-stand run** (above).

## Frontend — E2E run 2, 2026-10-11

A task-executor run on branch tip `56d5a3e` (the orchestrator's Log lines after
fix pass 2; the product's last change is `11fd0f0`) — after review round 1b's
two frontend fix passes (the look, V-1..V-13 and D-10; the behaviour, F-1..F-17).
A worktree of its own, database `mustask_feat027` on the external PostgreSQL
(port 5432), the stand lock `.git/stand.lock` taken before the build and
released after the stop. Times UTC, to the minute. Ports as in run 1: **panel
3100, API 5100, Keycloak 5010**, the Aspire dashboard 17125; 3000 and 5000 were
not touched (nothing was listening on them). The suite ran with
`E2E_PANEL_URL=http://localhost:3100`, `E2E_API_URL=http://localhost:5100` and
`E2E_ALLOW_DOCKER=1`. The secrets reached the run exactly as in run 1: the same
scratchpad helper, nothing printed or written.

| Step | Command | Result |
|------|---------|--------|
| 1 | `bash scripts/warm-worktree.sh` | node_modules copied from the cache |
| 2 | `dotnet build src/backend/Mustask.AppHost` | 0 errors; two warnings, both `git config core.hooksPath` of `Directory.Build.props` failing to lock `.git/config` (another worktree's build held it) — not the code's |
| 3 | helper `reset` | exit 0. This time the database existed from run 1 and was **dropped and created empty**, the Keycloak volume `mustask-keycloak-dbe9dedd12b91b69` **removed**: a cold pair. The same harmless "stale E2E stand file" line |
| 4 | helper `migrate` | exit 0: the eight migrations, `InitialCreate` through `AddWorkItemFormFields` |
| 5 | helper `start` (background) | the API answered 503 after the AppHost's banner while the Keycloak realm loaded; the realm's well-known document was 200 at 14:12 |
| 6 | helper `seed` | exit 0 (14:12): `mustask` → «Администраторы», `member` → «Сотрудники / Участники»; `/health` was 200 right after |
| 7 | `cd src/frontend && npx playwright test` (env as above), about 14:13–14:24 | 69 tests in 25 files, **68 passed, 1 failed (26.6), 0 skipped, 0 flaky**, 10.6 min |
| 8 | `npx playwright test e2e/specs/26-backlog-tree-editing.spec.ts` after the spec fix (rerun cycle 1 of 2) | **6 passed**, 60 s |
| 9 | a throwaway probe of the page's and the card's heights at five viewport heights, then deleted | see "26.6" |
| 10 | a throwaway spec seeding the reference-shaped tree through the API and saving seven screenshots, run twice (the first take caught two menus mid-fade), then deleted | 1 passed, 21 s; seven PNGs — see "Screenshots" |
| 11 | stand stopped: `taskkill //PID <Mustask.AppHost.exe> //T //F`, `docker rm -f mustask-e2e-keycloak-5010` | 3000, 3100, 5000, 5010, 5100 and 17125 free (only TIME_WAIT sockets left); no `Mustask.*`, `dcp` or Vite process left; no Keycloak container; lock released. `mustask_feat027` and the Keycloak volume left as they are; `postgres-5432` untouched and running |

**Totals (full run):** 69 tests in 25 files, 68 passed, 1 failed, 0 skipped.
Tests per file as in run 1: one each in 01–11, 13, 14, 16, 17 and 18; 3 in 15; 13
in 19; 6 in 20; 8 in 21; 3 in 22; 3 in 23; 5 in 24; 6 in 25; 6 in 26. Every file
passed in full except 26, whose 26.1..26.5 passed and 26.6 failed. **Totals
(after the one rerun cycle):** 69 passed, 0 failed — file 26 reran 6/6 (the other
24 files are the full run's). Against run 1 (67 of 69, then 68): 25.1 passed in
the full run (8.2 s; the fix of `061ec4b` held), and 26.6's first failure, P-1,
is gone.

| File | Test | Result |
|---|---|---|
| 23 | 23.1, 23.2, 23.3 | passed (11.2, 3.7, 4.1 s) |
| 24 | 24.1..24.5 | passed (12.3, 6.6, 6.3, 7.2, 5.8 s) |
| 25 | 25.1..25.6 | passed (8.2, 7.1, 5.8, 5.3, 6.4, 8.9 s) |
| 26 | 26.1..26.5 | passed in the full run (12.1, 6.5, 7.9, 6.1, 6.4 s) and in the rerun |
| 26 | 26.6 the look | **failed** in the full run (9.1 s, a spec defect, below); **passed** after the fix (the rerun of the file) |

**Cold or warm.** The store was cold (step 3), but **the run was warm** by the
suite's own word: `e2e global setup: warm stand — http://127.0.0.1:5100/health
was green at the first check`. The seed took the minute the API needed, so
`/health` was green when the suite began; the cold-start window of 05-frontend §
7 was not exercised again, as in run 1. Whether to force it (start the suite
before the seed) is still the orchestrator's call.

### The one failure — 26.6, a spec defect; fixed in `68cbd14`, rerun green

The case failed in its step "the header stays in view while the rows scroll
inside the card", at the assertion fix pass 2 added for F-3: after
`page.setViewportSize({ width: 1440, height: 420 })` the page's
`scrollHeight` was **444** against a `clientHeight` of 420 ("the page does not
scroll for the table"). Every step before it passed — the separators, the colours
(D-10), the row states, the keyboard stops — and the steps after it were not
reached in the full run, but passed in the rerun.

The cause is the assertion's viewport, not the product. A throwaway probe (deleted)
opened an epic with eleven features and read the page and the card at five heights:

| Viewport height | Page `scrollHeight` | Card height | Card `scrollHeight` |
|---|---|---|---|
| 420 | 444 | 240 | 472 |
| 444 | 444 | 240 | 472 |
| 460 | 460 | 256 | 472 |
| 480 | 480 | 276 | 472 |
| 900 | 900 | 472 | 472 |

The card has a floor of 240 px (design § 4.2, "at least 240 px"), it starts 180
px down and the page keeps 24 px of padding beneath it (`maxHeight: calc(100dvh -
204px)`), so under 444 px of height the page scrolls for **the floor**: 180 + 240
+ 24 = 444 at 420 px, whatever the table holds (the card's own content was 472
and scrolled inside it throughout). From 444 px up the page does not scroll —
which is what F-3 is about (the visually hidden descriptions stretching the
document by the table's height). The case was written against the review's
proposal ("1440×420") and was type-checked and listed, never run, before this
run. The fix resizes to 1440×460, where the card is 256 px, the table (about 400
px of rows) still scrolls inside it and the F-3 stretch would still push the
document past 460. One line of the spec and a comment; no product file.

### Defects

**None in the product.** P-1 (the header separators as wide as their cells), the
only product defect of run 1, is fixed: 26.6's separator assertions
(`1px × 20px`, none after the hierarchy column, «Порядок» or the last header)
pass, and the labels read in the pictures below. Not a defect, but worth the
human's knowing: **below 444 px of window height the page scrolls 24 px** for
the card's 240 px floor (design § 4.2); no case asserts that, and it is how the
design reads.

### Specs changed

- `src/frontend/e2e/specs/26-backlog-tree-editing.spec.ts`, commit `68cbd14`
  (`test(e2e): 26.6 reads the page height above the card's floor`): the
  page-scroll read of 26.6 at 1440×460 instead of 1440×420. No other spec file
  was edited; `25-work-item-fields.spec.ts` is as `061ec4b` left it.

### Screenshots (for the human's side-by-side comparison with the reference screenshot)

Not an assertion. Taken after the fix passes by a throwaway spec (deleted, never
committed) that signed in the way file 26 does and seeded, through the API, its own
organization «Организация снимков …» with the project «Снимки бэклога» and the
team «Команда снимков» (deactivated at the end; the rows stay in
`mustask_feat027`). The first run's spec was not kept, so the tree was rebuilt
from run 1's pictures and the description above: epic 1 (a long title) → feature
«Подписки и каналы доставки» → the story «Пользователь выбирает каналы
уведомлений в профиле» → a bug; the same feature → two stories with long titles
(they truncate), «us1» («Закрыта») with the bug «bg1» and the tasks «be», «fe»,
«qa», «release» («Закрыта») and «qa1» («Новая»), and «US2»; epic 1 → feature
«Журнал отправленных уведомлений» → one story; epic 2 «Платформа аналитики» →
the feature «Дашборд ключевых показателей», both with «Бизнес-ценность» 100: 18
rows (run 1's text counts 19; its pictures show 18). The PNGs are kept outside
the repository, in the run's session scratchpad folder `shots-027b/`, under the
names of run 1's `shots-027/`:

| File | Shows |
|---|---|
| `backlog-1366.png` | «Эпики» at 1366×868, the first epic row hovered (the pointer on its «Тип» cell): light grey, the grey «+», the dotted grip at the left edge, «⋮» |
| `backlog-selected.png` | the same, the nested bug row selected by a click: light blue, framed in the accent, grip and «⋮» shown, the pointer away at the top |
| `backlog-add-row.png` | the inline add row open directly beneath story 1 after «Добавить: Задача» (above the story's bug): the type «Задача», its icon, the field «Введите заголовок» focused with the accent ring, the row selected |
| `backlog-plus-menu.png` | feature «Подписки и каналы доставки»'s «+» menu open, settled: «Добавить: Пользовательская история», «Добавить: Баг», each with its type icon |
| `backlog-row-menu.png` | the «⋮» menu of the nested story open, settled, the story's row selected: «Изменить родителя…», «Убрать родителя» (the four moves are on top-level rows only) |
| `backlog-1920.png` | «Эпики» at 1920×1080, nothing hovered or selected: all 18 rows fit, including epic 2's feature with 100 |
| `kanban-head.png` | «Канбан» at 1366×868: the team's name with its drop-down and the board icon, the tab «Канбан», «Эпики» at the right, «Показать бэклог» as the grey secondary button, no primary button |

What the executor saw in them, for the comparison, against run 1's pictures: **the
grey bar over the headers is gone** — «Тип», «Заголовок», «Статус», «Трудоё…»,
«Бизнес…», «Вид ценности» read as small grey labels with a thin vertical
separator between them (the two cut labels are cut by design § 4.3, as the
reference's «Busin…»); the row «+» is grey, not orange (D-10, Q-2); the type
icons carry D-10's colours (the crown orange, the feature's cup purple, the
story's book blue, the bug red, the task ochre); the «Новая» dot is light grey,
«Закрыта» green; the primary button, the tab's underline, the selected row's frame
and the focus ring are the blue accent `#0078D4`. At 1366×868 the card does not
hold all 18 rows: it scrolls inside itself with the sticky header, and the last
feature (100) is below the fold, as it was in run 1 (1920 shows it). In
`kanban-head.png` a long epic title still wraps into a column of about 70 px
beside the identifier and breaks words mid-word («уведомлен / ий:»); that is the
board card's layout (mod-006, feat-022), outside this task, noted only because
it shows.

### What was not checked

As in run 1: the reference screenshot itself (the human's reading of the PNGs);
the 64-item depth bound's refusal in a browser (the refusal tables now have the
code and component tests read it; no E2E case does); what the spec leaves to the component
tests (the add row's pending state and double Enter, a create refused because
the parent has gone, the look of the drag frames, touch); the 2 000-drawn-rows
budget and the 5 000-item volumes in a browser; widths other than 1440 and 1280
as assertions; access beyond mod-002 D-a; and a stand whose first health check is
red. Fix pass 2's F-1 (a lost answer to the create) and F-2 (a table mounted on a
cached answer) are Vitest cases; no E2E case reads them.
