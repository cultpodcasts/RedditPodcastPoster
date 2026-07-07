# Indexing diagnostics — App Insights / Log Analytics KQL

Operational query reference for diagnosing scheduled indexing failures on **`indexer-infra`**, especially **06:00 / 18:00 UTC** YouTube windows (batches 3–4) and the **Jakub Jahl** case study.

**Related:** [indexing-investigation-jun2026.md](../indexing-investigation-jun2026.md) — full decision tree, Cosmos checks, deploy notes.

| Field | Value |
|-------|-------|
| Log Analytics workspace | `loganalytics-infra` |
| Workspace ID | `2b1c62ee-689f-422a-816b-be1605ae88fa` |
| Function app / role | `indexer-infra` (`AppRoleName` / `cloud_RoleName`) |
| App Insights resource | `ai-infra` (RG `AutomatedInfra`) |
| App Insights application ID | `9005e913-7271-45e9-8358-4b3177d0b56d` |
| Example indexer pass `operation_Id` | `5289449922f7727fbc0d998274a780a9` (batch 4, 2026-06-19 ~18:00 UTC) |
| Jakub Jahl podcast ID | `8a0c0f4e-79e0-4d87-bcd5-2156fc0d2f9e` |

---

## Prerequisites

```powershell
az login
az account set --subscription "Cultpodcasts"   # a6b8f1a2-6163-41bc-aa6d-e33928939a6e
```

### az CLI — Log Analytics workspace (preferred for this repo)

```powershell
$workspaceId = "2b1c62ee-689f-422a-816b-be1605ae88fa"

$query = @'
AppTraces
| where TimeGenerated > ago(24h)
| where AppRoleName == "indexer-infra"
| where Message has "RunHourly"
| project TimeGenerated, Message
| order by TimeGenerated desc
| take 20
'@

az monitor log-analytics query `
  -w $workspaceId `
  --analytics-query $query `
  -o json
```

Single-line variant:

```powershell
az monitor log-analytics query -w 2b1c62ee-689f-422a-816b-be1605ae88fa --analytics-query "AppTraces | where TimeGenerated > ago(24h) | where AppRoleName == 'indexer-infra' | where Message has 'RunHourly' | project TimeGenerated, Message | take 10"
```

### az CLI — Application Insights (classic table names)

```powershell
$appId = "9005e913-7271-45e9-8358-4b3177d0b56d"

az monitor app-insights query `
  --app $appId `
  --analytics-query "traces | where timestamp > ago(24h) | where cloud_RoleName == 'indexer-infra' | take 10" `
  --offset 24h `
  -o json
```

### Table / column cheat sheet

| Log Analytics workspace | Application Insights (classic) |
|-------------------------|--------------------------------|
| `AppTraces` | `traces` |
| `TimeGenerated` | `timestamp` |
| `Message` | `message` |
| `AppRoleName` | `cloud_RoleName` |
| `OperationId` | `operation_Id` |
| `SeverityLevel` (3 = Warning) | `severityLevel` (2 = Warning) |

**Production note:** `RedditPodcastPoster` namespaces log at **Warning** in production for cost control. Diagnostic logs added for indexing investigation are **Warning-level** and queryable without lowering filters. Older Information-level strings (`BuildIndexerKeyRing`, `Indexer pass N indexing-context`) may be absent in live telemetry.

---

## Execution audit guardrails (agents — HARD)

**Do not claim a run "did not start" from `AppTraces` alone.** Durable execution is proven by **`AppRequests`** (`orchestration:HourlyOrchestration`, `activity:Indexer`, …). Jul 2026: 9 PM UK hourly showed full orchestration in App Insights requests while Log Analytics `AppTraces` had only host startups — wrong trace-only audits caused false "missed run" reports.

**Required before verdict:**

```kusto
AppRequests
| where TimeGenerated between (datetime(<start>) .. datetime(<end>))
| where AppRoleName == "indexer-infra"
| where Name startswith "orchestration:HourlyOrchestration"
    or Name startswith "create_orchestration:HourlyOrchestration"
    or Name == "activity:Indexer"
    or Name == "activity:Bluesky"
