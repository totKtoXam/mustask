# QA — mod-014-keycloak-user-projection

## Backend

QA pass 1, 2026-10-02, qa-engineer (clean context). The test plan below
(coverage matrix and additional scenarios) was written from `spec.md`, the
six stories and `api-contract.md` only, and committed before any
implementation, test code or `plan-backend.md` was read. The Status column
and everything after "Additional scenarios" were filled in afterwards.

Legend for Check: **I** — integration test against PostgreSQL (Testcontainers);
**U** — unit test; **P** — provider test (Java, `deploy/keycloak`); **G** —
guard / architecture test; **L** — the opt-in live tests over the built
Keycloak image; **S** — a manual check on the live stand (consented, D-13);
**R** — review of sources or configuration.

Legend for Status (filled after the plan): ☑ covered and green; ◐ partly
covered, the gap named; ☒ fails — a defect below. Test names are given
without their class where the class is the one named in the row. **QA** marks
a test added in this pass (`UserDirectoryQaTests`). **S** results come from
the stand runs listed under "Live stand" below; times are measured from the
Keycloak admin call's answer to the row in `directory_users`.

### Coverage matrix

| Criterion (spec) | Check | Test type | Status |
|-----------------|----------|-----------|--------|
| US-1 / search by part of a name finds an active user; exactly four values | I: «Анна Смирнова» found by "смир"; G/R: `DirectoryUser` has exactly Id, DisplayName, Email, IsActive | integration, review | ☑ I `UserDirectoryReadModelTests.Search_PartOfAName_FindsTheActiveUserWithFourValues`; `DirectoryUserShapeTests` |
| US-1 / e-mail part ignores letter case | I: "ANNA.S@" finds her | integration | ☑ I `Search_PartOfAnEmailInUpperCase_FindsTheUser` |
| US-1 / username not searched | I: "a.smirn" answers nobody | integration | ☑ I `Search_PartOfTheUsername_FindsNobody` |
| US-1 / ё equals е, both directions | I: "семен" → «Семён Орлов»; "Семён" → «Семен Лебедев» | integration | ☑ I `Search_YoAndYe_AreTheSameLetter`; U `DirectorySearchTextTests` |
| US-1 / text under 2 characters answers nobody | I: " а " → `([], false)`; also "", "   " | integration | ☑ I `Search_FewerThanTwoCharacters_AnswersNobody` (3 cases); U |
| US-1 / text over 100 refused, exactly 100 accepted | I/U: 101 after trimming → `ValidationException` (`text`, `value_too_long`, `limit` 100); 100 → answered | integration | ☑ I `Search_OverOneHundredCharacters_IsRefused_AndOneHundredIsAccepted`; U |
| US-1 / inactive excluded by default, included on request | I: «Иван Петров» / disabled «Иван Сидоров» | integration | ☑ I `Search_InactiveUser_OnlyWhenAskedFor` |
| US-1 / capped at 20, more matched | I: 35 users → first 20 in order, `MoreMatched` | integration | ☑ I `Search_ThirtyFiveMatches_…`; QA `Search_AtTheEdgeOfTheAnswerSize_…` (20 → false, 21 → true) |
| US-1 / order: Cyrillic before Latin, case and ё ignored | I: the six names of the story in the stated order | integration | ☑ I `Search_OrdersCyrillicBeforeLatin_IgnoringCaseAndYo`; U `DirectoryCollationTests` (class order, byte order of the stored keys) |
| US-1 / equal names ordered by id, stable | I: 25 identical names, two searches equal, ids ordinal | integration | ☑ I `Search_TwentyFiveNamesakes_…`; U `DirectoryUserOrderTests` |
| US-1 / restricted to given ids; repeated and never-held ids | I | integration | ☑ I `Search_RestrictedToIds_…` |
| US-1 / empty id restriction answers nobody | I | integration | ☑ I `Search_EmptyIdRestriction_AnswersNobody` |
| US-1 / restriction of 501 refused, 500 answered | I: `amongIds`, `too_many_ids`, `limit` 500 | integration | ☑ I `Search_RestrictedToFiveHundredAndOneIds_…`; QA with repeats |
| US-1 / no first or last name → username, empty e-mail | I: "qa.fix" → "qa.fixture", `""` | integration | ☑ I `Search_UserWithNoNames_…`; S: a user created with no names was projected under its username |
| US-1 / lookup: active, disabled, deleted (name kept), unknown | I | integration | ☑ I `Lookup_ActiveDisabledDeletedAndUnknown` |
| US-1 / lookup: repeated id once, empty list empty | I | integration | ☑ I `Lookup_RepeatedIdOnce_AndEmptyListAnswersNothing` |
| US-1 / lookup of 501 refused, 500 answered | I: `ids`, `too_many_ids` | integration | ☑ I `Lookup_FiveHundredAndOneIds_…` |
| US-1 / reads work while Keycloak is unreachable | I: the read tests run with no Keycloak at all; R: the read path has no HTTP dependency | integration, review | ☑ I `Reads_KeycloakUnreachable_AnswerFromTheProjection`; G `UserDirectoryBoundaryRules` |
| US-1 / reads within 500 ms at 10 000 users | I: timed search (3 characters) and 500-id lookup over 10 000 rows | integration (perf) | ☑ I `UserDirectoryVolumeTests.SearchAndLookup_AtTenThousandUsers_…` |
| US-2 / created in the console becomes findable within 10 s; id = sub | S: create through Keycloak's admin surface (a test tool, not shipping code), poll the projection; id equals the Keycloak user id; L if covered | stand, live | ☑ S: projected 0.58 s after the create; the `sub` of her account token (ADR-0023 impersonation) = Keycloak id = projected id; L `ACreatedUser_IsPushedSignedWithItsFields` |
| US-2 / change of name followed | S + L | stand, live | ☑ S 0.55 s; L `NameEmailAndUsernameChanges_ArePushed`; I |
| US-2 / change of e-mail followed | S + L | stand, live | ☑ S 0.52 s; L; I |
| US-2 / username rename moves the fallback display name | push level (I) and the live tests' throwaway realm (L), per D-15 | integration, live | ☑ I `Push_UsernameRename_MovesTheFallbackDisplayName`; L. S: the admin rename is refused by Keycloak (400 `error-user-attribute-read-only`, `editUsernameAllowed` off) and the projection is unchanged — as D-15 decides |
| US-2 / a user's own profile change carried | P: the user-event (profile update) path; S if an impersonated account session can be obtained | provider, stand | ☑ S: `e2e-fixture` changed its own last name through Keycloak's account REST with an impersonated session (no password typed) → projected 0.46 s, then restored; P `theUserEventsThatChangeAProjectedField_queueTheState` |
| US-2 / password, group, role changes leave the projection as is | S: row values and moment unchanged after each; P: those events produce no push | stand, provider | ☑ S: password reset, group join, realm role, `mustask-api` client role → row identical (values, `moment`, `changed_at`) 12 s later; P; L `ARoleMapping_PushesNothing` |
| US-2 / created already disabled is inactive | S + L | stand, live | ☑ S inactive 0.41 s after the create; L; I |
| US-2 / service-account user never projected | S: no projected user named `service-account-*` (the `mustask-e2e` one); P | stand, provider | ☑ S: `service-account-mustask-e2e` exists, no row; P; L `TheServiceAccountUser_IsNeverPushed` |
| US-2 / another realm's user never projected | S: a user created in `master` is not projected; P | stand, provider | ☑ S: «Пётр Мастеров» in `master`, no row after 12 s (then deleted); P; L `AUserOfTheMasterRealm_IsNeverPushed` |
| US-3 / disable → inactive, nameable | S + L | stand, live | ☑ S inactive 0.84 s, name and e-mail kept; L; I |
| US-3 / enable again → active, same id | S + L | stand, live | ☑ S active 0.71 s, same id; L; I |
| US-3 / delete → inactive for good, name and e-mail kept, row kept | S + L | stand, live | ☑ S `Deleted` 0.97 s, name and e-mail kept; L `ADeletion_IsPushedCarryingTheLastState`; I |
| US-3 / reported deletion is final (also across a complete reconciliation) | I: through the push channel | integration | ☑ I `ReconciliationTests.Deletion_SurvivesALaterPushAndACompleteReconciliationThatCarriesTheUser`, `Push_EnabledAfterDeletion_IsHeldAsDeleted`; U |
| US-3 / new user with a deleted user's username and e-mail is another user | S: recreate, search answers exactly one active, old id still inactive | stand | ☑ S: new id projected 0.28 s, active; old id `Deleted`; two rows with that e-mail, one active; I |
| US-3 / temporary brute-force lockout is not inactivity | S: five wrong passwords at the login form, lookup still active | stand | ☑ S: six wrong passwords at the realm's token endpoint (never the real one) → attack detection `disabled: true`, account enabled, row active `Present`; P (login events ignored) |
| US-3 / the status authorizes nothing | R/G: no authentication or authorization code reads the projection | review | ☑ I `UserDirectoryWriteSurfaceTests.AnInactiveStatus_NeitherRefusesNorAdmits`; G `UserDirectoryBoundaryRules` |
| US-4 / backfill of existing users into an empty projection (200, 5 disabled, SA excluded) | L if covered; S: empty the projection, trigger a reconciliation | live, stand | ☑ S: 200 users (5 disabled) created while the listener was off, listener enabled → all 200 projected, 5 inactive, in 10.7 s; I `Reconcile_IntoAnEmptyProjection_…`; L `TheStartReconciliation_SendsAWholeSnapshot` |
| US-4 / stand comes up with seed and fixture users projected | S: after start, migration and seed | stand | ☑ S run 4 (after the reset, migration, seed): `e2e-fixture`, `mustask`, `member` projected active within 12 s of the seed |
| US-4 / second start keeps ids; reconciliation logs 0 added/changed/absent/deleted | S: stop, start again, compare ids, read the provider's reconciliation log line | stand | ☑ S run 1, on the store and database another worktree's stand had left: "received 3, added 0, changed 0, marked absent 0, deleted 0"; run 4 the same over run 3's store; one row per user |
| US-4 / realm drift fails the start naming the role and the reset; reset rebuilds; other DB untouched | S: delete a declared `mustask-api` client role, restart, observe; run the reset on this task's database only; check `mustask` untouched | stand | ☑ S run 2: `keycloak` Unhealthy, provisioning not run, AppHost logs "Client role `mustask-api/dictionaries.languages.write` is in the realm import but not in the live realm; reset the stand: scripts/stand-reset.sh …"; the reset dropped and recreated only `mustask_mod014` (oid and table count of the ten other databases unchanged) and removed the store volume; run 4 came up ready. The check's repetition was D-3, fixed in re-check 1: RC-10 (a start drift logged once, cleared without a reset), RC-8 |
| US-4 / two checkouts take turns on one external DB | S/R: no container left behind after stop; the store is keyed to the database | stand, review | ◐ S: run 1 took over the store and database the developer's worktree left — same ids, 0 added, 0 absent; no container left after a graceful stop. The third-checkout leg (own container database) was not run; C `StandStoreKeyTests` covers the keying |
| US-4 / backfill reads Keycloak in-process only | G: ADR-0023 guards + the provider's read-only guard green | guard | ☑ G (ADR-0023 guards in `verify.sh`); P `ReadOnlyGuardTest` |
| US-4 / lost deletion caught by the next reconciliation | L/S | live, stand | ☑ S: api stopped, user deleted, Keycloak restarted, api started → `Absent`, inactive, name kept, 12.4 s after the api start; I |
| US-4 / lost change caught by the next reconciliation | L/S | live, stand | ☑ S: same sequence, «Иван Петров-Водкин» 12.5 s after the api start; I |
| US-4 / user created during a reconciliation stays active | I: B-2, then B-1 creation newer than the start, then B-4 without her | integration | ☑ I `Reconcile_UserPushedAfterTheEnumerationStart_IsNotMarkedAbsent` |
| US-4 / absence yields to a newer push, not to an older one | I | integration | ☑ I `Absence_YieldsToANewerPushButNotToAnOlderOne`; U |
| US-4 / reconciliation repeats on its period; default 60 | P: default; L: short period, ≥ 2 completions logged | provider, live | ☑ S run 4 (period 2): the start run and the periodic run both logged with their counts within 90 s; the configured line reads `reconcilePeriod=PT1H` without the setting (run 1); P `start_schedulesTheStartRunAndThePeriod`, `FeedOptionsTest`. Retries: D-2, fixed in re-check 1 (RC-6, RC-7); D-4 |
| US-4 / interrupted snapshot marks nobody absent | I: pages applied, no completion or a mismatched one → nobody absent | integration | ☑ I `InterruptedReconciliation_PagesWithoutCompletion_MarksNobody`, `Complete_CountsNotMatchingWhatArrived_…`; P `aFailedPage_sendsNoCompletion` |
| US-4 / reads keep answering during a 10 000-user reconciliation | I (perf) | integration | ☑ I `SearchAndLookup_DuringATenThousandUserReconciliation_…` |
| US-4 / repeated reconciliation over an unchanged realm changes nothing | I: counts 0/0/0/0; S: the second start's log line | integration, stand | ☑ I `Reconcile_AnUnchangedRealmAgain_…`; S (row above) |
| US-4 / provider tests gate verify.sh | R: verify.sh classification; a provider-only change runs the provider bucket | review | ☑ R: `verify.sh --list` puts `deploy/keycloak/providers/*`, the Dockerfile and `.dockerignore` in `provider`; the bucket fails when `docker build --target provider` (`mvn verify`) fails; it ran in this pass |
| US-5 / Keycloak does not wait for a stopped MUSTASK (timing) | L | live | ☑ S, medians of 5 create / disable / delete: listener off 18 / 19 / 43 ms; api stopped 13 / 13 / 32 ms; both up 23 / 22 / 56 ms. Failed deliveries logged at WARN naming ids only; L `ASlowOrRefusingMustask_…` |
| US-5 / Keycloak does not wait for a slow MUSTASK (30 s stand-in) | L | live | ☑ L `ASlowOrRefusingMustask_NeitherDelaysKeycloakNorLosesTheChange`; P `aSlowMustask_timesOutOnTheSendersThread_andKeepsTheBatch` |
| US-5 / catch-up after MUSTASK returns (A created, B disabled, C renamed) | L/S | live, stand | ☑ re-check 1 (D-1 fixed): RC-2 ~1 s, and after a `keycloak` resource restart RC-1 0.25 s and 51 s after `/health` 200. Pass 1: ☒ D-1 — with Keycloak running since the stand's start, A, B and C caught up 12.2–12.5 s after the api start (also three more api restarts, 11–16 s); after the `keycloak` resource was restarted, the next api restart left the provider unable to reach the api |
| US-5 / statuses as last received while the feed is interrupted | L/S | live, stand | ☑ S: «Иван Петров» disabled while the api was stopped → row still active until the api returned; I `Statuses_StayAsLastReceived_…` |
| US-5 / same push twice → one record, second unchanged | I | integration | ☑ I `Push_SameTwice_SecondIsUnchangedAndOneRecordIsHeld` |
| US-5 / older after newer ignored | I | integration | ☑ I `Push_OlderAfterNewer_IsIgnored`; U |
| US-5 / older "enabled" after newer "disabled" stays inactive | I | integration | ☑ I `Push_OlderEnabledAfterNewerDisabled_StaysInactive`; U |
| US-5 / replayed captured push cannot undo a later change | I: within the window harmless; outside it 401 | integration | ☑ I `Push_ReplayedCapturedRequest_CannotUndoALaterChange`; `Push_TimestampOutsideTheWindow_IsRefused` |
| US-5 / concurrent T1/T2 pushes, 50 times → newest | I: concurrent requests | integration | ☑ I `PushConcurrencyTests.ConcurrentPushes_FiftyTimes_TheNewestStateWins`; QA page and change at once, 20 times |
| US-5 / concurrent deletion and "enabled", 50 times → deleted | I | integration | ☑ I `ConcurrentDeletionAndEnabled_FiftyTimes_TheUserStaysDeleted` |
| US-5 / a disabled provider freezes the projection, not the reads | S: listener removed from the realm, change not projected, reads answer; re-enabled → catch-up | stand | ☑ S: listener removed → 200 creations, a rename and five create/disable/delete cycles not projected; re-enabled → caught up in 10.7 s; P `ListenerEnablementWatchTest`. What the stand logged meanwhile was D-3; re-check 1 RC-9: stays Healthy, nothing logged, caught up 16.5 s after re-enabling |
| US-6 / no credential → refused, unchanged, logged without name/e-mail | I | integration | ☑ I `Push_WithoutCredential_IsRefused_AndTheRefusalLogNamesNoPerson`; S: 401, the api logs "User directory push refused (MissingHeaders) at POST /api/user-directory/feed/changes." and no name, e-mail or username |
| US-6 / wrong credential → refused | I | integration | ☑ I; S 401 (`WrongSignature`) |
| US-6 / Keycloak access token is not a credential (ordinary and privileged) | I: Bearer token in place of the signature → 401; S with a real token | integration, stand | ☑ I `Push_WithAKeycloakAccessToken_IsRefused`; S: real tokens of the `mustask-e2e` service account, of `member` and of `mustask` → 401 on B-1 and B-2, projection unchanged |
| US-6 / no MUSTASK user can write the projection | I/R: the only routes added are the feed; Bearer callers get 401; feed not in OpenAPI | integration, review | ☑ I `UserDirectoryWriteSurfaceTests.EveryRoute_WithAUsersToken_…`, `EndpointCategoryInventoryTests`, `PushRoutes_AreNotInTheOpenApiDocument` |
| US-6 / a sign-in does not fill the projection | I/R: an authenticated call for an unprojected id leaves lookup "unknown"; no code writes from claims | integration, review | ☑ I `ATokenForAnUnpushedUser_LeavesItUnknown`; G |
| US-6 / provider with the wrong credential fails loudly and recovers | L | live | ☑ P `aRefusedCredential_isLoggedAtErrorAndRetried_neverPrintingTheSecret`; L. Not run on the stand: the stand hands one generated secret to both sides |
| US-6 / credential only from environment or user secrets | R: repository searched for the stand's live secret value and for literals | review, stand | ☑ G `UserProjectionPushSecretPlacementRules`; P `FeedOptionsTest`; S: the live value occurs in none of the worktree's 824 files (node_modules, .git, bin, obj aside) |
| US-6 / credential never printed | S: AppHost output, Keycloak container log and Api log searched for the live value (count only, value never printed) | stand | ☑ S runs 1 and 4: 0 occurrences in the AppHost output, the api log, the Keycloak container log and the `keycloak` resource log; I `UserDirectoryLoggingTests` |
| US-6 / a fresh clone needs no new secret | R/S: the stand starts on the three listed secrets, pushes accepted | stand | ☑ S: the stand started on the three secrets (plus the external-database string), generated a 43-character push secret, pushes accepted |
| US-6 / no client and no service account added | G: ADR-0023 guards | guard | ☑ G (unchanged, green) |
| US-6 / the provider's read-only guard passes | P | provider | ☑ P `ReadOnlyGuardTest` |
| US-6 / the read-only guard catches a write and a credential read | P: not-vacuous fixtures; a scratch mutation if fixtures are absent | provider | ☑ P `ReadOnlyGuardIsNotVacuousTest` over the fixtures `WritesAUser`, `ReadsCredentials`, `EnumeratesRealms`, `LooksUpAnotherRealm`, `BindsARealmElsewhere` |
| NFR Freshness / 10 s with both sides up | S: measured on the stand | stand | ☑ S: 0.28–0.97 s for every kind of change |
| NFR Logging / refusal, failed delivery, completed reconciliation with counts; id only | I (log capture) + S | integration, stand | ☑ I `UserDirectoryLoggingTests`; S: the api's refusal lines, the provider's failed-delivery WARN lines naming ids, "reconciliation … completed — MUSTASK received …" lines |
| NFR Stand / no fixed-name container outlives the AppHost | S: after stop | stand | ☑ S: after `aspire stop` (runs 2, 3, 4) `mustask-e2e-keycloak-5010` was gone within 2 s. A force-killed AppHost (run 1) leaves it, as it leaves any DCP container |
| NFR Stand / stand-reset touches only its pair (external DB: only the named database) | R + S | review, stand | ☑ S (drift row above); without `--yes` and without a terminal the reset refused, exit 1 |
| Migrations / clean database, repeat, rollback and re-apply | S/I: `dotnet ef database update` on an empty database, again, down to the previous migration, up again | migration | ☑ S on the reset (empty) `mustask_mod014`: update (2 applied), again (none), to `InitialCreate` (reverted), update (applied) — exit 0 each; I `MigrationsTests` |

