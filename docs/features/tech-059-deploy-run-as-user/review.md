# tech-059 — code review

## Round 1

Reviewer: code-reviewer (clean context), 2026-11-06. Diff `origin/main...4ca4aa51` (5 files). Requirements: the plan in `status.md`. `bash -n deploy/vm/install.sh` passes; nothing else was run (no containers).

**Verdict: changes requested** (3 major, 3 minor).

### Findings

**F1 — major — `deploy/vm/install.sh:90`** (and `docs/deploy/vm.md:177`, `vm.ru.md:177-178`). `user=mustask` is the default even when `/etc/mustask/run-as` already holds another account. The runbook says "`prepare` can be run again", for example to add `--create-db` or `--install-packages` later. On a VM prepared with `--run-as ubuntu`, a re-run without the flag quietly switches the service back. It creates the `mustask` account, rewrites `run-as`, and regroups the env file and the state directory. The next `deploy` then runs the API as `mustask`, which is the account the human chose not to have. **Fix:** default to the recorded choice: `user=$(run_as)` before the argument loop, so only an explicit `--run-as` changes it. Say so in both runbooks ("left out, the recorded account is kept").

**F2 — major — `deploy/vm/install.sh:147`** (and `vm.md:180,183-184`, `vm.ru.md:181-185`). `chown "$user:$group" "$state"` changes only the directory itself. On a switch (the runbook says "to change it, run `prepare` again with another `--run-as` and deploy"), everything inside `/var/lib/mustask` stays with the old account. That covers the Data Protection key ring under `.aspnet/DataProtection-Keys`, plus the `/etc/skel` files from `useradd --create-home`. The new account cannot write new keys into the old account's directory, and cannot read the existing ones either if the ring is owner-only. Cookies stop decrypting, and key rotation fails, which is the "everyone is signed out" row at `vm.md:529`. **Fix:** `chown -R "$user:$group" "$state"`. The directory belongs to MUSTASK only, so a recursive chown is safe. Make the docs' "`/var/lib/mustask` to the user" true of its contents.

**F3 — major — `docs/deploy/vm.md:288-289`, `vm.ru.md:286-287`** (direction 2; not in the diff). Section 5 still says the env file is "root-owned, mode `0640`, group `mustask`". The diff makes the group that of the run-as account (`install.sh:158`), and its own sentence at `vm.md:179-180` says so. The same diff disproves the unchanged sentence. An operator checking permissions on the `ubuntu` VM sees `root:ubuntu`, and may "fix" it. **Fix:** "group of the service account (`mustask` by default, section 2)", in both files.