| project TimeGenerated, Name, Success, DurationMs, OperationId
| order by TimeGenerated asc
```

| Outcome | When to say it |
|---------|----------------|
| **Succeeded** | `orchestration:HourlyOrchestration` + activities, `Success == true` |
| **Failed** | Request exists with `Success == false` |
| **Unconfirmed** | Empty `AppRequests`, slot <30 min — ingestion lag; retry |
| **Likely missed** | Empty `AppRequests` >30 min after slot, no portal E2E transaction |

Cursor rule: [`.cursor/rules/production-execution-truth.mdc`](../.cursor/rules/production-execution-truth.mdc) (`alwaysApply: true`).

---

## Warning-level diagnostic log catalog

Deployed or pending (diagnostic logging work — `HourlyOrchestration`, `OrchestrationTrigger`, `Indexer`, `IndexIdProvider`, `PodcastsUpdater`, `YouTubeEpisodeRetrievalHandler`):

| Message prefix | Emitted by | Use |
|----------------|------------|-----|
| `OrchestrationTrigger RunHourly initiated` | `OrchestrationTrigger` | Timer fired at top of hour |
| `OrchestrationTrigger hourly-scheduled` | `OrchestrationTrigger` | Durable instance scheduled (`trigger`, `hour-utc`, `instance-id`) |
| `RunHourly skipped. Existing 'HourlyOrchestration' instance` | `OrchestrationTrigger` | Overlap — hourly skipped |
| `HourlyOrchestration pass-selection` | `HourlyOrchestration` | `first-pass`, `last-pass`, `youtube-enabled-hour` |
| `HourlyOrchestration indexer-operation-ids` | `HourlyOrchestration` | Maps pass 1–4 → `operation_Id` for deep-dive |
| `HourlyOrchestration indexer-pass-complete` | `HourlyOrchestration` | Per-pass rollup: `success`, `skip-youtube`, `youtube-error` |
| `HourlyOrchestration batch-4-rollup` | `HourlyOrchestration` | Batch 4 only — same fields as pass-complete |
| `IndexerPassStart` / `IndexerPassComplete` | `Indexer` activity | Pass start/end with `operation-id`, YouTube flags |
| `IndexerCostProbe.Update` | `Indexer` activity | `update-ms` for batch duration |
| `IndexIdProvider batch-4-summary` | `IndexIdProvider` | Podcast count in batch 4 |
| `YouTubeDiscoveryPath` | `YouTubeEpisodeRetrievalHandler` | Warning when YouTube-authority; Info otherwise |
| `YouTubeAuthorityPodcastAudit` | `PodcastsUpdater` | Per YouTube-authority podcast |
| `YouTubeAuthorityIndexingAudit` | `PodcastsUpdater` | Batch-level YouTube-authority summary |

Jakub Jahl is **usually not** YouTube-authority (Spotify/Apple discovery). For Jakub, prefer `Batch 4:` membership, podcast name logs, and `YouTubeDiscoveryPath` at Info level.

---

## 1. Quick start — verify 06:00 / 18:00 run fired

YouTube-enabled scheduled indexing for **batch 4** runs only when **passes 3–4** execute at **06:00 or 18:00 UTC** (`hour % 6 == 0`).

### A. Timer + orchestration scheduled (last 24h)

**Log Analytics:**

```kusto
AppTraces
| where TimeGenerated > ago(24h)
| where AppRoleName == "indexer-infra"
| where Message has "OrchestrationTrigger RunHourly initiated"
    or Message has "OrchestrationTrigger hourly-scheduled"
    or Message has "HourlyOrchestration.RunAsync initiated"
| extend hourUtc = hourofday(TimeGenerated)
| project TimeGenerated, hourUtc, Message
| order by TimeGenerated desc
```

**Application Insights:**

```kusto
traces
| where timestamp > ago(24h)
| where cloud_RoleName == "indexer-infra"
| where message has "RunHourly initiated"
    or message has "hourly-scheduled"
    or message has "HourlyOrchestration.RunAsync initiated"
| extend hourUtc = hourofday(timestamp)
| project timestamp, hourUtc, message
| order by timestamp desc
```

### B. Pass selection at 06 / 18 (new Warning logs)

```kusto
AppTraces
| where TimeGenerated > ago(48h)
| where AppRoleName == "indexer-infra"
| where Message has "HourlyOrchestration pass-selection"
| extend hourUtc = toint(extract(@"hour-utc='(\d+)'", 1, Message))
| extend firstPass = extract(@"first-pass='(\d+)'", 1, Message)
| extend lastPass = extract(@"last-pass='(\d+)'", 1, Message)
| extend youtubeEnabled = extract(@"youtube-enabled-hour='(True|False)'", 1, Message)
| where hourUtc in (6, 18)
| project TimeGenerated, hourUtc, firstPass, lastPass, youtubeEnabled, Message
| order by TimeGenerated desc
```

**Expect at 06/18:** `first-pass='3'`, `last-pass='4'`, `youtube-enabled-hour='True'`.

Legacy fallback (if Warning logs not yet deployed):

```kusto
traces
| where timestamp > ago(48h)
| where cloud_RoleName == "indexer-infra"
| where message has "Selected indexer passes 3-4"
| where hourofday(timestamp) in (6, 18)
| project timestamp, message
| order by timestamp desc
```

### C. Daily count — orchestrations per hour (miss detection)

```kusto
traces
| where timestamp > ago(7d)
| where cloud_RoleName == "indexer-infra"
| where message has "HourlyOrchestration.RunAsync initiated"
| extend hour = hourofday(timestamp)
| summarize count() by bin(timestamp, 1d), hour
| order by timestamp desc, hour asc
```

Expect **one** `HourlyOrchestration` per UTC hour (24/day). Gaps at 06 or 18 → check section 5 (timer / catch-up / cold start).

### D. Narrow time window (example: 2026-06-19 18:00 UTC)

```kusto
AppTraces
| where TimeGenerated between (datetime(2026-06-19T17:58:00Z) .. datetime(2026-06-19T18:05:00Z))
| where AppRoleName == "indexer-infra"
| where Message has "RunHourly"
    or Message has "pass-selection"
    or Message has "Selected indexer passes 3-4"
    or Message has "batch-4-rollup"
