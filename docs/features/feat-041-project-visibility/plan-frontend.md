# Frontend plan: feat-041-project-visibility

Base: `api-contract.md` (this feature), doc-016 `design.md` § 6.1, `spec.md` BR-TD-12..-16, story US-6. No new slice: every change
lives in `features/organizations` and `features/workitems`. Lists, selectors, search, counts (feat-061/062) are out of scope.

## 1. «Видим всей организации» (design § 6.1)

- `api/organizationsApi.ts`: `Project.visibleToOrganization?: boolean`; `setProjectVisibility` = `PUT /projects/{id}/visibility`
  with `{ visibleToOrganization }` (contract § 2.1).
- `hooks/mutations.ts`: `useSetProjectVisibility`, invalidating the project read and the project list.
- `permissions.ts`: `visibilityWrite = 'organizations.visibility.write'`, an object-scope code read from the project's `codes`
  (`useObjectAccess`), the same way `projectWrite` is on that page.
- `components/ProjectVisibilityControl.tsx`: `Section` «Видимость» with a MUI `Switch` «Видим всей организации» and the helper.
  Turning on opens `ConfirmDialog` (button «Сделать видимым»); turning off writes at once. Notices as in design § 6.1. A refusal
  is shown in the dialog (on) or as an error notice (off), by the server's text; 403 reads «Недостаточно прав».
- `pages/ProjectGeneralPage.tsx`: the control is mounted in «Параметры» (the settings section; the task text says «Настройки», the
  panel's and design's name is «Параметры») only when the project's `codes` hold the code; otherwise the section is absent.
- Copy: `projectTexts.visibility` in `lib/teamTexts.ts`, exactly design § 6.1. Nothing invented.

## 2. Assignee picker (contract § 3)

`AssigneeField` already passes the item's `projectId` to `GET /workitems/assignee-candidates` (mod-019 OQ-11). 409
`assignee_unavailable` already lands under «Исполнитель» by code (`workitems/lib/refusals.ts`) with the server's text «Этого
пользователя нельзя назначить исполнителем.». No code change; a component test is added.

## 3. «Не найден» answers

Pages choose their frame by error code (`notFoundLevel`: `organizations.project|team|area.not_found`), never by status, so a
hidden object (404 with the same code as a missing one) already shows the not-found frame and a 403 no longer arises for it.
No new screen. A test pins it for the project's page.

## 4. Tests

`pages/ProjectVisibility.test.tsx` (off by default, confirm-then-on, off without question, refusal in the dialog, absent without the
code, hidden project 404); `WorkItemFormPage.assignee.test.tsx` (409 on a newly set assignee); `paths.test.ts` (catalog pin gains
the code). The organizations test double serves `visibleToOrganization` and `PUT …/visibility`.
