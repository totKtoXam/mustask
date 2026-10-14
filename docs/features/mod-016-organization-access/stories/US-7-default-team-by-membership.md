# US-7: A project's views open on the user's own team

- story: US-7
- depends-on: US-3
- shippable: no — it reads the team memberships of US-3
- status: pending

As a **member of a project's team**, I want **the switcher, «Бэклоги» and
«Канбан» to open on my team when I choose the project**, so that **I land on
my own work, not on the first team's (D-5, mod-015 BR-11, mod-002
BR-M1-24)**.

## Acceptance criteria

The project's team order stays mod-002 BR-M1-24's: the first team first, the
rest alphabetically. feat-028 carries the rule to organization-level teams.

```gherkin
Scenario: Choosing a project selects the user's own team
  Given the project «Пример проекта» with the teams «Пример проекта» (first),
    «Аналитика» and «Платформа», and a user who is a member of «Платформа»
    only
  When they choose «Пример проекта» in the switcher
  Then the team level shows «Платформа»
  And «Бэклоги» opens «Платформа»'s «Истории» backlog

Scenario: Several teams — the first of them in the project's order
  Given the same user, now a member of «Аналитика» and «Платформа»
  When they choose «Пример проекта» in the switcher
  Then the team level shows «Аналитика»

Scenario: In no team of the project — the project's first team
  Given a member of the organization who is in none of the project's teams
  When they choose «Пример проекта» in the switcher
  Then the team level shows «Пример проекта», as before this task

Scenario: Choosing the organization selects the user's team too
  Given the user of the first scenario, «Пример проекта» being the first
    project of «Пример организации»
  When they choose «Пример организации» in the switcher
  Then the project level shows «Пример проекта» and the team level
    «Платформа»

Scenario: A first load with nothing remembered selects the user's team
  Given the user of the first scenario, signing in with no remembered
    selection, «Пример проекта» being the first project of the first
    organization
  When the panel loads
  Then the team level shows «Платформа»

Scenario: A remembered team that is gone falls back to the user's team
  Given a user in «Аналитика» and «Платформа» whose remembered selection is
    «Пример проекта», «Платформа»
  When «Платформа» has since been deleted and the user reloads
  Then the team level shows «Аналитика», not the project's first team
  And the fallback is shown as mod-002 US-8 shows a remembered selection
    that is no longer available

Scenario: A remembered team that still exists is kept
  Given a user in «Платформа» only whose remembered selection is
    «Пример проекта», «Аналитика»
  When they reload
  Then the team level still shows «Аналитика» (mod-002 BR-M1-24)

Scenario: A team in the address wins
  Given the user of the first scenario
  When they open a link to the «Аналитика» backlog of «Пример проекта»
  Then the switcher shows «Аналитика» and the page is its backlog
    (mod-015 BR-18)

Scenario: Leaving the team changes the default the next time the list is read
  Given the user of the first scenario, on the project «Отчёты», whose
    «Платформа» membership an administrator removes
  When they choose «Пример проекта» in the switcher
  Then the team level shows «Пример проекта», the project's first team
    (BR-M1-27)
```
