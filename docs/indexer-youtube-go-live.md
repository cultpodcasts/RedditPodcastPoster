# Indexer YouTube go-live checklist

Production checklist for deploying the **indexer** (`indexer-infra`) with the YouTube key-ring, quota reporting, and Reattempt2 keys 15–16. Synthesizes work on branch `cursor/align-apple-spotify-enrichment-youtube-delay`.

**Out of scope here:** `local.settings.json`, dotnet user-secrets, and bicep provision (currently broken) — except where noted as future automation.

**Related docs:**

- [youtube-keys.md](youtube-keys.md) — key vault naming, slot map, literal app settings
- [indexing-app-insights-queries.md](indexing-app-insights-queries.md) — KQL validation at 06:00 / 18:00 UTC
- [interim-deployment.md](interim-deployment.md) — local deploy when GitHub Actions is offline

---

## What this release fixes

| Area | Change (branch commits) |
|------|-------------------------|
| Key ring | `5eee6bbe` — single indexer ring, rotate on quota, Cosmos `YouTubeIndexerKeyState` |
| Tolerant YouTube | `bab1ddc6` — `TolerantYouTubeChannelService` + rotation on quota exhaustion |
| Hourly reliability | `79c7841a` / `7651a0d3` — `HourlyCatchUp`, Jakub/YouTube-first fixes, diagnostic logging |
| Quota persistence | `4ce89093` — flush `YouTubeQuotaUsageState` at indexer pass boundary |
| Quota report | `5eee6bbe` — daily `YouTubeQuotaReport` timer (06:55 UTC) → Cosmos |
| Keys 15–16 | `55003a90` / `6a5dcaaa` / `ff716b7b` — Reattempt2 slots 13/15, lowercase `cultpodcasts` Name |
| Ops docs | `03ad809c` / `47684b1f` — Cosmos query docs, App Insights KQL reference |

**Live state (as of planning):** indexer code was deployed ~**20:03 UTC 19 Jun 2026** and may be **behind HEAD** on this branch. App settings for slots 13/15 may still have **wrong Key Vault reference URIs** from an earlier manual attempt — the running app does **not** resolve KV at runtime.

---

## Architecture (reminder)

```
Pacific quota day
    │
    ▼
Single indexer key ring (slots 1–4, 8–11, 13–16)
    │  hour-window primary → rotate on quota exhausted
    ▼
YouTube enabled when hour % 6 == 0  →  00, 06, 12, 18 UTC
    │
    ├─ Passes 1–2 at 00 / 12 UTC
    └─ Passes 3–4 at 06 / 18 UTC  ← batch 4 YouTube window (Jakub case study)
```

- **Ring exhaustion:** if keys are exhausted at 12:00 UTC, 18:00 UTC may run with `skip-youtube='True'`. Keys 15–16 add Reattempt2 capacity for hour-windows 1 and 3.
- **Runtime config:** app reads **literal** `youtube__Applications__*` strings from app settings only — never Key Vault.

---

## 1. Prerequisites

Before starting, confirm:

- [ ] `az login` and subscription **Cultpodcasts** (`a6b8f1a2-6163-41bc-aa6d-e33928939a6e`)
- [ ] Branch checked out: `cursor/align-apple-spotify-enrichment-youtube-delay` (or merged `main` with same commits)
- [ ] Google Cloud API keys created for Reattempt2 hour-windows **1** and **3** (slots **13** and **15**)
- [ ] Key Vault `cultpodcasts-deployment` (RG `Management`) has secrets (for future bicep; **not** runtime):
  - `Youtube-ApiKey-15` → maps to app slot **13**
  - `Youtube-ApiKey-16` → maps to app slot **15**
- [ ] You have the **literal key strings** ready (from GCP console or `az keyvault secret show` — deploy-time read only)
- [ ] Target Function app: **`indexer-infra`** (RG `AutomatedInfra`)
- [ ] Log Analytics workspace `loganalytics-infra` access for post-deploy validation
- [ ] Optional: Cosmos DB Shell or `scripts/query-cosmos-lookups.ps1` for ground-truth checks

**Do not** set `@Microsoft.KeyVault(...)` reference URIs on YouTube app settings.

---

## 2. Indexer app settings (`indexer-infra`)

Apply **literal** values for the two new Reattempt2 keys and metadata. Slots **13** and **15** only need new ApiKey values; slots 14 and 16 keep existing keys (shared `Youtube-ApiKey-14`).

### Required settings (placeholders)

| App setting | Value |
|-------------|--------|
| `youtube__Applications__13__ApiKey` | `YOUR_KEY_FROM_Youtube-ApiKey-15` |
| `youtube__Applications__13__Name` | `cultpodcasts` |
| `youtube__Applications__13__DisplayName` | `Indexer-HourPrimary-1-Reattempt2-CultPodcasts` |
| `youtube__Applications__13__Reattempt` | `2` |
| `youtube__Applications__15__ApiKey` | `YOUR_KEY_FROM_Youtube-ApiKey-16` |
| `youtube__Applications__15__Name` | `cultpodcasts` |
| `youtube__Applications__15__DisplayName` | `Indexer-HourPrimary-3-Reattempt2-CultPodcasts` |
| `youtube__Applications__15__Reattempt` | `2` |

