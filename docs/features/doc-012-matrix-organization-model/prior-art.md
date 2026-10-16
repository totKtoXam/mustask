# Prior art — pitfalls only

On 2026-10-15/16 an external AI agent attempted this refactoring in one pass,
outside the pipeline. The human rejected it and chose to start from scratch
(status.md). Nothing of it is reused — not its model, not its code. What it
leaves is a list of the places where a matrix refactoring of this codebase
breaks; stage 1 states a rule for each, and stage 2 checks that it did.

| # | What broke | What the spec must settle |
|---|-----------|---------------------------|
| P-1 | mod-002's rules were deleted silently: the first team (BR-M1-9, BR-M1-18), the immutable project root area (BR-M1-11), the team's default area (BR-M1-14), area ownership (US-6) | every existing BR-M1 rule is kept, replaced or retired by name, with the reason |
| P-2 | A team's backlog lost its filter: it showed every area of the project for any team (mod-006) | what a team's backlog and board hold once teams own no project areas |
| P-3 | Repository reads and locks lost the organization filter; one lock took the root areas of every organization | the tenant boundary as a rule every read, write and lock obeys, not only the links |
| P-4 | Restricted projects were filtered only in three new boards; lists, single items, search, parent candidates and the old boards showed them | visibility as one rule applied to every read, counter and search, named per surface |
| P-5 | A second authorization model was built beside mod-016 with hard-coded roles | the scoped model as an extension of ADR-0027's membership-derived codes, not a parallel one |
| P-6 | Link, unlink, project settings and membership operations existed in code but no route reached them; the UI called routes that did not exist | operation completeness: every operation has a role and a surface, or an explicit "no" |
| P-7 | Unlinking a team from a project left items owned by it and their sprints dangling | SC-09 decided: what happens to owned items, their sprints and the default team |
| P-8 | Sprint state transitions and the sprint–owning-team match were not enforced anywhere | the sprint's lifecycle and the item–sprint–team invariant as rules |
| P-9 | Shared roots: one root area renamed through any project linked to it | which area operations a project may perform once areas are shared |
| P-10 | Existing contracts changed with no note in the owning feature's status.md | the decomposition names, per delivery line, which existing contracts it breaks |
| P-11 | The test count was reported from unit suites only; 1944 of 2141 integration tests failed | out of the analysis's scope, recorded for the delivery lines: green means `scripts/verify.sh` |
