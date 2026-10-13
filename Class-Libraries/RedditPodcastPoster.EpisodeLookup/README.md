# RedditPodcastPoster.EpisodeLookup — the episode lookup clearing-house

**This project is shared by many use cases.** Every Spotify and Apple *single-episode* lookup in the system
goes through it. If you change it or fix a bug in it, think about **every** use case below, and **write a
business-rules test first** (in `RedditPodcastPoster.EpisodeLookup.BusinessRules.Tests`, plus a cross-platform case in
`PodcastServices.CatalogueContract.Tests` when the platforms are involved).

## Use cases (callers)

| Use case (`EpisodeLookupFlow`) | Entry point | Callers |
|---|---|---|
| `Index` | `EpisodeLookupUseCases.ForIndex` | `PodcastUpdater.Update`: Cloud Indexer, Api `PodcastIndexService`, Index CLI |
| `Submit` (default) | `EpisodeLookupUseCases.ForSubmit` / unmarked context | URL submission (`UrlSubmission` `PodcastService`), `SpotifyUrlCategoriser`, `AppleUrlCategoriser`, ImageUpdater |
| `Discovery` | `EpisodeLookupUseCases.ForDiscovery` | Discovery `SpotifyEnricher`, `AppleEnricher` |
| `Enrich` | `EpisodeLookupUseCases.ForEnrich` | `EnrichExistingEpisodesFromPodcastServices` CLI |

`EpisodeLookupFlow` and `CatalogueLookup` (the lookup input) live in `RedditPodcastPoster.Episodes`
(`Matching/Candidates`), next to the shared matcher, because `IndexingContext` and the matcher need them.

Platform adapters stay thin: `SpotifyEpisodeResolver` and `AppleEpisodeResolver` only implement
`ICatalogueCandidateSource<T>` (fetch a newest-first page, search by title, probe the show size, map to `Episode`).
They contain no strategy logic.

## How a lookup runs

1. Known episode id → fetched directly by the resolver (a stale id falls through).
2. `CatalogueLookupExecutor` gets the show size: the podcast's `spotifyEpisodeCount` / `appleEpisodeCount`,
   or one cheap probe (Spotify `Paging.Total` on page 0; Apple iTunes `trackCount`).
3. `EpisodeLookupPlanner` picks the chain: no show id → title search; known small show (≤ `FullShowReadMaxEpisodes`)
   → full read; otherwise the chain declared in **`Planning/EpisodeLookupChains.cs`** for (use case, release age).
4. The executor runs the steps (pages read once and reused, ids tried once, each read bounded by date and/or cap)
   and logs one line per lookup:
   `Catalogue episode lookup: platform=… flow=… plan=… stage=… show-episodes=… pages-read=…`
   (`stage` is the strategy that found the episode, or `miss`).
5. Every HTTP call is wrapped by `CatalogueRequestGuard` (`RequestTimeout`, default 10 s); when it fires it logs
   `Catalogue request timeout guard fired:` and the lookup moves on (clean miss, never an exception).

## How to adjust it

| Change | Where | Test to add first |
|---|---|---|
| A threshold, cap or timeout | `episodeLookup` config section / `Planning/EpisodeLookupOptions.cs` defaults (validated at startup) | `EpisodeLookupOptionsTests` + planner case |
| Reorder or add a strategy in a use case | one row in `Planning/EpisodeLookupChains.cs` | `EpisodeLookupPlannerRules.Plan_matrix` row |
| A new strategy | `CatalogueLookupStrategy` + `EpisodeLookupPlanner.ToStep` + executor | planner + executor rules + contract case |
| A new use case | `EpisodeLookupFlow` value, `EpisodeLookupUseCases.ForX`, rows in `EpisodeLookupChains`, call it from the caller | `Every_flow_declares_its_chains` (fails until rows exist) + matrix rows + caller rule |
| A new platform | `CataloguePlatforms`, a walk cap in options, an `ICatalogueCandidateSource` adapter | contract tests for the new platform |

Options (`episodeLookup`): `FullShowReadMaxEpisodes` 200, `FullShowReadMaxPages` 20, `RecentWindow` 14 d, `IndexMaxPages` 20,
`SpotifyBoundedWalkPages` 4, `AppleBoundedWalkPages` 5, `RequestTimeout` 10 s.

Use the App Insights `Catalogue episode lookup:` lines (group by `flow`, `stage`, `pages-read`) to tune with data.
Function apps cap `RedditPodcastPoster` at Warning, so these Information lines need the
`RedditPodcastPoster.EpisodeLookup` = Information override in `Cloud/Azure/ApplicationLogLevels.cs` and in every
host's `host.json` (pinned by `FunctionHost.Tests` `EpisodeLookupLogLevelRules`). A new function app that calls
`AddEpisodeLookup` needs the same `host.json` entry and a row in that test. CLIs log at Information by default.
KQL: [`docs/indexing-app-insights-queries.md`](../../docs/indexing-app-insights-queries.md) §11.

More background: [`docs/catalogue-pagination.md`](../../docs/catalogue-pagination.md) ("Episode lookup planner").
