# Testing conventions: which layer checks what, and the kit

Written by tech-035 (phase 1). The kit below is for **new** tests. The ~hundreds
of existing tests keep their local copies until phase 2 moves them; do not
migrate one as a drive-by in an unrelated task. The stack conventions are in
[backend.md](backend.md) § Tests and [frontend.md](frontend.md) § Tests; this file
adds who owns which check, the helpers, and what not to write.

## 1. General: one check, one owner

A behaviour is asserted at the **lowest layer that can see it**, once. A higher
layer proves the wiring, not the rule again.

| Check | Owner | Not here |
|---|---|---|
| A domain rule (name length, state transition, invariants) | Domain unit test, no mocks | handler, endpoint |
| What a use case does (calls, order, which error it raises) | Application test on fakes | endpoint |
| SQL, mapping, constraints, migrations, locking | Infrastructure test on real PostgreSQL | endpoint |
| Route, auth, status code, body shape, error text, race of two requests | Api integration test | domain, handler |
| Error code exists, catalog wording, realm/stand files, repo rules | Contracts / Architecture tests | everywhere else |
| A component renders its states; a page wires a route and a role | Frontend test (section 3) | backend |

Rules that follow:

- Every acceptance criterion has at least one test, and its comment names the
  story/criterion (`// US-2 · "…"`). A criterion goes to the layer in the table.
- An endpoint test asserts the **contract** (status, code, shape, text, stored
  effect), not the rule's every edge: the edge cases of a rule live in the
  domain or Application test, and an endpoint test adds the one case that proves
  the validator is wired.
- A race test is an endpoint test (the database decides the winner). Nothing
  else is.
- Expected user-facing text comes from the **error catalog**
  (`contracts/errors/*.json`), not from a string typed into the test. Wording is
  edited in one place; a retired code fails the test that names it.
- Names: `Method_Scenario_ExpectedResult`. Data is built through the domain and
  the repositories, never through another use case's endpoint, so tests do not
  depend on each other.

### Anti-patterns

- Copying a helper (`PropertyNames`, `Now`, `Creator`, `Name`, `RepositoryLayout`)
  into a class or project. Use the kit; if the kit lacks it, add it to the kit.
- A Russian message pasted from the catalog into `Assert.Equal`.
- Asserting the same rule at three layers.
- Building the arrangement by calling the endpoints under test's neighbours.
- `Task.Delay` / sleeping to "let the other request get there": use `Race`.
- A shared database name or a fixed id/name in a test: the integration database is
  shared by the collection and never emptied, so names and ids are unique per test
  (`TestData.UniqueName`, `Guid.CreateVersion7()`).
- A test that passes when its assertion is deleted (vacuous): the kit's own
  tests show the assertions *fail* when they should.

## 2. Backend

Commands: `src/backend/AGENTS.md`. Docker must run for the integration layers.
Kit code lives in two places and each is the only copy:

### 2.1 Shared by every test project: `tests/Shared/Kit/`

Compiled into every test project by `tests/Directory.Build.props` (no project of
its own, no NuGet dependency). Namespace `Mustask.Testing`, `internal`.

| Helper | Use |
|---|---|
| `TestData.Creator` | The user a test organization is founded by (its only admin) in the Api tests; the other fixtures converge on it in phase 2. |
| `TestData.Now` | A fixed moment for code that takes the time as an argument. |
| `TestData.UniqueName(prefix)` | A name nobody else uses (prefix + 32 hex digits). |
| `TestData.Name(string)` | A valid `EntityName`, or throws (the test is wrong). Not in `Mustask.Shared.Tests` (no domain). |
| `RepositoryLayout.Root / Source / Frontend / ErrorCatalog / PathUnderRoot(...)` | Where repository files are. The per-project `RepositoryLayout` of Architecture/Contracts stay until phase 2. |

### 2.2 Api integration tests: `Mustask.Api.IntegrationTests/Kit/`

Namespace `Mustask.Api.IntegrationTests.Kit`. A test class is, as before,
`[Collection(MustaskApiCollection.Name)]` taking `MustaskApiFactory`.

