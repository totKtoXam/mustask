# Backend plan: Area, project and team navigation (feat-043)

Stage 4a. The line is almost all panel; the backend is the additive reads only. Contract: [api-contract.md](api-contract.md). No ADR is owed (the choice of a read family is inside ADR-0014/0029; the cursor is feat-048's).

## 1. Decision: two reads, not client-side assembly

design.md § 10 builds «Обзор» and the selectors' offers on the panel from existing lists and keeps "an additive `selector-offers`" as the plan's call. The existing lists cannot give the fit data in a constant number of reads: `GET …/projects` carries no place and no links, `GET …/teams` carries each team's projects but not its departments, and department–team links are readable only per department (`…/areas/{a}/teams`), one read per department (200 teams × 1 000 areas). So the plan adds two bounded reads (api-contract §§ 1-2) and nothing else. D-2 C (a new overview read with 5 rows) is taken because the counts of «Обзор» need all rows otherwise.

## 2. Affected slices

| Where | Change |
|---|---|
| `Mustask.Application/Abstractions/Organizations/INavigationReadModel.cs` | new: `ReadOverviewAsync`, `ReadSelectorOffersAsync`, each with `CallerAccess caller` first (BR-NV-15) |
| `Mustask.Application/Features/Organizations/GetOverview/`, `GetSelectorOffers/` | new use cases (query, handler, response); DI in `Application/DependencyInjection.cs` |
| `Mustask.Infrastructure/Organizations/NavigationReadModel.cs` | new read model over the reachability specification; DI in `Infrastructure/DependencyInjection.cs` |
| `Mustask.Api/Features/Organizations/OrganizationEndpoints.cs` | two routes, `organizations.organization.read` |
| `OrganizationAddressedRoutes.cs`, `TeamViewContainmentTests` | the route inventory gains the two rows; the pinned count 71 becomes 73 |
| `TeamResponse.cs`, `SetDefaultTeamEndpointTests.cs` (comments) | US-9 CR-3/CR-6: no behaviour, route, shape or assertion changes |

No migration, no domain change, no new error code, no new permission. Departments are `areas` with `type = Department`; a project's place is the parent of its `Project` node.

## 3. Statement counts (pinned, NFR Volumes)

| Read | Statements | Which |
|---|---|---|
| `overview` | 7 | 3 counts (departments, active projects, active teams); 3 first-5 reads; the caller's own teams |
| `selector-offers` | 5 | departments; projects with place; project–team links; teams with `isMember`; department–team links |
| `GET …/areas` (the list «Структура») | 1 | existing, pinned by `AreaTree_GetRows_FiveHundredNodesTenLevels_OneQuery` |
| `GET …/teams` (existing) | 2 | existing, pinned by `OrganizationTeams_ByName_WithActiveProjects_TwoStatements` |
| `GET …/projects` (existing) | 2 | organization check and list; unchanged |

`NavigationReadModelTests.BothReads_StatementCountIsPinned_AtFiveAndAtTwoHundredObjects` pins the two new counts at 5 projects/5 teams/2 departments and at 200/200/20. The lists «Проекты» and «Команды» are served by `selector-offers` (name, key, place; name, mark), so they add no read.

## 4. BR-NV-15: the caller as a required argument

Both reads take `CallerAccess` first; feat-041 adds the visible set inside them. `NavigationReadSignatureTests` fails on a method of `INavigationReadModel` that lacks a non-optional `CallerAccess` first parameter, and on a third method added without the test being touched. **Open point (question to the human/orchestrator):** the existing `GET …/areas` tree read (`IAreaTreeReadModel.GetRowsAsync(organizationId)`), the project list and the organization-teams list do not take the caller; the list «Структура» uses the tree. Moving them behind the caller touches five handlers and about ten fakes and tests for no behaviour change; the plan leaves them and records that feat-041 must move the tree read (the only read of the three that returns project nodes) when it lands.

## 5. Tests

| Test | Covers |
|---|---|
| `Mustask.Application.Tests/…/Navigation/NavigationReadsHandlerTests` | handlers hand the read the caller; rows pass through; malformed id is `organizations.organization.not_found` without reading |
| `…/Navigation/NavigationReadSignatureTests` | BR-NV-15 |
| `Mustask.Infrastructure.Tests/…/NavigationReadModelTests` | US-2 counts (departments only), first five by name, caller's teams, BR-NV-13 exclusions, place and links, statement pins at both volumes |
| `Mustask.Api.IntegrationTests/…/NavigationReadsEndpointTests` | response shapes; viewer reads; non-member 404, no token 401; deactivated project absent |
| `OrganizationAccessInventoryTests` (existing) | the two routes declare their code, deactivated organization 404 (through `OrganizationAddressedRoutes`) |

Not run yet (serialized test runs); the code and tests compile.

## 6. Retired addresses and menus: one source

The retired addresses (BR-NV-6) and the menu per scope (ADR-0032 § 2) are **panel routes, with no backend part**: no API route is retired or renamed. The single source for both is the frontend's, a route table (`src/frontend/…/navigation`) that the route-inventory test and the retired-address test read (design § 10). The backend holds no copy, so nothing here can drift from it.

## 7. Lanes

Two use cases: not four, no lanes. Done in one pass.
