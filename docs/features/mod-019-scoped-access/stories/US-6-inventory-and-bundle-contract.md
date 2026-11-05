# US-6: The inventory test per scope and the role-bundle contract

- story: US-6
- depends-on: US-3
- shippable: no — it checks US-3, and nothing else uses it
- status: pending

As a **developer**, I want **a test that no route escapes the policy, a matrix generated from the domain, and the bundles as a contract file the panel reads**, so that **a new route or a changed bundle cannot silently widen or narrow access, and the panel's tests need no hand copy (BR-SA-14, tech-026 OP-7)**.

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

Scenario: The matrix is generated, not written
  When the matrix test runs
  Then it takes the bundles from the domain and the routes' declarations, and for each route takes each of the 11 roles held on the addressed object
  And for a project route it also holds the role on a department above, with the switch on, with it off, and on the department of another project
  And each cell expects a pass or 403 «Недостаточно прав» per ADR-0029 §§ 4 and 6
  And the "not found" cells are added by `feat-041-project-visibility`, which crosses the matrix with visibility

Scenario: The evaluator's three forms agree
  When generated organizations are checked
  Then the single form, the set form and the inverse form give equal answers, and a project is readable exactly when the union holds `organizations.project.read` on it

Scenario: Cases of the brief
  Then tests exist for a conflict between grants (a wider and a narrower one), the limit of a switch that is off, and the order of grants

Scenario: The bundles are a contract
  Given the contract file under `contracts/` lists each role of each scope and the codes of its bundle
  When the contract test runs
  Then it fails when the file and the domain's bundles differ in either direction, and the realm contract test still compares instance codes only

Scenario: The panel reads the contract in place of the hand copy
  When the panel's tests need a role's codes
  Then they read the contract file, and `src/frontend/src/test/organizationRoles.ts` no longer holds a list of codes

Scenario: A new code without a bundle fails
  When a code is added to the catalog and to no bundle, or a bundle names an unregistered code
  Then the contract test fails
```
