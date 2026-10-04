# US-1: Consumers search and resolve users through the read model

- story: US-1
- depends-on: -
- shippable: no — the read side of the feed of US-2..US-6; alone it answers from an empty store
- status: pending

As a **consumer module (mod-016, mod-003)**, I want **to search users by part
of a name or e-mail and to resolve known ids to a name and a status, from
MUSTASK's own projection**, so that **member search, the assignee picker and
the «неактивен» marking work without calling Keycloak and without reopening
this task (D-2 (3), D-3)**.

The projection in these scenarios is filled through the feed (US-2..US-4) or,
in a test, by pushes the provider would send. The read model is BR-UP-11
and BR-UP-14..17 of `spec.md`.

## Acceptance criteria

```gherkin
Scenario: A search by part of a name finds an active user
  Given the projection holds an active user «Анна Смирнова» with username
    "a.smirnova" and e-mail anna.s@example.test
  When a consumer searches for "смир"
  Then the answer holds that user with exactly four values: the Keycloak id,
    the display name «Анна Смирнова», the e-mail and "active" (BR-UP-11)
  And nothing else about the user — no username, role, group or
    membership (BR-UP-2)

Scenario: A search by part of an e-mail ignores letter case
  Given the user from the previous scenario
  When a consumer searches for "ANNA.S@"
  Then the answer holds that user

Scenario: The username is not searched
  Given the user from the first scenario
  When a consumer searches for "a.smirn"
  Then nobody is answered (BR-UP-14; D-8)

Scenario: ё and е count as the same letter
  Given an active user «Семён Орлов»
  When a consumer searches for "семен"
  Then the answer holds «Семён Орлов»
  And a search for "Семён" finds a user «Семен Лебедев» as well

Scenario: A text shorter than 2 characters returns nobody
  Given a projection of 50 active users
  When a consumer searches for " а " (one character after trimming)
  Then the answer holds no user and says that nothing more matched
  And the whole directory is never returned for an empty or short text

Scenario: A text longer than 100 characters is refused
  Given a projection of 50 active users
  When a consumer searches for a text of 101 characters after trimming
  Then the search is refused as invalid and nothing is answered
  And a text of exactly 100 characters is accepted

Scenario: An inactive user is not a candidate unless asked for
  Given an active user «Иван Петров» and a disabled user «Иван Сидоров»
  When a consumer searches for "иван" with the defaults
  Then the answer holds «Иван Петров» only (BR-M1-17)
  When the consumer searches for "иван" with "include inactive"
  Then the answer holds both, «Иван Сидоров» marked inactive

Scenario: A broad search is capped at 20 and says more matched
  Given 35 active users whose e-mail ends with "@example.test"
  When a consumer searches for "example"
  Then the answer holds the first 20 of them in the order of BR-UP-15
  And it says that more users matched

Scenario: The order puts Cyrillic before Latin and ignores case and ё
  Given active users, all with e-mails ending "@order.test": «Zoe Adams»,
    «Alex Brown», «Юлия Щукина», «Ёлкин Пётр», «Егор Лось», «анна Абрамова»
  When a consumer searches for "order.test"
  Then the answer lists, in this order: «анна Абрамова», «Егор Лось»,
    «Ёлкин Пётр», «Юлия Щукина», «Alex Brown», «Zoe Adams»

Scenario: Equal display names are ordered by id, the same way every time
  Given 25 active users all named «Анна Смирнова», each with its own id
  When a consumer searches for "смирнова" twice
  Then both answers hold the same 20 users: those whose ids come first when
    compared character code by character code, in that order
  And both say that more users matched

Scenario: A search can be restricted to a given set of ids
  Given active users «Анна Смирнова», «Анна Кузнецова» and «Анна Волкова»
  When a consumer searches for "анна" only among the ids of the first two
  Then the answer holds «Анна Кузнецова» and «Анна Смирнова» only
  When the id list repeats the first id and adds an id never held
  Then the answer is the same

Scenario: An empty id restriction answers nobody
  Given the users of the previous scenario
  When a consumer searches for "анна" only among an empty list of ids
  Then nobody is answered

Scenario: A search restricted to more than 500 ids is refused
  Given 501 ids
  When a consumer searches for "анна" only among them
  Then the search is refused as too large, and nothing is answered
  And the same search among 500 of those ids is answered

Scenario: A user with no first or last name is shown by username
  Given an active user with username "qa.fixture", no first name, no last
    name and no e-mail
  When a consumer searches for "qa.fix"
  Then the answer holds that user with the display name "qa.fixture" and an
    empty e-mail (BR-UP-3)

Scenario: A lookup resolves known ids and reports unknown ones
  Given an active user A, a disabled user B, a deleted user C, and an id D
    the projection has never held
  When a consumer looks up A, B, C and D at once
  Then A is answered active, B and C inactive with their last received
    display names and e-mails, and D "unknown" — neither active nor inactive
  And the lookup does not fail because of D

Scenario: A lookup answers a repeated id once and an empty list with nothing
  Given the users of the previous scenario
  When a consumer looks up A, A and B
  Then the answer holds exactly two entries, A and B
  When a consumer looks up an empty list
  Then the answer is empty and not an error

Scenario: A lookup of more than 500 ids is refused
  Given 501 ids
  When a consumer looks them up at once
  Then the lookup is refused as too large, and nothing is answered partially
  And a lookup of 500 of them is answered

Scenario: Reads work while Keycloak is unreachable
  Given the projection holds «Анна Смирнова»
  And the Keycloak container is stopped
  When a consumer searches for "смир" and looks up her id
  Then both answer from the projection as before
  And no call to Keycloak is made (doc-006 NFR Member search)

Scenario: Reads answer in time at the target volume
  Given a projection of 10 000 users
  When a consumer searches for a 3-character text, and looks up 500 ids
  Then each answer arrives within 500 ms on the stand
```
