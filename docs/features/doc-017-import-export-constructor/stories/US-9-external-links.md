# US-9: A work item carries external links

- story: US-9
- depends-on: -
- shippable: no — an analysis story; delivered by `feat-066-work-item-external-links`
- status: pending

As a **member**, I want **to keep an item's outside links on it**, so that **I open its documents, and the place it came from, from the card**.

## Acceptance criteria

```gherkin
Scenario: A member adds an external link
  Given an item the member may change
  When they add «https://docs.example.com/d/1» titled «Техническое задание»
  Then the card lists «Техническое задание», which opens the address in a new tab (BR-IE-24)

Scenario: A link without a title shows its address
  Given a link added with no title
  When the card is shown
  Then the link reads as its address (BR-IE-24)

Scenario: Only http and https addresses are accepted
  Given the addresses «ftp://files.example.com/a», «javascript:alert(1)» and «docs.example.com»
  When a member adds each
  Then each is refused with «Укажите адрес, начинающийся с http:// или https://.» (BR-IE-24)

Scenario: The same address is not added twice
  Given an item already linked to «https://docs.example.com/d/1»
  When the same address is added again by hand or by an import
  Then the item still has one such link (BR-IE-11, BR-IE-24)

Scenario: Limits on links
  Given an item with 50 links
  When a 51st is added
  Then it is refused
  And an address over 2 000 characters or a title over 200 is refused (BR-IE-24)

Scenario: A member removes a link
  Given an item with two links
  When a member who may change the item removes one
  Then only the other stays (BR-IE-24)

Scenario: A reader sees links and cannot change them
  Given an organization «Читатель»
  When they open the card
  Then they see and open the links, and are offered no add or remove (§ Roles)

Scenario: A deleted item takes no new link
  Given an item in «Удалена»
  When anyone adds a link to it
  Then it is refused, as any write to a deleted item is (mod-005)

Scenario: Links leave with the item's purge
  Given an item with two links in «Удалена»
  When it is purged
  Then its links are gone with it (BR-IE-24, mod-005 BR-DR-16)

Scenario: An import adds a link from a hyperlink cell, titled by the cell's text
  Given «Ссылка» with «URL из гиперссылки» mapped to «Внешние ссылки»
  When a row's cell shows «Документ» and links to «https://docs.example.com/d/1»
  Then the item gets the link «https://docs.example.com/d/1» titled «Документ»
  And a cell whose shown text is the address itself gives a link with no title (BR-IE-24)

Scenario: An import adds links and removes none
  Given an item with the link «https://docs.example.com/d/1» added by a member
  When an import's row naming the item holds «https://docs.example.com/d/2»
  Then the item has both links (BR-IE-11)
```
