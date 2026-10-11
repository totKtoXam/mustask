# tech-021-pipeline-ceremony-caps — code review

## Round 1

Scope: `git diff main...HEAD` (6 files, docs + `scripts/verify.sh`), checked against `status.md` `## Plan` items 1-5. All five plan items are present. `bash scripts/verify.sh` exits 0 on the branch.

Verdict: **changes-requested**. 4 major, 2 minor. Every fix is prose, so per 06-code-review.md the next step is one delta read and no QA session.

### Findings

**F1 — major — `.claude/commands/ship.md:14-15`** (direction 2). "Blocker/major findings → send back … repeat QA on the affected part, at most 2 rounds." This is the old "at most 2 fix rounds" cap that the diff removed from 06-code-review.md:60. An orchestrator that drives stage 8 through `/ship` on an S or M still runs two rounds, which is exactly the feat-027 pattern this task exists to stop. It also re-runs QA after every fix, but 06-code-review.md:41-43 skips QA for a prose-only fix. Fix: replace "at most 2 rounds" with "at most the delta reads routing.md 'Ceremony caps by size' allows (S/M: one, L: two)". Replace "repeat QA on the affected part" with "QA re-runs only when the fix changed runtime code or tests (06-code-review.md)".

**F2 — major — `docs/ai-workflow/routing.md:272` and `:286`, against `stages/04-backend.md:136-137`, `stages/05-frontend.md:105-106` and `stages/s-route.md:33`** (direction 2). The table gives S/M one QA session and L two, and line 286 says "No pass outside this table". Three files the diff does not touch say otherwise:
- QA defect re-checks: "at most two re-checks" (04) and "at most twice" (05);
- S QA: "the cycle runs until zero blockers" (s-route).

Faced with a QA major on an M, the orchestrator does the wrong thing under one of the two rules. Fix (no decision needed, keeps current behaviour): rename the row "QA passes (defect re-checks per 04-backend.md § 5 item 3)", or add a sentence below the table saying QA re-checks after blocker/major defects follow the stage files and are not extra passes. If the human wants QA re-checks capped as well, that is a new row and a decision, not part of this fix.

**F3 — major — `docs/ai-workflow/routing.md:273` and `:282-285`, against `stages/05-frontend.md:114-117`** (direction 2, and partly within the diff). Line 283 says "A second full run is owed only to the warm-stand rule". 05-frontend.md names two exceptions: the re-run when the first run was against mocks rather than the real backend, and the warm-stand re-run. Read literally, routing.md forbids the real-backend run after a mocks run. Within the diff itself, the table cell "at most 1" contradicts the bullet that allows a second run. Fix: make line 283 "…owed only to the two exceptions in 05-frontend.md, 'Run economy' paragraph (a first run against mocks, a warm stand)", and make the cell "at most 1 (+ those exceptions)".

**F4 — major — `docs/ai-workflow/routing.md:253-258`**. "An `M` holds at most three user stories" is stated as a limit, with the reason "the story count is known before any work starts". For an M it is not. Stories are written at stage 01, after classification, and 01-business-analysis.md (Rules, :74-82) has no story-count stop, only per-file line budgets. The size checklist (:130-153) does not ask the question either. A BA that writes a fourth story into an M meets nothing that stops it, so the limit the orchestrator relies on is not there. Fix: change the reason to "estimated at classification from the line", and add one Rules bullet to 01-business-analysis.md: "an `M` whose story table reaches a fourth story: return a split proposal to the orchestrator (routing.md, 'Route shortcuts for M')". Optionally add the question to checklist block 2.

**F5 — minor — `scripts/verify.sh:212`, and `docs/features/README.md:162-163`**. In Git Bash with `LANG` unset, which is the default here and is also what `verify.ps1` → `invoke-sh.ps1` passes through, gawk 5.0 `length()` counts bytes. I tested this: `printf 'жж\n' | awk '{print length}'` → 4, and with `LC_ALL=C.UTF-8` → 2. A 207-character Cyrillic entry was flagged as over 300. Em dashes and arrows are 3 bytes each, and this repo's prose uses them. The README also says verify "names each" long entry, but the script prints one count per file. An entry wrapped onto continuation lines escapes the check. It is a note, not a failure, so no agent action changes. CRLF is handled correctly: I tested `sub(/\r$/,"")` on a CRLF file. The loop also survives `set -eu` when the last file has k=0, and simulation hit warn at 16 KB and fail at 28 KB. Fix: `LC_ALL=C.UTF-8 awk …` (where that locale is missing it falls back to byte counts, which is harmless). Change the README to "counts, per changed `status.md`, the `## Log` lines over 300 characters".

**F6 — minor — `.claude/agents/code-reviewer.md:22`** (direction 2). This line always prescribes a side heading (`## Round 1 — backend`). 06-code-review.md:73-75 now keeps the side only for an L reviewed by side. Fix: "`## Round 1` (an L reviewed by side: `## Round 1 — backend`)".

### Behavioural claims checked

