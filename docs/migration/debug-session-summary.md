# Debug Session Summary

## Cosmos LINQ projection failure in homepage publishing

- Exception observed: `Microsoft.Azure.Cosmos.Linq.DocumentQueryException: Constructor invocation is not supported`.
- Failing location: `EpisodeRepository.GetAllBy<TProjection>` during `.ToFeedIterator()` for projected queries.
- Root cause: constructor-based projection expressions (record primary constructors) cannot be translated by Cosmos LINQ provider.
- Fix applied: kept repository server-side `.Select(projection)` path and changed call-site projections to anonymous-type initializers (no constructor arguments in expression tree).
- Validation evidence: workspace build completed successfully after changes.

## Follow-up checks

- Execute homepage publish path to confirm runtime behavior and RU profile are stable with server-side projections.
- Keep future Cosmos query projections constructor-free in expression trees.

## April 2026 cost spike investigation

- Source analyzed: `C:\Users\jonbr\Downloads\cost-analysis.csv`.
- Daily totals increased from `~$0.07` (2026-04-15) to `~$0.24` (2026-04-16) and `~$0.30` (2026-04-17).
- Increase is concentrated in `automatedinfra` (`~$0.01` to `~$0.19–$0.24`), while `automateddata` stayed around `~$0.05–$0.06`.
- Mitigation implemented: added active-instance guards to `Indexer` and `Discovery` timer triggers to prevent duplicate orchestration scheduling during overlap.
- Correction applied: removed invalid sub-40 `maxInstanceCount` overrides from `Infrastructure/functions.bicep`; Flex Consumption scale settings now honor a minimum floor of `40` in `Infrastructure/function.bicep`.
- Pending validation: deploy changes and compare 48h post-deploy `automatedinfra` daily run-rate.

## Live drift validation (Cultpodcasts subscription) — 2026-04-18

- Subscription context used: `Cultpodcasts` (`a6b8f1a2-6163-41bc-aa6d-e33928939a6e`).
- Initial mismatch explained: workspace queries against `loganalytics-infra` used default lookback behavior and appeared to show only recent rows. Re-ran against the exact Application Insights resource (`ai-infra`) with explicit `hours` lookback and obtained full pre-spike and spike windows.
- Pre-spike window analyzed: `2026-04-09..2026-04-15`.
- Spike window analyzed: `2026-04-16..2026-04-18`.

### Concurrent execution (pre vs spike)

- **Pre**: `MaxConcurrent=10`, `AvgConcurrent=4.009`, `MinutesWithOverlap=739`, `TotalActiveMinutes=894`.
- **Spike**: `MaxConcurrent=10`, `AvgConcurrent=4.128`, `MinutesWithOverlap=231`, `TotalActiveMinutes=296`.
- Overlap remains normal for this durable-orchestration design (orchestrator + activities), with a slight increase in average concurrent active executions during spike.

### Duration comparison (pre vs spike)

- `orchestration:HourlyOrchestration`: `63.0s` → `62.6s` (flat/slightly down)
- `activity:Indexer`: `16.53s` → `17.02s` (slight increase)
- `orchestration:HalfHourlyOrchestration`: `14.76s` → `13.45s` (down)
- `activity:Poster`: `2.97s` → `2.71s` (down)
- `activity:Publisher`: `7.67s` → `7.21s` (down)
- `activity:Tweet`: `13.00s` → `12.91s` (flat)
- `activity:Bluesky`: `2.08s` → `1.80s` (down)
- `activity:Categoriser`: `4.23s` → `3.45s` (down)
- Discovery path dropped materially:
  - `orchestration:Orchestration`: `139.08s` → `48.47s`
  - `activity:Discover`: `138.74s` → `48.14s`

### Cost-per-function proxy summary

- Normalized execution-time/day proxy is down across all measured indexer/discovery functions during spike window.
- Highest absolute contributors remain `orchestration:HourlyOrchestration` and `activity:Indexer`, but neither shows spike-period expansion vs pre-window.
- Conclusion: elevated costs are not explained by a single function-duration regression in request telemetry.

