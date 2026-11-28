# Architecture decision records

Short records of structural decisions for this repo.

| ADR | Title | Status |
|-----|-------|--------|
| [0001](0001-folder-equals-namespace.md) | Folder equals namespace | Accepted |
| [0002](0002-separate-catalogue-content-containers.md) | Separate Cosmos containers per catalogue content family | **Accepted** (2026-09-23) |
| [0003](0003-unified-playable-search-document.md) | Unified playable search document with `contentKind` facet | **Accepted** (2026-09-23) |
| [0004](0004-scraper-content-classification-outline.md) | Scraper content classification rules | Outline (CLS-001…008 signed 2026-09-23; validate before Phase 3) |
| [0006](0006-film-file-key-collision-manual.md) | Film public file-key collisions stay manual | **Accepted** (2026-11-28) |

`0005` is reserved for FileKeyClaims permanence (#1307) and lands in sibling PR #1598 (inserts the row between `0004` and `0006`; no renumber of this ADR).

Catalogue content-types epic: [catalogue-content-types-epic.md](../catalogue-content-types-epic.md). Product type for one-off documentaries is **Film** (not Movie).
