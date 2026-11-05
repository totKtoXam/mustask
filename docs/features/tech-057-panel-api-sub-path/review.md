# tech-057 — code review

## Round 1

Reviewer: code-reviewer (clean context), 2026-11-05. Diff `origin/main...5bc6ecdf` (28 files). Requirements: ADR-0037, the plan in `status.md`, `qa.md`.

**Verdict: changes requested** (2 major, 3 minor).

### Findings

**F1 — major — `deploy/vm/nginx-mustask-locations.conf:11,36`** (and `docs/deploy/vm.md:426-428`, `vm.ru.md:419-421`). Both locations are plain prefix locations. In nginx a matching regex location wins over a plain prefix, and a regex for static files (`location ~* \.(js|css|png|svg)$ { expires …; }`) is common in a portal's server. That regex would take `/mustask/assets/*.js` and serve the portal's root for it, so the panel comes up blank. A regex like `~ /api/` would take `/mustask/api/` the same way. The snippet exists only to sit inside a server nobody here controls, and the rehearsal could not cover that (vm.md, "Still unverified"). The runbook hands the risk to the portal's owner, but the kit can rule it out with one token. **Fix:** `location ^~ @BASE_PATH@/api/` and `location ^~ @BASE_PATH@/`. Then shorten the vm.md and vm.ru.md paragraph to the prefix clash only. A regex location can no longer shadow these.

**F2 — major — `deploy/vm/install.sh:217-219`** (and `docs/deploy/vm.md:422-423,514`, `vm.ru.md:417,509`). The probe passes, `mv` replaces the installed snippet with no backup, and then the whole-config `nginx -t` runs. If that fails, the script dies with "the snippet is not included by anything yet". On a re-run with the include already in place that message is false, and the new snippet stays on disk under a failing configuration. Any later reload or restart then hits it. The runbook says "If the test fails, the snippet installed earlier is left as it was", and an operator would rely on that. **Fix:** do what `cmd_nginx` does (`install.sh:262-279`). Keep `$snippet.bak` before the `mv`, and when the full `nginx -t` fails, restore it, or remove the snippet when there was none. Then die with "restored" or "removed". Make both docs say that.

**F3 — minor — `src/backend/Mustask.Api/Features/Session/ReturnUrlValidator.cs:11-22`.** `IsLocalPath` accepts dot segments: `/../x`, and `/%2e%2e/x`, which browsers also treat as `..`. At a root this changes nothing. Under a base, the redirect `{PanelUrl}{returnUrl}` = `https://portal/mustask/../x` resolves in the browser to `https://portal/x`. A crafted sign-in link then lands the user on any path of the portal after login. It is the same host, so this is not an off-site redirect, but it does leave MUSTASK, and this task creates that exposure. **Fix:** reject a `.` or `..` segment, plain or percent-encoded, in `IsLocalPath`, and add two cases to the validator's tests.

**F4 — minor — `docs/deploy/vm.md:514`, `vm.ru.md:509`.** The troubleshooting row quotes `install: nginx -t failed` for `--path`. That is the 7.1 message. The `--path` run prints `install: the snippet failed nginx -t; …` (`install.sh:214`). **Fix:** quote the real text. This can go into F2's edit.

**F5 — minor — `docs/deploy/vm.ru.md:388`.** The translation names `deploy/vm/nginx-mustask-locations.conf` as plain code. The English source at `vm.md:392` links it. **Fix:** add the same link.

### QA defects re-checked

- **D-1 fixed.** `WorkItemCard.tsx:530` uses `pagePath: withBase(...)`. `pagePath` has one consumer (`useCardParts.tsx:92` → `CardHeaderBar.tsx:101`), and no other raw absolute `href` or `location.*` is left in `src/` (grep).
- **D-2 fixed.** `SessionOidcOptions.cs:93-104` covers it, with the test `EmptyPathBaseWithPanelUrlPath_HostDoesNotStart`. The Aspire stand's PanelUrl has no path (`AppHost.cs:481`), so the stand is unaffected.
- **D-3:** the formatting is fixed (`SessionAuthenticationSetup.cs:40`). `normaliseBase` still does not validate (`vite.config.ts:18-21`). That stays a debt, because the kit validates the base before the build.

### Checked, no finding

