# ADR-0024: Build every panel screen on one shell and one theme, carry the work context in the address, and name type appearance by a closed token set

- Status: accepted (delegated by the human 2026-10-01, recorded 2026-10-02,
  mod-015 D-24)
- Date: 2026-10-01
- Context: `mod-015-app-shell-design-system`, stage 3 (design) —
  [spec](../features/mod-015-app-shell-design-system/spec.md) BR-1..BR-19 and
  [design](../features/mod-015-app-shell-design-system/design.md). Owed by
  mod-015 (its D-1, D-23; doc-010 § ADR debt as tech-001 amends it).
  Amends [ADR-0007](0007-frontend-ui-stack-and-screen-state-canon.md)'s
  screen-state canon and closes the "empty after filter" follow-up of
  [ADR-0019](0019-ui-stack-narrowed-to-actual.md).

## Problem

Every MVP module (M1–M5) is about to add screens to the panel, and today the
panel has no frame to put them in: MUI's default blue app bar, a flat
two-item drawer, page-local headers, and an empty state that differs per
page. Left as is, each module's `design.md` invents its own layout, and the
panel is restyled slice by slice later. Four decisions have to be made once,
before the first module screen:

- **The frame and the look.** The visual model is the reference tracker
  captured in `docs/features/doc-001-domain-map/screenshots/`. It has to be
  reproduced with the stack ADR-0019 fixed (MUI core, `@mui/icons-material`),
  with `app/theme` as the only place for appearance
  (`docs/conventions/frontend.md`).
- **Where the work context lives.** Boards, backlogs and lists belong to an
  organization, a project and a team. A link sent to a colleague has to open
  the same team's board. A reload must not lose the context, and a link to a
  context the user may not see must not reveal that it exists (doc-006
  BR-M1-8).
- **How a work item type looks.** doc-007's logical model gives each type a
  free `icon` and `colour` set by the seed. Free values in data put
  appearance outside `app/theme`, cannot be contrast-checked, and leave the
  panel to render whatever a seed or a later custom type contains.
- **How far the layout reflows.** The bar is WCAG 2.1 AA (ADR-0007). A dense
  board and registry cannot reflow to 320 CSS px (WCAG 1.4.10) without a
  phone layout the MVP has no use for.

The canon also has gaps. ADR-0007 has no "not found" state, and ADR-0019
recorded that "empty after filter" was never finished in the shared layer.

## Options considered

### Option 1: MUI defaults, each module lays out its own pages

- Pros: nothing to build now; no shared component to agree on.
- Cons: every later `design.md` designs a header, states and navigation
  again. The look diverges per module and is restyled later in every slice.
  The canon stays a declaration (ADR-0019's debt).

### Option 2: adopt a ready design system or component kit beside MUI

- Pros: more ready-made patterns (navigation rails, filter bars, trees).
- Cons: a new UI dependency, and two component vocabularies in one panel,
  right after ADR-0019 narrowed the stack to what is used. Its look is its
  own, not the reference tracker's. It is not needed: every pattern the MVP
  uses is expressible with MUI core.

### Option 3: one shell, one theme and shared patterns, built with MUI core (chosen)

- Pros: one place for appearance (`app/theme`), one frame (`app/`), one set
  of patterns (`shared/ui`). Later designs fill a known pattern, and review
  and QA check against one document. No new dependency.
- Cons: the shell and patterns are code this project owns and maintains;
  every page has to be wired through the shared header and state frame.

For the work context:

- **(a) Client state only** (the selection kept in the browser): links lose
  the context, two tabs cannot show two projects, and a reload depends on
  storage. Rejected.
- **(b) Query parameters** (`?organization=…&project=…`): optional by nature,
  easily dropped by a link builder, and a page cannot tell "no context" from
  "context omitted". Rejected.
- **(c) Fixed path prefixes** (chosen): the context is part of the page's
  identity, every link carries it, and routing validates it uniformly.

For type appearance:

- **(a) Free icon and colour values in the seed** (doc-007 as written):
  unbounded, not contrast-checked, appearance outside the theme. Rejected.
- **(b) A closed set of token names that the seed stores and the theme
  resolves** (chosen).

For reflow:

- **(a) Claim WCAG 1.4.10 down to 320 CSS px:** needs a phone layout for
  boards, backlogs and dense registries. Not in the MVP.
- **(b) A supported floor of 640 CSS px** (200 % zoom of a 1280 px window),
  with the gap below it recorded (chosen).

## Decision

Option 3, with these rules for every screen of the panel.

**1. Shell and theme.**

- One frame for every signed-in page: a fixed 48 px top bar (product mark,
  context switcher, page trail, user menu), a left navigation, and a main
  region. Auth screens and the crash screen stay outside it, on the same
  theme.
- The navigation is one model in `app/`. An entry is shown only when its page
  exists in the build, the shared permission check passes, and, for a scoped
  entry, the context it needs is real. Nothing is shown disabled or as
  "coming soon". Settings pages show a second navigation with the main one
  as a rail. The main navigation collapses to a rail, the choice kept per
  browser. Width defaults are expanded at ≥ 1280 px and a rail at
  1024–1279 px; below 1024 px the navigation is an overlay.
- `app/theme` is the only source of appearance: the colour roles
  (application background, navigation surface, content surface, neutral
  text, one blue accent with a closed list of uses), the semantic palette
  darker than MUI's defaults, density tokens (14 px base text, 48 px top bar,
  registry rows ≤ 40 px), layout widths and breakpoints. A test run by
  `npm test` rejects colour literals outside `app/theme` (test files and
  `src/test/` excepted, as the spec's BR-1 scopes it); contrast is
  unit-tested.
- `shared/ui` holds the page header with its command bar and tabs, the state
  frame, the registry filter bar and skeletons, the form dialog and the
  confirmation dialog. The work item form, board, ordered backlog, side
  panes, type picker, large settings dialog and tree registry are designed in
  mod-015's `design.md` and built by their first consumer.

**2. The work context is part of the address.**

- Fixed prefixes: `/organizations/:organizationId/…` for organization-level
  pages, `/organizations/:organizationId/projects/:projectId/…` for
  project-level pages, and `…/projects/:projectId/teams/:teamId/…` for
  team-level pages. Pages without a context («Главная», instance settings
  such as `/dictionaries/languages`) keep their addresses.
- Route ids are untyped strings that the page validates, so a malformed id
  and an unknown id get the same answer.
- The switcher shows the address's context. Levels below what the address
  carries show the current selection, and changing them does not move the
  page. Choosing another entry at a level the address carries opens the
  landing page of the same navigation entry in the new context (an
  entity's page goes to its entry's landing page), or «Главная» when that
  entry is not visible there.
- A link whose context is not found answers for the outermost missing level
  — «Организация не найдена», «Проект не найден», «Команда не найдена» — and
  leaves the switcher's selection unchanged. A context that does not exist
  and one the user is not a member of get the same answer.
- mod-015 fixes the rule. mod-002 and later lines build the routes, and
  mod-002 wires the switcher to real lists.

**3. Type appearance is a closed token set.**

- The set is `epic`, `feature`, `story`, `task`, `bug`. The process seed
  stores exactly one token name per type, replacing doc-007's `icon` and
  `colour`; mod-001 carries the doc-007 amendment.
- `app/theme` holds each token's icon and colour. Its lookup takes a token
  name only, and an unknown name renders a neutral fallback. Slices read the
  lookups through the theme object, never by importing `app/`. State
  categories are keyed on doc-007's category codes (`Proposed`,
  `InProgress`, `Resolved`, `Completed`, `Removed`), never on labels or
  state names.
