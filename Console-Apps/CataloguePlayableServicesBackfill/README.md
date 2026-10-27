# CataloguePlayableServicesBackfill

Repairs Film / TvShowEpisode / NewsReport `services` (url + image) that were stripped or left incomplete by migration/Move. Scans Cosmos first, builds truth from:

1. **Episode backup** (`episode/{id}.json` from CosmosDbDownloader) — services plus leftover urls/images
2. **PublicDatabase** (flat `*.json` checkout) — nested episode/report or film root `urls` maps (urls only; no images)

Episode-backup urls win on conflict; public urls fill gaps. Images come only from the episode backup. At least one of `--episode-backup-dir` or `--public-database-dir` is required, unless `--youtube-id-only` is set.

Every run also syncs the compact `youTubeId` from `services.youtube.url` (search reads `c.youTubeId`, so a url-only playable shows no YouTube icon). That sync happens even when a playable has no truth row.

## Dry-run (default)

```powershell
dotnet run --project Console-Apps/CataloguePlayableServicesBackfill -- `
  --episode-backup-dir C:\path\to\CosmosDbDownloaderRoot `
  --public-database-dir C:\path\to\PublicDatabase `
  --kind all
```

`--episode-backup-dir` may be the CosmosDbDownloader export root (contains `episode\`) or the `episode` folder itself.

Writes `catalogue-playable-services-backfill.jsonl` (overwritten each run) with every missing-truth and discrepancy candidate. No Cosmos writes. JSONL uses `missingTruth: true` when neither episode backup nor PublicDatabase had the GUID (finish log / run counter: `MissingTruth`).

Exit codes: `0` ok, `1` bad arguments, `2` apply refused (missing/wrong `--consent`), `3` one or more Cosmos writes failed (`FailedWrites > 0`).

## YouTube id only

Fill `youTubeId` from `services.youtube.url` on playables written before the compact id existed. No truth sources are read and `services` are not changed. Dry-run:

```powershell
dotnet run --project Console-Apps/CataloguePlayableServicesBackfill -- `
  --youtube-id-only `
  --kind all `
  --change-log catalogue-playable-youtube-id.jsonl
```

Apply uses the same flags with its own change log (so the dry-run log is kept for comparison), plus `--apply` and `--consent` (see **Apply** below):

```powershell
dotnet run --project Console-Apps/CataloguePlayableServicesBackfill -- `
  --youtube-id-only `
  --kind all `
  --change-log catalogue-playable-youtube-id-apply.jsonl `
  --apply `
  --consent "<operator-held phrase>"
```

**Url wins.** The id is always taken from `services.youtube.url`, overwriting a stored id that disagrees. If the url is missing or is not a YouTube video url (e.g. a channel url), an existing id is **kept**, never cleared, and the row is logged with `youTubeIdOutcome: "skipped-unparseable"`. Each change-log row records the real before → after id (`changes[].before` / `after` on field `youTubeId`) and `youTubeIdOutcome` (`filled` null → id, `overwritten` idA → idB, `cleared` id → null — always 0 here, `skipped-unparseable`). The finish log splits counts the same way: `YouTubeIdFilled`, `YouTubeIdOverwritten`, `YouTubeIdCleared`, `YouTubeIdSkippedUnparseable`.

## Apply (human only)

`--apply` requires `--consent` with the human consent phrase, which the operator holds; it is deliberately not written in this repository. Agents must never pass `--consent`. Without the exact phrase, `--apply` exits 2 and writes nothing.

**Run applies only between :15 and :55 past the hour** (the search indexers run at :03 and :08), and never while a catalogue Move is in progress.

```powershell
dotnet run --project Console-Apps/CataloguePlayableServicesBackfill -- `
  --episode-backup-dir C:\path\to\CosmosDbDownloaderRoot `
  --public-database-dir C:\path\to\PublicDatabase `
  --kind all `
  --change-log catalogue-playable-services-backfill-apply.jsonl `
  --apply `
  --consent "<operator-held phrase>"
```

How writes are made:

- **NewsReport / TvShowEpisode**: the document is re-read with its ETag just before the write. If it is gone (deleted or moved since the scan) the row is skipped as `skipped: "not-found"`; if its `services` or `youTubeId` changed since the scan it is skipped as `skipped: "conflict"`. Otherwise a Cosmos **partial patch** sets only the changed paths (`/services` and/or `/youTubeId`) with `If-Match` on that ETag, so other fields edited concurrently are untouched. A patch never creates a document: 404 → `not-found`, 412 → `conflict`. Skips are logged as warnings and counted (`SkippedNotFound`, `SkippedConflict`); they do not fail the run.
- **Film**: ETag-guarded read-modify-write through the film repository.
- **Search**: updated search documents are pushed in batches of 100 (films have no search pull; for news/TV the `_ts` bump also lets the indexer re-pull).
- `--snapshot-dir` writes each playable's before `{ services, youTubeId }`.

## Flags

| Flag | Notes |
|------|--------|
| `--episode-backup-dir` | Private episode JSON (url + image). Optional if `--public-database-dir` is set |
| `--public-database-dir` | PublicDatabase checkout (urls only). Optional if `--episode-backup-dir` is set |
| `--youtube-id-only` | Only sync `youTubeId` from `services.youtube.url`; truth dirs are ignored |
| `--kind` | `film` \| `tvshowepisode` \| `newsreport` \| `all` |
| `--ids` | Comma-separated GUIDs |
| `--limit` | Stop after N discrepancy candidates (`0` = no limit) |
| `--change-log` | JSONL path |
| `--snapshot-dir` | Optional before `{ services, youTubeId }` JSON slices |
