# US-2: Every signed-in page sits in one application frame

- story: US-2
- depends-on: US-1
- shippable: no — the frame is the host of US-3 and US-5; without them it is an empty bar
- status: pending

As **any signed-in user**, I want **every page inside the same frame — top
bar, left navigation, content**, so that **I always know where I am and how
to get elsewhere, whichever page I opened**.

The pages this task builds, and the not-found frames it gives later lines
(BR-6, BR-12). The trail lists ancestors only; «Языки»
in a trail is a link, «Настройки» and «Справочники» are plain text.

| Page | Level-1 heading | Browser tab | Page trail |
|---|---|---|---|
| `/home` | «Главная» | «Главная — Mustask» | none |
| `/dictionaries/languages` | «Языки» | «Языки — Mustask» | «Настройки / Справочники» |
| `/dictionaries/languages/new` | «Добавить язык» | «Добавить язык — Mustask» | «Настройки / Справочники / Языки» |
| `/dictionaries/languages/<код>` | «Язык: <код>» | «Язык: <код> — Mustask» | «Настройки / Справочники / Языки» |
| an unknown address | «Страница не найдена» | «Страница не найдена — Mustask» | none |
| an address whose organization, project or team is not found (BR-18; built by mod-002 and later lines on this task's frame) | «Организация не найдена», «Проект не найден» or «Команда не найдена» | «<heading> — Mustask» | none |
| a page refused by its code | «Доступ ограничен» | «Доступ ограничен — Mustask» | none |
| every auth screen state (outside the shell) | «Mustask», the card's product mark | «Mustask» | none |
| the crash screen (outside the shell) | «Что-то пошло не так» | «Mustask» | none |

## Acceptance criteria

```gherkin
Scenario: The frame's regions (BR-6)
  Given a signed-in dictionaries reader on «Языки»
  When the page is rendered
  Then there is one banner region holding, left to right, the product mark
    «Mustask», the context switcher, the page trail and the user menu button
  And there is a navigation region named «Основное меню»
  And there is one main region holding the page

Scenario: The product mark leads home
  Given a signed-in dictionaries reader on «Языки»
  When they activate the link «Mustask» in the top bar
  Then «Главная» opens without the document reloading
  And «Mustask» is not a heading

Scenario: Top bar and navigation stay while the content scrolls
  Given «Языки» in a window too short for the whole page
  When the user scrolls the content to the bottom
  Then the top bar and the navigation are still fully visible

Scenario: Page trail (BR-6)
  Given a signed-in dictionaries administrator
  When they open «Языки»
  Then the navigation region «Путь к странице» reads «Настройки / Справочники»
    and neither segment is a link
  When they open the form for a new language
  Then the trail reads «Настройки / Справочники / Языки», «Языки» is a link
    to the registry, and «Добавить язык» appears only as the heading
  When they open «Главная»
  Then there is no trail

Scenario Outline: One title, and the tab says the same (BR-6)
  Given a signed-in dictionaries administrator
  When they open <page>
  Then the page has exactly one level-1 heading, <heading>
  And the browser tab reads <tab>

  Examples:
    | page                              | heading              | tab                             |
    | `/home`                           | «Главная»            | «Главная — Mustask»             |
    | `/dictionaries/languages`         | «Языки»              | «Языки — Mustask»               |
    | `/dictionaries/languages/new`     | «Добавить язык»      | «Добавить язык — Mustask»       |
    | `/dictionaries/languages/ru`      | «Язык: ru»           | «Язык: ru — Mustask»            |
    | `/no-such-page`                   | «Страница не найдена» | «Страница не найдена — Mustask» |

Scenario: User menu and sign-out
  Given a signed-in user whose display name is «Иван Петров»
  Then the top bar shows no display name as text
  And the user menu button shows «ИП» and its accessible name is
    «Профиль: Иван Петров»
  When they open it
  Then it shows «Иван Петров» and the menu item «Выйти»
  When they choose «Выйти»
  Then sign-out starts exactly as the former top-bar «Выйти» button did
  And if the auth service does not answer, the screen «Выход не завершён:
    сервис аутентификации недоступен.» appears, as today

Scenario Outline: Initials
  Given a signed-in user whose display name is <name>
  Then the user menu button shows <initials>

  Examples:
    | name                    | initials |
    | «Иван Петров»           | «ИП»     |
    | «Иван Петрович Сидоров» | «ИП»     |
    | «ivan.petrov»           | «I»      |
    | «e2e-fixture»           | «E»      |

  # Rule: the first letters of the first two space-separated words,
  # upper-cased; a single word gives its first letter.

Scenario: No display name
  Given a signed-in user whose token carries neither a name nor a username
  Then the user menu button shows a generic person icon instead of initials
  And its accessible name is «Профиль»
```
