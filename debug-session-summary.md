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
