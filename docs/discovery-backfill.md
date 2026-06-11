# Discovery backfill (console app)

Use when scheduled `discover-infra` runs were missed. This documents **`Discover.exe`** (project `Console-Apps/Discover`), not the Azure Function host.

## Scheduled runs (production)

`DiscoveryTrigger` cron: `30 2/6 * * *` (UTC) → **03:30, 09:30, 15:30, 21:30 BST** (when UK is on BST, UTC+1).

Production search window: `discover__SearchSince = 6:10:00` in [`Infrastructure/functions.bicep`](../Infrastructure/functions.bicep) — each run searches episodes released in the **6 hours 10 minutes** before the trigger.

Missed trigger on **11 Jun 2026**:

| Missed run (BST) | Trigger (UTC) | Backfill `--time-since` (UTC) |
|------------------|---------------|-------------------------------|
| 03:30 | 2026-06-11T02:30:00Z | `2026-06-10T20:20:00Z` (6h10m before trigger) |
| 09:30 | 2026-06-11T08:30:00Z | `2026-06-11T02:20:00Z` (6h10m before trigger) |

## CLI — verified

Executable: `Console-Apps/Discover/bin/Release/net10.0/win-x64/Discover.exe` (or publish via `.\scripts\publish-console-apps.ps1`).

```text
Discover 1.0.0+...

  -r, --number-of-hours       Search window as hours from now
  -t, --time-since            Discover items released since this time
  -l, --include-listen-notes  Search Listen Notes
  -d, --include-taddy         Search Taddy
  -s, --exclude-spotify       Exclude Spotify
  -y, --include-youtube       Search YouTube
  -e, --enrich-listennotes-from-spotify  (default: true)
  -a, --enrich-spotify-from-apple        (default: true)
  -o, --taddy-offset          Extra Taddy lookback (production: 2 hours)
  -u, --use-remote            Process unprocessed remote discovery docs
  --help
```

### Code path (custom time range)

1. `Program.cs` → `Parser.ParseArguments<DiscoveryRequest>`
2. `DiscoveryProcessor.CreateDiscoveryContext` — validates `-t` / `-r`, computes `since` (UTC)
3. `DiscoveryProcessor.GetDiscoveryResults` — passes `since` into `IndexingContext` and `GetServiceConfigOptions`
4. `DiscoveryService.GetDiscoveryResults` → `SearchProvider.GetEpisodes` — queries use `indexingContext` / config `Since`

**Verified 2026-06-11:** Ran `Discover.exe --time-since "2026-06-11T02:20:00Z"` locally; output included:

```text
Discovering items released since '2026-06-11T02:20:00.0000000Z' ...
Initiating discovery at '...'
Discovery initiated at '...'.
```

Exit code `0`. Cosmos/API credentials were available via user secrets on the operator machine.

### Prefer `--time-since` over `-r`

`-t` / `--time-since` is reliable for backfill. `-r` / `--number-of-hours` has a code quirk: `GetDiscoveryResults` falls back to a fixed 6-hour window when `-t` is omitted, which can disagree with `-r`. Use explicit UTC timestamps for missed windows.

## Prerequisites

1. `az login` not required for the console app; Cosmos/API keys via **user secrets** (`UserSecretsId` on the csproj) or `RedditPodcastPoster_*` environment variables.
2. Publish if needed:

   ```powershell
   .\scripts\publish-console-apps.ps1
   # or
   dotnet publish Console-Apps/Discover/Discover.csproj -c Release -r win-x64
   ```

3. Run from repo root or ensure `appsettings.json` is beside the exe (queries differ from production bicep — see below).

## Backfill commands (11 Jun 2026)

Production-equivalent flags (from bicep `discover__*` settings):

```powershell
$exe = ".\Console-Apps\Discover\bin\Release\net10.0\win-x64\Discover.exe"

# Missed 03:30 BST run
& $exe --time-since "2026-06-10T20:20:00Z" `
  --include-listen-notes --include-taddy --include-youtube `
  --taddy-offset 02:00:00

# Missed 09:30 BST run
& $exe --time-since "2026-06-11T02:20:00Z" `
  --include-listen-notes --include-taddy --include-youtube `
  --taddy-offset 02:00:00
```

Successful run prints `Discovery initiated at '<timestamp>'.` and persists results via `IDiscoveryResultsRepository.Save` (same path as normal CLI discovery, not the Durable Function orchestration).

## Differences from cloud Discovery

| Aspect | Cloud (`discover-infra`) | Console (`Discover.exe`) |
|--------|--------------------------|---------------------------|
| Entry | Timer → Durable orchestration | Direct CLI |
| Queries | `discover__Queries__*` in bicep | `appsettings.json` → `Discover:Queries` |
| Search window | `discover__SearchSince` app setting | `-t` / `-r` arguments |
| Notifications / publisher | Full orchestration pipeline | Saves discovery document only |

Console `appsettings.json` query list is **not identical** to production bicep. For a full production parity backfill, align queries via configuration or accept console defaults.

## When cloud discovery is down

1. Confirm missed runs (no `DiscoveryTrigger` traces in App Insights for `discover-infra`).
2. Run backfill commands above.
3. Fix hosting separately — see [deployment.md](./deployment.md). Do not change app settings during code deploy.
4. After `discover-infra` is healthy, scheduled runs resume; backfill covers the gap only if CLI runs completed successfully.

## Agent rules

- **Do not** run backfill against production Cosmos without user approval if credentials are unavailable.
- **Do** verify `--help` and a dry `--time-since` run before documenting new flags.
- **Do** use UTC in `--time-since` strings (`Z` suffix).