### Interpretation

- We do observe concurrent function execution, but that pattern exists both before and during spike.

## April 21st deployment (feature/costs-increase)

Deployed commit `d3fb7933` (2026-04-18) containing the mitigations identified during the spike investigation:

- **Orchestration guards**: Added active-instance checks to `Cloud/Indexer/OrchestrationTrigger.cs` and `Cloud/Discovery/DiscoveryTrigger.cs` to prevent duplicate orchestration scheduling when a prior instance is still running.
- **Bicep `maxInstanceCount` fix**: Removed invalid sub-40 overrides in `Infrastructure/function.bicep`; Flex Consumption plan enforces a minimum of `40`.
- **`CategorisePodcastEpisodesProcessor`**: Now saves only changed episodes, reducing unnecessary Cosmos writes.

These changes directly target the signals observed in the spike analysis:
- Concurrent orchestration overlap was present in both pre and spike windows, but the guards eliminate the risk of scheduling a new run before the prior one completes, which could inflate GB-s billing.
- The Bicep fix ensures scale-out is not incorrectly capped, which could have caused queuing/retry pressure contributing to elevated execution time.

### Post-deployment validation status
- Pending: compare `automatedinfra` and `automateddata` daily run-rate for the 48–72h window following 2026-04-21 against the spike-period baseline (`$0.19–$0.24/day` in `automatedinfra`).

## Cost & telemetry analysis via az CLI — 2026-04-25

### Full billing picture (March–April, `automatedinfra` Functions, GBP)

Queried via `Microsoft.CostManagement/query` API against subscription `a6b8f1a2-6163-41bc-aa6d-e33928939a6e`:

| Period | Daily cost |
|---|---|
| 2026-03-01 – 2026-03-10 | £0.00/day |
| 2026-03-11 | £0.70 (migration deployment day) |
| 2026-03-12 | £0.95 (peak) |
| 2026-03-13 – 2026-03-31 | £0.17–0.27/day |
| 2026-04-01 – 2026-04-15 | £0.00/day |
| 2026-04-16 – 2026-04-24 | £0.14–£0.18/day |

### Revised interpretation — monthly free-tier exhaustion

- The orchestration (`HourlyOrchestration`, `activity:Indexer`, all activities) has been running consistently since **at least March 1**, with stable execution counts (48 `activity:Indexer` invocations/day, 24 `HourlyOrchestration`/day) throughout March and April.
- **April 1–15 £0.00 is a new-month free-grant reset**, not a genuine cost reduction. The Flex Consumption plan includes a monthly free execution grant (GB-s); March's grant was exhausted on March 11 (migration deployment date), April's grant ran out around April 16.
- The April 16 "spike" is therefore **not a new regression** — it is the same post-migration execution level that billed throughout March, now appearing again after the April free-grant was consumed.
- The March 11–12 peak (£0.70–0.95) represents the initial over-execution on migration deployment day; steady-state since March 13 has been £0.17–0.27/day.

### Effect of April 21 mitigations

- March billing period (2026-03-13 – 2026-03-31, steady state): **~£0.20/day average**.
- April billing period (2026-04-16 – 2026-04-24): **~£0.15/day average**.
- Approximate **~25% reduction** in daily Functions cost, consistent with the orchestration guards and episode-save optimisation reducing unnecessary GB-s.

### Application Insights data range

- Queried via Log Analytics workspace `loganalytics-infra` (customer ID `2b1c62ee-689f-422a-816b-be1605ae88fa`).
- Data range available: `2026-01-25` to present; 515K+ traces.
- Execution volumes are flat across the full window — no anomalous invocation count or duration regression detected on or after April 16.

### Free-grant details and reset schedule

- All three Function apps (`indexer-infra`, `discover-infra`, `api-infra`) run on **Flex Consumption (FC1)** plans, all configured at **2048 MB (2 GB)** instance memory.
- Free grant resets on the **1st of each calendar month** — confirmed by cost data showing £0.00 from 2026-04-01 to 2026-04-15.
- Empirically derived free grant: **~322,337 GB-s/month** (back-calculated from Apr 1–15 execution seconds × 2 GB = 161,169 s × 2 = 322,337 GB-s consumed before billing started Apr 16).

