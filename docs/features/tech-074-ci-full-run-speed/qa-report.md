# tech-074 — QA report (backend/CI pass)

Checklist-style (a `tech` CI task, no business logic). Read from the diff
`origin/main...HEAD`, run: both case scripts; `ci-coverage-gate.sh` failure
modes; CI runs read with `gh run view`. Full `verify.sh` not run locally
(economy mode).

## Results

| Check | Result |
|---|---|
| Local run without the new variables unchanged | ok: defaults `all`/empty keep every branch; `verify-base-and-abort-cases.sh` 35 green, stamp cases green |
| Part / filter / shard / coverage-out never write or take a stamp | ok: `partial_run` gates the stamp block; **gap closed** (see Added cases) |
| Floors never checked on a slice | ok: filter -> "NOT CHECKED", coverage-out -> left to merge, shard -> blob only |
| Unknown part, shard without `VERIFY_FULL=1`, malformed shard -> exit 2 | ok (cases exist) |
| Backend filters are a partition | ok: the three groups are `~A`, `~B`, `!~A & !~B`; every test namespace starts with its own project name, no project's namespace contains another's name; a new assembly lands in "rest" |
| Required check fails on failed/cancelled needed job, passes on unneeded skipped | ok by reading `check()`; a failed plan fails it (plan checked as needed) |
| Reported when an earlier job fails; name unchanged; SHA-pinned artifact actions | ok: `if: always()`, name `verify.sh (change-proportionate)`, upload/download pinned by SHA |
| `ci-coverage-gate.sh` regrouping matches `coverage-gate.mjs` | ok: `<job>/<project>` -> `<project>/<job>`, `.IntegrationTests` suffix still tells integration; no reports / unknown mode -> exit 1 / 2, usage -> 2 |
| CI evidence | frontend path proven (runs 43922066342, 43927683530: floor ok on merged blobs; docs-only 43920631146 green) |

## Added cases

`scripts/tests/verify-stamp-cases.sh` (24 -> 32): a part, a filter and a
coverage-out dir do not take an existing stamp (even with `VERIFY_FORCE=1`),
do not remove it, and on a tree with no stamp write none.

## Defects

- **D-1 (minor): the backend merge path has never run in CI.** The only run
  with green backend jobs (43899184051, commit 641b8ddb) failed in the static
  job (case scripts then lacked the new variables, fixed in c90c4c0b), so the
  verify job skipped "Fetch the coverage reports" and "Backend coverage floors".
  Later green runs were frontend-only (backend skipped). Untested live:
  artifact folder layout after `download-artifact` (`coverage-backend-<group>/<project>/...`),
  the `mv` into `$RUNNER_TEMP/backend`, and the merged backend floors. Reading
  matches `coverage-gate.mjs`, but the first PR that touches `src/backend` is
  the real test; confirm by a backend-touching push on this branch before
  merge, or accept the risk (a wrong layout fails red, not silently green).
- **D-2 (nit): the skipped matrix jobs show an unexpanded name**
  (`backend tests (${{ matrix.group }})`, `frontend tests (shard ${{ matrix.shard }}/6)`)
  in the checks list when the plan skips them. Cosmetic; not a required context.
- **D-3 (nit): `keycloak` part in `static` duplicates the KeycloakLive tests
  already inside the `rest`/full backend group when .NET changed.** Harmless
  extra runner minutes; unverified whether the group excludes them.

No blocker or major found. Not done: E2E (no UI behaviour changed; out of scope).

## Verdict

No open blocker/major; D-1 is a minor carried to gate-3 as a debt unless the
orchestrator prefers a backend-touching CI run first.
