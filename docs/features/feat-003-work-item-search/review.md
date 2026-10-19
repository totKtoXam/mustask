# Review — feat-003-work-item-search

## Round 1 — requirements

Requirements reviewer, clean context, 2026-10-18. I did not write the spec. This is the M route's review round before
the UX design. gate-1 and gate-2 go to the human as one package.

**Read:**
- `spec.md` and US-1..US-3 (`git diff origin/main...feat/003-work-item-search`, 40b16959..56f3a485).
- doc-002: US-11 and § Fields (W-3).
- doc-010: D-14 and the feat-003 row.
- doc-012: US-11, OQ-1, NFR Volumes, Client load, Query shape, and the decomposition rows for feat-013 and mod-018.
- doc-013: BR-NE-8 and the feat-003 and feat-043 rows.
- mod-005: spec BR-DR-1..-18, § Surfaces and writes, OQ-2; design §§ 4 and 6; review-code H-1; ADR-0030 rule 2.
- mod-016: BR-M1-26 and inventory rows 30-37.
- mod-014: BR-UP-11, -14, -15, -17.
- mod-017: BR-OT-8, OQ-9; api-contract §§ 2.1 and 3; design §§ 5.2 and 6.5.
- mod-020's branch: its `useTeamFilter.ts` and `WorkItemsPage.tsx`, and its amendments to mod-017.
- feat-040's branch: spec BR-TS-8, OQ-2, § Owed by; design § 4.
- The queue lines for feat-016, feat-017, feat-043 and mod-020.

**Code read:**
- `WorkItemsPage.tsx`, `WorkItemFormPage.tsx`, `FilterBar.tsx`, `lib/assignee.ts`, `lib/deletion.ts`, `lib/teams.ts`,
  `workItemsApi.ts`.
- `WorkItemFilter.cs`, `WorkItemFilterPredicate.cs`, `WorkItemReadModel.GetPageAsync`, `GetWorkItems*`,
  `WorkItemEndpoints.cs`.
- `UserDirectoryReadModel.LookupAsync`, `UserDirectoryBatches`, `SeededProcess.cs`, `WorkItemText.cs`.

**Checked:**
- The quoted texts match `contracts/errors/*.json`.
- The seeded state order gives BR-WS-3's list of options.
- Every scenario's expected rows follow from § Common data.
- There are three stories, which fits an M.

### Rule → story map

