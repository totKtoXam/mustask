#!/usr/bin/env python3
"""PreToolUse hook: blocks a search/listing of the whole disk or the whole
profile.

Reads a PreToolUse event as JSON from stdin (the tool_input.command field —
the raw command line, as the shell sees it), decides whether to block the
call, and reports the decision through the exit code — the only channel
that stays stable over time and does not depend on the exact field names
inside the nested JSON:

    exit 0  — allow, print nothing (silent).
    exit 2  — block; stderr goes to the agent as the reason and a hint on
              where to go instead.

Fault tolerance — deliberately fail-open: any input-parsing error (invalid
JSON, `tool_input`/`cwd` of an unexpected type, an internal exception)
leads to `exit 0`, not a crash with a traceback. A false ALLOW on a
parsing failure is safer than a legitimate command hanging or being
blocked by an internal hook error the agent can't diagnose. See the
`try/except` in `main()`.

Why this is a separate script and not just `permissions.deny`: deny
matches a command's PREFIX against a fixed list of pattern strings
(broken by compound commands, quoting, different flag order). Here it's a
real (simplified) command-line parse: compound commands are split into
subcommands on `&&`, `||`, `;`, `|`, `|&`, `&`, line breaks; a working
directory change via `cd` is tracked; paths are resolved relative to it.
This is not a full shell parser (nor should it be — the protection
boundary is deliberately limited: the hook closes known practical holes,
not every conceivable way to bypass the ban; see .claude/hooks/README.md).

The offline command table for verification is
.claude/hooks/tests/cases.tsv, run by .claude/hooks/tests/run_cases.py
without a live session.
"""

from __future__ import annotations

import json
import os
import posixpath
import re
import shlex
import sys

# --- Splitting the command line into subcommands -----------------------------

_OP2 = ("&&", "||", "|&")
_OP1 = (";", "|", "&", "\n")


def split_subcommands(command: str, powershell: bool) -> list[str]:
    """Splits a compound command on unescaped `&&`, `||`, `;`, `|`, `&`.

    Accounts for single/double quotes: a separator inside quotes does not
    count as a separator. In Bash a backslash escapes the next character
    (including a separator); in PowerShell a backslash is an ordinary
    path character (`C:\\`), it does not escape.
    """
    parts: list[str] = []
    buf: list[str] = []
    quote: str | None = None
    i = 0
    n = len(command)
    while i < n:
        ch = command[i]
        if quote:
            buf.append(ch)
            if ch == quote:
                quote = None
            i += 1
            continue
        if ch in ("'", '"'):
            quote = ch
            buf.append(ch)
            i += 1
            continue
        if not powershell and ch == "\\" and i + 1 < n:
            buf.append(ch)
            buf.append(command[i + 1])
            i += 2
            continue
        two = command[i : i + 2]
        if two in _OP2:
            parts.append("".join(buf))
            buf = []
            i += 2
            continue
        if ch in _OP1:
            parts.append("".join(buf))
            buf = []
            i += 1
            continue
        buf.append(ch)
        i += 1
    parts.append("".join(buf))
    return [p for p in (p.strip() for p in parts) if p]


def tokenize(subcommand: str, powershell: bool) -> list[str]:
    if powershell:
        # Bash shlex semantics are wrong here: `\` in Windows paths is not
        # an escape character, and `C:\` with nothing after it (end of
        # token) crashes shlex's posix mode with a "No escaped character"
        # exception.
        tokens: list[str] = []
        buf: list[str] = []
        quote: str | None = None
        for ch in subcommand:
            if quote:
                if ch == quote:
                    quote = None
                else:
                    buf.append(ch)
                continue
            if ch in ("'", '"'):
                quote = ch
                continue
            if ch.isspace():
                if buf:
                    tokens.append("".join(buf))
                    buf = []
                continue
            buf.append(ch)
        if buf:
            tokens.append("".join(buf))
        return tokens
    try:
        return shlex.split(subcommand, posix=True)
    except ValueError:
        # Unbalanced quotes and the like — don't crash the hook, work
        # with what we have via a naive split. An error on the command's
        # own side will show up on its own once the shell actually runs
        # it.
        return subcommand.split()


_WRAPPERS = {"sudo", "time", "nice", "nohup", "env", "xargs"}
_ASSIGN_RE = re.compile(r"^[A-Za-z_][A-Za-z0-9_]*=")