### Apply via Azure CLI (recommended)

```powershell
az login

$rg = 'AutomatedInfra'
$app = 'indexer-infra'

az functionapp config appsettings set `
  --resource-group $rg `
  --name $app `
  --settings `
    'youtube__Applications__13__ApiKey=YOUR_KEY_FROM_Youtube-ApiKey-15' `
    'youtube__Applications__13__Name=cultpodcasts' `
    'youtube__Applications__13__DisplayName=Indexer-HourPrimary-1-Reattempt2-CultPodcasts' `
    'youtube__Applications__13__Reattempt=2' `
    'youtube__Applications__15__ApiKey=YOUR_KEY_FROM_Youtube-ApiKey-16' `
    'youtube__Applications__15__Name=cultpodcasts' `
    'youtube__Applications__15__DisplayName=Indexer-HourPrimary-3-Reattempt2-CultPodcasts' `
    'youtube__Applications__15__Reattempt=2'
```

### Or via script (also updates DisplayNames on discover/api)

```powershell
.\scripts\apply-youtube-keys.ps1 -ApiKey15 'YOUR_KEY' -ApiKey16 'YOUR_KEY' -ApplyNewKeysOnly
```

### Verify (no secrets in output)

```powershell
az functionapp config appsettings list `
  --resource-group AutomatedInfra `
  --name indexer-infra `
  --query "[?contains(name, 'youtube__Applications__1') && (contains(name, 'ApiKey') || contains(name, 'Name') || contains(name, 'DisplayName'))].{name:name,value:value}" `
  -o table
```

Checklist:

- [ ] Slot 13 `ApiKey` is a **literal** `AIza...` string, **not** `@Microsoft.KeyVault(...)`
- [ ] Slot 15 `ApiKey` is literal
- [ ] Slot 13 and 15 `Name` = `cultpodcasts` (lowercase)
- [ ] App restarted after settings change (automatic with `az functionapp config appsettings set`)

Full slot map: [youtube-keys.md § Slot map](youtube-keys.md#slot-map-quick-reference).

---

## 3. Deploy indexer code

Deploy **code only** from the current branch. Deploy scripts do **not** change app settings.

```powershell
az login
cd C:\Users\jonbr\source\repos\cultpodcasts\RedditPodcastPoster

.\scripts\deploy-indexer.ps1
```

Defaults (`scripts/deploy-indexer.json`): RG `AutomatedInfra`, app `indexer-infra`, storage `cultpodcastsstg`, container `indexer-deployment`, blob `released-package.zip`.

- [ ] Publish + zip completes without error
- [ ] Blob upload to `indexer-deployment/released-package.zip` succeeds
- [ ] Function app restarts and host starts cleanly

See [interim-deployment.md](interim-deployment.md) if prompts ask for first-time JSON overrides.

---

## 4. Verify — 06:00 or 18:00 UTC window

Run validation **~10–45 minutes after** the top of a YouTube hour (**06:00** or **18:00 UTC** preferred for passes 3–4 / batch 4).

**Full KQL playbook:** [indexing-app-insights-queries.md](indexing-app-insights-queries.md)

### Quick validation checklist

| Step | What to confirm | Doc section |
|------|-----------------|-------------|
| Timer fired | `RunHourly initiated hour-utc='6'` or `'18'`; no spurious `RunHourly skipped` | §1, §5 |
| Pass selection | `first-pass='3'`, `last-pass='4'`, `youtube-enabled-hour='True'` | §7 Step 2 |
| Batch 4 rollup | `skip-youtube='False'`, `success='True'`, `youtube-error='False'` | §7 Step 2 |
| Key ring | No sustained `ring exhausted`; rotation only if quota hit | §4, §8 |
| Catch-up | If hourly skipped, `HourlyCatchUp` at :05 scheduled missed hour | §5 |
| Jakub (regression) | Podcast `8a0c0f4e-...` indexed with YouTube URLs on target episodes | §7 Steps 3–4 |

### Example Log Analytics query (workspace `2b1c62ee-689f-422a-816b-be1605ae88fa`)

```powershell
$query = @'
AppTraces
| where TimeGenerated > ago(3h)
| where AppRoleName == "indexer-infra"
| where Message has "pass-selection" or Message has "batch-4-rollup" or Message has "RunHourly"
| project TimeGenerated, Message
| order by TimeGenerated asc
'@

