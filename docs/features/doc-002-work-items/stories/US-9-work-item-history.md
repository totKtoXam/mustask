# US-9: A team member reads a work item's history

- story: US-9
- depends-on: US-1, US-2
- shippable: no — analysis only; delivered by `mod-003-work-item-core`
- status: pending

As a **team member**, I want **to see who changed what on a work item and
when**, so that **I can tell why it looks the way it does without asking
around**.

## Acceptance criteria

```gherkin
Scenario: Creation is the first revision
  Given a work item just created (US-1)
  When a team member opens «История»
  Then there is exactly one revision, naming the author, the timestamp and
    the fact that the work item was created (BR-10)

Scenario: One save is one revision
  Given a team member changes Описание, Приоритет and Исполнитель in a
    single save (US-2)
  When the history is opened
  Then exactly one new revision appears
  And it lists all three fields with their previous and new values
    (screenshot 06)

Scenario: History covers more than fields
  Given a work item that has had a state change, a parent link added, a tag
    added, a comment posted and then deleted, an attachment uploaded and
    removed, and has been deleted and restored
  When a team member opens «История»
  Then every one of those acts is present as its own entry, with who and
    when (BR-10, doc-001 BR-4)
  And nothing in the list can be edited or removed

Scenario: The newest change is found first
  Given a work item with many revisions
  When a team member opens «История»
  Then the newest revision is shown first
  And older ones are reachable by paging or scrolling, without loading the
    whole history at once (spec § NFR)

Scenario: A long history does not slow the form down
  Given a work item with 500 revisions
  When a team member opens it
  Then the form opens within the same 1 s budget as any other work item
    (spec § NFR)

Scenario: Deleted content stays attributable
  Given a comment that was posted and later deleted (US-6)
  When a team member reads the history
  Then it says that a comment was added, by whom and when, and that it was
    deleted, by whom and when
  And the comment's own text is not shown there — MVP does not keep it
    (BR-12)

Scenario: History is not writable by anyone
  Given any user, including an organization administrator
  When they look for a way to edit or delete a history entry
  Then none exists in the interface
  And such a request sent directly to the server is refused (BR-10)

Scenario: History disappears only with a purge
  Given a work item in state «Удалена» with a long history
  When it is restored
  Then the whole history is intact, including the delete and the restore
  And only an administrator's purge removes the item together with its
    history (US-10, spec § NFR)

Scenario: A reader can read the history
  Given a user with only the "viewer" role
  When they open «История» on a work item they can read
  Then they see every revision with its author and timestamp
```
