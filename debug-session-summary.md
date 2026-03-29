## 2026-03-27 - Default subject logged but not persisted
- Observed warning: `Applying default-subject ...` for episode `be80d5d5-1fb8-4b78-ba43-4490f37dc958`.
- Root cause in `Class-Libraries/RedditPodcastPoster.Subjects/SubjectEnricher.cs`: the final default-subject fallback branch appends to `additions` and logs the warning but does not mutate `episode.Subjects`.
- Persistence paths save the full episode document; if `episode.Subjects` is unchanged at save time, Cosmos keeps `subjects: []` even though logs indicate default-subject application.
- Fix approach: when final fallback condition applies, update `episode.Subjects` to include default-subject before returning.

## 2026-03-27 - Indexer cost-probe telemetry capture gap
- Queried `loganalytics-infra` (`AppTraces`, `AppEvents`, `FunctionAppLogs`) across the last 72h and found **no** `*CostProbe*` rows for `indexer-infra`.
- `AppTraces` severity mix for `indexer-infra` over the last 24h is dominated by warnings/errors (`SeverityLevel 2: 4756`, `SeverityLevel 3: 143`) with only `3` informational rows, so instrumentation evidence is currently missing from captured telemetry.
- Informational rows present are telemetry-channel warnings (`"TelemetryChannel found a telemetry item without an InstrumentationKey"`), indicating dropped telemetry items are occurring.
- Local cost exports (`C:\Users\jonbr\Downloads\cost-analysis(2).csv`, `cost-analysis(3).csv`) currently end at `2026-03-26`, so the full post-probe day row for `2026-03-27` is not yet available for cost attribution.
- Immediate next action: fix probe ingestion visibility (or emit probes at warning level temporarily), then run a fresh 24h window before making pass-level cost conclusions.

## 2026-03-27 - Cost probe visibility fix for Information-filtered App Insights
- Root cause for missing probe telemetry: all `*CostProbe*` events were emitted with `LogInformation`, but production ingestion excludes Information-level traces.
- Applied fix in Indexer orchestration activities: `IndexIdProvider`, `Indexer`, `Categoriser`, `Poster`, `Publisher`, `Tweet`, and `Bluesky` now emit `*.CostProbe.Start` / `*.CostProbe.Complete` at `LogWarning`.
- Scope intentionally limited to instrumentation events only; no functional behavior changes to posting/tweeting workflows.
- Next validation: deploy this branch, capture a fresh 24h UTC window, then re-run Log Analytics queries for `*CostProbe*` and correlate to updated `cost-analysis(3).csv` export.

## 2026-03-27 - Commands run and findings (Indexer cost-probe investigation)
- `subscription_list` → confirmed default subscription `Cultpodcasts` (`a6b8f1a2-6163-41bc-aa6d-e33928939a6e`).
- `monitor_workspace_list` (subscription `Cultpodcasts`) → discovered workspace `loganalytics-infra`.
- `group_list` → confirmed relevant resource groups (`AutomatedInfra`, `AutomatedData`, `Cultpodcasts`, `Management`).
- `monitor_table_type_list` + `monitor_table_list` for `loganalytics-infra` → confirmed `AppTraces`, `AppEvents`, `FunctionAppLogs` are available query targets.
- KQL query on `AppTraces` (`where TimeGenerated > ago(72h) and Message/Properties contains "CostProbe"`) → returned `0` rows for `indexer-infra`.
- KQL query on `AppEvents` (`Name/Properties contains "CostProbe"`) → returned `0` rows.
- KQL query on `FunctionAppLogs` attempted with `Properties` filter → semantic error (`Properties` column not resolved for this table in current schema/query).
- KQL query on `AppTraces` severity summary (`summarize count() by SeverityLevel`) → `SeverityLevel 0: 3`, `SeverityLevel 2: 4756`, `SeverityLevel 3: 143` for last 24h.
- KQL query for `SeverityLevel == 0` rows → only telemetry-channel warnings such as `TelemetryChannel found a telemetry item without an InstrumentationKey`.
- PowerShell command `Import-Csv C:\Users\jonbr\Downloads\cost-analysis(2).csv | Select-Object -Last 5` → latest local total-cost export ends at `2026-03-26`.
- PowerShell command `Import-Csv C:\Users\jonbr\Downloads\cost-analysis(3).csv | Select-Object -Last 10` → latest local `indexer-infra` export also ends at `2026-03-26`.
- Discovery from command set: missing probe evidence was caused by Information-level filtering; remediation implemented by changing all `*.CostProbe.*` emissions to Warning level in Indexer activities.

