#!/bin/sh
# Verification proportionate to the change.
#
# Looks at which files changed and runs only the checks that make sense for
# them: a README edit doesn't spin up Testcontainers, but a
# Directory.Packages.props edit does — that's a dependency graph for the
# whole solution, not "just a config".
#
#   scripts/verify.sh                    run the checks
#   scripts/verify.sh --list             only show the classification
#   VERIFY_FULL=1 scripts/verify.sh      the whole frontend suite
#
# Comparison base is main, overridden by the BASE variable.
# An unknown path is treated as code: missing a breakage costs more than an
# extra build.
#
# Frontend tests (tech-028): by default Vitest runs only the tests that reach
# a file changed since the merge-base with $BASE (`vitest run --changed`), and
# the whole suite when there is no merge-base. A change to the test harness,
# the theme or the kit (forceRerunTriggers in vite.config.ts) runs everything
# by itself. Tests that read sources off disk (the kit guard, the colour scan,
# the e2e guards, the error-code readers) are found by grep and always run next
# to the affected ones: no import ever reaches them. VERIFY_FULL=1 forces the
# whole suite; scripts/finish-task.sh and .githooks/pre-push set it, so a merge
# or a push is always gated by a full run. A full run holds a lock in the
# shared git directory (<git-common-dir>/vitest-full.lock): one full run at a
# time across all worktrees, the rest wait. A lock older than 60 minutes or
# held by a dead process is taken over. Affected-only runs take no lock.
#
# Green stamp (tech-030): a green VERIFY_FULL=1 run on a clean tree (and the
# tree still the same when the checks end), with no check skipped, writes
# <git-common-dir>/verify-stamps/<tree>-<merge-base>. The next VERIFY_FULL=1
# run of the same tree and merge-base — the same code, a clean working tree —
# returns green at once, so QA, review, finish-task.sh and pre-push do not
# repeat a suite that already passed. It is a statement about the code on this
# machine: the stamp lives in the git directory every worktree shares, but
# node_modules, ignored local files and Docker are each worktree's own, so
# after changing the environment run with VERIFY_FORCE=1. A run that goes on
# (forced, or a miss) deletes the old stamp first: a red or partial run never
# leaves a green one behind.

set -eu

BASE=${BASE:-main}
VERIFY_FULL=${VERIFY_FULL:-0}
# The fast tier (npm run test:fast, tech-029) is a development aid: whatever
# the caller exported, a verification never runs with it.
unset VITEST_FAST
LIST_ONLY=0
[ "${1:-}" = "--list" ] && LIST_ONLY=1

# We check not just that the command exists but that it runs: WindowsApps
# often puts a broken python.exe shim on PATH. On Git for Windows we fall
# back to a local runtime; the PowerShell adapter puts it on PATH anyway.
find_python() {
    for candidate in python3 python; do
        if command -v "$candidate" >/dev/null 2>&1 &&
           "$candidate" -c "import sys" >/dev/null 2>&1; then
            printf "%s\n" "$candidate"
            return 0
        fi
    done

    if [ -n "${LOCALAPPDATA:-}" ] && command -v cygpath >/dev/null 2>&1; then
        local_app_data=$(cygpath -u "$LOCALAPPDATA" 2>/dev/null || true)
        local_python="$local_app_data/Python/bin/python.exe"
        if [ -x "$local_python" ] && "$local_python" -c "import sys" >/dev/null 2>&1; then
            printf "%s\n" "$local_python"
            return 0
        fi
    fi

    return 1
}

# VERIFY_PYTHON overrides the search (an empty value means "no python"): the
# stamp cases need a run that skips a check.
if [ "${VERIFY_PYTHON+set}" = set ]; then
    PYTHON_COMMAND=$VERIFY_PYTHON
else
    PYTHON_COMMAND=$(find_python || true)
fi

failed=""
passed=""

