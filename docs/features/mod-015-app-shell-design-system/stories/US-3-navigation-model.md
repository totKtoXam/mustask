# US-3: Navigation shows the hubs and settings the user may open

- story: US-3
- depends-on: US-2
- shippable: no — its only built entries are «Главная» and «Языки», whose placement is decided with the rest of the gate-2 design (D-1)
- status: pending

As **a signed-in user**, I want **the navigation to list, in grouped hubs
with settings at the bottom, exactly the pages I may open**, so that **I
never click into a page that turns me away or hunt for one that is
hidden**.

Settings navigation rule: BR-19. «Языки» sits under «Настройки» →
«Справочники» at its unchanged address (D-14).

## Acceptance criteria

```gherkin
Scenario: The main navigation outside settings (§ Navigation model)
  Given a signed-in dictionaries reader on «Главная»
  Then the navigation «Основное меню» lists «Главная»
  And «Настройки» is at its bottom, above the collapse toggle
  And no settings navigation is shown and no entry «Языки» is visible

Scenario: «Настройки» opens the first settings page the user may open (BR-19)
  Given a signed-in dictionaries reader on «Главная» in a 1440 px window
  When they activate «Настройки»
  Then `/dictionaries/languages` opens
  And a second navigation region «Меню настроек», titled «Настройки», shows
    the section «Справочники» with the entry «Языки»
  And «Основное меню» stands beside it as a rail of icons, with «Настройки»
    marked current and no collapse toggle

Scenario: A settings address opened directly shows the same navigation
  Given a signed-in dictionaries reader
  When they open `/dictionaries/languages` from a bookmark
  Then «Меню настроек» is shown with «Языки» marked current
  And «Основное меню» is a rail with «Настройки» marked current

Scenario: Leaving settings
  Given a dictionaries reader on «Языки» with the settings navigation shown
  When they activate «Главная» in the rail
  Then «Главная» opens, «Меню настроек» is gone
  And «Основное меню» is back to the kept choice or the width default (BR-10)

Scenario: A user with no settings page sees no settings entry (BR-7)
  Given a signed-in user holding no `dictionaries.*` code
  Then «Основное меню» lists «Главная» and no «Настройки»
  When they open `/dictionaries/languages` by address
  Then «Доступ ограничен» is shown inside the shell
  And no settings navigation is shown

Scenario: Pages that do not exist yet never appear (BR-7)
  Given a build holding only this task's pages, and any signed-in user,
    whatever codes they hold
  Then no navigation shows «Рабочие элементы», «Доски», «Бэклоги»,
    «Спринты», «Запросы», «Организация», «Проект» or «Процесс»
  And no entry is shown disabled or marked as coming later

Scenario: An organization-scoped entry waits for a real context (BR-8, D-18)
  Given, in a component test, a navigation model holding a test entry gated
    by an organization-scoped code, and no source of served codes
  And the switcher reading the placeholder dataset
  Then the entry is not shown, whatever codes the user holds
  And a page gated by that code, opened by its address, is not refused with
    «Доступ ограничен»
  When the context source reports a real organization, as mod-002's will
  Then the entry is shown to any signed-in user, without a page reload

Scenario: The current page is marked
  Given a signed-in dictionaries administrator
  When they open `/dictionaries/languages`
  Then «Языки» in «Меню настроек» carries `aria-current="page"`
  When they open `/dictionaries/languages/new`
  Then «Языки» still shows the selected marker and carries
    `aria-current="true"`
  And no other entry of «Меню настроек» carries `aria-current`
  And in the rail only «Настройки» carries `aria-current="true"`

Scenario: Visibility follows the permission check, not a copy of it (BR-8, BR-19)
  Given a dictionaries reader on «Языки» with the settings navigation shown
  When the session renews and the new token no longer carries
    `dictionaries.languages.read`
  Then, without a page reload, «Настройки» and «Меню настроек» disappear
  And the page answers «Доступ ограничен»
```