## 2026-03-28 - Live 24h Indexer cost analysis capture (agent-run)
- Confirmed Azure context from CLI: subscription `Cultpodcasts` (default), workspace `loganalytics-infra` (`AutomatedInfra`), Function app `indexer-infra` running in `AutomatedInfra`.
- `indexer-infra` app settings check:
  - `indexer__EnableCostInstrumentation=true`
  - `APPLICATIONINSIGHTS_CONNECTION_STRING` is present
  - logging defaults are Warning (`Logging__LogLevel__Default`, `Logging__LogLevel__Indexer`, `AzureFunctionsJobHost__Logging__ApplicationInsights__LogLevel__Default`).
- Probe visibility check (last 24h):
  - `AppTraces` CostProbe rows: `0`
  - `AppEvents` CostProbe rows: `0`
  - `FunctionAppLogs` CostProbe rows: `0`
  - `AppTraces` for `AppRoleName == indexer-infra`: `5235` rows total (`Warning 5121`, `Error 113`, `Info 1`).
- Telemetry ingestion warning still present:
  - `AI: TelemetryChannel found a telemetry item without an InstrumentationKey` observed in `AppTraces` at `2026-03-28T15:30:16Z`.
- Runtime profile (`AppRequests`, last 24h):
  - `activity:Indexer`: `96` calls, avg `18488.72ms`, p95 `31489.30ms`
  - `orchestration:HourlyOrchestration`: `24` calls, avg `91199.21ms`, p95 `140388.67ms`
  - `activity:Publisher`: `48` calls, avg `6686.20ms`
  - `activity:Categoriser`: `24` calls, avg `3036.32ms`
  - `activity:Poster`: `48` calls, avg `2747.66ms`
  - `activity:Bluesky`: `48` calls, avg `2461.61ms`
- Dependency profile (`AppDependencies`, `indexer-infra`, last 24h):
  - `amp-api.podcasts.apple.com`: `3866` calls, avg `196.54ms`
  - `api.spotify.com`: `2935` calls, avg `159.20ms`
  - `cultpodcasts-db-uksouth.documents.azure.com`: `1375` calls, avg `32.66ms`, total `~44.9s`
  - `youtube.googleapis.com`: `582` calls, avg `166.38ms`
- Old DB leak check:
  - No `cultpodcasts-ukdb` dependency rows observed in the last 24h.
- Durable storage transaction check:
  - `cultpodcastsstg` `Transactions` sum over `2026-03-27T00:00:00Z..2026-03-28T00:00:00Z` = `195357` (within previously observed 195k-206k/day band).
- Conclusion from this capture:
  - CostProbe attribution is still blocked by missing probe ingestion despite warning-level configuration and active runtime traffic.
  - Available evidence points to heavy activity/orchestration runtime plus high external dependency volume as current load contributors.
  - No evidence in this window of old Cosmos DB (`cultpodcasts-ukdb`) usage or a new storage-transaction spike.
- Immediate next step:
  - Resolve probe-ingestion gap first (InstrumentationKey warning path), then repeat same 24h capture to unlock per-activity cost attribution from `*.CostProbe.Complete` metrics.

