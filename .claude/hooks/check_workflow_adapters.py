#!/usr/bin/env python3
"""PostToolUse: catch AI-workflow adapter drift at the edit that caused it.

`scripts/verify.sh` already runs `scripts/validate-ai-workflow.py`, so a
broken vendor pair — a Claude agent whose Codex twin was not changed with
it, a stage file renamed in one place only — never reaches `main`. But it
is found on the way out, at the push, by which point the session that
caused it may have moved on to something else.

This hook runs the same validator at the moment a file under one of
WATCHED_PREFIXES is written, and only then: the check takes milliseconds,
but running it after every edit in the repository would be noise. On a
failure it exits 2, and the validator's own report goes to stderr, which
PostToolUse feeds back into the session.

Doctrine, shared with `block_wide_fs_search.py`: **fail open**. An
unexpected payload shape, a missing interpreter, a validator that will not
start — none of it stops the work. The hook is an early signal, not the
gate; the gate is `verify.sh`, and it runs for both vendors.

Codex has no PostToolUse wiring here: its hook schema is not verified for
that event, and guessing at it would risk breaking live sessions for a
convenience. For Codex the signal stays `verify.sh`.
"""

from __future__ import annotations

import json
import pathlib
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
VALIDATOR = ROOT / "scripts" / "validate-ai-workflow.py"

# Everything the validator actually reads. A file outside this list cannot
# make it fail, so running it would only cost time.
WATCHED_PREFIXES = (
    "docs/ai-workflow/",
    ".claude/agents/",
    ".claude/commands/",
    ".claude/settings.json",
    ".claude/skills/",
    ".codex/",
    ".agents/skills/",
)

_MAX_PATH_LEN = 4096


def is_watched(file_path: str) -> bool:
    """True if editing this path can change the validator's verdict.

    The path may arrive absolute or relative, with either separator. What
    matters is where it lands relative to the repository root; anything
    outside the repository is not ours to check.
    """
    if not file_path or not isinstance(file_path, str):
        return False
    if len(file_path) > _MAX_PATH_LEN:
        return False

    candidate = pathlib.Path(file_path.replace("\\", "/"))
    try:
        if candidate.is_absolute():
            relative = candidate.resolve().relative_to(ROOT).as_posix()
        else:
            # str.lstrip strips characters, not a prefix: "./x".lstrip("./")
            # eats the leading dot of ".claude/..." as well. Caught by the
            # offline table, which is why every dot-prefixed adapter path is
            # in it.
            relative = candidate.as_posix()
            while relative.startswith("./"):
                relative = relative[2:]
    except (ValueError, OSError):
        return False

    return any(relative.startswith(prefix) for prefix in WATCHED_PREFIXES)


def main() -> int:
    # Same reason as in block_wide_fs_search.py: the Windows console is not
    # UTF-8 by default, and a report the reader cannot decode is worse than
    # no report at all.
    if hasattr(sys.stderr, "reconfigure"):
        try:
            sys.stderr.reconfigure(encoding="utf-8")
        except Exception:
            pass

    try:
        payload = json.load(sys.stdin)
        if not isinstance(payload, dict):
            return 0

        tool_input = payload.get("tool_input") or {}
        if not isinstance(tool_input, dict):
            return 0

        if not is_watched(tool_input.get("file_path", "")):
            return 0

        result = subprocess.run(
            [sys.executable, str(VALIDATOR)],
            cwd=str(ROOT),
            capture_output=True,
            encoding="utf-8",
            errors="replace",
            timeout=60,
        )
    except Exception:
        # See the module docstring: an early signal never blocks work.
        return 0

    if result.returncode == 0:
        return 0

    try:
        report = (result.stderr or result.stdout or "").strip()
        sys.stderr.write(
            "AI workflow adapters drifted after this edit "
            "(scripts/validate-ai-workflow.py):\n\n"
            f"{report}\n\n"
            "Both vendors change together — a Claude agent and its Codex "
            "twin, a stage file and every reference to it.\n"
        )
    except Exception:
        pass
    return 2


if __name__ == "__main__":
    sys.exit(main())
