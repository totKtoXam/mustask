# US-9: A member reads a work item's history

- story: US-9
- depends-on: US-1, US-2
- shippable: no — analysis only; delivered by `mod-003-work-item-core`
- status: pending

As an **organization member**, I want **to see who changed what on a work item and
when**, so that **I can tell why it looks the way it does without asking
around**.

## Acceptance criteria

```gherkin
Scenario: Creation is the first revision
  Given a work item just created (US-1)
  When a member opens «История»
  Then there is exactly one revision, naming the author, the timestamp and
    the fact that the work item was created (doc-001 BR-4)

Scenario: One save is one revision
  Given a member changes Описание, Приоритет and Исполнитель in a
    single save (US-2)
  When the history is opened
  Then exactly one new revision appears
  And it lists all three fields with their previous and new values
    (screenshot 06)

Scenario: History covers more than fields
  Given a work item that has had a state change, a parent link added, a tag
    added, a comment posted, edited and then deleted, an attachment uploaded
    and removed, and has been deleted and restored
  When a member opens «История»
  Then every one of those acts is present as its own entry, with who and
    when (doc-001 BR-4)
  And nothing in the list can be edited or removed

Scenario: A board move is a state revision by the mover
  Given a member drags the item's card to another column of its board
    (doc-003 US-1)
  When a member opens «История»
  Then a revision records the state change, authored by that member
    (doc-001 BR-4)

Scenario: The newest change is found first
  Given a work item with many revisions
  When a member opens «История»
  Then the newest revision is shown first
  And older ones are reachable by paging or scrolling, without loading the
    whole history at once (spec § NFR)

Scenario: A long history does not slow the form down
  Given a work item with 500 revisions
  When a member opens it
  Then the form opens within the same 1 s budget as any other work item
    (spec § NFR)

Scenario: Deleted and edited content stays attributable
  Given a comment that was posted, edited and later deleted (US-6)
  When a member reads the history
  Then it says that a comment was added, that its text changed and that it
    was deleted, each with who and when
  And no text of the comment — neither the earlier nor the last — is shown
    there; MVP does not keep it (BR-M3-12)

Scenario: History is not writable by anyone
  Given any user, including an organization administrator
  When they look for a way to edit or delete a history entry
  Then none exists in the interface
  And such a request sent directly to the server is refused (doc-001 BR-4)

Scenario: History disappears only with a purge
  Given a work item in state «Удалена» with a long history
  When it is restored
  Then the whole history is intact, including the delete and the restore
  And only an administrator's purge removes the item together with its
    history (US-10, spec § NFR)

Scenario: A reader can read the history
  Given a user whose role in the organization is viewer
  When they open «История» on a work item they can read
  Then they see every revision with its author and timestamp
```
