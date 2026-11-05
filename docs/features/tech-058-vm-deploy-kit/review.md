# Review: tech-058-vm-deploy-kit

## Round 1

Reviewed: `git diff origin/main...HEAD` at `0e19fbbb` (whole diff, once), against
the plan in `status.md`, `qa.md` and ADR-0002. `bash -n` on the three scripts:
ok. Kit files `eol: lf`; `*.sh` committed 100755. Not run (by instruction):
install.sh, Docker, a release build, the full `verify.sh`.

QA fixes re-checked and holding: 1 (password via `ENVIRON`, heredoc to psql,
never argv), 2 (link removed on a failed `nginx -t`, cert/key made absolute),
3, 4 (`.healthy` gate on `previous`), 5 (rename swap), 6, 7 (role and
database separately), 8 (`val`), 9 (in install.sh only, see F7), 10, 11, 12,
13, 14 (root `out/` only, see F10), 15. 16 and 17 accepted as documented.

### Findings

**F1 — major — `deploy/vm/install.sh:123-127`.** `$env_file.tmp` is created
by a shell redirect under root's umask (022): `/etc/mustask/mustask.env.tmp`
is 0644 in a 0755 directory while it holds the generated DB password (and,
on a re-run after the suggested `DROP ROLE`, the Keycloak secret as well). If
the script dies between the redirect and the `rm`, the file stays. This is
the same short-lived exposure QA rated major as defect 1. The comment at :120-122
and vm.md §3 say the password is never exposed. Fix: `umask 077` at the top of
the script (the explicit `chmod 0640` calls already set the final modes), or
create the temp file with `install -m 0600 /dev/null` before the redirect.

**F2 — major — `deploy/vm/install.sh:182,201-207`.** `src=$(cd "$src" && pwd)`
keeps the *logical* path. `install.sh deploy /opt/mustask/current` (or
`/opt/mustask/previous`), a natural way to redeploy after the home-directory
tarball is gone, gives `src != target`. `cp -R` then copies the symlink as a
symlink, `mv "$target" "$target.old"` moves the real release aside,
`mv "$target.new" "$target"` makes the release path a symlink that loops onto
itself, and `rm -rf "$target.old"` deletes the live (or rollback) release.
The next `chmod -R` stops on the loop. This is an `rm -rf` driven by the
command's input. Fix: `src=$(cd "$src" && pwd -P)` (then `src == target` takes
the in-place branch), and refuse a `src` that resolves inside `$releases`
under another name.

**F3 — major (prose and default recipe) — `docs/deploy/vm.md:37-38,141-156`
(vm.ru.md the same).** The recipe for the target VM, which already has
PostgreSQL, passes `--install-packages`. `apt-get install … postgresql nginx`
on a box where they are installed upgrades them to the newest available
version. That restarts the shared PostgreSQL cluster and nginx, so every
other database and site on the VM goes down briefly. With a PGDG `postgresql`
metapackage it can also pull a newer major version and create a second
cluster. "Leave the flag out when the packages are installed another way"
does not cover the case this task is for. Fix: in both docs, say the flag
installs *and upgrades* the packages, and on a VM where PostgreSQL or nginx
already serve others, leave it out and install only
`aspnetcore-runtime-10.0`. Drop it from the whole-path example. Optionally,
make the script install only packages that are missing
(`dpkg -s … || apt-get install`).

**F4 — minor — `docs/deploy/vm.md:177-178` (vm.ru.md:177).** "If the role
already exists, nothing is touched" overstates what the code does. With the
role present, the script dies when the env file still has `Password=CHANGE_ME`
(`install.sh:113-114`) and creates the database if it is missing (:133-137).
An existing `mustask` database owned by someone else is accepted silently.
Fix: describe the three cases. Optionally, check `datdba` too.

**F5 — minor — `deploy/vm/install.sh:128-130`.** The password travels as a
literal inside `CREATE ROLE`. If that statement fails, PostgreSQL's default
`log_min_error_statement = error` writes the whole statement, password
included, into `/var/log/postgresql/*.log`, which group `adm` can read. Fix:
put `SET log_min_error_statement = panic; SET log_statement = 'none';` in the
same heredoc before `CREATE ROLE`.

