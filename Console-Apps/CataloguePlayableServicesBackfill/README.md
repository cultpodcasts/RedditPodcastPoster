# CataloguePlayableServicesBackfill

Repairs Film / TvShowEpisode / NewsReport `services` (url + image) that were stripped or left incomplete by migration/Move. Scans Cosmos first, loads the matching episode backup by preserved GUID (`episode/{id}.json`), diffs, and carries backup discrepancies onto the live entity.

## Dry-run (default)

```powershell
dotnet run --project Console-Apps/CataloguePlayableServicesBackfill -- `
  --episode-backup-dir C:\path\to\CosmosDbDownloaderRoot `
  --kind all
```

`--episode-backup-dir` may be the CosmosDbDownloader export root (contains `episode\`) or the `episode` folder itself.

Writes `catalogue-playable-services-backfill.jsonl` (overwritten each run) with every missing-backup and discrepancy candidate. No Cosmos writes.

Exit codes: `0` ok, `1` bad arguments, `2` apply refused (missing/wrong `--consent`), `3` one or more Cosmos writes failed (`FailedWrites > 0`).

## Apply (human only)

```powershell
dotnet run --project Console-Apps/CataloguePlayableServicesBackfill -- `
  --episode-backup-dir C:\path\to\CosmosDbDownloaderRoot `
  --kind all `
  --apply `
  --consent "I approve CataloguePlayableServicesBackfill production writes"
```

Agents must never pass `--consent`. Without the exact phrase, `--apply` exits 2 and writes nothing.

## Flags

| Flag | Notes |
|------|--------|
| `--kind` | `film` \| `tvshowepisode` \| `newsreport` \| `all` |
| `--ids` | Comma-separated GUIDs |
| `--limit` | Stop after N discrepancy candidates (`0` = no limit) |
| `--change-log` | JSONL path |
| `--snapshot-dir` | Optional before-`services` JSON slices |
