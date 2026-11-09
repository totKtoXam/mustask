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
| `TestData.Creator` | The user a test organization is founded by (its only admin). |
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
var world = await World.With(factory).Member("vera", OrganizationRole.Viewer).BuildAsync();
var answer = await world.As("vera").PutAsync($"{OrganizationsApi.Organization(world.Organization.Id)}/name", new { name = "" });
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

<!-- TODO(tech-035 frontend stage): fill in. Cover: renderPage(path, {role, data}),
src/test/api.ts (apiUrl, problem, serveProblem, recordRequests, hold), fixture
factories with one id scheme, which layer owns which check on the frontend
(unit / component / page / e2e), and the frontend anti-patterns. Keep the general
rules of section 1 as they are. -->

*To be written by the frontend stage of tech-035.*