### Instance memory impact on free-grant coverage

Average daily execution across all three apps: **~10,745 seconds/day** (Apr 1–15 mean).

| Instance memory | GB-s/day | Free-grant seconds | Days free/month |
|---|---|---|---|
| 512 MB (0.5 GB) | ~5,372 GB-s | ~644,674 s | **~60 days → entire month free** |
| **2048 MB (2 GB)** ← current | ~21,490 GB-s | ~161,169 s | **~15 days → billing from ~Apr 16** |
| 4096 MB (4 GB) | ~42,980 GB-s | ~80,584 s | ~7.5 days → billing from ~Apr 8 |

**Only `512`, `2048`, and `4096` are valid Flex instance-memory values.** `1024` is not valid per Microsoft documentation.

### Memory feasibility — confirmed via Private Bytes telemetry (Apr 16–24)

Queried `AppPerformanceCounters` (Private Bytes) per instance from `loganalytics-infra`:

| Metric | Value |
|---|---|
| Avg peak per instance | 534.2 MB |
| Median (P50) peak | 534.6 MB |
| P95 peak | 569.2 MB |
| Absolute max | 689.2 MB |

- **512 MB: ruled out.** Median peak (534 MB) already exceeds the 512 MB limit under normal load. More than half of all instances would hit OOM, causing retries and higher cost than staying at 2048 MB.
- **2048 MB: keep as baseline.** It remains the smallest valid memory size that is compatible with observed memory usage.
- **4096 MB: not cost-optimal** for this workload because it would consume free-grant GB-s faster.

### Trial correction (2026-04-25)

- Reverted `Infrastructure/function.bicep` to valid Flex values `[512, 2048, 4096]` and default `2048`.
- Cancelled the earlier 1024 MB trial assumption after doc validation.
- Next optimization path: keep `2048` and reduce execution seconds (especially `activity:Indexer` and `orchestration:HourlyOrchestration`) to lower monthly billed GB-s.

### Alerting and memory instrumentation implementation (2026-04-25)

Implemented in branch:

- **Cost budget alerts (free-allowance proxy)**
  - Added `Microsoft.Consumption/budgets` resource in `Infrastructure/functions.bicep` scoped to subscription.
  - Budget filtered to current deployment resource group + `ServiceName == Functions`.
  - Threshold notifications configured at **50/75/90/100%**.
- **Operational alerts**
  - Added action group + scheduled query alerts in `Infrastructure/functions.bicep` for:
    - OutOfMemory signals (`AppExceptions`/`AppTraces`)
    - Host drain spikes (`AppRequests` with `/admin/host/drain`)
    - Failed executions (`AppRequests` where `Success == false`)
- **Warning-level memory probes (Info-filter safe)**
  - Added shared helper: `Class-Libraries/RedditPodcastPoster.Common/Diagnostics/MemoryProbe.cs`.
  - Added start/end memory probe logging to **all Indexer activities**:
    - `IndexIdProvider`, `Indexer`, `Categoriser`, `Poster`, `Publisher`, `Tweet`, `Bluesky`.
  - Extended probes to **Discovery** (`Discover`, `DiscoveryTrigger`) and **API** (`BaseHttpFunction` request pipeline).
  - Probe payload now includes required fields:
    - `GC.GetTotalMemory(false)`
    - `GC.GetGCMemoryInfo().HeapSizeBytes`
    - `Process.WorkingSet64` / `Process.PrivateMemorySize64`
    - `function-name`, `invocation-id`, `elapsed-ms`.
  - Added deployment toggle in app settings: `memoryProbe__Enabled: 'true'`.

### Immediate validation checks

1. Deploy and verify budget resource and 3 scheduled query alerts exist in `AutomatedInfra` scope.
2. Confirm `MemoryProbe.Start` / `MemoryProbe.Complete` warning events appear for Indexer, Discovery, and API in `AppTraces`.
3. Run 24–72h collection and determine whether API/Discovery peak private-bytes are materially below Indexer before considering any per-app memory-size changes.

