# US-2: The stand seeds its own fixture user(s) and its own client

- story: US-2
- depends-on: -
- shippable: no — provisions a confidential client with the right to impersonate any user; merged without the story that exercises it (US-1/US-4), that right sits in `main` unused — the exact counter-example the per-story delivery rule cites
- status: pending

As **the stand**, I want the fixture user(s) and the stand-only client the
suite needs to be provisioned automatically when AppHost starts it, so that
a human is not needed before an agent can run the suite.

## Acceptance criteria

```gherkin
Scenario: Fixture user(s) and the stand-only client exist right after a fresh stand start
  Given a `keycloak` container created for the first time (no prior seed)
    by `dotnet run --project src/backend/Mustask.AppHost`
  When the stand reports ready
  Then the fixture user(s) the E2E suite signs in as already exist in
    realm `mustask`, without a human running any seed command by hand
  And the stand-only client also exists in realm `mustask`, and a token can be
    obtained for it with the secret AppHost generated and wrote into the
    one file BR-1 names

Scenario: Re-seeding on every start is harmless
  Given a stand that is being started again after being brought down
  When AppHost starts it
  Then the fixture user(s) and the stand-only client are present again, the
    same way `scripts/keycloak-seed.sh` is safe to run repeatedly today

Scenario: The fixture identity has the rights the suite needs, no password, and a usable profile
  Given the fixture user AppHost seeds
  When an admin-API call inspects that user's group membership, profile,
    and credentials
  Then it carries rights equivalent to realm `mustask`'s `/Администраторы`
    group today (including `workitems.items.write`, which spec 6 needs to
    see "Создать задачу" rendered), `E2E_USER` resolves to that same
    identity, and it has no password credential at all — nothing in the
    seeding path ever sets one (BR-4)
  And it is fully set up in the realm's own terms — `enabled`,
    `emailVerified`, an `email`, a `firstName` and a `lastName` are all
    present, with no pending required actions — because realm `mustask`'s
    declarative user profile requires all three of the latter, and a user
    seeded without them is created but cannot complete a sign-in
    ("Account is not fully set up", `scripts/keycloak-seed.sh`'s own header
    records this as a live failure on the stand); the seed itself is what
    guarantees this, not an assumption about Keycloak's defaults

Scenario: The stand file is named once and stays untracked
  Given `plan-backend.md` and `plan-frontend.md` fix and repeat verbatim
    the one file path AppHost writes the stand-only client's secret to
    (BR-1)
  When a stand starts
  Then `.gitignore` carries a pattern that covers that path — not merely
    an assumption that it is ignored
  And a check after the stand start confirms the file is untracked (e.g.
    `git status --porcelain` reports nothing for it), checked once at
    backend QA and recorded in `qa.md`

Scenario: The manual seed script and the automatic seed do not fight
  Given a human runs `scripts/keycloak-seed.sh` by hand for the
    human-facing accounts (`mustask`/`partneradmin`/`inspector`/`operator`),
    exactly as today
  When that run and AppHost's automatic seed both exist on the same stand
  Then neither interferes with the other — they touch disjoint accounts
    (BR-6, D-4) — and the script's existing requirement for both
    `MUSTASK_PASSWORD` and `PARTNER_PASSWORD` is unaffected, since AppHost's
    automatic seed never calls into that script, and a manual run of it
    cannot change or remove the fixture identity's group membership
```
