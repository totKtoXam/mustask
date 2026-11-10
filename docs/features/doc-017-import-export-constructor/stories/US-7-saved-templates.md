# US-7: Templates are saved per organization, edited, copied and deleted

- story: US-7
- depends-on: US-1
- shippable: no — an analysis story; delivered by `mod-NNN-import-constructor`
- status: pending

As an **organization administrator**, I want **to keep a template by name and apply it again**, so that **next week's sheet goes in with the same settings, and another team's sheet gets its own**.

## Acceptance criteria

```gherkin
Scenario: A template is saved with everything it describes
  Given a template with its sheet, rows, mappings, converters, recoding tables, policies, key and source «DevEx»
  When the administrator saves it as «DevEx — реестр»
  Then reopening it shows the same settings (BR-IE-18)

Scenario: A saved template is applied to a new file
  Given the template «DevEx — реестр»
  When the administrator uploads next week's file and picks it
  Then the dry run runs without any setting re-entered (BR-IE-18)

Scenario: New values in a new file show up in the recoding table
  Given «DevEx — реестр» and a new file with the type value «Spike», in no entry
  When the administrator opens the type's recoding table
  Then «Spike» is listed as a new value with its row count, and the saved entries are kept (BR-IE-7)

Scenario: Names are unique in the organization
  Given a template «DevEx — реестр»
  When another is saved as « devex — реестр »
  Then it is refused as a taken name (BR-IE-18)

Scenario: A name must be 1–100 characters
  Given a name of spaces only, or of 101 characters
  When the template is saved
  Then it is refused (BR-IE-18)

Scenario: Copy keeps the settings under a new name
  Given «DevEx — реестр»
  When the administrator copies it
  Then «Копия DevEx — реестр» appears with the same settings
  And copying again gives «Копия DevEx — реестр 2» (BR-IE-18)

Scenario: Two sheets, two templates
  Given «DevEx — реестр» with source «DevEx»
  When the administrator builds «ТОС — задачи» over another sheet with source «ТОС»
  Then their keys never match each other's items (BR-IE-10, OQ-2)

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
```
