# Build 2 — Search (`contentKind`)

**Status:** in progress on `feat/catalogue-build-2-search`. Not deployed.

`title` replaces `episodeTitle`. `seriesName` replaces `podcastName`. `description` replaces `episodeDescription`. Do not store both. Do not add `seriesDescription`. Film omits `seriesName`.

The hourly indexer keeps writing the old names until the index is rebuilt. CreateSearchIndex for the new index writes only the new names.

---

## Done when

Search projection code is **merged to `main`** and ready for the cutover below.

## Checklist

- [x] Replacement field names in code (legacy names stay on the hourly upload until the rebuild)
- [ ] Merged to `main`
- [ ] Cutover in [gate-2.md](./gate-2.md)

## When Build 2 is done

Open **only** [gate-2.md](./gate-2.md).
