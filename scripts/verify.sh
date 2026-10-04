#!/bin/sh
# Verification proportionate to the change.
#
# Looks at which files changed and runs only the checks that make sense for
# them: a README edit doesn't spin up Testcontainers, but a
# Directory.Packages.props edit does — that's a dependency graph for the
# whole solution, not "just a config".
#
#   scripts/verify.sh          run the checks
#   scripts/verify.sh --list   only show the classification
#
# Comparison base is main, overridden by the BASE variable.
# An unknown path is treated as code: missing a breakage costs more than an
# extra build.

set -eu

BASE=${BASE:-main}
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

PYTHON_COMMAND=$(find_python || true)

failed=""
passed=""

note() { printf "%s\n" "$1"; }
fail() { failed="$failed  - $1
"; printf "  FAIL: %s\n" "$1" >&2; }
pass() { passed="$passed  - $1
"; printf "  ok: %s\n" "$1"; }

merge_base=$(git merge-base HEAD "$BASE" 2>/dev/null || echo "")

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
        # KeycloakRealmRolesTests from Mustask.Contracts.Tests.
        deploy/keycloak/realms/*)                    echo dotnet ;;
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
    bucket=$(classify "$file")
    printf "  %-9s %s\n" "$bucket" "$file"
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

note ""
if [ "$need_prose$need_agents$need_shell$need_ci$need_dotnet$need_frontend$need_provider" = "0000000" ]; then
    note "No checks needed: the changes affect nothing."
    [ "$LIST_ONLY" = 1 ] && exit 0
    note ""
    note "RESULT: green"
    exit 0
fi

[ "$LIST_ONLY" = 1 ] && exit 0

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
    over=""
    warn=""
    for file in $changed; do
        case "$file" in docs/features/*/status.md) ;; *) continue ;; esac
        [ -f "$file" ] || continue
        grep -q "^- stage: complete" "$file" && continue
        n=$(wc -l < "$file" | tr -d " ")
        if [ "$n" -gt 250 ]; then
            over="$over $file:$n"
        elif [ "$n" -gt 150 ]; then
            warn="$warn $file:$n"
        fi
    done
    if [ -n "$warn" ]; then
        note "    over 150 lines, roll the oldest Log into history.md:$warn"
    fi
    if [ -n "$over" ]; then
        printf "    over 250 lines:%s\n" "$over" >&2
        fail "status.md has become a journal (docs/features/README.md, \"Size budget\")"
    else
        pass "status.md stays within its budget"
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
        note "  NOT CHECKED: no python, AI configuration JSON needs a manual check"
    fi

    if [ -n "$PYTHON_COMMAND" ]; then
        if "$PYTHON_COMMAND" scripts/validate-ai-workflow.py; then
            pass "Claude Code/Codex AI workflow compatibility"
        else
            fail "Claude Code/Codex AI workflow compatibility"
        fi
    else
        note "  NOT CHECKED: no python, AI workflow adapters not checked"
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
            note "  NOT CHECKED: no python, hook offline tables not run"
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
            note "  NOT CHECKED: no PowerShell, Windows hook adapter not run"
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
                fi
                ;;
            *.ps1)
                for ps_command in pwsh powershell.exe; do
                    command -v "$ps_command" >/dev/null 2>&1 || continue
                    "$ps_command" -NoProfile -Command \
                        '& { param([string]$Path) $tokens=$null; $errors=$null; [System.Management.Automation.Language.Parser]::ParseFile($Path, [ref]$tokens, [ref]$errors) | Out-Null; if ($errors.Count) { $errors | ForEach-Object { [Console]::Error.WriteLine($_.Message) }; exit 1 } }' \
                        "$file" || bad="$bad $file($ps_command syntax)"
                done
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
                note "  NOT CHECKED: not Windows, invoke-sh.ps1 cases not run"
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
            note "  NOT CHECKED: no PowerShell, invoke-sh.ps1 cases not run"
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
        note "  NOT CHECKED: no python with PyYAML, CI workflow needs a manual check"
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
            note "  Docker not running: skipping integration tests, running the rest"
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
            note "  NOT CHECKED: integration tests, Docker required"
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
        if (cd "$app" && npm test --if-present -- --run); then
            pass "tests $app"
        else
            fail "tests $app"
        fi
    done
fi

note ""
if [ -n "$failed" ]; then
    note "RESULT: failed"
    printf "%s" "$failed"
    exit 1
fi

note "RESULT: green"
printf "%s" "$passed"
exit 0