note() { printf "%s\n" "$1"; }
# A check that could not run (no Docker, no python, ...): the run may still be
# green, but it must not leave a stamp that says the tree was fully verified.
unchecked=0
skipped() { unchecked=1; note "$1"; }
fail() { failed="$failed  - $1
"; printf "  FAIL: %s\n" "$1" >&2; }
pass() { passed="$passed  - $1
"; printf "  ok: %s\n" "$1"; }

merge_base=$(git merge-base HEAD "$BASE" 2>/dev/null || echo "")

# --- One full Vitest run at a time ---------------------------------------------------
# Several agents in several worktrees each running the whole jsdom suite
# starve each other of cores and turn timeouts into false failures. The lock is
# a directory (mkdir is atomic) in the git directory all worktrees share, with
# owner.txt: pid, worktree, start time (epoch seconds). Waiting is unbounded
# on purpose, but a lock never outlives its owner or an hour.
vitest_lock_dir=""
vitest_lock_held=0
vitest_lock_max_age=3600

vitest_lock_field() { sed -n "s/^$1: //p" "$vitest_lock_dir/owner.txt" 2>/dev/null | head -n 1; }

vitest_lock_release() {
    [ "$vitest_lock_held" = 1 ] || return 0
    vitest_lock_held=0
    # Only our own: after a takeover the directory belongs to someone else.
    [ "$(vitest_lock_field pid)" = "$$" ] && rm -rf "$vitest_lock_dir"
    return 0
}

vitest_lock_acquire() {
    common=$(git rev-parse --path-format=absolute --git-common-dir 2>/dev/null || true)
    if [ -z "$common" ]; then
        note "  no git directory — the full run goes without a lock"
        return 0
    fi
    vitest_lock_dir="$common/vitest-full.lock"
    trap vitest_lock_release EXIT
    trap 'exit 130' INT TERM
    waited=0
    unreadable=0
    while :; do
        if mkdir "$vitest_lock_dir" 2>/dev/null; then
            {
                printf "pid: %s\n" "$$"
                printf "worktree: %s\n" "$(git rev-parse --show-toplevel)"
                printf "started: %s\n" "$(date +%s)"
            } > "$vitest_lock_dir/owner.txt"
            vitest_lock_held=1
            return 0
        fi
        owner_pid=$(vitest_lock_field pid)
        owner_started=$(vitest_lock_field started)
        stale=""
        if [ -z "$owner_pid" ] || [ -z "$owner_started" ]; then
            # Between the owner's mkdir and its owner.txt, or a broken lock:
            # give the first a moment, then treat it as the second.
            unreadable=$((unreadable + 1))
            [ "$unreadable" -gt 10 ] && stale="no owner.txt"
        else
            unreadable=0
            now=$(date +%s)
            if [ $((now - owner_started)) -gt "$vitest_lock_max_age" ]; then
                stale="held for over 60 minutes by pid $owner_pid"
            elif ! kill -0 "$owner_pid" 2>/dev/null; then
                stale="owner pid $owner_pid is dead"
            fi
        fi
        if [ -n "$stale" ]; then
            # rename is atomic: of several waiters that all judged the lock
            # stale only one gets the directory, and it checks below whose
            # it was. A `kill -0` blind spot remains: a pid written by a
            # different runtime (WSL bash) looks dead — the 60 minutes still
            # bound it.
            grave="$vitest_lock_dir.stale.$$"
            if mv "$vitest_lock_dir" "$grave" 2>/dev/null; then
                # We may have renamed a lock another waiter took in between:
                # check whom the renamed directory belonged to, and put a
                # live one back (best effort — a third mkdir may have won).
                grave_pid=$(sed -n "s/^pid: //p" "$grave/owner.txt" 2>/dev/null | head -n 1)
                if [ "$grave_pid" = "$owner_pid" ] || [ -z "$owner_pid" ]; then
                    note "  stale full-run lock removed ($stale)"
                    rm -rf "$grave"
                elif ! mv "$grave" "$vitest_lock_dir" 2>/dev/null; then
                    rm -rf "$grave"
                fi
            fi
            unreadable=0
            continue
        fi
        if [ $((waited % 30)) -eq 0 ]; then
            note "  waiting for another full run: pid $owner_pid in $(vitest_lock_field worktree)"
        fi
        sleep 2
        waited=$((waited + 2))
    done
}

changed=$(
    {
        [ -n "$merge_base" ] && git diff --name-only "$merge_base" HEAD
        git diff --name-only HEAD
        git ls-files --others --exclude-standard
    } 2>/dev/null | sort -u
)

if [ -z "$changed" ]; then
    note "No changes — nothing to verify."
    exit 0
fi

# prose    text, executed by no one
# agents   agent instructions: text with links to repository files
# inert    has no effect on the build or on behavior
# shell    scripts and git hooks
# ci       CI workflow
# dotnet   code and everything that participates in the backend build
# frontend frontend code and configuration
# provider the Keycloak provider and the image that carries it (ADR-0025):
#          built and unit-tested in Docker, then the .NET tests tagged
#          KeycloakProvider — Docker is required, not optional
classify() {
    case "$1" in
        .githooks/*|scripts/*|*.sh)                  echo shell ;;
        .github/workflows/*)                         echo ci ;;
        .claude/*|.agents/*|.codex/*|AGENTS.md|CLAUDE.md)
                                                     echo agents ;;
        # scoped instructions living inside a source tree: text,
        # not code — before the frontend/dotnet patterns, otherwise editing
        # a command list would build the solution and run every test
        src/*/AGENTS.md|src/*/CLAUDE.md)             echo agents ;;
        src/frontend/*)                              echo frontend ;;
        contracts/errors/*)                          echo dotnet ;;
        # The Keycloak provider (mod-014, ADR-0025 § 1): Java built and
        # tested only inside the image build — before the other
        # deploy/keycloak patterns.
        deploy/keycloak/providers/*|deploy/keycloak/Dockerfile|deploy/keycloak/.dockerignore)
                                                     echo provider ;;
        # Keycloak realm-import — a contract artifact: checked by
        # KeycloakRealmRolesTests from Mustask.Contracts.Tests (dotnet) — and
        # imported by a real Keycloak, which no file test can promise (mod-016
        # D-16 OP-10: an import Keycloak rejects only shows when it is
        # started): the provider bucket runs the live tests tagged
        # KeycloakProvider, LiveRealmImportTests among them. Two buckets.
        deploy/keycloak/realms/*)                    echo "dotnet provider" ;;
        # Login themes: KeycloakRealmRolesTests checks that the theme exists
        # and that the files listed in its `styles` exist. It can't check how
        # the page looks — after a Keycloak version bump it still needs a
        # look by eye on a running stand.
        deploy/keycloak/themes/*)                    echo dotnet ;;
        Directory.*.props|*.csproj|*.sln|global.json|.editorconfig|nuget.config)
                                                     echo dotnet ;;
        src/*)                                       echo dotnet ;;
        docs/*|*.md)                                 echo prose ;;
        .gitignore|.gitattributes|LICENSE|.vscode/*) echo inert ;;
        *)                                           echo dotnet ;;
    esac
}

need_prose=0
need_agents=0
need_shell=0
need_ci=0
need_dotnet=0
need_frontend=0
need_provider=0

note "Changed files:"
for file in $changed; do
    buckets=$(classify "$file")
    printf "  %-9s %s\n" "$buckets" "$file"
    # A file may belong to more than one bucket (a realm import: dotnet and provider).
    for bucket in $buckets; do
        case "$bucket" in
            prose)    need_prose=1 ;;
            agents)
                need_prose=1
                need_agents=1
                case "$file" in .codex/hooks/*.ps1) need_shell=1 ;; esac
                ;;
            shell)    need_shell=1 ;;
            ci)       need_ci=1 ;;
            dotnet)   need_dotnet=1 ;;
            frontend) need_frontend=1 ;;
            provider) need_provider=1 ;;
        esac
    done
done

note ""
if [ "$need_prose$need_agents$need_shell$need_ci$need_dotnet$need_frontend$need_provider" = "0000000" ]; then
    note "No checks needed: the changes affect nothing."
    [ "$LIST_ONLY" = 1 ] && exit 0
    note ""
    note "RESULT: green"
    exit 0
fi

[ "$LIST_ONLY" = 1 ] && exit 0

# --- The green stamp (tech-030) ---------------------------------------------------
# A green VERIFY_FULL=1 run on a clean tree leaves a stamp keyed by the tree
# hash and the merge-base (the second decides which buckets ran). The same
# code, tested once: finish-task.sh and pre-push, both VERIFY_FULL=1, find it
# and do not repeat the run. VERIFY_FORCE=1 ignores it. Never written from a
# dirty tree or from a run that skipped a check.
# Is the working tree exactly HEAD? By the exit code of `git status`, not by its
# silence (a failing `git status` prints nothing), and without files flagged
# assume-unchanged / skip-worktree, which `git status` hides.
tree_is_clean() {
    porcelain=$(git status --porcelain 2>/dev/null) || return 1
    [ -z "$porcelain" ] || return 1
    hidden=$(git ls-files -v 2>/dev/null | grep -E '^[hS] ' || true)
    [ -z "$hidden" ]
}

stamp_file=""
stamp_tree=""
stamp_commit=""
common_dir=$(git rev-parse --path-format=absolute --git-common-dir 2>/dev/null || true)
if [ "$VERIFY_FULL" = 1 ] && [ -n "$common_dir" ] && tree_is_clean; then
    stamp_tree=$(git rev-parse "HEAD^{tree}" 2>/dev/null || true)
    stamp_commit=$(git rev-parse HEAD 2>/dev/null || true)
    if [ -n "$stamp_tree" ]; then
        stamp_file="$common_dir/verify-stamps/$stamp_tree-$(printf '%.12s' "${merge_base:-none}")"
        if [ "${VERIFY_FORCE:-0}" != 1 ] && [ -f "$stamp_file" ]; then
            note "Already verified green: this tree and merge-base passed a full run on this machine (VERIFY_FORCE=1 to repeat)."
            sed 's/^/  /' "$stamp_file"
            note ""
            note "RESULT: green"
            exit 0
        fi
        # A run that goes on is a new verdict: the old stamp must not outlive a
        # red or partial one (`VERIFY_FORCE=1` that fails, a check that skips).
        rm -f "$stamp_file"
    fi
fi

# --- Prose ----------------------------------------------------------------------
if [ "$need_prose" = 1 ]; then
    broken=$(
        for file in $changed; do
            case "$file" in *.md) ;; *) continue ;; esac
            [ -f "$file" ] || continue
            dir=$(dirname "$file")
            grep -oE "\]\([^)]+\)" "$file" 2>/dev/null | sed "s/^](//; s/)$//" |
            while read -r link; do
                case "$link" in http*) continue ;; esac
                case "$link" in mailto:*) continue ;; esac
                case "$link" in "#"*) continue ;; esac
                target="$dir/${link%%#*}"
                [ -e "$target" ] || printf "%s -> %s\n" "$file" "$link"
            done
        done
    )
    if [ -n "$broken" ]; then
        printf "%s\n" "$broken" | sed "s/^/    /" >&2
        fail "broken links in documents"
    else
        pass "links in documents are intact"
    fi

    # The queue is docs/backlog.md plus docs/backlog/*.md: a
    # withdrawn id must not come back in Next either, so both are one set.
    if [ -f docs/backlog.md ]; then
        dups=$(cat docs/backlog.md docs/backlog/*.md 2>/dev/null | grep -oE "^- \[[ x]\] [a-z]+-[0-9]{3}" | grep -oE "[a-z]+-[0-9]{3}$" | sort | uniq -d || true)
        if [ -n "$dups" ]; then
            printf "    duplicates: %s\n" "$dups" >&2
            fail "duplicate ids in backlog"
        else
            pass "ids in backlog are unique"
        fi
    fi

    # status.md is current state, not a journal — docs/features/README.md,
    # "Size budget". Judged only for tasks still in flight: `stage: complete`
    # marks an archive, and an archive is never rewritten to fit a later rule
    # (that is also why editing an old folder — a translation, a link fix —
    # must not turn into a demand to restructure it).
    # Lines and bytes both: long lines beat a line count alone (feat-027 had
    # 73 lines and 35 KB).
    over=""
    warn=""
    long=""
    for file in $changed; do
        case "$file" in docs/features/*/status.md) ;; *) continue ;; esac
        [ -f "$file" ] || continue
        grep -q "^- stage: complete" "$file" && continue
        n=$(wc -l < "$file" | tr -d " ")
        b=$(wc -c < "$file" | tr -d " ")
        if [ "$n" -gt 250 ] || [ "$b" -gt 24576 ]; then
            over="$over $file:${n}L/${b}B"
        elif [ "$n" -gt 150 ] || [ "$b" -gt 12288 ]; then
            warn="$warn $file:${n}L/${b}B"
        fi
        # One Log entry, one line of at most 300 characters.
        k=$(LC_ALL=C.UTF-8 awk '{sub(/\r$/, "")} /^## /{inlog=($0=="## Log")} inlog && /^- / && length($0)>300{c++} END{print c+0}' "$file")
        [ "$k" -gt 0 ] && long="$long $file:$k"
    done
    if [ -n "$long" ]; then
        note "    Log entries over 300 characters, move the detail to the commit or review.md:$long"
    fi
    if [ -n "$warn" ]; then
        note "    over 150 lines or 12 KB, roll the oldest Log into history.md:$warn"
    fi
    if [ -n "$over" ]; then
        printf "    over 250 lines or 24 KB:%s\n" "$over" >&2
        fail "status.md has become a journal (docs/features/README.md, \"Size budget\")"
    else
        pass "status.md stays within its budget"
    fi

    # design.md, review.md and qa.md have budgets too — docs/features/README.md,
    # "Artifact budgets". A note, not a failure: designs in flight are already
    # far past it, and rewriting an approved design costs more than it saves.
    big=""
    for file in $changed; do
        case "$file" in
            docs/features/*/design.md|docs/features/*/review.md|docs/features/*/qa.md) ;;
            *) continue ;;
        esac
        [ -f "$file" ] || continue
        st="$(dirname "$file")/status.md"
        [ -f "$st" ] && grep -q "^- stage: complete" "$st" && continue
        b=$(wc -c < "$file" | tr -d " ")
        [ "$b" -gt 32768 ] && big="$big $file:$((b / 1024))KB"
    done
    if [ -n "$big" ]; then
        note "    over the 32 KB artifact budget (docs/features/README.md, \"Artifact budgets\"):$big"
    fi

    # spec.md and its stories state the target requirements; findings and
    # verdicts belong in review.md, and how a decision was reached belongs in
    # history.md.
    bad=""
    for file in $changed; do
        case "$file" in
            docs/features/*/spec.md|docs/features/*/stories/*.md) ;;
            *) continue ;;
        esac
        [ -f "$file" ] || continue
        sibling="$(dirname "$file" | sed 's#/stories$##')/status.md"
        if [ -f "$sibling" ] && grep -q "^- stage: complete" "$sibling"; then
            continue
        fi
        if grep -qiE "^#{2,4} *(review|verdict|findings|re-?check|orchestrator)" "$file"; then
            bad="$bad $file"
        fi
    done
    if [ -n "$bad" ]; then
        printf "    review history inside the spec:%s\n" "$bad" >&2
        fail "spec.md/stories carry a review/verdict section (findings go to review.md)"
    else
        pass "spec.md and its stories hold requirements only"
    fi
fi

# --- Agent instructions -----------------------------------------------------------
if [ "$need_agents" = 1 ]; then
    bad=""
    for file in $changed; do
        case "$file" in
            .claude/agents/*.md|.claude/commands/*.md|.agents/skills/*/SKILL.md) ;;
            *) continue ;;
        esac
        [ -f "$file" ] || continue
        head -n 1 "$file" | grep -q "^---$" || bad="$bad $file"
    done
    if [ -n "$bad" ]; then
        printf "    missing frontmatter:%s\n" "$bad" >&2
        fail "broken frontmatter in agent instructions"
    else
        pass "agent instructions frontmatter is in place"
    fi

    if [ -n "$PYTHON_COMMAND" ]; then
        bad=""
        for file in $changed; do
            case "$file" in .claude/*.json|.codex/*.json) ;; *) continue ;; esac
            [ -f "$file" ] || continue
            "$PYTHON_COMMAND" -c "import sys, json; json.load(open(sys.argv[1], encoding='utf-8'))" "$file" >/dev/null 2>&1 || bad="$bad $file"
        done
        if [ -n "$bad" ]; then
            printf "    invalid JSON:%s\n" "$bad" >&2
            fail "invalid JSON in AI configuration"
        else
            pass "JSON in AI configuration is valid"
        fi
    else
        skipped "  NOT CHECKED: no python, AI configuration JSON needs a manual check"
    fi

    if [ -n "$PYTHON_COMMAND" ]; then
        if "$PYTHON_COMMAND" scripts/validate-ai-workflow.py; then
            pass "Claude Code/Codex AI workflow compatibility"
        else
            fail "Claude Code/Codex AI workflow compatibility"
        fi
    else
        skipped "  NOT CHECKED: no python, AI workflow adapters not checked"
    fi

    touches_hooks=0
    for file in $changed; do
        case "$file" in
            .claude/hooks/*|.claude/settings.json) touches_hooks=1 ;;
            .codex/hooks.json|.codex/hooks/*) touches_hooks=1 ;;
        esac
    done
    if [ "$touches_hooks" = 1 ]; then
        if [ -n "$PYTHON_COMMAND" ]; then
            if "$PYTHON_COMMAND" .claude/hooks/tests/run_cases.py; then
                pass "offline table .claude/hooks/tests/run_cases.py"
            else
                fail "offline table .claude/hooks/tests/run_cases.py"
            fi
            if "$PYTHON_COMMAND" .claude/hooks/tests/run_adapter_cases.py; then
                pass "offline table .claude/hooks/tests/run_adapter_cases.py"
            else
                fail "offline table .claude/hooks/tests/run_adapter_cases.py"
            fi
        else
            skipped "  NOT CHECKED: no python, hook offline tables not run"
        fi

        codex_ps_command=""
        command -v pwsh >/dev/null 2>&1 && codex_ps_command=pwsh
        if [ -z "$codex_ps_command" ] && command -v powershell.exe >/dev/null 2>&1; then
            codex_ps_command=powershell.exe
        fi
        if [ -n "$codex_ps_command" ]; then
            if "$codex_ps_command" -NoProfile -ExecutionPolicy Bypass \
                -File .codex/hooks/tests/run-cases.ps1; then
                pass "Codex Windows hook: Bash to PowerShell adapter"
            else
                fail "Codex Windows hook: Bash to PowerShell adapter"
            fi
        else
            skipped "  NOT CHECKED: no PowerShell, Windows hook adapter not run"
        fi
    fi
fi

# --- Shell -----------------------------------------------------------------------
if [ "$need_shell" = 1 ]; then
    bad=""
    for file in $changed; do
        [ -f "$file" ] || continue
        case "$file" in
            .githooks/*|*.sh)
                sh -n "$file" 2>/dev/null || bad="$bad $file(syntax)"
                [ -x "$file" ] || bad="$bad $file(not executable)"
                ;;
            *.py)
                if [ -n "$PYTHON_COMMAND" ]; then
                    "$PYTHON_COMMAND" -c "import ast, pathlib, sys; ast.parse(pathlib.Path(sys.argv[1]).read_text(encoding='utf-8'))" "$file" 2>/dev/null \
                        || bad="$bad $file(syntax)"
                else
                    skipped "  NOT CHECKED: no python, $file syntax not checked"
                fi
                ;;
            *.ps1)
                ps_found=0
                for ps_command in pwsh powershell.exe; do
                    command -v "$ps_command" >/dev/null 2>&1 || continue
                    ps_found=1
                    "$ps_command" -NoProfile -Command \
                        '& { param([string]$Path) $tokens=$null; $errors=$null; [System.Management.Automation.Language.Parser]::ParseFile($Path, [ref]$tokens, [ref]$errors) | Out-Null; if ($errors.Count) { $errors | ForEach-Object { [Console]::Error.WriteLine($_.Message) }; exit 1 } }' \
                        "$file" || bad="$bad $file($ps_command syntax)"
                done
                [ "$ps_found" = 1 ] || skipped "  NOT CHECKED: no PowerShell, $file syntax not checked"
                ;;
        esac
    done
    if [ -n "$bad" ]; then
        printf "   %s\n" "$bad" >&2
        fail "problems in tooling scripts"
    else
        pass "tooling scripts: syntax and permissions"
    fi

    # Every scripts/*.ps1 wrapper goes through invoke-sh.ps1.
    touches_wrappers=0
    for file in $changed; do
        case "$file" in scripts/*.ps1) touches_wrappers=1 ;; esac
    done
    # The launcher needs Git for Windows' bash: elsewhere (the Linux CI runner
    # has pwsh) the cases would only print NOT CHECKED and pass.
    case "$(uname -s)" in
        MINGW*|MSYS*|CYGWIN*) ;;
        *)
            if [ "$touches_wrappers" = 1 ]; then
                skipped "  NOT CHECKED: not Windows, invoke-sh.ps1 cases not run"
            fi
            touches_wrappers=0
            ;;
    esac
    if [ "$touches_wrappers" = 1 ]; then
        wrapper_ps_command=""
        command -v pwsh >/dev/null 2>&1 && wrapper_ps_command=pwsh
        if [ -z "$wrapper_ps_command" ] && command -v powershell.exe >/dev/null 2>&1; then
            wrapper_ps_command=powershell.exe
        fi
        if [ -n "$wrapper_ps_command" ]; then
            if "$wrapper_ps_command" -NoProfile -ExecutionPolicy Bypass \
                -File scripts/tests/invoke-sh-cases.ps1; then
                pass "PowerShell wrappers: invoke-sh.ps1 cases"
            else
                fail "PowerShell wrappers: invoke-sh.ps1 cases"
            fi
        else
            skipped "  NOT CHECKED: no PowerShell, invoke-sh.ps1 cases not run"
        fi
    fi

    # handoff.sh: which task a short id resolves to (bug-001).
    touches_handoff=0
    for file in $changed; do
        case "$file" in scripts/handoff.sh|scripts/tests/handoff-cases.sh) touches_handoff=1 ;; esac
    done
    if [ "$touches_handoff" = 1 ]; then
        if sh scripts/tests/handoff-cases.sh; then
            pass "handoff.sh: short-id resolution cases"
        else
            fail "handoff.sh: short-id resolution cases"
        fi
    fi

    # The green stamp of this very script (tech-030): the cases run a nested
    # verify.sh in a throwaway repository, so they are cheap and never touch the
    # real stamps.
    touches_stamp=0
    for file in $changed; do
        case "$file" in scripts/verify.sh|scripts/tests/verify-stamp-cases.sh) touches_stamp=1 ;; esac
    done
    if [ "$touches_stamp" = 1 ]; then
        if sh scripts/tests/verify-stamp-cases.sh; then
            pass "verify.sh: green stamp cases"
        else
            fail "verify.sh: green stamp cases"
        fi
    fi
fi

# --- CI ----------------------------------------------------------------------------
if [ "$need_ci" = 1 ]; then
    if [ -n "$PYTHON_COMMAND" ] && "$PYTHON_COMMAND" -c "import yaml" >/dev/null 2>&1; then
        bad=""
        for file in $changed; do
            case "$file" in .github/workflows/*) ;; *) continue ;; esac
            [ -f "$file" ] || continue
            "$PYTHON_COMMAND" -c "import sys, yaml; yaml.safe_load(open(sys.argv[1], encoding='utf-8'))" "$file" >/dev/null 2>&1 || bad="$bad $file"
        done
        if [ -n "$bad" ]; then
            printf "    invalid YAML:%s\n" "$bad" >&2
            fail "invalid CI workflow"
        else
            pass "CI workflow: YAML is valid"
        fi
    else
        skipped "  NOT CHECKED: no python with PyYAML, CI workflow needs a manual check"
    fi
fi

# --- Backend -------------------------------------------------------------------
if [ "$need_dotnet" = 1 ]; then
    if ! command -v dotnet >/dev/null 2>&1; then
        fail "dotnet not found, backend build not checked"
    elif dotnet build Mustask.sln --nologo -v q; then
        pass "backend build"
        if docker info >/dev/null 2>&1; then
            if dotnet test Mustask.sln --no-build --nologo -v q; then
                pass "backend tests, including integration"
            else
                fail "backend tests"
            fi
        else
            skipped "  Docker not running: skipping integration tests, running the rest"
            for project in src/backend/tests/*/; do
                case "$project" in *IntegrationTests/) continue ;; esac
                [ -d "$project" ] || continue
                grep -rqE "\[(Fact|Theory)" "$project" --include="*.cs" 2>/dev/null || continue
                # The RequiresDocker trait also shows up outside
                # *IntegrationTests (e.g. MigrationsTests in
                # Mustask.Infrastructure.Tests).
                if dotnet test "$project" --no-build --nologo -v q --filter "Category!=RequiresDocker"; then
                    pass "tests ${project%/}"
                else
                    fail "tests ${project%/}"
                fi
            done
            skipped "  NOT CHECKED: integration tests, Docker required"
        fi
    else
        fail "backend build"
    fi