| Helper | Use |
|---|---|
| `new ApiClient(factory)` | One client carrying a token. Default caller is `TestData.Creator`. `.As(token)`, `.Anonymous()` return another client. `GetAsync/PostAsync/PutAsync/DeleteAsync/SendAsync`, an optional `CommitGate`. Returns the existing `Answer`. |
| `ProblemAssert.Problem(answer, status, code, ("param", value)…)` | Status, `errorCode`, `detail` **rendered from the catalog** with the parameters, and exactly those `params` with their types. |
| `ProblemAssert.Validation(answer, "x.validation_failed", "field", "x.reason")` | A 400 whose `errors[field]` is exactly one reason with the catalog's text. |
| `ProblemAssert.ExpectedText(code, params…)` | The catalog text, when the test needs it elsewhere. |
| `ResponseFields.AssertExactly(answer, "id", "name")` | The body has exactly these properties, any order. Replaces the copied `PropertyNames`. |
| `World.With(factory).Organization(...).Project(...).Member("vera", OrganizationRole.Member).BuildAsync()` | Composable arrangement through the domain/repositories. `world.Admin`, `world.As("vera")`, `world.Outsider`, `world.SystemAdmin`, `world.Anonymous`; `world.Organization`, `world.Project`, `world.Member("vera")`. Takes only the steps the test names. |
| `Race.TwoAsync(factory, gate => first(gate), () => second())` | Holds the first request at its commit, waits until the second is seen blocked on a lock, releases, returns both answers. `secondWaits: false` when the second is refused without waiting. |

The shape of a new endpoint test:

```csharp
var world = await World.With(factory).BuildAsync();
var answer = await world.Admin.PutAsync($"{OrganizationsApi.Organization(world.Organization.Id)}/name", new { name = "" });
ProblemAssert.Validation(answer, "organizations.validation_failed", "name", "organizations.name_length");
```

Reference tests (one per layer, moved onto the kit in tech-035):
Api `RenameOrganizationEndpointTests.Rename_TwoConcurrentToSameFreeName_OneWins`,
Application `GetOrganizationHandlerTests.Handle_AfterRename_ReturnsTheNewName`,
Domain `OrganizationTests.Create_IsActiveWithItsProcessAndCreator`,
Infrastructure `MembershipRepositoriesTests.TeamList_IsMember_FollowsTheCallersTeamMemberships_InOneStatement`.

The kit's own logic (the assertions fail when they should) is tested in
`Mustask.Api.IntegrationTests/Kit/KitAssertionsTests.cs`; it needs no Docker.

Not in the kit yet (phase 2): migrating the existing copies, a kit for the
Application fakes, a kit for Infrastructure fixtures.

## 3. Frontend

Commands: `src/frontend/AGENTS.md`; the stack rules: [frontend.md](frontend.md)
§ Tests. Vitest + Testing Library + MSW; the API is always MSW, never a mocked
`fetch` or a mocked module. The kit lives in `src/frontend/src/test/` and each
helper is the only copy. The slices keep their own `testRender`/`testApi`/
`testFixtures` until phase 2; a new test uses the kit and does not copy from them.

### 3.1 Which layer owns which check

| Check | Owner | Not here |
|---|---|---|
| A pure rule (a validator, a formatter, a mapping, a reducer) | Unit test, no render, no MSW | component, page |
| One component or hook: its states (default, loading, empty, error), keyboard, what it calls on the callbacks it is given | Component test with `renderWithProviders`; the API through the kit's `api.ts` | page |
| A page or route: it is mounted at its address, reads its data, and the role decides what it shows (open, «Доступ ограничен», «не найдено») | Page test with `renderPage` | component |
| The wording of a refusal | The catalog: `problemText(code, params)` | a string typed in the test |
| Cross-layer happy path against a live stand | E2E (Playwright) at QA, only when the human answered yes; not `npm test` | everything above |

The rule of section 1 holds: a rule is asserted at the lowest layer that sees
it. A page test proves the wiring (route, role, data, one refusal), not each edge
of a validator.

