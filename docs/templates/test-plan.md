# QA — <feature name>

<!-- Template for stages 5 and 7. One qa.md file per feature: the "Backend"
     section is filled in by stage 5 QA, the "Frontend" section by stage 7
     QA, each following the structure below (matrix → scenarios → runs →
     defects → verdict).
     The test plan is written BEFORE reading the implementation code. -->

## Backend | Frontend  <!-- one section per side -->

### Coverage matrix

| Criterion (spec) | Check | Test type | Status |
|-----------------|----------|-----------|--------|
| US-1 / scenario 1 | | unit / integration / component / e2e / manual | ☐ |

### Additional scenarios (beyond the spec)

<!-- Boundaries, empty/huge lists, concurrency, resubmission,
integration failure, each role's permissions for each action. -->

| # | Scenario | Expectation | Status |
|---|----------|----------|--------|
| | | | |

### Runs

| Date | What was run | Result |
|------|-----------------|-----------|
| | | |

### Defects

#### D-1: <title>

- Severity: blocker | major | minor
- Steps: 
- Expected / Actual: 
- Status: open | fixed | won't fix (<who decided>)

### Verdict

blockers: <number> · result: pass | fail