fi

# --- Keycloak provider -----------------------------------------------------------
# ADR-0025 § 1: a provider change verified by nothing is what the NFR forbids,
# so without Docker this bucket fails instead of printing "not checked".
if [ "$need_provider" = 1 ]; then
    if ! docker info >/dev/null 2>&1; then
        fail "Docker is required to verify the Keycloak provider (deploy/keycloak/providers, ADR-0025)"
    else
        provider_log=$(mktemp)
        if DOCKER_BUILDKIT=1 docker build --progress=plain --target provider deploy/keycloak >"$provider_log" 2>&1; then
            grep -E "Tests run: [0-9]+, Failures" "$provider_log" | tail -n 1 | sed "s/^/    /"
            pass "Keycloak provider: compile, unit tests, read-only guard (docker build --target provider)"
        else
            tail -n 60 "$provider_log" | sed "s/^/    /" >&2
            fail "Keycloak provider: docker build --target provider"
        fi
        rm -f "$provider_log"

        provider_built=1
        if [ "$need_dotnet" != 1 ]; then
            if ! command -v dotnet >/dev/null 2>&1 || ! dotnet build Mustask.sln --nologo -v q; then
                fail "backend build (for the KeycloakProvider tests)"
                provider_built=0
            fi
        fi
        if [ "$provider_built" = 1 ]; then
            provider_tests_ok=1
            for project in src/backend/tests/Mustask.Contracts.Tests src/backend/tests/Mustask.Api.IntegrationTests; do
                MUSTASK_KEYCLOAK_PROVIDER_TESTS=1 dotnet test "$project" --no-build --nologo -v q \
                    --filter "Category=KeycloakProvider" || provider_tests_ok=0
            done
            if [ "$provider_tests_ok" = 1 ]; then
                pass ".NET tests tagged KeycloakProvider (shared signature vectors, image, live provider)"
            else
                fail ".NET tests tagged KeycloakProvider"
            fi
        fi
    fi