## 2026-03-28 - CostProbe visibility correction (KQL operator issue)
- Re-ran probe queries with `Message contains "CostProbe"` (instead of `has`) for `AppTraces` and immediately observed probe rows for `indexer-infra`.
- Key finding: prior zero-row conclusions were false negatives caused by KQL term matching behavior with dotted probe strings (for example `IndexerCostProbe.Complete ...`) when using `has`.
- Probe coverage now confirmed across the orchestration window (`>24h` query):
  - `IndexIdProviderCostProbe.Complete`: `24`
  - `IndexerCostProbe.Complete`: `96`
  - `CategoriserCostProbe.Complete`: `24`
  - `PosterCostProbe.Complete`: `47`
  - `PublisherCostProbe.Complete`: `47`
  - `TweetCostProbe.Complete`: `23`
  - `BlueskyCostProbe.Complete`: `45`
- Hourly probe-row shape aligns with expected schedule: mostly `~26` probe rows/hour (hourly + half-hourly activities), indicating instrumentation is broadly active.
- Sample complete rows confirmed include structured timings (for example):
  - `IndexerCostProbe.Complete ... initiate-ms='27' update-ms='22037' complete-ms='22' total-ms='22088'`
  - `PublisherCostProbe.Complete ... search-indexer-ms='112' homepage-publish-ms='5989' total-ms='6102'`
- Revised interpretation:
  - Probe ingestion is working; attribution should proceed using `contains`-based queries and parsed `total-ms`/`update-ms` values.
  - TelemetryChannel InstrumentationKey warnings still appear occasionally, but they are not preventing probe visibility.

## 2026-03-28 - Probe-based attribution summary (24h)
- Parsed `AppTraces` rows where `Message contains "CostProbe.Complete"` for `indexer-infra` and extracted `total-ms` from probe message payloads.
- Activity total-time share from probe `total-ms` (24h):
  - `IndexerCostProbe`: `96` runs, `1,772,536 ms` (`74.05%`)
  - `PublisherCostProbe`: `47` runs, `313,421 ms` (`13.09%`)
  - `PosterCostProbe`: `47` runs, `102,816 ms` (`4.29%`)
  - `BlueskyCostProbe`: `45` runs, `91,032 ms` (`3.80%`)
  - `CategoriserCostProbe`: `24` runs, `72,653 ms` (`3.03%`)
  - `TweetCostProbe`: `23` runs, `29,438 ms` (`1.23%`)
  - `IndexIdProviderCostProbe`: `24` runs, `11,960 ms` (`0.50%`)
- Indexer internal split (`IndexerCostProbe.Complete`):
  - `update-ms` average `18,372.84 ms`
  - `update-ms` contributes `99.51%` of Indexer `total-ms`
  - `initiate-ms` + `complete-ms` overhead together `<0.5%`.
- Dependency total-time share (`AppDependencies`, `indexer-infra`, non-localhost):
  - `Invoke`: `~2,499.4s` (`61.29%`) [in-proc dependency spans]
  - `amp-api.podcasts.apple.com`: `~759.8s` (`18.63%`)
  - `api.spotify.com`: `~467.3s` (`11.46%`)
  - `youtube.googleapis.com`: `~96.8s` (`2.37%`)
  - `bsky.social`: `~62.6s` (`1.53%`)
  - `cultpodcasts-db-uksouth.documents.azure.com`: `~44.9s` (`1.10%`)
- Interpretation:
  - Dominant cost pressure is activity runtime in `Indexer` (mainly `UpdatePodcasts` work), not Durable handoff overhead and not Cosmos latency alone.
  - Cosmos V2 remains active but is a small fraction of dependency time in this 24h slice.
  - Durable storage transactions remain in prior band (`195,357/day`) and do not indicate a new spike.
- Cost-row note:
  - `az consumption usage list` currently returns `pretaxCost/usageQuantity=None` for recent windows in this tenant, so final currency mapping still requires Cost Management export / delayed usage finalization.