### Additional scenarios (beyond the spec)

Planned from the contract before the code was read; the Status column was
filled afterwards.

| # | Scenario | Expectation | Status |
|---|----------|----------|--------|
| A-1 | Search text containing LIKE wildcards: `%%`, `__`, `a_`, `\` | matched literally; `%%` does not answer everyone | ☑ I `Search_LikeWildcardsInTheText_AreLiteral`; U `Parse_EscapesLikeWildcards` (`\` included) |
| A-2 | Search text of 2 code points made of one surrogate pair plus a letter; 1 emoji alone | emoji alone (1 code point) answers nobody; length counted in code points | ☑ U `Parse_CountsCodePointsNotUtf16Units`; R: the length is counted in runes |
| A-3 | 100 emoji (200 UTF-16 units) | accepted (100 code points) | ☑ U `Parse_CountsCodePointsNotUtf16Units` |
| A-4 | Decomposed `е` + U+0308 in the text | NFC → `ё` → `е`: matches «Семён» | ☑ U `Normalize_FoldsYoAndCase` |
| A-5 | Control characters inside the text | removed before matching | ☑ U `Normalize_RemovesControlCharacters`, `Parse_FewerThanTwoCharacters_IsTooShort` |
| A-6 | `amongIds` of 250 distinct ids each twice + 1 (501 entries) | refused: repeats count toward 500 | ☑ QA `Search_RestrictionOfRepeatedIds_CountsEveryEntryTowardTheLimit` |
| A-7 | Lookup ids differing only in letter case | distinct; ordinal | ☑ I `Lookup_IdsAreOpaqueAndCaseSensitive` |
| A-8 | Lookup of `""` and of a 10 000-character id | unknown, not an error | ☑ I `Lookup_IdsAreOpaqueAndCaseSensitive` (`""`, 300 characters) |
| A-9 | `SearchAsync(null)`, `Text = null`, `LookupAsync(null)` | `ArgumentNullException` | ☑ I `Reads_NullArguments_AreProgrammingErrors` |
| A-10 | Exactly 20 matches / 21 matches | `MoreMatched` false / true | ☑ QA `Search_AtTheEdgeOfTheAnswerSize_SaysMoreMatchedOnlyAboveTwenty` |
| A-11 | `UserSearchResult.Merge`: overlapping batches, a batch with `MoreMatched`, > 20 total | de-duplicated, ordered by the comparer, first 20, flags as contract | ☑ U `UserSearchResultMergeTests` |
| A-12 | `DirectoryUserOrder.Comparer` agrees with the order the database returns | same order for punctuation, digits, Cyrillic, Latin, ё, case | ☑ U `SortKey_BytewiseOrderIsTheSameOrder`, `Compare_RanksClassesInTheContractsOrder`; I `Search_ThirtyFiveMatches_…` compares the SQL order with the comparer |
| A-13 | Signature: the worked example of B.2 verifies; shared vectors read by both sides | match | ☑ `PushSignatureVectorsTests`; P `PushSignerTest` |
| A-14 | Timestamp exactly 300 s old / 301 s old / 300 s ahead / 301 s ahead | accepted / 401 / accepted / 401 | ☑ re-check 1: QA `PushTimestampWindowTests` on a fixed clock (±300 accepted, ±301 stale). Pass 1: I ±290 accepted, ±301 refused |
| A-15 | Signature in upper-case hex, without `v1=`, wrong length; timestamp not an integer | 401, no detail of which check failed | ☑ I `Push_UppercaseHexSignature_IsRefused`, `Push_MalformedSignatureHeader_IsRefused`; QA `Push_TimestampNotADecimalInteger_IsRefused` (5 forms) |
| A-16 | `PreviousSecret` signs a push | accepted; a third secret refused | ☑ I `Push_SignedWithThePreviousSecret_IsAcceptedWhileItIsConfigured` |
| A-17 | Secret missing or under 32 characters at startup | the Api refuses to start | ☑ I `MissingPushSecret_…`, `ShortPushSecret_…` |
| A-18 | Body over 4 MiB (signed); malformed JSON (signed); unknown properties | 400 `push_malformed`; 400 `push_malformed`; ignored | ☑ I `Push_BodyOverFourMebibytes_IsMalformed`, `Push_BodyNotOfTheDeclaredShape_IsMalformed`, `Push_UnknownPropertiesAreIgnored` |
| A-19 | Entry validation: id ""/256, username missing, moment 0/negative/> 1 h ahead, e-mail 256, `enabled` missing without `deleted`, 0 and 501 entries | 400 with JSON-path field keys and the codes of Part C | ◐ I `Push_InvalidEntries_AreRefusedWithFieldPaths_AndNothingChanges` (id missing, moment 0 and > 1 h, e-mail 256, `enabled` missing), `Push_EmptyOrMissingList_IsRefused`, `Push_FiveHundredAndOneEntries_…`; re-check 1: QA `Push_EntryFieldsOutsideTheirLimits_…` (id and username of 256 or empty, username missing, names of 256, moment −1) and `Push_EntryFieldsAtTheirLimits_…` (255 accepted) |
| A-20 | `deleted: true` in a snapshot page; an id twice in one page | 400 `value_invalid` | ☑ I `Page_WithADeletionOrARepeatedId_IsRefusedWithFieldPaths` |
| A-21 | B-2: same id same start (idempotent), same id other start, new id not newer, newer id supersedes open older | 200 / 409 / 409 / older `superseded` | ☑ I `BeginSnapshotEndpointTests` |
| A-22 | B-3: unknown snapshot, completed, superseded, same page twice, negative page, non-integer page | 404 / 409 / 409 / stored counts not re-applied / 400 `pageNumber` / 404 | ☑ I `ApplySnapshotPageEndpointTests` |
| A-23 | B-4: counts mismatch, extra page beyond `pageCount`, repeated completion, superseded, unknown, `0, 0` | 409 incomplete (`expected`, `received`), stays open / 409 / stored counts / 409 / 404 / everyone older absent | ☑ I `CompleteSnapshotEndpointTests`; U `EnsureCompletable_SomethingMissing_IsIncomplete` |
| A-24 | Absence marking excludes deleted, already absent, stamped, newer-than-start users | only the rest marked; `markedAbsent` counts newly marked | ☑ I/U (`MarkAbsent_*`); QA `CompleteSnapshotWithoutAnyHeldUser_AtTenThousand_MarksEveryoneAbsent` (10 000 marked in one completion) |
| A-25 | Deletion entry for an id never held; deletion older than the held state | added as deleted with the carried state; deleted keeping the held name | ☑ I `Push_ManyEntries_CountsEachOnce`; U `Apply_OlderDeletionAfterNewerState_StillDeletesAndKeepsTheNewerName`; S (deletions queued during an outage arrived as `Deleted` rows) |
| A-26 | Newer moment, same values | `unchanged`, moment advanced (an older push after it is ignored) | ☑ I `Page_EntryWithUnchangedValues_CountsUnchangedAndStoresTheNewerMoment`; U |
| A-27 | One B-1 request carrying the same id twice, older second | newest wins | ☑ I `Push_ManyEntries_CountsEachOnce` |
| A-28 | Counts invariant added + changed + unchanged + heldAsDeleted = received | holds in every response | ☑ every counts assertion in I |
| A-29 | Feed routes with a valid Bearer token and no signature; with both | 401 / accepted only on the signature | ☑ I Bearer only, Bearer beside a wrong signature; QA `Push_ValidSignatureBesideABearerToken_IsAcceptedOnTheSignature` |
| A-30 | Feed routes absent from the OpenAPI document; reachable before the realm is ready | absent; not gated | ☑ I `PushRoutes_AreNotInTheOpenApiDocument`; R: `.Public` routes carry `IAllowAnonymous`, which the realm-readiness gate lets through |
| A-31 | Concurrent B-1 and B-3 for the same id; two concurrent B-2 | newest wins; one supersedes the other, no deadlock | ☑ QA `PageAndChangeForOneUser_AtTheSameTime_TheNewestWins`, `TwoSnapshotsBegunAtTheSameTime_LeaveOnlyTheNewerOpen` (20 times each) |
| A-32 | Error texts for every code in `contracts/errors/user_directory.json` (ru, kk) and `docs/error-codes.md` | present, registry test green | ☑ `ErrorCodesReferenceTests` and the error-catalog tests, green |
| A-33 | A refused push's log line | reason and route; no body, name, e-mail, secret | ☑ I; S (five refusal kinds, names absent) |
| A-34 | The stand reset with an overridden connection string (this task's database) | drops only the database the effective string names — never `mustask` | ☑ S |
| A-35 | Provider: reconciliation runs while the listener is removed from the realm (US-5 last scenario vs. the periodic reconciliation) | behaviour stated by the ADR and observed | ☑ S: nothing pushed and no reconciliation while removed; one reconciliation on re-enable; P `whileTheListenerIsDisabled_nothingIsSent` |

### Live stand

External-database mode on this task's own database `mustask_mod014` on the
local server (port 5432), default launch profile, the lock
`.git/stand.lock` held from 2026-10-02T15:37Z to 2026-10-03T21:27Z. No other
database on the server was written; the reset touched only
`mustask_mod014` and its Keycloak store volume.

| Run | Started from | What it served |
|---|---|---|
| 1 | the store volume and database the developer's worktree stand left | E2E run 1; US-2, US-3, US-5, US-6 checks; the 200-user backfill; D-1 found; a declared role deleted for run 2. Ended by a force-kill (the Keycloak container left behind was removed by hand) |
| 2 | the same store, no reset | the drift failure; then `aspire stop`, `stand-reset.sh`, migrations |
| 3 | nothing (after the reset) | disturbed by a ~29 h machine sleep before it came up; stopped, not used as evidence |
| 4 | run 3's store, a fresh AppHost, `ReconcilePeriodMinutes=2` | E2E run 2; the backfill after a reset; the period; D-1 reproduced; D-2 observed |

E2E (`E2E_ALLOW_DOCKER=1 npm run e2e`, the existing suite as regression for
the changed AppHost and Keycloak image — the task adds no screen, so no spec
was written for it):

- Run 1, 2026-10-02 (stand run 1): exit 0, Playwright JSON report 19
  expected, 0 unexpected, 0 flaky, 0 skipped. `e2e-results/stand.json`:
  `{"stand": "warm", "waitedMs": 6, "url": "http://127.0.0.1:5000/health", "checkedAt": "2026-10-02T15:48:02.057Z"}`.
- Run 2, 2026-10-03, on a stand started for it (stand run 4; the suite
  started right after the seed): exit 0, 19 expected, 0 unexpected, 0 flaky.
  `e2e-results/stand.json`:
  `{"stand": "warm", "waitedMs": 11, "url": "http://127.0.0.1:5000/health", "checkedAt": "2026-10-03T21:12:08.388Z"}`.
- Both runs warm: per `05-frontend.md` § 7 the repeat stops here. **Deviation
  to carry into the gate-3 summary and the MR's "Deliberately left open":** no
  run of this task observed a cold stand.

### Runs

| Date | What was run | Result |
|------|-----------------|-----------|
| 2026-10-02 | `dotnet build Mustask.sln` | exit 0, 0 warnings |
| 2026-10-02 | `dotnet ef database update` on `mustask_mod014` as left by the developer | exit 0, up to date |
| 2026-10-02 | stand run 1 + `scripts/keycloak-seed.sh` | ready (3000/5000/5010 → 200) after 178 s; seed exit 0 |
| 2026-10-02 | E2E run 1 | exit 0; 19/19; warm |
| 2026-10-02 | live checks, stand run 1 (US-2, US-3, US-5, US-6, backfill) | matrix above; D-1 |
| 2026-10-02 | stand run 2, a declared `mustask-api` role deleted beforehand | `keycloak` Unhealthy, drift named with the reset; D-3 seen |
| 2026-10-02 | `scripts/stand-reset.sh` without `--yes`, no terminal | exit 1, refused, nothing touched |
| 2026-10-02 | `scripts/stand-reset.sh --yes` | exit 0; `mustask_mod014` dropped and created empty; the other ten databases unchanged; store volume removed |
| 2026-10-02 | migrations on the empty `mustask_mod014`: update, update, update to `InitialCreate`, update | exit 0 four times |
| 2026-10-03 | stand run 4 + seed | Keycloak ready and the stand file written after 105 s; seed exit 0 |
| 2026-10-03 | E2E run 2 | exit 0; 19/19; warm |
| 2026-10-03 | api restarted three times; then the `keycloak` resource restarted and the api restarted twice | 3 × caught up in 11–16 s; then 2 × never (D-1); D-2 observed in the Keycloak log |
| 2026-10-03 | `dotnet test src/backend/tests/Mustask.Api.IntegrationTests --filter UserDirectoryQaTests` | exit 0, 12 passed |
| 2026-10-04 | `bash scripts/verify.sh` (vs main, after the QA tests were committed) | exit 0, `RESULT: green`: prose, agents, shell, backend build; tests — Shared 53, Architecture 75, Domain 119, Contracts 109, Application 51, Infrastructure 89, Api.IntegrationTests 480 passed + 9 skipped (the live tests, run by the `provider` bucket); `provider` bucket — `docker build --target provider` ok (served from BuildKit's cache: identical inputs), then `Category=KeycloakProvider` with `MUSTASK_KEYCLOAK_PROVIDER_TESTS=1`: Contracts 17, Api.IntegrationTests 9 (the live tests) passed |
| 2026-10-04 | `docker build --no-cache --target provider deploy/keycloak` (so the Java tests run in this pass, not from cache) | exit 0; `Tests run: 81, Failures: 0, Errors: 0, Skipped: 0`; BUILD SUCCESS |

### Defects

#### D-1: On the stand, after the `keycloak` resource restarts, an api restart cuts the provider off for good

- Severity: major
- Steps: 1. Stand up and seeded (external-database mode). 2. `aspire resource keycloak restart --apphost src/backend/Mustask.AppHost/Mustask.AppHost.csproj`; wait for `/realms/mustask` → 200. 3. `aspire resource api stop`; create a user in realm `mustask`; `aspire resource api start`; `/health` answers 200 within ~7 s. 4. Poll `directory_users` for the new id.
- Expected / Actual: expected the user projected within 5 minutes of the api being reachable (US-5 "The projection catches up after MUSTASK comes back", NFR Freshness). Actually never: Keycloak logs `could not deliver N change(s) to MUSTASK (IOException); retrying in 60 s` and `reconciliation … stopped at its begin (HTTP/1.1 header parser received no bytes)` until the AppHost is restarted. From inside the Keycloak container `aspire.dev.internal:5000` — the address `api.GetEndpoint("http")` gives the container, Aspire's container tunnel — resets the connection, while `host.docker.internal:5000` answers `HTTP/1.1 200 OK`; the tunnel proxy container logs `dcptun.client Failed to listen on client proxy address {"Address": "0.0.0.0", "Port": 5000, "error": "listen tcp 0.0.0.0:5000: bind: address already in use"}` at the api's start. Reproduced in stand run 1 (2026-10-02 16:04Z and 16:10Z) and stand run 4 (2026-10-03, twice); without a prior Keycloak restart three api restarts caught up in 11–16 s. The E2E suite's own Keycloak stops (`docker stop`/`start` of the same container) did not trigger it. plan § 13 assumption 4 named the fallback: the api endpoint is non-proxied on `0.0.0.0`, so `http://host.docker.internal:{Ports:Api}` reaches it without the tunnel.
- Status: fixed (`7a11b52`), verified in re-check 1 (RC-1..RC-3)