fi

# --- Frontend ------------------------------------------------------------------
if [ "$need_frontend" = 1 ]; then
    for app in src/frontend; do
        [ -f "$app/package.json" ] || continue
        if [ ! -d "$app/node_modules" ]; then
            fail "$app/node_modules missing, run npm ci first"
            continue
        fi
        if (cd "$app" && npm run lint --if-present); then
            pass "lint $app"
        else
            fail "lint $app"
        fi
        # Smoke build, not an artifact: the guard in vite.config requires
        # VITE_API_BASE_URL, and a real value isn't needed here and has
        # nowhere to come from.
        if (cd "$app" && VITE_API_BASE_URL="${VITE_API_BASE_URL:-http://verify.invalid}" npm run build --if-present); then
            pass "build $app"
        else
            fail "build $app"
        fi
        if [ "$VERIFY_FULL" = 1 ] || [ -z "$merge_base" ]; then
            if [ "$VERIFY_FULL" = 1 ]; then
                note "  tests $app: the whole suite (VERIFY_FULL=1)"
            else
                note "  tests $app: the whole suite (no merge-base with $BASE)"
            fi
            vitest_lock_acquire
            if (cd "$app" && npm test --if-present -- --run); then
                vitest_lock_release
                pass "tests $app"
            else
                vitest_lock_release
                fail "tests $app"
            fi
        else
            # --passWithNoTests: a change that no test reaches is not a failure.
            note "  tests $app: only those reaching changes since $(printf '%.9s' "$merge_base") (VERIFY_FULL=1 for the whole suite)"
            if (cd "$app" && npm test --if-present -- --run --changed "$merge_base" --passWithNoTests); then
                pass "tests $app (affected)"
            else
                fail "tests $app (affected)"
            fi
            # `--changed` follows imports, and these tests read sources, the
            # error-code contracts or other tests off disk instead: no
            # change would ever "reach" them, so they always run (the kit
            # guard, the colour scan, the e2e guards, the contract readers).
            guards=$(cd "$app" && grep -rlE "node:fs|from 'fs'|readFileSync|readdirSync" src --include='*.test.ts' --include='*.test.tsx' 2>/dev/null | sort || true)
            if [ -n "$guards" ]; then
                # shellcheck disable=SC2086 -- file names have no spaces
                if (cd "$app" && npm test --if-present -- --run $guards); then
                    pass "tests $app (source-reading guards, always run)"
                else
                    fail "tests $app (source-reading guards)"
                fi
            fi
        fi
    done
