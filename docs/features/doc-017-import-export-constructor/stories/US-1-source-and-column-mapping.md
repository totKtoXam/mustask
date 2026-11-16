# US-1: An importer describes the source and maps columns to fields

- story: US-1
- depends-on: -
- shippable: no — an analysis story; delivered by `mod-024-import-constructor`
- status: pending

As an **organization administrator**, I want **to point a template at a sheet of my file and say which column feeds which work item field**, so that **a team's register lands in MUSTASK without retyping it**.

Examples use a made-up workbook «Реестр.xlsx» with a sheet «Задачи», header row 2, data from row 3, and columns «Номер», «Проект», «Заголовок», «Вид», «Статус», «Ответственный», «Важность», «Срок», «Ссылка».

## Acceptance criteria

```gherkin
Scenario: The template reads the chosen sheet from the chosen rows
  Given the organization administrator uploads «Реестр.xlsx»
  When they choose the sheet «Задачи», header row 2 and first data row 3
  Then the template shows the nine headers of row 2 as its columns
  And a sample of the first rows from row 3 under each header (BR-IE-3)

Scenario: Columns are matched to fields by name and the rest are not moved
  Given a new template over «Задачи»
  When the columns are listed
  Then «Заголовок» is proposed for the field «Заголовок» and «Проект» for «Проект»
  And «Ответственный» and «Вид», which name no field, are proposed as «Не переносить»
  And the administrator may set any column to any offered field or to «Не переносить» (BR-IE-4, BR-IE-5)

Scenario: The project comes from a constant instead of a column
  Given a template whose «Проект» column is set to «Не переносить»
  When the administrator sets the field «Проект» to the constant «Платформа»
  Then every row of the dry run targets the project «Платформа» (BR-IE-1)

Scenario: A template without a source for a required field cannot be saved
  Given a template with no source for «Тип»
  When the administrator saves it
  Then it is not saved, and «Тип» shows «Укажите столбец или константу.» (BR-IE-4)

Scenario: One column feeds two fields
  Given the column «Вид»
  When the administrator maps it to «Тип» and, by a second mapping, to «Теги»
  Then each mapping has its own converters and recoding (BR-IE-4)
  # the «Теги» target is owed by feat-016, which adds it to the catalogue

Scenario Outline: A filled cell for a field the row's type lacks
  Given «Срок» mapped to «Целевая дата» with the option <option>
  And a row of type «Задача», which carries no «Целевая дата», whose «Срок» holds <cell>
  When the dry run runs
  Then the row is <result> (BR-IE-5)

  Examples:
    | option                              | cell       | result                                                                                     |
    | «Ошибка строки»                     | empty      | not an error for «Срок»                                                                    |
    | «Ошибка строки»                     | 01.03.2026 | an error «Это поле недоступно для рабочего элемента этого типа.» naming «Срок»             |
    | «Не переносить для типов без поля»  | 01.03.2026 | written without the date, with a row warning naming «Срок»                                 |

Scenario: A later file without a mapped column is refused whole
  Given a saved template that maps «Ответственный»
  When the importer runs it on a file whose header row has no «Ответственный»
  Then the file is refused before any row is read, naming «Ответственный»
  And extra columns of the file are ignored (BR-IE-4)

Scenario: A header matches ignoring case and surrounding spaces
  Given a template mapping «Заголовок»
  When the file's header reads « заголовок »
  Then the column is matched (BR-IE-4)

Scenario: A CSV in Windows-1251 with «;» is detected and can be overridden
  Given a .csv file saved in Windows-1251 with «;» between values
  When the administrator uploads it
  Then the template shows the encoding «Windows-1251» and the delimiter «;»
  And the headers read correctly in Cyrillic
  When the administrator changes the delimiter to «,»
  Then the columns are read again with «,» (BR-IE-2)

Scenario: A quoted value with a line break stays one value
  Given a .csv row whose description is a quoted text holding two line breaks
  When the file is read
  Then the row has as many values as the header, and the description keeps both line breaks (BR-IE-2)

Scenario: An unsupported or oversized file is refused
  Given a file «Реестр.xls»
  When the importer uploads it
  Then it is refused with «Поддерживаются файлы .xlsx и .csv.»
  Given a .xlsx of 5 001 data rows on the chosen sheet
  When the importer uploads it
  Then it is refused whole, naming the 5 000-row limit, and no row is read (BR-IE-21)

Scenario: A formula's last value is read and never evaluated
  Given a cell holding a formula whose stored value is «Платформа»
  When the file is read
  Then the cell gives «Платформа» (BR-IE-3)

Scenario: Empty rows are skipped and counted
  Given three rows whose mapped cells are all empty
  When the dry run runs
  Then they are reported as «Пропущен» with «Пустая строка» and are counted (BR-IE-3, BR-IE-13)

Scenario: Who builds templates
  Given an organization «Участник» who manages no project or department, and a project «Менеджер»
  When they open the organization's «Импорт»
  Then the «Участник» cannot create or change a template
  And the «Менеджер» may create a template, and pick and read any saved one (§ Roles, OQ-12 B)
```
