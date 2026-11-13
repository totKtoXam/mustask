# doc-017 input brief

The human's ask (2026-11-10), quoted:

- «создать импорт/экспорт из excel/csv и конструктор для него, в котором
  ключевое, что будут указывать маппинг полей, их конвертация»
- «что касается остального функционала — собери, что нам нужно сделать
  (инструменты и функционал) на основе анализа отправленного файла»

The file is the team's planning workbook (the «DevEx sheet»), which the
team moves into MUSTASK. It holds company work data: this brief keeps only
its shape. No names, handles or emails go into any artifact of this task;
examples use made-up values.

## The workbook

21 sheets. What each becomes:

| Sheet | What it is | In MUSTASK |
|---|---|---|
| `DevEx` | the task register, 543 rows, 20 columns, 11 drop-down lists, 206 hyperlinks (147 to documents, 52 to another tracker) | work items: the main import |
| `Dict` | dictionaries: states, priorities, estimate buckets with hours, budget lines, projects, chat groups, people (chat handle, email), types, a Fibonacci scale | organization dictionaries + recoding tables of an import template |
| `Логи` | a change log: when, who, old → new value | item history (feat-015) |
| `tg_msg_logs`, `configs` | a chat bot's log: assignment, state change, a 09:00 risk report | notifications + a Telegram channel (not queued) |
| `Current` and dated weekly sheets | weekly snapshots for the customer report: project, task, assignee, budget line, state, estimates, deadlines, links | a "snapshot on a date" report = query + history; no one-off import needed |
| `Дашборд` | counts by project and state | dashboards (mod-009 / feat-006) |
| `Задачи ТОС_2026` | another team's work list: system, module, description, deadline | a second import template |
| `TestSheet` | scratch | not moved |

## `DevEx` columns

| Column | Filled | Values | MUSTASK field | Conversion | Exists? |
|---|---|---|---|---|---|
| Id | 432/543 | `DDMMYY - NN`, unique | external key | as is; a re-import updates instead of duplicating | no external key field |
| Проект | 543 | 20 values | project (+ department) | recoding table, create missing | yes |
| Заголовок | 543 | text | title | trim, within the limit | yes |
| Теги | 69 | `FE, BE, UI`, comma-separated | tags | split by «,» | feat-016 queued |
| TaskType | 542 | UserStory 448, Bug 88, HotFix 6 | type | UserStory → История, Bug → Ошибка, HotFix → Ошибка + priority, or its own type | yes (HotFix to decide) |
| Changer | 537 | 14 people | assignee | recoding table name → user (by email from `Dict`) | field yes; the person must exist → feat-053 (invite by email) |
| Статус | 543 | 11 ordered states, «01. …» … «11. …» | state | recoding table 11 → N | fixed state set; 11 states do not map 1:1 |
| Priority | 534 | `00 urgent` … `03 low` | priority 1–4 | 00→1 … 03→4 | yes |
| Team | 462 | 116 combinations of people | participants / watchers | split by «,», each → user | no: an item has one assignee |
| Вес A | 290 | buckets «10-20 мин» … «1 месяц» | analysis estimate | bucket → hours by `Dict` (0.5 … 160) | no second estimate field |
| Вес D | 249 | the same buckets | «Исходная оценка» (hours) | bucket → hours | yes |
| Оценка | 11 | 3, 5 | story points | number | yes |
| Срок | 76 | Excel serial date | target date | serial → date | yes |
| Комментарии | 130 | text | first comment | as is | feat-002 queued |
| Ссылка | 218 | text + hyperlink | external link | take the URL from the hyperlink | no external links (only item-to-item links) |
| Баг репорт | 1 | — | attachment/link | — | mod-004 queued |
| Git branches | 0 | — | — | not moved | — |
| TG_* | 73 | message ids | link to a chat thread | service data | no Telegram channel |
| budget line (`Current`, `Dict`) | — | 30+ values like `<DEPT>_<PROJECT>_RnD_2026` | budget classifier | recoding table | no custom fields |

## The constructor outline (the orchestrator's, to be analysed, not adopted)

1. Source: .xlsx and .csv (spreadsheets online through their export).
   Sheet, header row, first data row.
2. Target: organization; project from a column or a constant.
3. Column mapping: column → field, auto-match by name, «не переносить».
4. Value recoding for dictionary fields (type, state, priority, project,
   people): file value → MUSTASK value, suggestions by match, «create
   missing» (projects), a default.
5. Converters, chained per column: trim; split by a delimiter; date from a
   serial number or a string by format; number; bucket → hours by a table;
   URL from a hyperlink; regex extract; constant; concatenation of columns
   (into the description).
6. Key: an external-key column → re-import updates, not duplicates
   (upsert).
7. Dry run: a preview of N result rows, a per-row report: created /
   updated / error and why. Nothing is written.
8. Import: in batches, through the same validation as the form; the result
   is an import batch with a report; the batch can be undone.
9. Template: steps 1–6 saved by name per organization, applied again (the
   weekly snapshot again and again).
10. Export by the same template the other way: chosen fields → columns,
    values recoded back, .xlsx / .csv.

Permissions: import and templates — organization administrator (or a
separate permission). Row and file limits, encoding, CSV delimiter — to
decide.

## Gaps for a lossless transfer

| # | Capability | Why, from the file | Queued? |
|---|---|---|---|
| 1 | external key and external links on an item | the `Id` column, 206 links | no |
| 2 | organization custom fields (text, number, list, date) | Вес A, budget line, future columns | no (doc-007 row `mod-NNN-process-custom-fields`, unqueued, doc-010 D-21) |
| 3 | item participants (several people besides the assignee) | the `Team` column, 462 rows | no |
| 4 | custom states / a configurable process | 11 ordered states, a board by them | no (doc-007 row `feat-NNN-process-custom-states`, unqueued, D-21): decide recoding vs configuration |
| 5 | notifications + a Telegram channel | the bot: assignment, state change, 09:00 risk report, mention groups | no (doc-010 D-18: owner not settled) |
| 6 | invite by email | 14 assignees must exist before the import | feat-053 |
| 7 | tags, comments, history, attachments | Теги, Комментарии, `Логи`, Баг репорт | feat-016, feat-002, feat-015, mod-004 |
| 8 | snapshot on a date / customer report | the weekly sheets | partly: mod-008 queries + feat-015 history |
| 9 | dashboard by project and state | `Дашборд` | mod-009, feat-006 |

The orchestrator's suggested order: this analysis; the minimum for the first
transfer (external key + links, feat-053, feat-016); the constructor
(import with mapping, recoding, converters, dry run, templates), then
export; lossless extras (custom fields, participants, states); notifications
+ Telegram as its own line.
