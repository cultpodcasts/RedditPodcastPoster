# Discovery backfill (console app)

Use when scheduled `discover-infra` runs were missed, or for the **mandatory first CLI seed** when Cosmos has no `discoveryBegan` watermark.

> **CRITICAL cold start:** Cloud Discovery is Dynamic-only and **fail-closed**. If there is no prior successful Discovery document, the timer/orchestration **will fail** on purpose. Seed with `Discover.exe` first — full ops runbook: **[discovery-uk-schedule.md](./discovery-uk-schedule.md)**.

This documents **`Discover.exe`** (project `Console-Apps/Discover`), not the Azure Function host.

## Scheduled runs (production)

`DiscoveryTrigger` cron: `0 */30 * * * *` (UTC). Runs only when UK local time matches Cosmos `DiscoveryScheduleConfig.runTimes` (defaults `08:00` and `22:00` UK) within ±15 minutes. See [discovery-uk-schedule.md](./discovery-uk-schedule.md).

Lookback is **Dynamic only**: `since = lastSuccess - discover__DynamicLookbackOverlap` (prod `00:10:00`). There is **no** `discover__SearchSince` / Static mode. Missed slots extend the window automatically once a watermark exists.

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

3. Run from any directory; config loads from the exe folder (`appsettings.json` or `Discover.appsettings.json`). After `publish-console-apps.ps1`, `artifacts\tools\Discover.appsettings.json` is copied alongside `Discover.exe`.

## First-run seed (required when watermark missing)

**Requires user approval before running against production Cosmos.**

```powershell
$exe = ".\Console-Apps\Discover\bin\Release\net10.0\win-x64\Discover.exe"
& $exe --time-since "2026-07-18T20:00:00Z" `
  --include-listen-notes --include-taddy --include-youtube `
  --taddy-offset 02:00:00
```

Adjust `--time-since` to the intentional backfill start (UTC). Successful run prints `Discovery initiated at '<timestamp>'.` and persists results via `IDiscoveryResultsRepository.Save`. After that, scheduled cloud runs use Dynamic lookback from the new watermark.

## Backfill command (historical — 11 Jun 2026, old 6h UTC cron)

For the miss under the **previous** schedule (`:33` UTC / 6h cadence). Prefer UK-slot-aware `--time-since` going forward.

```powershell
$exe = ".\Console-Apps\Discover\bin\Release\net10.0\win-x64\Discover.exe"
& $exe --time-since "2026-06-10T20:20:00Z" `
  --include-listen-notes --include-taddy --include-youtube `
  --taddy-offset 02:00:00
```

## Differences from cloud Discovery

| Aspect | Cloud (`discover-infra`) | Console (`Discover.exe`) |
|--------|--------------------------|---------------------------|
| Entry | 30-min Timer → UK schedule gate → Durable orchestration | Direct CLI |
| Queries | `discover__Queries__*` in bicep | `discover:Queries` in `appsettings.json` / `Discover.appsettings.json` |
| Search window | Dynamic from Cosmos watermark (fail-closed if none) | `-t` / `-r` arguments |
| Notifications / publisher | Full orchestration pipeline | Saves discovery document only |

CLI service flags (`--include-listen-notes`, `--include-taddy`, `--include-youtube`) and `--taddy-offset` default to production bicep values. Search window comes from `-t` / `-r` only.

## When cloud discovery is down

1. Confirm missed runs via **`AppRequests`** for `discover-infra` (not traces alone) — see production execution truth rules.
2. If fails are lookback fail-closed: seed/backfill with CLI (approval required).
3. Fix hosting separately — see [deployment.md](./deployment.md). Do not change app settings during code deploy.
4. After `discover-infra` is healthy and a watermark exists, scheduled UK slots resume.

## Agent rules

- **Do not** run backfill against production Cosmos without explicit user approval.
- **Do** verify `--help` and a dry `--time-since` run before documenting new flags.
- **Do** use UTC in `--time-since` strings (`Z` suffix).