- Next actions (ordered):
  1. Reduce Indexer `update-ms` by shrinking per-pass workload (fewer IDs/pass, narrower released-since windows, or skip high-cost enrichments on non-primary passes).
  2. Add pass-level strategy to limit Apple/Spotify-heavy enrichment to one pass/hour and keep follow-up passes lightweight.
  3. Keep existing social/query reductions and validate 24h probe deltas after each change before broadening scope.
  4. Refresh cost export once finalized billing rows are available, then map probe-time deltas to actual `Flex Consumption - On Demand Execution Time` spend.

## 2026-03-28 - Deep dive: Indexer pass/flag impact (24h)
- Parsed `IndexerCostProbe.Start` + `IndexerCostProbe.Complete` pairs by `instance-id + pass` to compare `update-ms` by pass and strategy flags.
- By pass (`IndexerCostProbe.Complete`):
  - Pass 1: `24` runs, avg `update-ms ~23,519`, p95 `~32,450`, total `~569.2s`
  - Pass 2: `24` runs, avg `update-ms ~19,211.9`, p95 `~28,144`, total `~462.4s`
  - Pass 3: `24` runs, avg `update-ms ~13,281.1`, p95 `~17,756`, total `~320.1s`
  - Pass 4: `24` runs, avg `update-ms ~17,479.4`, p95 `~22,037`, total `~420.9s`
- By `index-spotify` flag (from Start probe):
  - `index-spotify=False`: `48` runs, avg `update-ms ~20,622.7`
  - `index-spotify=True`: `48` runs, avg `update-ms ~16,123.0`
- By expensive-query flags (from Start probe):
  - `skip-expensive-youtube=True`, `skip-expensive-spotify=True`: `80` runs, avg `update-ms ~17,161.3`
  - `skip-expensive-youtube=True`, `skip-expensive-spotify=False`: `12` runs, avg `update-ms ~23,713.0`
  - `skip-expensive-youtube=False`, `skip-expensive-spotify=False`: `4` runs, avg `update-ms ~26,584.0`
- Interpretation:
  - Runs that allow expensive Spotify/YouTube querying are materially slower and likely amplify Flex execution-time billing spikes.
  - Indexer pass workload is uneven; pass 1 contributes the largest total `update-ms` among passes.
- Candidate low-risk optimization to test next:
  - Keep current orchestration shape, but enforce heavy enrichment on one pass/hour only (for example pass 1) and force `skip-expensive-* = true` on remaining passes; then compare 24h `IndexerCostProbe` `sum(total-ms)` delta before broader changes.

## 2026-03-28 - Implementation: pass-level expensive-query gating in Indexer
- Applied code change in `Cloud/Indexer/Indexer.cs` to gate expensive enrichment queries by pass.
- New behavior:
  - Pass `1`: retains existing strategy-driven behavior for expensive Spotify/YouTube queries.
  - Passes `2-4`: always force `SkipExpensiveYouTubeQueries=true` and `SkipExpensiveSpotifyQueries=true`.
- Implementation detail:
  - Added `isPrimaryPass = indexerContextWrapper.Pass == 1` and set:
    - `SkipExpensiveYouTubeQueries = !isPrimaryPass || !indexingStrategy.ExpensiveYouTubeQueries()`
    - `SkipExpensiveSpotifyQueries = !isPrimaryPass || !indexingStrategy.ExpensiveSpotifyQueries()`
- Intent:
  - Preserve orchestration shape (4 passes) while reducing high-cost enrichment work on non-primary passes.
- Validation target for next 24h capture:
  - Reduce `IndexerCostProbe` `sum(total-ms)` and `avg(update-ms)`.
  - Specifically reduce `update-ms` on passes `2-4` compared to baseline (`~19.2s`, `~13.3s`, `~17.5s`).

## 2026-03-28 - Update: rotating primary pass by hour
- Refined pass-level gating so the primary pass rotates by time instead of being fixed to pass `1`.
- Code updates:
  - `Cloud/Indexer/IIndexingStrategy.cs`: added `IsPrimaryPass(int pass, int totalPasses)`.