### 3.2 The kit

| Helper | Use |
|---|---|
| `renderPage(path, { role, data, permissions, subject })` (`renderPage.tsx`) | Mounts the **real route table** (`AppRoutes`) at `path` under a router, a fresh `QueryClient` without retries and a signed-in session; serves `data` (default: the stories' example) and the access of `role` (`admin` by default). Returns the Testing Library result plus `queryClient` and `api` (the recording test API of the organizations slice). The address is `screen.getByTestId('location')`. |
| `apiUrl(route)` (`api.ts`) | The URL of an endpoint as MSW matches it (`/api/organizations/:id/access`); the base comes from the test env, never typed. |
| `problem(code, params?, status?)` / `problemText(code, params?)` | A problem body / the Russian text, **from `contracts/errors/*.json`** with the parameters filled in. An unknown code throws, so a retired code fails the test that names it. |
| `serveProblem(method, route, code, params?, { once, status })` | Answers `method route` with that problem and the problem content type; returns the body. |
| `recordRequests(method, route, answer?)` | Serves the route (204 by default) and keeps `{ method, path, search, body }` of every request. Assert `toEqual([...])` for «this was sent» and `toEqual([])` for «nothing was sent». |
| `hold(method, route, body?)` | Keeps the answer pending: `await reached`, assert the loading state, then `release()`. Every test that holds one releases it. |
| `fixtureId(kind, n)`, `organizationOf`, `projectOf`, `teamOf`, `datasetOf` (`fixtures.ts`) | Records with **one id scheme**: `<kind letter>0000000-0000-4000-8000-<n>` (organization `a`, project `b`, team `c`, area `d`, user `e`), the scheme `ids` of the organizations slice already follows. Ask for the n-th thing; give only the fields the test is about. `datasetOf` leaves everything else empty. |

The role bundles (`organizationRoles.ts`: `servedFor`, `checkFor`) and the shell
helpers (`renderShell.tsx`) are shared test code too; `renderPage` uses them.

Reference tests (one per layer, moved onto the kit in tech-035): page
`app/routes.access.test.tsx` ««Деактивированные» проекты are «Доступ ограничен»
for a member»; component `DefaultLanguageDialog.test.tsx` «отказ 409 (язык
неактивен)…». The kit's own logic is tested in `src/test/kit.test.tsx`.

The shape of a new page test:

```tsx
renderPage(`/organizations/${organization.id}/projects?status=deactivated`, {
  role: 'member',
  data: datasetOf({ organizations: [organization] }),
});
expect(await screen.findByRole('heading', { level: 1, name: 'Доступ ограничен' })).toBeInTheDocument();
```

### 3.3 Frontend anti-patterns

- Hand-assembling `QueryClientProvider` + `AuthContext` + `MemoryRouter` +
  `AppRoutes` in a test file: call `renderPage`.
- A Russian refusal text pasted into `findByText`: use `problemText`. Interface
  copy that is not an API message (headings, buttons) is typed as in `design.md`.
- A fixed delay (`setTimeout(…, 20)`) in a handler to "see the spinner": use `hold`.
- Counting requests through a `vi.fn()` inside a handler, or a request log built
  by hand: use `recordRequests`.
- A raw id string or `id-1` in a new fixture: use `fixtureId` / the factories.
- Mocking a module (`vi.mock`) to fake the API: the API is MSW. Modules shared by
  the files of a worker may be mocked by one file only (`mockRegistry.test.ts`).
- Asserting the same rule in the component, the page and E2E.
- Tests that depend on each other's data: every test builds its arrangement and
  `setup.ts` resets handlers, session and DOM after it.
- A helper that passes with its assertion deleted: the kit's tests show each
  helper fails when it should (`kit.test.tsx`).

Not in the kit yet (phase 2): migrating the existing slice helpers and their
tests, a `renderPage` for pages outside the organizations dataset (dictionaries,
boards, work items serve their own data today), E2E fixtures (tech-036).
