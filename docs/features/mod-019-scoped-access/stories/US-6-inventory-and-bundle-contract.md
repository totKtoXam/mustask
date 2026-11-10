# US-6: The inventory test per scope and the role-bundle contract

- story: US-6
- depends-on: US-3
- shippable: no — it checks US-3, and nothing else uses it
- status: pending

As a **developer**, I want **a test that no route escapes the policy, a matrix generated from the domain, and the bundles as a contract file the panel reads**, so that **a new route or a changed bundle cannot silently widen or narrow access, and the panel's tests need no hand copy (BR-SA-14, tech-026 OP-7, ADR-0033 D-6)**.

## Acceptance criteria

```gherkin
Scenario: Every organization-addressed route declares one code and one scope
  When the inventory test lists the routes with `{organizationId}`
  Then each declares one code and its resource scope, or is on the list of exceptions with a reason (the instance rescue of an organization admin among them)

Scenario Outline: The inventory fails verify
  Given a route that <fault>
  When the test runs
  Then it fails and names the route

  Examples:
    | fault                                                                 |
    | has a project scope and no project id in its path                      |
    | declares a code whose scope is neither the organization nor its own    |
    | reads rows of several projects and declares no row code                |
    | is a write and declares a `*.read` code                                |

Scenario: The matrix is generated, not written, and every cell is realizable
  When the matrix test runs
  Then it takes the bundles from the domain and the routes' declarations
  And organization roles come through the membership; a role of the route's own scope is held on the addressed object
  And a route of a project, team or area is also run with an area role held on the department above (switch on, switch off) and on another department, and with a project or team role held on another object of its scope
  And the project roles are also held on the project the route's node or sub-area belongs to (the area-scope cells of BR-SA-16, switch on and off), and on another project
  And no cell holds an area role on a project, team or sub-area, or a team role on a project or area route, since such a grant cannot exist (US-1)
  And `POST /projects`, whose place is in the body, is run as an exception cell: the place's code decides at step 0 (BR-SA-17)
  And each cell expects a pass or 403 «Недостаточно прав» per ADR-0029 §§ 4 and 6 as amended by BR-SA-16, -17
  And the "not found" cells are added by `feat-041-project-visibility`, which crosses the matrix with visibility

Scenario: The evaluator's three forms agree
  When generated organizations are checked
  Then the single form, the set form and the inverse form give equal answers, and a project is readable exactly when the union holds `organizations.project.read` on it

Scenario: The cases of the brief
  Then tests exist for a conflict between grants (a wider and a narrower one), a switch that is off, and the order of grants

Scenario: The bundles are a contract
  Given the contract file under `contracts/` lists each role of each scope and the codes of its bundle
  When the contract test runs
  Then it fails when the file and the domain's bundles differ in either direction
  And a code added to the catalog and to no bundle, or a bundle naming an unregistered code, fails it

Scenario: The contract also carries the instance role-to-code table (OQ-7 A, ADR-0033)
  Given the four client roles of the company-wide realm and the table that maps them to instance codes
  When the contract test runs
  Then it compares that table with the code's, and fails on any difference
  And the realm contract test compares the four client roles; no code, organization or instance, is in the realm or the token

Scenario: The panel reads the contract in place of the hand copies
  When the panel's tests need a role's codes, or the instance role table
  Then they read the contract file, and the panel's runtime code imports it at build time; `src/frontend/src/test/organizationRoles.ts` holds no list of codes and `src/frontend/src/shared/api/tokenClaims.ts` holds no hand mirror of the table
```