#### D-2: The provider's reconciliation retries pile up during an outage

- Severity: major
- Steps: stand with `Keycloak:UserProjection:ReconcilePeriodMinutes=2`; make MUSTASK unreachable for several minutes (as in D-1, or `aspire resource api stop`); read `docker logs mustask-e2e-keycloak-5010` for `stopped at its begin`.
- Expected / Actual: expected one retry pending at a time and one reconciliation once MUSTASK is back — ADR-0025 § 5: "One reconciliation runs at a time; triggers during a run coalesce into one more run". Actually (stand run 4, 2026-10-03) attempts at 21:21:34, 21:22:34, 21:23:04, 21:23:34, 21:24:04, 21:24:34, 21:25:04 (twice), 21:25:34 — one per minute growing to three per minute in four minutes. `Reconciler.runCoalesced` schedules an independent `request` 60 s later after every failed run (a coalesced rerun that fails schedules one more), and `start()` adds a fixed-rate `request` every period; nothing cancels or merges a pending retry. With the default 60-minute period an outage of H hours leaves about H + 1 retry chains firing every minute, each attempt first enumerating every user id of the realm in Keycloak's database (`source.enumerate()` runs before the begin request); once MUSTASK is back, each chain that fires after the previous run completed starts another full reconciliation (20 pages at 10 000 users). `ReconcilerTest.aFailedRun_isRetriedAMinuteLater` checks a single retry only.
- Status: fixed (`ef22b58`), verified in re-check 1 (RC-6, RC-7); what remains of the outage's end is D-4

#### D-3: The stand's drift check runs on every health probe and re-logs every finding

- Severity: minor
- Steps: start a stand and count "Keycloak store: the live realm matches the realm import." in the AppHost output (48 lines in ~30 minutes of run 1); or start one on a store that lacks a declared role (run 2: each finding logged 6 times in ~4 minutes); or remove `mustask-user-projection` from realm `mustask`'s event listeners during a run (US-5's operator action).
- Expected / Actual: expected a start check — ADR-0025 § 7 "A passing result is cached for the AppHost process", and the class summary "Each new finding is logged once". Actually `AppHost.cs` registers `new HealthCheckRegistration(key, services => new KeycloakDriftHealthCheck(…), …)`, and the health-check service calls that factory on every probe, so `_passed` and `_logged` live for one probe: the whole drift check (a dozen admin REST calls) runs on every Aspire health probe for the stand's lifetime, each finding is logged at error on every probe, and a listener disabled mid-run (run 1) turned the running `keycloak` resource Unhealthy and logged "… is not enabled in the live realm … reset the stand: scripts/stand-reset.sh" nine times in 33 s — advice to wipe the stand's database after a legitimate operator action.
- Status: fixed (`7a11b52`), verified in re-check 1 (RC-8..RC-10)

Observations, not defects of this task:

- A fresh worktree's first AppHost start prints "could not delete the stale stand file … DirectoryNotFoundException" because `.e2e-stand/` does not exist yet — ADR-0023's code, unchanged here, harmless.
- Stand run 3 was overtaken by a ~29 h machine sleep while Keycloak was building; its results are not used.

### Verdict

blockers: 0 · majors: 2 (D-1, D-2) · minor: 1 (D-3) · result: fail

### Re-check 1, 2026-10-04

qa-engineer, a clean context that wrote neither the code nor pass 1. The
plan below was written from pass 1's defects, `spec.md` and US-4/US-5, and
committed before the fix commits (`ef22b58`, `7a11b52`) were read; the
Status column was filled in afterwards.

