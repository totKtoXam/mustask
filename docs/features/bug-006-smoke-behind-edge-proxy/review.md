# Code review: bug-006-smoke-behind-edge-proxy

## Round 1

Diff `origin/main...dca926f5`, 9 files. Spec = the plan in `status.md`.
`sh scripts/tests/cd-runner-cases.sh`: all passed (14 smoke.sh cases among them).

**Verdict: approve.** No blocker or major; three minors, left as debt.

What holds:

- Spec conformance: plan items 1-4 are all there. smoke.sh `--resolve-to` (smoke.sh:29-37, 54),
  `smoke_args_of` (mustask-cd.sh:74-97), runner-setup.sh `--smoke-args` (runner-setup.sh:83-98),
  tests, verify.sh trigger (verify.sh:841), vm.md 7.2/8/10/11.2 and the .ru translation.
- Default unchanged: with no flag `resolve` is empty and no `--resolve` is added (smoke.sh:54);
  pinned by the "no flags, no --resolve" case.
- Nothing weakened: `-k` only with `--insecure` (smoke.sh:53); every check keeps its expected
  value. Through the edge, "/health is not exposed" now actually tests the public hop - stronger.
- Security: the value is constrained to `[0-9.]` by the same regex in all three places before it
  reaches anything; it goes to curl as one argv element (`opts` array), never through `eval` or a
  shell string. `$args` unquoted in `cmd_smoke` (mustask-cd.sh:131) only ever carries words that
  passed the whitelist. The file stays root-owned 0644 (runner-setup.sh:154-155).
- Validator consistency: all three accept `--insecure`, `--resolve-local`, `--resolve-to=<a.b.c.d>`,
  refuse two different resolve flags; `smoke_args_of` drops an exact repeat before calling smoke.sh,
  so CD never hands smoke.sh a duplicate (see m-3 for the hand-run case).
- Tests discriminate: the stub curl reaches the "edge" only with `--resolve host:port:192.0.2.10`,
  so ignoring the flag fails the passing case; the request-log check catches a request that bypasses
  the edge; bad values are shown to exit 2 with an empty request log.

### Findings

- **m-1 (minor) — deploy/vm/smoke.sh:35, mustask-cd.sh:82, runner-setup.sh:91.** QA D-1: the IPv4
  check is shape-only (`999.1.1.1` passes). I agree it is low: the charset closes injection, and a
  bad octet makes curl fail every proxied check loudly, so nothing passes wrongly. Fix if ever
  touched: an octet <= 255 check, ideally one shared helper so the three copies cannot drift. Not
  worth a task of its own.
- **m-2 (minor) — docs/deploy/vm.md:587, vm.ru.md:588.** The troubleshooting row says *every*
  `https://` check shows `curl: (60)`. In that state the script check reports
  `no <script src> in index.html` and the API/sign-in checks `got 000` / `got '000 '`; the `(60)`
  appears on stderr and in the panel/route checks. Nobody acts differently, but reword to "the
  `https://` checks fail, with `curl: (60) … self-signed certificate`".
- **m-3 (minor) — deploy/vm/smoke.sh:30.** smoke.sh now refuses an exact repeat
  (`--resolve-local --resolve-local` exits 2; before this diff it was accepted), while
  `smoke_args_of` and runner-setup.sh tolerate one. This matches the plan ("a repeat … exit 2") and
  CD dedups first, so only a hand-run is affected. Fix if wanted: let smoke.sh ignore an identical
  repeat like the other two.

Out of scope, pre-existing: `for word in $(tr …)` in `smoke_args_of` (mustask-cd.sh:79) and
`for w in $smoke_args` (runner-setup.sh:86) are subject to pathname expansion. A glob word that
expands still has to pass the whitelist, and the file is root-owned, so no defect today.

### Behavioural claims

- smoke.sh:12-15 "every check stays as strict; through the public hop" -> smoke.sh:53-54 (no `-k` added; `--resolve` on 80 and 443). Holds.
- mustask-cd.sh:16-17, runner-setup.sh:17-22 accepted flag set -> mustask-cd.sh:79-95, runner-setup.sh:86-98. Holds.
- runner-setup.sh:83-84 "the same rule as smoke_args_of" -> identical regex and one-resolve rule; both tolerate an exact repeat. Holds.
- cd-runner-cases.sh:18-19 "verify.sh runs it when … smoke.sh … changes" -> verify.sh:841. Holds.
- vm.md:460-461 (7.2) "needs `--resolve-to` when the name points at the VM" -> diagnosis in status.md Plan; smoke.sh:54. Holds.
- vm.md:488-494 "Ubuntu writes the VM's own name into /etc/hosts … http answers 200, https self-signed" -> observed on the production VM (status.md Plan); environmental, weak anchor, scoped by "on such a VM". Holds.
- vm.md:508-513 "certificate verified; redirect is the proxy's; panel, API, realm redirect through it; only one of the two; address change fails until updated" -> smoke.sh:29-37, 53-54, 75-127. Holds.
- vm.md:586 refused-file row -> mustask-cd.sh:79-95, 129. Holds.
- vm.md:587 troubleshooting symptom -> m-2.
- vm.md:672-675 (11.2) `--smoke-args --resolve-to=<edge address>` -> runner-setup.sh:86-98, 150-152. Holds.
- vm.ru.md: the same sentences, section by section (460, 488-513, 587-588, 674-677); translation matches, same claims.
- Direction 2: behaviour changed = accepted smoke flags (smoke.sh, `smoke_args_of`, `--smoke-args`); `git grep` for `resolve-local`, `smoke.sh`, `cd-smoke-args`, `smoke_args`. Hits: vm.md/vm.ru.md (all updated), build-release.sh:27,135 (copies the file, no flag claims), verify.sh, backlog.md and tech-057/058/059/066 artifacts (historical, describe their own tasks). No stale current-behaviour prose.
