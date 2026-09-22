#!/usr/bin/env python3
"""Offline table for check_workflow_adapters.py.

Two things are checked, and they need different machinery:

1. **Which paths the hook reacts to** — `is_watched()` is imported and
   called directly. The alternative, running the hook as a subprocess and
   looking at the exit code, cannot tell "did not match" from "matched and
   the repository is valid": in a healthy repository both are 0.
2. **Fail-open on a broken payload** — that one is end-to-end, as a real
   subprocess, because the point is the exit code the harness sees.

The table is inline rather than in a .tsv (unlike the sibling
`cases.tsv`): fifteen paths do not need a data file, and the shell command
table next door has ninety-four rows for a parser, not a prefix match.

    python .claude/hooks/tests/run_adapter_cases.py
"""

from __future__ import annotations

import importlib.util
import json
import pathlib
import subprocess
import sys

HERE = pathlib.Path(__file__).resolve().parent
HOOK = HERE.parent / "check_workflow_adapters.py"
ROOT = HERE.parents[2]

# expect, path
PATH_CASES = [
    # Watched: the validator reads these, an edit here can change its verdict.
    (True, "docs/ai-workflow/feature.md"),
    (True, "docs/ai-workflow/dispatch.md"),
    (True, "docs/ai-workflow/stages/06-code-review.md"),
    (True, ".claude/agents/code-reviewer.md"),
    (True, ".claude/commands/feature.md"),
    (True, ".claude/settings.json"),
    (True, ".codex/agents/code-reviewer.toml"),
    (True, ".codex/config.toml"),
    (True, ".codex/hooks.json"),
    (True, ".agents/skills/feature/SKILL.md"),
    (True, "./docs/ai-workflow/routing.md"),
    (True, r".claude\agents\qa-engineer.md"),
    (True, str(ROOT / "docs" / "ai-workflow" / "README.md")),
    # Not watched: the validator never opens them.
    (False, "docs/conventions/backend.md"),
    (False, "docs/features/feat-001-example/status.md"),
    (False, "src/backend/Mustask.Api/Program.cs"),
    (False, "scripts/verify.sh"),
    (False, ".claude/hooks/check_workflow_adapters.py"),
    (False, "AGENTS.md"),
    (False, ""),
    (False, "../outside-the-repository/settings.json"),
]

# A payload the hook must survive without blocking: fail open, always.
BROKEN_PAYLOADS = [
    "",
    "not json at all",
    "[]",
    '{"tool_input": "a string, not an object"}',
    '{"tool_input": {"file_path": 12345}}',
    "{}",
]


def load_hook():
    spec = importlib.util.spec_from_file_location("check_workflow_adapters", HOOK)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def run_payload(payload: str) -> int:
    proc = subprocess.run(
        [sys.executable, str(HOOK)],
        input=payload,
        capture_output=True,
        encoding="utf-8",
        errors="replace",
        timeout=90,
    )
    return proc.returncode


def main() -> int:
    for stream in (sys.stdout, sys.stderr):
        if hasattr(stream, "reconfigure"):
            stream.reconfigure(encoding="utf-8")

    hook = load_hook()
    failures = 0

    for expect, path in PATH_CASES:
        got = hook.is_watched(path)
        ok = got == expect
        failures += 0 if ok else 1
        print(f"  {'ok' if ok else 'FAIL':4} watched={str(expect):5} got={str(got):5} {path!r}")

    print()
    for payload in BROKEN_PAYLOADS:
        code = run_payload(payload)
        ok = code == 0
        failures += 0 if ok else 1
        print(f"  {'ok' if ok else 'FAIL':4} fail-open exit={code} payload={payload[:40]!r}")

    total = len(PATH_CASES) + len(BROKEN_PAYLOADS)
    print()
    print(f"Total: {total} cases, {failures} failed.")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