| project TimeGenerated, Message
| order by TimeGenerated asc
```

---

## 2. Single operation deep-dive (`operation_Id`)

Each indexer pass gets a durable `operation_Id`. Example **batch 4 pass** from 2026-06-19 ~18:00 UTC:

`5289449922f7727fbc0d998274a780a9`

### A. Resolve pass operation IDs from orchestration (new Warning logs)

```kusto
AppTraces
| where TimeGenerated between (datetime(2026-06-19T17:58:00Z) .. datetime(2026-06-19T18:30:00Z))
| where AppRoleName == "indexer-infra"
| where Message has "HourlyOrchestration indexer-operation-ids"
| project TimeGenerated, Message
```

Parse: `pass-4='{operation_Id}'`.

### B. All telemetry for one `operation_Id`

**Log Analytics:**

```kusto
let op = "5289449922f7727fbc0d998274a780a9";
union AppTraces, AppRequests, AppDependencies, AppExceptions
| where OperationId == op
| project TimeGenerated, ItemType, SeverityLevel, Message = coalesce(Message, Name), ResultCode, DurationMs
| order by TimeGenerated asc
```

**Application Insights:**

```kusto
let op = "5289449922f7727fbc0d998274a780a9";
union traces, requests, dependencies, exceptions
| where operation_Id == op
| project timestamp, itemType, severityLevel, message = coalesce(message, name), resultCode, duration
| order by timestamp asc
```

### C. Warning diagnostics only (indexer pass timeline)

```kusto
let op = "5289449922f7727fbc0d998274a780a9";
AppTraces
| where OperationId == op
| where SeverityLevel >= 3
    or Message has "IndexerPass"
    or Message has "IndexerCostProbe"
    or Message has "YouTubeDiscoveryPath"
    or Message has "YouTubeAuthority"
    or Message has "BuildIndexerKeyRing"
    or Message has "Rotate indexer"
    or Message has "quota"
    or Message has "Jakub"
    or Message has "8a0c0f4e"
| project TimeGenerated, SeverityLevel, Message
| order by TimeGenerated asc
```

### D. Pass 4 rollup vs activity logs

```kusto
let op = "5289449922f7727fbc0d998274a780a9";
AppTraces
| where TimeGenerated between (datetime(2026-06-19T17:58:00Z) .. datetime(2026-06-19T18:30:00Z))
| where AppRoleName == "indexer-infra"
| where Message has op
    or Message has "batch-4-rollup"
    or Message has "IndexerPassComplete"
| where Message has "pass='4'" or Message has "pass-4" or Message has op
| project TimeGenerated, Message
| order by TimeGenerated asc
```

**Healthy pass 4 at 18:00 UTC:** `IndexerPassStart` with `skip-youtube='False'`, `IndexerPassComplete` with `success='True'`, `batch-4-rollup` with `skip-youtube='False'`, `youtube-error='False'`.

---

## 3. Batch 4 / Jakub

### A. Batch 4 membership (podcast in pool)

```kusto
let podcastId = "8a0c0f4e-79e0-4d87-bcd5-2156fc0d2f9e";
AppTraces
| where TimeGenerated > ago(7d)
| where AppRoleName == "indexer-infra"
| where Message has "Batch 4:" and Message has podcastId
| project TimeGenerated, Message
| order by TimeGenerated desc
| take 5
```

### B. Batch 4 rollup (new Warning log)

```kusto
AppTraces
| where TimeGenerated > ago(48h)
| where AppRoleName == "indexer-infra"
| where Message has "HourlyOrchestration batch-4-rollup"
| extend hourUtc = toint(extract(@"hour-utc='(\d+)'", 1, Message))
| extend success = extract(@"success='(True|False)'", 1, Message)
| extend skipYouTube = extract(@"skip-youtube='(True|False)'", 1, Message)
| extend youtubeError = extract(@"youtube-error='(True|False)'", 1, Message)
| extend operationId = extract(@"operation-id='([^']+)'", 1, Message)
| project TimeGenerated, hourUtc, success, skipYouTube, youtubeError, operationId, Message
| order by TimeGenerated desc
```

### C. Jakub — broad pass-4 window search

Czech characters can break `has` filters; use podcast ID and partial name:

```kusto
AppTraces
| where TimeGenerated between (datetime(2026-06-19T18:00:00Z) .. datetime(2026-06-19T18:02:00Z))
| where AppRoleName == "indexer-infra"
| where Message has "8a0c0f4e"
    or Message has "Jakub"
    or Message has "Batch 4"
    or Message has "pass 4"
    or Message has "youTubeRefreshed"
    or Message has "skip-youtube"
    or Message has "BuildIndexerKeyRing"
    or Message has "Rotate indexer"
    or Message has "RunAsync Completed. Pass: 4"
