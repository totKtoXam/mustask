# Azure DevOps Boards — research notes (collected 2026-09-22)

Source: live Azure DevOps Services org, read-only REST API (GET + WIQL id query), api-version 7.1.
Only the product's domain model and configuration were collected. No work item content
(titles, descriptions, names) was read or copied.

## Organization / project
- Organization → Projects. The project uses the **Agile** system process (inherited processes supported;
  4 system processes: Basic, Scrum, Agile, CMMI; custom processes inherit from one of them).
- Project capabilities: process template + version control (Git). One default team per project ("<Project> Team");
  more teams can be added. Team field = Area Path (a team owns a set of area paths).

## Work item types (WIT) and states (state → category)
State categories: Proposed, InProgress, Resolved, Completed, Removed. Boards/backlogs/burndowns work on categories,
so each type can have its own state names.

- **Basic**: Epic, Issue, Task (To Do→Proposed, Doing→InProgress, Done→Completed); Test Case/Plan/Suite.
- **Agile**: Epic, Feature, User Story (New, Active, Resolved[InProgress], Closed, Removed); Task (New, Active, Closed, Removed);
  Bug (New, Active, Resolved[Resolved], Closed); Issue (Active, Closed); Test Case (Design, Ready, Closed); Test Plan; Test Suite.
- **Scrum**: Epic, Feature (New, In Progress, Done, Removed); Product Backlog Item and Bug (New, Approved, Committed, Done, Removed);
  Task (To Do, In Progress, Done, Removed); Impediment (Open, Closed); Test*.
- **CMMI**: Epic, Feature, Requirement, Task, Bug, Issue, Change Request, Risk, Review (Proposed, Active, Resolved, Closed); Test*.

## Backlog levels ("behaviors"), ranks
Portfolio: Epics (40), Features (30); Requirement level: Stories/Backlog items/Requirements/Issues (20); Tasks (10);
Ordered behavior (stack rank). Custom portfolio levels can be added. Each WIT maps to one level (default one marked).
Bugs: team setting `bugsBehavior` = on requirement backlog / as tasks / hidden (this team: asTasks).
Team toggles which backlog levels are visible (Epics, Features, Stories).

## Fields (Agile). Types: string, html, integer, double, dateTime, treePath, identity, plainText (tags), history, picklist
Common to all: ID, Title (req), Description (html), Assigned To (identity), Area Path, Iteration Path (treePath), State (req, default New),
Reason, History/Discussion, Tags, Work Item Type, Created/Changed By/Date, State Change Date, Activated/Resolved/Closed By/Date,
Priority (1–4, default 2), Stack Rank (backlog order).
- Epic/Feature: Risk (1-High,2-Medium,3-Low), Business Value (int), Time Criticality, Effort, Start Date, Target Date, Value Area (Architectural|Business, req).
- User Story: Story Points, Acceptance Criteria (html), Risk, Start/Finish Date, Value Area.
- Task: Activity (Deployment|Design|Development|Documentation|Requirements|Testing), Original Estimate, Remaining Work, Completed Work, Start/Finish Date.
- Bug: Repro Steps, System Info (html), Severity (1-Critical..4-Low, default 3), Found In / Integration Build, Story Points, Remaining/Original/Completed Work, Activity, Resolved Reason (As Designed, Cannot Reproduce, Copied to Backlog, Deferred, Duplicate, Fixed, Fixed and verified, Obsolete, Will not Fix).
- Issue: Due Date, Activity, Story Points, Remaining Work.
System/bookkeeping fields also exist: Rev, counts of links/attachments/comments, BoardColumn, BoardColumnDone, BoardLane, Parent.

## Rules (per WIT; 37–136 rules each)
Condition types: when, whenNot, whenWas, whenChanged, whenNotChanged, (group membership).
Actions: copyValue, setDefaultValue, makeRequired, makeReadOnly, setValueToEmpty, copyFromClock, copyFromCurrentUser, hide.
Built-in examples (Bug): Reason is read-only unless State changes; each transition sets a default Reason and makes it required
(New→Active "Approved", Active→Resolved "Fixed", Resolved→Closed "Verified", Closed→Active "Regression", …);
Activated/Resolved/Closed By/Date are stamped by rules on entering the state category and cleared on leaving it.

## Transitions
In inherited processes any state → any state is allowed (User Story: New↔Active↔Resolved↔Closed, →Removed; Removed→New).
Each transition has a Reason list. Entering Resolved from New/Active triggers actions (e.g. assign back to creator).

