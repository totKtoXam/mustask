# US-2: A team member edits a work item on its form

- story: US-2
- depends-on: US-1
- shippable: no — analysis only; delivered by `mod-003-work-item-core`
- status: pending

As a **team member**, I want **one form that shows everything a work item
carries and lets me change it in one save**, so that **I keep the item
current without hunting through several screens**.

## Acceptance criteria

The form's regions, in reading order (reference: screenshot 05): header —
identifier, Тип (read-only, BR-1), Заголовок, Исполнитель, Теги; a status
strip — Статус, Область, Итерация; the body — Описание, the type-specific
field (US-12: «Критерии приёмки» on Пользовательская история, «Шаги
воспроизведения» on Баг) and «Обсуждение» (US-6); a side column — Приоритет;
and the tabs «История» (US-9), «Связи» (US-5) and «Вложения» (US-7), each
showing its own count.

The regions arrive with their slices, not all at once: `mod-003` ships the
form with Описание, «История» and the common field set; «Связи»,
«Обсуждение», the type-specific field and «Вложения» appear with `feat-001`,
`feat-002`, `feat-NNN-work-item-type-fields` and `mod-004`, and the Итерация
control with `mod-007` (spec § Fields). Until a slice ships, its region is
absent, not empty and broken.

```gherkin
Scenario: A team member edits several fields in one save
  Given an existing work item opened by a team member
  When they change Описание, Приоритет and Исполнитель and save once
  Then all three changes are stored
  And the history gains exactly one revision listing the three changes with
    their previous and new values (BR-10)

Scenario: The Исполнитель candidate set is the whole organization
  Given a work item in a project of organization «Орг А»
  When the team member opens the Исполнитель picker and searches by name
  Then every member of «Орг А» is offered, whether or not they belong to the
    team that owns the item's Область (doc-001 BR-13)
  And a member of «Орг Б» is never offered
  And «Не назначен» is always available as a way back to unassigned

Scenario: An assignee who left the organization stays on the item
  Given a work item assigned to user «Иван», whose membership in «Орг А» an
    organization administrator then removes
  When a team member opens the work item
  Then Исполнитель still shows «Иван», marked as a former member of the
    organization (W-3)
  And the history entries naming «Иван» are unchanged
  And saving a change to any other field keeps «Иван» as the assignee and is
    not refused because of him

Scenario: A former member is never offered for a new assignment
  Given «Иван» is a former member of «Орг А», still assigned to some items
  When a team member opens the Исполнитель picker on any work item of «Орг А»
  Then «Иван» is not offered, even when searched for by name (W-3)
  And once his item is reassigned to someone else or to «Не назначен»,
    «Иван» cannot be picked back
  And if «Иван» is later made a member of «Орг А» again, he is offered and
    shown as a member like anyone else

Scenario: Приоритет accepts only 1..4
  Given the work item form
  When the team member sets Приоритет to 0 or to 5
  Then the save is refused, and the message names the allowed range 1..4
  And clearing the field returns it to the default 2 (spec § Fields)

Scenario: Rich text keeps formatting but drops anything executable
  Given Описание containing headings, a bullet list, a link, bold text and a
    code block, plus a <script> tag and an onclick attribute
  When the team member saves
  Then the formatting is preserved on reopening
  And the script tag and the event attribute are gone (BR-4)
  And no image is embedded inline — an image belongs in «Вложения» (US-7)

Scenario: Two members save the same work item at once
  Given two team members who opened the same work item at the same revision
  When both save a field change
  Then the first save succeeds
  And the second is refused with «Элемент изменён другим пользователем»,
    inviting a reload and a reapply (doc-001 BR-11)
  And the first member's change is never silently overwritten

Scenario: A comment is not blocked by a stale field revision
  Given a team member holding an outdated revision of a work item
  When they add a comment without touching any field
  Then the comment is added successfully (BR-15)
  And no conflict is reported, because the comment is a separate write

Scenario: Leaving the form with unsaved changes warns first
  Given a team member has changed Заголовок but has not saved
  When they navigate away from the form
  Then they are asked to confirm, and the change is discarded only after
    they confirm

Scenario: A save arrives for a work item deleted meanwhile
  Given a team member editing a work item that another member deletes before
    the save reaches the server
  When the save arrives
  Then it is refused, naming the item's new state «Удалена» and offering to
    reload (BR-14)
  And no field of the deleted item changes

Scenario: A deleted work item is read-only
  Given a work item in state «Удалена»
  When a team member opens it
  Then every field is shown read-only, with only «Восстановить» offered —
    and «Удалить навсегда» as well for an organization administrator
    (BR-14, US-10)

Scenario: A reader cannot change anything
  Given a user with only the "viewer" role
  When they open a work item
  Then every field is read-only and no Save control exists
  And a write sent directly to the server is refused (doc-001 BR-5)
```
