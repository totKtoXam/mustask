# Frontend plan: Area, project and team navigation (feat-043)

Stage 6a artifact. Inputs: `spec.md`, US-1..5 and US-7..9 (US-6 and US-10 are `feat-048`'s: no code, their menu entries stay absent), `design.md`, `api-contract.md` (two additive reads: `GET …/overview`, `GET …/selector-offers`), ADR-0032, `prototype/index.html`. Absorbs tech-019: the shell's kit look lands here.

## 1. Principles

- **Reuse first** (design § 1). `TopBar`, `MainNavigation`, `SettingsNavigation`, `NavigationOverlay`, `PageHeader` and the rest of the kit stay. The only new part is `SearchMenu`.
- **Pure core, thin shell.** The decisions of BR-NV-17 / D-7 (what a selector offers, what a choice resets) and the address model (menu table, retired addresses) are pure modules under `app/navigation/` with test tables. The components and the context source only call them.
- **One source for addresses.** `app/navigation/addressModel.ts` holds the menu table (ADR-0032 § 2) and the retired-address table (§ 4). Tests read it: the route-inventory test (every menu segment has a route; no retired shape has one) and the retired-address test (each answers «Страница не найдена»).
- **No new API shape.** Pages read what `api-contract.md` lists; a mismatch with the real API is a blocker for the orchestrator, not a patch.
- **Drag and drop stays the existing native mechanism** (`BacklogRow`, `BoardColumns`); this line adds none, and feat-048 reuses it.

## 2. New and changed modules

| Module | Role |
|---|---|
| `app/navigation/selectorFit.ts` | Pure: `offerFor(kind, selection, data)` → `{ fitting, others }`; `applyChoice(selection, kind, id, data)` → `{ selection, reset }`; `openObject(selection, kind, id, data)` (D-3); `resetNotice(reset, names)` → «Выбор сброшен: …». `selectorFit.test.ts` is the test table (the US-8 cast). |
| `app/navigation/addressModel.ts` | The menu table by most specific kind (segments, labels, scope, code), `menuFor(kind)`, `RETIRED_ADDRESSES`. Read by tests. |
| `app/navigation/selection.ts` | The selection (organization + department + team + project) read from and written to the address (`teamId`, `departmentId` in the query, ADR-0032 § 1) and the remembered selection (`mustask.context.v2.<sub>.…`, § 6), with the fallback. Pure parts tested; storage injected. |
| `shared/ui/actions/SearchMenu.tsx` | The selector popup (Popover + search + grouped list + states). Exported from the kit: see § 6, deviation 1. |
| `app/shell/ContextSelectors.tsx` | The four triggers in the `TopBar`, replacing `ContextSwitcher`; reads `selector-offers`. |
| `app/shell/*Navigation*`, `model.ts` | The menu built from `addressModel` for the most specific object; «Настройки» derived; settings groups per chosen kind. |
| `features/organizations/…` | Pages: «Обзор», «Области», «Проекты», «Команды», department, node/sub-area, project («Общие», «Команды», «Параметры»), team («Обзор», «Проекты», «Участники и доступ», «Параметры»); link/placement dialogs (US-7); API for the two reads. Project and team views (workitems, boards) keep their slices; the team filter is the selector. |
| `app/routes.tsx`, `features/organizations/paths.ts` | New routes per design § 2; the retired ones removed. |
| `app/shell/TopBar.tsx` | The kit look (grey trail, hairline chevrons): tech-019. |
| `docs/…` | US-9 marks (see § 5). |

## 3. State

- Server state: TanStack Query, keys `['navigation', 'selector-offers', orgId]` and `['navigation', 'overview', orgId]`, invalidated after every link, unlink, placement, create, deactivate and rename (US-7: both ends' queries and the offers).
- Client state: the selection lives in the address (the most specific object in the prefix, the others in the query) and the remembered value in `localStorage`; the address wins in its tab.
- Fit is judged client-side from `selector-offers` (no new request per choice).

## 4. Test plan

- `selectorFit.test.ts`: table of the US-8 cast (Логистика/Финансы, A–D, MARS/VENUS/JUPITER): offers per selector in each selection state, a fitting choice resets nothing, a choice from «Остальные» resets with the notice (one and two kinds), `openObject` (D-3), the team order (own first), no project node / foreign / deactivated.
- `addressModel.test.ts`: menu table per kind (order, segments, no duplicates), «Настройки» not a row, retired shapes match nothing in the menu; the route inventory against `routes.tsx`.
- Retired addresses: each answers «Страница не найдена» through the router (routes test), a non-member sees «Организация не найдена».
- `SearchMenu.test.tsx`: roles and names, groups, search narrowing, keyboard, loading / error / empty, the caption; a showcase entry.
- Page tests with MSW per page and per state of design.md (loading, empty, empty after filter, error, not found, permissions), the statement/request counts for «Обзор» and the lists (one read of `selector-offers`, one of `overview`).
- Source-reading test for tech-019: no shell file imports the legacy look the kit replaces.

## 5. Commit sequence

1. Plan (this file).
2. `selectorFit` + test table.
3. `addressModel` (menu table, retired addresses) + tests.
4. `SearchMenu` (kit) + tests + showcase entry + guide block.
5. US-9 documentation marks (ADR-0024 §§ 1, 2, 4, ADR-0028 rule 7, ADR-0031 rule 5 → ADR-0032; doc-012 US-10 / BR-MX-27 → feat-043; «Breaks» notes).
6. API layer for the two reads + types + MSW fixtures.
7. Selection (address + remembered) and `ContextSelectors` in the `TopBar`; menu from `addressModel`; settings navigation by chosen kind.
8. Routes and paths; the retired ones removed; «Страница не найдена» tests.
9. Pages: «Обзор»; «Области»; «Проекты», «Команды».
10. Pages: department, node and sub-area.
11. Pages: project and team (menus, «Параметры»).
12. Link and placement dialogs (US-7).
13. Interim retirements (US-9 code half): `ContextSwitcher`, feat-040's team level, mod-020's fallback team, `switchTarget`; bug-003 / tech-009 F4 matching.
14. Verification pass: lint, type check, affected tests.

## 6. Deviations and open points

1. **`SearchMenu` is in `shared/ui`, not shell-local.** Design § 1 says shell-local, built from `Popover`. `KIT-3` of `kitGuard.test.ts` forbids a `Popover` import outside `shared/ui` and the allow-list may only shrink, so a shell-local part could not import it. The part is generic (no business words) and is exported from the kit with its tests, showcase entry and guide block (ui-kit.md § 7).
2. The design's "no overview read unless the statement-count test fails" is superseded by the backend's `GET …/overview` and `…/selector-offers` (`api-contract.md`).
3. `design.md` § 1 is corrected to say `SearchMenu` is in the kit (deviation 1), and `openObject` returns `{ selection, reset }`: a row link whose less specific selector does not fit resets it with the notice, as D-7 does ("does the same", design § 3); the more specific kinds still reset silently.
4. Step 7 is split: `selection.ts` (address and remembered selection, pure) and `ContextSelectors` (controlled, no data source) landed first. Wiring them into `TopBar` in place of `ContextSwitcher`, the menu built from `addressModel`, the settings groups per chosen kind, and the provider that reads the address and `selector-offers` replace the whole `WorkContextSource` seam (`shared/lib/workContext`, `switchTarget`, `navigationModel`, about 40 tests in `app/`). They go together with step 8 (routes) and step 13 (retirements) as one reviewed move, because the old and the new shell cannot both stand on the same routes.
