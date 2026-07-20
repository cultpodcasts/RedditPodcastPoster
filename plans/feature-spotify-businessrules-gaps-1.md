---
goal: Close remaining Spotify BusinessRules and leftover unit-test gaps from the Jul 2026 audit
version: 1.0
date_created: 2026-07-15
last_updated: 2026-07-15
owner: Cult Podcasts
status: 'Completed'
tags: [testing, business-rules, spotify, discovery, submiturl, indexer, chore]
---

# Introduction

![Status: Completed](https://img.shields.io/badge/status-Completed-brightgreen)

Close the remaining Spotify BusinessRules / unit-test gaps identified by audit agent [`21b3e8e5`](../../.cursor/projects/c-Users-jonbr-source-repos-cultpodcasts-RedditPodcastPoster/agent-transcripts/f8b53c23-6b86-4355-b0ba-85eb92364316/subagents/21b3e8e5-1713-4a3c-a560-a4c9eaf1d2da.jsonl) (fresh gap audit, PodcastServices.Spotify). Work is **tests-only**, ordered by production surface value (Discovery → SubmitUrl/API → Indexer → CLI → polish), and executable as incremental PRs.

**Out of scope for this plan:** production behavior changes, coverage-floor raises without measured gains, UrlSubmission `EpisodeHelperTests` migration (noted only).

**Execution note (2026-07-15):** PR-1 through PR-6 implemented in one pass. `dotnet test` Spotify.Tests: **111 passed**. WS-H skip/defer items left untouched.

## Recommended PR / commit sequence

| PR | Milestone | Items | Est. |
|----|-----------|-------|------|
| **PR-1** | Discovery search + URL authority | WS-A Searcher; WS-B UrlCategoriser URL `Resolve` | M + M |
| **PR-2** | Submit routing gates + migrate leftover | WS-C IdResolver/Matcher; fold `SpotifyUriExtensionsTests` | S |
| **PR-3** | Indexer provider filter | WS-E SpotifyEpisodeProvider | S–M |
| **PR-4** | CLI podcast enrich | WS-D PodcastResolver + PodcastEnricher | M |
| **PR-5** (optional) | Paginator extras | WS-F QueryPaginator rewrite / Date / growth-stop | M |
| **PR-6** (optional) | Selective medium gaps | WS-G Factory `Create(string)`; FindMatchingPodcasts; RetrievalHandler skip | S |
| — | **Skip / defer** | WS-H low-value infra | — |

Prefer one PR per milestone. PR-1 may split into two commits/PRs if Searcher mocks are large. Do not implement in the same PR as production logic changes.

## 1. Requirements & Constraints

- **REQ-001**: Add BusinessRules coverage for each prioritized gap listed in §2; do not change production logic in these PRs unless a test uncovers a confirmed bug (then separate signed-off behavior PR).
- **REQ-002**: New tests live under `Class-Libraries/RedditPodcastPoster.PodcastServices.Spotify.Tests/BusinessRules/**` as `*Rules.cs` with plain-English `[Fact(DisplayName = "...")]` (or `[Theory]` + `InlineData`/`MemberData` for matrices).
- **REQ-003**: Follow `.cursor/rules/unit-tests.mdc`: Arrange/Act/Assert comments; `DomainTestFixture` where episode/podcast specimens apply; no hardcoded calendar literals / platform IDs when assertion does not depend on them; relative date helpers (`UtcDateDaysAgo`, etc.).
- **REQ-004**: Tests-only PRs preferred; run `dotnet test Class-Libraries/RedditPodcastPoster.PodcastServices.Spotify.Tests` before marking a phase done.
- **REQ-005**: Fold `Extensions/SpotifyUriExtensionsTests.cs` into IdResolver BusinessRules and delete the leftover file in the same PR as WS-C.
- **CON-001**: Do not re-prove Layer-2 matcher heuristics already covered by `SearchResultFinderCatalogueWrapperRules` / EpisodeResolver rules when adding PodcastResolver tests — mock `ISpotifySearchResultFinder` where appropriate.
- **CON-002**: Do not migrate `UrlSubmission.Tests/EpisodeHelperTests.cs` in this plan (out of PodcastServices.Spotify; optional future UrlSubmission BusinessRules work).
- **GUD-001**: Mirror existing style in `SpotifyQueryPaginatorRules`, `SpotifyEpisodeRetrievalHandlerRules`, `FindSpotifyEpisodeRequestFactoryRules`, `SpotifyUrlCategoriserRules`.
- **PAT-001**: Mock `ISpotifyClientWrapper` / resolvers with Moq or small capturing fakes (see `CapturingSpotifyClientWrapper` in paginator rules).
- **PAT-002**: Characterize current behavior when quirks appear (e.g. `SpotifyIdResolver.GetEpisodeId` returns empty string on no match, while URL `Resolve` null-checks) with `// KNOWN:` comments — do not "fix" in a tests-only PR.

## 2. Implementation Steps

### Implementation Phase 1 — Discovery + SubmitUrl authority (PR-1)

- GOAL-001: Lock Spotify discovery search filters and submit/curation URL-authority resolve path.

| Task | Description | Completed | Date | Est. |
|------|-------------|-----------|------|------|
| TASK-001 | **WS-A SpotifySearcher** — add `BusinessRules/Search/SpotifySearcherRules.cs` covering ReleasedSince floor-to-day, `\bterm\b` filter, PaginateAll → GetSeveral hydrate (see § item catalog WS-A) | ✅ | 2026-07-15 | M |
| TASK-002 | **WS-B SpotifyUrlCategoriser URL Resolve** — extend `BusinessRules/Categorisers/SpotifyUrlCategoriserRules.cs` (or sibling `SpotifyUrlCategoriserUrlAuthorityRules.cs`) for `Resolve(podcast, episodes, url, …)` (see WS-B) | ✅ | 2026-07-15 | M |
| TASK-003 | Run `dotnet test` on Spotify.Tests; ensure criteria/`ReleasedSince` MatchOtherServices paths unchanged | ✅ | 2026-07-15 | S |

### Implementation Phase 2 — Submit routing gates + leftover migration (PR-2)

- GOAL-002: Pin hot URL routing gates used by SubmitUrl / API / curation; remove non-BusinessRules leftover.

| Task | Description | Completed | Date | Est. |
|------|-------------|-----------|------|------|
| TASK-004 | **WS-C SpotifyIdResolver + SpotifyPodcastServiceMatcher** — add `BusinessRules/Resolvers/SpotifyIdResolverRules.cs` and `BusinessRules/SpotifyPodcastServiceMatcherRules.cs` (or combine) | ✅ | 2026-07-15 | S |
| TASK-005 | Migrate `CleanSpotifyUrl` scenarios from `Extensions/SpotifyUriExtensionsTests.cs` into IdResolver (or dedicated Extensions BusinessRules); delete leftover `*Tests.cs` | ✅ | 2026-07-15 | S |
| TASK-006 | Run Spotify.Tests | ✅ | 2026-07-15 | S |

### Implementation Phase 3 — Indexer episode provider (PR-3)

- GOAL-003: Lock indexer-facing post-filter and catalogue→Episode mapping in `SpotifyEpisodeProvider`.

| Task | Description | Completed | Date | Est. |
|------|-------------|-----------|------|------|
| TASK-007 | **WS-E SpotifyEpisodeProvider** — add `BusinessRules/Providers/SpotifyEpisodeProviderRules.cs` | ✅ | 2026-07-15 | S–M |
| TASK-008 | Run Spotify.Tests; confirm no double-coverage of adapter internals (assert Episode list shape / ReleasedSince filter / ExpensiveQueryFound passthrough) | ✅ | 2026-07-15 | S |

### Implementation Phase 4 — CLI podcast enrich (PR-4)

- GOAL-004: Cover `SpotifyPodcastResolver.FindPodcast` and `SpotifyPodcastEnricher.AddIdAndUrls` used by CLI `AddAudioPodcast`.

| Task | Description | Completed | Date | Est. |
|------|-------------|-----------|------|------|
| TASK-009 | **WS-D SpotifyPodcastResolver** — add `BusinessRules/Resolvers/SpotifyPodcastResolverRules.cs` | ✅ | 2026-07-15 | M |
| TASK-010 | **WS-D SpotifyPodcastEnricher** — add `BusinessRules/Enrichers/SpotifyPodcastEnricherRules.cs` | ✅ | 2026-07-15 | M |
| TASK-011 | Run Spotify.Tests | ✅ | 2026-07-15 | S |

### Implementation Phase 5 — Optional paginator extras (PR-5)

- GOAL-005: Complete QueryPaginator lock-in for rewrite, date truncate, and reverse-chrono growth stop (hang/PaginateAll already covered).

| Task | Description | Completed | Date | Est. |
|------|-------------|-----------|------|------|
| TASK-012 | **WS-F** Extend `BusinessRules/Paginators/SpotifyQueryPaginatorRules.cs` with three missing behaviors | ✅ | 2026-07-15 | M |
| TASK-013 | Run Spotify.Tests | ✅ | 2026-07-15 | S |

### Implementation Phase 6 — Optional selective medium (PR-6)

- GOAL-006: Close remaining selective gaps that are thin but complete the factory/finder/handler matrix.

| Task | Description | Completed | Date | Est. |
|------|-------------|-----------|------|------|
| TASK-014 | **WS-G** Factory `Create(string episodeId)` in `FindSpotifyEpisodeRequestFactoryRules.cs` | ✅ | 2026-07-15 | S |
| TASK-015 | **WS-G** Real `FindMatchingPodcasts` in SearchResultFinder rules (exact name, case/trim, null list) | ✅ | 2026-07-15 | S |
| TASK-016 | **WS-G** RetrievalHandler `SkipSpotifyUrlResolving` → `Handled=false` in `SpotifyEpisodeRetrievalHandlerRules.cs` | ✅ | 2026-07-15 | S |

### Implementation Phase 7 — Explicit skip / defer (no PR)

- GOAL-007: Document low-value items that must **not** block the gap-closure work.

| Task | Description | Completed | Date | Est. |
|------|-------------|-----------|------|------|
| TASK-017 | **WS-H SKIP/DEFER** — confirm no work for Provider Flush/cache, Client 429→SkipSpotifyUrlResolving, `PodcastExtensions` map, client factories unless a production incident requires characterization | ✅ (skipped as planned) | 2026-07-15 | — |

---

## Item catalog (executable detail)

Each item below is self-contained for an implementing agent.

### WS-A — SpotifySearcher (Discovery) — Priority 1 — Estimate **M**

| Field | Detail |
|-------|--------|
| **Goal** | Prove discovery search floors `ReleasedSince` to day UTC, filters episodes by whole-word query in name/description, paginates search hits, then hydrates matching IDs via `GetSeveral`. |
| **Production** | `Class-Libraries/RedditPodcastPoster.PodcastServices.Spotify/Search/SpotifySearcher.cs` |
| **Call site** | `RedditPodcastPoster.Discovery/SearchProvider` |
| **Files to touch** | **Create** `...Spotify.Tests/BusinessRules/Search/SpotifySearcherRules.cs` |
| **Dependencies** | None (first PR item). Needs fake `ISpotifyClientWrapper` + `IHtmlSanitiser` (or real sanitiser if DI-light). |
| **Key scenarios** | (1) `ReleasedSince` with time-of-day → context copy uses `.Floor(1 day)` before filter (assert via client capturing the filtered boundary or by episode release relative to floor). (2) Episode name matches `\bquery\b` → included; substring without word boundary → excluded. (3) Description-only whole-word match → included. (4) After filter, `GetSeveral` called with matching episode IDs; result maps to `EpisodeResult` with Spotify URL and `DiscoverService.Spotify`. (5) No search results / null paginate → empty list, no `GetSeveral`. |
| **Done when** | All scenarios green; DisplayNames document discovery risk (false positives without word boundary; missing hydrate). |

### WS-B — SpotifyUrlCategoriser.Resolve(podcast, episodes, url) — Priority 2 — Estimate **M**

| Field | Detail |
|-------|--------|
| **Goal** | Prove URL-authority path: short-circuit on existing `Urls.Spotify`, extract episode id, `Create(episodeId)` find, throw on miss. |
| **Production** | `Categorisers/SpotifyUrlCategoriser.cs` lines 109–151 |
| **Call sites** | SubmitUrl, discovery curation `DiscoveryResultProcessor`, `UrlCategoriser` |
| **Files to touch** | Extend `BusinessRules/Categorisers/SpotifyUrlCategoriserRules.cs` **or** add `SpotifyUrlCategoriserUrlAuthorityRules.cs` (prefer sibling if criteria rules file is already large) |
| **Dependencies** | Prefer after or with WS-C (IdResolver) so URL parsing scenarios share fixtures; can stub id via known fixture URLs. |
| **Key scenarios** | (1) Podcast + episode list where `Urls.Spotify == url` → returns `ResolvedSpotifyItem` from existing episode **without** calling resolver. (2) New URL with parseable episode id → calls `FindEpisode(Create(episodeId), …)` and maps FullEpisode fields. (3) Resolver returns null FullEpisode → `InvalidOperationException` with episode id in message. (4) **KNOWN:** document whether non-episode Spotify URL yields empty id vs throw (current `GetEpisodeId` returns `Groups[…].Value`, not null — characterize, do not fix here). |
| **Done when** | Criteria/`AppleTitle` rules untouched; URL authority path has ≥3 Facts with DisplayNames. |

### WS-C — SpotifyIdResolver / SpotifyPodcastServiceMatcher (+ migrate UriExtensions) — Priority 3 — Estimate **S**

| Field | Detail |
|-------|--------|
| **Goal** | Pin submit/API/curation routing gates: host is Spotify; episode id extracted from path; CleanSpotifyUrl strips query. |
| **Production** | `Resolvers/SpotifyIdResolver.cs`, `SpotifyPodcastServiceMatcher.cs`, `Extensions/SpotifyUriExtensions.cs` |
| **Files to touch** | **Create** `BusinessRules/Resolvers/SpotifyIdResolverRules.cs`, `BusinessRules/SpotifyPodcastServiceMatcherRules.cs`; **Delete** `Extensions/SpotifyUriExtensionsTests.cs` after migrate |
| **Dependencies** | None; feeds WS-B clarity |
| **Key scenarios** | **Matcher:** host contains `spotify` (case-insensitive) → true; non-Spotify host → false. **IdResolver:** `/episode/{id}` → id; no episode segment → empty string (characterize). **CleanSpotifyUrl:** with/without `?si=` query (migrated Facts with DisplayNames). |
| **Done when** | Zero `*Tests.cs` outside BusinessRules under Spotify.Tests; all green. |

### WS-D — SpotifyPodcastResolver + SpotifyPodcastEnricher.AddIdAndUrls — Priority 4 — Estimate **M** each / **M** combined in one PR

| Field | Detail |
|-------|--------|
| **Goal** | CLI enrich when podcast `SpotifyId` empty: find show, set id + expensive flag; then fill missing episode SpotifyIds. |
| **Production** | `Resolvers/SpotifyPodcastResolver.cs`, `Enrichers/SpotifyPodcastEnricher.cs` |
| **Call site** | CLI `AddAudioPodcast` |
| **Files to touch** | **Create** `BusinessRules/Resolvers/SpotifyPodcastResolverRules.cs`, `BusinessRules/Enrichers/SpotifyPodcastEnricherRules.cs` |
| **Dependencies** | WS-G FindMatchingPodcasts helpful but not required (mock finder). Enricher depends on resolver + episode resolver mocks. |
| **Key scenarios — Resolver** | (1) `SkipSpotifyUrlResolving` → null, no client calls. (2) Known `PodcastId` → `GetFullShow` hit → wrapper with FullShow. (3) Miss full show → search shows → finder returns candidates → optional episode URL match path OR fuzzy fallback (mock finder outputs). (4) ExpensiveQueryFound from episodes provider bubbled on wrapper. |
| **Key scenarios — Enricher** | (1) Empty podcast SpotifyId + resolver returns id → podcast.SpotifyId set, returns true. (2) Resolver returns ExpensiveQueryFound → `SpotifyEpisodesQueryIsExpensive` true. (3) Podcast already has SpotifyId; episode missing id → `FindEpisode` sets episode.SpotifyId. (4) Episode already has SpotifyId → no FindEpisode for that episode. (5) No match → returns false / no mutation. |
| **Done when** | CLI enrich happy-path and skip/expensive gates covered without calling real Spotify. |

### WS-E — SpotifyEpisodeProvider — Priority 5 — Estimate **S–M**

| Field | Detail |
|-------|--------|
| **Goal** | Indexer post-filter: when `ReleasedSince` set, only episodes with `GetReleaseDate() >= ReleasedSince` reach `MapEpisode`; expensive flag passthrough. |
| **Production** | `Providers/SpotifyEpisodeProvider.cs` |
| **Call site** | Indexer via `SpotifyEpisodeRetrievalHandler` |
| **Files to touch** | **Create** `BusinessRules/Providers/SpotifyEpisodeProviderRules.cs` |
| **Dependencies** | Needs `ISpotifyPodcastEpisodesProvider` mock returning SimpleEpisodes; real or test doubles for `IEpisodeCatalogueAdapter<SpotifyCatalogueInput>`, `IEpisodeFromCandidateFactory`, `IHtmlSanitiser` (prefer real adapters from DI-free construction used elsewhere, or mock adapter to return known candidates). |
| **Key scenarios** | (1) No ReleasedSince → all provider episodes mapped. (2) ReleasedSince set → older episodes excluded. (3) `ExpensiveQueryFound` from inner provider preserved on response. (4) Empty inner list → empty results. |
| **Done when** | Filter boundary asserted with `UtcDateDaysAgo` specimens; no assertion of adapter internals beyond Episode identity/count. |

### WS-F — SpotifyQueryPaginator extras — Priority 6 — Estimate **M** — **Optional PR-5**

| Field | Detail |
|-------|--------|
| **Goal** | Lock three remaining paginator rules beyond existing null/skip/hang/PaginateAll coverage. |
| **Production** | `Paginators/SpotifyQueryPaginator.cs` lines 21–25, 76–79, 87–110 |
| **Files to touch** | Extend `BusinessRules/Paginators/SpotifyQueryPaginatorRules.cs` |
| **Dependencies** | Existing capturing wrapper patterns in same file |
| **Key scenarios** | (1) When `ReleasedSince` is null and `Next` contains `/show/`, rewrite to `/shows/` before `PaginateAll`. (2) `ReleasedSince` with time-of-day → comparison uses `.Date` truncate (episode at start-of-day boundary in/out). (3) Reverse-chrono ordered pages: growth loop stops when last release &lt; ReleasedSince or no growth (`seenGrowth`). |
| **Done when** | Three new DisplayName Facts green; existing hang/skip Facts unchanged. |

### WS-G — Selective / medium — Priority 7 — Estimate **S** — **Optional PR-6**

| Sub-item | Goal | Files | Key scenarios | Done when |
|----------|------|-------|---------------|-----------|
| **G1 Factory Create(string)** | Direct-id request for URL resolve path | Extend `FindSpotifyEpisodeRequestFactoryRules.cs` | `Create(episodeId)` → EpisodeSpotifyId set, empty podcast fields, third bool/`HasEpisodes` path as coded (line 49–57) | One Fact |
| **G2 FindMatchingPodcasts** | Exact name match (case/trim), null→empty | Extend `SearchResultFinderCatalogueWrapperRules.cs` or new `SpotifySearchResultFinderPodcastMatchRules.cs` | null list → empty; exact name ignore case; trim on show name; non-match excluded | Matrix Theory OK |
| **G3 RetrievalHandler skip** | Rate-limit skip → not handled | Extend `SpotifyEpisodeRetrievalHandlerRules.cs` | Podcast has SpotifyId; provider called; `SkipSpotifyUrlResolving=true` → `Handled=false` (even if episodes returned) | One Fact |

### WS-H — Low-value skip / defer — Priority 8 — **SKIP**

| Item | Verdict | Rationale |
|------|---------|-----------|
| Provider Flush / cache | **SKIP** | Infra cache coherence; not a business rule; hard to assert without brittle internals. |
| Client 429 → `SkipSpotifyUrlResolving` | **DEFER** | Already partially implied by skip short-circuits on consumers; full client 429 matrix is HTTP/wrapper infra. Revisit only after prod rate-limit incident. |
| `PodcastExtensions` (`ToFindSpotifyPodcastRequest`) | **DEFER** | Thin map; adequately exercised once WS-D Enricher/Resolver covered; add only if enricher arrange becomes awkward. |
| Spotify client factories / DI | **SKIP** | Composition root; not BusinessRules. |
| `UrlSubmission.Tests/EpisodeHelperTests.cs` | **DEFER (out of scope)** | Outside PodcastServices.Spotify; Spotify matching fixtures exist but do not substitute WS-B/WS-A. Optional future UrlSubmission BusinessRules migration. |

## 3. Alternatives

- **ALT-001**: Single mega-PR for all gaps — rejected; harder review, slower merge; sequence above allows stopping after PR-3 with high production value.
- **ALT-002**: Cover Searcher only via Discovery integration tests — rejected; BusinessRules at SpotifySearcher isolate floor/filter/hydrate without orchestration noise.
- **ALT-003**: Keep `SpotifyUriExtensionsTests.cs` as legacy unit tests — rejected; audit requires BusinessRules consolidation; migration is S.
- **ALT-004**: Raise coverage floors in `coverage-baseline.json` as part of this plan — deferred until after measured post-PR coverage (out of scope here).

## 4. Dependencies

- **DEP-001**: `RedditPodcastPoster.Episodes.TestSupport` (`DomainTestFixture`) for podcast/episode specimens.
- **DEP-002**: Moq + existing `CapturingSpotifyClientWrapper` pattern in QueryPaginator rules (copy/adapt for Searcher).
- **DEP-003**: SpotifyAPI.Web DTOs (`SimpleEpisode`, `FullEpisode`, `SimpleShow`, `Paging<>`) — construct via helpers in rules files (same as existing paginator rules).
- **DEP-004**: WS-B benefits from WS-C IdResolver clarity but is not blocked.
- **DEP-005**: WS-D Enricher tests depend on mocking `ISpotifyPodcastResolver` and `ISpotifyEpisodeResolver`.
- **DEP-006**: Optional PR-5/PR-6 may wait until PR-1–PR-3 merged without blocking prod confidence.

## 5. Files

### Create

- **FILE-001**: `Class-Libraries/RedditPodcastPoster.PodcastServices.Spotify.Tests/BusinessRules/Search/SpotifySearcherRules.cs` ✅
- **FILE-002**: `Class-Libraries/RedditPodcastPoster.PodcastServices.Spotify.Tests/BusinessRules/Resolvers/SpotifyIdResolverRules.cs` ✅
- **FILE-003**: `Class-Libraries/RedditPodcastPoster.PodcastServices.Spotify.Tests/BusinessRules/SpotifyPodcastServiceMatcherRules.cs` ✅
- **FILE-004**: `Class-Libraries/RedditPodcastPoster.PodcastServices.Spotify.Tests/BusinessRules/Providers/SpotifyEpisodeProviderRules.cs` ✅
- **FILE-005**: `Class-Libraries/RedditPodcastPoster.PodcastServices.Spotify.Tests/BusinessRules/Resolvers/SpotifyPodcastResolverRules.cs` ✅
- **FILE-006**: `Class-Libraries/RedditPodcastPoster.PodcastServices.Spotify.Tests/BusinessRules/Enrichers/SpotifyPodcastEnricherRules.cs` ✅
- **FILE-007**: (optional) `.../BusinessRules/Categorisers/SpotifyUrlCategoriserUrlAuthorityRules.cs` if criteria file should stay focused ✅
- **FILE-008**: (optional) `.../BusinessRules/Finders/SpotifySearchResultFinderPodcastMatchRules.cs` ✅

### Extend

- **FILE-009**: `.../BusinessRules/Categorisers/SpotifyUrlCategoriserRules.cs` (if not using FILE-007) — N/A (used sibling)
- **FILE-010**: `.../BusinessRules/Paginators/SpotifyQueryPaginatorRules.cs` ✅
- **FILE-011**: `.../BusinessRules/Factories/FindSpotifyEpisodeRequestFactoryRules.cs` ✅
- **FILE-012**: `.../BusinessRules/SpotifyEpisodeRetrievalHandlerRules.cs` ✅
- **FILE-013**: `.../BusinessRules/Finders/SearchResultFinderCatalogueWrapperRules.cs` (or FILE-008) — used FILE-008

### Delete

- **FILE-014**: `Class-Libraries/RedditPodcastPoster.PodcastServices.Spotify.Tests/Extensions/SpotifyUriExtensionsTests.cs` (after migration in PR-2) ✅

### Production (read-only for this plan)

- **FILE-015**: `Search/SpotifySearcher.cs`
- **FILE-016**: `Categorisers/SpotifyUrlCategoriser.cs`
- **FILE-017**: `Resolvers/SpotifyIdResolver.cs`, `SpotifyPodcastServiceMatcher.cs`, `Resolvers/SpotifyPodcastResolver.cs`
- **FILE-018**: `Enrichers/SpotifyPodcastEnricher.cs`
- **FILE-019**: `Providers/SpotifyEpisodeProvider.cs`
- **FILE-020**: `Paginators/SpotifyQueryPaginator.cs`
- **FILE-021**: `Factories/FindSpotifyEpisodeRequestFactory.cs`
- **FILE-022**: `Finders/SpotifySearchResultFinder.cs`
- **FILE-023**: `SpotifyEpisodeRetrievalHandler.cs`

## 6. Testing

- **TEST-001**: SpotifySearcherRules — ReleasedSince floor, word-boundary filter, hydrate via GetSeveral (WS-A). ✅
- **TEST-002**: SpotifyUrlCategoriser URL authority Rules — existing URL short-circuit, id resolve hit/miss (WS-B). ✅
- **TEST-003**: SpotifyIdResolverRules + SpotifyPodcastServiceMatcherRules + CleanSpotifyUrl DisplayName Facts (WS-C). ✅
- **TEST-004**: SpotifyEpisodeProviderRules — ReleasedSince post-filter + expensive passthrough (WS-E). ✅
- **TEST-005**: SpotifyPodcastResolverRules + SpotifyPodcastEnricherRules (WS-D). ✅
- **TEST-006**: SpotifyQueryPaginatorRules extras (WS-F, optional). ✅
- **TEST-007**: Factory Create(string); FindMatchingPodcasts; RetrievalHandler Skip→Handled=false (WS-G, optional). ✅
- **TEST-008**: Validation command per PR: `dotnet test Class-Libraries/RedditPodcastPoster.PodcastServices.Spotify.Tests` — **111 passed** (2026-07-15).

## 7. Risks & Assumptions

- **RISK-001**: Searcher/`FullEpisode` DTO construction is verbose — mitigate with small private helpers in the rules class (copy pattern from QueryPaginatorRules).
- **RISK-002**: `GetEpisodeId` empty-string vs null mismatch vs categoriser null-check — characterization may surface KNOWN dead branch; do not fix without separate behavior PR.
- **RISK-003**: EpisodeProvider tests may pull adapter stack dependencies — keep asserts at filter/response level; mock inner provider heavily.
- **RISK-004**: PodcastResolver episode-URL verification path is nested — prefer mocking finder/provider to force branches rather than deep Spotify graph setup.
- **ASSUMPTION-001**: Criteria Resolve / hang / Limit / PaginateAll / EpisodeResolver by-date vs by-length remain covered as verified by audit; this plan does not re-test them.
- **ASSUMPTION-002**: UrlSubmission MatchOtherServices ReleasedSince remains Spotify-mocked at categoriser boundary; URL Resolve coverage belongs in Spotify.Tests (WS-B), not UrlSubmission.
- **ASSUMPTION-003**: No production deploy or AppRequests verification is required for tests-only PRs.

### KNOWN quirks characterized (not fixed)

1. `SpotifyIdResolver.GetEpisodeId` returns empty string (not null) when path has no `/episode/`; `SpotifyUrlCategoriser.Resolve(url)` null-check is dead — proceeds to `FindEpisode` with empty id.
2. `SpotifyPodcastWrapper` constructor accepts `expensiveQueryFound` but never assigns `ExpensiveQueryFound` (always false after `FindPodcast`).

## 8. Related Specifications / Further Reading

- Audit source: agent transcript subagent `21b3e8e5` (Jul 2026 Spotify gap audit)
- `.cursor/rules/unit-tests.mdc` — BusinessRules DisplayName / DomainTestFixture contract
- `plans/episode-domain-refactor/STEP-8-TEST-HARDENING-PLAN.md` — related tests-only sequencing pattern
- Existing specimens: `SpotifyQueryPaginatorRules.cs`, `SpotifyEpisodeRetrievalHandlerRules.cs`, `FindSpotifyEpisodeRequestFactoryRules.cs`, `SpotifyUrlCategoriserRules.cs`
- Production surfaces: Discovery `SearchProvider`; SubmitUrl / curation; Indexer retrieval handler; CLI `AddAudioPodcast`
