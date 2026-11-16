# Review: feat-078-description-images

## Round 1 — requirements

Reviewer: requirements-reviewer (clean context), 2026-11-16. Inputs: spec.md,
stories US-1..US-5, status.md Notes; cross-checked against feat-018 spec
(BR-RT-*), feat-059 spec (BR-FB-6, BR-FB-20 (a)-(e)), feat-059 design.md
(S2, S2a, S3a) and ADR-0041 decision 6 (feat-059 worktree, unmerged), mod-005
BR-DR-12, -15, -16.

### Coverage: inputs and rules → stories

| Input / rule | Covered by | Note |
|---|---|---|
| Human 2026-11-12: bytes in the DB, own schema (D-1) | BR-IM-17 (ADR), US-2, US-5; NFR Storage | design states volume and backup effect |
| Human 2026-11-12: large images compressed without visible loss (D-2) | BR-IM-4, US-2 | made measurable in R-6; PNG gap in R-12 |
| Formats and size cap (open) | BR-IM-2, -3; OQ-1, OQ-2 | for the human |
| feat-018 BR-RT-4 replaced, BR-RT-6 amended | BR-IM-9, -13, -18; US-1, US-3 | |
| feat-018 BR-RT-1, -3, -5, -8, -12 | BR-IM-7, -9, -12, -18; US-3 canonical cases | BR-RT-5 added to BR-IM-18 (R-11) |
| feat-059 BR-FB-20 (a) non-member pre-item upload | feat-059 (BR-IM-19, D-4); feat-078 ships the purpose slot | seam completed in R-1 |
| feat-059 BR-FB-20 (b) nothing in addresses | BR-IM-10; US-2, US-3, US-4 | |
| feat-059 BR-FB-20 (c) sender reads/adds through the feedback operation | feat-059; BR-IM-11 allows a purpose's own reader (R-1) | |
| feat-059 BR-FB-20 (d) unbound removed after 2 h, hourly | BR-IM-16 (D-3), US-5 "every purpose" | timing fixed in R-2 |
| feat-059 BR-FB-20 (e), BR-FB-6 image-only description | BR-IM-9, US-3 | |
| feat-059 BR-FB-13 purge leaves nothing | BR-IM-16 purge step, US-5 | |
| ADR-0041 decision 6 (c) stores through feat-078's storage | BR-IM-6, -19 | |
| mod-005 BR-DR-12, -16 purge | BR-IM-16, -18; US-5 | |
| Who uploads / who reads | BR-IM-5, -11; US-2, US-4 | |
| Crop (feat-018 D-5) | spun out, OQ-6 | for the human |

D-4 split check: feat-078 opens no write right nobody uses — the only upload
operation it ships is for an editor of an existing item; the purpose slot is a
data field, not a right. As first written, though, BR-IM-6, -8 and -11 were
closed rules that would have forbidden what feat-059 must build (R-1).

### R-1
Severity: major
Affects: BR-IM-6, BR-IM-8, BR-IM-11, BR-IM-19, US-1; feat-059 BR-FB-20 (c), (e), design S3a
Problem: The seam left to feat-059 was incomplete. (1) The editor took only its upload operation from the host, but feat-059's edit dialog shows the report's images to a non-member sender, who cannot use this task's read (BR-IM-11): the editor must also take the reference-to-address rule from the host, which in turn requires the stored markup to hold an identifier, not a route-bound address. (2) BR-IM-6 said an upload is bound "only by a description write of that same user to the item it was made for" and BR-IM-11 said "exactly those who may read the item": both, read literally, forbid feat-059's binding of a feedback upload made for no item and its sender's read. (3) BR-IM-8 would refuse feat-059's send, whose description references uploads made for no item.
Required resolution: scope BR-IM-6 and BR-IM-8 to the item-description purpose and this task's writes; let a purpose's own operation bind, check and read; make the editor take both upload and address from the host; state the stored reference is an identifier.
Status: resolved
Resolution: BR-IM-6 reworded per purpose; BR-IM-7 states the reference is an identifier and the address is derived by the showing surface; BR-IM-11 scoped to "this task's image read" with a purpose's own reader allowed; BR-IM-19 gains the address seam and the per-purpose reference check; US-1 gains the host-seam scenario.