| project TimeGenerated, OperationId, Message
| order by TimeGenerated asc
```

### D. `YouTubeDiscoveryPath` (YouTube-authority podcasts)

Warning level when `DependsOnYouTubeForEpisodeDiscovery()` is true:

```kusto
AppTraces
| where TimeGenerated > ago(48h)
| where AppRoleName == "indexer-infra"
| where Message has "YouTubeDiscoveryPath"
| extend podcastId = extract(@"podcast-id='([^']+)'", 1, Message)
| extend path = extract(@"path='([^']+)'", 1, Message)
| extend skipYouTube = extract(@"skip-youtube='(True|False)'", 1, Message)
| project TimeGenerated, podcastId, path, skipYouTube, Message
| order by TimeGenerated desc
```

For Jakub (non-authority), same string may appear at **Info** — query without `SeverityLevel` filter or use podcast ID:

```kusto
AppTraces
| where TimeGenerated > ago(48h)
| where Message has "YouTubeDiscoveryPath"
| where Message has "8a0c0f4e"
| project TimeGenerated, Message
| order by TimeGenerated desc
```

### E. `YouTubeAuthorityPodcastAudit` (authority pool only)

```kusto
AppTraces
| where TimeGenerated > ago(7d)
| where Message has "YouTubeAuthorityPodcastAudit"
| where Message has "8a0c0f4e-79e0-4d87-bcd5-2156fc0d2f9e"
| project TimeGenerated, Message
| order by TimeGenerated desc
```

```kusto
AppTraces
| where TimeGenerated > ago(24h)
| where Message has "YouTubeAuthorityIndexingAudit"
| project TimeGenerated, Message
| order by TimeGenerated asc
```

### F. Indexer pass 4 context (legacy Information log)

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

---

## 4. Key rotation — `BuildIndexerKeyRing`, `Rotate indexer api-key`

Indexer YouTube calls use a **key ring** persisted in Cosmos (`YouTubeIndexerKeyState`). Rotation happens on quota exhaustion.

### A. Key ring build + rotation in a time window

```kusto
AppTraces
| where TimeGenerated between (datetime(2026-06-19T17:50:00Z) .. datetime(2026-06-19T18:30:00Z))
| where AppRoleName == "indexer-infra"
| where Message has "BuildIndexerKeyRing"
    or Message has "Rotate indexer api-key"
    or Message has "Obtained api-key"
    or Message has "Persisted YouTube indexer key state"
    or Message has "Resuming YouTube indexer key ring"
    or Message has "quota exhausted"
    or Message has "Exceeded Quota"
| project TimeGenerated, Message
| order by TimeGenerated asc
```

### B. Scoped to indexer pass `operation_Id`

```kusto
let op = "5289449922f7727fbc0d998274a780a9";
AppTraces
| where OperationId == op
| where Message has "BuildIndexerKeyRing"
    or Message has "Rotate indexer"
    or Message has "SkipYouTubeUrlResolving"
    or Message has "quota"
| project TimeGenerated, Message
| order by TimeGenerated asc
```

### C. YouTube flags during 18:00 window

```kusto
AppTraces
| where TimeGenerated between (datetime(2026-06-19T17:58:00Z) .. datetime(2026-06-19T18:15:00Z))
| where AppRoleName == "indexer-infra"
| where Message has "BuildIndexerKeyRing"
    or Message has "Rotate indexer api-key"
    or Message has "TolerantYouTube"
    or Message has "skip-youtube"
    or Message has "bypass-youtube"
    or Message has "youTubeRefreshed"
    or Message has "SkipYouTubeUrlResolving"
    or Message has "quota exhausted"
