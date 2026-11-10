# ADR-0039: Give a work item an optional external key, unique per named source in its organization, and a list of external links

- Status: proposed
- Date: 2026-11-10
- Context: `doc-017-import-export-constructor`, stage 1: [spec](../features/doc-017-import-export-constructor/spec.md) BR-IE-10, BR-IE-11, BR-IE-24, US-9, OQ-2. The source is the team's planning workbook ([input-brief.md](../features/doc-017-import-export-constructor/input-brief.md), gaps 1). It builds on ADR-0027 and ADR-0029 (access), ADR-0030 (the recycle bin and the purge) and doc-012 BR-MX-1, BR-MX-9. It goes to the human at doc-017's gate-1; only the human sets `accepted` (`docs/adr/README.md`, Process 2). It is implemented by `feat-NNN-work-item-external-key-and-links`.

## Problem

A repeated import must update the items it created before, not duplicate them, so a row needs a stable way to find its item. The workbook has its own ids (`DDMMYY - NN`, 432 of 543 rows filled), and a second team's sheet will bring ids of its own that may collide. The sheet's project column can change between imports, and an item never moves between projects (BR-MX-9). The same item also carries 206 hyperlinks to documents and to another tracker, and today an item links only to other items. The key outlives any one import, is shown on the card and searched by the list, and constrains every later import line: an identifier scheme, hence an ADR.

## Options considered

### Option 1: no key; match rows by title or by the MUSTASK identifier only

- Pros: nothing new on the item.
- Cons: titles repeat and change; the first import has no MUSTASK identifiers in the file, so every re-import of the source sheet duplicates. Rejected.

### Option 2: the key only inside the import module (a map from key to item)

- Pros: the work items module is untouched.
- Cons: the card cannot show where an item came from, the list's keyword cannot find it, and the map has to follow every purge of another module's row. Two modules would own one fact. Rejected.

### Option 3: a key on the item, unique per project

- Pros: simple scope; projects already own numbering.
- Cons: a row whose project changed in the sheet no longer finds its item and creates a second one; two sheets with overlapping ids in one project collide. Not chosen (OQ-2 B).

### Option 4: a key on the item with a named source, unique per source in the organization

- Pros: a project change in the sheet still finds the item (and is then reported, not applied); two sheets never collide because each has its own source name; the card shows «DevEx: 010125 - 01».
- Cons: one more uniqueness rule across the organization's projects, checked under the organization boundary (BR-MX-1).

## Decision

Option 4.

1. **The key.** A work item has at most one external key: a source name (1–50 characters) and a key (1–100 characters), both trimmed. The pair is unique among the organization's items that are not purged, compared ignoring letter case and surrounding spaces; an item in «Удалена» keeps its key, and a purge frees it (ADR-0030). Concurrent writes of the same pair end in one item holding it and the other write refused, never two holders.
2. **Who sets it.** Only an import sets the key, and only the undo of that import or the item's purge removes it. The panel shows it read-only. No permission code is added for it: it is written under the import's codes (ADR-0040) and read under `workitems.workitem.read`.
3. **Search.** The list's keyword matches the key as it matches the identifier (feat-003's shared filter specification gains one arm).
4. **External links.** An item has 0–50 external links: an http(s) address of at most 2 000 characters and an optional title of at most 200. An address appears once per item. They are added and removed under `workitems.workitem.write`, refused on an item in «Удалена», and leave with the item's purge (mod-005 BR-DR-16). Item-to-item links (feat-001) are unchanged; an external link never points at a MUSTASK item by its address.

## Consequences

- Simpler: every later import and export finds items by key, and a round trip by the MUSTASK identifier stays possible (doc-005 BR-M6-8).
- Harder: the purge path gains the key and the links; a non-disclosure test covers the uniqueness refusal, which must not name the item that holds the key when the caller cannot read its project.
- Locked in: a source name is part of a template; renaming a source in a template does not rename keys already stored.
- Revisit when: an item needs keys from two sources at once, or users ask to edit a key by hand.
