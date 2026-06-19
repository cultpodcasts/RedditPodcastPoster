# Indexing investigation — Jakub Jahl (Jun 2026)

**KQL quick reference:** [docs/indexing-app-insights-queries.md](docs/indexing-app-insights-queries.md) — copy-paste queries for 06:00/18:00 runs, `operation_Id` deep-dives, batch 4, key rotation, timers, and quota report.

**Goal:** Determine whether podcast **"Jakub Jahl: Sekty a světová náboženství"** was indexed by scheduled runs after the Jun 2026 fixes, and **why not** if it was missed.

| Field | Value |
|-------|-------|
| Podcast name | Jakub Jahl: Sekty a světová náboženství |
| Podcast ID | `8a0c0f4e-79e0-4d87-bcd5-2156fc0d2f9e` |
| Suspected batch | **4** (upper half of index pool) |
| YouTube episodes of interest | `hh4MIFHUzRM`, `wuSWvcS2Yfo` |

---

## Context summary

### Original issue

Scheduled hourly indexing was **missing new episodes** (especially YouTube URLs on Spotify/Apple-first podcasts). A **manual API index** of the same podcast succeeded immediately — proving Cosmos, handlers, and enrichment work when YouTube retrieval actually runs.

### Fix 1 — PR #861 (merged, batch rotation)

