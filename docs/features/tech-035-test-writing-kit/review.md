# Review — tech-035-test-writing-kit

## Round 1

Reviewer: code-reviewer (Opus), clean session. HEAD `9a8117f4`, diff `origin/main...HEAD` (23 files). QA (`qa.md`)
not re-run; no tests hand-run.

**Verdict: changes requested.** 3 major, 2 minor. All three majors are small: two are prose-only, one is a few lines
of `ProblemAssert` plus a kit test.

Scope: holds. The diff touches no runtime code, csproj, sln or `package.json`. Only the six reference tests moved,
one test each. `tests/Directory.Build.props` imports the repository-wide props through `GetPathOfFileAbove`
(TFM, warnings-as-errors, coverage dir and git hooks still apply; the root file's `$(MSBuildThisFileDirectory)` stays
its own). The kit files sit outside every project folder and come in once through a `Link`, so nothing compiles twice.
The reference tests still check what they checked before. The race test keeps both "did not finish early" assertions
inside `Race` (`Race.cs:387,394`), and it now also checks the `params` type and count and the response shape. The
Domain, Application and Infrastructure tests only swap constants they compare with themselves.

### Findings

**F1 — major — `docs/conventions/testing.md:90-92`.** The "shape of a new endpoint test" fails if copied.
- A `Viewer` renaming the organization gets 403, not the validation 400. The route requires
  `OrganizationsOrganizationWrite` (`OrganizationEndpoints.cs:114`), and the viewer bundle holds read codes only
  (`OrganizationRoleBundles.cs:34`). The access filter answers before the handler validates (`OrganizationEndpoints.cs:446-448`).
- Fix: drop `.Member("vera", OrganizationRole.Viewer)` and send the request as `world.Admin`. If the example is
  meant to show roles, keep it but assert the forbidden answer instead.

**F2 — major — `src/backend/tests/Mustask.Api.IntegrationTests/Kit/ProblemAssert.cs:285-288, 298-301` vs
`testing.md:80`.** The doc promises «exactly those `params`», but:
- with no expected parameters the method returns before it reads `params`, so a body carrying unexpected `params` passes;
- number, date and moment values are checked by type only, never by value.

A test author relying on the documented guarantee gets a false green. This is QA minor 1, raised here under
item 6 because the guarantee is not there.
- Fix, parameters: when `parameters` is empty, assert that `params` is absent or an empty object.
- Fix, values: compare numbers by value (`GetDecimal`), and dates/moments against the server's wire format.
  Or narrow line 80 to «string values by value, others by type».
- Add two `KitAssertionsTests`: unexpected `params` with none expected, and a wrong number value.

**F3 — major — `docs/conventions/testing.md` (whole file) has no reader.**
- Nothing links to it: `backend.md` § Tests (`:335`), `frontend.md` § Tests (`:145`), stages `04-backend.md`
  and `05-frontend.md`, the developer agents and the `AGENTS.md` files all omit it.
- So its rules have no anchor: «a new test uses the kit» (`:3-5, :112-113`) and the anti-patterns (`:40-52, :159-175`)
  are never put in front of the developer. New tests will keep copying `PropertyNames`/`Name`/`Now`, which is what
  the task exists to stop.
- Fix: add one line to `backend.md` § Tests and one to `frontend.md` § Tests, along the lines of «New tests: the
  layer ownership, the kit and the anti-patterns are in `testing.md`». Stages 04 and 05 already assign
  those files (`04-backend.md:6`, `05-frontend.md:6`), so that is the anchor. It is the weakest kind (an
  instruction plus the stage that assigns it).

**F4 — minor — `testing.md:66`, `Kit/ApiClient.cs:93-103`, `Shared/Kit/TestData.cs:11`.**
- «`TestData.Creator` — the user a test organization is founded by» is true only in Api tests, and only because
  `OrganizationsTestData.cs:21` repeats the same literal.
- The other projects found organizations with their own creators (`OrganizationsDatabaseFixture.cs:32`,
  `InMemoryOrganizations.cs:24`, `OrganizationsDomainTestData.cs:11`).
- The Infrastructure reference test now links a team as `TestData.Creator` in an organization founded by `8a2f…`
  (`MembershipRepositoriesTests.cs:238`). That is harmless today.
- Fix: `public const string Creator = TestData.Creator;` in `OrganizationsTestData`, and add «(Api tests; the other
  fixtures converge in phase 2)» to line 66.

**F5 — minor — `src/frontend/src/test/api.ts:55-59`.**
- `problemText` silently leaves `{name}` in place when a parameter is missing. The served body and the expected
  text then carry the same placeholder, so the test passes on wording no user sees.
