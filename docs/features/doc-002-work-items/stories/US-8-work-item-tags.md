# US-8: A member tags a work item

- story: US-8
- depends-on: US-1
- shippable: no — analysis only; delivered by `mod-003-work-item-core`
- status: pending

As an **organization member**, I want **to put free-form tags on a work item and reuse
the ones my project already has**, so that **we can group work across types
and levels without anyone defining a taxonomy first**.

## Acceptance criteria

Теги are part of the form's field set: a chip added or removed is committed
by the form's Save, together with the other fields and under the same
revision check (doc-001 BR-11). A tag is a stored project entity whose
spelling is the first one typed (spec § Logical data model).

```gherkin
Scenario: A member creates a tag by typing it
  Given a work item in a project with no tags yet
  When the member types «Регрессия» in Теги and confirms it
  Then the tag is attached to the work item as a chip
  And it becomes available to every other work item of that project
  And the change is recorded in history (doc-001 BR-4, BR-M3-11)

Scenario: Existing project tags are suggested
  Given a project where «Регрессия» is already used on some work item
  When the member types «Рег» in Теги on another work item
  Then «Регрессия» is suggested
  And picking the suggestion reuses the same tag rather than creating a
    second one

Scenario: Matching ignores case and surrounding spaces
  Given a project where «Аудит» already exists
  When the member types « аудит » on another work item
  Then it is treated as the same tag, in its original spelling «Аудит»
  And the work item does not end up with two look-alike tags (BR-M3-11)

Scenario: An unused tag keeps its spelling
  Given «Аудит» was removed from every work item of the project
  When a member later types «аудит» on a work item
  Then the tag attached is «Аудит», in the spelling first typed
    (spec § Logical data model)

Scenario: Tags never leak between projects
  Given «Регрессия» used only in project «Проект Б»
  When a member types «Рег» on a work item of «Проект А»
  Then nothing is suggested, and confirming creates «Регрессия» as a new tag
    of «Проект А» — a separate tag that happens to share a name
    (doc-001 BR-8)

Scenario: Limits on tags
  Given a work item with 20 tags
  When the member adds one more
  Then the action is refused, with the limit of 20 named
  And a tag of 0 characters after trimming, or longer than 50, is refused
    with the reason shown (spec § NFR)

Scenario: A tag is removed from a work item
  Given a work item with the tag «Регрессия»
  When the member removes the chip
  Then the tag is gone from the item and the change is in history
  And the tag stays available to the project while any other item uses it

Scenario: A tag nobody uses stops being offered
  Given «Регрессия» was used on exactly one work item of the project
  When that tag is removed from it, the item is deleted into «Удалена», or
    the item is purged (US-10)
  Then the tag is no longer suggested in that project — a tag carried only
    by deleted items is not offered (BR-M3-11, doc-001 BR-14)
  And restoring the deleted item brings the tag back into the suggestions
  And no separate screen exists for renaming or deleting tags by hand — that
    is release 2

Scenario: A reader sees tags but cannot change them
  Given a user whose role in the organization is viewer
  When they open a tagged work item
  Then the tags are shown as plain chips
  And no add control and no remove cross is offered
```