**Commit:** `6b551c44` — *Rotate YouTube-enabled indexer passes across all batch halves (#861)*

**Problem:** YouTube URL resolving runs only when `hour % 6 == 0` (hours **0, 6, 12, 18 UTC**). With 4 index passes, the old `hour % 4` primary-pass logic meant **batches 3–4 rarely coincided with a YouTube-enabled hour**.

**Fix:** `HourlyIndexingPassSelector` alternates which batch pair runs on each hour so that:

| UTC hour | Batches run | YouTube enabled? |
|----------|-------------|------------------|
| 0, 12 | 1–2 | Yes |
| **6, 18** | **3–4** | **Yes** |
| Other hours | Alternating 1–2 or 3–4 | No |

Jakub Jahl is in **batch 4**. Its YouTube-enabled scheduled windows are **06:00 and 18:00 UTC** only (twice per day).

### Fix 2 — branch `cursor/align-apple-spotify-enrichment-youtube-delay`

**Commit:** `4c98a7e6` — *Fix scheduled YouTube episode discovery when Spotify handles retrieval first.*

**Not on `main` at time of writing** — verify whether `indexer-infra` was deployed from this branch.

Changes relevant to Jakub Jahl:

1. **`EpisodeProvider`** — always runs YouTube retrieval when `SkipYouTubeUrlResolving` is false and `YouTubeChannelId` is set, **even after** Spotify/Apple handled episode discovery first (previously YouTube was skipped when `handled == true`).
2. **`IndexIdProvider`** — merged query now projects `ReleaseAuthority`, `YouTubeChannelId`, `SpotifyId`, `AppleId` for audit pool logging.
3. **`PodcastsUpdater`** — `YouTubeAuthorityPodcastAudit` and `YouTubeAuthorityIndexingAudit` logs per batch.
4. **`Podcast.LastIndexed`** — set on successful index (no bypass, no merge failures); persisted to Cosmos.
5. **`DependsOnYouTubeForEpisodeDiscovery`** — audit scope for YouTube-authority podcasts only.

### Jakub Jahl profile (expected)

- **Batch 4** — confirm via `IndexIdProvider` `Batch 4:` log each hour.
- **Likely NOT** `ReleaseAuthority = YouTube` — probably has **Spotify and/or Apple** IDs for episode discovery.
- **Has** `YouTubeChannelId` and/or `YouTubePlaylistId` for URL enrichment.
- **`DependsOnYouTubeForEpisodeDiscovery` → false** when Spotify or Apple can discover episodes. Audit logs (`YouTubeAuthorityPodcastAudit`) will **not** fire for this podcast; use other signals below.

### Logging limitations

| Component | Logs podcast ID? | What to use instead |
|-----------|------------------|---------------------|
| `activity:Indexer` (Durable) | **No** | `IndexIdProvider` batch arrays, `PodcastUpdater` by name, audit logs |
| `IndexIdProvider` | **Yes** (in batch GUID arrays) | `Batch 4:` lines |
| `PodcastUpdater` / `PodcastsUpdater` | **Yes** (name + id in result reports) | `Podcast: 'Jakub Jahl...'` |
| `YouTubeAuthorityPodcastAudit` | Yes | Only for YouTube-authority pool podcasts |
| Cosmos `lastIndexed` | Yes | Direct ground truth |

---

## Pre-requisites

### Azure login

```powershell
az login
az account set --subscription "Cultpodcasts"   # a6b8f1a2-6163-41bc-aa6d-e33928939a6e
```

### Application Insights

| Setting | Value |
|---------|-------|
| Resource group | `AutomatedInfra` |
| App Insights resource | `ai-infra` |
| Application ID | `9005e913-7271-45e9-8358-4b3177d0b56d` |
| Function app | `indexer-infra` (`AppRoleName`) |

**Important:** Query the **Application Insights resource** (`ai-infra`), not the Log Analytics workspace alone. Workspace queries can truncate lookback windows.

#### az cli query template

```powershell
$appId = "9005e913-7271-45e9-8358-4b3177d0b56d"
$query = @'
traces
| where timestamp > ago(48h)
| where cloud_RoleName == "indexer-infra"
| take 10
'@

az monitor app-insights query `
  --app $appId `
  --analytics-query $query `
  --offset 48h `
  -o json
```

Alternative (resource name):

```powershell
az monitor app-insights query `
  -g AutomatedInfra `
  --app ai-infra `
  --analytics-query "requests | where timestamp > ago(24h) | take 5" `
  --offset 24h
```

Portal: **Application Insights → ai-infra → Logs** (not Log Analytics workspace explorer).

### Cosmos DB

| Setting | Value |
|---------|-------|
| Account | `cultpodcasts-db` (resource group `automateddata`) |
| Database | `cultpodcasts-db` |
| Podcasts container | `Podcasts` |
| Episodes container | `Episodes` |

#### Podcast document check

```powershell
az cosmosdb sql query `
  --account-name cultpodcasts-db `
  --resource-group automateddata `
  --database-name cultpodcasts-db `
  --container-name Podcasts `
  --query-text "SELECT c.id, c.name, c.lastIndexed, c.releaseAuthority, c.youTubeChannelId, c.youTubePlaylistId, c.spotifyId, c.appleId, c.latestReleased, c.indexAllEpisodes FROM c WHERE c.id = '8a0c0f4e-79e0-4d87-bcd5-2156fc0d2f9e'"
```

#### Episode YouTube URL check

```powershell
az cosmosdb sql query `
  --account-name cultpodcasts-db `
  --resource-group automateddata `
  --database-name cultpodcasts-db `
  --container-name Episodes `
  --query-text "SELECT c.id, c.title, c.release, c.urls FROM c WHERE c.podcastId = '8a0c0f4e-79e0-4d87-bcd5-2156fc0d2f9e' AND (c.urls.youTube contains 'hh4MIFHUzRM' OR c.urls.youTube contains 'wuSWvcS2Yfo') ORDER BY c.release DESC"
```

Fields to record:

- `lastIndexed` — null or stale → scheduled index not succeeding post-deploy
- `releaseAuthority` — `YouTube` vs null/`Spotify`/`Apple`
- `youTubeChannelId`, `youTubePlaylistId`, `spotifyId`, `appleId` — discovery path
- Episode `urls.youTube` — presence of `hh4MIFHUzRM` / `wuSWvcS2Yfo`

### Deploy reference dates

| Deploy | Approx date (UTC) | Contents |
|--------|-------------------|----------|
| P1/P5 orchestration preload | **2026-06-15** ~21:20 UTC | LoadRecentCandidates, posting lookback |
| PR #861 batch rotation | Verify in traces | `Selected indexer passes 3-4` at hours 6/18 |
| Fix 2 (YouTube after Spotify) | **Deploy if not done** | Branch `cursor/align-apple-spotify-enrichment-youtube-delay` |

Use `datetime(2026-06-15T21:20:00Z)` as the before/after cutover for P1/P5. For Fix 2, use the actual `indexer-infra` deploy timestamp once known.

---

## Investigation checklist

Work through in order. Stop early if a step proves root cause.

### 1. Was the fix deployed?

**Look for:** `Selected indexer passes 3-4 for UTC hour 6` (or `18`) in traces **after** deploy.

```kusto
traces
| where timestamp > datetime(2026-06-15T00:00:00Z)
| where cloud_RoleName == "indexer-infra"
| where message has "Selected indexer passes"
| where message has "for UTC hour 6" or message has "for UTC hour 18"
| project timestamp, message
| order by timestamp desc
```

- **If absent after Jun 15:** PR #861 not deployed — batch 4 never gets YouTube windows.
- **If present:** rotation fix is live; continue.

Also confirm Fix 2 deploy: search for `YouTubeAuthorityIndexPool` or `LastIndexed` behaviour (Fix 2 adds audit + field). If only P1/P5 deployed, YouTube-after-Spotify bug may still exist.

### 2. Is the podcast in the index pool (batch 4)?

`IndexIdProvider` runs once per hourly orchestration and logs all batches.

```kusto
let podcastId = "8a0c0f4e-79e0-4d87-bcd5-2156fc0d2f9e";
traces
| where timestamp > ago(7d)
| where cloud_RoleName == "indexer-infra"
| where message has "Batch 4:"
| where message has podcastId
| project timestamp, message
| order by timestamp desc
```

- **If never in Batch 4:** podcast may not match indexable query (`IndexAllEpisodes` or `EpisodeIncludeTitleRegex`), or was removed.
- **If in Batch 4:** scheduling pool is correct; continue.

### 3. Did batch 4 run with YouTube enabled?

At hours **6** and **18 UTC**, passes 3–4 must run with `bypass-youtube: 'False'`.

```kusto
traces
| where timestamp > ago(48h)
| where cloud_RoleName == "indexer-infra"
| where message has "Selected indexer passes 3-4"
| join kind=inner (
    traces
    | where message has "Indexer pass 4 indexing-context"
    | extend bypassYouTube = extract(@"bypass-youtube: '(True|False)'", 1, message)
    | project timestamp, bypassYouTube, passMessage = message
) on $left.timestamp == $right.timestamp
| project timestamp, bypassYouTube, passMessage
| order by timestamp desc
```

Simpler — pass 4 context only on YouTube hours:

```kusto
traces
| where timestamp > ago(48h)
| where cloud_RoleName == "indexer-infra"
| where message has "Indexer pass 4 indexing-context"
| extend bypassYouTube = extract(@"bypass-youtube: '(True|False)'", 1, message)
| where hourofday(timestamp) in (6, 18)
| project timestamp, bypassYouTube, message
| order by timestamp desc
```

- **`bypassYouTube == False` at 6/18:** YouTube pass reached batch 4 — continue.
- **`True` at 6/18:** rotation or strategy bug / wrong deploy.
- **No pass-4 log at 6/18:** orchestration didn't run or failed before Indexer.

### 4. Audit logs post-deploy (YouTube-authority pool only)

Jakub Jahl is **probably not** in this pool. Run anyway to confirm pool membership:

```kusto
let podcastId = "8a0c0f4e-79e0-4d87-bcd5-2156fc0d2f9e";
traces
| where timestamp > ago(7d)
| where message has "YouTubeAuthorityPodcastAudit"
| where message has podcastId
| project timestamp, message
| order by timestamp desc
```

```kusto
traces
| where timestamp > ago(7d)
| where message has "YouTubeAuthorityIndexPool"
| where message has "8a0c0f4e-79e0-4d87-bcd5-2156fc0d2f9e"
| project timestamp, message
```

- **No rows expected** for Spotify/Apple-discovery podcasts — use steps 5–6 instead.

### 5. Episode-specific: were `hh4MIFHUzRM` and `wuSWvcS2Yfo` ingested?

#### Cosmos (ground truth)

See Cosmos queries in Pre-requisites. Episodes may exist from Spotify/Apple without YouTube URLs.

#### App Insights — podcast by name

Czech characters can break `has` filters. Use partial ASCII match:

```kusto
traces
| where timestamp > datetime(2026-06-15T21:20:00Z)
| where cloud_RoleName == "indexer-infra"
| where message has "Jakub Jahl"
| project timestamp, severityLevel, message
| order by timestamp desc
```

Look for:

- `Podcast: 'Jakub Jahl` — `IndexPodcastResult` with added/merged/enriched episodes
- `Get Episodes for podcast 'Jakub Jahl` — `handled by` **YouTube** handler (Fix 2)
- `youTubeRefreshed: True` in Poster/Tweet/Bluesky (hour-level, not per-podcast)

#### YouTube video IDs in logs

```kusto
traces
| where timestamp > ago(7d)
| where message has "hh4MIFHUzRM" or message has "wuSWvcS2Yfo"
| project timestamp, message
| order by timestamp desc
```

### 6. `lastIndexed` in Cosmos after deploy

```sql
SELECT c.lastIndexed, c.latestReleased
FROM c
WHERE c.id = "8a0c0f4e-79e0-4d87-bcd5-2156fc0d2f9e"
```

- **Updated within 24h of a 6:00 or 18:00 UTC run:** scheduled index reached this podcast successfully.
- **Null or pre-deploy timestamp:** scheduled pass did not complete successfully for this podcast (or Fix 2 not deployed — field won't exist on old builds).

---

## App Insights Kusto queries (copy-paste)

Set `let podcastId = "8a0c0f4e-79e0-4d87-bcd5-2156fc0d2f9e";` at top where needed.

### Batch 4 membership for this GUID

```kusto
let podcastId = "8a0c0f4e-79e0-4d87-bcd5-2156fc0d2f9e";
traces
| where timestamp > ago(7d)
| where cloud_RoleName == "indexer-infra"
| where message has "Batch 4:"
| where message has podcastId
| summarize count(), max(timestamp) by bin(timestamp, 1d)
| order by timestamp desc
```

### Selected indexer passes 3–4 at hours 6/18

```kusto
traces
| where timestamp > ago(7d)
| where cloud_RoleName == "indexer-infra"
| where message has "Selected indexer passes"
| extend hour = extract(@"UTC hour (\d+)", 1, message)
| where hour in ("6", "18")
| extend passes = extract(@"passes (\d+-\d+)", 1, message)
| where passes == "3-4"
| project timestamp, hour, message
| order by timestamp desc
```

### `youTubeRefreshed` for Jakub Jahl (by name)

Hour-level flag from Poster/Tweet/Bluesky; pair with podcast-specific logs:

```kusto
traces
| where timestamp > ago(48h)
| where cloud_RoleName == "indexer-infra"
| where message has "Jakub Jahl"
| where message has "youTubeRefreshed" or message has "YouTubeEpisodeRetrievalHandler" or message has "bypass-youtube"
| project timestamp, message
| order by timestamp desc
```

### `YouTubeAuthorityPodcastAudit` for this podcast-id

```kusto
traces
| where timestamp > ago(7d)
| where message has "YouTubeAuthorityPodcastAudit"
| where message has "8a0c0f4e-79e0-4d87-bcd5-2156fc0d2f9e"
| project timestamp, message
| order by timestamp desc
```

### Missing YouTube pass in 24h (pool vs indexed)

Compare YouTube pool size vs batch audits (YouTube-authority podcasts only):

```kusto
traces
| where timestamp > ago(24h)
| where message has "YouTubeAuthorityIndexPool" or message has "YouTubeAuthorityIndexingAudit"
| project timestamp, message
| order by timestamp asc
```

For Jakub Jahl (non-authority), compare **batch-4 YouTube hours** vs **podcast name** logs:

```kusto
traces
| where timestamp > ago(24h)
| where cloud_RoleName == "indexer-infra"
| where (message has "Selected indexer passes 3-4" and hourofday(timestamp) in (6, 18))
    or (message has "Jakub Jahl" and message has "Podcast:")
| project timestamp, message
| order by timestamp asc
```

### Podcast name in PodcastUpdater / merge / enrichment logs

```kusto
traces
| where timestamp > ago(7d)
| where cloud_RoleName == "indexer-infra"
| where message has "Jakub Jahl"
| where message has "Podcast:" or message has "Get Episodes" or message has "AddedEpisodes" or message has "Enrichment"
| project timestamp, severityLevel, message
| order by timestamp desc
| take 50
```

### Indexer pass `indexing-context` / bypass-youtube

```kusto
traces
| where timestamp > ago(48h)
| where cloud_RoleName == "indexer-infra"
| where message has "indexing-context"
| extend pass = extract(@"Indexer pass (\d+)", 1, message)
| extend bypassYouTube = extract(@"bypass-youtube: '(True|False)'", 1, message)
| extend indexSpotify = extract(@"index-spotify: (True|False)", 1, message)
| project timestamp, pass, bypassYouTube, indexSpotify, message
| order by timestamp desc
```

### Compare before/after deploy `datetime(2026-06-15T21:20:00Z)`

```kusto
let deployTime = datetime(2026-06-15T21:20:00Z);
let podcastId = "8a0c0f4e-79e0-4d87-bcd5-2156fc0d2f9e";
traces
| where timestamp between (deployTime - 7d) .. (deployTime + 7d)
| where cloud_RoleName == "indexer-infra"
| where message has "Jakub Jahl" or message has podcastId
| extend period = iff(timestamp < deployTime, "before", "after")
| summarize count(), max(timestamp) by period, messageStart = substring(message, 0, 80)
| order by period, count_ desc
```

Batch rotation before/after:

```kusto
let deployTime = datetime(2026-06-15T21:20:00Z);
traces
| where timestamp between (deployTime - 3d) .. (deployTime + 3d)
| where message has "Selected indexer passes"
| extend period = iff(timestamp < deployTime, "before", "after")
| extend hour = toint(extract(@"UTC hour (\d+)", 1, message))
| extend passes = extract(@"passes (\d+-\d+)", 1, message)
| where hour in (6, 18)
| summarize count() by period, passes
| order by period, passes
```

---

## Decision tree

```
START: Was Jakub Jahl indexed on schedule after deploy?
│
├─ Cosmos lastIndexed updated after deploy at ~06:00 or ~18:00 UTC?
│  └─ YES → Likely indexed. Confirm YouTube URLs on hh4MIFHUzRM / wuSWvcS2Yfo in Cosmos.
│  └─ NO  → Continue ▼
│
├─ "Selected indexer passes 3-4" at hours 6 or 18 after Jun 15?
│  └─ NO  → PR #861 not deployed. Deploy indexer-infra from main.
│  └─ YES → Continue ▼
│
├─ Podcast GUID in "Batch 4:" logs?
│  └─ NO  → Not indexable (check IndexAllEpisodes / EpisodeIncludeTitleRegex / Removed).
│  └─ YES → Continue ▼
│
├─ At 6/18 UTC: "Indexer pass 4" with bypass-youtube 'False'?
│  └─ NO  → Batch rotation or IndexingStrategy bug; check deploy version.
│  └─ YES → Continue ▼
│
├─ Any "Jakub Jahl" logs at 6/18 runs?
│  └─ NO  → Batch ran but podcast not in that batch slice (re-check Batch 4 membership).
│  └─ YES → Continue ▼
│
├─ Logs show Spotify/Apple handler but NOT YouTube handler?
│  └─ YES → Fix 2 not deployed. EpisodeProvider still skips YouTube when Spotify handles first.
│           Deploy branch cursor/align-apple-spotify-enrichment-youtube-delay.
│  └─ NO  → Continue ▼
│
├─ YouTube handler ran but no AddedEpisodes / enrichment?
│  └─ YES → YouTube API / playlist / title mismatch / expensive-query bypass.
│           Check bypass-expensive-youtube-queries on primary pass (hour 0 only for expensive).
│  └─ NO  → Continue ▼
│
├─ Episodes in Cosmos without YouTube URLs; youTubeRefreshed False in Poster?
│  └─ YES → Index ran on non-YouTube hour OR YouTubeError on IndexerContext.
│  └─ NO  → Episodes fully ingested — investigate why posting didn't pick them up (separate path).
│
└─ Manual API index works but schedule doesn't?
   → Almost always: YouTube pass never coincided with batch (pre-#861) OR
     YouTube skipped after Spotify (pre-Fix-2). Deploy both fixes and wait for next 06:00/18:00 UTC.
```

---

## What success looks like after deploy

After **both** PR #861 and Fix 2 are on `indexer-infra`:

1. **06:00 and 18:00 UTC** hourly runs log `Selected indexer passes 3-4 for UTC hour 6` (or `18`).
2. **Pass 4** logs `bypass-youtube: 'False'` on those runs.
3. For Jakub Jahl, logs show **Spotify or Apple handler** then **`YouTubeEpisodeRetrievalHandler`** (Fix 2).
4. `IndexPodcastResult` reports added episodes and/or YouTube enrichment for `Jakub Jahl`.
5. Cosmos **`lastIndexed`** updates within minutes of that run.
6. Episodes gain **`urls.youTube`** containing `hh4MIFHUzRM` / `wuSWvcS2Yfo` without manual API index.
7. Downstream: `youTubeRefreshed: True` on the same hourly run (Poster/Tweet/Bluesky).

---

## Deploy reminder

If Fix 2 (or latest `main` + branch) is **not** yet on production:

```powershell
az login
git checkout cursor/align-apple-spotify-enrichment-youtube-delay
git pull

# From repo root — packages and deploys to indexer-infra (AutomatedInfra)
.\scripts\deploy-indexer.ps1
```

This publishes `Cloud/Indexer`, zips, uploads to `cultpodcastsstg/indexer-deployment/released-package.zip`, and restarts `indexer-infra`. **Does not change app settings** (bicep-only).

After deploy, note the UTC timestamp and wait for the next **06:00 or 18:00 UTC** window before concluding batch-4 YouTube indexing failed.

CI equivalent: push to trigger [`.github/workflows/deploy.yml`](.github/workflows/deploy.yml) (builds all three function apps when configured).

---

## Quick reference — code paths

| File | Role |
|------|------|
| `Cloud/Indexer/HourlyIndexingPassSelector.cs` | Maps UTC hour → passes 1–2 or 3–4; 6/18 → 3–4 + YouTube |
| `Cloud/Indexer/IndexingStrategy.cs` | `ResolveYouTube()` → `hour % 6 == 0` |
| `Cloud/Indexer/IndexIdProvider.cs` | Builds batches; `YouTubeAuthorityIndexPool` audit |
| `Class-Libraries/.../EpisodeProvider.cs` | Spotify/Apple then YouTube (Fix 2) |
| `Class-Libraries/.../PodcastsUpdater.cs` | `YouTubeAuthorityPodcastAudit` per podcast |
| `Class-Libraries/.../PodcastUpdater.cs` | Sets `LastIndexed` on success |
| `Class-Libraries/.../PodcastExtensions.cs` | `DependsOnYouTubeForEpisodeDiscovery` |

---

*Created for handoff — Jun 2026. Do not commit unless asked.*