# Wrapper flags that take a SEPARATE next token as their own argument
# (unlike `-n`/`-i` etc., which are self-contained, or `-I{}`, where the
# argument is already glued to the flag in the same token). The list is
# practical, not an exhaustive full man page — enough not to lose the
# start of the real command when looking for it after the wrapper.
_WRAPPER_ARG_FLAGS: dict[str, set[str]] = {
    "sudo": {"-u", "-g", "-p", "-h", "-C", "-D", "-U", "-r", "-T", "-a"},
    "env": {"-u", "-C", "-S"},
    "nice": {"-n"},
    "xargs": {"-I", "-i", "-L", "-n", "-P", "-s", "-d", "-a", "-E", "-l", "-x"},
}


def strip_wrappers(words: list[str]) -> list[str]:
    """Strips `VAR=val`, `sudo`, `time`, `nice`, `nohup`, `env`, `xargs`,
    `timeout N` — together with their own flags (including `sudo -n`,
    `sudo -u root`, `nice -n 10`, `env -i`, `xargs -I{}`), not just the
    bare wrapper name."""
    i = 0
    while i < len(words):
        w = words[i]
        if _ASSIGN_RE.match(w):
            i += 1
            continue
        if w == "timeout":
            i += 1
            while i < len(words) and words[i].startswith("-"):
                i += 1
            if i < len(words):
                i += 1  # duration
            continue
        if w in _WRAPPERS:
            arg_flags = _WRAPPER_ARG_FLAGS.get(w, frozenset())
            i += 1
            while i < len(words) and words[i].startswith("-"):
                flag = words[i]
                i += 1
                if flag == "--":
                    break
                if flag in arg_flags and i < len(words):
                    i += 1  # a separate flag argument, not the command start
            continue
        break
    return words[i:]


# --- Path resolution and the "too wide" check ---------------------------------

_HOME_CANDIDATES = [
    os.environ.get("HOME", ""),
    os.environ.get("USERPROFILE", "").replace("\\", "/"),
]
_HOME_CANDIDATES = [h for h in _HOME_CANDIDATES if h]

# `$HOME`/`${HOME}`/`$USERPROFILE`/`${USERPROFILE}`/`$env:USERPROFILE`/
# `%USERPROFILE%` — the environment variables actually used to search for
# NuGet packages in the incident (`find $HOME/.nuget/...`, `Get-ChildItem
# $env:USERPROFILE -Recurse`). The hook does not execute a shell and does
# not know the real value of the variable in the agent's session — it
# substitutes its own idea of the home directory (the same one already
# used for `~`); a textual match, not real variable expansion.
_HOME_VAR_RES = [
    re.compile(r"^\$\{HOME\}"),
    re.compile(r"^\$HOME(?![A-Za-z0-9_])"),
    re.compile(r"^\$\{USERPROFILE\}"),
    re.compile(r"^\$USERPROFILE(?![A-Za-z0-9_])"),
    re.compile(r"^\$env:USERPROFILE(?![A-Za-z0-9_])", re.I),
    re.compile(r"^%USERPROFILE%", re.I),
]


def _expand_home_vars(p: str) -> str:
    if not _HOME_CANDIDATES:
        return p
    home = _HOME_CANDIDATES[0]
    for pat in _HOME_VAR_RES:
        m = pat.match(p)
        if m:
            return home + p[m.end():]
    return p


def _norm(path: str) -> str:
    p = path.replace("\\", "/")
    p = posixpath.normpath(p)
    if p != "/" and p.endswith("/"):
        p = p[:-1]
    return p


def resolve(raw: str, cwd: str) -> str:
    """Resolves `raw` to an absolute path relative to the (simulated) `cwd`."""
    p = _expand_home_vars(raw)
    if p == "~":
        p = _HOME_CANDIDATES[0] if _HOME_CANDIDATES else "~"
    elif p.startswith("~/"):
        p = (_HOME_CANDIDATES[0] if _HOME_CANDIDATES else "~") + p[1:]
    p = p.replace("\\", "/")
    is_abs = bool(re.match(r"^([A-Za-z]:)?/", p))
    if not is_abs:
        p = cwd.rstrip("/") + "/" + p
    return _norm(p)


