# US-9: A work item carries an external key and external links

- story: US-9
- depends-on: -
- shippable: no — an analysis story; delivered by `feat-NNN-work-item-external-key-and-links`
- status: pending

As a **member**, I want **to see where an item came from and to keep its outside links on it**, so that **I find a task by the sheet's id and open its documents from the card**.

## Acceptance criteria

```gherkin
Scenario: The card shows the key read-only
  Given an item imported with the key «010125 - 01» from the source «DevEx»
  When a member opens its card
  Then it shows «DevEx: 010125 - 01», and no control changes it (BR-IE-24)

Scenario: An item created by hand has no key
  Given an item created in the panel
  When its card is opened
  Then no key is shown (BR-IE-24)

Scenario: The list's keyword finds an item by its key
  Given the item «DevEx: 010125 - 01»
  When a member types «010125 - 01» in the «Рабочие элементы» list's keyword
  Then the item is in the result (BR-IE-24)

Scenario: A key is unique per source in the organization
  Given an item carrying «DevEx: 010125 - 01» in the project «Платформа»
  When a later import's «DevEx» row keyed «010125 - 01» targets «Платформа»
  Then it updates that item, and no second item gets the key
  When such a row targets «Финансы»
  Then it is an error, since an item changes no project, and no item gets the key (BR-IE-10, BR-IE-11, OQ-2)

Scenario: A purge frees the key
  Given the item carrying «DevEx: 010125 - 01» is purged from the recycle bin
  When a row keyed «010125 - 01» is checked
  Then it is «Будет создан» (BR-IE-11)

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
  Then the item still has one such link (BR-IE-24)

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
```