**F6 — minor — `deploy/vm/install.sh:165-174`, `docs/deploy/vm.md:307-309`.**
(a) On a re-run of `nginx` whose test fails, a previously working MUSTASK site
loses its link and its old file is overwritten. MUSTASK disappears at the next
reload by anyone. Fix: back up the old file and link, and restore both on
failure (the second half of QA defect 2's fix). (b) "the other sites of that
nginx are never at risk" is too strong. On nginx < 1.25.1, `listen 443 ssl
http2` turns HTTP/2 on for every server on `*:443`. A server block without
`default_server` can also become the default for unmatched names on 443.
Fix: weaken the sentence and name both effects.

**F7 — minor — `deploy/vm/smoke.sh:28`.** `value()` does not strip
surrounding quotes, but `install.sh:45 env_value` now does (QA 9). With a
quoted `Session__PanelUrl`, deploy passes and every smoke check fails. Fix:
the same `sed` as in `env_value`.

**F8 — minor — `deploy/vm/build-release.sh:42`.** A `:port` is accepted in
the site URL, but nothing downstream supports it. The site listens on 443
only, `prepare`/`nginx --host` would put `host:port` into `server_name`, and
`smoke.sh:35` builds `--resolve host:port:443:…`. Fix: refuse a port (drop
`(:[0-9]+)?`) or document the extra steps.

**F9 — minor — `deploy/vm/install.sh:97,166,189-191`.** `install.sh` passes
`--host`, `--keycloak-host`, `--realm` and the cert paths unvalidated into a
`sed` replacement, where `&` or `|` corrupts the env file or the site. It also
trusts `tag=` from `VERSION` for the `rm -rf`/`mv` paths, where `..` would
target `/opt/mustask` itself. build-release validates the tag, but a
hand-edited `VERSION` bypasses that check. Fix: the same host regex as the
build, and in `deploy`, `^[A-Za-z0-9][A-Za-z0-9._-]*$` for the tag.

**F10 — minor — `deploy/vm/build-release.sh:33`, `.gitignore:51`.** The
default `out` is relative to the cwd, and the ignore rule covers only the repo
root. Run from `deploy/vm/`, the tarball lands untracked and unignored.
Fix: default to `$repo/out`.

### Behavioural claims (direction 1)

- vm.md:25 "Sections 2–9 are scripted": install.sh prepare/nginx/deploy/rollback, smoke.sh. The CA trust (§2) is manual and says so. Holds.
- vm.md:149 "prepare can be run again": install.sh:93-94, 85, 89. Holds.
- vm.md:152 "`--install-packages` runs apt-get install …": install.sh:77-82. Holds, but see F3.
- vm.md:165 "deploy stops waiting for /health": install.sh:232-237. Holds.
- vm.md:176-177 "password never printed": install.sh:118-131. Not printed, but see F1 and F5.
- vm.md:177-178 "If the role already exists, nothing is touched": F4.
- vm.md:194-196 "migrations … before it switches": install.sh:213-214 before :227-228. Holds (see F2 for the copy path).
- vm.md:197-198 "bundle applies only missing migrations": EF bundle semantics, design-time factory `MustaskDbContextFactory.cs:22` reads the env var. Holds.
- vm.md:216-218 "builds the tag … removes it afterwards": build-release.sh:51-54, 70-77. Holds.
- vm.md:228 "kit taken from the checkout": build-release.sh:104-108; `+dirty` at :57. Holds.
- vm.md:243-245 "prepare fills site, Keycloak, connection string": install.sh:97, 123-125. Holds.
- vm.md:248-249 "deploy refuses while CHANGE_ME": install.sh:47, 184-186. Holds.
- vm.md:254 "nginx site and install.sh use port 5000": nginx-mustask.conf:18, install.sh:54, smoke.sh:49. Holds.
- vm.md:282-284 unit contents: mustask-api.service:12-24. Holds.
- vm.md:297-298 "/health and /alive are not exposed": nginx-mustask.conf:17-33 (only `/api/` is proxied). Holds.
- vm.md:307-309 "disabled again … never at risk": install.sh:171-174. First half holds; the second half is F6.
- vm.md:332-336 deploy steps: install.sh:201-238. Holds. "Up to a minute" is true for a refused port; a hanging curl can take longer (not a finding).
- vm.md:338-343 smoke checks list: smoke.sh:49-91. Holds, one for one.
- vm.md:371-375 "previous only if it turned healthy … replaces in place": install.sh:223-226, 201-208. Holds.
- vm.md:378 "rollback current <-> previous": install.sh:250-252. Holds.
- vm.md:393-397 troubleshooting messages: install.sh:185, 196, 173, 108, 237. Holds.
- vm.md:417-420 tech-058 rehearsal record: status.md § Rehearsal findings and Log. This is a record, not checkable here.
- install.sh:18-19 / :121-122 comments: see F1.
- verify.sh:333-338 "its scripts get the shell checks": verify.sh:308, 633-660. Holds; the non-`.sh` kit files fall in `shell` and are skipped by the `.sh` case. As stated.
- Commit 0e19fbbb message: each item traced to the fixes above. Holds.
- vm.ru.md vs vm.md: code blocks identical bar translated comments and placeholders (scripted diff), headings 1–10 identical, the F3/F4/F6 sentences carry the same wording.

### Direction 2

The behaviour that changed is the VM deployment path. Grepped for
`deploy/vm`, `vm.md`, `vm.ru.md`, `build-release`, `mustask-api.service`,
`/opt/mustask`, `efbundle` and `migrations bundle` under `docs/`, `.claude/`
and the `AGENTS.md` files, excluding tech-056/058. Hits:
ADR-0033:109 (names vm.md only, still true), backlog.md:37-38 (queue lines),
tech-050 status.md (history), AGENTS.md:129-130 (vm.ru.md is the translation,
still true). ADR-0002 read: the bundle step in `deploy` is the "explicit
deployment step" it allows. None stale.

### Round verdict

Verdict: **needs-fixes** · blockers: 0 · majors: 3 (F1, F2, F3) · minors: 7
(F4–F10). F3 is prose only (plus an optional script change). F1 and F2 are
one-line script fixes. The delta read covers those three.
