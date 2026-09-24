# Repair evidence (always written on migrate)

Every migrate run (dry-run or `--apply`) **always** writes a repair pack. You do not opt in.

Default location: `./wire-cutover-evidence/wire-cutover-<utc>.jsonl` (+ siblings).  
Optional `--journal path` only overrides the path.

Files are written **as each episode is processed** (flushed immediately — a crash mid-run still leaves prior rows):

| File | Purpose |
|------|---------|
| `*.jsonl` | **Machine repair source of truth** — one JSON object per episode |
| `*-affected-ids.txt` | EpisodeId + PodcastId for every planned/touched row |
| `*-applied-ids.txt` | Only episodes Cosmos actually wrote |
| `*-changes.txt` | Human from→to lines per field |

Keep all three until GATE 1 audit is green (and longer if anything went wrong).

---

## How to fix by hand or with a fixer app

1. Open `run.jsonl`.
2. Keep only lines where `"applied":true` (Cosmos was actually patched).
3. For each line:
   - Identity: `episodeId` + `podcastId` (partition key).
   - Restore target: `before` — full cutover-field snapshot **before** migrate.
   - What we did: `fieldChanges[]` — each has `name`, `fromExists`/`fromJson`, `toExists`/`toJson`.
4. For each cutover field in `before`:
   - `exists: false` → **remove** that property from the Episode document.
   - `exists: true` → **set** that property to the raw JSON in `jsonValue`.
5. Do **not** touch any other Episode properties.

`after` is what migrate intended; use it only if you need to understand forward direction. **Repair = apply `before`.**

---

## Example `fieldChanges` (rename)

```json
{
  "name": "podcastSearchTerms",
  "fromExists": true,
  "fromJson": "\"cult\"",
  "toExists": false,
  "toJson": null
}
```

```json
{
  "name": "publisherSearchTerms",
  "fromExists": false,
  "fromJson": null,
  "toExists": true,
  "toJson": "\"cult\""
}
```

Human form in `*-changes.txt`:

```text
episodeId=... podcastId=... applied=true
  podcastSearchTerms: "cult" -> <absent>
  publisherSearchTerms: <absent> -> "cult"
  releaseSort: <absent> -> "2024-01-02T03:04:05Z"
```

---

## Schema

Journal entries include `"schemaVersion": 1`. A fixer app should refuse unknown versions.

Cutover fields only (never other Episode members):

`podcastSearchTerms`, `publisherSearchTerms`, `podcastLanguage`, `publisherLanguage`, `podcastMetadataVersion`, `parentMetadataVersion`, `podcastRemoved`, `parentRemoved`, `releaseSort`.