| Rule | Stories |
|---|---|
| BR-WS-1 the band | US-1..US-3 |
| BR-WS-2 combination | US-1 (two states; with the team), US-2 (any value), US-3 (all four) |
| BR-WS-3 «Статус» options and match | US-1 |
| BR-WS-4 deleted items hidden unless «Удалена» | US-1, US-2 (options), US-3 (keyword) |
| BR-WS-5 a deleted row and its card | US-1 (bin, restore, roles) — see R-1 |
| BR-WS-6..-8 «Исполнитель» options, reading, match | US-2 |
| BR-WS-9 keyword | US-3 |
| BR-WS-10 a value that names nothing | US-1 («Готово»), US-2 (outline, the Zoya case after R-3) |
| BR-WS-11 the address | US-1, US-2, US-3 |
| BR-WS-12 count and paging | US-1 (120 items), counts throughout |
| BR-WS-13 empty results | US-1 |
| BR-WS-14 creating while filtered | US-1 |
| BR-WS-15 containment | US-2 (Oleg; the options' read after R-9), US-3 (OTH, WRH after R-8) |
| BR-WS-16 one specification | contract-level; no scenario is due |
| BR-WS-17 freshness | US-1 (another user's restore) |
| NFR speed, query shape | US-3 (volume scenario) |

**doc-002 US-11:**
- The tag clauses go to feat-016 (OQ-1).
- The area clause retires (doc-012 OQ-1 A).
- Every other clause maps to US-1..US-3.

### R-1
Severity: major
Affects: BR-WS-5, US-1 "Each role reaches the bin", BPMN node O3
Problem: The way out of the card page after a purge, and through its trail, is not specified.
- Today «Удалить навсегда» on the card page runs `navigate(workItemsPath(…), { replace: true })` (`WorkItemFormPage.tsx:142`; mod-005 design § 6). This opens the **unfiltered** list.
- So an administrator who empties the bin one item at a time is thrown off «Удалена» after every purge.
- US-1 says "after Ann purges MSK-FEAT-6, the list under «Удалена» reads MSK-US-5 alone". It does not say how Ann gets back to that list, so the scenario cannot be tested as written.
- The card page's trail «Рабочие элементы» has the same gap. It is a link without parameters, so under BR-WS-11 it drops the filters. Nothing says whether that is intended.
- BR-WS-5 settles only the browser's Back.

Required resolution: The analyst decides, in BR-WS-5 and in US-1:
- Where a purge on the card page lands when the card was opened from a filtered list. The obvious reading is the list under the same filters, on page 1, with the purged address still unreachable by Back.
- Whether the trail keeps the filters.

If mod-005's landing changes, say so: it is a change to mod-005 design § 6, noted in mod-005's status.md. Add `WorkItemFormPage.tsx` to § Lines in flight. If the answer is "keep mod-005's landing", reword US-1 to name how Ann gets back to «Удалена».
Status: resolved
Resolution: The analyst decided both; no open question.
- **The purge landing changes.** BR-WS-5 now reads: when the card was opened from a row of the list, a purge returns to that list's address, filters included, on page 1, with mod-005's notice. A card reached any other way (its address, a link, another card's «Родитель») lands on the list unfiltered, as mod-005 has it. Either way Back never meets the purged address. BR-WS-5 names this as an amendment of mod-005 design § 6. § Lines in flight says the frontend stage adds the amendment note there and a note line in mod-005's status.md, as mod-020 did for mod-017. It also adds `pages/WorkItemFormPage.tsx`, which neither mod-020 nor feat-040 touches.
- **The trail does not keep the filters.** It is the navigation's link, derived by the shell from the navigation model, and opens the list unfiltered. BR-WS-11 adds it to the links that do so, as BR-OT-8's mod-020 amendment reads for the navigation entry. BR-WS-5 names Back as the way back under the filters.
- **US-1.** "Each role reaches the bin" now stops at the actions. Three scenarios are added: "A purge on a card opened from the bin returns to the bin" (Ann lands on «Удалена», page 1, MSK-US-5 alone, count 1, the notice, Back does not show the purged address); "A purge on a card not opened from the list lands on the list unfiltered"; and "The card's trail opens the list unfiltered, and Back keeps the filter".
- BPMN node O3 now names the landing.
- In passing: mod-020 merged into main on 2026-10-18 (PR #25, 0bc5311a). § Context and § Lines in flight now say so, and R-11's conditional order becomes "development takes main in first".

### R-2
Severity: minor
Affects: BR-WS-10, BR-WS-11
Problem: BR-WS-11 said "A blank value names nothing", and BR-WS-10 says "A value that names nothing selects nothing". Read together, `?<state>=` empties the list. mod-020's `teamFilterOf` drops blank values instead, so the link shows the whole list. US-3's blank keyword also expects "no filter".
Required resolution: A blank value is left out of the filter.
Status: resolved
Resolution: BR-WS-11 now reads "A blank value is left out, as mod-020's `teamId` is: a parameter with blank values only sets no filter".

### R-3
Severity: minor
Affects: BR-WS-6, BR-WS-10, US-2
Problem: BR-WS-10 defined an assignee value that names nothing as "an id of nobody assigned to an item of the project". Zoya is assigned only to the deleted MSK-FEAT-6. With «Удалена» cleared she is no option (BR-WS-6), but she is "assigned to an item of the project". So the rule did not say whether the field names her or what the band shows.
Required resolution: Tie the definition to the options under the chosen states, and add a criterion for this case.
Status: resolved
Resolution:
- BR-WS-10 now reads "an id that is none of the options BR-WS-6 gives under the chosen states … a person assigned only deleted items while «Удалена» is not chosen", and "the field names only values among its options".
- US-2 gains "A person offered only under «Удалена» names nobody once «Удалена» is cleared".

### R-4
Severity: minor
Affects: NFR Query shape
Problem: The NFR said the options' read takes a number of statements that "does not grow with … assignees". mod-014 BR-UP-17 caps a lookup at 500 ids (`UserDirectoryLimits.IdsPerRead`, `UserDirectoryBatches`). With 2,000 people that is 4 lookups. As written, the statement-count test contradicts an approved contract.
Required resolution: Allow mod-014's batches.
Status: resolved
Resolution: The NFR now reads "the options' people are named by mod-014's lookups, one per 500 distinct ids (BR-UP-17), and nothing else grows with assignees".

### R-5
Severity: minor
Affects: NFR Option sizes, BR-WS-11
Problem: «Исполнитель» offers up to 2,000 people, and each chosen value is a repeated parameter of about 50 bytes. Kestrel's 8 KB request line (mod-017 api-contract § 3, "no cap is added") ends at about 150 people, and past it the read fails. The spec was silent on this bound.
Required resolution: State the bound.
Status: resolved
Resolution: NFR Option sizes now names the bound and keeps mod-017's precedent: no cap is added, and a choice past the bound fails as the list's read error does. It is an accepted risk.

### R-6
Severity: minor
Affects: BR-WS-9, US-3
Problem: Four keyword inputs had no stated answer:
- Digits past `int` («99999999999») can throw where BR-WS-9 (c) is parsed.
- A character no title can hold (a line break, or U+0000, which PostgreSQL text rejects) can turn into a server error.
- A keyword given twice in the address.
- "a longer keyword lists nothing" did not say whether the length is counted before or after trimming.
Required resolution: Say that these match nothing, are never an error, and that the first value counts.
Status: resolved
Resolution:
- BR-WS-9 gains both sentences.
- US-3's outline gains the row «99999999999».
- The 255-character scenario now reads "longer than 255 characters after trimming".

### R-7
Severity: minor
Affects: BR-WS-3, BR-WS-10
Problem: A state is filtered by its name in the address, but how a name is compared was not stated. Whether «новая» filters like «Новая» or selects nothing could not be tested.
Required resolution: Compare names as `StateNameRule` does.
Status: resolved
Resolution: BR-WS-3 now reads "A name in the address is compared as stored, letter case included (`StateNameRule`): «новая» names no state".

### R-8
Severity: minor
Affects: § Common data, US-3, US-1
Problem: BR-WS-9 and BR-WS-15 say another project's identifier matches nothing. The only example was another **organization's** item (OTH-US-1). Another project of the **same** organization is where the risk lies, because there an identifier lookup by key would succeed. Separately, US-1's project «Пустой» named no organization.
Required resolution: Add a second project of «Альфа» to the data and a keyword example for it.
Status: resolved
Resolution:
- § Common data gains «Склад» (WRH) with WRH-US-1 «Оплата картой», unassigned. No existing expectation changes, because every list is «Платформа»'s.
- US-3's outline gains «WRH-US-1».
- US-1 now reads "«Пустой» (key PST) of «Альфа»".

### R-9
Severity: minor
Affects: BR-WS-15, US-2
Problem: The «Исполнитель» options are a new read, and no criterion covered its containment. Non-disclosure covers filter values, not the route. The panel never shows the options to a non-member, so a test written from the stories would miss it. mod-016's inventory test only forces a code onto the route.
Required resolution: Add a criterion.
Status: resolved
Resolution: US-2 gains "The options' read is contained like the list's": a non-member gets «Организация не найдена.», and a foreign or deactivated project gets «Проект не найден.».

### R-10
Severity: minor
Affects: § Owed by later lines (feat-043, feat-016), § Decomposition
Problem: doc-013 BR-NE-8 and feat-043's queue note list "the tag" among what feat-043 carries. Under OQ-1 A the tag filter is feat-016's. feat-016 comes after feat-013, so feat-043 will almost certainly merge first. Nothing then owes the tag filter on feat-043's lists.
Required resolution: Move that duty to feat-016, and amend feat-043's clause.
Status: resolved
Resolution:
- The feat-043 row now says "(no tag filter: feat-016's, OQ-1)".
- The feat-016 row adds "once feat-043 has merged, on its cross-project list and the project's section".
- § Decomposition gains a queue amendment for feat-043's BR-NE-8 clause.

### R-11
Severity: minor
Affects: § Decomposition, Lines in flight
Problem: "Development starts on a main that holds mod-020" was a hard order with no dep in the queue line, so it cannot be enforced. feat-040 handles the same overlap with a merge-order rule (BR-TS-8).
Required resolution: Use the merge-order rule.
Status: resolved
Resolution: The paragraph now reads "mod-020 is expected to merge before development starts; if it has not, whichever of the two merges second follows the other's address form (as feat-040 BR-TS-8 reads)".

### R-12
Severity: minor
Affects: OQ-2, § Decomposition
Problem: OQ-2 A did not give the human its cost, and the spec did not name the rules it changes.
- Until feat-017, the card's «Исполнитель» field reads «Иван Котов» while the row reads «Иван Котов (не в организации)».
- The list row's assignee has no active flag (`WorkItemAssigneeView` carries `isMember` only), so mod-003 api-contract § 1.2 changes.
- The column leaves mod-003 D-2's cut and mod-016 BR-M1-26's "the marks are feat-017's".
Required resolution: State the cost in OQ-2 A, and name the changed rules with a note in mod-003's status.md.
Status: resolved
Resolution: OQ-2 A now states the card's interim wording. Lines in flight now names § 1.2, D-2 and BR-M1-26, with the note.

### R-13
Severity: minor
Affects: BR-WS-13, BR-WS-14
Problem: Two problems:
- **Wrong citations.** The list's filtered-empty frame is mod-017 design § 5.2 (the spec cited § 6.3, the views), and the filtered-creation notice is § 6.5 (the spec cited § 4).
- **Duplicated keyword logic.** For the keyword, BR-WS-14 makes the panel repeat the server's matching. Nothing required the two to agree on letter case or «ё».
Required resolution: Fix the citations, and require the notice to use the same meanings.
Status: resolved
Resolution: The citations are fixed. BR-WS-14 adds "each with this spec's meaning (the keyword's letter case and «ё» included, BR-WS-9), so the notice never contradicts the list's next read".

### R-14
Severity: minor
Affects: BR-WS-6
Problem: Some options can be impossible to tell apart, and the order among unknown users was not stated.
- BR-WS-7 makes two unknown users two «Неизвестный пользователь» options.
- BR-UP-15 allows equal display names.
- Both give options that read the same, with the same accessible name.
Required resolution: Order unknown users by id, and leave telling the options apart to the design.
Status: resolved
Resolution: BR-WS-6 now reads "users the directory does not know last, by id … Two options that read alike (equal display names, two unknown users) are told apart as the design decides".

### R-15
Severity: minor
Affects: US-2, US-3, BR-WS-11
Problem: Four wordings could not be tested as written:
- **US-3, «Очистить фильтры».** The scenario said it "clears all four at once", but by then only the keyword was set.
- **US-2, "no longer knows Petr".** mod-014 BR-UP-11 never forgets a user: deleted users read as inactive, and "unknown" means never pushed (BR-UP-17).
- **US-2, the mark change.** The scenario re-read only the list, but the option's mark comes from the options' read.
- **BR-WS-11, the switcher.** It did not say whether a change of project in the switcher carries the filters.
Required resolution: Reword each.
Status: resolved
Resolution:
- US-3 now reads "with all four set again, «Очистить фильтры» clears them at once".
- US-2 now reads "does not know Petr's id (mod-014 BR-UP-17)".
- US-2 now reads "Mia's list and its options read again".
- BR-WS-11 adds "the switcher's change of project" to the links that open the list unfiltered, as mod-020 drops `teamId` when the user leaves the page.

### Round 1 verdict

**changes-requested.** R-1 (major) is left for the analyst. R-2..R-15 (minor) are fixed in place in `spec.md` and US-1..US-3.

OQ-1..OQ-3 stand as written, each with its recommended A, for the human at gate-1. Nothing above needs a new open question unless the analyst puts R-1 to the human.

No code under `src/` was touched. E2E was not run (the standing NO).
