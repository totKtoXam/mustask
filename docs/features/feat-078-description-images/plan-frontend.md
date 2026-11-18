# feat-078 — frontend plan (stage 6)

Scope: images in the feat-018 rich-text «Описание» editor (design.md) and the work item card's wiring.
No backend change; the contract (`api-contract.md`) is used as written.

## 1. Slices touched

| Place | What |
|---|---|
| `shared/lib/richText` | Client normaliser keeps the image element (BR-IM-7 placement rules, ported line for line from the server); `imageAlt.ts` (`storedAlt`: 255 code points, `<` `>` and NUL dropped, CR written as LF) |
| `shared/api/httpClient.ts` | Raw `Blob` body (`application/octet-stream`), `apiFetchBlob` |
| `features/workitems/api/workItemImagesApi.ts` | upload, reduce, read, and `itemImagesHost(org, project, identifier)` mapping `images.*` errors to `ImageRefusal` |
| `shared/ui/RichTextEditor/images/` | `host.ts` (the seam, BR-IM-19), `runtime.ts` (address cache, shared state), `nodes.ts` (image, placeholder, keys), `ImageViews.tsx`, `insert.ts` (insertion point, slots), `controller.ts` (upload queue, reduction queue), `dialogs.tsx` (reduction, full size, menu), `commands.ts` |
| `RichTextEditor` | `images` prop, `settle()` on the handle, toolbar control «Изображение» (hidden without host or edit right), paste / drop / file chooser, waiting helper «Дожидаемся загрузки изображений…» |
| `features/workitems` card | `DescriptionEditor` passes `itemImagesHost`; `useWorkItemEdits` awaits `settleDescription` before the flush |

## 2. Steps

1. Normaliser and alt rule (US-3 canonical cases, shared fixtures stay a fixed point).
2. API layer and host seam types.
3. Image node, placeholder, views, keyboard (US-1, US-4, design § Keyboard).
4. Controller: insert by button, paste, drop; upload placeholders with cancel; refusals (US-1).
5. Reduction dialog and queue (US-6); full-size view (US-4); image menu.
6. Card wiring and save waiting (BR-IM-14).
7. Tests with each step; ESLint and Prettier on touched files.

## 3. Tests (Vitest + Testing Library + MSW, each linked to a story criterion)

- `shared/lib/richText/images.test.ts`, `canonical.parity.test.ts`: US-3 canonical and hostile cases.
- `features/workitems/api/workItemImagesApi.test.ts`: US-1/US-2 error mapping, US-4 read.
- `RichTextEditor/images.test.tsx`: US-1 insert, paste, drop, refusals, host absent; US-4 states and read-only; US-6 dialog; keyboard.
- `paste.test.tsx`, `toolbar/model.test.ts`: host absent means silent ignore (D-8), control hidden.
- `useWorkItemEdits.test.tsx`: save waits for uploads.

## 4. Known limits

No E2E (standing answer NO). Timing and real-browser drag are covered by the prototype check, not jsdom.
