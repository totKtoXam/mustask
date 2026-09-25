# US-7: A member attaches files to a work item

- story: US-7
- depends-on: US-1, US-13
- shippable: no — analysis only; delivered by `mod-004-work-item-attachments`
- status: pending

As an **organization member**, I want **to attach screenshots, logs and documents to a
work item**, so that **the evidence behind a bug or a requirement is one
click away from it**.

## Acceptance criteria

Files are stored through the Google Drive API in the Drive of the work
item's organization, connected by its administrator (W-12, US-13), and
MUSTASK sets no size or count limit of its own — only that Drive's apply
(W-8, spec BR-M3-16). There is no extension filter and no virus scan of
MUSTASK's own (W-13). Unless a scenario says otherwise, the organization's
Drive is connected.

```gherkin
Scenario: Attaching is unavailable while no Drive is connected
  Given an organization with no Google Drive connected
  When a member opens the «Вложения» tab of one of its work items
  Then no upload is possible, and the tab says that the organization's file
    storage is not connected and that its administrator can connect it
  And creating, editing, commenting, linking, tagging, deleting and
    restoring the work item work as usual (BR-M3-17)

Scenario: A member attaches a file
  Given an existing work item
  When the member uploads a file on the «Вложения» tab
  Then it appears in the list with its name, size and the user and time of
    the upload, and the tab's counter goes up by one (screenshot 05)
  And the upload is recorded in history (doc-001 BR-4)

Scenario: Anyone with read access downloads it unchanged
  Given a work item with an attached file
  When any user who can read the work item downloads it
  Then the bytes they receive are identical to the bytes uploaded
  And nothing is previewed inline in MVP — download is the only way to open
    it (spec § Out of scope)

Scenario: Two files with the same name both survive
  Given a work item that already has a file named «отчёт.pdf»
  When the member uploads a different file with the same name
  Then both attachments exist and are downloadable separately
  And the list makes them distinguishable by upload time

Scenario: MUSTASK imposes no size limit of its own
  Given a file of 1 GB, of any extension, well within Google Drive's own
    per-file limit
  When the member attaches it
  Then the upload succeeds and the file downloads back unchanged (BR-M3-16)

Scenario: MUSTASK imposes no count limit of its own
  Given a work item that already has 100 attachments
  When the member attaches one more
  Then the upload succeeds and the counter shows 101 (BR-M3-16)

Scenario: A file Google Drive refuses is not attached
  Given a file the organization's Drive refuses to store — over Drive's own
    limits, or that Drive's storage quota is exhausted
  When the member tries to attach it
  Then the upload fails with a message saying the file storage refused it
  And the work item gains no attachment, the counter is unchanged and
    history gains no entry (BR-M3-16)

Scenario: The quota is per organization
  Given organization «Альфа» whose Drive quota is exhausted, and
    organization «Бета» whose Drive has room
  When a member of «Бета» attaches a file
  Then the upload succeeds — one organization's full Drive never blocks
    another's attachments (BR-M3-16)

Scenario: Existing attachments after the Drive is disconnected or broken
  Given a work item with attachments, in an organization whose Drive was
    then disconnected (US-13) or whose access to it stopped working
  When a member opens the «Вложения» tab and downloads a file
  Then every attachment is still listed with its name, size, uploader and
    time, and the counter is unchanged
  And the download fails with a message saying the organization's file
    storage is unavailable, naming the organization's administrator as the
    one who can fix it
  And nothing is removed from the work item or its history (BR-M3-17)
  And a member may still remove their own attachment and an administrator
    any — removal never waits on the Drive (BR-M3-17)

Scenario: Google Drive is unavailable
  Given the organization's access to its Drive is still valid, but Google
    Drive does not respond or returns a server-side error
  When the member tries to attach a file, or to download an attached one
  Then the action fails with a message saying the file storage is
    temporarily unavailable and the action can be retried later
  And no half-attached file appears in the list, and nothing is recorded in
    history
  And the rest of the work item — fields, state, comments, links, tags — can
    still be read and changed (spec § NFR, External service)

Scenario: MUSTASK serves files only through its permissions
  Given a work item with an attached file
  When a user who cannot read that work item — outside its organization, or
    whose membership was removed — tries to download the file by any link
    MUSTASK has shown
  Then the download is refused (doc-001 BR-5)
  And no link MUSTASK shows opens the file in Google Drive directly — the
    file is not shared to users inside Drive (BR-M3-16, US-13)

Scenario: An attachment is removed, never edited
  Given a work item with an attachment
  When the member removes it
  Then it disappears from the tab and the counter goes down
  And the removal is recorded in history with who and when (doc-001 BR-4)
  And no undo is offered — re-uploading the file is the only way back
    (BR-M3-13)

Scenario: A member removes their own, an administrator removes any
  Given an attachment uploaded by another member
  When a member who did not upload it tries to remove it
  Then the action is refused
  And an organization administrator removing the same attachment succeeds
  And an uploader whose role has become viewer can no longer remove their
    own attachment (BR-M3-13, doc-001 § Roles)

Scenario: Attachments on a deleted work item
  Given a work item in state «Удалена» that has attachments
  When a member opens it
  Then the attachments are listed and can still be downloaded
  And no upload or remove control is offered until it is restored
    (doc-001 BR-14), and restoring the item leaves every attachment in place

Scenario: A failed upload leaves no trace
  Given an upload interrupted midway, by a lost connection or a cancel
  When the member reopens the «Вложения» tab
  Then no half-written attachment is listed, and the counter is unchanged

Scenario: A reader can download but not attach
  Given a user whose role in the organization is viewer
  When they open the «Вложения» tab
  Then they can download every file
  And no upload or remove control is offered
```