### R-2
Severity: major
Affects: US-5 "An upload nobody saves", "A save binds before the sweep"; BR-IM-16
Problem: US-5 removed an upload made at 10:05 by the 12:00 sweep, 1 h 55 min after the upload, contradicting BR-IM-16 "never earlier" than 2 hours. Its "save binds before the sweep" scenario was vacuous for the same reason (U was not eligible at 12:00 anyway).
Required resolution: align the scenario times with BR-IM-16.
Status: resolved
Resolution: kept at 11:00 and 12:00, removed at 13:00 (2 h 55 min); the binding scenario moved to a save at 12:50 and the 13:00 sweep.

### R-3
Severity: major
Affects: BR-IM-8, BR-IM-16, US-3, US-5
Problem: A save validates its references and the sweep removes uploads concurrently; nothing forbade the interleaving "save checks U valid → sweep removes U → save stores a reference to U", which leaves a permanently broken image no test would catch.
Required resolution: state the two allowed outcomes and test the race.
Status: resolved
Resolution: BR-IM-16 "a bind and the sweep never interleave"; US-5 scenario "A save and the sweep at the same moment" and a forced-race integration test row.

### R-4
Severity: major
Affects: US-2 "The caps"; BR-IM-3
Problem: The "bomb" row (20 000 × 3 000 = exactly 60 000 000 pixels, exactly 20 000 px on a side) expected a refusal, but BR-IM-3 says "at most", so it must be accepted; the test as written would force an off-by-one cap.
Required resolution: make the bomb exceed the cap and add the exact-boundary accept.
Status: resolved
Resolution: boundary 20 000 × 3 000 accepted; the bomb is 20 000 × 20 000.

### R-5
Severity: major
Affects: BR-IM-3, BR-IM-4 (6), OQ-2, OQ-7, NFR Safety
Problem: An animated GIF/WebP is capped only per frame; 10 MB of compressed frames can declare hundreds of 60 MP frames, a decompression bomb that keeping animations (OQ-7) makes reachable, contradicting "none decoded past the caps".
Required resolution: a cap on frames and on total pixels over all frames, checked from the header.
Status: resolved
Resolution: BR-IM-3 adds at most 1 000 frames and 600 000 000 pixels over all frames; US-2 gains two refusal rows; US-1 a notice «Анимация слишком большая: не больше 1 000 кадров.»; OQ-2 names the numbers for the human.

### R-6
Severity: major
Affects: BR-IM-4, US-2
Problem: "Without visible loss" was not fully testable and partly self-contradictory. (a) SSIM ≥ 0.98 named no reference picture (the upload, or the upload after orientation and scaling?) and no method, so two correct implementations could disagree. (b) "Metadata removed" would strip an embedded colour profile, so an iPhone Display P3 photo changes colour, while "colours look the same" was not checkable. (c) A JPEG ≤ 1 MB with an orientation flag "is not re-encoded" yet must have the orientation "applied" (rotated pixels, no flag): rotating pixels is a re-encode.
Required resolution: define the SSIM reference and method; say what happens to a colour profile; let a non-re-encoded image keep its orientation field.
Status: resolved
Resolution: BR-IM-4 (1) keeps the orientation field when pixels are not re-encoded and keeps the colour profile or converts to sRGB; (3) and (4) measure against the picture after (1)-(2), SSIM on luminance, 8×8 windows; US-2 gains an orientation outline (600 KB and 3 MB), a Display P3 scenario, a similarity check on the scaled case and a large-PNG scenario.

### R-7
Severity: minor
Affects: BR-IM-7, US-3 canonical cases
Problem: BR-IM-7 said an image inside a paragraph is "moved out to stand right after that block", while the canonical case `<p>a<img>b</p>` splits the paragraph at the image; BR-IM-12 ("stands where it was put") agrees with the split.
Required resolution: one rule.
Status: resolved
Resolution: BR-IM-7: a paragraph is split at the image (no empty half kept); a heading is followed by the image; a link is left around the rest of its text. Two canonical cases added (leading image, paragraph inside a list item).

