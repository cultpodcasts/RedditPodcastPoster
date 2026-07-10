# PeopleMigrator (PeopleFromGuestHandlesSeeder)

Builds a local **people-seed.json** file from episode guest handle data. This tool produces a reviewable People register with aliases — it does **not** link `guests` on episodes and **never** writes episode documents.

## Guardrail

**Episode writes are forbidden.** Do not add `IEpisodeRepository`, episode upserts, or episode patches to this tool. See `.cursor/rules/episode-guest-handles-guardrail.mdc`.

**Cosmos writes are off by default.** Persisting to the People container requires both `--persist-cosmos` and `--apply`, plus explicit user approval in the conversation.

- Guest handle restoration: `EpisodeGuestHandleRestorer` (patches handles only).
- Guest linking (`guests: string[]`): future `EpisodeGuestsLinker` (patches `guests` only).

## Input (pick one)

| Flag | Description |
|------|-------------|
| `--cache-path` | `guest-handle-restore-cache.json` from `EpisodeGuestHandleRestorer` (recommended) |
| `--backup-path` | Scan a CosmosDbDownloader episode backup folder |
| `--from-cosmos` | Read-only SELECT of `twitterHandles` / `blueskyHandles` from Cosmos episodes |

When using `--cache-path`, episode `{id}.json` files from the cache's `backupPath` are used for title/description name extraction.

## Output (default)

Writes **people-seed.json** next to the cache (or `--output` path). No Cosmos or R2 writes.

```json
{
  "generatedAt": "...",
  "sourceCache": "...",
  "sourceBackupPath": "...",
  "people": [
    {
      "name": "Scott Jennings",
      "aliases": ["Jennings", "Scott Jennings"],
      "twitterHandle": "@ScottJennings",
      "blueskyHandle": "...",
      "sourceEpisodeIds": ["..."],
      "notes": "name from description"
    }
  ]
}
```

Use `--output people-seed.iteration-2.json` for iteration runs.

| `--clean-seed-from` | Post-process seed JSON: promote full names over titles/first-name-only canonicals, strip duplicate/noisy aliases |
| `--review-server` | Local web UI to review/edit a seed JSON file (no Cosmos writes) |
| `--enrich-aliases-from` | Alias-only pass: read existing seed, scan episodes, write enriched seed (no X re-scrape) |

## Examples

```powershell
# Default: JSON file only (515 episodes → people-seed.json)
dotnet run --project Console-Apps/PeopleMigrator -- `
  --cache-path "C:\path\to\guest-handle-restore-cache.json"

# Iteration with API name lookup
dotnet run --project Console-Apps/PeopleMigrator -- `
  --cache-path "C:\path\to\guest-handle-restore-cache.json" `
  --name-lookup `
  --output "C:\path\to\people-seed.iteration-2.json"

# Alias-only enrichment from existing seed (fast — no API lookup)
dotnet run --project Console-Apps/PeopleMigrator -- `
  --enrich-aliases-from "Console-Apps/PeopleMigrator/people-seed.iteration-3.json" `
  --output "Console-Apps/PeopleMigrator/people-seed.iteration-4.json"

# Promote canonical names + strip duplicate/noise aliases into next iteration
dotnet run --project Console-Apps/PeopleMigrator -- `
  --clean-seed-from "Console-Apps/PeopleMigrator/people-seed.iteration-6.json" `
  --output "Console-Apps/PeopleMigrator/people-seed.iteration-7.json"

# Interactive local reviewer (edits JSON on disk only)
dotnet run --project Console-Apps/PeopleMigrator -- `
  --review-server `
  --seed-path "Console-Apps/PeopleMigrator/people-seed.iteration-6.json" `
  --port 5188
```

Open **http://127.0.0.1:5188** — search/filter people, edit canonical name and aliases, open X/Bluesky profiles, refresh display names from live profiles, prev/next navigation, **Save** writes back to the seed file.

## Review server options

| Flag | Default | Description |
|------|---------|-------------|
| `--review-server` | off | Start the local review UI + API |
| `--seed-path` | `people-seed.json` in cwd | Seed JSON to load and save |
| `--port` | 5188 | HTTP port |

```powershell
# Cosmos persist (requires explicit user approval + both flags)
dotnet run --project Console-Apps/PeopleMigrator -- `
  --cache-path "C:\path\to\guest-handle-restore-cache.json" `
  --persist-cosmos --apply
```

## Options

| Flag | Default | Description |
|------|---------|-------------|
| `--output` | `people-seed.json` next to cache | Output path for seed JSON |
| `--name-lookup` | off | Call X/Bluesky profile APIs for display names |
| `--persist-cosmos` | off | Enable Cosmos writes (requires `--apply`) |
| `--apply` | off | Second confirmation for Cosmos writes |
| `--clear-people` | off | Delete all People documents first (requires both Cosmos flags) |
| `--sample N` | 15 | Console preview count (`0` = list all) |

## Dedup logic

Handles are normalized and deduplicated via `PersonMigrationRegistry`:

- Same X handle or same Bluesky handle → one person
- Aligned index pairs (`twitter[i]` + `bluesky[i]`) → linked as same person
- Cross-platform token match (local part) → merged when seen across episodes

When persisting to Cosmos, existing People container records are indexed first so re-runs update rather than duplicate.
