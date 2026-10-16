# Dispatch: which model runs a stage, and what runs in parallel

Which route a task takes is [routing.md](routing.md). This file is the other
half of the same decision — who executes the stages, on which model, and what
may run at the same time. It is read when the work is handed to agents, not
when the task is classified: keeping it in `routing.md` made every new task
load it in order to produce one line of classification.

## Model routing — which model at which stage

The principle: **the expensive/smart model where a mistake is costly or
thinking is needed; the fast one where the work is mechanical.** Model names
change, so we fix the levels:

Vendor adapters resolve these levels independently: Claude Code — through the
aliases in `.claude/agents/*.md`, Codex — through `model` and
`model_reasoning_effort` in `.codex/agents/*.toml`. The level table describes
the purpose of the class, not the eternal name of a concrete model.

| Level | Claude | Codex/OpenAI | Use |
|---------|--------|--------------|---------------|
| `deep` | Opus / the senior model, extended thinking | `gpt-5.6`, reasoning high | Architectural decisions, business analysis, requirements review, QA of `L` tasks, code review, digging into complex bugs |
| `standard` | Sonnet | `gpt-5.6-terra`, reasoning medium | Design, writing code and tests, QA of `S`/`M` tasks |
| `fast` | Haiku | `gpt-5.6-luna`, reasoning low | Mechanics: renamings, generating boilerplate from a sample, formatting, draft summaries |

**The only source of the "role → level" mapping is the table below.** A stage
file repeats its own tier in the header, and that copy is checked against this
table: the header must **lead** with the role's default level, overrides come
after it in parentheses. Until that check existed, `01-business-analysis.md`
said `standard` while this table, both vendor profiles and every actual run
said `deep` — for months, because nothing compared them. Vendor
profiles are obliged to conform to it; `scripts/validate-ai-workflow.py`
deterministically cross-checks the Claude and Codex configurations, so an
unnoticed drift between the two copies no longer passes `verify`.

| Role | Default level | Override |
|------|----------------------|----------|
| business-analyst | `standard` | `L`, or a spec rewritten after `gate-1: rejected` → `deep` |
| requirements-reviewer | `deep` | — |
| ux-designer | `standard` | — |
| backend-developer | `standard` | `L` plan → `deep` |
| frontend-developer | `standard` | — |
| qa-engineer | `standard` | QA of `L` → `deep` |
| code-reviewer | `deep` | — |
| task-executor | `standard` | — |

`task-executor` is not a stage; it is the role the orchestrator hands a
delegated run to instead of taking it in its own context at three times
the price — [stages/00-orchestrator.md](stages/00-orchestrator.md).

The principles by which the levels are assigned:

- deep — checking the requirements (2), the review (8), and the plan/QA of an
  `L`: a mistake there is either unrecoverable or expensive;
- standard — execution against ready artifacts (design, code, QA of `S`/`M`),
  and writing the spec of an `M` inside an existing slice: stories and
  criteria from a queue line are not an architectural decision, and the
  adversarial pass over them is a separate `deep` stage that follows;
- fast — mechanics (changelog, PR text).

### What a run actually costs — measure, do not guess

The session transcripts already carry it: every assistant turn records its
model and its token counts, and every entry carries `gitBranch`, so spend
resolves to a task. Subagents write their own transcripts with the role in a
sibling `meta.json`. `python scripts/usage-report.py` reads all of it —
`--by task|role|model`, `--task <id>`, `--prices <file>` for money.

Two numbers from the first run over this repository, and they are the reason
this section exists:

- **`cache_read` is ~200x the output volume.** Cached input, not generated
  text, is what the bill is made of — which makes the size of what a stage
  loads the first-order cost, ahead of which model runs it.
- **The role table below governs about 6% of the spend.** Roughly 80% is the
  orchestrator's own session, and ad-hoc `general-purpose` subagents cost
  twice all the pipeline roles put together. Retuning a tier here moves a
  fraction of that 6%; it is worth doing for correctness, not as an economy.

Before changing a tier in the table, run the report and check the change is
aimed at something that is actually being spent.

### The cost of a run is a criterion too

The model level is not the only source of cost. In the original project the
backend QA of a single task took 11 minutes and 67 tool calls, and the larger
part of that was repeated `dotnet build`/`test` and bringing up Testcontainers,
not thinking. The rules for economising are in the QA sections of stages 4–7
and in [stages/s-route.md](stages/s-route.md); the main one: **what is green
is not re-checked by running the same thing again**.

## Parallelism

- Independent features are run in separate branches/worktrees — one session per feature.
- Inside a feature: backend QA (stage 5) can go in parallel with the start of
  the frontend (stage 6), since the frontend works against `api-contract.md`
  and mocks. On an `M` with both layers there is no separate stage 5: one QA
  pass runs after stage 6 ([stages/05-frontend.md](stages/05-frontend.md), § 7).
- Reviewers (stages 2, 8) are always **a separate session/subagent with a
  clean context**, never the same context that wrote the artifact: a reviewer
  who has seen the author's train of thought inherits their blind spots.
- Inside one backend task, use cases can run as **lanes** — one agent each,
  after a sequential skeleton that includes one use case implemented as the
  reference ([stages/04-backend.md](stages/04-backend.md), § 4c). The trigger
  is four or more disjoint write zones, not the task's size. Dispatch in
  waves on a large module, keep the verification run single, and let the
  orchestrator do the committing: lanes are several agents in one worktree,
  which the "one worktree per stream" convention does not cover.

Parallelism buys the active hour, not the calendar day. Across 30 tasks here,
78% of a task's wall clock is waiting — 58% of it in nine pauses longer than
two hours — and only 10% is the short thinking turns that running agents side
by side can compress.
