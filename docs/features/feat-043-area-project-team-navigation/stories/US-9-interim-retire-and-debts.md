# US-9: The interim pages retire; the taken-over debts close

- story: US-9
- depends-on: US-1, US-2, US-3, US-4, US-5, US-6, US-7, US-8
- shippable: no — the clean-up of the interim work of feat-028, feat-039, feat-040, mod-018 and mod-020, which is retired only once the pages of US-3..US-8 stand
- status: pending

As the **owner of the panel**, I want **every interim page, entry and fallback of the earlier lines gone, and the debts handed to this line closed**, so that **nothing built "until the navigation line" lives on, and the next lines add sections to one set of pages**.

## Acceptance criteria

Each criterion is checked by a test on the panel or by reading the named file; the MR lists what was retired (P-10).

```gherkin
Scenario: The earlier lines' interim pages are gone from the routes (BR-NV-5, BR-NV-6; ADR-0028 rule 7)
  When the route table and the route-inventory test are read
  Then no route serves the team or area pages of the organization's settings (mod-018 BR-AW-12, feat-028), the organization's project list or members page, or a project's `settings/general`, `settings/teams` or `settings/areas`
  And each of those addresses (the table of retired addresses in the spec) answers «Страница не найдена» in a test, and the retired kinds are named once in a table the test reads

Scenario: The project's interim commands live on its page (feat-028 BR-OL-10; feat-039)
  When Ann opens A's «Команды»
  Then the linked teams, the unlink and the default mark are there, and the rows no longer carry the entries «Бэклог проекта» and «Доска проекта»; A's board and backlog are its sections «Канбан» and «Бэклог»
  And a team's page no longer offers a project's views in a team drop-down: the project's views are reached from the project's page

Scenario: feat-040's interim team level of the switcher is gone (BR-NV-10; feat-040 BR-TS-10)
  When the switcher's source and tests are read
  Then there is no level that lists the organization's teams whatever project is chosen, the header holds the four selectors (US-8), and no test names the interim level

Scenario: mod-020's fallback team is retired and its filter stays (BR-NE-9; BR-MX-11)
  Given A's default team is VENUS and Olga has chosen no team
  When she opens any page
  Then the default team selects nothing in the team selector, and a new item created from A's section still starts with VENUS (BR-MX-10)
  And the team filter in the address of A's «Рабочие элементы», «Бэклог» and «Канбан» works as mod-020 built it
  And doc-012 BR-MX-11 reads "The default affects BR-MX-10 only" without the mod-020 amendment, and mod-017 BR-OT-4 and BR-OT-11, `design.md` § 6.3 and § 8 and `api-contract.md` § 2.8 say the same, each marked "feat-043"

Scenario: The switcher compares ids as the router does (tech-009 F4)
  Given a team's id is written in capitals in the address
  When Olga opens its page
  Then the team selector shows that team as chosen, and a test covers a capitalised and a percent-encoded id

Scenario: Route matching is the router's (bug-003)
  When the app shell's matching of a route is read
  Then it uses the router's own matching, and a test opens `/Dictionaries/Languages` and `/dictionaries/%6Canguages` and finds the navigation marker, the settings menu and the trail

Scenario: mod-020 review CR-2's rest: the rule is written once (CR-2; H-1 A)
  When mod-017 BR-OT-8 and `design.md` § 6.3 are read
  Then they state that a link to the page without `teamId` opens it unfiltered, the selected navigation entry «Рабочие элементы» included, as feat-003 BR-WS-11 says, and US-6 tests it

Scenario: mod-020 review CR-3's and CR-6's leftovers are fixed (CR-3, CR-6)
  When `TeamResponse.cs`, `SetDefaultTeamEndpointTests.cs` and mod-016's `design.md` § 8 are read
  Then the comments no longer say that a member's team is the team the panel opens on or that the default affects creation only in the sense mod-020 amended, and § 8 carries a pointer to BR-M1-27's note
  And no behaviour, route, shape or test assertion of the backend changes with them

Scenario: tech-011 m4 is recorded obsolete (tech-011)
  When the MR's list of closed debts is read
  Then it names tech-011 m4 as made obsolete by this line's replacement of the context model, no work was done for it, and the orchestrator edits the queue line after the merge

Scenario: The docs that name the interim pages or contradict the typed tree are brought to date (BR-NE-7; ADR-0031)
  When the documents of § Decomposition's Breaks list are read
  Then each is marked "feat-043" with the text that now holds: doc-012 BR-MX-27 and BR-MX-28, the US-10 clauses that hold area–project links («Логистика» counting 3 projects, «Проект не входит ни в одну область», «Логистика / Склад»), doc-013 BR-NE-6, ADR-0024 §§ 1, 2 and 4, ADR-0028 rule 7's sentence on the interim pages, and the fate-table lines that name a board as an entry
  And the e2e specs the address changes break are named in the MR (E2E stays paused)
```
