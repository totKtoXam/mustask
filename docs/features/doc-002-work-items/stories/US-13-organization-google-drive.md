# US-13: An organization administrator connects the organization's Google Drive

- story: US-13
- depends-on: -
- shippable: no — analysis only; delivered by `mod-004-work-item-attachments`
- status: pending

As an **organization administrator**, I want **to connect my organization's
own Google Drive to MUSTASK**, so that **our work item attachments live in
storage we own, under our quota, and nobody else's Drive holds them**.

## Acceptance criteria

Each organization connects its own Drive, and MUSTASK stores in it only that
organization's attachment files (W-12, spec BR-M3-16, BR-M3-17). W-12 names
no role; the organization administrator connects and disconnects it, as with
every other organization-wide setting in doc-001 (§ Roles, D-9), through a
section of the organization settings (doc-006 § Roles). One Drive
is connected at a time and is never swapped in place — disconnect, then
connect another (spec § Operation completeness). The Drive belongs to the
organization: whoever the organization lets into it — its owner, its Google
Workspace administrators — can reach the files there directly, outside
MUSTASK, and MUSTASK neither prevents nor promises otherwise. The
setting sits among the organization's settings; which credentials Google
requires and how MUSTASK keeps them are `mod-004-work-item-attachments`'
ADR.

```gherkin
Scenario: An administrator connects the organization's Drive
  Given an organization with no Google Drive connected
  When its administrator opens the organization's settings, provides the
    Drive's access and saves
  Then MUSTASK checks that it can store and read back a file in that Drive
  And the setting shows the Drive as connected, with who connected it and
    when
  And members of that organization can attach files from now on (US-7)

Scenario: A Drive MUSTASK cannot use is not connected
  Given an organization with no Google Drive connected
  And its administrator provides access that is wrong, revoked, or lacks
    the right to write files
  When they save the setting
  Then the Drive is not connected and the administrator sees a message
    saying MUSTASK could not store a file in it
  And the organization stays with no Drive, and attaching stays unavailable

Scenario: The credentials are never shown back
  Given an organization whose Drive is connected
  When anyone — the administrator who entered them included — opens the
    setting, or reads any page or response MUSTASK returns
  Then the Drive's credentials are not shown; only the fact of the
    connection, who made it and when
  And the credentials appear in no log MUSTASK writes (spec § NFR)

Scenario: Only an administrator manages the connection
  Given a user whose role in the organization is member or viewer
  When they open the organization's settings
  Then no connect or disconnect control is offered
  And such a request sent directly to the server is refused (doc-001 BR-5)

Scenario: Organizations never share a Drive connection
  Given organizations «Альфа» and «Бета», each with its own Drive connected
  When a member of «Альфа» attaches a file
  Then the file is stored in «Альфа»'s Drive, never in «Бета»'s
  And an administrator of «Бета» neither sees nor changes «Альфа»'s setting

Scenario: MUSTASK shares no file inside Drive
  Given a file attached through MUSTASK
  When the file's sharing settings in the organization's Drive are inspected
  Then MUSTASK has added no share to any user's Google account — the file
    carries only the access the Drive itself already grants
  And every download MUSTASK serves goes through its permission check, so a
    user whose membership is removed can no longer download the
    organization's files through MUSTASK (doc-001 BR-5)

Scenario: An administrator disconnects the Drive
  Given an organization whose Drive is connected and whose work items have
    attachments
  When its administrator disconnects the Drive and confirms — the dialog
    says that existing attachments cannot be downloaded once it is
    disconnected
  Then attaching becomes unavailable in that organization (US-7)
  And every existing attachment stays listed on its work item, and nothing
    is removed from any work item or its history (BR-M3-17)
  And the files themselves are not deleted from the Drive by the disconnect
  And the setting shows who disconnected it and when
```

Not decided here: what reconnecting the same or a different Drive restores
for existing attachments — the recovery policy is
`mod-004-work-item-attachments`' ADR (spec BR-M3-17). MUSTASK does not move
files between Drives in MVP (spec § Out of scope).
