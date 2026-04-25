# Handover: Cost Alerting + Memory Probe Orchestration (2026-04-25)

## Scope of this session

This session implemented and stabilized the cost-observability path for Flex Consumption Functions across `Indexer`, `Discovery`, and `Api`, with focus on:

1. Budget and alert coverage for free-allowance exhaustion signals.
2. Warning-level probe telemetry for execution duration and memory footprint.
3. Refactor to a centralized probe orchestration model (`IMemoryProbeOrchestrator`) so call sites remain minimal and consistent.
4. Removal of legacy `EnableCostInstrumentation` toggle from `IndexerOptions`; probe enablement now uses only `MemoryProbeOptions`.

---

## What was added

### 1) Infrastructure alerting (Bicep)

- Added subscription-scope budget deployment module:
  - `Infrastructure/functions-budget-subscription.bicep`
- Integrated budget module from `Infrastructure/functions.bicep`.
- Added/updated alert resources in `Infrastructure/functions.bicep`:
  - Action group
  - Scheduled query alerts for:
    - `OutOfMemory` signals
    - host drain spikes (`/admin/host/drain`)
    - failed executions

### 2) Memory probe model in Azure assembly

Added under `Cloud/Azure/Diagnostics`:

- `MemoryProbe.cs`
  - `MemoryProbeOptions` (`Enabled` flag)
  - `MemoryProbeSnapshot` capture (`GC.GetTotalMemory`, heap size, working set, private bytes)
- `IMemoryProbeOrchestrator.cs`
  - `IMemoryProbeOrchestrator.Start(string functionName)`
  - `IMemoryProbeScope.End()` / `End(bool success, string? errorType)`
- `MemoryProbeSession.cs`
  - concrete logging session
  - no-op scope implementation
- `MemoryProbeOrchestrator.cs`
  - reads `MemoryProbeOptions`
  - returns real/no-op scope

### 3) Centralized call-site pattern

Call sites now use:

- `var probe = _memoryProbeOrchestrator.Start(nameof(ClassName));`
- `probe.End();` on success
- `probe.End(false, ex.GetType().Name);` on failure

This was applied across:

- Indexer activities (`IndexIdProvider`, `Indexer`, `Categoriser`, `Poster`, `Publisher`, `Tweet`, `Bluesky`)
- Discovery (`Discover`, `DiscoveryTrigger`)
- API via `MemoryProbedHttpBaseClass`

### 4) API base-class split

- `BaseHttpFunction` restored to pure auth/dispatch responsibility.
- `MemoryProbedHttpBaseClass` introduced for probe-wrapped request handling.
- API controllers switched to inherit `MemoryProbedHttpBaseClass`.

### 5) Configuration simplification

- Removed `EnableCostInstrumentation` from:
  - `Class-Libraries/RedditPodcastPoster.Configuration/IndexerOptions.cs`
  - `Infrastructure/functions.bicep` app settings (`indexer__EnableCostInstrumentation`)

Probe on/off is now controlled by `memoryProbe__Enabled` only.

---

## Why this was added

1. **Cost control visibility**: identify where runtime spend is concentrated (duration + memory footprint) and detect regressions quickly.
2. **Production-safe telemetry**: warnings are retained where Information level is filtered.
3. **Operational readiness**: budget and monitor alerts provide early warning before/when costs exceed expected levels.
4. **Maintainability**: orchestrator pattern removes duplicated start/complete probe blocks and keeps instrumentation behavior consistent.

---

## Free allowance and reset timing

### Flex Consumption (current hosting model)

Per subscription, per month (On-Demand meters):

- **Execution time**: `100,000 GB-s`
- **Executions**: `250,000`

Free grant resets at the **start of each billing month** (calendar month boundary on billing scope).

Budget alerts in this session were configured as **monthly threshold notifications** (50/75/90/100%) to approximate free-allowance exhaustion progression and notify before overspend.

---

## What the extra logging enables

With current probes you can:

1. Compare per-activity latency trends (`update-ms`, `total-ms`) for indexing paths.
2. Correlate memory growth by function invocation:
   - managed bytes
   - heap size
   - working set
   - private bytes
   - deltas from start to end
3. Determine whether specific apps/functions may safely use smaller memory tiers based on p50/p95/max private-bytes.
4. Cross-correlate cost spikes with:
   - host drain/restart behavior
   - failed executions
   - OOM-type traces/exceptions

---

## Logging and probe configuration

### App setting toggle

- `memoryProbe__Enabled`
  - `true` => orchestrator emits full probe logs
  - `false` => orchestrator returns no-op scope (call sites unchanged)

### Severity

- Probe logs are emitted at **Warning** level to remain queryable in current production filtering.

### Current timing guidance in repo

- For Indexer cost probe timing, only `updateMs` should be emphasized as primary timing metric in logging.

---

## Follow-up checklist

1. Deploy branch and verify budget + alert resources exist.
2. Confirm `MemoryProbe.Start`/`MemoryProbe.Complete` warnings appear for Api/Discovery/Indexer.
3. Run 24–72h analysis window and compute p50/p95/max memory by function/app.
4. Re-evaluate per-app memory size only after telemetry confirms safe headroom.
5. Tune alert thresholds/windows if noisy.

---

## Notes

- Flex Consumption valid `instanceMemoryMB` values remain: `512`, `2048`, `4096`.
- `1024` is invalid and should not be reintroduced.
