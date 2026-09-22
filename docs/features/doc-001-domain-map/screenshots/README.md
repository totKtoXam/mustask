# Azure DevOps Boards — UI reference screenshots

Captured 2026-09-22 from a live Azure DevOps Services organization (Agile
process) as a visual reference for the domain analysis. Research notes with
the configuration read through the REST API are in
[../research/ado-boards.md](../research/ado-boards.md).

**Masking.** The source project is a real one, so before every capture the
page DOM was rewritten in the browser (nothing was written to Azure DevOps):
every Cyrillic text — work item titles, descriptions, acceptance criteria,
project, team and area/iteration names — and every user name became the
filler `Пример формулировки задачи`; the organization slug became
`organization`. Only the layout, the English UI chrome, the built-in field
names, states and tags (generic English words) are genuine. The header avatar
still shows the captured account's initials; they are not project data.

| File | Screen |
|------|--------|
| `01-board-stories.png` | Kanban board of the requirement level (Stories): columns New / Active / Resolved / Closed with WIP limits, cards with id, title, state, tags |
| `02-board-features.png` | Kanban board of the Features portfolio level |
| `03-backlog-stories.png` | Product backlog: ordered list, columns (Order, Type, Title, State, Story Points, Value Area, Iteration), the Planning pane with sprints for drag-and-drop |
| `04-backlog-epics.png` | Portfolio backlog (Epics) |
| `05-workitem-form.png` | Work item form (User Story): header (state, reason, area, iteration, tags), Description, Acceptance Criteria, Discussion, Planning, Classification, Development, Related Work |
| `06-workitem-history.png` | Work item History tab: state graph and the field-by-field revision list |
| `07-workitem-links.png` | Work item Links tab: link groups (Parent) with state and latest update |
| `08-workitems-list.png` | Work items hub: recently updated list with Types / Assigned to / States / Area / Tags filters, Import, Recycle Bin |
| `09-workitem-new-dropdown.png` | The "New Work Item" type picker (Bug, Epic, Feature, Issue, Task, Test Case, User Story) |
| `10-sprint-taskboard.png` | Sprint Taskboard (empty: no tasks and no iteration dates in the source project) |
| `11-sprint-backlog.png` | Sprint backlog view |
| `12-sprint-capacity.png` | Sprint Capacity: per-member days off, activity, capacity per day; team days off |
| `13-queries-list.png` | Queries hub: My Queries / Shared Queries folders |
| `14-query-editor.png` | Query editor: clause builder (And/Or, field, operator, value), query type |
| `15-query-results.png` | Query results with the work item preview pane |
| `16-dashboard.png` | Team dashboard (empty: "Add a widget") |
| `17-settings-iterations.png` | Team configuration → Iterations (root iteration, sprint list with dates) |
| `18-settings-areas.png` | Team configuration → Areas |
| `19-settings-templates.png` | Team configuration → work item Templates |
| `20-settings-permissions.png` | Project settings → Permissions: built-in groups (Project Administrators, Contributors, Readers, …) and the team |
| `21-settings-notifications.png` | Project settings → Notifications: default team subscriptions by category |
| `22-process-list.png` | Organization settings → Process: the four system processes (Basic, Agile, Scrum, CMMI), inherited processes are created from them |
| `23-process-agile-types.png` | Agile process → Work item types |
| `24-process-agile-backlog-levels.png` | Agile process → Backlog levels: portfolio (Epics, Features), requirement (Stories), iteration (Tasks), other types (Bug per team setting, Issue on no backlog) |
| `25-process-userstory-layout.png` | User Story → Layout: form pages, groups and fields with their types |
| `26-process-userstory-states.png` | User Story → States grouped by category (Proposed, In Progress, Completed, Removed) |
| `27-board-settings-*.png` | Board settings dialog: General, Cards (Fields, Styles, Tag colors, Annotations, Tests), Boards (Columns with state mapping and WIP, Swimlanes, Card reordering) |

Not captured: Delivery Plans (none exist in the source project, the hub
returns 404 without a plan), Analytics views and charts (empty without
history), the Taskboard with tasks (the source project has no tasks).
