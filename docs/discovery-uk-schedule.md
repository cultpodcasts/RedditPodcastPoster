# Discovery UK schedule + Dynamic lookback

Production Discovery (`discover-infra`) runs on a **UK-local schedule** with **Dynamic-only** lookback. This document is the ops runbook.

## CRITICAL: First run must be CLI (fail-closed cold start)

Cloud Discovery **will not invent a lookback window** when there is no prior success.

| Symptom | Cause |
|---------|--------|
| `activity:Discover` / orchestration fails in App Insights `AppRequests` with `Success=false` | Lookback fail-closed |
| Traces mention `DiscoveryLookbackUnavailableException` or `fail-closed` / `no prior discoveryBegan` | No watermark (or Cosmos read failed) |
| Timer ticks every 30 minutes at scheduled UK times but Discover activity errors immediately | Same — cold start |

**Cause:** Dynamic lookback requires a Cosmos `discoveryBegan` watermark from a previous successful Discovery results document. There is **no** ColdStartLookback / Static `SearchSince` fallback. Empty or null watermark → **fail closed**.

**Fix — seed the first success with the console app**, then the timer takes over:

```powershell
# From RedditPodcastPoster repo root (publish first if needed)
.\scripts\publish-console-apps.ps1
# or: dotnet publish Console-Apps/Discover/Discover.csproj -c Release -r win-x64

$exe = ".\Console-Apps\Discover\bin\Release\net10.0\win-x64\Discover.exe"
# Choose an intentional window (UTC). Example: last 24 hours:
& $exe --time-since (Get-Date).ToUniversalTime().AddHours(-24).ToString("yyyy-MM-ddTHH:mm:ssZ") `
  --include-listen-notes --include-taddy --include-youtube `
  --taddy-offset 02:00:00
```

Requires Cosmos credentials via user secrets / `RedditPodcastPoster_*` env (see [discovery-backfill.md](./discovery-backfill.md)).

After a successful CLI run persists a Discovery results document, the next scheduled timer run resolves `since = lastSuccess - DynamicLookbackOverlap` (default 10 minutes).

## UK schedule

| Piece | Behaviour |
|-------|-----------|
| Azure Timer | `0 */30 * * * *` (every 30 minutes) |
| Gate | UK local time (`GMT Standard Time` / `Europe/London`) must match a Cosmos `runTimes` entry within ±15 minute grace |
| Slot id | UK date + `HH:mm`, e.g. `2026-07-19 08:00 UK` |
| Prior slot | Previous entry on the sorted schedule (not a fixed −6h) |
| Defaults | If LookUps document missing: `runTimes=["08:00","22:00"]`, `enabled=true` (function does not hard-fail) |
| DST | Spring-forward missing local times are skipped (Dynamic recovers the gap). Fall-back uses a stable first offset so each slot id runs once. |

### Edit schedule (website)

Admin menu → **Discovery Schedule**: toggle 30-minute chips, preview next runs, Save. Calls `PUT /discovery-schedule` (admin via Cloudflare; Azure Function requires `curate`).

Or API (Cloudflare worker proxies to Azure Function `api-infra` route `discovery-schedule`):

```http
GET  /discovery-schedule
PUT  /discovery-schedule
Content-Type: application/json

{ "runTimes": ["08:00", "22:00"], "enabled": true }
```

**Cloudflare Worker secret (required for website/API gateway):** set `secureDiscoveryScheduleEndpoint` to the Azure Function URL for `discovery-schedule` (same pattern as other `secure*Endpoint` secrets). Local: add the same key to `.dev.vars`.

Cosmos LookUps singleton: `DiscoveryScheduleConfig` (`type: DiscoveryScheduleConfig`).

## Dynamic-only lookback

- `discover__SearchSince` and `discover__LookbackMode` / Static mode are **retired**.
- Production keeps `discover__DynamicLookbackOverlap` (default `00:10:00`).
- Document field `searchSince` still records the **observed** window duration for each run.
- Intentional fixed windows: always use CLI `-t` / `--time-since` (see [discovery-backfill.md](./discovery-backfill.md)).

## Migration from 4× UTC cron + Static SearchSince

| Before | After |
|--------|-------|
| Cron `0 33 2/6 * * *` (02:33/08:33/14:33/20:33 UTC) | Every 30 min + UK `runTimes` gate |
| Optional Static floor `6:10:00` | Removed |
| Dynamic fallback to Static when no watermark | **Fail closed** — CLI first |
| Slot = UTC hour:33, prior = −6h | Slot = UK date+HH:mm, prior = previous runTime |

Steps:

1. Deploy discover-infra + api-infra with this change (script blob deploy — see [deployment.md](./deployment.md)).
2. Create/edit schedule via website (or accept defaults 08:00 & 22:00 UK).
3. If Cosmos has **no** prior Discovery success document: run CLI once (section above).
4. Confirm next due UK slot: `AppRequests` for `discover-infra` show orchestration + `activity:Discover` Success=true.

## Related

- Backfill / missed windows: [discovery-backfill.md](./discovery-backfill.md)
- Execution proof: `.cursor/rules/production-execution-truth.mdc` (`AppRequests`, not traces alone)
