#!/usr/bin/env python3
"""Offline run of the command table for block_wide_fs_search.py.

Does not require a live Claude Code session: builds the same JSON the hook
receives on stdin from PreToolUse, runs the hook script as a real
subprocess, and checks the exit code (0 = allow, 2 = block) against the
expectation from cases.tsv.

    python .claude/hooks/tests/run_cases.py
"""

from __future__ import annotations

import json
import pathlib
import subprocess
import sys

HERE = pathlib.Path(__file__).resolve().parent
HOOK = HERE.parent / "block_wide_fs_search.py"
CASES = HERE / "cases.tsv"

# The paths are fake: the hook only looks at the shape of the path (/,
# /c, /c/Users/<name>, the project directory, ...), the values aren't
# tied to a real machine.
CWD_VALUES = {
    "PROJ": "/c/Users/jdoe/workspace/mustask",
    "ROOT": "/",
    "HOME": "/c/Users/jdoe",
}
CWD_VALUES["SUBDIR"] = CWD_VALUES["PROJ"] + "/src"


def load_cases() -> list[tuple[str, str, str, str]]:
    rows = []
    for line in CASES.read_text(encoding="utf-8").splitlines():
        if not line.strip() or line.startswith("#"):
            continue
        expect, tool_name, cwd_key, command = line.split("\t", 3)
        rows.append((expect, tool_name, CWD_VALUES[cwd_key], command))
    return rows


def run_case(tool_name: str, cwd: str, command: str) -> tuple[int, str]:
    payload = json.dumps(
        {
            "hook_event_name": "PreToolUse",
            "tool_name": tool_name,
            "tool_input": {"command": command},
            "cwd": cwd,
        }
    )
    proc = subprocess.run(
        [sys.executable, str(HOOK)],
        input=payload,
        capture_output=True,
        # The encoding is set explicitly as UTF-8, rather than via
        # `text=True` (which would read the child's output in the host's
        # local encoding — on Windows that's often cp1251/cp866, not the
        # UTF-8 the hook itself writes after reconfigure). `errors="replace"`
        # — so that an encoding regression in the hook turns into visible
        # garbage in stderr and a failure of the BLOCK_MARKER check below,
        # rather than an unhandled exception that crashes the whole table
        # run.
        encoding="utf-8",
        errors="replace",
        timeout=10,
    )
    return proc.returncode, proc.stderr.strip()


# A substring from the hook's message (block_wide_fs_search.py, main())
# — if it's missing from a BLOCK case's stderr when decoded as UTF-8, the
# reader gets garbage instead of the blocking reason (see status.md, QA
# round 2, M-1).
BLOCK_MARKER = "Blocked by the wide-search guard"


def main() -> int:
    # The Windows console isn't UTF-8 by default — without this, Russian
    # text in the output (including the summary line) turns into garbage.
    for stream in (sys.stdout, sys.stderr):
        if hasattr(stream, "reconfigure"):
            stream.reconfigure(encoding="utf-8")

    rows = load_cases()
    failures = []
    for expect, tool_name, cwd, command in rows:
        code, stderr = run_case(tool_name, cwd, command)
        got = "BLOCK" if code == 2 else "ALLOW" if code == 0 else f"ERROR({code})"
        ok = got == expect
        if ok and expect == "BLOCK" and BLOCK_MARKER not in stderr:
            ok = False
            got = "BLOCK(garbage in stderr)"
        status = "ok" if ok else "FAIL"
        if status == "FAIL":
            failures.append((expect, got, tool_name, command, stderr))
        print(f"  {status:4} expect={expect:5} got={got:5} [{tool_name}] {command}")

    print()
    print(f"Total: {len(rows)} cases, {len(failures)} failed.")
    if failures:
        print()
        print("Failures:")
        for expect, got, tool_name, command, stderr in failures:
            print(f"  expect={expect} got={got} [{tool_name}] {command}")
            if stderr:
                print(f"    stderr: {stderr}")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
