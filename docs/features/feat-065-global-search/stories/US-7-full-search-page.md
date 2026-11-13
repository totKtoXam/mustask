# US-7: A full search page finds work items by their description too, with snippets

- story: US-7
- depends-on: US-3
- shippable: no — reached only from the palette's last row; delivered by `feat-NNN-search-full-page` after `feat-NNN-search-description-index`
- status: pending

As a **member of an organization**, I want **a page that searches work items by title, identifier and description text, in Russian or English word forms, and shows where each match is**, so that **I find an item I only remember by a phrase from its description**.

Quick search (the palette, US-2/US-3) stays identifier and title, as typed. The page is the second mode (BR-GS-18). It lists work items only: structure objects and people have no description to search, and the palette already finds them by every field they have. Description = plain text derived from the sanitized HTML (ADR-0039). Cast: Max sees ALP-US-7 «Оплата картой» (description «Через Stripe, повторная попытка списания»), BRV-BUG-3 «Падение при входе» (description «Оплата картой не проходит на шаге 3»); Olga sees only project ALP.

## Acceptance criteria

```gherkin
Scenario: Reached from the palette
  Given Max typed «оплата» in the palette
  When he chooses the last row «Искать везде, включая описания»
  Then the palette closes and the page «Поиск» opens in the organization with «оплата» in its field and the results shown
  And Back returns to the page he came from

Scenario: Description match with a snippet
  When the page searches «повторная попытка»
  Then ALP-US-7 is listed with a snippet from its description around the match, the matching words highlighted
  And the snippet is plain text: no markup of the description is rendered or executed

Scenario: Word forms
  When Max searches «списание»
  Then ALP-US-7 is found by «списания» in its description (Russian stemming)
  And an English description with «payments» is found by «payment»

Scenario: Title and identifier too
  When Max searches «ALP-7» or «alp-us-7»
  Then ALP-US-7 is listed first, as the palette ranks it (BR-GS-7)
  And a title match ranks above a description-only match, which is ranked by relevance

Scenario: Several words
  When Max searches «оплата картой шаг»
  Then items whose title and description together carry all the words are listed; those missing a word are not

Scenario: Sight
  When Olga searches «оплата»
  Then BRV-BUG-3 (description «Оплата картой не проходит…») is not listed, nor counted, nor named in a snippet
  And the body is the same as for a phrase that exists nowhere when only hidden items match

Scenario: Removed and inactive
  Given a Removed item and an item of a deactivated project contain «оплата» in the description
  Then neither is listed

Scenario: Fresh description
  Given Max saves ALP-US-7 with the description «Нужен возврат средств»
  When he searches «возврат» on the next request
  Then ALP-US-7 is found, and the old text is no longer found

Scenario: Paging and count
  Given 45 visible items match
  Then the page shows 20 items, the count of visible matches «Найдено: 45» and a pager; the second page shows the next 20 in the same order
  And a page past the last answers no rows and the right count

Scenario: Empty and errors
  When nothing visible matches
  Then the page says «Ничего не найдено»
  When the read fails or answers 429
  Then it says «Не удалось выполнить поиск. Попробуйте ещё раз.» with «Повторить»

Scenario: The page's own field
  When Max changes the text and presses Enter
  Then the results are replaced, the address carries the new text and page 1
  And a text under 2 characters says «Введите не менее 2 символов»; at most 100 characters are accepted

Scenario: Opening a result
  When Max chooses a row
  Then the card opens as in US-5, with the context chosen
  And Ctrl/Cmd+click opens it in a new tab

Scenario: Link to the page
  Given Max sends the address of the page to Olga
  When she opens it
  Then she sees the results within her own sight, and a non-member sees «Организация не найдена.»

Scenario: Speed
  Given an organization with 100 000 work items, descriptions averaging 1 500 characters, and a user who sees all of them
  When a 2-word query is sent
  Then the page's read answers within 1 s at p95, snippets included
```
