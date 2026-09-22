#!/usr/bin/env python3
"""What the work on this repository actually cost, from the session transcripts.

Claude Code writes every assistant turn to `~/.claude/projects/<encoded repo
path>/`, with the model and the token counts on the message and `gitBranch`
on the entry itself. Subagents get their own transcripts in
`<session>/subagents/agent-*.jsonl`, next to a `.meta.json` that names the
role. So cost per task, per role and per model is already on disk; nothing
needs instrumenting.

    python scripts/usage-report.py
    python scripts/usage-report.py --by role
    python scripts/usage-report.py --task tech-004
    python scripts/usage-report.py --prices .claude/usage-prices.json

Three things this has to get right, each of which produced a wrong answer
first:

1. **Deduplicate by `uuid`.** One session file appears under several worktree
   directories, so a naive sum over the tree counted 12090 entries where
   there were 4400 — an overcount of nearly three times.
2. **Subagents live elsewhere.** They are not sidechain entries in the parent
   transcript; they are separate files one directory down. A listing that
   misses them attributes 100% of the spend to the orchestrator, when the
   real split is about 80/20.
3. **`cache_read` is the volume that matters.** Across this repository it runs
   ~180x the output tokens. A report that shows only input/output hides the
   line item that dominates the bill.

Prices are not built in: rates change and depend on the account. Point
`--prices` at a JSON file and the report adds money; without one it reports
tokens and says so.
"""

from __future__ import annotations

import argparse
import collections
import json
import pathlib
import re
import subprocess
import sys

TOKEN_FIELDS = (
    ("input", "input_tokens"),
    ("cache_write", "cache_creation_input_tokens"),
    ("cache_read", "cache_read_input_tokens"),
    ("output", "output_tokens"),
)


def repo_root(explicit: str | None) -> pathlib.Path:
    if explicit:
        return pathlib.Path(explicit).resolve()
    out = subprocess.run(
        ["git", "rev-parse", "--path-format=absolute", "--git-common-dir"],
        capture_output=True,
        encoding="utf-8",
        check=False,
    )
    if out.returncode != 0:
        sys.exit("Not a git repository, and --repo was not given.")
    # .git of the main worktree — its parent is the repository root, whichever
    # worktree this was run from.
    return pathlib.Path(out.stdout.strip()).parent.resolve()


def project_dirs(root: pathlib.Path, projects: pathlib.Path) -> list[pathlib.Path]:
    """Every transcript directory of this repository, worktrees included.

    Claude Code encodes the working directory into the name by replacing
    everything that is not a letter or a digit with a hyphen; a worktree adds
    its own suffix to the same prefix.
    """
    prefix = re.sub(r"[^A-Za-z0-9]", "-", str(root))
    if not projects.is_dir():
        sys.exit(f"No transcripts directory: {projects}")
    return sorted(d for d in projects.iterdir() if d.is_dir() and d.name.startswith(prefix))


def transcripts(dirs: list[pathlib.Path]):
    """(path, role) for every transcript, the orchestrator's and the agents'."""
    for d in dirs:
        for f in sorted(d.glob("*.jsonl")):
            yield f, "orchestrator"
        for sub in sorted(d.glob("*/subagents")):
            for f in sorted(sub.glob("*.jsonl")):
                meta = f.with_suffix("").with_suffix(".meta.json")
                role = "subagent"
                if meta.is_file():
                    try:
                        role = json.loads(meta.read_text(encoding="utf-8")).get(
                            "agentType", "subagent"
                        )
                    except (json.JSONDecodeError, OSError):
                        pass
                yield f, role


def task_of(branch: str) -> str:
    """A task branch becomes its id; anything else is shown as it is."""
    if re.match(r"^(feat|bug|hot|tech|mod|doc)/[0-9]{3}-", branch):
        return branch.replace("/", "-", 1)
    return branch or "-"


def collect(dirs: list[pathlib.Path], since: str | None):
    seen: set[str] = set()
    records = []
    skipped_dupes = 0
    for path, role in transcripts(dirs):
        try:
            handle = path.open(encoding="utf-8", errors="replace")
        except OSError:
            continue
        with handle:
            for line in handle:
                try:
                    entry = json.loads(line)
                except json.JSONDecodeError:
                    continue
                if entry.get("type") != "assistant":
                    continue
                message = entry.get("message") or {}
                usage = message.get("usage") or {}
                if not usage:
                    continue
                uuid = entry.get("uuid")
                if uuid in seen:
                    skipped_dupes += 1
                    continue
                seen.add(uuid)
                stamp = entry.get("timestamp") or ""
                if since and stamp[:10] < since:
                    continue
                records.append(
                    {
                        "task": task_of(entry.get("gitBranch") or ""),
                        "role": role,
                        "model": message.get("model") or "?",
                        "session": entry.get("sessionId") or "?",
                        "date": stamp[:10],
                        "tokens": {n: usage.get(k, 0) or 0 for n, k in TOKEN_FIELDS},
                    }
                )
    return records, skipped_dupes


def load_prices(path: str | None) -> dict:
    if not path:
        return {}
    p = pathlib.Path(path)
    if not p.is_file():
        sys.exit(f"No such price file: {path}")
    data = json.loads(p.read_text(encoding="utf-8"))
    return data.get("per_million_tokens", data)


def cost_of(tokens: dict, model: str, prices: dict) -> float | None:
    rates = prices.get(model)
    if not rates:
        return None
    return sum(tokens[name] / 1_000_000 * rates.get(name, 0) for name in tokens)


