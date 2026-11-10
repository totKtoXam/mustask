# US-3: An administrator converts a column's values by a chain of converters

- story: US-3
- depends-on: US-1
- shippable: no — an analysis story; delivered by `mod-NNN-import-constructor`
- status: pending

As an **organization administrator**, I want **to chain converters on a column**, so that **a value written the sheet's way becomes a value the field accepts**.

## Acceptance criteria

```gherkin
Scenario: Trim removes ordinary and non-breaking spaces
  Given «Заголовок» with «Обрезать пробелы» and a cell « Отчёт по продажам » with a non-breaking space at the end
  When the row is converted
  Then the title is «Отчёт по продажам» (BR-IE-8)

Scenario: Split gives a list without empty parts
  Given «Теги» with «Разделить» by «,» and a cell «FE, BE,, UI »
  When the row is converted
  Then the result is the list FE, BE, UI (BR-IE-8)

Scenario: An Excel serial number becomes a date
  Given «Срок» with «Дата из числа Excel» and a cell holding 46023 in a workbook of the 1900 system
  When the row is converted
  Then the date is 01.01.2026 (BR-IE-8)

Scenario: A text date is read by its format
  Given «Дата по формату» «ДД.ММ.ГГГГ» and a cell «15.03.2026»
  When the row is converted
  Then the date is 15.03.2026
  And a cell «2026-03-15» under the same format makes the row an error naming the cell, the value and the converter (BR-IE-8, BR-IE-9)

Scenario: A date outside 2000–2099 is an error
  Given «Дата из числа Excel» and a cell holding 1
  When the row is converted
  Then the row is an error «Укажите дату с 2000 по 2099 год.» (BR-IE-9, feat-022 BR-M3-35)

Scenario: A number accepts either separator and group spaces
  Given «Число» and the cells «2,5», «2.5» and «1 200»
  When the rows are converted
  Then the values are 2,5, 2,5 and 1200
  And a cell «abc» makes its row an error (BR-IE-8, BR-IE-9)

Scenario: A bucket becomes hours by a table
  Given «Вес D» with «По таблице»: «10-20 мин» → 0,5, «1-2 часа» → 2, «1 месяц» → 160
  When a row holds «1-2 часа»
  Then «Исходная оценка» gets 2 (BR-IE-8)
  And a row holding «2 месяца», in no entry, follows the table's policy (BR-IE-7)

Scenario: A hyperlink gives its target address
  Given «Ссылка» with «URL из гиперссылки» and a cell showing «Документ» that links to «https://docs.example.com/d/1»
  When the row is converted
  Then the result is «https://docs.example.com/d/1» (BR-IE-8)
  And a cell with no hyperlink whose text is «https://docs.example.com/d/2» gives that text
  And a cell with neither gives empty

Scenario: A regular expression extracts its first group
  Given «Извлечь по шаблону» «#(\d+)» and a cell «Задача #4521 в работе»
  When the row is converted
  Then the result is «4521»
  And a cell with no match gives empty (BR-IE-8)

Scenario: A runaway regular expression stops the row, not the run
  Given a pattern that takes more than 100 ms on a cell
  When the row is converted
  Then that row is an error «Шаблон выполняется слишком долго.», and the other rows go on (NFR file safety)

Scenario: A constant ignores the cell
  Given «Вид работ» fed by «Константа» «Разработка»
  When any «Задача» row is converted
  Then «Вид работ» is «Разработка» (BR-IE-8)

Scenario: Concatenation builds the description from several columns
  Given «Описание» fed by «Объединить» «{Комментарии}\nTG: {TG_id}»
  When a row holds «Согласовать макет» and «4417»
  Then the description is «Согласовать макет», a line break, «TG: 4417» (BR-IE-8)
  And a placeholder naming a column the file lacks refuses the file, as a missing mapped column does (BR-IE-4)

Scenario: Truncate keeps the limit and warns
  Given «Заголовок» with «Обрезать до длины» at the title's limit
  When a cell is longer than the limit
  Then the title is cut to the limit and the row carries a warning (BR-IE-8)

Scenario: Converters run left to right and recoding runs last
  Given «Team» with «Обрезать пробелы», then «Разделить» by «,», then a people recoding
  When a row holds « А , Б »
  Then «А» and «Б» are recoded each (BR-IE-9)

Scenario: A chain has at most ten converters
  Given a chain of ten converters
  When the administrator adds an eleventh
  Then it is not added (BR-IE-9)

Scenario: A list cannot feed a single-value field
  Given «Changer» with «Разделить» mapped to «Исполнитель»
  When the administrator saves the template
  Then it is not saved: «Исполнитель» holds one value (BR-IE-9)
```
