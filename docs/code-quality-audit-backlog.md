# Code quality audit — ranked backlog (Phase 2)

**Date:** 2026-07-22  
**Inputs:** Phase 1 identify boards (C# / Angular / Api)  
**Scorecard:** `value = (prod_risk × change_frequency × confidence_gain) / effort` (factors 1–5)  
**Ordering rule:** Rank by value; when close, prefer items that **unlock** later work (behavior harnesses before structural refactors).

**Canonical backlog for all three projects.** Thin pointers live in the Angular and Api repos.

---

## Phase 1 hypothesis corrections

| Plan hypothesis | Correction |
|-----------------|------------|
| Episodes BusinessRules **~881** facts | **~233** `[Fact]`/`[Theory]` under `Episodes.Tests` (**~228** in `BusinessRules/`). Suite + CI coverage gate remain strong; do not pad BR unit tests. |
| Angular Auth0 bearer copies **~29** | **~38** `getAccessTokenSilently` call sites across ~32 files. |
| Lifecycle DI Scoped vs Singleton (C#) | **Mostly refuted as defect** — Cosmos repos as Singleton is normal; no proven captive dependency. |
| Angular OnPush / signals “almost none” | **0** OnPush / `takeUntilDestroyed` / `computed`; `signal()` in 5 files only — confirmed with nuance. |

---

## Top 15 (cross-project)

| # | project | finding | value | effort | wave | unlocks | evidence path |
|---|---------|---------|-------|--------|------|---------|---------------|
| 1 | Angular | Playwright harness + discovery / episode / auth smoke journeys | **41.7** | M | 2 | Auth interceptor, `*-send` collapse, dialog dedupe, OnPush-on-touch | `website/cultpodcasts/package.json` (no e2e); `src/app/discovery-api/`, `edit-episode-dialog/`, `has-role.guard.ts`; only 3 util `*.spec.ts` |
| 2 | Api | Vitest + `@cloudflare/vitest-pool-workers` + first route-contract journeys | **41.7** | M | 2 | Status-forwarding design, `proxyToAzure`, OpenAPI tighten, CI `test`/`lint` | `Api/package.json` (no test script); zero `*.test.ts`; journeys in Phase 1 Api board |
| 3 | Angular | `ScrollDispatcher.scrolled()` without teardown on infinite-scroll lists | **36.0** | S | 1 | Safer revisit of list pages; OnPush/`takeUntilDestroyed` later | `search-api.component.ts`, `podcast-api.component.ts`, `subject-api.component.ts`, `bookmarks-api.component.ts` |
| 4 | C# | Episode mutate API behavior tests (auth + Post/Publish/Delete side-effects) | **33.3** | M | 2 | Safe `EpisodeHandler` split; shared handler error-contract tests | `Cloud/Api/EpisodeController.cs`, `Cloud/Api/Handlers/EpisodeHandler.cs`; only IoC resolve in `ApiIocTests` |
| 5 | C# | Bluesky / Twitter / social activity behavior tests | **33.3** | M | 2 | Safer posting / dry-run / skip-platform changes on hourly path | `HourlyOrchestration.cs`, `Activities/Bluesky.cs`, `Tweet.cs`; no `*.Bluesky.Tests` / `*.Twitter.Tests` |
| 6 | Api | `AddResponseHeaders` Allow-Methods — `toUpperCase` not invoked | **30.0** | S | 1 | Correct ACA-Methods on proxied responses; easy Vitest header assert | `Api/src/AddResponseHeaders.ts:20` |
| 7 | Api | Azure proxies collapse non-success to opaque 500 (esp. discovery-curation) | **26.7** | M | 2 | `proxyToAzure({ expectedStatuses })` contract; curator UI gets real 4xx | `submitDiscovery.ts`, `getDiscoveryReports.ts`; contrast `publicGetEpisode.ts` / `getEpisode.ts` 404 forward |
| 8 | Angular | Auth0 bearer assembled inline ~38× — no auth HTTP interceptor | **26.7** | M | 3 | Kill bearer clones; simplify dialogs / `*-send` / profile calls | `getAccessTokenSilently` across `*-api`, `*-send`, `*-dialog`; `app.config.ts` interceptors |
| 9 | C# | No solution-wide analyzer baseline | **24.0** | S | 1 | CI-wide smell/bug signal before large refactors | `Directory.Build.props` |
| 10 | Angular | `rename-podcast-dialog` `tokenCtr` + nested promise anti-pattern | **24.0** | S | 1 | Reliable admin rename; pattern for other dialogs | `rename-podcast-dialog.component.ts` (~L46–56) |
| 11 | Api | 401 vs 403 (and OpenAPI “Unauthorized” labels) inconsistent | **24.0** | S | 1 | Stable auth matrix for Vitest; predictable client handling | R2 list handlers → 401; most proxies → 403; `openapiRoutes.ts` authResponses |
| 12 | Api | Secrets plaintext in set-secret scripts | **20.0** | S | 1 | Remove checked-in Search/Auth0 secrets; rotate Search key | `scripts/set-secrets-production.ps1`, `set-secrets-preview.ps1` |
| 13 | C# | Cosmos episode/podcast repo partition & query assumptions untested | **20.0** | M | 2 | Safer Persistence changes; real repo coverage (today’s 4 facts are domain match only) | `EpisodeRepository.cs`, `PodcastRepository.cs`, `Persistence.Tests/*` |
| 14 | Api | `LogCollector` ASN typo (`'asn)'`) — ASN never recorded | **16.0** | S | 1 | Correct request logging; unit assert with harness | `Api/src/LogCollector.ts:50-52` |
| 15 | Angular | Six near-clone `*-send` spinner dialogs | **16.0** | M | 3 | One sender + progress UX; clears path to add/edit dialog dedupe | `add-episode-send/` … `edit-person-send/`; contrast `episode-update.service.ts` |

**Just below the cut (execute in wave checklists / next rank):** C# migration-doc fix (16.0, W1); C# discovery orchestration journey (16.0, W2); Api `proxyToAzure` extract (15.0, W3); Angular add/edit dialog dedupe (12.8, W3); C# handler error-contract unify (12.0, W2 with #4); Angular `console.log` strip (12.0, W1); C# `EpisodeHandler` split (11.3, W3).

---

## Wave 1 — Quick wins / proven bugs

Theme: ship low-effort fixes that reduce prod risk or unlock cleaner Wave 2 contracts. No large structural refactors.

### Checklist

- [x] **Api — Allow-Methods `()`**  
  - Paths: `Api/src/AddResponseHeaders.ts`  
  - Acceptance: `Access-Control-Allow-Methods` contains uppercase method strings (e.g. `GET`, `POST`), not function `toString` garbage; covered by a Vitest header assert once Wave 2 harness exists (or a tiny unit test in W1).

- [x] **Api — LogCollector ASN**  
  - Paths: `Api/src/LogCollector.ts`  
  - Acceptance: `hasOwnProperty('asn')` (no stray `)`); ASN from `collectRequest` appears in collected log lines.

- [x] **Api — Normalize 401 / 403 + OpenAPI labels**  
  - Paths: `getSubjects.ts`, `getFlairs.ts`, `getLanguages.ts`, `getDiscoveryInfo.ts`, `getPeople.ts`, `openapiRoutes.ts`, docs gate in `index.ts`  
  - Acceptance: missing/invalid auth → **401**; authenticated missing permission → **403**; OpenAPI `authResponses` wording matches; document intended matrix for Vitest.

- [x] **Api — Secrets out of repo (+ rotate)**  
  - Paths: `scripts/set-secrets-production.ps1`, `scripts/set-secrets-preview.ps1` (+ `.cmd` wrappers)  
  - Acceptance: no Search `apikey` / Auth0 client secrets in git; scripts read from Key Vault / env / interactive `wrangler secret`; Azure Search key rotated if it was committed.  
  - **Done 2026-07-22:** plaintext removed; scripts require `CULTPODCASTS_SEARCH_APIKEY` + `CULTPODCASTS_AUTH0_CLIENT_ID` env vars. **Ops follow-up:** rotate Azure Search key (was previously committed) and re-`wrangler secret put apikey`.

- [x] **Angular — Scroll subscription teardown**  
  - Paths: `search-api.component.ts`, `podcast-api.component.ts`, `subject-api.component.ts`, `bookmarks-api.component.ts`  
  - Acceptance: scroll subscription uses `takeUntilDestroyed` (or equivalent); revisiting a list route does not stack duplicate `scrolled` handlers.  
  - **Done 2026-07-22:** `takeUntilDestroyed` + one-shot `scrollSubscribed` guard; `scrollDisplatcher` → `scrollDispatcher`.

- [x] **Angular — rename-podcast rewrite**  
  - Paths: `rename-podcast-dialog.component.ts`  
  - Acceptance: no `tokenCtr` gate; async/await (or single `firstValueFrom` chain); uses shared auth headers pattern if available, else clean one-shot token fetch.  
  - **Done 2026-07-22:** async/await + single token fetch; `tokenCtr` removed.

- [x] **C# — Analyzer baseline** (done 2026-07-22)  
  - Paths: `Directory.Build.props`, `.editorconfig`  
  - Acceptance: `AnalysisLevel` (and preferably `EnforceCodeStyleInBuild` or warn-only NET analyzers) set solution-wide; CI still green (warnings or baseline suppressions documented if needed).  
  - **Shipped:** `AnalysisLevel=latest`, `EnableNETAnalyzers=true`, `AnalysisMode=Default`, CodeQuality category warn-only in `.editorconfig`. **`EnforceCodeStyleInBuild` left false** — enabling it with Style rules caused per-project IDE0005/`GenerateDocumentationFile` meta-warnings and slow builds; IDE0130 remains IDE/editorconfig warning. `TreatWarningsAsErrors` unset on purpose. Verified: `Episodes` + `Api` + `Persistence` Release builds — 0 warnings / 0 errors.

- [x] **Also (hygiene, high ROI leftovers)**  
  - [x] C#: fix `docs/migration/remaining-work-audit.md` — stop claiming zero detached-episode tests; point at BusinessRules (~228 facts) + `scripts/coverage-gate.ps1` (done 2026-07-22).  
  - [x] Angular (2026-07-22): strip curator `console.log`; delete unused `FormBuilder` in `add-episode-dialog` / `edit-subject-dialog`; refresh `README.md` to Angular 21 / local ports; rename `scrollDisplatcher` on touched list APIs (`FeatureSwtichService` deferred until that service is touched).

**Wave 1 done when:** all Api bugs above fixed; Angular scroll + rename shipped; C# analyzers on; secrets no longer in Api scripts.

---

## Wave 2 — Behavior-test harnesses + first journeys

Theme: stand up harnesses and green critical journeys **before** structural splits. Prefer behavior-level over unit theater.

### Checklist

- [x] **Api — Vitest + scripts** (2026-07-22)  
  - Paths: `Api/package.json` (`test` / `lint`), `vitest.config.ts` (node env), `tests/*.spec.ts`  
  - Acceptance met: `npm test` → 9 passed (auth matrix, CORS, headers, LogCollector ASN, discovery-curation contracts). Full `@cloudflare/vitest-pool-workers` SELF.fetch deferred (optional follow-on).

- [x] **Api — Status allowlist / forward mapping** (2026-07-22)  
  - Paths: `proxyToAzure.ts`, `submitDiscovery.ts`, `getDiscoveryReports.ts`  
  - Acceptance: discovery-curation GET/POST forward Azure 4xx via `forwardStatuses`; covered by discovery-curation specs.

- [x] **Angular — Playwright harness** (2026-07-22)  
  - Paths: `playwright.config.ts`, `e2e/*.spec.ts`, `npm run test:e2e`  
  - Acceptance: 4 passed — auth gate, discovery curation (mocked), episode save (mocked).

- [x] **C# — Episode mutate API behavior tests** (2026-07-22)  
  - Paths: `Cloud/Unit-Tests/FunctionHost.Tests/Api/EpisodeHandlerDeleteTests.cs`  
  - Acceptance: 401 missing principal; Delete conflict/not-found/bad-request/success; no social posters on delete.

- [x] **C# — Social activity / poster behavior tests** (2026-07-22)  
  - Paths: `Cloud/Unit-Tests/Indexer.Tests/SocialActivityBehaviorTests.cs` — 4 passed.

- [x] **C# — Cosmos repo partition-key contract** (2026-07-22)  
  - Paths: `EpisodeRepository.CreatePartitionKey` + `Persistence.Tests/EpisodeRepositoryPartitionKeyTests.cs`

- [x] **C# — Discovery slot journey (stretch in Wave 2)** (2026-07-22)  
  - Paths: `Cloud/Unit-Tests/Discovery.Tests/DiscoverActivityBehaviorTests.cs`  
  - Acceptance: Discover activity — happy path (lookback → service → Save), lookback fail-closed (no providers/save), duplicate operation skip, save failure → `DiscoveryOrchestrationIncompleteException`. Provider I/O mocked; dedupe remains library-unit-covered (not on Durable Discover path).

**Wave 2 done when:** each project has a harness and ≥1 critical journey green — **met** (Api/Angular/C#).

---

## Wave 3 — Structural (unlocked by Wave 2)

Theme: refactor with regression nets in place. Stop for review after each major extract.

### Checklist

- [x] **Api — Extract `proxyToAzure`** (2026-07-22 → bulk migration complete)  
  - Paths: `Api/src/proxyToAzure.ts` (+ `pathSuffix`, optional permission, `passthroughOtherStatuses`); all simple Azure Function proxies migrated. Left as-is: `search.ts`, R2 handlers, `getPeople` R2+fallback.  
  - Acceptance: discovery + episode/podcast/subject/person/publish proxies use helper; Vitest still green (9).

- [x] **Angular — Auth interceptor / shared authenticated HTTP** (2026-07-22)  
  - Paths: `auth.interceptor.ts`, `app.config.ts`, `episode-update.service.ts`  
  - Acceptance: API-host requests get bearer via interceptor; `AUTH_SCOPE` context for scope.

- [x] **Angular — Collapse `*-send` → services** (2026-07-22)  
  - Paths: `curation-submit.service.ts`; all six `*-send` dialogs call service (no inline token fetch).

- [x] **Angular — Deduplicate add/edit episode (then podcast) dialogs** — full merge still deferred; **safe slices done 2026-07-22** on [website #426](https://github.com/cultpodcasts/website/pull/426): `episode-form.util.ts` + `podcast-form.util.ts`; thin add/edit shells retained.

- [x] **C# — Split `EpisodeHandler` by use-case** (2026-07-22 → completed on PR [#911](https://github.com/cultpodcasts/RedditPodcastPoster/pull/911))  
  - Paths: Controllers → Handlers/{Area} → Services/{Area}; `EpisodeHandler` removed. Exemplary Dtos/`architecture.md` on same PR.

- [ ] **Follow-ons / zoneless readiness**  
  - Angular (**list APIs OnPush done 2026-07-23** on website #426): OnPush + signals/`toSignal` + `takeUntilDestroyed` on `search-api`, `podcast-api`, `subject-api`, `bookmarks-api`, `episodes-api`, `outgoing-episodes-api`, `discovery-api`. Dual router removed (`app.routes.ts` + `provideRouter` only). Still Zone (`provideZoneChangeDetection`) — most non-list components remain Default CD; do **not** drop Zone until those are OnPush-ready.  
  - Api: **Zod request schemas started 2026-07-23** on Api #115 (`openapiSchemas.ts`); opaque responses remain until modelled;  deprecate old `/episode/:id` after client cutover; decide Analytics Engine use-or-drop; bump `compatibility_date` deliberately.  
  - C#: **ValidateOnStart started 2026-07-23** on PR #911 — `PosterOptions` (Indexer) + `HostingOptions` (Api), mirroring Discovery's `DiscoverOptions` pattern; `LoggerMessage` on hot paths when edited; optional `DomainTestFixture` split only if BR churn demands it.

**Wave 3 done when:** Api proxy helper landed; Angular auth interceptor + `*-send` collapse landed; C# EpisodeHandler split landed — each with Wave 2 tests still green. **Met.** Remaining items are follow-ons (zoneless completion, Zod, ValidateOnStart).

---

## Wave summary

| Wave | Goal | Top-15 items |
|------|------|----------------|
| **1** | Bugs + hygiene | #3, #6, #9, #10, #11, #12, #14 (+ migration doc / console.log / FormBuilder) |
| **2** | Harnesses + journeys | #1, #2, #4, #5, #7, #13 |
| **3** | Structural | #8, #15 (+ `proxyToAzure`, EpisodeHandler split, dialog dedupe) |

Stop after each wave for a short review so remaining effort stays on highest score.
