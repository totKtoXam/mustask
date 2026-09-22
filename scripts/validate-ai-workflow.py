#!/usr/bin/env python3
"""Checks Claude Code and Codex AI-workflow adapter compatibility.

This is not a state machine validator for features. The script checks only
the static configuration: the shared feature contract, thin adapters, the
hook source of truth, and model-tier parity between vendor profiles.
"""

from __future__ import annotations

import json
import pathlib
import re
import sys
import tomllib

ROOT = pathlib.Path(__file__).resolve().parents[1]
CANONICAL = "docs/ai-workflow/feature.md"

ROLE_STAGES = {
    "business-analyst": "01-business-analysis.md",
    "requirements-reviewer": "02-requirements-review.md",
    "ux-designer": "03-ux-design.md",
    "backend-developer": "04-backend.md",
    "frontend-developer": "05-frontend.md",
    "qa-engineer": "04-backend.md",
    "code-reviewer": "06-code-review.md",
    "task-executor": "00-orchestrator.md",
}

TIER_MODELS = {
    "deep": ("opus", "gpt-5.6", "high"),
    "standard": ("sonnet", "gpt-5.6-terra", "medium"),
    "fast": ("haiku", "gpt-5.6-luna", "low"),
}


def read(relative: str) -> str:
    path = ROOT / relative
    if not path.is_file():
        raise FileNotFoundError(relative)
    return path.read_text(encoding="utf-8")


def frontmatter(relative: str) -> dict[str, str]:
    text = read(relative)
    lines = text.splitlines()
    if not lines or lines[0] != "---":
        raise ValueError(f"{relative}: missing YAML frontmatter")
    try:
        end = lines.index("---", 1)
    except ValueError as error:
        raise ValueError(f"{relative}: frontmatter not closed") from error

    result: dict[str, str] = {}
    for line in lines[1:end]:
        match = re.match(r"^([a-zA-Z0-9_-]+):\s*(.*)$", line)
        if match:
            result[match.group(1)] = match.group(2).strip()
    return result


def role_tiers() -> dict[str, str]:
    """Reads the canonical role->tier table directly from dispatch.md."""
    result: dict[str, str] = {}
    pattern = re.compile(r"^\|\s*([a-z]+(?:-[a-z]+)*)\s*\|\s*`(deep|standard|fast)`\s*\|")
    for line in read("docs/ai-workflow/dispatch.md").splitlines():
        match = pattern.match(line)
        if match and match.group(1) in ROLE_STAGES:
            result[match.group(1)] = match.group(2)
    missing = sorted(set(ROLE_STAGES) - set(result))
    if missing:
        raise ValueError(f"dispatch.md: no role->tier for {', '.join(missing)}")
    return result


