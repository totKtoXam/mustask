# tech-057 — QA (backend + panel, one pass)

Requirements: ADR-0037 and the plan in `status.md` (no spec). Test plan written before reading the diff.

## Test plan (criterion -> test)

| # | Criterion | Covered by |
|---|---|---|
| 1 | Empty base: `redirect_uri`, `/health`, no `/mustask` route | `PathBaseTests.EmptyPathBase_RootBehaviourUnchanged` |
| 2 | `/mustask`: `redirect_uri`, correlation cookie path, session cookie path, final redirect | `UnderPathBase_RedirectUriCookiesAndRedirectsCarryThePrefix` |
| 3 | Malformed base refuses to start; mismatch with `PanelUrl` refuses | `ParsePathBase_*`, `MalformedPathBase_*`, `PathBaseNotMatchingPanelUrlPath_*` |
| 4 | `Location` prefixing: relative prefixed; bare request, absolute, `//`, no-slash, absent untouched | `RelativeLocation_*`, added `Location_NotRootRelative_IsLeftAlone` (4 cases) |
| 5 | Real pipeline routes `/api` through the prefix | added `UnderPathBase_ApiRouteThroughThePrefix_IsRoutedAndChallenged` |
| 6 | Panel `stripBase`/`withBase` edges (root, lookalike, nested base, inverse) | `basePath.test.ts` (two cases added) |
| 7 | `returnUrl`, `/signed-out`, `authError` strip under the base | `AuthContext.test.tsx` (existing 3) |
| 8 | Copied link has the base | `WorkItemCard.actions.test.tsx` (existing) |
| 9 | Every in-panel absolute address carries the base | found a gap, see D-1 |

## Commands

- `dotnet test src/backend/tests/Mustask.Api.IntegrationTests --filter FullyQualifiedName~PathBaseTests`: 30 passed, 0 failed.
- `npx vitest run src/shared/config/basePath.test.ts` plus the dialog test: basePath green; 1 failed (D-1, the new test).
- `npx tsc -b`, `npx eslint` on touched dirs: clean.
- Not run: E2E (standing answer no), full verify, deploy/ (rehearsed).

## Defects

**D-1 (major, panel).** The work-item dialog's "Открыть на отдельной странице" link (`CardHeaderBar.tsx` raw MUI `<Link href={typeHref} target="_blank">`, fed by `pagePath: workItemPath(...)` in `WorkItemCard.tsx` ~l.530) has no base. A raw `href` is outside the router's `basename`, so under `/mustask` it opens `https://portal/organizations/...` (the portal's root, 404). Reproduce: build with `VITE_BASE_PATH=/mustask`, open a card in the board/backlog dialog, click the type line. Fix: `pagePath: withBase(workItemPath(...))`. Test: `WorkItemCardDialog.test.tsx` "keeps the path base in that link" is RED until fixed (expected `/mustask/organizations/...`, got `/organizations/...`).

**D-2 (minor, API).** `Session:PanelUrl` with a path (`https://host/mustask`) while `Hosting:PathBase` is empty starts without complaint (verified by a probe: the host builds). A forgotten `Hosting__PathBase` shows up as a Keycloak `redirect_uri` mismatch at first sign-in, which ADR-0037 says to avoid ("fails the deploy's health wait, not the first sign-in"). Suggest: with an empty base, require `PanelUrl` path to be empty. Check it does not break CI/stand values first.

**D-3 (minor, nit).** `SessionAuthenticationSetup.cs`: `CallbackPath ="/api/session/callback"` has no space before `=` (formatting). `vite.config.ts` `normaliseBase` keeps inner `//` (`/a//b/`) and a full URL; the API would reject such a base, so it is a misconfiguration the build does not catch (kit validates, rehearsed).

## Verdict

Not ready: D-1 blocks (major). D-2 and D-3 are carried as debts if not fixed.