| # | Check | Expectation | Kind | Status |
|---|-------|-------------|------|--------|
| RC-1 | D-1 as reported: restart the `keycloak` resource, then stop the api, create A, disable B, rename C, start the api; twice | A active, B inactive, C renamed within 5 min of `/health` 200, both times (US-5 catch-up, NFR Freshness) | S | ☑ `aspire resource keycloak restart` at 22:55:41Z (container recreated 22:55:53Z, its `--add-host` kept; the start reconciliation completed, 0 changes). Then api restart 1 (5 s outage): A, B, C as expected 0.25 s after `/health` 200; api restart 2 (60 s outage): 51 s after `/health` 200 — the provider's delivery backoff had reached 60 s. Pass 1 saw "never" on both legs. A second Keycloak restart in RC-11 caught up too |
| RC-2 | D-1, the path that worked before: api restart with no Keycloak restart | still caught up | S | ☑ A, B, C written at 22:49:54.95Z, `/health` 200 at 22:49:54Z; "delivery to MUSTASK recovered; a reconciliation follows", one reconciliation, 0 changes |
| RC-3 | D-1: the address the provider is handed | the api's host port through `host.docker.internal`, no `aspire.dev.internal`; follows `Ports:Api` rather than a literal | S, R | ☑ S: the container's `…__ENDPOINT=http://host.docker.internal:5000`, `HostConfig.ExtraHosts` `["host.docker.internal:host-gateway"]`, the name resolves inside the container, the provider logs `configured, FeedOptions[endpoint=http://host.docker.internal:5000, …]` on every start. R: `UserProjectionIdentifiers.FeedEndpoint(apiPort)`, `apiPort` = `Port("Api")`; G `UserProjectionStandWiringRules`; C `UserProjectionStandTests` (5000 and 5100) |
| RC-4 | D-1 regression: what the api listens on | the fix does not widen the api's listening address beyond what the stand had | R | ☑ the api's `0.0.0.0`, non-proxied endpoint (`ExposeToNetwork`) is on main already (main's `AppHost.cs`, `AnyInterface`, `TargetHost`); the fix adds only the runtime argument and the variable's value |
| RC-5 | D-1/D-3 regression: the E2E suite on a stand brought up from nothing, right after the seed | green; `stand.json` quoted | E2E | ☑ exit 0; JSON report 19 expected, 0 unexpected, 0 flaky, 0 skipped; `stand.json` warm — see "E2E" below |
| RC-6 | D-2 as reported: `ReconcilePeriodMinutes=2`, api stopped ≥ 6 min, count `stopped at its begin` in the Keycloak log | at most one retry per minute plus the periodic trigger, not a growing number; after the api returns one reconciliation (plus at most one coalesced), within 5 min | S | ☑ D-2 fixed, D-4 found. Attempts 23:15:36, 23:16:36, 23:17:36Z; the machine then slept ~7.6 h with the api still stopped; after the wake 06:56:56, 06:57:56, 06:58:56, 06:59:56Z — one a minute throughout, the 2-minute period merged into it (pass 1: up to three a minute after four minutes). The api back at 07:00:06Z: user G, created during the outage, projected 48 s later by the retry's reconciliation (07:00:58Z, added 1); 6 s later the delivery recovery started a second full reconciliation (07:01:04Z, 0 changes) — D-4 |
| RC-7 | D-2: no lost retry | a retry that fails schedules the next one; a success leaves none; the period survives a cancelled retry; a trigger during a run yields one more run | P (read the tests), S | ☑ P `ReconcilerTest` 14: the developer's `anOutage_keepsOneRetryPending_…` bounds the attempts from above only, so QA added `anOutageWithoutARecoveryTrigger_isRetriedEveryMinute_andCaughtUpByTheRetryAlone` (60-minute period, 503 for 10 minutes: exactly 10 attempts, the first after recovery completes, no retry left, the period still runs at 60 min); `requestsDuringARun_coalesceIntoOneMoreRun` unchanged. S: RC-11's start reconciliation retried 23:01:55, 23:02:55, 23:03:55Z, then one reconciliation |
| RC-8 | D-3: a healthy start | "the live realm matches the realm import" once per AppHost process over ≥ 20 min of probes; no reset advice while Keycloak is still starting | S | ☑ run A: one line in 35 min of AppHost life (22:35–23:10Z), across two `keycloak` resource restarts and the E2E suite's two `docker stop`/`start`; 0 findings, 0 reset advice, 0 `fail:` lines. Run B: one line, after RC-10's fix. Pass 1: 48 lines in 30 min |
| RC-9 | D-3: listener removed from the realm mid-run (US-5's operator action) | `keycloak` stays Healthy, no reset advice logged; re-enabled → one reconciliation | S | ☑ removed at 23:05:48Z; user F created and `qa.rc1x.a` disabled → after 150 s no row for F, `qa.rc1x.a` still active; `aspire describe`: `keycloak` Running, Healthy; 0 findings logged; `/health` 200. Re-added → "enabled again; a reconciliation follows", one reconciliation (added 1, changed 1), both caught up 16.5 s later |
| RC-10 | D-3 regression: drift at start (listener off before the AppHost starts) | the start fails naming the reset, each finding logged once, the failure is not cached: fixing the realm lets the stand become healthy without a restart or a reset | S, I | ☑ run B started on the store with the listener removed: `keycloak` Unhealthy, provisioning not run, "Keycloak store differs from the realm import: The event listener `mustask-user-projection` is not enabled in the live realm but the realm import enables it; reset the stand: scripts/stand-reset.sh …" logged once over two minutes of probes (pass 1: six times in four). Listener re-added at 23:13:47Z → "matches" logged once, provisioning finished 23:13:57Z, no restart, no reset. C `KeycloakDriftHealthCheckTests` (4) |
| RC-11 | Regression of what the fixes touch: lost deletion and lost change healed by the start reconciliation after a Keycloak restart; the push secret absent from every log | as pass 1 | S | ☑ api stopped, `qa.rc2.a` deleted and `qa.rc2.c` renamed, `keycloak` restarted (unsent changes lost), api kept down 170 s more: `qa.rc2.a` `Absent`, inactive, name and e-mail kept, `qa.rc2.c` renamed, 11.4 s after `/health` 200 (one reconciliation: changed 1, marked absent 1). Push secret (43 characters, both runs): 0 occurrences in the AppHost outputs, the E2E output, the Keycloak container log, `aspire logs` of api, keycloak and frontend, and the worktree's files. US-6 live: no credential, a wrong signature and a realm access token of the `mustask-e2e` service account → 401 each, logged as `MissingHeaders`, `WrongSignature`, `BearerInsteadOfSignature`, no row |
| RC-12 | `scripts/verify.sh` | green, `provider` bucket included | run | ☑ exit 0, `RESULT: green` — "Runs, re-check 1" below |
| RC-13 | Pass 1's ◐ gaps that are tests, not defects: A-19 (id and username of 256, username missing, negative moment) and A-14 (the exact ±300 s edge, if the clock can be fixed) | covered by added tests | I | ☑ QA `PushTimestampWindowTests` (the verifier on a fixed clock: −301 and 301 stale, −300, 0 and 300 accepted); QA `UserDirectoryQaTests.Push_EntryFieldsOutsideTheirLimits_AreRefusedEachUnderItsOwnPath` (id 256 and `""`, username 256, missing and `""`, first and last name 256, moment −1 — each under its own path, the valid entry beside them without a key, nothing applied) and `Push_EntryFieldsAtTheirLimits_AreAcceptedAndHeldAsSent` (255 in every field, a 511-character display name): 7 passed, the product as the contract says |

E2E (`E2E_ALLOW_DOCKER=1 npm run e2e`, once, as the task asked): stand run A
was started from no running stand at 22:34:48Z (migrations: none pending),
ready at 22:36:18Z, seeded at 22:36:39Z, and the suite started at 22:36:49Z
before any manual check. Exit 0; Playwright JSON report 19 expected, 0
unexpected, 0 flaky, 0 skipped (5.6 min). `e2e-results/stand.json`, its
`checkedAt` inside this run:
`{"stand": "warm", "waitedMs": 8, "url": "http://127.0.0.1:5000/health", "checkedAt": "2026-10-03T22:36:54.276Z"}`.
Warm a third time in this task. Pass 1 already carries the deviation to
gate-3 and the MR's "Deliberately left open" (no run observed a cold stand);
this run adds nothing to it but the count. During the suite the feed kept
working: spec 16's throwaway user was projected and then held `Deleted`, and
after each of the suite's Keycloak stops the start reconciliation completed
with 0 changes.

#### Live stand, re-check 1

External-database mode on `mustask_mod014` (port 5432; no other database
read or written), default launch profile, the Keycloak store volume pass 1
left. The lock `.git/stand.lock` was held from 2026-10-03T22:33:48Z to
2026-10-04T07:03:23Z; the machine slept from about 23:18Z to 06:56Z inside
that window with stand run B up and its api stopped (RC-6 uses the sleep as
a long outage; nothing else ran across it).

| Run | Started from | What it served |
|---|---|---|
| A | no running stand; store and database as pass 1 left them (the start reconciliation added the two users pass 1 created while D-1 had the provider cut off) | E2E; RC-2, RC-1, RC-11, RC-9, RC-8, the secret scan, then the listener removed for RC-10; `aspire stop`: no container left, ports free |
| B | A's store, the listener removed, `Keycloak:UserProjection:ReconcilePeriodMinutes=2` | RC-10, RC-6, US-6 refusals, the secret scan; `aspire stop`: no container left, ports free, no AppHost, api or DCP process |

#### Runs, re-check 1

| Date | What was run | Result |
|------|-----------------|-----------|
| 2026-10-04 | `dotnet test src/backend/tests/Mustask.Api.IntegrationTests --filter …PushTimestampWindowTests\|…Push_EntryFields` | exit 0, 7 passed |
| 2026-10-04 | `docker build --target provider deploy/keycloak` (QA's new `ReconcilerTest` case) | exit 0; `Tests run: 84, Failures: 0, Errors: 0, Skipped: 0`; BUILD SUCCESS |
| 2026-10-04 | `dotnet ef database update` on `mustask_mod014` before runs A and B | exit 0, "No migrations were applied" (migrations untouched by the fixes; pass 1's clean, repeat and rollback runs stand) |
| 2026-10-04 | stand run A + `scripts/keycloak-seed.sh` | ready after 122 s (90 s from the AppHost start); seed exit 0 |
| 2026-10-04 | E2E | exit 0; 19/19; warm |
| 2026-10-04 | RC-2, RC-1 (×2), RC-11, RC-9 on run A | table above. A first RC-2 attempt is discarded: QA's own Keycloak tool resolved `localhost` to `::1`, where Keycloak does not listen, so no user was created |
| 2026-10-04 | stand run B (period 2, listener off) | RC-10; then RC-6 across the machine sleep; US-6 refusals |
| 2026-10-04 | `bash scripts/verify.sh` (vs main, after QA's tests were committed; before this file's results were written) | exit 0, `RESULT: green`: prose, agents, shell, backend build; tests — Shared 53, Architecture 77, Application 51, Contracts 114, Domain 119, Infrastructure 89, Api.IntegrationTests 487 passed + 9 skipped (the live tests, run by the `provider` bucket); `provider` bucket — `docker build --target provider` ok (the inputs of the 84-test build above, so BuildKit served it), then `Category=KeycloakProvider`: Contracts 17, Api.IntegrationTests 9 (the live tests) passed |

#### Defect status after re-check 1

- D-1: fixed (`7a11b52`), verified — RC-1, RC-2, RC-3; no regression in RC-4, RC-5, RC-11.
- D-2: fixed (`ef22b58`), verified — RC-6, RC-7; the end of an outage still
  costs up to two reconciliations, not the one the amended ADR states: D-4.
- D-3: fixed (`7a11b52`), verified — RC-8, RC-9, RC-10.

#### D-4: The end of an outage can run two full reconciliations, where ADR-0025 § 5 now says one

- Severity: minor
- Steps: stand with `Keycloak:UserProjection:ReconcilePeriodMinutes=2`; `aspire resource api stop`; create a user in realm `mustask` (so the provider's change delivery fails too); keep the api down for more than a minute and a half (a reconciliation attempt fails and its retry is pending, the delivery backoff grows towards 60 s); `aspire resource api start`; read `docker logs mustask-e2e-keycloak-5010` for `completed`.
- Expected / Actual: expected one reconciliation once MUSTASK is back — ADR-0025 § 5 as amended for D-2: "An outage of any length therefore costs one attempt a minute (plus the periods), and its end one reconciliation", and the same in `Reconciler`'s class comment. Actually two, whenever the pending retry fires before the change delivery's next attempt: RC-6, 07:00:58Z `completed — MUSTASK received 18, added 1 …` (the retry), then 07:01:04Z `delivery to MUSTASK recovered; a reconciliation follows.` and a second full run, `received 18, added 0, changed 0 …`. The recovery's `request()` arrives when no run is pending, so nothing coalesces it; if it arrives during the retry's run, `rerun` makes the same second run. Only when the delivery recovers first does the retry get cancelled and one run happen (RC-11, RC-2). `afterAnOutage_theRecoveryRunsOneReconciliation` drives only that order. Bounded at two whatever the outage's length — D-2's growth is gone — and the second run changes nothing, but at 10 000 users it is one more full enumeration in Keycloak's database and 20 more pages. Either skip a recovery request when a reconciliation has completed since the delivery started failing, or state "at most two" in the ADR and the class comment.
- Status: fixed (`7562620`, code review F1), verified in re-check 2 (RC2-1..RC2-8; one microsecond window left, O-1). The sender hands the reconciler the mark taken at the outage's first failure; the recovery asks for a run only when no run begun since that mark has completed or is under way. `ReconcilerTest.afterAnOutage_whenTheRetryCompletesFirst_theRecoveryAddsNoRun` drives the order of RC-6 (one completion), `aRecoveryDuringARunBegunInTheOutage_addsNoRun` the recovery during the retry's run, `aRecoveryAfterOnlyRunsBegunBeforeTheFailure_runsOne` that the rule is not vacuous; both of the first two failed against the code before the fix. ADR-0025 § 5 and `Reconciler`'s class comment keep "one reconciliation", now true for both orders

Observations for the code review, not defects:

- ADR-0025 rejects store option B partly because a container reaching the
  host "through the Docker host gateway (listen addresses, … Linux's missing
  `host.docker.internal`)" is "a networking dependency on a machine we do not
  control". The D-1 fix now routes the feed through that same gateway (with
  `--add-host …:host-gateway` for Linux), and the ADR does not record the
  feed's stand address at all — it is in the plan (§ 9.3, § 13) and
  `src/backend/AGENTS.md` only. Verified here on Docker Desktop for Windows;
  a Linux engine with a host firewall over the bridge is not.
- A start with the listener disabled names only the reset as the remedy,
  though re-enabling the listener cleared it in seconds (RC-10). NFR Stand
  asks for naming the reset, so this is as specified.

#### Verdict, re-check 1

blockers: 0 · majors: 0 · minor: 1 (D-4, open) · D-1, D-2, D-3 fixed and verified · result: pass

### Re-check 2 (after code review fixes), 2026-10-04

qa-engineer, a clean context that wrote neither the code, pass 1 nor
re-check 1. Scope: only what the code review's fix passes touched — round 1's
F1..F11 (`7562620`, `f1f8e52`, `ce70bdb`, `6b14866`, `cad9c2a`, `7bdbcd5`),
round 2's F12/F13 (`0366b6e`, `f4ce0d6`, `5c4ca3d`, `fd50be8`, `971c1cc`) and
round 3's prose-only fix (`80d481f`, `d818410`; its one code-file change is a
Javadoc). Stage 8: after review findings are fixed, QA is re-run over what was
touched; the rest of the plan is not re-run (pass 1 and re-check 1 stand).

The plan below was written from `review.md` (rounds 1-3 and their resolution
notes), `spec.md` (BR-UP-7, BR-UP-8, NFR Freshness, NFR Logging, US-4, US-5),
this file's D-4 and re-check 1, and committed (`e831d8e`) before any fix code,
test or `plan-backend.md` was read. The Status column was filled in afterwards.
The live stand was not used (the round-3 reviewer observed the start-up timing
on a throwaway container, D-4's fix has unit tests, and every behaviour below was
provable without it); no stand lock was taken.

Legend as above; **M** — a mutation check: the fix is taken out or a
violation is put in a scratch copy, the test must fail, the copy is
reverted and never committed. **QA** names a test added in this pass:
`OutageRecoveryQaTest` (Java, provider), `StaleSortKeyQaTests` and
`UserDirectoryRawSqlQaRules` (.NET); **dev** a test the fixes came with.

| # | Behaviour the fixes changed | Scenario and check | Kind | Status |
|---|---|---|---|---|
| RC2-1 | D-4 / F1: an outage ends with exactly one reconciliation, the pending retry first | Delivery fails (change feed and a reconciliation attempt), a retry is pending, the retry fires and completes, then the sender's next attempt succeeds. Exactly one completed run after the outage; the recovery adds none | P | ☑ dev `ReconcilerTest.afterAnOutage_whenTheRetryCompletesFirst_theRecoveryAddsNoRun`; QA `anOutageOfEveryRoute_endsInOneRun_whicheverOfRetryAndRecoveryComesFirst`, retry-first leg, through the real `FeedSender` handing its real mark to the real `Reconciler`: the start run completes, a period run and a chain of retries fail for 16 minutes with exactly one retry pending, the retry then completes, the sender's recovery asks for nothing, one completion in total at the end |
| RC2-2 | D-4: the delivery recovers first | The delivery recovers while a retry is still pending: exactly one run, and the pending retry does not run a second one a minute later | P | ☑ dev `afterAnOutage_theRecoveryRunsOneReconciliation`; QA, recovery-first leg of the same test: one completion, no retry left behind, queue empty |
| RC2-3 | D-4: the recovery arrives during the retry's run | Recovery while the retry's run is in flight: no rerun, one completion in total | P | ☑ dev `aRecoveryDuringARunBegunInTheOutage_addsNoRun` |
| RC2-4 | D-4, not vacuous: a run that began before the failure covers nothing | A period run completed before the outage began, then an outage, then a recovery: the recovery asks for a run (exactly one). A run that began before the failure and is still under way when it starts failing does not count either | P | ☑ dev `aRecoveryAfterOnlyRunsBegunBeforeTheFailure_runsOne`; QA `aRecoveryDuringARunThatBeganBeforeTheFailure_runsOneMore` (the under-way case: the comparison against the mark is strict, two completions, nothing after) |
| RC2-5 | D-4, the mark is per outage | Two outages back to back (fail, recover, fail, recover): each end gets its own run; a failure in the middle of an outage does not move the mark; a recovery that finds no failure (the first success of the sender's life) asks for nothing | P | ☑ dev `FeedSenderTest.theRecovery_handsBackTheMarkOfTheOutagesFirstFailure`; QA `twoOutagesInARow_eachEndInOneRunOfItsOwn` (3 completions after two outages, then 3 for ten more minutes) and `aSendersFirstEverSuccess_asksForNoRun` |
| RC2-6 | D-4, ordering of requests: no trigger lost, no double run | Triggers from different threads (the scheduler's period, the sender's recovery, an overflow, a re-enablement) in any interleaving: never more than one rerun pending, and after the last trigger at least one run begins. Deterministic orders in the virtual-scheduler tests; the interleavings with real threads in a repeated randomized test | P (+ added) | ☑ QA `triggersFromManyThreads_leaveNoTriggerUnserved_noStuckFlag_andNoMissingRecoveryRun`: 300 rounds on a real `ScheduledThreadPoolExecutor`, three kinds in turn — a retry's request against the recovery, a storm of three requests and the recovery, the recovery alone — released together on plain threads. Every round: at least one run, one that enumerated after the latest request began, no trigger thread stuck, and one lone request afterwards makes exactly one run (no flag left set); the recovery alone runs exactly one. Not a pass-through: of 100 retry-against-recovery rounds, 11 and 13 (two runs) ran twice — direct requests landing inside a recovery's run, which the provider's own order does not produce; the one window it can produce is O-1 |
| RC2-7 | D-4, a run that fails during the outage | The retry's run itself fails (MUSTASK still down): the mark survives, the next retry and the recovery still end in one completed run after the outage; no growth of retries (D-2 stays fixed) | P | ☑ the through-the-sender test above: 16 minutes of failing retries and failing sender attempts, one retry pending throughout, one completion at the end in both orders |
| RC2-8 | D-4, a period run completes while only the change route fails | Changes stay queued and are delivered after the recovery; the recovery asks for no extra run; no change is lost | P | ☑ QA `aChangeRouteOutage_healedByAPeriodRun_costsNoRunAtRecovery_andLosesNoChange` (2 completions, the kept change delivered, no retry) and its control `aChangeRouteOutage_withNoRunSinceTheFailure_endsInExactlyOneRun` (the start run does not cover an outage that began after it: exactly one more) |
| RC2-9 | F13 / F6: the start-up path with a realm import that is slow | The realm arrives before the start run is due (8 s): the start run is the only run. The realm arrives after it (42 s): the start run was skipped and is made up once at the first sighting, by seconds. The realm arrives after the quick phase (more than 5 minutes): made up within a minute of the arrival. Never two runs for one start | P | ☑ dev `aRealmImportedAfterTheFirstChecks_leavesTheStartRunTheOnlyRun`, `aRealmImportedAfterTheStartRunWasDue_isReconciledAtItsFirstSighting`, `aStartRunSkippedWhileTheStateWasUnknown_isMadeUpWhenTheListenerIsFirstSeenEnabled`; QA `aRealmImportedAfterTheQuickChecksEnded_isReconciledWithinAMinuteOfItsArrival` (arrival at 7 minutes: one run within a minute, one in ten more) |
| RC2-10 | F13: the cadence | No check before 2 s; the first at 2 s; one every 2 s while the state is unknown (up to 5 minutes), then each minute; each minute once the state is known | P | ☑ dev `theFirstCheck_runsTwoSecondsAfterTheStart_thenEachMinute`, `whileTheStateIsUnknown_theCheckRepeatsEveryTwoSeconds`, `aStateStillUnknownAfterFiveMinutes_isCheckedEachMinute_andReportedOnce` (150 probes in 5 minutes, 155 in 10) |
| RC2-11 | F13: a missing realm | Missing while the state is unknown stays unknown and is not a re-enablement; the arrival is the first sighting (`requestIfSkipped`), at most one run. Once known, a vanished realm counts as not enabled and its return is a re-enablement (one run) | P | ☑ dev `aRealmMissingBeforeTheStateIsKnown_…`, `aRealmGoneOnceTheStateIsKnown_…`; QA `aRealmThatVanishesAndReturns_isReconciledOnceOnReturn` through the wired watch and reconciler: one run on return, none more in ten minutes |
| RC2-12 | F13 / F6: the first reading finds the listener NOT enabled | Realm exists, listener absent at the first reading, enabled later: one re-enablement run, and the skipped start run is not made up a second time | P | ☑ QA `aRealmFirstSeenWithoutTheListener_thenEnabled_isReconciledOnce` |
| RC2-13 | F13: a failing probe | A probe that throws while unknown keeps the state unknown (changes held and captured), logs one line per episode and not one per 2 s probe; later success is the first sighting | P | ☑ dev (the 150-probe test: one WARN and one more INFO); QA `checksThatFailUntilAfterTheStartRun_stillLeaveOneReconciliation_andOneLogLine` (20 throwing probes, one "could not read" line, one run made up at the 21st probe, 42 s) |
| RC2-14 | F13: removal and re-enabling in the middle of a run | The listener is removed from the realm while a reconciliation is in flight and while changes are queued: nothing is pushed from then on, nothing captured while known-disabled except what was queued; re-enabled: exactly one reconciliation and the queue drains; a removal seen while the state is unknown is still captured (`isEnabledOrUnknown`) | P | ◐ dev `UserRemovalListenerTest` (captured while unknown, ignored while disabled); QA `aListenerRemovedAndAddedBack_costsOneRun_notOnePerSkippedTrigger` (the period run in the window skipped, one run on adding back, none more). Not tested: a run already in flight when the listener is removed — it runs to its end, because the watch's probes share the reconciler's single scheduler thread with the runs (O-2) |
| RC2-15 | F13: the chain's end | `close()` mid-chain (the scheduler shut down): no exception escapes, no check runs afterwards, no thread outlives the provider; `init` again after `close` works | P | ☑ dev `aShutDownScheduler_endsTheChecksQuietly`; QA `closingTheProvider_endsItsThreads_andItCanStartAgain`: `init`, `postInit` with a mocked session factory, `close`, twice; the sender thread and the scheduler's thread exist after `postInit` and are gone within 5 s of `close` each time |
| RC2-16 | F13: the sender is held until the first sighting | Changes captured before the first sighting are held (not sent at 1.999 s, sent in order after it), none lost, overflow of the held queue still asks for its own run | P | ☑ dev `FeedSenderTest.aChangeCapturedAtTheStart_isSentOnceAQuickCheckSeesTheRealm`, `PendingChangesTest.overTheCap_…`, `ListenerEnablementWatchTest.theFirstCheck_…`. No integrated overflow-while-unknown test: it is the same path as the skipped start run (`request()` then SKIPPED then the flag), covered by RC2-9 |
| RC2-17 | F13: freshness from readiness on, in the built image | The live tests, with a fixture that measures one push before any test; the test bounding the start window to 10 s. Run the whole class twice, and each of the first test alone (order-independence, the very thing F13 was) | L | ☑ `Category=KeycloakProvider` 10/10 twice in a row (25 s, 16 s) and once more inside `verify.sh`; alone: `ADeletion_IsPushedCarryingTheLastState` (the test F13 failed) 1/1, `TheStartReconciliation_SendsAWholeSnapshot` 1/1, `AChangeMadeAsKeycloakBecomesReady_IsPushedWithinTheFreshnessBound` 1/1. Measured by a scratch probe (not committed) printing the fixture's `FirstPushAfterReady` on three more fresh containers: 0.41 s, 0.42 s, 1.43 s. Round 2 saw 2 failures in 3 runs; here 0 in 10 fixtures (two more of them inside the two `verify.sh` runs) |
| RC2-18 | New and changed log lines: no secret, no user name or e-mail, no per-probe noise | Log capture over RC2-1..RC2-16: the push secret, names and e-mails absent; ids only; the watch's failing probe at most one INFO or WARN per episode; each recovery and each outage logged once | P | ☑ by reading and by the tests: the watch logs the probe failure's exception class, never its message; the reconciler's two new lines (`a reconciliation begun during the outage covers it`, `a reconciliation follows the recovery of delivery`) carry no data; the sender's lines name ids only, and dev `FeedSenderTest` asserts no name, e-mail or secret in them; RC2-13's count |
| RC2-19 | F5: the completion re-keys stale sort keys, and only sort keys | Users whose sort key was computed under another collation version (deleted, absent, inactive, active): after one completion their key is current; lifecycle, moment, change time, name, e-mail and status unchanged; the answer of an `IncludeInactive` search in the order of the read model | I | ☑ dev `ReconciliationTests.Reconcile_RecomputesStaleSortKeys_OfDeletedAndAbsentUsersToo`; QA `Completion_RekeyingADeletedUserWhoseLastStateWasEnabled_KeepsItInactive` (a deletion carrying "enabled" stays inactive after the re-key, out of the default search, in the inactive one). M: the stale rows taken out of the completion's lock-and-load (the fix reverted in a scratch copy): 4 tests failed (3 QA, the dev's), 10 others passed; reverted |
| RC2-20 | F5, bounds | 0 stale; exactly 1000; 1001 (the 1001st waits for the next completion, no error); an interrupted snapshot (no completion) re-keys nothing | I | ☑ QA `Completion_RecomputesAtMostTheBound_andTheNextCompletionTheRest` (1 001 deleted users: 1 left stale after the first completion, 0 after the second, all still deleted at their moment), `AnInterruptedSnapshot_RecomputesNoKey_TheCompletedOneDoes`; 0 stale is every other test's baseline |
| RC2-21 | F5, concurrency and locks | A completion re-keying stale rows while pushes for the same users apply (and a second snapshot's completion): no deadlock, no lost newer push, the newer state wins, run repeatedly | I | ◐ QA `ACompletionRekeyingTheUsers_BesideConcurrentPushesForThem_…`: ten rounds of a reconciliation (every third user an absence candidate, every key stale) beside thirty steps of 20-user push batches mixing both sets, all requests 2xx, the newest moment and e-mail kept for all 60 users each round, no stale key left; and `Completion_WithTenThousandCandidatesAndAThousandStaleOthers_LocksThemAllInOneTransaction`: 11 000 users in one completion on the test PostgreSQL's default lock table (O-3). Not shown: that the concurrency test fails when the two sets are locked in two calls — the mutation edit was refused by the permission classifier and not pursued, so its power against that regression is unproven; it passes on the real code |
| RC2-22 | F5: only candidates can be marked absent | A stale-key row that is not an absence candidate (newer than the snapshot's begin, reported deleted, already absent) is re-keyed but never marked absent | I | ☑ QA `AStaleKeyThatIsAlsoAnAbsenceCandidate_IsMarkedAbsentAndRekeyed_AndANewerUserOnlyRekeyed` (the same id in both sets: marked absent once, key current; a newer present user missing from the snapshot: re-keyed, still present, moment, change time and name untouched); the deleted and absent rows of RC2-19/20 |
| RC2-23 | F10: the boundary rule is real | `DirectoryUsers`, the snapshot sets, `ProjectedUser` and both store interfaces named from Infrastructure/Api outside the sub-slice, `MustaskDbContext.cs` and `Migrations/`: flagged; inside them: not; legitimate neighbours (`IUserDirectory`, `DirectoryUser`) not flagged. A violation put into a scratch copy makes the architecture test fail | G, M | ☑ M: a scratch `src/backend/Mustask.Infrastructure/Organizations/ScratchQa.cs` naming `context.DirectoryUsers` in a string: `Projection_write_side_and_storage_are_used_only_by_the_sub_slice` failed (on Windows paths too), 79 others passed; file deleted, never committed. Dev's `Projection_internals_rule_is_not_vacuous` green |
| RC2-24 | F10: the residual the reviewer named — raw SQL on the projection's tables | A consumer's raw SQL naming `directory_users` or the snapshot tables from outside the sub-slice | G | ☑ QA `UserDirectoryRawSqlQaRules` (2 tests, 81 in the project): no source outside the allowed places names the three tables today; flagged in the same scratch run (M above, `directory_users` in a second string), flagged on synthetic files in its own not-vacuous test |
| RC2-25 | F4: a refused stand reset keeps the E2E stand file; a performed one removes it | `--stand-reset` while a stand runs: refused, `.e2e-stand/keycloak-e2e-client.json` still there. Idle reset: the file is gone after the wipe. A normal start still deletes a stale file | G, I (stand only if no test reaches it) | ◐ dev `UserProjectionStandWiringRules.AppHostCs_KeepsTheE2eStandFile_WhenRunAsAStandReset` (source shape). QA behaviour, no stand and no database touched: a dummy `.e2e-stand/keycloak-e2e-client.json` in this worktree, `dotnet run --project src/backend/Mustask.AppHost --no-build -- --stand-reset` with stdin redirected and a dummy `ConnectionStrings__mustask` naming `mustask_mod014`: exit 1, "Stand reset refused: no terminal to confirm on", the file still there; dummy removed. Not run: the pre-fix control, an idle reset that wipes (it needs a database to drop), and a normal start's deletion (unchanged code, needs a stand) |
| RC2-26 | Regression: the provider's read-only guard and boundary | The guard's allowlist stays as ADR § 8 says after the watch's new reads (`getContext`, `setRealm` from `ProjectedRealm` only); the not-vacuous fixtures still fail | P | ☑ `ReadOnlyGuardTest` (2) and `ReadOnlyGuardIsNotVacuousTest` (1) inside the 114/114 below; the watch reads `realms().getRealmByName` through `ProjectedRealm.find`, writes nothing |
| RC2-27 | Regression: the probes put no load on Keycloak | A probe opens and closes its session each time (no leak at 150+ probes in 5 minutes); the quick phase ends; known-state probes are one a minute | P | ☑ by reading: the probe goes through `KeycloakTransactions` (`runJobInTransaction`: Keycloak opens and closes the session and transaction); 150 probes in the first 5 minutes only while the state is unknown, one a minute otherwise (dev's cadence tests); live: the fixture's first push arrived 0.4-1.4 s after readiness on three fresh containers (RC2-17) |
| RC2-28 | Everything the fixes touched, together | `docker build --target provider` (Java unit tests, count), the `KeycloakProvider` live category twice, the sub-slice's .NET tests, Architecture tests, `bash scripts/verify.sh` | run | ☑ "Runs, re-check 2" below |

#### Runs, re-check 2

| Date | What was run | Result |
|------|-----------------|-----------|
| 2026-10-04 | `docker build --target provider deploy/keycloak`, first QA tests | exit 1: `Tests run: 113, Failures: 1`. The failure was QA's own new stress test: an invariant that counted a recovery covered by a run already under way as a request needing a later enumeration. A test defect, not a product one; the invariant was narrowed to `request()` and a recovery-alone round added |
| 2026-10-04 | `docker build --target provider deploy/keycloak` | exit 0; `Tests run: 113, Failures: 0, Errors: 0, Skipped: 0`; BUILD SUCCESS |
| 2026-10-04 | the same, after QA's `aRecoveryDuringARunThatBeganBeforeTheFailure_runsOneMore` | exit 0; `Tests run: 114, Failures: 0, Errors: 0, Skipped: 0` (`ListenerEnablementWatchTest` 13, `ReconcilerTest` 21, `FeedSenderTest` 13, `OutageRecoveryQaTest` 13); BUILD SUCCESS |
| 2026-10-04 | `dotnet test src/backend/tests/Mustask.Architecture.Tests` | exit 0, 81 passed (79 + QA's 2) |
| 2026-10-04 | the same with the scratch violation (M, RC2-23) | exit 1, 2 failed (the dev's rule and QA's raw SQL rule), 79 passed; scratch deleted |
| 2026-10-04 | `dotnet test src/backend/tests/Mustask.Api.IntegrationTests --filter StaleSortKeyQaTests` | exit 0; 4, 5 and finally 6 passed as the tests were added; then with `ReconciliationTests` and `UserDirectoryVolumeTests`: 18 passed |
| 2026-10-04 | the same with M (the stale rows out of the completion's lock-and-load) | 4 failed, 10 passed; the handler restored with `git checkout`, `git status` clean |
| 2026-10-04 | `MUSTASK_KEYCLOAK_PROVIDER_TESTS=1 dotnet test src/backend/tests/Mustask.Api.IntegrationTests --filter Category=KeycloakProvider`, twice in a row | exit 0 both times; 10 passed (25 s, 16 s) |
| 2026-10-04 | the same, one test each: `ADeletion_IsPushedCarryingTheLastState`, `TheStartReconciliation_SendsAWholeSnapshot`, `AChangeMadeAsKeycloakBecomesReady_IsPushedWithinTheFreshnessBound` | exit 0 each; 1 passed. The two first ran at the same time on two fresh containers |
| 2026-10-04 | a scratch probe printing `FirstPushAfterReady`, three fresh containers | 0.41 s, 0.42 s, 1.43 s; probe deleted |
| 2026-10-04 | `dotnet run --project src/backend/Mustask.AppHost --no-build -- --stand-reset < /dev/null`, dummy connection string and dummy stand file | exit 1, refused; the stand file still there (RC2-25) |
| 2026-10-04 | `bash scripts/verify.sh` (vs main, at `f180cb9`, after the QA tests were committed and before this section's results were written) | exit 0, `RESULT: green`: prose, agents, shell and PowerShell-wrapper checks, backend build; tests — Shared 53, Architecture 81, Domain 120, Application 51, Contracts 114, Infrastructure 89, Api.IntegrationTests 494 passed + 10 skipped (the live tests, run by the `provider` bucket); `provider` bucket — `docker build --target provider` ok (BuildKit served it: the inputs of the 114-test build above), then `Category=KeycloakProvider`: Contracts 17, Api.IntegrationTests 10 (the live tests) passed; frontend lint, build and tests ok after `npm ci` in `src/frontend` (a fresh worktree has no `node_modules`). An earlier run at `8c0ab59` was green too (Api.IntegrationTests 493 + 10 skipped), before QA's last two tests |

#### Defects, re-check 2

None open. The re-check found no behaviour of the fixes that contradicts the
spec, the stories or ADR-0025 as the code review left them. D-4 is verified.

- D-4 (D-4's fix, F1): verified — RC2-1..RC2-8. The end of an outage runs one
  reconciliation in both orders and for a recovery during the retry's run; a
  recovery after only earlier runs runs one; the mark is per outage; a change
  route outage healed by a period run costs no extra run and loses no change.
- QA's own first stress test failed once (see Runs); that was the test.

Observations, not defects:

- O-1. "Its end one reconciliation" (ADR-0025 § 5, `Reconciler`'s class comment)
  is exact for every order the provider's threads produce except one
  microsecond window: the recovery reads `runUnderWay` and `lastCompleted`
  (sender thread), logs a line, and only then calls `request()`; if the retry
  fires on the scheduler thread within about the time that log call takes, the
  retry's run begins between the recovery's reads and its `request()`, and the
  recovery's `rerun` is set after the run cleared it — two runs, the second
  changing nothing, never none (RC2-6's rounds with a lost trigger: 0 in 300).
  With a retry a minute and a sender attempt at most a minute apart the chance
  per outage end is of the order of 1e-6. Not worth a change; if the sentence
  is to be exact it can say "one, or — if the retry and the recovery coincide —
  two". Moving the log line after `request()` would shrink the window to
  nanoseconds.
- O-2. The enablement probes run on the reconciler's single scheduler thread,
  so a reconciliation in flight (up to about five minutes at 10 000 users) runs
  to its end when the operator removes the listener, and the removal is seen
  after it. US-5's scenario ("A disabled provider freezes the projection") says
  nothing about a run in flight; unchanged by the fixes (the probe was on this
  thread before them).
- O-3. A completion can lock 11 000 users in one transaction (10 000 absence
  candidates plus the 1 000 stale rows that are not candidates); it passed on
  the test PostgreSQL's default `max_locks_per_transaction` (64). The code
  review's note on a tuned external server with less lock-table slack stands;
  nothing was run against one.
- O-4. `Reconciler.deliveryRecovered`'s Javadoc says that for a run under way
  "its retry is already pending"; the run cancelled the pending retry when it
  began, and schedules the next one if and when it fails. The outcome is the
  same; the word is not exact.
- The mutation that would show whether RC2-21's concurrency test can fail
  (two lock calls instead of one) was not run; see RC2-21.

#### Verdict, re-check 2

blockers: 0 · majors: 0 · minors: 0 open · D-4 verified · result: pass

### Re-check 3 (after the security pass), 2026-10-04

qa-engineer, a clean context that wrote neither the code, pass 1, re-check 1
nor re-check 2. Scope: only what the security pass's fixes touched — S1, S2,
S3, S4, S5 as `review.md` ("Security pass — backend") records them resolved,
and re-check 2's O-1 and O-4 that `d65834f` took along. Fix commits: `d65834f`
(S1, O-1, O-4, provider), `f928b23` (S2), `513ac88` (S3, S4), `e83c995` (S5),
`319f169` (docs, contract rows). Stage 8: after review findings are fixed, QA is
re-run over what was touched; the rest of the plan is not re-run (pass 1,
re-check 1 and re-check 2 stand).

The plan below was written from `review.md`'s security pass (the findings and
their `Status: resolved` notes), `api-contract.md` (Part B.2, B.3, B.6, Part C
`value_invalid`), `spec.md` (BR-UP-3, BR-UP-5, BR-UP-6, BR-UP-7, BR-UP-8,
BR-UP-10, NFR Logging) and re-check 2's O-1 and O-4, and committed before any
fix code, test or diff was read. The Status column is filled in afterwards.

Legend as above; **M** — a mutation check: the fix (or one field's share of it)
is taken out in a scratch copy, the tests must fail, the copy is reverted and
never committed. **QA** names a test added in this pass; **dev** a test the
fixes came with. What the fixes could have broken is the question of each row,
beside whether they do what they say.

#### S1 — the provider makes what it projects well-formed

| # | Behaviour the fix changed | Scenario and check | Kind | Status |
|---|---|---|---|---|
| RC3-1 | Event path: an unpaired surrogate never reaches the wire | An own-profile update (`UPDATE_PROFILE`) and a create with an unpaired high surrogate, an unpaired low one, a high one before a non-low, a low before a high (reversed pair), a high at the very end, a run of several, in each of id, username, first name, last name, e-mail. Every shape → U+FFFD at the same position, one for each unpaired unit; the body is valid UTF-8 with no escaped surrogate. Not only the shapes the developer chose: a seeded property test against an independent oracle (walk by code points; a lone surrogate → U+FFFD; U+0000 dropped; everything else unchanged) | P | ☑ P: QA `ProjectedTextQaTest` — `theCleaning_agreesWithAnIndependentOracle_onTwoHundredThousandRandomTexts` (200 000 texts of 15 units — lone high and low halves, valid pairs, U+0000, U+FFFD, controls — equal to a code-point-walk oracle, nothing malformed left, a fixed point), `theReader_cleansEveryField_onRandomProfiles` (300 profiles, five fields, a null stays null), `anOwnProfileUpdate_throughTheListener_…`; dev: nine shapes in four fields and the id. M: the last name's cleaning taken out of `read` in a scratch copy: 17 of 139 provider tests failed (dev `ProjectedRealmAndReaderTest` ×10, `UserProjectionListenerTest`, `UserRemovalListenerTest`, `KeycloakRealmSourceTest`; QA ×4; the 18th failure of that build is RC3-36's mutation), reverted |
| RC3-2 | U+0000 is dropped, on every field | U+0000 alone, inside, at both ends, repeated, next to an unpaired surrogate (`"\u0000\uD800"`) and inside a valid pair's neighbourhood, in each of the five fields; the result is the same text without it | P | ☑ the oracle drops U+0000 in every position and beside lone halves; dev `"\u0000\uD800\u0000"` → one U+FFFD. Recorded: a U+0000 between two halves does not join them (the pair is judged on the text as Keycloak holds it), so `\uD83D\u0000\uDE00` becomes two U+FFFD — deterministic and the same on every path, harmless |
| RC3-3 | The cleaning leaves a value MUSTASK can still refuse: one that cleaning empties | A username or id that is only U+0000 (Keycloak's own validators refuse it; an unvalidated import into the stand's store may carry it) becomes empty and MUSTASK answers 400 `value_required` — the poison of S1 by another door. Check what the provider does with such a user (skip with a log line naming its id, or push and be refused). A first or last name that cleaning empties is fine (`""`, display name falls back to the username, BR-UP-3) | P, I | ◐ O-2: a username or id that cleaning empties (U+0000 alone) is sent as `""` and MUSTASK answers 400 `value_required`, which drops the batch — the S1 stall by another door. Reachable only through a username Keycloak's own validator refuses (`error-username-invalid-character`, the security pass's probe), i.e. an unvalidated import into the stand's store; not run. An empty first or last name is `""`, which optional-text validation takes; not run separately |
| RC3-4 | Removal path: the deletion carries the cleaned last state | A user with a poisoned first name and last name is deleted: the deletion entry (B-1) carries U+FFFD for the surrogates, no U+0000, and MUSTASK applies it (200, `deleted`) | P, L | ☑ P: `theEventTheRemovalAndThePage_carryTheSameCleanText_inJsonMustaskCanRead` (100 users, the deletion carries the cleaned text), dev `UserRemovalListenerTest`; L: `EmojiSurviveAndLoneHalvesAreCleaned_…` deletes a user with halves in both names: the deletion entry carries U+FFFD and `Лон�ный` |
| RC3-5 | Reconciliation path: a page carries the cleaned state | A realm page holding poisoned users (all fields) among good ones: the page is well-formed, the receiver answers 200, the completion follows and marks nobody absent; the same user pushed by event and then by the page is the same text (`changed` 0 on the page — the two paths clean alike, so no churn on every reconciliation) | P, L | ☑ P: the page path and the event/removal path give the same text for 100 random users, so a reconciliation does not churn what an event pushed; L: the page of a real reconciliation after an outage holds the emoji user unchanged, and every body of that live window is read by MUSTASK's own request types, validation and derivation without an exception. Not shown live: MUSTASK's `changed` counter — the receiver in the fixture is a stand-in that answers every entry as unchanged |
| RC3-6 | A valid surrogate pair survives unchanged, end to end | An emoji (U+1F600), a supplementary CJK character (U+20000) and a flag (two regional indicators) in a first name, last name, username, e-mail: unchanged on the event, removal and page paths, in the push body as UTF-8 (4 bytes, not escaped), and in MUSTASK: the stored value, the display name (BR-UP-3), a search by the emoji and by the text beside it. Pair next to an unpaired unit: `"😀\uD800"` → `"😀�"`, `"\uD800😀"` → `"�😀"`, `"\uD83D😀"` → `"�😀"` | P, I | ☑ P: `aValidPair_isKeptUnchanged_onEveryPath_andTravelsAsUtf8` (emoji, a supplementary ideograph, a flag, a pair beside a lone half, a lone high half before a pair, a reversed pair = two U+FFFD); I: `AnEmojiInAName_IsFound_ByTheTextBesideIt_AndByTheEmoji`, `EveryOtherCharacter_…` (U+1F600, U+20000); L: emoji in first and last name via admin REST, pushed unchanged. Finding O-1: Jackson writes a valid pair as two escapes (`\ud83d\ude00`), not as four UTF-8 bytes — valid JSON that System.Text.Json joins; the developer's note "valid UTF-8 with no escaped surrogate" holds for the cleaned halves, not for pairs |
| RC3-7 | Lengths: cleaning never lengthens a value, counting is by code points | 255 emoji (510 UTF-16 units) in a name → accepted; 256 → `value_too_long`; 255 characters with U+0000 inside (so 254 after cleaning) as the provider sees them; the largest batch the provider can build (500 entries, five fields of 255 emoji each) is under the 4 MiB body bound | I, P | ☑ I: `TheLimit_IsCodePoints_256EmojiAreTooLong_255Pass_InEveryTextField` (256 → `value_too_long` in each of five fields; 255 stored and read back, display name 511 code points), `TheLargestBatchKeycloaksLimitsAllow_FitsTheBodyBound_AndIsApplied` (500 entries, every field at Keycloak's 255 UTF-16 units: 3.85 MB, 92% of 4 MiB, applied). O-1: five fields of 255 emoji — which MUSTASK takes and Keycloak's length check does not — would be 7.7 MB on the wire |
| RC3-8 | A user whose cleaned name collides with another's | Two users `"Сам\uD800"` and `"Сам\uD801"` (both → `"Сам�"`), and one with a real U+FFFD, all different ids: all held (the projection is keyed by id, BR-UP-5: names and e-mails are not unique), a search finds all, in the order of BR-UP-15 (ties by id), a lookup answers each by its id; the collation and the sort key take U+FFFD without a 500. Two **ids** that clean to one id would merge two users: reachable only for ids that are not Keycloak's UUIDs — recorded, not tested | I | ☑ I: `UsersWhoseCleanedNamesCollide_AreAllHeld_Found_AndOrderedById` (four ids, one display name holding U+FFFD: all held, found by "сам" and by the text with U+FFFD, ordered by id, all four known to a lookup). Two ids that clean to one are recorded, not tested (Keycloak's ids are UUIDs) |
| RC3-9 | The 400 lead: a page refused with 400 logs its ids | A page the receiver answers 400: one ERROR line with the page number, the code and the ids of the whole page (up to 500), ids only — no name, e-mail, username, secret, no echo of the response body; the run stays FAILED, nobody is marked absent, it is retried in 60 s; a refused begin or completion (400) names no id; a 5xx, a 401 and a timeout on a page log no ids | P | ☑ read, with dev `ReconcilerTest` ×3: one ERROR line with the ids of the whole refused page and of no other, no name or username, no completion sent; a 503 on a page and a refused begin name no id. M not run |
| RC3-10 | The new live test | `AUsersOwnNameWithAnUnpairedSurrogate_…` (account REST, 204, a receiver that answers 400 to a body System.Text.Json cannot read) is not flaky: the whole `KeycloakProvider` category twice, the test alone twice. It is not vacuous per field: M — one field's cleaning removed from the reader (last name only, then username only): the provider's unit tests fail | L, M | ☑ `Category=KeycloakProvider`: 13 passed + 1 skipped inside `verify.sh`, then 14 passed + 1 skipped on its own (the skipped one is QA's D-5 live test); `AUsersOwnNameWithAnUnpairedSurrogate_…` alone twice: 1/1, 1/1. Per field: the mutation of RC3-1 |
| RC3-11 | Every path goes through the one place | No provider source outside `ProjectedUserReader` reads a projected string off a Keycloak model (`getUsername`, `getFirstName`, `getLastName`, `getEmail`, `getId`) to send or to log it | R | ☑ R: `getUsername`, `getFirstName`, `getLastName`, `getEmail` and `getId` of a user are called only in `ProjectedUserReader`; `ChangeCapture` and `KeycloakRealmSource` go through `read` |

#### S2 — U+0000 in a pushed text field is a 400

| # | Behaviour the fix changed | Scenario and check | Kind | Status |
|---|---|---|---|---|
| RC3-12 | B-1: U+0000 in each of the five fields | `id`, `username`, `firstName`, `lastName`, `email` with U+0000 (JSON escape `\u0000`): 400 `user_directory.validation_failed`, the field's error key its JSON path (`changes[0].id`, …) with reason `user_directory.value_invalid`, no 500; the value never echoed in the answer or the log; nothing written (no row, no row changed); an entry among good ones refuses the whole request and applies none of them; the same in a deletion entry (`deleted: true`) | I | ☑ I: dev `PushNulCharacterTests`; QA `PushNulCharacterQaTests` — a deletion with U+0000 in each field (the held user stays active, `changes[0].<field>` `value_invalid`), and the refusal echoes neither the text nor its escape in the answer or the log (5 fields). M: the NUL check taken out of `UserPushValidation`: 25 dev and QA tests failed (a 500 each), reverted |
| RC3-13 | B-3: the same on a page | The five fields, key `entries[0].…` (and `entries[2].…` among good ones); the page is not recorded: the snapshot's page counter is unchanged, so a corrected page with the same number is then applied (not answered with stored counts) and the completion counts hold | I | ☑ I: `ARefusedPage_IsNotRecorded_ACorrectedPageWithTheSameNumberIsApplied_…` (the corrected page 0 is applied — counts 1 added, not stored ones — and the completion holds), `APage_WithTheBadEntryThird_NamesEntryTwoOnly` |
| RC3-14 | What is not refused | Other control characters (U+0001, U+0008, tab, LF, CR, U+001F, U+007F, U+0085, U+2028, U+200B, U+FEFF, noncharacter U+FFFF), a valid emoji, U+FFFD, in each of the five fields: 200, stored and read back unchanged. A raw 0x00 byte in the body (not an escape) is invalid JSON: 400 `push_malformed` | I | ☑ I: `EveryOtherCharacter_IsStoredAsSent_InEveryTextField` ×17 (U+0001, U+0008, tab, LF, CRLF, U+001F, U+007F, U+0085, U+2028, U+2029, U+200B, U+FEFF, U+FFFF, U+FFFD, an emoji, a supplementary ideograph, a pair between controls), each in all five fields, the id included, read back as sent; `ARawNulByteInsideAJsonString_IsMalformed_…`. U+FFFE is not in the list: D-5 |
| RC3-15 | Boundaries and order of the checks | `username` that is only U+0000 (one character): `value_invalid`, not `value_required`; an empty username still `value_required`; 256 characters with a U+0000 among them: `value_too_long` first (the resolution note); null for the three nullable fields still means empty; U+0000 beside another violation in the same entry (a moment out of range): every violation reported, none a 500 | I | ☑ I: `AUsernameOfOnlyU0000_IsInvalid_NotRequired_…`, `ATextOver255CodePointsWithU0000_IsTooLongFirst_…` (and every violation of one entry reported: `firstName` and `moment`), `NullNames_AndANullEmail_StillMeanEmpty_…` |
| RC3-16 | An unpaired surrogate on MUSTASK's side stays 400 `push_malformed` | The unchanged residue (the provider never sends it): the escape `\uD800` and a lone low surrogate escape: 400, not 500, nothing written (the probe is in `PushChannelSecurityProbeTests`) | I | ☑ I: `AnUnpairedSurrogateEscape_IsMalformed_NotAServerError_…` ×4 (low half, high half, halves beside text): 400 `push_malformed`, 0 rows |
| RC3-17 | M: the check is per field | One field's U+0000 check taken out in a scratch copy: its test fails (a 500 or a write) and the other nine pass | I, M | ◐ one shared check serves all five fields, so the mutation of RC3-12 fails the tests of all five at once; a per-field mutation does not exist |

#### S3 — the envelope is checked before the body is read

| # | Behaviour the fix changed | Scenario and check | Kind | Status |
|---|---|---|---|---|
| RC3-18 | Unsigned or malformed envelope, oversized body: 401, 0 bytes read | On each of the four routes: no headers; only a timestamp; only a signature; a malformed timestamp (`abc`, empty, a fraction, a negative, overflow, a sign, spaces, full-width digits); a malformed signature (no `v1=`, upper-case hex, 63 and 65 digits, non-hex, `V1=`); a stale and a future timestamp (±301 s); a header sent twice; a Bearer token and nothing else — each with a body over 4 MiB: 401 `push_unauthorized`, the same answer as without the body, a counter on the request stream reads 0 bytes. Chunked, with no `Content-Length`, the same | I | ☑ I: `PushEnvelopeQaTests.ABadEnvelope_WithABodyOverTheLimit_…` ×26 shapes (no headers; one header only; timestamp abc, empty, a fraction, a negative, a plus sign, overflow, hex, scientific, full-width digits; signature without `v1=`, upper-case hex, `V1=`, `v2=`, 63 and 65 digits, non-hex, empty; stale and future by 400 s with a right HMAC; each header twice; a Bearer alone and beside a malformed signature), each with a 4 MiB + 1 body: 401 `push_unauthorized` and the server read 0 bytes; `EveryRoute_Refuses…` (four routes × five shapes); `AChunkedUnsignedBody_…` (8 MiB, no Content-Length): 401, 0 bytes |
| RC3-19 | A well-formed envelope with a wrong HMAC | A wrong secret, an altered body, another route's signature, a Bearer beside: 401 after the body is read (the counter reads the whole body), nothing changes; with a body over 4 MiB: 400 `push_malformed`, the signature not yet checked (B.2 says so); with a body of exactly 4 MiB (4 194 304 bytes, whitespace-padded JSON, correctly signed) accepted, one byte more: 400 | I | ☑ I: `AWellFormedEnvelope_WithAWrongSignature_IsReadAndThenRefused_401` (wrong secret, altered body, another route's signature: 401, the whole body read, nothing written), `…WithABodyOverTheLimit_IsMalformed_WhateverTheSignature` (400 with the right secret and with a wrong one), `TheBodyBound_IsExactly4MiB_ForASignedRequest` (4 194 304 bytes signed → 200 and the user held; one byte more → 400) |
| RC3-20 | The envelope check and the full check agree on what a good envelope is | Differential: for each timestamp and signature form in RC3-18, with a body correctly signed, the status the whole pipeline gives is the one the contract implies (a well-formed one → 200, a malformed or stale one → 401). The window's edges: +300 s and −300 s with a valid HMAC → 200 on all four routes; ±301 s → 401 with 0 bytes read. Header names in any letter case → 200. A request that was valid before the change is valid now (all four routes, `PushChannelAuthenticationTests`) | I | ☑ I: `TheFourOperations_AreAcceptedAtTheEdgesOfTheWindow` (+300 s and −290 s on B-2, B-3, B-4, B-1); U: `PushSecretAuditQaTests.TheEnvelopeCheck_HasTheSameWindowAsTheFullCheck` (±300 accepted, ±301 stale, the full check agrees); `PushChannelAuthenticationTests` and `PushTimestampWindowTests` green. Header-name case: by construction (ASP.NET's header dictionary), not run |
| RC3-21 | The answer says no more than the contract says | The 401 for: no headers, malformed headers, stale, wrong secret, a Bearer instead — the same status, content type, `errorCode`, title and detail, no extra header, no `WWW-Authenticate` that differs between them (bodies compared after the trace id is taken out). An unreadable-body 400 is told apart from a 401 only for a body over 4 MiB with a well-formed envelope, as B.2 states. No log line, answer or error says which check failed beyond "reason, route" (B.2 allows the reason in the log, not in the answer) | I | ☑ I: `EveryRefusal_LooksTheSame_ToTheCaller` — seven kinds (no headers, malformed timestamp, malformed signature, stale, Bearer, wrong secret, an oversized unsigned body): one status, one content type, the same properties and headers, trace id taken out |
| RC3-22 | The refusal is still logged, once, without a person | A refused envelope logs its reason and the route (NFR Logging), no body, no secret, no timestamp or signature value; a flood of unsigned bodies writes no more lines than before. The Warning of S3's two-lines (the verifier's and the exception handler's) is as the review described, no new third line | I | ☑ I: `ARefusedEnvelope_IsLoggedOnce_WithItsReasonAndRoute_NoValues` (three refusals: a `MalformedHeader` line each with the route, at most six warnings in all, no header value, body or secret in the output); U: `TheEnvelopeStep_LogsNothingForAGoodEnvelope_…` |
| RC3-23 | A real socket, not the in-process host | A client that sends a large unsigned body to a real Kestrel gets a 401, not a connection reset (the in-process test host does not have Kestrel's draining). Run if the test host can serve over Kestrel; otherwise recorded as unproven and why | I (stretch) | ☑ I: `ARealKestrel_AnswersALargeUnsignedBody_…`: `UseKestrel(0)` on the factory (.NET 10), a real socket (port ≠ 80), ten rounds of a 4 MiB + 1 unsigned body: ten 401 `push_unauthorized`, no reset |
| RC3-24 | M: the early check is the point of the fix | The envelope step moved back after the body read in a scratch copy: the unsigned-oversized cases fail with 400 (or the counter reads the body); restored | I, M | ◐ not run: taking the envelope step out of `SignedPushBody` in a scratch copy was refused by the permission classifier ("Security Weaken") and not pursued by other means. In its place: QA's and the developer's counter tests read the request stream from a middleware ahead of the pipeline — 0 bytes against the control's whole body — so what they measure is that step |

#### S4 — a request accepted under `PreviousSecret` is audited

| # | Behaviour the fix changed | Scenario and check | Kind | Status |
|---|---|---|---|---|
| RC3-25 | The Warning | A request signed under `PreviousSecret` and accepted (any of the four routes): 200 as before, one Warning naming `UserDirectory:Push:PreviousSecret`; three more inside the hour: no more lines; after the hour (a settable clock): again, once. The line carries neither secret (not the current, not the previous, not a prefix of either), no body, no name | I | ☑ I: dev `PreviousSecretAuditTests` (host: one Warning for three requests, key not value); U: `TheWarning_NamesTheKey_AndCarriesNoSecretOrPrefixOfOne` (no prefix of 8 characters or more of either secret in the line), dev `TheWarning_IsRateLimited_AnHourApart` |
| RC3-26 | The Warning does not fire when it should not | Under the current secret: none, with `PreviousSecret` configured or not; a request refused (wrong HMAC, stale) by anyone: none; a request signed under a third secret: none (401); `PreviousSecret` equal to `Secret`: accepted under the current secret, no Warning; the envelope-only step logs nothing; `Check` alone is free of side effects (the verifier's other callers) | I | ☑ U: `NoWarning_ForTheCurrentSecret_ARefusal_AThirdSecret_OrASecretConfiguredTwice` (the current secret; a stale request under the previous one; a third secret; a missing signature; `PreviousSecret` equal to `Secret`: none); dev `TheCheckAlone_LogsNothing` |
| RC3-27 | Once an hour holds under concurrency | Fifty requests under `PreviousSecret` at the same time: exactly one Warning. A clock that steps backwards does not produce a Warning per request | I | ☑ U: `FiftyConcurrentRequestsUnderThePreviousSecret_WarnExactlyOnce_InTwoHundredRounds` (200 fresh verifiers, 50 threads released together: exactly one Warning each), `AClockThatStepsBack_…` (one; five requests two hours back: no more; the next 61 minutes after the first) |
| RC3-28 | The startup notice | `PreviousSecret` configured: one Information line at startup naming the key, never a value; not configured, an empty string, whitespace: no line and no acceptance of a request signed with an empty key; a `PreviousSecret` shorter than 32 characters: what happens (start refused, or ignored) is as ADR-0025 § 3 and B.1 say, and the value is not printed either way | I | ☑ U: `TheStartupNotice_NamesTheKeyOnce_…` (Information, the key, no value; nothing when not configured), `AnEmptyOrMissingPreviousSecret_IsNone`, `AShortPreviousSecret_StopsTheStart_…` (the message names `UserDirectory:Push:PreviousSecret`, never the value; the same for `Secret`), `ASignatureMadeWithAnEmptyKey_IsRefused_…` (with and without a previous secret); dev: the host-level startup line |
| RC3-29 | M: the Warning is not decorative | A scratch copy that warns on the current secret, and one that warns on every request: the tests fail | I, M | ☑ M: the Warning raised for every request while the key is configured: 3 tests failed (dev `ARequestAcceptedUnderTheCurrentSecret_IsNoWarning_…`, `TheWarning_IsRateLimited_…`, QA `NoWarning_ForTheCurrentSecret_…`), reverted |

#### S5 — the ids of deletions are logged

| # | Behaviour the fix changed | Scenario and check | Kind | Status |
|---|---|---|---|---|
| RC3-30 | The count and the ids line | A B-1 with a deletion of a held user, of an id not held (added as deleted) and of an id twice in the request: the Information line ends "deletions applied N" with N the number of **distinct ids newly deleted**, the second Information line names those ids, once each; the counts of `FeedCounts` are unchanged by the fix (`added + changed + unchanged + heldAsDeleted = received`) | I | ☑ I: `DeletionAuditLogQaTests.EveryIdNewlyDeleted_IsCountedAndListedOnce_WhateverItsPreviousState` (a present user deleted with a moment older than its state, an absent user, an id never held deleted twice: "deletions applied 3", each id once; an absent user revived by a state entry is not listed); dev `DeletionAuditLogTests`, `ProjectionBatchTests` |
| RC3-31 | A deletion that applies nothing is not listed | A deletion of an already deleted user (a repeat in a later request, and in the same request after the first): N = 0 and no ids line. A state entry for an id held as deleted (`heldAsDeleted`) is not a deletion. A request with no deletion at all logs no ids line | I | ☑ I: `ARepeatedDeletion_WithOtherValuesAndANewerMoment_IsNotApplied_AndNamesNoId` (a deleted user, a deletion with another surname and e-mail and a newer moment: `unchanged 1`, 0 deletions, no ids line, the held surname unchanged), `AStateEntryForADeletedId_IsHeldAsDeleted_NotADeletion` |
| RC3-32 | Ordering inside one request | A state entry with a newer moment and a deletion with an older one, and the reverse, for one id (a deletion wins over any state): the id is listed as deleted exactly once, whichever order the entries come in; a state entry after a deletion in the same request changes neither N nor the list | I | ☑ I: `ADeletionAndANewerState_InEitherOrder_ListTheIdOnce_AndTheDeletionWins` |
| RC3-33 | No personal data | The deletion carries a surname, an e-mail and a username: none is in any line at any level (Information and Debug), none of the secret; only the ids. An id the provider sends is a UUID; an id that is not (a line feed, an escape character) is the log-forging question: the line is probed with LF/CR/U+001B in an id and the result recorded | I | ◐ no surname, e-mail or username in any line: dev `DeletionAuditLogTests`, QA `FiveHundredDeletions_…`. The log-forging question (an id holding a line feed) was not probed: production writes `CompactJsonFormatter`, which escapes it, the Development text template does not — O-3 |
| RC3-34 | What is logged is what was applied | A batch refused (400 for one entry) logs neither a count nor an ids line, and writes nothing; a batch of 500 deletions logs one ids line of 500 ids (size noted); a deletion that was applied inside a request whose later step fails (a database error) is not listed as applied — read from the handler | I, R | ☑ I: `ARefusedBatch_LogsNeitherTheCountsNorTheIds_AndDeletesNobody`, `FiveHundredDeletions_AreOneIdsLine_…` (one line, 500 ids, each once); read: the lines are written after `ExecuteInTransactionAsync` returns, so a failed save lists nothing |
| RC3-35 | M: an already-deleted id is listed | A scratch copy that lists every deletion entry: the repeated-deletion tests fail | I, M | ☑ M: every deletion entry listed (the outcome test taken out): 3 failed (dev `ADeletionRepeated_IsNotApplied_…`, QA two), reverted |

#### O-1 and O-4 — the recovery, in `Reconciler`

| # | Behaviour the fix changed | Scenario and check | Kind | Status |
|---|---|---|---|---|
| RC3-36 | The recovery asks for its run before it logs | `deliveryRecovered` calls `request()` first; with the order reversed in a scratch copy `aRecovery_asksForItsRunBeforeItLogsThatOneFollows` fails (M). The line it logs is true on each branch: a run under way covers it (the "covers" wording), none under way (the "follows" wording), a run begun after the failure covers it; no branch logs "follows" for a run that was not requested | P, M | ☑ M: `request()` and the log call swapped: `ReconcilerTest.aRecovery_asksForItsRunBeforeItLogsThatOneFollows` failed (1 of 139), reverted. Read: the line is true on both branches — a request either starts a run or sets the rerun flag; a run that covers the outage logs the other sentence and returns |
| RC3-37 | The re-check 2 results still hold | RC2-1..RC2-8 (`OutageRecoveryQaTest`, 13 tests; `ReconcilerTest`, `FeedSenderTest`) stay green after the reorder: one run per outage in both orders, per-outage mark, no run lost | P | ☑ `OutageRecoveryQaTest` 13/13, `ReconcilerTest` 25/25, `FeedSenderTest` — all inside the 139/139 build |
| RC3-38 | The wording | The Javadoc of `deliveryRecovered` says "should it fail, it schedules the retry itself" and nothing about a retry "already pending"; ADR-0025 § 5 says the same; no other sentence in the ADR, the plan or the class comments still says "its retry is already pending" | R | ☑ R: the Javadoc reads "should it fail, it schedules the retry itself"; ADR-0025 § 5 "(should one under way fail, it schedules the retry itself)"; no sentence in the ADR, the plan or the sources says "already pending" (re-check 2's own text above quotes the old words) |

#### Documents and the whole

| # | Behaviour the fix changed | Scenario and check | Kind | Status |
|---|---|---|---|---|
| RC3-39 | Contract, ADR and prose agree with the code | B.1 "Request body", B.2, B.3's user-entry paragraph, B.6 and Part C's `value_invalid` row say what the tests prove; ADR-0025 §§ 3, 4, 5 say the same as the contract (envelope order, `PreviousSecret` audit, the 400 as input the provider now prevents); no sentence anywhere still says a 400 is only "a contract bug", that the body is read before any header, or that U+0000 is a 500; `contracts/errors/user_directory.json` and `docs/error-codes.md` carry the same cases as Part C | R | ◐ R: contract B.1/B.2/B.3/B.6 and Part C, ADR-0025 §§ 3–5, plan § 5 and the `FeedSender` Javadoc agree with the code for S1–S5; `contracts/errors/user_directory.json` and `docs/error-codes.md` carry no new code, as stated. One sentence is wider than the code: B.3 says the provider "never sends" what MUSTASK refuses — true for U+0000 and a lone half, not for U+FFFE (D-5) |
| RC3-40 | Together | `docker build --target provider deploy/keycloak` (the provider's unit tests, count), `Features.UserDirectory` in the Api integration tests, Domain, Application, Contracts and Architecture tests, the opt-in live category twice, `bash scripts/verify.sh` | run | ☑ "Runs, re-check 3" below |

#### Runs, re-check 3

All 2026-10-04. The live stand was not used and no stand lock was taken: every behaviour was provable under
Testcontainers and the built image. Docker containers of the live fixture and Testcontainers were removed by their
fixtures and Ryuk; nothing of QA's is left running.

| What was run | Result |
|---|---|
| `docker build --target provider deploy/keycloak` at `319f169` (the developer's tests) | exit 0; `Tests run: 133, Failures: 0, Errors: 0, Skipped: 0` |
| the same with QA's first `ProjectedTextQaTest` | exit 1, 2 failures — both QA's own assertion: Jackson writes a valid pair as two escapes, not four UTF-8 bytes (O-1), which the test had assumed otherwise; and its 30 000 mock-built profiles took 105 s, cut to 200 000 plain texts plus 300 mocked profiles (2 s) |
| the same, corrected, then with two scratch mutations (the last name's cleaning out of `read`; `request()` and its log call swapped) | exit 0, 138; then exit 1, 18 of 139 failed (RC3-1, RC3-36); the scratch changes reverted with `git checkout` |
| `docker build --target provider --no-cache-filter provider` at the final test state | exit 0; `Tests run: 139, Failures: 0, Errors: 0, Skipped: 0` (`ProjectedTextQaTest` 6, `ReconcilerTest` 25, `OutageRecoveryQaTest` 13); BUILD SUCCESS |
| `dotnet test src/backend/tests/Mustask.Api.IntegrationTests`, QA's S2, S4 and S5 classes | first: 60 passed, 1 failed — `EveryOtherCharacter_IsStoredAsSent…` for U+FFFE: HTTP 500 (D-5); U+FFFE moved out of it into a skipped reproduction, then 41 passed + 1 skipped |
| a scratch probe, 9 noncharacters and private-use points × 5 fields on B-1 (deleted) | 500 only for U+FFFE in `firstName`, `lastName`, `email`; every other point and every field 200 |
| `dotnet test src/backend/tests/Mustask.Domain.Tests --filter ProjectedUserTotalityQaTests`, every scalar value, unskipped | 2 failed: U+FFFE in the first name, last name, e-mail (3 pairs) and U+FFFE as a search text — `ArgumentException` from `string.Normalize`; no other of the 1 112 064 scalar values; then skipped with D-5 |
| `dotnet test … --filter PushEnvelopeQaTests` | first 37 passed, 1 failed — QA's own: the \"altered body\" case replaced Cyrillic in a body whose Cyrillic is JSON-escaped, so nothing was altered; fixed, 38 passed (26 envelope shapes, four routes, chunked, 4 MiB exact, window edges, uniform refusals, logging, a real Kestrel) |
| `MUSTASK_KEYCLOAK_PROVIDER_TESTS=1 dotnet test … --filter LiveProviderQaTests` | first: 2 failed — the D-5 live reproduction (MUSTASK's reader and derivation throw `String contains invalid Unicode code points` on the provider's own bodies; Keycloak answered admin REST create: first name 201, last name 201, e-mail 201, username 400) and the emoji test, which had read the shared fixture's D-5 bodies too (window narrowed to its own). Then 1 passed (+ the D-5 test skipped) |
| scratch mutations of the .NET side: the NUL check out of `UserPushValidation`; the Warning raised for every request; every deletion entry listed (the envelope-step mutation was refused by the permission classifier and not run) | 25, 3 and 3 tests failed (RC3-12, RC3-29, RC3-35); the three files restored with `git checkout`, `git status` clean |
| `bash scripts/verify.sh` at `eeb12a8` (QA's tests committed; vs main) | exit 0, `RESULT: green`: prose, agent, shell and PowerShell-wrapper checks, backend build; Domain 122 passed + 2 skipped (D-5), Shared 53, Architecture 81, Application 51, Contracts 114, Infrastructure 89, Api.IntegrationTests 624 passed + 15 skipped (14 live, off in the plain run, and D-5's theory); `provider` bucket: `docker build --target provider` ok, then `Category=KeycloakProvider` Contracts 17, Api.IntegrationTests 13 passed + 1 skipped (D-5); frontend lint, build, 182 tests (after `npm ci` in `src/frontend`) |
| `Category=KeycloakProvider`, a second time on its own, with QA's account-REST probe added | exit 0; 14 passed, 1 skipped (19 s) |
| `bash scripts/verify.sh` at `3f5feb1` (the commit with these results; vs main) | exit 0, `RESULT: green`: Shared 53, Domain 122 + 2 skipped, Contracts 114, Architecture 81, Application 51, Infrastructure 89, Api.IntegrationTests 624 passed + 16 skipped (15 live and D-5's theory); `provider` bucket: build ok, Contracts 17, `Category=KeycloakProvider` 14 passed + 1 skipped (D-5's live test); frontend lint, build, 182 tests. The skipped reproductions of D-5 are the only tests not run; run unskipped they fail as described |
| `AUsersOwnNameWithAnUnpairedSurrogate_…` (the developer's S1 live test) alone, twice | 1/1 and 1/1 (2 s each, fresh container each) |

#### Defects, re-check 3

One open: **D-5**, major. S1–S5, O-1 and O-4 are verified as the review's resolution notes say; D-5 is the S1 class,
reached by a character the fixes did not consider.

**D-5 (major, open) — U+FFFE in a first name, last name or e-mail is a 500, which the provider retries without end.**

- What: `string.Normalize(NormalizationForm.FormC)` throws `ArgumentException` ("String contains invalid Unicode code
  points") for U+FFFE, and only for it of all 1 112 064 scalar values. `DirectorySearchText.Normalize` runs it for the
  normalised search name (first + last name) and e-mail while a user is created or changed
  (`ProjectedUser.Create`/`Apply`). Validation accepts U+FFFE (S2 refuses U+0000 only), the provider's cleaning (S1)
  handles lone surrogates and U+0000 only, and Keycloak accepts it. A consumer's search text holding U+FFFE throws in
  `DirectorySearchText.Parse` the same way (A.6 lists no such refusal).
- Reachable by any user: the account REST update of one's own profile answers 204 for `"firstName":"Сам￾"`
  (`LiveProviderQaTests.AUser_CanPutTheNoncharacterU_FFFE_InTheirOwnName_AndKeycloakAcceptsIt`, passes today), and
  admin REST creates it in the first name, last name and e-mail (201); the username is refused (400). Users have
  `manage-account` by default (the review's S1 analysis).
- Effect, as S1's: the provider pushes the user (the body is valid UTF-8 and JSON) and MUSTASK answers 500
  `http.internal_error`. The provider treats a 5xx as retryable: it requeues the batch with a back-off of 1 s doubling to
  60 s, without end, so the poisoned entry blocks every batch it is in — the change feed stops for everyone. The
  reconciliation page carrying the user fails the same way (FAILED, retried in 60 s) and no completion follows, so
  nothing lost is healed and nobody is marked absent (BR-UP-7, BR-UP-8, NFR Freshness). Until the user's name is
  edited again, or the user is deleted, nothing else gets through. No data is exposed; nothing is authorised by it.
- Reproduction (the reproductions are committed, skipped with the defect's name — remove each `Skip`):
  1. Api: `dotnet test src/backend/tests/Mustask.Api.IntegrationTests --filter
     "FullyQualifiedName~TheNoncharacterU_FFFE_IsStoredOrRefused_NeverAServerError"` — three cases, each answers 500.
  2. Domain: `dotnet test src/backend/tests/Mustask.Domain.Tests --filter ProjectedUserTotalityQaTests` — every scalar
     value, in the first name, last name and e-mail, and as a search text; reports the three pairs and the search text.
  3. Live: `MUSTASK_KEYCLOAK_PROVIDER_TESTS=1 dotnet test src/backend/tests/Mustask.Api.IntegrationTests --filter
     LiveProviderQaTests` — `ANoncharacterTheValidationAccepts_InAName_IsPushedInAFormMustaskCanDerive` creates the users
     through admin REST and applies MUSTASK's own request types, validation and derivation to every body the provider
     sent: `ArgumentException String contains invalid Unicode code points`.
- Fix directions, for the developer to choose: make MUSTASK's derivation total (so no accepted text can throw — the
  exhaustive Domain test is fix-agnostic: a value validation refuses, or one that derives, both pass); and/or refuse it
  at validation as `value_invalid` and have the provider replace it as it replaces a lone half — the contract (B.3: "the
  provider never sends what MUSTASK refuses") needs the second half whenever the first is chosen, because a 400 drops the
  whole batch. The search text is a consumer-facing read: it should be answered or refused as invalid, never a 500.
  Un-skip the four QA reproductions (an Api theory of three cases, two Domain tests, one live test) with the fix.
- Status: resolved (`bc32287`, `3570878`); QA's re-check is owed. Both halves of the fix, because B.3's "the provider
  never sends what MUSTASK refuses" needs both:
  - MUSTASK refuses. `UserPushValidation` refuses U+0000 or a Unicode noncharacter in each of the five text fields
    (`id`, `username`, `firstName`, `lastName`, `email`) as `user_directory.value_invalid`, at the field's path, on B-1
    (a deletion entry too) and on B-3. The length is checked first, so a text over 255 code points that holds one is
    `value_too_long`. A noncharacter is U+FDD0..U+FDEF and the last two code points of each of the seventeen planes
    (U+FFFE, U+FFFF, U+1FFFE … U+10FFFF): sixty-six values (`DirectorySearchText.IsNoncharacter`).
  - `Normalize` is total. `DirectorySearchText.Normalize` removes control characters, reads a noncharacter as U+FFFD,
    composes to NFC and, should the platform's composition throw `ArgumentException`, goes on with the text
    uncomposed. The stored search columns and a consumer's search text both go through it, so a search text holding a
    noncharacter is answered, read as U+FFFD (api-contract.md A.3), and never a 500.
  - The provider replaces. `ProjectedUserReader.wellFormed`, for the id, username, names and e-mail on the event,
    removal and reconciliation paths: a noncharacter becomes U+FFFD (one U+FFFD for a pair such as U+1FFFE), an
    unpaired surrogate becomes U+FFFD, U+0000 is dropped, everything else is kept.
  - The four reproductions are un-skipped and pass: the Api theory (3 cases: 400 `value_invalid` now), the two
    `ProjectedUserTotalityQaTests` (every scalar value, derived and as a search text) and the live
    `ANoncharacterTheValidationAccepts_InAName_IsPushedInAFormMustaskCanDerive`; the live account-REST test now also
    asserts the pushed form (`Сам�И`). Developer tests: `PushNoncharacterTests`, the cases in `DirectorySearchTextTests`
    and `ProjectedUserTests`, the provider's `ProjectedRealmAndReaderTest` and `ProjectedTextQaTest` (extended with the
    same set). RC3-14's list of characters stored as sent no longer holds U+FFFF (now refused, like U+FFFE); RC3-14
    and RC3-39 above are left as QA wrote them.

#### Observations, re-check 3 (not defects)

- O-1. Jackson writes a valid surrogate pair as two escapes (`😀`), not as four UTF-8 bytes. Valid JSON,
  joined by System.Text.Json; harmless. The developer's note on S1 — "valid UTF-8 with no escaped surrogate" — is true of
  cleaned halves and not of pairs. It makes the largest wire batch Keycloak's limits allow 3.85 MB, 92% of the 4 MiB bound
  (500 entries, every field at 255 UTF-16 units); the 255-code-point limit MUSTASK states would allow 7.7 MB. Keycloak's
  length check (that it counts UTF-16 units is assumed, not probed), not MUSTASK's, is what keeps a batch under the bound — worth a line in ADR-0025 § 4 if the bound is ever
  lowered.
- O-2. A username or id that the cleaning empties is pushed as `""`; MUSTASK answers 400 `value_required` and the batch
  is dropped (RC3-3). Reachable only by a username of U+0000 alone, which Keycloak's validators refuse and an unvalidated
  import into the stand's store would not. The provider could skip such a user with a log line naming its id.
- O-3. The ids of deleted users are logged at Information as one line, up to 500 ids; the ids are the provider's
  (Keycloak's UUIDs), but the contract does not shape them. Production logs `CompactJsonFormatter`, which escapes a line
  feed in an id; the Development text template would write it raw. Not probed.
- O-4. QA could not run the S3 mutation (the permission classifier refused weakening the binder in a scratch copy). The
  counter tests measure the early step directly (0 bytes read against the control's whole body), so the claim holds
  without it; the developer's note that four of five of his tests fail without the step is unconfirmed.

#### Verdict, re-check 3

blockers: 0 · majors: 1 open (D-5) · minors: 0 · S1, S2, S3, S4, S5, O-1, O-4 verified · result: fail — D-5 goes back to
the backend developer; QA re-checks it with the four reproductions un-skipped.

### Re-check 4 (D-5, after the security delta), 2026-10-04

qa-engineer, a clean context that wrote neither the code nor any earlier pass or re-check. Scope: only the fix of
D-5 (re-check 3) as its `Status: resolved` line records it — `bc32287` (provider: a noncharacter becomes U+FFFD in
`ProjectedUserReader.wellFormed`), `3570878` (MUSTASK: `UserPushValidation` refuses U+0000 and the 66 noncharacters as
`value_invalid`, `value_too_long` first; `DirectorySearchText.Normalize` made total; api-contract.md A.3 "Matching",
B.3, Part C) and the four reproductions un-skipped. Loop economy (06-code-review.md "Every finding", 04-backend.md § 5
item 3): this is the one delta re-check over one fixed major; pass = no open blocker or major, minors are debts with a
proposed fix.

The plan below was written from D-5 with its Status line, `api-contract.md` (A.3, B.3, Part C `value_invalid`) and the
spec, before any fix code, test or diff was read (the plan's draft is in the handoff directory; this session is a
reading role and commits nothing). What the fix could have broken is the question of each row, beside whether it does
what it says. **M** — a mutation in a scratch copy, reverted with `git checkout`; **P** — a scratch probe written in
this pass, deleted before the end (`git status --short` empty), the content described in the row. Probes are not
committed; the proposals under Debts name the ones worth promoting.

#### The rows

| # | Behaviour the fix changed | Scenario and check | Kind | Status |
|---|---|---|---|---|
| RC4-1 | The four reproductions pass, un-skipped | Api theory `TheNoncharacterU_FFFE_IsStoredOrRefused_NeverAServerError` (3 cases); Domain `ProjectedUserTotalityQaTests` (2); live `ANoncharacterTheValidationAccepts_InAName_IsPushedInAFormMustaskCanDerive`; live account-REST `AUser_CanPutTheNoncharacterU_FFFE_InTheirOwnName_AndKeycloakAcceptsIt` (now also asserts `Сам�И` and that MUSTASK's reader takes the body) | I, U, L | ☑ Api theory by name: 3 passed (`lastName`, `firstName`, `email`); Domain 138 passed, 0 skipped; the live category 15 passed, 0 skipped, twice (21 s, 20 s). None of the four is skipped any more |
| RC4-2 | Validation refuses exactly U+0000 and the 66 noncharacters, in each of the five fields, on both apply paths | P (Domain): every one of the 1 112 064 scalar values in all five fields at once against an oracle built from the standard's definition (U+FDD0..U+FDEF, `plane << 16 \| 0xFFFE/0xFFFF` for 17 planes; 66 values); each of the five paths alone, and the 66 plus the values next to each of them, in six positions (alone, start, end, middle, doubled, between two emoji) × B-1 state, B-1 deletion, B-3 → the key is `changes[0].<field>` / `entries[0].<field>` with one code, `user_directory.value_invalid`; the neighbours (U+FDCF, U+FDF0, U+FFFD, U+10000, U+1FFFD, …) are accepted. P (Api): all 66 × 5 fields × {state, deletion} on B-1 and × page on B-3 (990 requests) → 400, the field's path and code in the answer, the character never echoed, 0 rows, and the page not recorded (a good page 0 with the same snapshot is then applied) | I, P | ☑ both probes passed; M below catches both |
| RC4-3 | The order of the two checks and the counting | P (Domain): 255 / 256 code points in each field; 254 + a noncharacter → `value_invalid`, 255 + a noncharacter → `value_too_long`; 255 astral noncharacters (510 UTF-16 units) → `value_invalid` (one code point each), 256 → `value_too_long`; 255 / 256 emoji; U+0000 the same. The dev's `ATextOver255CodePointsWithANoncharacter_IsTooLong_NotInvalid` and QA's `ATextOver255CodePointsWithU0000_IsTooLongFirst_…` | U, I | ☑ probe passed; M (d) catches the order |
| RC4-4 | Valid astral characters survive, end to end | P (Api): U+1F600, U+20000, U+2A6DF, U+2F800, a flag pair, U+E0001, U+1D11E, U+10000, and the private-use and last assigned points beside noncharacters (U+1FFFD, U+3FFFD, U+FFFFD, U+10FFFD) in each of the five fields on B-1 and on B-3: 200, stored as sent (id, username, names, e-mail), and a search by `н<char>н` finds the user. P (live, built image, real Keycloak): see RC4-6 for the three provider paths | I, P | ☑ passed; the dev's `EveryOtherCharacter_…` (U+1F600, U+20000 in five fields) and RC3-6's tests are in the same green run |
| RC4-5 | The provider replaces what MUSTASK refuses — the two languages agree | P (cross-implementation): a scratch Java test dumped 8 000 seeded random texts of 0–8 units from a 40-unit alphabet (lone halves of both kinds, valid pairs, U+0000, U+FFFD, U+FFFE, U+FFFF, U+FDD0, U+FDEF, U+FDCF, U+FDF0, astral noncharacters U+1FFFE, U+10FFFF, U+FFFFE, U+2FFFE, neighbours U+1FFFD, U+10FFFD, `ё`, combining marks, `%`, `_`, `\`) as `original → ProjectedUserReader.wellFormed(original)` in UTF-16 hex (taken from the build log); a scratch .NET test read them: (1) the Java output equals an independent .NET oracle (an explicit UTF-16 walk: lone surrogate and noncharacter → U+FFFD, a noncharacter pair → one U+FFFD, U+0000 dropped, the rest kept); (2) it is strict-UTF-8 well-formed; (3) `UserPushValidation` accepts it in each of the five fields (so "the provider never sends what MUSTASK refuses" holds on the corpus); (4) `Normalize(original) == Normalize(cleaned)`, so a text copied from Keycloak finds the user. More than 1 500 of the texts held a noncharacter and more than 2 000 changed. The same property, in .NET alone, over 300 000 seeded texts: `Normalize` never throws, its output is well-formed with no noncharacter or control, `Parse` never throws and its pattern is well-formed, the provider's cleaning (re-implemented from its rule) normalises to the same text as the original | P | ☑ 8 000 / 8 000 and 300 000 / 300 000. The scratch Java test is deleted; the Java side was run in the provider's own build (159 tests = 158 + the dump, 0 failures) |
| RC4-6 | The three provider paths on the built image against a real Keycloak | P (live): admin REST creates `u1` (U+FDD0 in the first name, U+FFFF in the last name, U+FDEF in the e-mail), `u2` (U+1FFFE and U+10FFFF as surrogate pairs), `u3` (emoji, U+20000, U+2F800, a flag), `u4` (NUL, three noncharacters and a lone half beside a pair) and `u5` (U+FDD0 in the username). Keycloak answered 201, 201, 201, 400, 400: the NUL and a username with a noncharacter are refused by Keycloak, the rest taken. Event: each field pushed as expected (`A�B`, `C�D`, `e�@example.test`; the valid astral text unchanged). Page: an outage (receiver answers 401) and its recovery; the reconciliation's pages carry the same texts for the three users. Removal: the deletion entry carries the same cleaned last state. All 10 bodies of the window read through MUSTASK's own `ChangesRequest` / `PageRequest`, `UserPushValidation.Validate` and `ProjectedUser.Create`: no exception | P | ☑ passed. The users are deleted again; the Testcontainers Keycloak is removed by its fixture |
| RC4-7 | The search path answers or refuses, never a 500 | P (Domain, `Parse`): `ab` + U+FFFE / U+FDD0 / U+10FFFF / U+1FFFE / a lone high half / a lone low half / a reversed pair, each → a `Pattern` whose `LikePattern` is `%ab�cd%` (the lone halves read as U+FFFD, as `EnumerateRunes` does); a text of one noncharacter, one lone half, one astral noncharacter, or a noncharacter between spaces → `TooShort`; a noncharacter and a letter → `Pattern` (a pair counts one code point); two noncharacters → `Pattern`; `a` + NUL → `TooShort`; `%`, `_`, `\` still escaped around a noncharacter; 100 / 101 noncharacters (BMP or astral) and 101 lone halves → `Pattern` / `TooLong`. Every scalar value as a search text (the dev's Domain test) is green. P (Api, through `SearchAsync` and PostgreSQL): a user `Сам�И` is found by `сам` + each of U+FFFE, U+FDD0, U+FDEF, U+FFFF, U+1FFFE, U+10FFFF, a lone high half, a lone low half and U+FFFD itself, and by `са\0м`; `Сам` (no U+FFFD) is not found by the noncharacter form; nine odd texts (`\uD800\uD800`, `%￾%`, `\\￾`, …) are answered; one noncharacter or one lone half → nobody; 100 noncharacters → nobody, 101 → `ValidationException` (the contract's refusal, not a 500); an e-mail `an�na@…` is found by `an` + U+FFFE and by `an` + U+FFFD and comes back unchanged | U, I, P | ☑ passed. One finding outside the delta: Debt M-2 |
| RC4-8 | Nothing else in `Normalize` moved | P: `СЕМЁН`, `Семён` (decomposed), `e` + U+0301, Hangul jamo → the syllable, `😀А`, U+20000, the empty string unchanged; `a` + U+FFFE + U+0301 → `a�` + U+0301 (U+FFFD blocks the composition, as it does in the provider's text); the dev's four `Normalize_FoldsYoAndCase` cases green | U, P | ☑ passed. Recorded, not a defect and not the fix's: `Normalize` is not idempotent for `ё` + U+0308 (the `ё` → `е` fold runs after the composition, a second pass then composes `е` + U+0308 into `ё`); both sides of a match are normalised once, so a match is unaffected |
| RC4-9 | The 255-code-point limit still counts code points after replacement | P (Domain): the replacement never lengthens (a lone half → one U+FFFD, a pair → one); 255 U+FFFD accepted, 256 refused; 255 emoji (510 units) accepted, 256 refused; what Keycloak's own 255-unit limit allows, once replaced, is at most 255 code points and always accepted | U, P | ☑ passed |
| RC4-10 | The documents say what the code does | R: api-contract.md B.3 (the five fields, the 66 values, `value_invalid`, `value_too_long` first, "the provider drops U+0000 and replaces an unpaired surrogate and a noncharacter", a surviving surrogate is `push_malformed`), Part C `value_invalid` (U+0000 or a noncharacter, `value_too_long` instead when also over the limit), A.3 "Matching" (a noncharacter read as U+FFFD, answered never an error), ADR-0025 § 4 (the 400, "holds for the whole of B.3's text rule"), plan-backend.md § ProjectedUserReader, the `ProjectedUserReader` and `FeedSender` Javadoc; `contracts/errors/user_directory.json` and `docs/error-codes.md` carry the one `value_invalid` code with its one message, no new code; `Contracts` tests (114) green, `ErrorCodesReferenceTests` included | R | ☑ every claim matches the code and the tests above. One sentence is narrower than the code: Debt M-3 |
| RC4-11 | M: MUSTASK's validation rule alone | (a) the noncharacter test taken out of `UserPushValidation.HoldsAnUnacceptableCharacter` (only U+0000 left): Api `Features.UserDirectory` 21 failed — the dev's `PushNoncharacterTests` ×20 (every kind in a name ×8, `SnapshotPage_…` ×5, `Changes_WithU_FFFE…` ×5, the deletion, the entry-naming test) and QA's 66 × 5 matrix; the Domain tests: **none fails**, 138 / 138 green (only QA's three scratch probes fail) — Debt M-1; the reproduction theory stays green by design (stored 200 once the normaliser is total). (b) the block's upper edge moved, `0xFDEF` → `0xFDEE` in `IsNoncharacter`: 5 failed (dev `IsNoncharacter_IsExactlyTheStandardsSixtySixValues`, `Normalize_ReadsANoncharacter…(65007)`; three probes). (c) the plane rule limited to the basic plane (`0xFFFE or 0xFFFF`): 8 failed (dev `…IsExactlyTheStandardsSixtySixValues`, `Normalize_Reads…(131070)` and `(1114111)`; five probes). (d) the order swapped, the noncharacter check before the length check: Api 2 failed (dev `ATextOver255CodePointsWithANoncharacter_IsTooLong_NotInvalid`, QA `ATextOver255CodePointsWithU0000_IsTooLongFirst_…`). (e) the normaliser's totality taken out (the noncharacter reading and the `try`/`catch`) with validation intact: Domain 15 failed — 12 of the branch's (`Normalize_ReadsANoncharacter…` ×6, `…WhenTheCompositionStepThrows…`, `Parse_TwoNoncharacters…`, `Parse_ATextWithU_FFFE…`, `Normalize_IsTotal_OverEveryScalarValue`, `ProjectedUserTests.Create_AndApply…`, `ProjectedUserTotalityQaTests.EveryScalarValue_AsASearchText…`) and three probes; `EveryScalarValueTheValidationAccepts_IsDerivable_…` stays green, as it must — validation refuses what would throw | M | ☑ every mutation is caught; each file restored with `git checkout`, `git status --short` empty. (The live reproduction against a provider without the replacement was not run; it needs a Dockerfile edit — the task excluded it) |
| RC4-12 | Together | `docker build --target provider deploy/keycloak`, Api `Features.UserDirectory`, Domain, the live category twice, `bash scripts/verify.sh` | run | ☑ "Runs, re-check 4" |

#### Runs, re-check 4

All 2026-10-04, at `cd28f35`. The live stand was not used and no stand lock was taken; the Keycloak image and the
database were Testcontainers', removed by their fixtures. The containers on the machine that are not QA's (a
`postgres:latest` up for 47 hours, an exited `postgres:18.3`) were already there.

| What was run | Result |
|---|---|
| `docker build --target provider deploy/keycloak` | first, from the cache: exit 0, 4 s (no tests shown); then with `--no-cache-filter provider`: exit 0, `Tests run: 158, Failures: 0, Errors: 0, Skipped: 0` (`ProjectedRealmAndReaderTest` 36, `ProjectedTextQaTest` 6, `ReconcilerTest` 25, `OutageRecoveryQaTest` 13, …), BUILD SUCCESS |
| the same with the scratch corpus-dump test | tests ran, 159, 0 failures; the build then stopped in Maven's packaging (`Failed to read artifact descriptor for … maven-archiver`, a plugin download), exit 1 — a network failure of the build, not of the code; the scratch test was deleted and the next provider builds (`verify.sh`'s) succeeded |
| `dotnet test src/backend/tests/Mustask.Domain.Tests` | exit 0, 138 passed, 0 skipped (before QA's probes; 144 with them) |
| `dotnet test … Mustask.Api.IntegrationTests --filter FullyQualifiedName~Features.UserDirectory` | exit 0, 314 passed (the branch's 310 and QA's four probes), 15 skipped — the 15 live tests, off without the opt-in |
| the theory `TheNoncharacterU_FFFE_IsStoredOrRefused_NeverAServerError`, by name | 3 passed |
| `MUSTASK_KEYCLOAK_PROVIDER_TESTS=1 dotnet test … --filter Category=KeycloakProvider`, twice | exit 0 and exit 0; 15 passed, 0 skipped (21 s) and 15 passed, 0 skipped (20 s) |
| QA's live probe (RC4-6), alone | 1 passed (5 s) |
| scratch mutations (a)–(e) | RC4-11; the validation file and `DirectorySearchText.cs` restored after each |
| `bash scripts/verify.sh` at `cd28f35`, the tree clean | **exit 0, `RESULT: green`**: prose, agent, shell and PowerShell-wrapper checks, backend build; Shared 53, Architecture 81, Application 51, Contracts 114, Domain 138, Infrastructure 89, Api.IntegrationTests 656 passed + 15 skipped (live, off in the plain run); the `provider` bucket: `docker build --target provider` ok, Contracts 17, `Category=KeycloakProvider` 15 passed; frontend lint, build, 182 tests |

#### Defects, re-check 4

None open. D-5 is verified: the fix does what its Status line says, on the three provider paths, in MUSTASK's
validation, in the derivation and in the search path, and what it could have broken (valid astral characters, the length
limit, the order of the checks, NFC of ordinary text) is intact. Three minors, debts with their fixes; none blocks.

**Debts (minor)**

- **M-1. The Domain tests do not pin the validation rule.** With the noncharacter test removed from
  `UserPushValidation` (RC4-11 a) all 138 Domain tests stay green; only the Api tests (Testcontainers, the slow layer)
  catch it, 21 of them. The reproduction `ProjectedUserTotalityQaTests` is fix-agnostic by design and cannot. Fix: add a
  Domain test of the rule itself — QA's scratch test is the shape: every scalar value in all five fields against the
  standard's 66 values, the five paths alone, `value_invalid` at the path, `value_too_long` first; it ran in about
  1 s.
- **M-2. `LookupAsync` / `SearchAsync` with `AmongIds` throws on an id holding a lone surrogate or U+0000.** Probed
  outside the delta, and not touched by the fix: an id with a lone high half → `EncoderFallbackException` (Npgsql's strict
  UTF-8), an id with U+0000 → `PostgresException`; U+FFFE, a pair and a noncharacter pair are answered (unknown /
  nobody). A.4 says an id is "opaque text, compared ordinally, not checked for shape; `""` or an over-long id is simply
  unknown", and names no refusal for it; so an id that cannot have been stored should be unknown too, not a throw. The
  ids are the caller's own (a token's `sub`, a stored id), not a user's text, so it is a programming error rather than
  an input — but "never a 500" holds for the text and not for the ids. Fix: answer an id holding U+0000 or an unpaired
  surrogate as unknown (nobody for `AmongIds`) before the query, or say in A.4 that it is a precondition.
- **M-3. A.3 names a noncharacter in a search text and not an unpaired surrogate.** The code reads a lone surrogate as
  U+FFFD too (`EnumerateRunes`; found by `сам` + U+D800, tested above), and the dev's `Normalize_TakesALoneSurrogate_…`
  pins it, but A.3's "Matching" row and the `Normalize` summary do not say so. Fix: "a Unicode noncharacter or an
  unpaired surrogate read as U+FFFD".

**Observations (not defects)**

- O-5. The two sides keep the noncharacter set in two places (`DirectorySearchText.IsNoncharacter`,
  `ProjectedUserReader.isNoncharacter`); each is pinned against the standard's definition by its own exhaustive test,
  and QA's 8 000-text differential found no disagreement. A shared vector file (the Contracts tests already hold the
  signature vectors) would make the agreement a test, not a coincidence.
- O-6. A row stored before the fix with U+FFFF in a name (RC3-14 stored it as sent) keeps that text in its search
  columns, and a search for it now reads U+FFFF as U+FFFD; the provider replaces it on the next event or reconciliation
  and the row is rewritten. Only a stand that ran the branch before `3570878` can hold such a row; mod-014 is not
  merged. Not run.
- O-7. The noncharacter replacement is by code point and 1:1 (a pair becomes one U+FFFD), so cleaning never lengthens a
  text; the stand's Keycloak refuses U+0000 and a noncharacter in a username (400, RC4-6 `u4`, `u5`), so a username or id
  that cleaning empties (re-check 3, O-2) is still reachable only through an unvalidated import.

D-5's Status line in "Defects, re-check 3" reads "QA's re-check is owed": it is now verified in this re-check; the
orchestrator may append "Verified, re-check 4" to it.

#### Verdict, re-check 4

blockers: 0 · majors: 0 · minors: 3 (debts M-1..M-3) · D-5 verified · result: pass