## Link types (18)
Work item links: Parent/Child (tree), Related (network), Predecessor/Successor (dependency), Duplicate/Duplicate Of (tree),
Affects/Affected By (dependency), Tests/Tested By, Test Case/Shared Steps, References/Referenced By (test).
Resource links: Attached File, Hyperlink, Artifact Link (commits, branches, PRs, builds, wiki pages).
Topologies: tree (one parent, no cycles), dependency (no cycles), network (any).

## Team settings
- Working days (Mon–Fri default); default iteration macro @CurrentIteration; backlog (root) iteration.
- Iterations: tree of classification nodes with start/finish dates; the team selects which iterations it uses; timeFrame past/current/future.
- Areas: tree of classification nodes; team owns area paths (optionally incl. sub-areas).
- Capacity per iteration: per member, per activity hours/day, days off (member and team).

## Boards (Kanban), one per backlog level per team
Columns: incoming (first) / inProgress / outgoing (last), each maps each WIT to a state; WIP limit per column (default 5);
split columns (Doing/Done); Definition of Done per column. Swimlanes (rows, default + custom, e.g. Expedite).
Card settings: which fields show per WIT (defaults: ID, Title, Assigned To, Tags, State, Story Points), show empty fields,
annotations (child tasks checklist, tests), card styling rules (color by query criteria), tag colors.
Board fields: BoardColumn, BoardColumnDone, BoardLane are stored on the work item per team board.

## Backlogs & sprints (feature knowledge, confirmed by API surface)
Product backlog (ordered by Stack Rank, drag & drop, "add at top/bottom"), portfolio backlogs, mapping pane (drag to parent),
parents/children expand, forecast by velocity, "In progress items" filter, column options.
Sprint: sprint backlog (requirements + child tasks), Taskboard (tasks by state, rows per requirement or per person),
Capacity (hours per member/activity, days off), sprint burndown (remaining work), velocity chart, CFD, lead/cycle time.

## Queries
My Queries / Shared Queries folders (permissions per folder). Query types: flat list, work items and direct links, tree of work items.
WIQL (SQL-like): macros @Me, @Today±n, @CurrentIteration, @Project, @Follows, @RecentMentions; operators =, <>, >, <, In, Not In,
Contains, Contains Words, Under, Was Ever, In Group, Is Empty, Changed Date etc.; column options, sorting, grouping via charts;
query-based charts (pie, bar, stacked, pivot, trend). Results: bulk edit, export to CSV, email.

## Security (namespaces relevant to Boards)
- Project: view, edit project info, delete/move/permanently delete work items, bypass rules, suppress notifications, change process, manage backlogs/plans.
- Area (CSS): read/edit/create child/delete nodes, **view/edit work items in this node**, edit comments.
- Iteration: read/write/create children/delete.
- Query folders: read, contribute, delete, manage permissions, full control.
- Tagging: enumerate, create, update, delete (project has ~40 tags).
- Plan (delivery plans): view, edit, delete, manage. Dashboards: read, create, edit, delete, manage permissions.
- Process: create/edit/delete process, administer process permissions. Boards: view/create/change metadata/move card/delete/manage.
- Identity: groups (Project Administrators, Contributors, Readers, Build Administrators, Project Valid Users), membership management.
- Access levels (Stakeholder / Basic) limit features.

## Other product capabilities (knowledge)
Work item form: header (ID, title, assigned, state, reason, area, iteration), tabs (Details, History, Links, Attachments),
Discussion (comments with @mentions, #work-item refs, reactions, edit/delete), Development/Related Work sections, Follow,
templates (prefill fields), copy work item, move to another project/change type, delete to Recycle Bin (restore / permanent delete).
Bulk edit, CSV import/export, Excel integration. Notifications (subscriptions: assigned to me, changes to items I follow, mentions).
Dashboards per team/project with widgets (query tile, chart for query, burndown, velocity, CFD, sprint overview, markdown, etc.).
Delivery Plans (timeline across teams by iteration), Analytics views / Power BI, Search across work items.
Integrations: Git commits/PRs linked (AB#123), service hooks, REST API, webhooks.

## How the analysed project actually uses Boards (aggregate only)
34 work items: Epic 2, Feature 10, User Story 22; all in state New; 32 of 34 have a parent (Epic→Feature→Story hierarchy);
Priority, Tags, Iteration Path on every item; Acceptance Criteria on every story. No tasks, bugs, estimates, capacity or custom
board columns yet; 3 iterations without dates; no areas beyond root; one team. → Used as a structured requirements backlog.