| project TimeGenerated, Message
| order by TimeGenerated asc
```

**Red flag:** `Rotate indexer api-key` repeated until ring exhausted, then `SkipYouTubeUrlResolving` / `youtube-error='True'` on batch rollup.

---

## 5. Timer reliability — `RunHourly`, `HourlyCatchUp`, cold starts

| Function | Schedule | Role |
|----------|----------|------|
| `Hourly` (`RunHourly`) | `0 */1 * * *` (top of each UTC hour) | Start `HourlyOrchestration` if none active |
| `HourlyCatchUp` (`RunHourlyCatchUp`) | `0 5 * * * *` (minute 5 each hour) | Schedule missed hour if no instance for current hour |
| `HalfHourly` | `30 */1 * * *` | Separate half-hourly orchestration |

### A. RunHourly timeline (today)

```kusto
traces
| where timestamp > datetime(2026-06-19T00:00:00Z)
| where cloud_RoleName == "indexer-infra"
| where message has "RunHourly"
| project timestamp, message
| order by timestamp asc
```

### B. Skipped / catch-up / schedule failures

```kusto
AppTraces
| where TimeGenerated > ago(48h)
| where AppRoleName == "indexer-infra"
| where Message has "RunHourly skipped"
    or Message has "RunHourlyCatchUp scheduling missed"
    or Message has "RunHourlyCatchUp skipped"
    or Message has "Failure to execute" and Message has "HourlyOrchestration"
| project TimeGenerated, Message
| order by TimeGenerated desc
```

### C. Cold start / host lock (18:00 miss pattern)

Long gap between `RunHourly initiated` and `HourlyOrchestration.RunAsync initiated` often indicates **cold start** or **host lock lease** delay:

```kusto
AppTraces
| where TimeGenerated between (datetime(2026-06-19T17:58:00Z) .. datetime(2026-06-19T18:05:00Z))
| where AppRoleName == "indexer-infra"
| where Message has "Host started"
    or Message has "Initializing"
    or Message has "Loading functions metadata"
    or Message has "Host lock lease acquired"
    or Message has "RunHourly"
    or Message has "HourlyOrchestration"
| project TimeGenerated, Message
| order by TimeGenerated asc
```

### D. Full 18:00 timeline (host + orchestration + batch 4)

```kusto
AppTraces
| where TimeGenerated between (datetime(2026-06-19T17:50:00Z) .. datetime(2026-06-19T18:30:00Z))
| where AppRoleName == "indexer-infra"
| where Message has "RunHourly"
    or Message has "HourlyOrchestration"
    or Message has "Selected indexer passes"
    or Message has "Host started"
    or Message has "Host lock lease"
    or Message has "BuildIndexerKeyRing"
    or Message has "Rotate indexer"
    or Message has "Indexer pass"
    or Message has "Batch 4:"
    or Message has "8a0c0f4e"
    or Message has "Jakub Jahl"
    or Message has "SkipYouTube"
    or Message has "quota"
    or Message has "bypass-youtube"
    or Message has "batch-4-rollup"
| project TimeGenerated, Message
| order by TimeGenerated asc
```

---

## 6. Quota report — Cosmos + flush logs at 06:55 UTC

`YouTubeQuotaReport` timer runs **06:55 UTC daily** (`0 55 6 * * *`). Flushes prior Pacific quota day to Cosmos **`LookUps`** container (`type = 'YouTubeQuotaReport'`).

### A. Flush confirmation in logs

```kusto
AppTraces
| where TimeGenerated > ago(7d)
| where AppRoleName == "indexer-infra"
| where Message has "Flushing YouTube quota usage report"
    or Message has "Saved YouTube quota report"
| project TimeGenerated, Message
| order by TimeGenerated desc
```

The `Saved YouTube quota report` line includes report-level counters: `podcastsNotIndexedDueToQuota`, `podcastsNotEnrichedDueToQuota`, `ringExhaustionCount`, `nonQuotaErrorCount` (see section 6D for Cosmos field definitions).

### B. Quota / bypass signals near YouTube hours

```kusto
traces
| where timestamp > ago(72h)
| where cloud_RoleName == "indexer-infra"
| where message has "SkipYouTubeUrlResolving"
    or message has "YouTubeQuota"
    or message has "quota exhausted"
    or message has "Exceeded Quota"
| project timestamp, message
| order by timestamp desc
| take 50
```

### C. 06:00 UTC pass quota context

```kusto
traces
| where timestamp between (datetime(2026-06-19T06:00:00Z) .. datetime(2026-06-19T06:30:00Z))
| where cloud_RoleName == "indexer-infra"
| where message has "SkipYouTubeUrlResolving"
    or message has "quota"
    or message has "YouTubeQuota"
    or message has "bypass-youtube: 'False'"
