# tech-061-deploy-forwarded-proto — code review

## Round 1

Diff: `git diff origin/main...HEAD`, commit 36a10330 (5 files). The reviewer
checked the branch, read the code, ran `bash -n deploy/vm/install.sh` and ran
both sed substitutions on the template (`$scheme` and `https` both render
line 18 correctly). No server was touched and the full `verify.sh` was not
run.

**Verdict: approve.** No blockers and no majors. Two minors are left as debts.

### What was checked

- **sed value.** `$` is literal in a sed replacement, so the only characters
  that matter are `|`, `&` and `\`. The value is always `$scheme`, `https` or
  `http`, because the `case` at install.sh:300-303 runs before
  `cmd_nginx_locations`. `'$scheme'` is single-quoted at :238 and expands
  nowhere.
- **`set -euo pipefail`.** `head … 2>/dev/null || true` (:298) inside a
  command substitution survives a missing file. An empty file falls through
  to `${proto:-scheme}`. `$2` at :229 is always passed. `val` rejects an
  empty value and a value that starts with `--`.
- **Sync between the state file and the snippet.** `/etc/mustask/forwarded-proto`
  is written only after both `nginx -t` runs pass (:268-270). Every failure
  path (:248-249, :258-264) dies before that point, so a restored or removed
  snippet leaves the old state file in place, and the two stay consistent. A
  failed `systemctl reload` (:274) exits after both are written, and they
  still agree on disk. A host with no state file from before this change
  renders `$scheme`, which is byte-identical to the old snippet.
- **Refusals.** A bad value is refused at :302. The flag with `--host` (or
  with neither mode) is refused at :308, before any file is touched. A
  missing value is refused by `val`.
- **Usage header and docs against the code.** The synopsis (:10) and the
  paragraph (:36-41) match :291-308. vm.md §7.2 matches as well.
- **vm.ru.md against vm.md.** The new paragraph is a faithful translation:
  same command, same file, same `scheme` reset, same restriction to `--path`.

### Findings

**F1 — minor — docs/deploy/vm.md:544, docs/deploy/vm.ru.md (same row).**
The troubleshooting row «Keycloak shows "Invalid redirect URI"» still gives
only two causes: a missing redirect URI, or a missing
`ASPNETCORE_FORWARDEDHEADERS_ENABLED`. It does not give the cause this task
exists for: TLS ends on a proxy in front of the portal and the snippet sends
`$scheme`. An operator with the env flag set gets no lead from the table. The
row is incomplete, not wrong, and §7.2 does cover the case.
*Fix:* add to the row in both files "or, under a path behind a TLS-ending
proxy, the snippet was installed without `--forwarded-proto https`
(section 7.2)".

**F2 — minor — deploy/vm/install.sh:298-302.**
When the value comes from the state file, a bad value (or `https\r` from a
hand edit made on Windows) is refused as "--forwarded-proto 'x' must be …",
even though the operator never passed that flag.
*Fix:* when the flag was not given, name `$proto_file` in the message, for
example "… (from /etc/mustask/forwarded-proto)".

### Note (not a finding)

The backlog line says the live VM of tech-058 has `https` in its snippet,
edited by hand. That VM has no state file, so its first re-run of
`nginx --path` must pass `--forwarded-proto https` once. Without the flag it
renders `$scheme` again. Overwriting a hand edit on a re-run is existing
behaviour, and §7.2 tells the operator to pass the flag. The tech-058
follow-up should know about this.

### Behavioural claims in prose

- install.sh:36-38 "by default nginx's own $scheme": anchored to :299 and :238.
- install.sh:39-41 "kept in /etc/mustask/forwarded-proto; a re-run without it keeps the recorded one": anchored to :269 and :298.
- install.sh:41 "`scheme` goes back to the default": anchored to :238-239 and :269.
- nginx-mustask-locations.conf:9-10 "@FORWARDED_PROTO@ is $scheme or https/http": anchored to install.sh:238-240.
- vm.md:442-446 (and ru) "default $scheme; behind a TLS-ending proxy that is http and the API builds http:// addresses": anchored to the template line 18. Keycloak's refusal of a URI that is not registered is that system's behaviour, already relied on at vm.md:308 and :544.
- vm.md:452-454 (and ru) "kept in the file; a later run without the flag keeps https; `scheme` goes back": anchored to install.sh:269, :298 and :238.
- vm.md:454-455 (and ru) "the flag is only for --path": anchored to install.sh:308.
- install.sh:271 prints the effective value: the code is the claim.
- Direction 2: the changed behaviour is the X-Forwarded-Proto the snippet sends. The sweep grepped `X-Forwarded-Proto`, `$scheme`, `forwarded-proto`, `nginx --path` and `FORWARDEDHEADERS` under docs/, deploy/ and src/backend. Hits: vm.md:65, :417 and :578 are still true, because the flag is optional. vm.md:308 is still true. vm.md:544 is incomplete (F1). nginx-mustask.conf:23 keeps `$scheme` on purpose (whole-site mode). The tech-050 log line is history. Not swept: `.claude/` and the `AGENTS.md` files, because they hold no deploy-kit prose.
