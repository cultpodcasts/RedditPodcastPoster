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
- There is **no broad before→after duration blow-up** across indexer/discovery requests during the spike window; most functions are flat or faster, with only small increases in selected paths.
- This points away from a pure request-duration regression as the primary cost driver and supports continuing with scale-control and meter-level cost attribution checks.

## Cost concentration check (function-level proxy) — 2026-04-18

Using `ai-infra` `requests` telemetry, execution-time share by function was compared for pre-spike (`2026-04-09..2026-04-15`) and spike (`2026-04-16..2026-04-18`) windows.

- **Spike share leaders**:
  - `orchestration:HourlyOrchestration`: `36.70%`
  - `activity:Indexer`: `19.96%`
  - `activity:Publisher`: `8.46%`
  - `orchestration:HalfHourlyOrchestration`: `7.87%`
  - `activity:Tweet`: `7.57%`
- **Top two combined share** increased from ~`47.07%` (pre) to ~`56.66%` (spike), indicating stronger concentration in Hourly orchestration + Indexer activity rather than a broad across-the-board rise.
- Discovery path share dropped materially (`~22.69%` pre combined for orchestration/activity to `~9.60%` spike combined).
- Conclusion: the elevated period is concentrated, with `orchestration:HourlyOrchestration` the largest proxy contributor and `activity:Indexer` second; however, neither shows a major per-call duration blow-up versus pre-window.