- The backend `ExpectedText` fails in the same case (`ProblemAssert.cs:332`).
- Fix: throw on an unfilled `{param}` (the catalog's `params` lists them), and add a `kit.test.tsx` case.

Carried, not re-raised: QA minor 2 (`Race`/`World`/`ApiClient` have no failure-mode tests of their own; `testing.md:51-52`
holds for the assertions) and QA item 3 (`AggregateViewVolumeTests` budget), both for gate-3.

### Behavioural claims (item 6)

- testing.md:3-5, 112-113 «new tests use the kit»: no stage or convention links the file → F3.
- testing.md:61 «compiled into every test project by tests/Directory.Build.props»: `tests/Directory.Build.props:11-15`.
- testing.md:62 «namespace Mustask.Testing, internal»: `Shared/Kit/TestData.cs:1,8`, `RepositoryLayout.cs:1,8`.
- testing.md:66 TestData.Creator «the user a test organization is founded by»: Api only → F4.
- testing.md:68 UniqueName «prefix + 32 hex»: `TestData.cs:17` (`:N`).
- testing.md:69 Name «not in Mustask.Shared.Tests»: `Directory.Build.props:14-15` condition.
- testing.md:79 ApiClient default caller, `.As`/`.Anonymous` return another client: `ApiClient.cs:13-31`, `OrganizationsTokens.cs:9,29`.
- testing.md:80 Problem «detail rendered from the catalog»: `ProblemAssert.cs:283,331`; «exactly those params»: → F2.
- testing.md:81 Validation «exactly one reason with catalog text»: `ProblemAssert.cs:316-318`.
- testing.md:83 ResponseFields «exactly these, any order»: `ResponseFields.cs:424-429`, `KitAssertionsTests.cs:220-243`.
- testing.md:84 World «through the domain/repositories; only named steps»: `World.cs:547-556` → `OrganizationsTestData.cs:37-80`.
- testing.md:85 Race «holds first, waits until second blocked, releases»: `Race.cs:383-399`, `PgLocks.cs:20-47`.
- testing.md:90-92 example: fails with 403 → F1.
- testing.md:101-102 KitAssertionsTests «needs no Docker»: no collection or factory (`KitAssertionsTests.cs:146`).
- testing.md:133 renderPage «real AppRoutes, no retries, signed-in, role admin by default»: `renderPage.tsx:36-63`.
- testing.md:134 apiUrl «base from the test env»: `api.ts:18-21`.
- testing.md:135 problem/problemText «from contracts/errors; unknown code throws»: `api.ts:29-52`, `kit.test.tsx:36-41`; parameters «filled in»: → F5.
- testing.md:136 serveProblem «problem content type, once»: `api.ts:101-110`, `kit.test.tsx:45-68`.
- testing.md:137 recordRequests «204 by default, keeps method/path/search/body»: `api.ts:135-150`, `kit.test.tsx:72-102`.
- testing.md:138 hold «pending until release»: `api.ts:165-174`, `kit.test.tsx:106-122`.
- testing.md:139 id scheme «a…e, the scheme ids already follows»: `fixtures.ts:78-94`, `kit.test.tsx:126-131`, `testFixtures.ts:32-57`.
- testing.md:141-142 «renderPage uses servedFor, renderShell»: `renderPage.tsx:13-14`.
- testing.md:173 «setup.ts resets handlers, session and DOM»: `src/test/setup.ts:52-83`.
- Direction 2, test project build composition: grepped `Directory.Build.props` in docs/, .claude/, scripts/ and the
  AGENTS.md files. Hits: `git.md:194` (git hooks), tech-046 `status.md:18` and `coverage-gate.mjs:8` (coverage dir).
  All still hold through the import. `verify.sh:849` walks `tests/*/`; `Shared/` has no `[Fact]` and is skipped.
- Direction 2, the convention set: grepped `testing.md` in `backend.md`, `frontend.md`, `docs/ai-workflow/`, the
  agents and the `AGENTS.md` files. No hits → F3.
- Not swept: the slices' own `testRender`/`testApi`/`testFixtures` prose (their behaviour is unchanged by this diff).

## Round 2 (delta)

Delta read of `5efef878` (fixes F1–F5), the one delta read M allows. Targeted runs:
`KitAssertionsTests` 17/17 green; Vitest `kit.test.tsx` + `DefaultLanguageDialog.test.tsx` 24/24 green.

**Verdict: approve.** No finding open.

- **F1 closed.** `testing.md:90-92` now builds `World.With(factory).BuildAsync()` and renames as `world.Admin`.
  The admin holds the write code, so the request reaches the handler's validation.
- **F2 closed.** `ProblemAssert.cs:27-35`: with no parameters expected, a non-empty `params` fails. The server
  omits the key (`BusinessExceptionHandler.cs:225-228`), so an absent or empty `params` passes.
  `ProblemAssert.cs:46-64`: values are compared for every type (date `yyyy-MM-dd`, moment in UTC, number as decimal).
  This matches what the server writes (`BusinessExceptionHandler.cs:257-265`). New tests
  `Problem_UnexpectedParams_Fails` and `Problem_NumberOfAnotherValue_Fails` fail on the value check, not
  earlier: `organizations.validation_failed` has no `{param}`, so the detail still matches.
- **F3 closed.** `backend.md:341-342` and `frontend.md:153-154` point to `testing.md`. Stages 04 and 05 assign
  both files, so a developer is put in front of it.
- **F4 closed.** `OrganizationsTestData.cs:21` is now `= Mustask.Testing.TestData.Creator`, and `testing.md:66`
  is narrowed to the Api tests.
- **F5 closed.** `api.ts:55-63` throws on an unfilled `{param}`, covered by `kit.test.tsx:43-45`. The only
  callers of the kit's `problem`/`problemText` pass every parameter: `kit.test.tsx`, `DefaultLanguageDialog.test.tsx`
  and `renderPage.tsx` (which imports only `apiUrl`).

Nothing new broke. The fix touches only test code and docs, and the claims list of round 1 still holds with
the anchors above. Direction 2: the changed behaviours are the `problemText` throw and the strict `params`
check. Grepping the kit's import path finds only the three callers named above. Grepping `ProblemAssert`
outside `Kit/` finds only the race reference test, which passes a string parameter, a path the fix leaves unchanged.
The race test itself was not re-run (Docker); QA minor 2 and item 3 stay carried to gate-3.