def aggregate(records, key):
    buckets = collections.defaultdict(
        lambda: {"calls": 0, "tokens": collections.Counter(), "models": set(), "cost": 0.0, "priced": True}
    )
    for r in records:
        b = buckets[r[key]]
        b["calls"] += 1
        b["tokens"].update(r["tokens"])
        b["models"].add(r["model"])
        if r["cost"] is None:
            b["priced"] = False
        else:
            b["cost"] += r["cost"]
    return buckets


def render(title, buckets, priced_at_all):
    print(f"\n=== {title} ===\n")
    head = f"{'':<32} {'calls':>6} {'output':>11} {'cache write':>13} {'cache read':>14}"
    if priced_at_all:
        head += f" {'cost':>10}"
    print(head)
    order = sorted(buckets.items(), key=lambda kv: -kv[1]["tokens"]["cache_read"])
    for name, b in order:
        line = (
            f"{str(name)[:31]:<32} {b['calls']:>6} {b['tokens']['output']:>11,}"
            f" {b['tokens']['cache_write']:>13,} {b['tokens']['cache_read']:>14,}"
        )
        if priced_at_all:
            line += f" {b['cost']:>10,.2f}" if b["priced"] else f" {'—':>10}"
        print(line)


def own_prefix(dirs: list[pathlib.Path]) -> tuple[int, pathlib.Path] | None:
    """The prefix this very session is carrying, from its own transcript.

    Claude Code appends to the transcript as the session runs, so the last
    assistant turn already on disk is this session's current context size to
    within one turn. That makes the "new session or carry on" rule in
    docs/ai-workflow/handoff.md a measurement rather than a feeling — the
    only row of it that needs a number is the >=250K one.

    The newest file wins: a session writes to its transcript continuously, so
    the most recently modified one is the caller's own.
    """
    files = [f for d in dirs for f in d.glob("*.jsonl")]
    if not files:
        return None
    newest = max(files, key=lambda f: f.stat().st_mtime)
    last = 0
    with newest.open(encoding="utf-8", errors="replace") as handle:
        for line in handle:
            try:
                entry = json.loads(line)
            except json.JSONDecodeError:
                continue
            if entry.get("type") != "assistant":
                continue
            usage = (entry.get("message") or {}).get("usage") or {}
            if usage:
                last = usage.get("cache_read_input_tokens", 0) or 0
    return last, newest


def report_self(dirs: list[pathlib.Path]) -> int:
    found = own_prefix(dirs)
    if not found:
        print("No transcript to read — fall back to the cruder test in")
        print("docs/ai-workflow/handoff.md: a task already finished in this")
        print("session means the next one starts in a new session.")
        return 1
    prefix, path = found
    print(f"Prefix now:  {prefix:,} tokens  ({path.name})")
    if prefix >= 250_000:
        print("Band:        >=250K — hand the next task over even if the rule")
        print("             would otherwise let you carry on: a compaction is")
        print("             about to fire, and it will fire mid-task.")
    else:
        print("Band:        under 250K — the prefix does not force a handoff.")
        print("             Decide by the table in docs/ai-workflow/handoff.md:")
        print("             review/QA of your own work, or an independent next")
        print("             task, still means a new session.")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--repo", help="repository root (default: this one)")
    parser.add_argument(
        "--projects",
        default=str(pathlib.Path.home() / ".claude" / "projects"),
        help="Claude Code transcripts directory",
    )
    parser.add_argument("--prices", help="JSON with per-million-token rates per model")
    parser.add_argument("--task", help="only this task id or short id (tech-004)")
    parser.add_argument("--since", help="only entries from this date (YYYY-MM-DD)")
    parser.add_argument(
        "--by",
        choices=["task", "role", "model", "session", "all"],
        default="all",
        help="grouping (default: all three tables)",
    )
    parser.add_argument("--json", action="store_true", help="machine-readable output")
    parser.add_argument(
        "--self",
        dest="own",
        action="store_true",
        help="this session's own prefix, and what it means for the next task",
    )
    args = parser.parse_args()

    root = repo_root(args.repo)
    dirs = project_dirs(root, pathlib.Path(args.projects))
    if not dirs:
        sys.exit(f"No transcripts for {root} under {args.projects}")

    if args.own:
        return report_self(dirs)

    records, dupes = collect(dirs, args.since)
    if args.task:
        records = [r for r in records if r["task"].startswith(args.task)]
        if not records:
            sys.exit(f"Nothing recorded for '{args.task}'.")

    prices = load_prices(args.prices)
    for r in records:
        r["cost"] = cost_of(r["tokens"], r["model"], prices)
    priced_at_all = bool(prices)
    unpriced = sorted({r["model"] for r in records if r["cost"] is None}) if prices else []

    if args.json:
        print(json.dumps(records, indent=2))
        return 0

    print(f"Repository:  {root}")
    print(f"Transcripts: {len(dirs)} directories, {len(records):,} turns"
          f" ({dupes:,} duplicate entries skipped)")
    if args.task:
        print(f"Filter:      task {args.task}")
    if args.since:
        print(f"Since:       {args.since}")

    wanted = ["task", "role", "model"] if args.by == "all" else [args.by]
    titles = {"task": "by task", "role": "by role", "model": "by model", "session": "by session"}
    for key in wanted:
        render(titles[key], aggregate(records, key), priced_at_all)

    total = collections.Counter()
    for r in records:
        total.update(r["tokens"])
    print(
        f"\nTotals: {len(records):,} turns · output {total['output']:,}"
        f" · cache write {total['cache_write']:,} · cache read {total['cache_read']:,}"
    )
    ratio = total["cache_read"] / total["output"] if total["output"] else 0
    print(f"cache read is {ratio:.0f}x the output volume — that is where the bill lives.")

    if not prices:
        print("\nTokens only: pass --prices <file> with per-million rates per model for money.")
    elif unpriced:
        print(f"\nNo rate for: {', '.join(unpriced)} — those rows show a dash instead of a cost.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
