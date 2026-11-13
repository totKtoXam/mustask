# QA report: bug-006-smoke-behind-edge-proxy

Verdict: no blocking defects; 1 low defect, 1 test gap closed.

## Test plan (written from status.md before reading the code)

1. Default (no flags) smoke.sh unchanged: no `--resolve` reaches curl.
2. `--resolve-to=<edge>` sends every proxied request to the edge on 80 and 443; the VM-own hop still fails; a wrong address fails; `--insecure` combines.
3. Bad flags (empty, host name, short, `;id`, both resolve flags, two addresses) exit 2 before any request.
4. Flags file via `smoke_args_of`: valid, repeat, empty, name, shell tail, two words, two addresses, both flags.
5. runner-setup.sh `--smoke-args` accepts what `smoke_args_of` accepts (read, needs root).
6. Nothing in a flags file reaches a shell unsafely.
7. vm.md and vm.ru.md say the same.

## What ran

`sh scripts/tests/cd-runner-cases.sh`: all passed. The stub curl reaches the "edge" only when given `--resolve host:port:192.0.2.10`, so a regression where `--resolve-to` is ignored fails the "passes every check" case (exit 1), and the request-log case catches a request without the edge `--resolve`. It discriminates.

Added: "no flags, no --resolve (default unchanged)" case (gap in plan item 1).

By reading: runner-setup, smoke_args_of and smoke.sh agree on the rule (one resolve flag, exact repeat tolerated, IPv4-shaped value). The same regex is used everywhere and the word is only matched by `case`/`grep`, then printed; never evaluated. EN/RU docs match section by section.

## Defects

- D-1 (low): the IPv4 check is shape-only, so `999.1.1.1` is accepted by all three scripts. It fails loudly later (curl error in smoke), no safety impact. Left as is.
- Not covered: runner-setup.sh validation has no automated test (needs root); reviewed by reading only.
