# US-2: A member reorders the project's backlog

- story: US-2
- depends-on: US-1
- shippable: no — one of the three stories of `feat-039-project-board`; it moves the rows of the backlog US-1 lists
- status: pending

As an **organization member who plans a project's work**, I want **to move a row of the project's backlog to the top, the bottom or after another row, in an order that belongs to the project**, so that **the project agrees what comes next across its teams without touching any team's own order**.

## Acceptance criteria

Unless a scenario says otherwise: the data of US-1, and A's «Истории» backlog lists S1, S2, S3 while MARS's lists S1, S2 and VENUS's S3. U and U2 are organization members («Участник»), V a viewer («Читатель»). Addresses are below `/organizations/O`.

```gherkin
Scenario: A reorder by keyboard (BR-MX-18, mod-006 BR-M4-8, feat-027 BR-T-13, BR-T-14)
  When U opens «Ещё действия: ALP-US-3» on A's backlog and chooses «В начало»
  Then A's backlog lists S3, S1, S2, with «Порядок» 1, 2 and 3
  And focus stays on the row's «Ещё действия: ALP-US-3», and the status region announces «ALP-US-3 перемещён: позиция 1 из 3.»
  And no work item's field or «Изменён» has changed
  And on the first row «В начало» and «Выше» are disabled, on the last row «Ниже» and «В конец»

Scenario: A reorder by pointer, from any point of the row (feat-027 BR-T-13)
  When U drags the row of ALP-US-3 by its «Заголовок» cell above the first row and drops it
  Then A's backlog lists S3, S1, S2, as «В начало» does
  And dropped onto another row it is a parent write (feat-027 BR-T-11) and no place changes

Scenario: The project's order and a team's order are independent (BR-MX-18, ADR-0028 rule 9; re-run by `mod-017-work-item-owning-team` with owned items)
  When U moves S3 to the top of A's backlog
  Then A's backlog lists S3, S1, S2 and MARS's still lists S1, S2
  When U then moves S2 to the top of MARS's backlog
  Then MARS's backlog lists S2, S1 and A's still lists S3, S1, S2
  And on each board the cards of a column stand in the order of their own view's backlog

Scenario: An item that changes team keeps its place on the project's backlog (BR-MX-18; here by the item's «Область», by the owning team once `mod-017-work-item-owning-team` merges)
  Given A's backlog lists S2, S1, S3 and MARS's lists S1, S2, each placed by a reorder
  When S1's «Область» is changed to an area VENUS owns
  Then S1 leaves MARS's backlog and stands on VENUS's below every placed item
  And S1 is still second on A's backlog

Scenario: New and unplaced items follow the placed ones (BR-MX-18, mod-006 BR-M4-6)
  Given A's backlog has been reordered, and S6 (created 10:00), S7 and S8 (both created 10:05, S7 with the lower number) are unplaced
  When U reads A's backlog twice
  Then both reads list the placed items first, then S6, S7, S8
  And a story U creates afterwards from MARS's backlog is the last row of A's backlog

Scenario: A reorder naming an item that is not on this backlog is refused (mod-006 BR-M4-26)
  When U's reorder moves S1 directly after BET-US-1, or moves an epic of A on the «Истории» backlog
  Then the answer is 409 `boards.backlog.reorder_stale` and nothing changes
  And the panel reads A's backlog again and shows «Не удалось переместить: бэклог изменился. Список обновлён — повторите перемещение.», the row back where it was
  And a reorder that gets no answer shows «Не удалось сохранить перемещение. Попробуйте ещё раз.»

Scenario: Two users reorder at once (mod-006 BR-M4-25)
  When U moves S1 to the bottom of A's backlog while U2 moves S3 to its top
  Then both take effect and the next read lists S3, S2, S1, whichever arrived first
  And when both move S2, the later arrival decides its place and nobody is told
  And a reorder of MARS's backlog at the same moment fails neither request

Scenario: A deactivated project keeps its order (ADR-0028 rule 9)
  Given A's backlog lists S3, S1, S2 by a reorder
  When an administrator deactivates A
  Then A's backlog, and a reorder of it, answer «Проект не найден.», and nothing changes
  When an administrator restores A
  Then A's backlog lists S3, S1, S2

Scenario: Who may reorder (BR-PB-5)
  When V opens A's backlog
  Then no row has «Ещё действия», so none of «В начало», «Выше», «Ниже», «В конец» is offered, and no row can be dragged (spec § Roles)
  And V's direct reorder answers 403 `organizations.access.forbidden` «Недостаточно прав» and changes nothing
  And the reorders of U and of an administrator take effect

Scenario: Who may reorder, by scope (BR-MX-22, BR-MX-23; owed by `mod-019-scoped-access`)
  Given V1 is a viewer with project «Изменение» on A, V2 a viewer with team «Планирование» on MARS, V3 a viewer with no grant
  When each of them and U reorders A's backlog
  Then the reorders of V1 and U take effect, and those of V2 and V3 are refused with 403
  And no refused reorder changes anything

Scenario Outline: The order of answers on a reorder (mod-016 api-contract § 0.1 until `feat-041-project-visibility`, BR-MX-24 after it)
  When <who> reorders <where>
  Then the answer is <answer>
  Examples:
    | who | where | answer |
    | nobody signed in | on A's backlog | 401 |
    | a person outside O | on A's backlog | 404 «Организация не найдена.» |
    | V | on A's backlog | 403 «Недостаточно прав» |
    | V | in a project that names nothing | 403 «Недостаточно прав» |
    | U | in a project that names nothing | 404 «Проект не найден.» |
    | U | on A's backlog with the position «sideways» | 400 `boards.validation_failed`, field reason `boards.backlog.position_invalid` «Некорректная позиция перемещения.» |

Scenario: A reorder of a large project backlog (spec NFR: volumes)
  Given A holds 5 000 stories
  When U moves one story to the top of A's backlog
  Then the move is saved within 1 s and the backlog is read again within 2 s
```