| project timestamp, message
| order by timestamp asc
```

### D. Cosmos — daily quota report document

**Cosmos targets:** account `cultpodcasts-db`, RG `AutomatedData`, database `cultpodcasts-db`, container **`LookUps`**, document `type = 'YouTubeQuotaReport'`.

Azure CLI has **no** `az cosmosdb sql query` (control plane only). Use **Cosmos DB Shell** or [`scripts/query-cosmos-lookups.ps1`](../scripts/query-cosmos-lookups.ps1).

**Cosmos DB Shell** (after `dotnet tool install --global CosmosDBShell --prerelease` and `az login`):

```powershell
$yesterday = (Get-Date).ToUniversalTime().AddDays(-1).ToString("yyyy-MM-dd")

cosmosdbshell --connect https://cultpodcasts-db.documents.azure.com:443/ `
  --connect-subscription a6b8f1a2-6163-41bc-aa6d-e33928939a6e `
  --connect-resource-group AutomatedData `
  -c "cd cultpodcasts-db/LookUps; query `"SELECT TOP 1 c.id, c.reportDate, c.sourceApplication, c.podcastsNotIndexedDueToQuota, c.podcastsNotEnrichedDueToQuota, c.ringExhaustionCount, c.nonQuotaErrorCount, c.keys FROM c WHERE c.type = 'YouTubeQuotaReport' AND c.reportDate = '$yesterday' AND c.sourceApplication = 'Indexer' ORDER BY c._ts DESC`""
```

**Script:**

```powershell
.\scripts\query-cosmos-lookups.ps1 -Query QuotaReport -ReportDate $yesterday
```

**Report-level counters** (Pacific quota day rollup):

| Field | Meaning |
|-------|---------|
| `podcastsNotIndexedDueToQuota` | Podcasts skipped for indexing when the key ring was exhausted |
| `podcastsNotEnrichedDueToQuota` | Podcasts indexed but YouTube enrichment skipped due to quota |
| `ringExhaustionCount` | Times the indexer key ring was fully exhausted |
| `nonQuotaErrorCount` | YouTube API failures not attributed to quota (auth, 5xx, etc.) |

**Per-key stats** (`keys[]`, `usedIndexerKeys[]`, `unusedIndexerKeys[]`):

| Field | Meaning |
|-------|---------|
| `quotaHits` | API responses indicating quota exhaustion |
| `quotaUsed` | Quota units charged by Google (when reported) |
| `estimatedQuotaUsed` | Estimated units consumed from operation counts × per-call costs |
| `quotaConsumedByOperation` | Breakdown: `searchList`, `channelsList`, `playlistItemsList`, `playlistsList`, `videosList` |
| `dailyLimit` | Configured daily limit for the key (typically 10,000) |
| `capacityHint` | `quota-exhausted` vs `spare-capacity-candidate` |

Compare `estimatedQuotaUsed` to `dailyLimit` for headroom. High `ringExhaustionCount` or `podcastsNotIndexedDueToQuota` on a report day correlates with batch 3–4 `skip-youtube='True'` / `youtube-error='True'` rollups (sections 4, 8).

### E. Cosmos — indexer key ring state

**Cosmos DB Shell:**

```powershell
cosmosdbshell --connect https://cultpodcasts-db.documents.azure.com:443/ `
  --connect-subscription a6b8f1a2-6163-41bc-aa6d-e33928939a6e `
  --connect-resource-group AutomatedData `
  -c "cd cultpodcasts-db/LookUps; query `"SELECT TOP 1 * FROM c WHERE c.type = 'YouTubeIndexerKeyState' ORDER BY c._ts DESC`""
```

**Script:**

```powershell
.\scripts\query-cosmos-lookups.ps1 -Query IndexerKeyState
```

---

## 7. Tomorrow morning checklist — 06:00 UTC validation (Jun 20, 2026)

Run after **~06:10 UTC** (allow orchestration + batch 4 to finish). Replace date literals with the validation day if needed.

### Step 1 — Timer fired

```kusto
AppTraces
| where TimeGenerated between (datetime(2026-06-20T05:58:00Z) .. datetime(2026-06-20T06:10:00Z))
| where AppRoleName == "indexer-infra"
| where Message has "RunHourly initiated" or Message has "hourly-scheduled"
| project TimeGenerated, Message
| order by TimeGenerated asc
```

- [ ] `RunHourly initiated hour-utc='6'`
- [ ] `hourly-scheduled trigger='RunHourly' hour-utc='6'`
- [ ] No `RunHourly skipped` (unless prior hour still running — then check `HourlyCatchUp` at :05)

### Step 2 — Passes 3–4 + YouTube enabled

```kusto
AppTraces
| where TimeGenerated between (datetime(2026-06-20T05:58:00Z) .. datetime(2026-06-20T06:45:00Z))
| where AppRoleName == "indexer-infra"
| where Message has "pass-selection" or Message has "batch-4-rollup"
| project TimeGenerated, Message
| order by TimeGenerated asc
```