fi

note ""
if [ -n "$failed" ]; then
    note "RESULT: failed"
    printf "%s" "$failed"
    exit 1
fi

if [ -n "$stamp_file" ] && [ "$unchecked" = 0 ]; then
    # The run lasted minutes: stamp only if the tree is still the one that was
    # tested — same commit, same tree, still clean.
    if [ "$(git rev-parse HEAD 2>/dev/null)" = "$stamp_commit" ] &&
       [ "$(git rev-parse 'HEAD^{tree}' 2>/dev/null)" = "$stamp_tree" ] && tree_is_clean; then
        mkdir -p "$(dirname "$stamp_file")" 2>/dev/null &&
        {
            printf "verified: %s\n" "$(date '+%Y-%m-%d %H:%M:%S')"
            printf "branch:   %s\n" "$(git rev-parse --abbrev-ref HEAD 2>/dev/null)"
            printf "commit:   %s\n" "$stamp_commit"
            printf "base:     %s\n" "${merge_base:-none}"
        } > "$stamp_file" 2>/dev/null || true
    else
        note "  no green stamp: the tree changed while the checks ran"
    fi
    # Stamps for trees nobody asks about any more.
    find "$(dirname "$stamp_file")" -type f -mtime +30 -exec rm -f {} + 2>/dev/null || true
fi

note "RESULT: green"
printf "%s" "$passed"
exit 0
