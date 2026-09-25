# Build 2 — Search (`contentKind`)

**Status:** in progress on `feat/catalogue-build-2-search`. Not deployed.

Build 1 / GATE 1 are done. Measured Free index after the 100-character description recreate (25 Sep 2026): **47.97 MB / 50 MB (95.9%)**, 86,085 documents. A second full index does not fit. The fresh index for `contentKind` is a **delete-and-rebuild** on Free, or a **SKU bump** if the old index must stay up during cutover. Overlap of legacy `episodeTitle` / `podcastName` / `episodeDescription` with the new names is in this branch; that extra text is why a second copy is the quota risk, not `contentKind` itself.

---

## Done when

Search projection code is **merged to `main`** and ready to deploy.

## Checklist (when unlocked)

- [x] Re-measure search storage (47.97 MB / 50 MB, 86,085 docs, 25 Sep 2026)
- [x] Projection fields + `contentKind` (legacy names kept until GATE 2)
- [x] Indexer fan-out for four playable sources (`--all-playables`; not run)
- [x] Fresh-index / SKU plan (delete-rebuild on Free, or SKU bump to keep the old index)
- [ ] Merged to `main`

## When Build 2 is done

Open **only** [gate-2.md](./gate-2.md).
