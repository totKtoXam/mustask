# tech-040-messagepack-pin-to-cpm — review

## QA (light, S route)

Verdict: **pass**. Open: 0 blocker, 0 major, 1 minor (D-1, a debt for gate-3).

Scope: `git diff main..HEAD` — `Directory.Packages.props` (+12), `Mustask.AppHost.csproj`
(+5/-5), this folder's `status.md`. No source file, no test, no `Mustask.sln` touched.
E2E: not asked — the standing answer is NO (recorded in `status.md`, Decisions).

### Plan coverage (status.md, Plan 1-4)

The change has no code to unit-test; its behaviour is "the restore resolves a fixed
MessagePack and nothing else moves", so each plan line was checked by a command, on a
copy of the tree outside the worktree where the check had to change files.

1. **Baseline.** `StreamJsonRpc 2.25.29` (nuspec on nuget.org) depends on
   `MessagePack >= 2.5.302`; `Aspire.Hosting 13.5.1` depends on StreamJsonRpc. The
   advisory (`gh api advisories/GHSA-qhrr-8q5h-9q3h`): MessagePack `< 2.5.303` and
   `>= 3.0.0, < 3.1.9`, first patched 2.5.303 — so 2.5.303 is the fix on the 2.5 line.
2. **The pin and the rejected alternative, both reproduced.**
   - Scratch project, `Aspire.Hosting.AppHost 13.5.1`, `TreatWarningsAsErrors`, central
     `PackageVersion MessagePack 2.5.303` and **no** reference → restore exit 1,
     `error NU1902 ... MessagePack 2.5.302`. Same project plus the plain
     `<PackageReference Include="MessagePack" />` → exit 0. The comment's claim that the
     explicit reference is what makes the version win holds.
   - A clean copy of `HEAD` (`git archive`), `dotnet restore Mustask.sln
     -p:CentralPackageTransitivePinningEnabled=true` → `NU1109` on Api, Infrastructure,
     AppHost and the test projects (Npgsql, `Microsoft.Extensions.Logging.Abstractions`).
     The comment's and `Decisions`' reason for leaving the switch off holds.
3. **By command.**
   - Clean restore of that same `HEAD` copy, whole solution, switch off: exit 0, no
     NU1902, no warning at all.
   - `dotnet list Mustask.sln package --vulnerable --include-transitive`: none in all
     14 projects (the audit reached nuget.org). AppHost: `MessagePack` requested 2.5.303,
     resolved 2.5.303; `MessagePack.Annotations` 2.5.303.
   - No double hyphen inside an XML comment of either changed file (the Aspire SDK's
     package check break the Decisions line names).
4. **`bash scripts/verify.sh`** — see "Commands run".

### Regression checks around the change

- **The resolved graph did not move.** `git archive ce7f055b` (the base, with the
  `VersionOverride`) restored in a scratch directory and compared with this branch's
  `obj/project.assets.json` for AppHost, Api, Infrastructure and ServiceDefaults: the
  `libraries` sets are equal (78 / 64 / 57 / 22 packages), the per-package dependency
  lists of the `net10.0` target are equal, no restore log entry on either side. So the
  change is a move of where the number lives, not a change of what loads.
- **Other consumers of MessagePack.** Only the AppHost names it. Api, Infrastructure,
  Application, ServiceDefaults carry no `MessagePack` in their assets; the test projects
  compile only `Provisioning/` and `Stand/` sources of the AppHost in and take no
  Aspire package. No lock file, no other `VersionOverride`, no
  `CentralPackageTransitivePinningEnabled` anywhere in the repository.
- **A new `PackageVersion` without a consumer is inert.** Under central management it
  moves no project that does not reference the package; the equal graphs above are the
  evidence.
- **Commits.** Four, English, no attribution line; `scripts/*.sh`, `.githooks/*` keep
  their modes in the index (9 files 100755).
- **Not verified:** the AppHost was not started. No test starts it, and its resolved
  graph is identical to the base's, so a start would exercise the base as much as this
  branch.

### Commands run

`bash scripts/verify.sh` (once, on `be747662`; classified `dotnet` + `prose`, no frontend
check applies) → **exit 0, `RESULT: green`**: prose checks ok; backend build 0 errors,
2 warnings (`MSB3073`, `git config core.hooksPath` could not lock the shared
`.git/config` while another session wrote to it — `ContinueOnError`, not this change;
no NU1902); backend tests Shared 53, Application 709, Architecture 83, Contracts 244,
Domain 658, Infrastructure 325, Api.IntegrationTests 2304 passed + 17 skipped (opt-in
live Keycloak), 0 failed (the Api figure equals the one in the developer's log line).
Everything else in
"Plan coverage" and "Regression checks" ran in scratch copies of the tree outside the
worktree, which is clean (`git status` empty before QA's own docs).

No stamp was written: this was the plain run, not `VERIFY_FULL=1`; `finish-task.sh` and
`pre-push` will make the full run on the final tree.

### Defects

#### D-1: nothing prompts the removal of the pin (minor, repository / docs)

- Steps: read the two comments (`Directory.Packages.props:13-23`,
  `Mustask.AppHost.csproj:17-20`) and `docs/backlog.md`.
- Expected / Actual: feat-039 QA D-4 asked for the pin to move into the central file
  **and** for its removal to be queued. The move is done and the drop condition is
  written in both places, but once this line leaves the queue no line holds the
  removal, and no check fails when the pin becomes redundant — a stale pin is silent
  by design (an explicit reference at 2.5.303 is harmless above it, and a future
  Aspire needing MessagePack 3.x would fail loudly with NU1109). Context for whoever
  writes the line: the latest `Aspire.Hosting` (13.6.1) now lists `MessagePack >=
  2.5.302` as a direct dependency, so a plain Aspire bump does not lift the floor; the
  condition is "Aspire's resolved MessagePack is 2.5.303+", as the comment says.
- Status: open, carried to gate-3 ("Deliberately left open"). Not sent back; a queue
  line is the cure if the human wants one.

#### Observation, not a defect

`status.md` carried `stage: 07-qa`, which is not a value of the `stage:` list in
`docs/features/README.md`; QA set it to `06-code-review` with this pass.