# The Git Bash drive form (`/c`, `/d`, ...) is generalized to any drive
# letter, symmetric with the Windows-native form (`C:`, `D:`, ...) — this
# used to hardcode only `/c`, which meant `find /d` (the POSIX form of
# another drive) passed through while `Get-ChildItem D:\ -Recurse` (the
# native form of the same drive) was already blocked: an asymmetry for no
# reason, drive D: is no safer than drive C:.
_ROOT_LIKE_RES = [
    re.compile(r"^/$"),
    re.compile(r"^/[a-z]$", re.I),
    re.compile(r"^[a-z]:$", re.I),
    re.compile(r"^/[a-z]/users$", re.I),
    re.compile(r"^[a-z]:/users$", re.I),
    re.compile(r"^/[a-z]/users/[^/]+$", re.I),
    re.compile(r"^[a-z]:/users/[^/]+$", re.I),
]


def is_root_like(resolved_path: str) -> bool:
    return any(r.match(resolved_path) for r in _ROOT_LIKE_RES)


# --- Rules for specific commands -----------------------------------------------

_FIND_PRE_PATH_OPTS = {"-H", "-L", "-P", "-O0", "-O1", "-O2", "-O3"}


def find_maxdepth(args: list[str]) -> int | None:
    """The value of `-maxdepth N`/`-maxdepth=N`, if present and parseable."""
    for i, a in enumerate(args):
        if a == "-maxdepth" and i + 1 < len(args):
            try:
                return int(args[i + 1])
            except ValueError:
                return None
        if a.startswith("-maxdepth="):
            try:
                return int(a.split("=", 1)[1])
            except ValueError:
                return None
    return None


def find_paths(args: list[str]) -> list[str]:
    idx = 0
    while idx < len(args) and args[idx] in _FIND_PRE_PATH_OPTS:
        idx += 1
    if idx < len(args) and args[idx] == "-D":
        idx += 2
    paths: list[str] = []
    while idx < len(args) and not args[idx].startswith("-") and args[idx] not in ("(", "!", ")"):
        paths.append(args[idx])
        idx += 1
    return paths


_RECURSIVE_SHORT_RE = re.compile(r"^-[A-Za-z]*[rR][A-Za-z]*$")


def grep_is_recursive(args: list[str]) -> bool:
    for a in args:
        if a in ("-r", "-R", "--recursive"):
            return True
        if a.startswith("--recursive"):
            return True
        if a.startswith("-") and not a.startswith("--") and _RECURSIVE_SHORT_RE.match(a):
            return True
    return False


def grep_paths(args: list[str]) -> list[str]:
    non_flags: list[str] = []
    has_e_flag = False
    skip_next = False
    for a in args:
        if skip_next:
            skip_next = False
            continue
        if a in ("-e", "--regexp", "-f", "--file"):
            has_e_flag = True
            skip_next = True
            continue
        if a.startswith("-"):
            continue
        non_flags.append(a)
    if has_e_flag:
        return non_flags
    return non_flags[1:]  # the first non-flag token is the pattern, not a path


def ls_is_recursive(args: list[str]) -> bool:
    for a in args:
        if a in ("-R", "--recursive"):
            return True
        if a.startswith("--recursive"):
            return True
        if re.match(r"^-Rec", a, re.I):
            return True
        if a.startswith("-") and not a.startswith("--") and "R" in a[1:]:
            return True
    return False


def non_flag_args(args: list[str], extra_flag_re: re.Pattern | None = None) -> list[str]:
    out = []
    for a in args:
        if a.startswith("-"):
            continue
        if extra_flag_re and extra_flag_re.match(a):
            continue
        out.append(a)
    return out


_DIR_CMD_FLAG_RE = re.compile(r"^/[A-Za-z]$")


def gci_is_recursive(args: list[str], base: str) -> bool:
    for a in args:
        if re.match(r"^-Rec", a, re.I):
            return True
        if base == "dir" and re.match(r"^/S$", a, re.I):
            return True
    return False


def gci_paths(args: list[str], base: str) -> list[str]:
    paths: list[str] = []
    i = 0
    while i < len(args):
        a = args[i]
        low = a.lower()
        if low in ("-path", "-literalpath"):
            if i + 1 < len(args):
                paths.append(args[i + 1])
                i += 2
                continue
        if low.startswith("-path=") or low.startswith("-literalpath="):
            paths.append(a.split("=", 1)[1])
            i += 1
            continue
        if a.startswith("-") or (base == "dir" and _DIR_CMD_FLAG_RE.match(a)):
            i += 1
            continue
        paths.append(a)
        i += 1
    return paths


# --- The main check for a single subcommand -------------------------------------