**F4 — minor — `deploy/vm/install.sh:142,326-328,365-366`.** The group name never passes a check before it goes into the `sed` replacement and into `Group=`. `id -gn` can return a directory-service name with a space, `@` or `\`. `&` or `|` would corrupt the sed expression. A gid with no name makes `id -gn` fail, and under `set -e` that kills the script with only id's own message. `deploy` also takes `run-as` back without re-checking it against the regex or against uid 0. The file is root-owned 0644, so this is defence in depth, not an injection hole. **Fix:** drop `Group=@RUN_GROUP@` from the unit. With `User=` set, systemd uses that user's primary group by default. In `deploy`, run the same regex and the not-root check on `$(run_as)` that `prepare` runs.

**F5 — minor — `docs/deploy/vm.md:177-184`, `vm.ru.md:178-185`.** The runbook presents `--run-as ubuntu` without its cost. The account's other processes can read `mustask.env` (group read) and `/proc/<pid>/environ`, so they get the DB password and the client secret. They can also read and replace the Data Protection keys. The service also inherits the account's supplementary groups through initgroups. `NoNewPrivileges` stops `sudo`, but not `docker`: connecting to the docker socket is allowed on a `ProtectSystem=strict` mount. Separately, `ProtectHome=true` hides `/home`, so an `ExecStart` that points at a `dotnet` under `~ubuntu/.dotnet` fails. **Fix:** add one sentence on what sharing the account gives up, and note that `dotnet` has to live outside `/home`. The human's choice stands, and this only records it.

**F6 — minor — `deploy/vm/install.sh:19-23`.** The header comment reads "With --run-as is the account the service runs as". **Fix:** "--run-as is the account …".

### Checked, no finding

- Existing install with no `run-as` file: `deploy` falls back to `mustask` (`install.sh:73`), which is what the old `prepare` created. The rendered unit is the same as before.
- Root refusal: by uid (`install.sh:115`), so it also catches a uid-0 alias. A not-yet-created `mustask` gives an empty uid and passes, which is correct.
- User regex `^[a-z_][a-z0-9_-]*$`: nothing sed or systemd would misread can pass it. It rejects `.`, uppercase and `$`, which is stricter than useradd and acceptable.
- `ProtectSystem=strict` + `ReadWritePaths=/var/lib/mustask` + `HOME=/var/lib/mustask`: these are independent of the account, and `ProtectHome=true` does not touch `/var/lib`. `ubuntu`'s real home is not needed at runtime.
- Rollback does not re-render the unit. It keeps the one the last deploy wrote, which already has the current account.
- `deploy` refuses when the recorded user is gone (`install.sh:328`) before anything is copied.
- vm.ru.md matches vm.md in all three changed passages (sections 2 and 6).
- LF endings on all changed files (`git ls-files --eol`).

### Behavioural claims (direction 1)

- vm.md:179 "no `mustask` user is created": anchored at `install.sh:137`.
- vm.md:179-180 "env file goes to that user's group and `/var/lib/mustask` to the user": the env half is anchored at `install.sh:158`. The state half → F2, because only the directory is moved.
- vm.md:180-181 "never creates an account other than `mustask` and refuses root": anchored at `install.sh:113-115,137`.
- vm.md:181-183 "kept in `/etc/mustask/run-as`, and `deploy` writes it into `User=`/`Group=`": anchored at `install.sh:145,327,365-366`.
- vm.md:183-184 "to change it, run `prepare` again with another `--run-as` and deploy": the switch half → F2. Running `prepare` again without the flag → F1.
- vm.md:206 "the unit sets `HOME=/var/lib/mustask` whatever user it runs as": anchored at `mustask-api.service:16`.
- vm.md:333 "as the user of `/etc/mustask/run-as` (`mustask` by default)": anchored at `install.sh:73,365`.
- install.sh:20-23 header: the same anchors as vm.md:179-183.

### Behavioural claims (direction 2)

- The changed behaviour is the service account and the env file's group. I grepped `service user|home directory|User=|chown|/var/lib/mustask|group .mustask` and their Russian forms over `docs/deploy`, `deploy/`, `docs/adr` and `src/backend/AGENTS.md`. The hits were `vm.md:174` (still true for the default), `vm.md:288-289` / `vm.ru.md:286-287` (→ F3) and `vm.md:529` / `vm.ru.md:528` (true). `build-release.sh` and `smoke.sh` do not mention the account. I did not sweep feature artifacts of earlier tasks (tech-050/056/058), because they are history.

## Round 2 (delta)

Reviewer: code-reviewer, 2026-11-06. Delta `cce1c9f1...313699da` (install.sh, the unit, vm.md, vm.ru.md, status.md). `bash -n deploy/vm/install.sh` passes. The re-check run is the orchestrator's container run in the `status.md` Log; nothing was re-run here.

**Verdict: approve** (0 open; one note below, not a finding).

- **F1 fixed.** `install.sh:92-93` seeds `user=$(run_as)` before the loop, so a re-run without `--run-as` keeps the recorded account. The value from the file still passes the regex, existence and not-root checks at `install.sh:114-118`. The header (`:19-24`), `vm.md:183` and `vm.ru.md:184-185` say so.
- **F2 fixed.** `chown -R "$user:$group" "$state"` (`install.sh:153`). The path is a constant directory owned only by MUSTASK.
- **F3 fixed.** Section 5 now names the service account's group: `vm.md:294`, `vm.ru.md:293`.
- **F4 fixed.** `Group=` and `@RUN_GROUP@` are gone. The unit's comment says systemd takes the primary group, and `git grep` finds no leftover. `prepare` checks the group name (`install.sh:146-147`), which now feeds only `chown`. `deploy` re-checks the recorded name against the regex and refuses uid 0 (`install.sh:334,336`) before anything is copied. The sed input is again only a regex-checked name.
- **F5 fixed.** The cost of a shared account (secrets, keys, inherited groups, `dotnet` outside `/home`) is in `vm.md:184-188`. `vm.ru.md:186-190` matches it.
- **F6 fixed.** The header grammar.
- Claims re-anchored: "deploy writes it into the unit's `User=`" → `install.sh:372`. "a re-run … keeps the recorded account" → `install.sh:92-93`. "the service gets that account's groups" holds because systemd initialises supplementary groups for `User=`. Direction 2: `RUN_GROUP|Group=` over `deploy/` and `docs/deploy` finds only the new unit comment.
- Note: a primary gid with no name still stops `prepare` at `id -gn` (`install.sh:145`) under `set -e`, with id's own message, before the new check runs. It fails closed, so this is no defect.
