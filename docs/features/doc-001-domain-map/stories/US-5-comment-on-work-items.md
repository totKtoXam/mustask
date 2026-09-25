# US-5: A member comments on a work item

- story: US-5
- depends-on: US-4
- shippable: no — delivered by feat-002-work-item-comments
- status: pending

As a **user whose role in the organization is member**, I want **to discuss
a work item with the others through comments**, so that **decisions and
context stay attached to the item**.

## Acceptance criteria

```gherkin
Scenario: A member adds a comment
  Given an existing work item (US-4)
  When a member adds a comment
  Then the comment appears on the work item with its author and timestamp
  And the addition is recorded in history (BR-4)

Scenario: The author edits their own comment
  Given a comment added by a member
  When that same member edits its text
  Then the comment shows the new text, marked as edited with the time of
    the edit
  And history records that the text changed and who changed it, without
    keeping the earlier text (BR-4)

Scenario: A member cannot edit someone else's comment
  Given a comment added by another member
  When a different member attempts to edit it
  Then the action is refused

Scenario: An author downgraded to viewer can no longer edit their comment
  Given a comment added by a member whose role was later changed to viewer
  When they attempt to edit or delete that comment
  Then the action is refused — the right to one's own comments goes with
    the role (§ Roles, ownership predicate)

Scenario: An administrator deletes a comment as moderation
  Given a comment added by any member
  When an organization administrator deletes it
  Then the comment no longer shows on the work item
  And the deletion is recorded in history with who did it (BR-4)

Scenario: A reader can read comments but not add one
  Given a user whose role in the organization is viewer
  When they open a work item with comments
  Then they can read every comment
  And no control on the screen lets them add one
```