- [ ] `first-pass='3'`, `last-pass='4'`, `youtube-enabled-hour='True'`
- [ ] `batch-4-rollup` with `skip-youtube='False'`, `success='True'`, `youtube-error='False'`
- [ ] Note `operation-id` from `batch-4-rollup` for deep-dive if failed

### Step 3 — Jakub indexed

```kusto
AppTraces
| where TimeGenerated between (datetime(2026-06-20T05:58:00Z) .. datetime(2026-06-20T06:45:00Z))
| where AppRoleName == "indexer-infra"
| where Message has "8a0c0f4e" or Message has "Jakub Jahl"
| project TimeGenerated, OperationId, Message
| order by TimeGenerated asc
```

- [ ] `Podcast: 'Jakub Jahl` / `IndexPodcastResult` with added or enriched episodes
- [ ] `YouTubeDiscoveryPath` for podcast-id (Info or Warning)

### Step 4 — Cosmos ground truth

Use Cosmos DB Shell or the repo script (not `az cosmosdb sql query` — see section 6D).

```powershell
.\scripts\query-cosmos-lookups.ps1 -Query Podcast
.\scripts\query-cosmos-lookups.ps1 -Query Episodes
```

Or **Cosmos DB Shell** interactively:

```
CS> cd cultpodcasts-db/Podcasts
CS cultpodcasts-db/Podcasts> query "SELECT c.id, c.name, c.lastIndexed, c.latestReleased FROM c WHERE c.id = '8a0c0f4e-79e0-4d87-bcd5-2156fc0d2f9e'"

CS> cd cultpodcasts-db/Episodes
CS cultpodcasts-db/Episodes> query "SELECT c.id, c.title, c.release, c.urls FROM c WHERE c.podcastId = '8a0c0f4e-79e0-4d87-bcd5-2156fc0d2f9e' AND (CONTAINS(c.urls.youTube, 'hh4MIFHUzRM') OR CONTAINS(c.urls.youTube, 'wuSWvcS2Yfo')) ORDER BY c.release DESC"
```

- [ ] `lastIndexed` within ~15 min of 06:00 UTC run
- [ ] Target episodes have `urls.youTube` populated

### Step 5 — If failed, branch quickly

| Symptom | Next query / action |
|---------|---------------------|
| No `RunHourly` at 06:00 | Section 5C cold start; check Function App health / deployment |
| `RunHourly skipped` | Prior orchestration still running — find stuck instance |
| `skip-youtube='True'` at 06:00 | Section 4 key rotation / quota exhaustion |
| Batch 4 ran, no Jakub logs | Section 3A batch membership |
| Jakub logs, no YouTube URLs | Section 2 on pass-4 `operation_Id`; verify Fix 2 deployed |
| `youtube-error='True'` on rollup | Section 4 + 6 quota report |

### Step 6 — 06:55 quota flush (same morning)

After 06:55 UTC, confirm prior day report saved (Section 6A) before concluding quota exhaustion.

---

## 8. Key ring exhaustion — day-by-day comparison

Indexer YouTube uses a **Cosmos-persisted key ring** (`YouTubeIndexerKeyState`) that **resumes within the same Pacific quota day**. Exhaustion at **12:00 UTC** (passes 1–2) leaves **18:00 UTC** (passes 3–4) with no keys. `Rotate indexer api-key` is **Information-level** and usually absent in production; use **`AppExceptions`** with `ring exhausted` and **`YouTubeAuthorityIndexingAudit`** instead.

**YouTube windows:** hours **0 / 6 / 12 / 18 UTC** (`hour % 6 == 0`). Passes **1–2** at 0/12; passes **3–4** at 6/18.

### A. Ring exhaustion heatmap (14d)

```kusto
AppExceptions
| where TimeGenerated > ago(14d)
| where AppRoleName == "indexer-infra"
| where OuterMessage has "ring exhausted"
| extend day = startofday(TimeGenerated)
| extend hourUtc = hourofday(TimeGenerated)
| summarize exceptions = count() by day, hourUtc
| order by day desc, hourUtc asc
```

**Observed Jun 2026:** exhaustion only at **Jun 20 18:00** (135) and **Jun 24 12:00** (147) + **18:00** (136). All other days/hours in 14d: **zero** `ring exhausted` exceptions.

### B. YouTube-window pass outcomes (Warning rollups)

