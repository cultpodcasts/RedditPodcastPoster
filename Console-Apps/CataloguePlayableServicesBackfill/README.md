# CataloguePlayableServicesBackfill

Repairs Film / TvShowEpisode / NewsReport `services` (url + image) that were stripped or left incomplete by migration/Move. Scans Cosmos first, builds truth from:

1. **Episode backup** (`episode/{id}.json` from CosmosDbDownloader) — services plus leftover urls/images
2. **PublicDatabase** (flat `*.json` checkout) — nested episode/report or film root `urls` maps (urls only; no images)

Episode-backup urls win on conflict; public urls fill gaps. Images come only from the episode backup. At least one of `--episode-backup-dir` or `--public-database-dir` is required.

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

## Apply (human only)

```powershell
dotnet run --project Console-Apps/CataloguePlayableServicesBackfill -- `
  --episode-backup-dir C:\path\to\CosmosDbDownloaderRoot `
  --public-database-dir C:\path\to\PublicDatabase `
  --kind all `
  --apply `
  --consent "I approve CataloguePlayableServicesBackfill production writes"
```

Agents must never pass `--consent`. Without the exact phrase, `--apply` exits 2 and writes nothing.

## Flags

| Flag | Notes |
|------|--------|
| `--episode-backup-dir` | Private episode JSON (url + image). Optional if `--public-database-dir` is set |
| `--public-database-dir` | PublicDatabase checkout (urls only). Optional if `--episode-backup-dir` is set |
| `--kind` | `film` \| `tvshowepisode` \| `newsreport` \| `all` |
| `--ids` | Comma-separated GUIDs |
| `--limit` | Stop after N discrepancy candidates (`0` = no limit) |
| `--change-log` | JSONL path |
| `--snapshot-dir` | Optional before-`services` JSON slices |