- No type or category colour equals the accent, and each meets 3:1 on the
  surfaces it sits on. Colour is always accompanied by an icon or text.
- The set grows only by a change to `app/theme`. Custom per-organization
  type appearance (release 3) is a new decision.

**4. The screen-state canon gains "not found".** ADR-0007's states (loading
as a skeleton, empty, empty after a filter, error with retry, access denied)
plus not found. Empty, empty after a filter, access denied and not found
share one frame in `shared/ui`: an icon, a heading, a hint and at most one
action. Not found is named by level (page, organization, project, team),
and an unknown panel address shows «Страница не найдена» without
redirecting. This closes ADR-0019's "empty after filter" follow-up in the
shared layer.

**5. Reflow floor.** The panel is supported down to 640 CSS px wide, with the
overlay navigation and no horizontal page scroll outside a wide table's own
container. WCAG 1.4.10 reflow below 640 CSS px is not claimed for the MVP.

**6. The stack is not closed here.** mod-015 adds no dependency that renders
UI, and this ADR restates no closed list of libraries. ADR-0019's revisit of
a data grid at the work item list stands and is mod-003's.

**Convention.** When this ADR is accepted, `docs/conventions/frontend.md`
§ MUI is rewritten in mod-015's implementation stage. It drops the lines
about `@mui/x-data-grid`, `react-hook-form` + `zod` and
`@mui/x-date-pickers` that ADR-0019 lifted. It points to this ADR for the
shell, the theme tokens, the shared patterns, the context-in-address rule
and the appearance tokens.

## Consequences

- **Simpler:** a later module's design fills known patterns (header, states,
  registry, dialogs, work-tracking surfaces) instead of drawing a layout.
  Review and QA check one document. Every link carries its context. Type
  appearance and contrast are testable in one place, and mod-001's seed
  stores a short, stable name.
- **Harder:** a new type look needs a code change, not data. Addresses are
  longer, and every context page must validate its ids and answer not found
  by level. Every page goes through the shared header and frame. The
  navigation model is code, so each line registers its own entry.
- **Locked in for the long term:** the address prefixes, because changing
  them breaks every shared link, and the token names stored in seed data,
  because renaming one is a data migration.
- **Spawned or owed elsewhere:** `tech-003-keycloak-login-theme` (the
  Keycloak login page in this visual language); mod-002 wires the switcher,
  the routes and the tree registry; mod-016 swaps the permission source to
  served organization-scoped codes without changing the navigation model;
  mod-001 amends doc-007's model to one appearance token per type.
- **Revisit when:** phones or reflow below 640 CSS px become a requirement;
  per-organization branding or custom type appearance is taken up (release
  3); a second product area needs a navigation the model cannot express; or
  ADR-0019's data-grid revisit changes what registries are built on.