def check_subcommand(words: list[str], cwd: str) -> tuple[bool, str] | None:
    """Returns (block?, human-readable description), or None if no rule
    applies to this command."""
    if not words:
        return None
    base = os.path.basename(words[0])
    base = re.sub(r"\.exe$", "", base, flags=re.I)
    base_l = base.lower()
    args = words[1:]

    if base_l in ("bash", "sh", "zsh") and "-c" in args:
        # `bash -c '...'` / `sh -c '...'` — the real command isn't the
        # first token but the string argument of `-c`; quotes have
        # already been stripped by the tokenizer (the whole text arrived
        # as one word), run it through the same evaluate.
        idx = args.index("-c")
        if idx + 1 < len(args):
            blocked, reason = evaluate(args[idx + 1], cwd, powershell=False)
            if blocked:
                return True, f"{base_l} -c: {reason}"
        return False, ""

    if base_l == "find":
        maxdepth = find_maxdepth(args)
        if maxdepth is not None and maxdepth <= 1:
            # `-maxdepth 0`/`-maxdepth 1` doesn't descend — cheap and safe
            # regardless of the path (can't eat hours of CPU, which is the
            # whole reason this rule exists). It was with exactly this
            # form that the developer verified the facts about `/` and
            # `/proc/registry` in round 2 — it used to be blocked too,
            # which was a false positive on the developer's own safe
            # reconnaissance.
            return False, ""
        paths = find_paths(args) or [cwd]
        for raw in paths:
            resolved = resolve(raw, cwd)
            if is_root_like(resolved):
                return True, f"find on «{raw}» (= {resolved})"
        return False, ""

    if base_l in ("rg", "ripgrep"):
        if "--files" in args:
            candidates = non_flag_args(args) or [cwd]
        else:
            nf = non_flag_args(args)
            candidates = nf[1:] if len(nf) > 1 else ([cwd] if not nf else [])
        for raw in candidates:
            resolved = resolve(raw, cwd)
            if is_root_like(resolved):
                return True, f"rg on «{raw}» (= {resolved})"
        return False, ""

    if base_l in ("grep", "egrep", "fgrep"):
        if not grep_is_recursive(args):
            return False, ""
        candidates = grep_paths(args) or [cwd]
        for raw in candidates:
            resolved = resolve(raw, cwd)
            if is_root_like(resolved):
                return True, f"grep -r on «{raw}» (= {resolved})"
        return False, ""

    if base_l in ("ls", "gci", "dir", "get-childitem"):
        recursive = ls_is_recursive(args) if base_l == "ls" else gci_is_recursive(args, base_l)
        if not recursive:
            return False, ""
        if base_l == "ls":
            candidates = non_flag_args(args) or [cwd]
        else:
            candidates = gci_paths(args, base_l) or [cwd]
        for raw in candidates:
            resolved = resolve(raw, cwd)
            if is_root_like(resolved):
                return True, f"{words[0]} -Recurse/-R on «{raw}» (= {resolved})"
        return False, ""

    if base_l == "du":
        candidates = non_flag_args(args) or [cwd]
        for raw in candidates:
            resolved = resolve(raw, cwd)
            if is_root_like(resolved):
                return True, f"du on «{raw}» (= {resolved})"
        return False, ""

    if base_l == "tree":
        candidates = non_flag_args(args) or [cwd]
        for raw in candidates:
            resolved = resolve(raw, cwd)
            if is_root_like(resolved):
                return True, f"tree on «{raw}» (= {resolved})"
        return False, ""

    return None


SUGGESTION = (
    "Searching the whole disk or the whole user profile is forbidden — it "
    "grinds through node_modules, the NuGet cache, Docker layers, and pins "
    "the CPU for hours (see AGENTS.md, the «Do not search the whole disk» "
    "item in the «Hard rules» section). Instead:\n"
    "  - repository files — the Glob/Grep tools, not find/grep -r/ls -R;\n"
    "  - a build from NuGet — ~/.nuget/packages/<package>/<version>/ "
    "(not the bare ~/.nuget/packages as a whole, but a subpath is fine);\n"
    "  - a project's build output — src/<Project>/bin/Debug/net10.0/;\n"
    "  - the path is unknown — narrow it to a specific subtree "
    "(find src -name '*.cs'), don't start at /, /c, C:\\ or the whole home "
    "directory."
)


_LEADING_KEYWORDS = {"do", "then", "else", "!"}


