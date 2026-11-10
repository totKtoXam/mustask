# US-7: Templates are saved per organization, edited, copied and deleted

- story: US-7
- depends-on: US-1
- shippable: no — an analysis story; delivered by `mod-NNN-import-constructor`
- status: pending

As an **organization administrator**, I want **to keep a template by name and apply it again**, so that **next week's file goes in with the same settings, and another source gets its own**.

## Acceptance criteria

```gherkin
Scenario: A template is saved with everything it describes
  Given a template with its sheet, rows, mappings, converters, recoding tables, policies and identifier setting
  When the administrator saves it as «Реестр — задачи»
  Then reopening it shows the same settings (BR-IE-18)

Scenario: A saved template is applied to a new file
  Given the template «Реестр — задачи»
  When an importer uploads next week's file and picks it
  Then the dry run runs without any setting re-entered (BR-IE-18)

Scenario: New values in a new file show up in the recoding table
  Given «Реестр — задачи» and a new file with the type value «Research», in no entry
  When the administrator opens the type's recoding table
  Then «Research» is listed as a new value with its row count, and the saved entries are kept (BR-IE-7)

Scenario: Names are unique in the organization
  Given a template «Реестр — задачи»
  When another is saved as « реестр — задачи »
  Then it is refused as a taken name (BR-IE-18)

Scenario: A name must be 1–100 characters
  Given a name of spaces only, or of 101 characters
  When the template is saved
  Then it is refused (BR-IE-18)

Scenario: Copy keeps the settings under a new name
  Given «Реестр — задачи»
  When the administrator copies it
  Then «Копия Реестр — задачи» appears with the same settings
  And copying again gives «Копия Реестр — задачи 2» (BR-IE-18)

Scenario: Two sources, two templates
  Given «Реестр — задачи» over a workbook and «Трекер — CSV» over another tracker's export
  When each is applied to its own file
  Then each reads its own columns with its own chains and tables, and neither changes the other (BR-IE-18)

Scenario: The later of two concurrent saves wins
  Given two administrators editing «Реестр — задачи», and a dry run made on the version they both opened
  When one saves and then the other saves
  Then the template holds the second save, and the dry run offers no «Импортировать» (BR-IE-14, BR-IE-18)

Scenario: Delete is final and keeps the batches
  Given «Старый шаблон» with one batch
  When the administrator deletes it and confirms
  Then it leaves the list, and the batch still opens with its report (BR-IE-18)

Scenario: An organization holds at most 100 templates
  Given 100 templates
  When the administrator saves a new one
  Then it is refused with the limit named (BR-IE-18)

Scenario: Templates stay in their organization
  Given a template of the organization «Альфа»
  When a member of «Бета» only asks for it by its id
  Then the answer is the one for an id that names nothing (doc-012 BR-MX-1)

Scenario: Members read templates, only the administrator changes them
  Given an organization «Читатель»
  When they open «Экспорт по шаблону»
  Then they see the organization's templates by name, with the columns each exports
  And they see no recoding table or other setting, and are offered no create, edit, copy or delete (§ Roles)

Scenario: A manager edits only the templates they authored
  Given a project «Менеджер» who authored «Мой реестр», and «Реестр — задачи» authored by the organization «Администратор»
  When they open «Реестр — задачи» in «Импорт»
  Then they see its settings, recoding tables included, read-only, and may copy it
  And they are offered no edit or delete on it, but are offered both on «Мой реестр» (§ Roles, BR-IE-18, OQ-12 B)

Scenario: A template outlives its author's role
  Given «Мой реестр» authored by a «Менеджер» who has since lost «Менеджер» everywhere
  When they open it
  Then they see no edit or delete on it
  And the organization «Администратор» may still edit or delete it (BR-IE-18)
```