- Open redirect off-host: `returnUrl` must still start with `/` and not `//` or `/\`, and `PanelUrl` comes from configuration.
- `redirect_uri` and correlation cookies: they follow `PathBase` (PathBaseTests:362-368).
- Session cookie path: `PathBase + /api/session` (`SessionAuthenticationSetup.cs:107`). No other code deletes or sets that cookie by path.
- Location middleware: it captures `PathBase` before `OnStarting` runs. It leaves alone an absolute address, `//`, a relative address with no slash, and a request without the prefix. No `CreatedAtRoute`/`LinkGenerator` exists to double the prefix.
- `UsePathBase` is first and applies only when the base is set, so the root is unchanged for the stand and CI.
- nginx `alias /…/panel/` inside `location /mustask/`: both ends have the slash, so there is no off-by-slash traversal. `/mustaskx` does not match `/mustask/`. `= /mustask` is exact. `try_files` falls back through an internal redirect into the same location.
- `sed` inputs: they are validated before substitution (`base_path`, `host_name`).
- The env template: `@HOST@@BASE_PATH@` substitutes in the right order.
- `deploy`: it compares the `VERSION` site with `PanelUrl`, so the whole chain from build to start is checked.

### Behavioural claims (direction 1)

- ADR-0037 §2 "refuses to run when malformed / PanelUrl does not end with it" → `SessionOidcOptions.cs:93-104,116-141`. The code is stricter: the path must be equal.
- ADR-0037 §2 "redirect_uri and correlation cookies follow with no code" → PathBaseTests:362-368.
- ADR-0037 Option 2 "a request without the prefix (/health) still works" → PathBaseTests:357.
- ADR-0037 §2 "sign-out goes to {PanelUrl}/signed-out" → `SessionEndpoints.cs:326`.
- ADR-0037 §3 "returnUrl and /signed-out inside the panel" → `AuthContext.tsx:64,83` + tests.
- ADR-0037 §3 "copied link adds the base" → `WorkItemCard.tsx:258` + actions test.
- ADR-0037 Consequences "mismatch fails the deploy's health wait" → the API throws at start, which fails `wait_health`.
- `src/backend/AGENTS.md:146-152` (base rules, cookie path, redirect URI) → `ParsePathBase`, `:107`, tests.
- `.env.example` "accepts `/mustask` and `/mustask/`" → `vite.config.ts:18-21`.
- vm.md §4 "deploy refuses a release whose address differs" → `install.sh:305-308`.
- vm.md §4 "checks the path reached the panel build" → `build-release.sh:127-131`.
- vm.md §4 "MSYS_NO_PATHCONV on the npm line only" → `build-release.sh:124`.
- vm.md §5 "the API refuses to start when they differ; `prepare --path` fills both" → the template, `install.sh:135-137`.
- vm.md 7.2 "nginx -t in a throwaway server, then the whole configuration" → `install.sh:208-219`.
- vm.md 7.2 "a later run reloads once the include exists" → `install.sh:221-223`.
- vm.md 7.2 "if the test fails, the earlier snippet is left as it was" → **F2**, true only for the probe step.
- vm.md 7.2 "/mustask/health is not exposed" → conf `:36-38` + `smoke.sh` health check.
- vm.md 7.2 "regex locations must not shadow" → **F1**.
- vm.md §8 "smoke checks the first script" → `smoke.sh` js block.
- vm.md §8 "the http check expects the portal's redirect with the path" → `smoke.sh:59-63`.
- vm.md troubleshooting "the snippet failed" → **F4**.
- vm.ru.md is the same content as vm.md, apart from F5.

### Direction 2

- Behaviours swept: sub-path support, the cookie path `/api/session`, and the new settings.
- Identifiers grepped: `sub-path|tech-057|host root|root of its host|VITE_BASE_PATH|Hosting__PathBase` and `cookie path|path /api/session` over `docs/`, `.claude/`, `src/*/AGENTS.md`, `deploy/`.
- Hits:
  - the merged tech-056 status: history, left as it is;
  - ADR-0022: accepted, and superseded in part by ADR-0037, which names it;
  - `docs/backlog.md`: the queue line;
  - `src/frontend/AGENTS.md`: no mention of `VITE_BASE_PATH`, which is fine, because `.env.example` carries it and no command changed.
- No stale prose found. Not swept: `docs/conventions/frontend.md`, for the rule that new absolute panel addresses must use `withBase`. Only ADR-0037 states that rule.

### Verified by running

- `npx vitest run` on basePath, AuthContext, WorkItemCardDialog and WorkItemCard.actions: 52 passed.
- `dotnet test --no-build --filter PathBaseTests`: 30 passed. A plain build was locked by an already-running `testhost.exe` (pid 79196) from this worktree's `bin`, which was not started by this review.
