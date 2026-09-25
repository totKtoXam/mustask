# US-6: A member discusses a work item in comments

- story: US-6
- depends-on: US-1
- shippable: no — analysis only; delivered by `feat-002-work-item-comments`
- status: pending

As an **organization member**, I want **to discuss a work item right on it**, so that
**the reasoning behind a decision stays with the work instead of in a chat
nobody can find later**.

## Acceptance criteria

```gherkin
Scenario: A member adds a comment
  Given an existing work item
  When a member writes a comment in «Обсуждение» and posts it
  Then the comment appears with its author, its timestamp and its text
  And the item's comment count goes up by one
  And the addition is recorded in history (doc-001 BR-4)

Scenario: Comments are shown oldest first with the newest at the bottom
  Given a work item with three comments posted at different times
  When a member opens it
  Then the comments are in chronological order, and the composer sits below
    the last one

Scenario: A comment carries the same rich text as a description
  Given the comment composer
  When a member posts text with bold, a list, a link and a code block,
    together with a <script> tag
  Then the formatting survives and the script tag does not (BR-M3-4)
  And a comment longer than 10 000 characters is refused with the limit
    named (spec § NFR)

Scenario: An empty comment cannot be posted
  Given the comment composer containing only spaces or an empty list item
  When the member posts
  Then the action is refused and nothing is added

Scenario: The author edits their own comment
  Given a comment written by the signed-in member
  When they edit its text and save
  Then the text updates and the comment is marked as edited with the time of
    the edit
  And history records that the comment's text changed, by whom and when —
    the earlier text is kept nowhere (BR-M3-12, doc-001 BR-4)

Scenario: A member cannot edit someone else's comment
  Given a comment written by another member
  When a different member opens it
  Then no edit control is offered
  And an edit sent directly to the server is refused (BR-M3-12)

Scenario: An author whose role became viewer no longer edits
  Given a comment written by «Анна», whose role in the organization an
    administrator then changes from member to viewer
  When «Анна» opens the comment
  Then no edit or delete control is offered, and such a request sent
    directly is refused — the right goes with the role (BR-M3-12,
    doc-001 § Roles)

Scenario: An author deletes their own comment
  Given a comment written by the signed-in member
  When they delete it
  Then it no longer shows on the work item and the count goes down
  And the history keeps the fact that a comment existed and was deleted, by
    whom and when — the text itself is not recoverable in MVP (BR-M3-12)

Scenario: An administrator deletes any comment as moderation
  Given a comment written by any member
  When an organization administrator deletes it
  Then it disappears from the work item
  And the history records the administrator as the one who deleted it

Scenario: Comments on a deleted work item
  Given a work item in state «Удалена» that has comments
  When a member opens it
  Then the existing comments are readable
  And no composer is offered until the item is restored (doc-001 BR-14)

Scenario: A reader can read but not write
  Given a user whose role in the organization is viewer
  When they open a work item with comments
  Then they read every comment with its author and timestamp
  And no composer, edit or delete control is offered

Scenario: Mentions are not a feature yet
  Given the comment composer
  When a member types "@" or "#"
  Then nothing is suggested and the characters stay plain text — @mentions
    and work item references are release 2 (spec § Out of scope)
```