def main() -> int:
    errors: list[str] = []

    required = [
        CANONICAL,
        ".claude/commands/feature.md",
        ".agents/skills/feature/SKILL.md",
        ".codex/config.toml",
        ".codex/hooks.json",
        ".codex/hooks/run-pre-tool-use.ps1",
    ]
    for relative in required:
        if not (ROOT / relative).is_file():
            errors.append(f"not found: {relative}")

    if errors:
        return report(errors)

    canonical = read(CANONICAL)
    for marker in (
        "docs/ai-workflow/README.md",
        "docs/ai-workflow/routing.md",
        "scripts/new-task.ps1",
        "gate-3",
        "code-reviewer",
    ):
        if marker not in canonical:
            errors.append(f"{CANONICAL}: missing required link {marker}")

    claude_adapter = read(".claude/commands/feature.md")
    codex_adapter = read(".agents/skills/feature/SKILL.md")
    for relative, text in (
        (".claude/commands/feature.md", claude_adapter),
        (".agents/skills/feature/SKILL.md", codex_adapter),
    ):
        if CANONICAL not in text:
            errors.append(f"{relative}: does not link the shared contract {CANONICAL}")
        if len(text) > 3_000:
            errors.append(f"{relative}: the adapter has grown and duplicates the shared contract")

    if "$ARGUMENTS" not in claude_adapter:
        errors.append(".claude/commands/feature.md: lost the $ARGUMENTS passthrough")
    if (ROOT / ".claude/skills/feature").exists():
        errors.append(".claude/skills/feature is forbidden: it would intercept the working /feature command")

    try:
        skill_meta = frontmatter(".agents/skills/feature/SKILL.md")
        if skill_meta.get("name") != "feature":
            errors.append("the Codex skill must be named feature")
        if not skill_meta.get("description"):
            errors.append("Codex skill: missing description")
        command_meta = frontmatter(".claude/commands/feature.md")
        if not command_meta.get("description"):
            errors.append("Claude /feature: missing description")
    except (FileNotFoundError, ValueError) as error:
        errors.append(str(error))

    for relative in (".claude/settings.json", ".codex/hooks.json"):
        try:
            json.loads(read(relative))
        except (FileNotFoundError, json.JSONDecodeError) as error:
            errors.append(f"{relative}: invalid JSON: {error}")

    # The adapter drift check is wired as a PostToolUse hook so that a
    # broken vendor pair surfaces at the edit, not at the push. Removing
    # the wiring silently would put that back the way it was.
    try:
        settings = json.loads(read(".claude/settings.json"))
        post = settings["hooks"]["PostToolUse"][0]
        if "Edit" not in post.get("matcher", ""):
            errors.append(".claude/settings.json: PostToolUse must match Edit")
        if "check_workflow_adapters.py" not in post["hooks"][0].get("command", ""):
            errors.append(
                ".claude/settings.json: PostToolUse does not run the adapter drift check"
            )
        if not (ROOT / ".claude/hooks/check_workflow_adapters.py").is_file():
            errors.append("not found: .claude/hooks/check_workflow_adapters.py")
    except (KeyError, IndexError, TypeError, FileNotFoundError, json.JSONDecodeError) as error:
        errors.append(f".claude/settings.json: PostToolUse hook is not wired: {error}")

    try:
        config = tomllib.loads(read(".codex/config.toml"))
        concurrency = config.get("agents", {}).get("max_concurrent_threads_per_session")
        if not isinstance(concurrency, int) or concurrency < 1:
            errors.append(".codex/config.toml: max_concurrent_threads_per_session must be > 0")
    except (FileNotFoundError, tomllib.TOMLDecodeError) as error:
        errors.append(f".codex/config.toml: invalid TOML: {error}")

    try:
        tiers = role_tiers()
    except ValueError as error:
        errors.append(str(error))
        tiers = {}

    # A stage file states its own model tier in the header, and for a long time
    # 01-business-analysis.md said "standard" while the table, both vendor
    # profiles and the actual runs said "deep". Nothing compared the two, so
    # the contradiction survived every check. The header must lead with the
    # role's default tier; overrides come after it in parentheses.
    for role, stage_file in ROLE_STAGES.items():
        tier = tiers.get(role)
        if tier not in TIER_MODELS:
            continue
        relative = f"docs/ai-workflow/stages/{stage_file}"
        try:
            header = ""
            for line in read(relative).splitlines():
                if "**Model" in line:
                    header = line.split("**Model", 1)[1]
                    break
            if not header:
                errors.append(f"{relative}: the header does not state a model tier")
                continue
            stated = re.search(r"\b(deep|standard|fast)\b", header)
            if not stated:
                errors.append(f"{relative}: no deep/standard/fast in the model header")
            elif stated.group(1) != tier:
                errors.append(
                    f"{relative}: header says {stated.group(1)}, dispatch.md says {tier} for {role}"
                )
        except FileNotFoundError as error:
            errors.append(str(error))
    for role, stage_file in ROLE_STAGES.items():
        tier = tiers.get(role)
        if tier not in TIER_MODELS:
            continue
        claude_model, codex_model, effort = TIER_MODELS[tier]
        claude_path = f".claude/agents/{role}.md"
        codex_path = f".codex/agents/{role}.toml"
        try:
            claude = frontmatter(claude_path)
            if claude.get("model") != claude_model:
                errors.append(
                    f"{claude_path}: tier {tier} expects model: {claude_model}"
                )
            # Every stage hands its role a file to produce (spec.md, review.md,
            # design.md, qa.md, code) — Edit alone cannot create one, so a
            # profile without Write strands the stage (a requirements review
            # once had to be saved by hand).
            tools = {t.strip() for t in claude.get("tools", "").split(",")}
            if "Write" not in tools:
                errors.append(f"{claude_path}: the stage produces a file, tools must include Write")
        except (FileNotFoundError, ValueError) as error:
            errors.append(str(error))

        try:
            codex = tomllib.loads(read(codex_path))
            if codex.get("name") != role:
                errors.append(f"{codex_path}: name must match {role}")
            for field in ("description", "developer_instructions"):
                if not codex.get(field):
                    errors.append(f"{codex_path}: missing {field}")
            if codex.get("model") != codex_model:
                errors.append(f"{codex_path}: tier {tier} expects model {codex_model}")
            if codex.get("model_reasoning_effort") != effort:
                errors.append(f"{codex_path}: tier {tier} expects effort {effort}")
            # Same rule on the Codex side: read-only cannot write review.md.
            # Reviewers still do not fix code — that is their instructions, not
            # a sandbox setting.
            if codex.get("sandbox_mode") != "workspace-write":
                errors.append(f"{codex_path}: the stage produces a file, sandbox_mode must be workspace-write")
            if stage_file not in codex.get("developer_instructions", ""):
                errors.append(f"{codex_path}: missing link to {stage_file}")
        except (FileNotFoundError, tomllib.TOMLDecodeError) as error:
            errors.append(f"{codex_path}: invalid TOML: {error}")

    try:
        hooks = json.loads(read(".codex/hooks.json"))
        handler = hooks["hooks"]["PreToolUse"][0]["hooks"][0]
        if hooks["hooks"]["PreToolUse"][0].get("matcher") != "^Bash$":
            errors.append(".codex/hooks.json: PreToolUse matcher must be ^Bash$")
        if ".claude/hooks/block_wide_fs_search.py" not in handler.get("command", ""):
            errors.append(".codex/hooks.json: the POSIX hook does not use the shared Claude policy")
        if ".codex/hooks/run-pre-tool-use.ps1" not in handler.get("commandWindows", ""):
            errors.append(".codex/hooks.json: missing Windows adapter")
        windows_adapter = read(".codex/hooks/run-pre-tool-use.ps1")
        if ".claude\\hooks\\block_wide_fs_search.py" not in windows_adapter:
            errors.append("the Codex Windows hook does not use the shared Claude policy")
        if "toolName.Value = 'PowerShell'" not in windows_adapter:
            errors.append("the Codex Windows hook does not normalize the Bash tool name to PowerShell")
    except (KeyError, IndexError, TypeError, json.JSONDecodeError) as error:
        errors.append(f".codex/hooks.json: unexpected structure: {error}")

    return report(errors)


def report(errors: list[str]) -> int:
    if errors:
        print("AI workflow adapters: FAIL", file=sys.stderr)
        for error in errors:
            print(f"  - {error}", file=sys.stderr)
        return 1
    print("AI workflow adapters: OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())