def _strip_group_chars(sub: str) -> str:
    """Strips one grouping/subshell `(`/`)` glued without a space,
    e.g. `(find / -name x)`. Not full parenthesis balancing — a heuristic
    for the specific, most common form; nested/unbalanced cases are
    outside the guarantee (see the module docstring on the protection
    boundary)."""
    if sub.startswith("("):
        sub = sub[1:]
    if sub.endswith(")"):
        sub = sub[:-1]
    return sub.strip()


def _strip_shell_wrapping(words: list[str]) -> list[str]:
    """Strips `(`/`{`/the keywords `do`/`then`/`else`/`!` from the front
    and `)`/`}` from the back — what's left as separate tokens after
    splitting into subcommands (`{ find / -name x; }` → tokens
    `{ find / -name x` and `}`)."""
    while words and (words[0] in ("(", "{") or words[0] in _LEADING_KEYWORDS):
        words = words[1:]
    while words and words[-1] in (")", "}"):
        words = words[:-1]
    return words


def extract_command_substitutions(command: str) -> list[str]:
    """Extracts the content of `$( ... )` (accounting for nesting) —
    `x=$(find / -name x)`, `echo $(find / -name x)`. Backtick substitution
    (`` `...` ``) is not parsed — it didn't come up during verification,
    left as a known boundary (see AGENTS.md, "What is NOT intercepted")."""
    subs: list[str] = []
    i = 0
    n = len(command)
    while i < n:
        if command[i : i + 2] == "$(":
            depth = 1
            j = i + 2
            start = j
            while j < n and depth > 0:
                if command[j] == "(":
                    depth += 1
                elif command[j] == ")":
                    depth -= 1
                j += 1
            subs.append(command[start : max(j - 1, start)])
            i = j
            continue
        i += 1
    return subs


def evaluate(command: str, cwd: str, powershell: bool = False) -> tuple[bool, str]:
    cwd = _norm(cwd or "/")
    for inner in extract_command_substitutions(command):
        blocked, reason = evaluate(inner, cwd, powershell)
        if blocked:
            return True, reason
    for sub in split_subcommands(command, powershell):
        sub = _strip_group_chars(sub)
        words = _strip_shell_wrapping(tokenize(sub, powershell))
        words = strip_wrappers(words)
        if not words:
            continue
        if os.path.basename(words[0]).lower() in ("cd", "pushd"):
            target = words[1] if len(words) > 1 else "~"
            cwd = resolve(target, cwd)
            continue
        result = check_subcommand(words, cwd)
        if result and result[0]:
            return True, result[1]
    return False, ""


# A megabyte-sized token pushes shlex/our parser into quadratic time and
# risks hitting the hook's timeout on the Claude Code side — a crude
# early exit is cheaper than parsing something no one types by hand.
_MAX_COMMAND_LEN = 50_000


def main() -> int:
    # The Windows console isn't UTF-8 by default (often cp1251/cp866) —
    # without reconfigure, Russian text in stderr either turns into
    # garbage for a reader expecting UTF-8, or, on a locale without
    # Cyrillic (C/POSIX), crashes the write with a UnicodeEncodeError.
    # stderr's encoding is fixed explicitly, not left dependent on the
    # host locale.
    if hasattr(sys.stderr, "reconfigure"):
        try:
            sys.stderr.reconfigure(encoding="utf-8")
        except Exception:
            pass

    try:
        payload = json.load(sys.stdin)
    except (json.JSONDecodeError, ValueError):
        return 0  # couldn't parse the input — don't block blindly

    try:
        tool_name = payload.get("tool_name", "")
        if tool_name not in ("Bash", "PowerShell"):
            return 0

        tool_input = payload.get("tool_input") or {}
        if not isinstance(tool_input, dict):
            return 0
        command = tool_input.get("command", "")
        if not command or not isinstance(command, str):
            return 0
        if len(command) > _MAX_COMMAND_LEN:
            return 0

        powershell = tool_name == "PowerShell"

        cwd = payload.get("cwd") or os.getcwd()
        if not isinstance(cwd, str):
            cwd = os.getcwd()

        blocked, reason = evaluate(command, cwd, powershell)
    except Exception:
        # Unexpected input shape (wrong field type, a parsing failure,
        # etc.) — fail-open, see the module docstring. We don't propagate
        # the exception further: a traceback on stderr with an unblocked
        # return code is already almost indistinguishable from the same
        # thing on a normal exit 0.
        return 0

    if blocked:
        try:
            sys.stderr.write(f"Blocked by the wide-search guard: {reason}.\n\n{SUGGESTION}\n")
        except Exception:
            pass
        return 2
    return 0


if __name__ == "__main__":
    sys.exit(main())
