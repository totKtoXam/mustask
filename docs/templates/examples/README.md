# docs/templates/examples/ — filled task folders, for illustration

Two finished task folders, kept to show what the artifacts look like when
they are filled in — the current `status.md` format, the artifact split of
[docs/features/README.md](../../features/README.md), review rounds, QA
reports, the log:

| Folder | What it shows |
|---|---|
| [tech-172-invoke-sh-utf8-repo-root](tech-172-invoke-sh-utf8-repo-root/status.md) | an `S` tooling task: `status.md` with an inline plan, and `review.md` with QA and three code-review rounds |
| [tech-159-e2e-without-password](tech-159-e2e-without-password/status.md) | a full-cycle task: `spec.md` with story files, `plan-backend.md`/`plan-frontend.md`, `qa.md`, `review.md`, `history.md` |

**These are not tasks.** They live outside `docs/features/` on purpose, so
that numbering ([numbering.md](../../features/numbering.md)), the queue,
`scripts/backlog.sh`, `scripts/new-task.sh` and `scripts/check-gates.sh`
never see them. Their ids take no number from any counter and are not in
the backlog.

**Ids, dates, commit hashes and paths are historical.** The folders were
written in an earlier project and lightly neutralized for reuse here: other
task ids they mention, commit hashes, and files such as a stand runbook or a
second (`partner`) realm do not exist in this repository. Read them for the
shape of the work, not as facts about this codebase.

**Their length is not the model.** Both folders predate the artifact budgets
([docs/features/README.md](../../features/README.md), "Artifact budgets")
and are two to three times over them — tech-159's `review.md` is 105 KB and
its `qa.md` 61 KB, tech-172's `review.md` 55 KB. Copy the structure, not the
length.