az monitor log-analytics query -w 2b1c62ee-689f-422a-816b-be1605ae88fa --analytics-query $query -o table
```

**Red flags:** `skip-youtube='True'` at a YouTube hour, `youtube-error='True'`, `ring exhausted`, missing `RunHourly` (cold start — see §5C in queries doc).

---

## 5. Quota report — 06:55 UTC

The `YouTubeQuotaReport` timer runs **daily at 06:55 UTC** (`0 55 6 * * *`). It flushes the **prior Pacific quota day** to Cosmos.

| Item | Value |
|------|--------|
| Cosmos account | `cultpodcasts-db` (RG `AutomatedData`) |
| Database / container | `cultpodcasts-db` / **`LookUps`** |
| Document type | `YouTubeQuotaReport` |
| `sourceApplication` | `Indexer` |

### After 06:55 UTC (next morning)

1. **Logs** — confirm flush ([§6A](indexing-app-insights-queries.md#6-quota-report--cosmos--flush-logs-at-0655-utc)):
   - `Flushing YouTube quota usage report`
   - `Saved YouTube quota report`

2. **Cosmos** — query yesterday's report:

```powershell
$yesterday = (Get-Date).ToUniversalTime().AddDays(-1).ToString("yyyy-MM-dd")
.\scripts\query-cosmos-lookups.ps1 -Query QuotaReport -ReportDate $yesterday
```

Review `keys[].quotaHits`, `keys[].quotaUsed`, `keys[].remainingQuota`, `keys[].capacityHint`.

3. **Indexer key state** (optional):

```powershell
.\scripts\query-cosmos-lookups.ps1 -Query IndexerKeyState
```

Also persisted at pass boundary: `YouTubeQuotaUsageState` (`4ce89093`).

---

## 6. Optional — `api-infra` / `discover-infra`

**Not required for indexer YouTube go-live.**

| App | Action | When |
|-----|--------|------|
| `discover-infra` | DisplayName-only sync if telemetry labels matter | `.\scripts\apply-youtube-keys.ps1 -DisplayNamesOnly` |
| `api-infra` | Same DisplayName sync | same script |
| Either | Code deploy | `.\scripts\deploy-discover.ps1` / `deploy-api.ps1` only if unrelated fixes needed |

Discover uses YouTube keys slots 5–6; Api uses slot 12. Neither uses the new Reattempt2 keys 13/15.

---

## 7. Order of operations

Numbered sequence for a safe go-live:

1. **Prerequisites** — login, keys in KV (for records), literal key strings in hand, branch checked out.
2. **App settings first** — apply literal slots 13/15 on `indexer-infra` (§2). Confirm no KV reference URIs.
3. **Deploy indexer code** — `.\scripts\deploy-indexer.ps1` from current branch (§3).
4. **Smoke** — confirm host started in App Insights; optional non-YouTube hour `RunHourly` log.
5. **Wait for YouTube window** — next **06:00** or **18:00 UTC**.
6. **Validate indexing** — §4 checklist + [indexing-app-insights-queries.md §7](indexing-app-insights-queries.md#7-tomorrow-morning-checklist--0600-utc-validation-jun-20-2026).
7. **Next 06:55 UTC** — confirm `YouTubeQuotaReport` in Cosmos (§5).
8. **Optional** — DisplayName sync on discover/api (§6).

**Settings before code** is recommended so the first YouTube pass after deploy sees the new keys. If you must deploy code first, apply settings immediately and before the next `hour % 6 == 0` window.

---

## 8. Rollback

| Layer | Rollback |
|-------|----------|
| **Code** | Redeploy previous package: check out prior commit/tag, run `.\scripts\deploy-indexer.ps1`, or restore earlier `released-package.zip` from storage if retained. |
| **App settings** | Restore previous slot 13/15 ApiKey values (or remove Reattempt2 keys and revert `Name` to prior values) via Portal or `az functionapp config appsettings set`. App restarts automatically. |
| **Cosmos state** | `YouTubeIndexerKeyState` / `YouTubeQuotaUsageState` are forward-compatible; no rollback required unless debugging — delete state doc only if you intend a clean ring reset (rare). |
| **Key ring behavior** | Old code without tolerant rotation may exhaust ring faster; rolling back code without rolling back keys 15–16 is usually safe. |

Document what was live before rollback (commit hash, settings snapshot) for repeatability.

---

## Future — when bicep provision works

1. Ensure KV has `Youtube-ApiKey-15` and `Youtube-ApiKey-16`.
2. Run `functions.bicep` provision (CI `deploy.yml` or manual `az deployment`).
3. Bicep writes **literal** keys from KV at deploy time — no further manual app settings for YouTube.
4. See [youtube-keys.md § Step 4](youtube-keys.md#step-4--when-bicep-deploys-work-again).

---

## Appendix — commit reference

```
ff716b7b  Clarify literal app settings, not runtime KV
6a5dcaaa  Lowercase cultpodcasts Name for KV keys 15-16
55003a90  Wire YouTube API keys 15 and 16 (Reattempt2)
03ad809c  Fix Cosmos query docs
4ce89093  Persist YouTube quota at indexer pass boundary
7651a0d3  Restore indexer diagnostic logging
79c7841a  HourlyCatchUp + Jakub/YouTube-first fixes
bab1ddc6  TolerantYouTube + rotation on quota
5eee6bbe  Key ring, quota report, per-podcast isolation
```