- routing.md:253-258, M ≤ 3 stories, split at classification → F4.
- routing.md:262-265, the caps are a ceiling and a pass beyond them only on the human's ask, logged → instruction anchor 00-orchestrator.md:54-57 plus the orchestrator role that owns dispatch (00-orchestrator.md:50-58). This is the weakest anchor available.
- routing.md:269-271, rounds / delta reads / fan-out per size → instruction anchor 06-code-review.md:60-66 plus the code-reviewer agent bound to stage 06 (weakest anchor). Contradicted by ship.md:14-15 → F1.
- routing.md:272, QA sessions per size → F2.
- routing.md:273 and :282-285, E2E run count and re-runs → F3.
- routing.md:275-278, whole-diff round after the last dev stage on S/M → instruction anchor 06-code-review.md:61-62 (weakest anchor). No stage file schedules an early backend round on S/M (grep "backend round", "by side": only 06:74 for L).
- routing.md:286-289, no pass outside the table → instruction-only (00-orchestrator.md:54-57). Overlaps F2.
- routing.md:291-298, history: feat-027 `status.md` 73 lines / 35298 B (`git show main:…`), bug-002 `review.md` 361 lines → verified. The hour figures are rationale and were not checked.
- 00-orchestrator.md:54-57 → instruction anchor plus the orchestrator role (weakest).
- 06-code-review.md:60-66, cap by size → anchored to routing.md's table (prose to prose), so the real anchor is the same instruction as above. ship.md contradicts it → F1.
- 06-code-review.md:73-75, round headings → instruction anchor. code-reviewer.md:22 contradicts it → F6.
- features/README.md:138-142, a Log entry is one line ≤ 300 characters → enforced only as a note, verify.sh:211-217, which matches the README's "a note, not a failure". Characters vs bytes → F5.
- features/README.md:153-157, byte columns → verify.sh:205-209 (24576 / 12288; simulated 16 KB → warn, 28 KB → fail). The 8 KB target is unenforced and labelled "target", which is fine. On a CRLF working copy `wc -c` counts the CRs (≤ 250 B at the line limit), so the result is a little platform-dependent. Noted, not a finding.
- features/README.md:159-164, verify names long entries → F5.
- verify.sh:195-196 and :211 comments → match the code, apart from F5.
- **Direction 2.** Behaviours swept:
  - review rounds and delta reads (grep "fix rounds", "at most 2", "round 4", "rounds", "re-check", "delta" in docs/ai-workflow, .claude/agents, .claude/commands, AGENTS.md files): hits ship.md:15 → F1; 04-backend.md:137, 05-frontend.md:105, s-route.md:33 → F2. The Round 2 at 02-requirements-review.md:45-48 is the stage-2 loop and is not affected.
  - round headings ("Round 1 — backend", "## Round"): code-reviewer.md:22 → F6.
  - E2E run count ("E2E run", "full run", "re-run"): 05-frontend.md:114-117 → F3. dispatch.md:90 is consistent.
  - status.md budget and Log entry length ("250 lines", "150 lines", "budget", "Newest first", "log entr"): none outside README and verify.sh. 01-business-analysis.md:75 is spec.md's budget, unrelated. The new-task.sh:313-316 skeleton writes short entries, consistent.
  - stories per M: 01-business-analysis.md has no rule → F4.
  - Not swept: docs/templates/examples (archive), other features' folders.

## Round 2 (delta)

Scope: `git show 0e8f7b7` against F1-F6, plus any contradiction the fix itself creates. `bash scripts/verify.sh` is green. Verdict: **approve**. One new minor, which is a debt.

- F1 closed: ship.md:14-17 points at the caps and re-runs QA only for runtime/test fixes, which matches 06-code-review.md:41-43.
- F2 closed: the routing.md row is renamed and a new bullet binds re-checks to 04-backend.md § 5 item 3. s-route.md:33-35 now matches 04 (at most two, a minor is a debt), and so does 05-frontend.md:105-106.
- F3 closed: routing.md:286-290 names both exceptions, and the paragraph after step 5 in 05-frontend.md matches. The table cells agree with the bullet.
- F4 closed: routing.md:255-262 gives the reason as an estimate, and 01-business-analysis.md Rules now carries the stop at stage 1, where the stories are written.
- F5 closed: `LC_ALL=C.UTF-8 awk`, retested on the round-1 CRLF fixture: only the 301-character ASCII line counts and the 207-character Cyrillic one does not. README:162 now says "counts, per file".
- F6 closed: code-reviewer.md:22-23.
- **F7 — minor (debt) — routing.md:292-294.** It paraphrases 04-backend.md § 5 item 3 as re-checks "over the fixes only". 04:136-137 says the re-check "covers the fixes and what they could have broken". A QA agent works from 04 itself, so nothing it does changes. Fix: quote 04's wording or drop the paraphrase and keep only the pointer.
- Claims in the fix diff: routing.md:286-294 → instruction anchors 05-frontend.md:113-117 and 04-backend.md:136-137 (prose to prose, the weakest anchor; the F7 wording aside). 01-business-analysis.md new Rules bullet → instruction plus the BA role assigned at stage 1 (weakest anchor). ship.md:14-17 → 06-code-review.md:41-43 and routing.md table (instruction). verify.sh:212 → tested. No new direction-2 behaviour: the fix only re-points prose.