### Handover document (2026-04-25)

- See `docs/migration/handover-2026-04-25-memory-probe-and-alerting.md` for a full handover of this session.
- The handover includes:
  - implemented alerting resources,
  - memory probe orchestration refactor,
  - free-grant/reset context,
  - logging configuration and follow-up checks.

### Instrumentation toggle simplification

- Removed legacy `IndexerOptions.EnableCostInstrumentation` toggle.
- Probe telemetry now uses centralized `IMemoryProbeOrchestrator` and only the `memoryProbe__Enabled` configuration switch.

### CostProbe logging reduction (2026-04-25)

- Removed `*CostProbe*` warning logs from Indexer activities (`Indexer`, `IndexIdProvider`, `Categoriser`, `Poster`, `Publisher`, `Tweet`, `Bluesky`).
- Removed stopwatch timing that only existed to support those CostProbe warning payloads.
- Kept memory-usage telemetry through `IMemoryProbeOrchestrator` (`Start(nameof(Class))` + `End()` / `End(false, errorType)`).
- This keeps memory observability while reducing warning-log volume now that free-allowance overage root cause is known.

### Budget deployment split (2026-04-25)

- Removed subscription-scope budget module invocation from `Infrastructure/functions.bicep`.
- Added a separate subscription-scope deployment step in `.github/workflows/deploy.yml` for `Infrastructure/functions-budget-subscription.bicep`.
- `Functions (Deploy Bicep)` now handles resource-group scoped infra only; budget deploy runs independently with explicit subscription scope.
- This separation reduces blast radius and makes budget failures easier to isolate/retry.

### CI publish failure on discover build (run 24935396728)

- Failure source: GitHub Actions run `24935396728`, matrix leg `build (discover, ./Cloud/Discovery, output/discover, discover)`.
- Blocking errors were malformed XML doc-comment parse errors from vendored third-party project files under:
  - `Third-Party/sirkris-Reddit.NET-1.5.3/src/Reddit.NET/...`
- Representative failures included:
  - "Missing equals sign between attribute and attribute value"
  - "Required white space was missing"
  - "Reference to undefined entity 'A.'"
- Remediation applied:
  - Updated `Third-Party/sirkris-Reddit.NET-1.5.3/src/Reddit.NET/Reddit.NET.csproj`
  - Set `GenerateDocumentationFile=false` for default and Debug property groups to prevent XML doc generation from malformed upstream comments.
- Intent: keep vendored third-party source behavior unchanged while unblocking CI publish for Api/Discovery/Indexer matrix legs.

### GitHub Actions run 24935587811 failure (provision / Functions Budget)

- Failed step: `Functions Budget (Deploy Bicep)` in run `24935587811`.
- Exact error from failed logs (`gh run view ... --log-failed`):
  - `Operation failed`
  - `Location is required`
- Root cause:
  - Deployment was moved to `scope: subscription`, and Azure requires a deployment `location` for subscription-scope deployments.
- Fix applied:
  - Updated `.github/workflows/deploy.yml` budget step to include:
    - `location: ${{ env.location }}`
- Expected outcome:
  - Budget deployment step should proceed past validation and deploy `functions-budget-subscription.bicep` successfully with existing RBAC.

### GitHub Actions run 24935928841 failure (budget parameter type)

- Failed run/job: `24935928841` / `73021502158`.
- Error:
  - `InvalidTemplate`
  - `monthlyFunctionsBudgetAmount` expected `Integer` but received `String`.
- Root cause:
  - Workflow passed `monthlyFunctionsBudgetAmount` in the JSON parameters payload as a quoted string.
- Fix applied:
  - Updated `.github/workflows/deploy.yml` budget deploy step to pass `monthlyFunctionsBudgetAmount` as numeric JSON value (unquoted).
- Validation intent:
  - Subscription-scope budget deployment should pass template validation for parameter typing.