### R-8
Severity: minor
Affects: BR-IM-13, US-1
Problem: Nothing said where an insert lands with the cursor in a heading, a table cell or a code block; the server drops an image in a cell or code block, so the member would watch an upload succeed and the image vanish on save.
Required resolution: a client placement rule matching BR-IM-7.
Status: resolved
Resolution: BR-IM-13 "Where an insert lands"; US-1 outline with the three places.

### R-9
Severity: minor
Affects: BR-IM-11, NFR Safety, US-4
Problem: "Read live, a revoked right stops it" cannot hold if image answers are cacheable by a shared proxy; content sniffing was not ruled out.
Required resolution: no shared caching, declared type, no sniffing; be explicit that the reader's own browser cache may keep what it already showed.
Status: resolved
Resolution: BR-IM-11 and NFR Safety amended; US-4 scenario "No shared cache keeps an image" with a header test.

### R-10
Severity: minor
Affects: BR-IM-13, US-1
Problem: Pasting an own-store image the writer cannot read (another item, revoked, purged) had no defined outcome.
Required resolution: define it.
Status: resolved
Resolution: left out with the upload-failed notice, the rest pasted; US-1 scenario added.

### R-11
Severity: minor
Affects: BR-IM-18
Problem: An image block inside a list item changes feat-018 BR-RT-5's canonical structure ("list items hold only paragraphs and nested lists"), which BR-IM-18 did not list.
Required resolution: list the amendment.
Status: resolved
Resolution: BR-IM-18 names BR-RT-5.

### R-12
Severity: minor
Affects: OQ-3, OQ-4, D-2
Problem: The open questions understated their consequences. OQ-3: the human asked that large images be compressed (D-2), but a large photographic PNG is only re-compressed losslessly and may stay near 10 MB. OQ-4: keeping a removed image costs more than storage — a screenshot removed because it showed a secret cannot be erased short of purging the item; and the alternative breaks feat-059's frozen "as submitted" description unless it counts as a reference.
Required resolution: state the consequences in the questions, the recommendations unchanged.
Status: resolved
Resolution: OQ-3 and OQ-4 reworded; BR-IM-4 (3) permits lossless re-compression; US-2 "A large PNG is never encoded lossily".

### R-13
Severity: minor
Affects: NFR Safety
Problem: Per-file caps bound one decode, not many parallel ones; ten parallel 60 MP uploads can exhaust memory.
Required resolution: the design bounds concurrent decodes.
Status: resolved
Resolution: NFR Safety sentence added.

### R-14
Severity: minor
Affects: § Out of scope (quotas, rate limits)
Problem: With no per-user quota or rate limit, any editor can store 10 MB uploads without bound for the 2-3 hours before the sweep, and bound images without bound (OQ-4 keeps them). Internal users only, and the out-of-scope line says so.
Required resolution: none in the spec; the human should see it.
Status: accepted-risk
Resolution: kept out of scope; mentioned in the gate-1 summary.

### R-15
Severity: minor
Affects: BR-IM-7, US-3
Problem: "255 characters" for the text alternative named no unit, where feat-018 counts code points and the legacy rule UTF-16 units.
Required resolution: name the unit.
Status: resolved
Resolution: 255 code points, counted as BR-RT-8.

### Checked, no finding

- Size: the task is one L (ADR, new storage slice), the crop line is S; nothing in the decomposition exceeds its size.
- Open questions OQ-1..OQ-7: each has a recommendation, and the spec's rules are written to it (formats BR-IM-2, caps BR-IM-3, processing BR-IM-4, kept-on-removal BR-IM-16, 2 h for item uploads D-3/BR-IM-16, crop out of scope, animation BR-IM-4 (6)).
- Permissions per step: upload (BR-IM-5, US-2 outline), save binding (BR-IM-8, US-3 outline), read (BR-IM-11, US-4 outline), «Удалена» / deactivated / purge (US-2, US-4, US-5), anonymous 401.
- Concurrent saves by two editors: the later save may drop the earlier one's image from the text; under OQ-4 it stays bound and nothing breaks.
- The reference tracker is not named.

### Verdict

needs-human-decision: OQ-1..OQ-7 are product choices for the human at gate-1.
