# US-7: Links are made and ended by those who manage their ends

- story: US-7
- depends-on: US-1, US-3
- shippable: no — the link codes change hands with the grants of US-1
- status: pending

As a **holder of «Управление» on a project, a team or a department**, I want **to link and unlink the objects I manage, from my own page**, so that **the matrix is kept by the people who run its parts, and nobody gains power over the other end of a link (BR-SA-15; doc-012 BR-MX-6, US-3, US-10; ADR-0031 rule 4)**.

## Acceptance criteria

Fixture and users of US-1 (everyone a «Читатель» unless said). Only two link kinds exist under the typed tree: project–team and department–team. MARS and VENUS are linked to A; the team JUPITER is linked to nothing. Kirill holds «Управление» on A; Pavel on «Логистика»; Olga on MARS; Irina on VENUS and on A; Lena (also a «Читатель») on A and on MARS.

```gherkin
Scenario: Creating a link needs the link code on both ends
  When Kirill links A to the team JUPITER (no grant of his there)
  Then it answers 403 «Недостаточно прав» and creates nothing
  When Irina, holding «Управление» on A and on VENUS, links VENUS to B (no grant of hers on B)
  Then it answers 403; Lena, managing A and MARS, links MARS to B only after she also manages B

Scenario: The multiselect offers only what the caller may link
  Given Lena also holds «Управление» on B, and MARS is not linked to B
  When Lena opens the command to add teams on B's «Команды»
  Then the multiselect offers MARS only: she holds the team link code on MARS, not on VENUS or JUPITER
  And typing narrows it, and Anna's multiselect offers every unlinked team

Scenario: Removing a project–team link needs both ends, as it reassigns items (doc-012 BR-MX-12)
  Given MARS owns items of A
  When Kirill, managing A only, unlinks MARS from A
  Then it answers 403 «Недостаточно прав» and nothing changes
  When Irina, managing A and VENUS but not MARS, unlinks MARS from A choosing VENUS as the destination
  Then it answers 403 as well, since MARS's end is lacking
  When Lena, managing A and MARS, unlinks it choosing VENUS
  Then it is kept, and the items are reassigned in the one operation

Scenario: The unlink destination does not need a grant on it
  When Lena unlinks MARS from A
  Then the confirmation offers VENUS as the destination though she holds no grant on VENUS, and «Без команды»

Scenario: A department–team link ends on either end
  Given «Логистика» is linked to MARS
  When Pavel, managing «Логистика» only, removes the link
  Then it is kept, and MARS and «Логистика» are otherwise unchanged
  And creating the link again needs the link code on both «Логистика» and MARS, so Pavel alone gets 403 and Olga alone gets 403

Scenario: A link gives no power over its other end
  Given Pavel manages «Логистика», linked to MARS, and holds nothing on MARS
  When he renames, deletes or adds a member to MARS, or Olga renames «Логистика»
  Then each answers 403 «Недостаточно прав», and the link is the only thing either can end

Scenario: Row commands are shown by the codes
  When Kirill, Irina, Olga and Vera open A's «Команды»
  Then the unlink command is on the row of MARS only for a user who holds the link code on both ends
  And on a department's «Команды» it is on the row for a holder of either end's code; Vera sees rows and no command

Scenario: The addressed end is judged before the other end (ADR-0029 § 7)
  When Vera links A to an id that names no team, and Kirill, holding the code on A, does the same
  Then Vera's answers 403 «Недостаточно прав», and Kirill's answers «Команда не найдена.»; a team of another organization answers as one that names nothing

Scenario: Linking or unlinking changes no access
  Given Pavel holds «Изменение» on «Логистика» and A is linked to MARS
  When the link is removed and made again
  Then no user's codes on A or on MARS change
```
