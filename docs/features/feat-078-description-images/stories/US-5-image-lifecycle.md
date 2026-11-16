# US-5: Unbound uploads are swept; an item's images live and die with it

- story: US-5
- depends-on: US-3
- shippable: no — the sweep and the purge step clean up what US-2 and US-3 create
- status: pending

As **the organization that keeps its work items in MUSTASK**, we want
**pictures nobody saved removed soon, and an item's pictures kept exactly as
long as the item**, so that **the database holds no abandoned images, and a
description never loses a picture while its item exists**.

Rules: spec BR-IM-6, -16, -19; amends mod-005 BR-DR-12, -16 (the purge).

## Acceptance criteria

```gherkin
Scenario: An upload nobody saves is removed after 2 hours
  Given a member uploaded image U for item X at 10:05 and never saved it
  When the hourly sweep runs at 11:00
  Then U is kept (less than 2 hours old)
  And when it runs at 12:00 U is removed
  And the next sweep, at 13:00 at the latest, has removed it in any case

Scenario: An upload removed from the editor before the save
  Given a member inserted U, removed it from the text and saved the card
  Then U stays unbound and is removed by the first sweep 2 hours after its upload

Scenario: A save binds before the sweep
  Given U was uploaded at 10:05 and X's Описание referencing it was saved at 11:50
  When the sweep runs at 12:00
  Then U is kept: it is X's image now

Scenario: A save after the sweep is refused
  Given U was swept while the card stayed open
  When the member saves Описание still referencing U
  Then the save is refused as US-3 says and the member pastes the image again

Scenario: Removed from the text, kept with the item
  Given image M is bound to X
  When a later save of X's Описание no longer references M
  Then M is kept and stays readable by X's readers (US-4)
  And no sweep removes it

Scenario: Deletion keeps the images, restore shows them
  Given X with images is deleted to «Удалена»
  Then its images are kept
  And after X is restored its Описание shows them again

Scenario: A purge takes the images with the item
  Given X in «Удалена» with three bound images, one of them no longer referenced
  When X is purged (mod-005)
  Then all three are removed in the same purge
  And a read of any of them answers not found
  And if the purge fails, the item and its images both stay

Scenario: The sweep removes nothing it should not
  Given a bound image uploaded three days ago and an unbound upload made 10 minutes ago
  When the sweep runs
  Then both are kept

Scenario: The sweep applies to every purpose
  Given an unbound upload made through another purpose of the model (feat-059's feedback submission, once it exists)
  Then the same 2-hour sweep removes it if nothing bound it
```

The sweep writes one log line per run with the number of uploads it removed,
nothing about their owners or items.

## Verification

| What | How |
|---|---|
| The 2-hour window and the hourly cadence | integration tests with a controlled clock |
| Binding beats the sweep; a save after the sweep | integration (API) tests |
| Kept on removal, deletion and restore; removed by the purge, atomically | integration tests over mod-005's delete, restore and purge |
