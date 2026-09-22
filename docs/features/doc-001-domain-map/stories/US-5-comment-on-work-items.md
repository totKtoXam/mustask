# US-5: A team member comments on a work item

- story: US-5
- depends-on: US-4
- shippable: no — delivered by feat-work-item-comments
- status: pending

As a **team member**, I want **to discuss a work item with my team through
comments**, so that **decisions and context stay attached to the item**.

## Acceptance criteria

```gherkin
Scenario: A team member adds a comment
  Given an existing work item (US-4)
  When a team member adds a comment
  Then the comment appears on the work item with its author and timestamp
  And the addition is recorded in history (BR-4)

Scenario: The author edits their own comment
  Given a comment added by a team member
  When that same member edits its text
  Then the comment's text updates
  And the edit is recorded in history (BR-4); the comment's own text is
    never rewritten silently — the edit shows in history, not in the
    comment itself (Operation completeness: Comment)

Scenario: A member cannot edit someone else's comment
  Given a comment added by another team member
  When a different member attempts to edit it
  Then the action is refused

Scenario: An administrator deletes a comment as moderation
  Given a comment added by any team member
  When an organization administrator deletes it
  Then the comment no longer shows on the work item
  And the deletion is recorded in history with who did it (BR-4)

Scenario: A reader can read comments but not add one
  Given a user signed in with only the "viewer" role
  When they open a work item with comments
  Then they can read every comment
  And no control on the screen lets them add one
```
