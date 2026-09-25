# Build 2 — Search (`contentKind`)

**Status:** in progress on `feat/catalogue-build-2-search`. Not deployed.

Build 1 / GATE 1 are done. The Free index measured on 25 Sep 2026, before the overlapping searchable fields in this branch, was **47.97 MB / 50 MB (95.9%)**, 86,085 documents. That figure is not a bound for the new index. This branch keeps `episodeTitle` / `podcastName` / `episodeDescription` and adds analyzed `title` / `seriesName` / `description`, plus `seriesDescription`. Shrinking descriptions from about 158 characters to 100 only bought about 7 MB (54.95 → 47.97). A second searchable copy of the capped description is larger than that saving, before titles and parent blurbs.

**Delete-and-rebuild on Free is not an approved cutover.** Do not delete the live index from this document. Approval needs a measurement that includes the overlapping fields, or a hard upper bound that shows the single index stays under 50 MB. If that bound fails, the plan is a SKU bump before any teardown. GATE 2 stays locked either way.

The hourly indexer still omits `contentKind`, `title`, `seriesName`, `description`, and `seriesDescription` until the live schema contains them. `CreateIndex` / `--update-existing` can add those fields from `EpisodeSearchRecord`. `--all-playables` CreateOrUpdates sibling datasources and indexers and, with `--run-indexer`, runs them. That path has not been executed against Azure.

---

## Done when

Search projection code is **merged to `main`** and ready to deploy.

## Checklist (when unlocked)

- [ ] Re-measure search storage with the overlapping fields (the 47.97 MB / 50 MB figure is the index before those fields, 86,085 docs, 25 Sep 2026)
- [ ] Projection fields + `contentKind` on the live index (legacy names stay until GATE 2; hourly push stays off until the schema has the new fields; `seriesDescription` comes from denormalised `publisherDescription`)
- [ ] Indexer fan-out for four playable sources (`--all-playables` CreateOrUpdates siblings and `--run-indexer` runs them; not run against Azure)
- [ ] Fresh-index / SKU plan with a size bound that includes the overlapping fields (SKU bump before teardown if the bound fails; delete-and-rebuild on Free is not approved)
- [ ] Merged to `main`

## When Build 2 is done

Open **only** [gate-2.md](./gate-2.md).