```kusto
AppTraces
| where TimeGenerated > ago(14d)
| where AppRoleName == "indexer-infra"
| where Message has "HourlyOrchestration indexer-pass-complete"
| extend hourUtc = toint(extract(@"hour-utc='(\d+)'", 1, Message))
| extend pass = toint(extract(@"pass='(\d+)'", 1, Message))
| extend skipYouTube = extract(@"skip-youtube='(True|False)'", 1, Message)
| extend youtubeError = extract(@"youtube-error='(True|False)'", 1, Message)
| extend podcastCount = extract(@"podcast-count='(\d+)'", 1, Message)
| where hourUtc in (0, 6, 12, 18)
| extend day = startofday(TimeGenerated)
| project day, hourUtc, pass, skipYouTube, youtubeError, podcastCount, TimeGenerated
| order by day desc, hourUtc asc, pass asc
```

### C. Authority bypass vs ring exhaustion (same window)

```kusto
AppTraces
| where TimeGenerated > ago(14d)
| where AppRoleName == "indexer-infra"
| where Message has "YouTubeAuthorityIndexingAudit"
| extend hourUtc = hourofday(TimeGenerated)
| extend day = startofday(TimeGenerated)
| extend inBatch = toint(extract(@"in-batch='(\d+)'", 1, Message))
| extend bypassed = toint(extract(@"youtube-bypassed='(\d+)'", 1, Message))
| where hourUtc in (0, 6, 12, 18)
| summarize totalInBatch = sum(inBatch), totalBypassed = sum(bypassed) by day, hourUtc
| order by day desc, hourUtc asc
```

**Healthy:** `totalBypassed` ≪ `totalInBatch` (e.g. Jun 25 06:00 — 3/86). **Exhausted:** `totalBypassed == totalInBatch` (e.g. Jun 24 12:00 — 95/95; Jun 24 18:00 — 86/86).

### D. Single evening window deep-dive (example: Jun 24 18:00 UTC)

```kusto
AppTraces
| where TimeGenerated between (datetime(2026-06-24T17:55:00Z) .. datetime(2026-06-24T18:30:00Z))
| where AppRoleName == "indexer-infra"
| where Message has "pass-selection"
    or Message has "indexer-operation-ids"
    or Message has "indexer-pass-complete"
    or Message has "batch-4-rollup"
    or Message has "YouTubeAuthorityIndexingAudit"
| project TimeGenerated, OperationId, Message
| order by TimeGenerated asc
```

Jun 24 18:00 operation IDs: orchestration `8febe8aa2e19fdc545db0367df7eb3e6`; pass 3 `73d5717d-9ef1-5449-a7f6-6d2b218128e7`; pass 4 `4ed40d0f-ded2-5535-ac06-382ffa643ae6`.

### E. Compare exhaustion day vs non-exhaustion day (exceptions)

```kusto
let exhaustedDay = datetime(2026-06-24);
let healthyDay = datetime(2026-06-17);
AppExceptions
| where AppRoleName == "indexer-infra"
| where startofday(TimeGenerated) in (exhaustedDay, healthyDay)
| where hourofday(TimeGenerated) in (6, 18)
| extend day = startofday(TimeGenerated)
| extend hourUtc = hourofday(TimeGenerated)
| summarize
    ringExhausted = countif(OuterMessage has "ring exhausted"),
    quotaForbidden = countif(OuterMessage has "Exceeded Quota" or OuterMessage has "exceeded your")
  by day, hourUtc
| order by day desc, hourUtc asc
```

### F. Pre-window depletion check (same Pacific day)

Before blaming 18:00 alone, check whether **12:00** already exhausted the ring:

```kusto
AppExceptions
| where AppRoleName == "indexer-infra"
| where OuterMessage has "ring exhausted"
| where startofday(TimeGenerated) == datetime(2026-06-24)
| summarize count() by hourofday(TimeGenerated)
| order by hourofday_TimeGenerated asc
```

Jun 24: **147 @ 12:00**, then **136 @ 18:00** — evening inherited a dead ring from the noon window.

### G. az CLI one-liner (ring exhaustion heatmap)

```powershell
az monitor log-analytics query -w 2b1c62ee-689f-422a-816b-be1605ae88fa -t P14D --analytics-query "AppExceptions | where AppRoleName == 'indexer-infra' | where OuterMessage has 'ring exhausted' | extend day = startofday(TimeGenerated), hourUtc = hourofday(TimeGenerated) | summarize count() by day, hourUtc | order by day desc" -o json
```

**Note:** use **single-line** `--analytics-query` with `-t P14D`; multi-line heredocs can return unfiltered workspace noise.

---

## PowerShell helper — run a query file

```powershell
$workspaceId = "2b1c62ee-689f-422a-816b-be1605ae88fa"
$query = Get-Content -Raw ".tmp-q18-batch4.kql"   # or paste from this doc
az monitor log-analytics query -w $workspaceId --analytics-query $query -o json | Out-File .tmp-query-out.json
```

---

*Created Jun 2026 for indexing investigation handoff.*
