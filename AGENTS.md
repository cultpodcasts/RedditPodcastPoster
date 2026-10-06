# Cult Podcasts / RedditPodcastPoster — agent notes

## Auth0 permissions (api-infra)

Azure `HandleRequest` checks JWT **`permissions`** / OAuth **`scope`** via `ClientPrincipal.HasScope` — not ID-token roles. Submit URL: `["curate", "submit"]` on `SubmitUrlController`.

- Cross-repo map: [`website/cultpodcasts/docs/auth0-roles-and-permissions.md`](../../website/cultpodcasts/docs/auth0-roles-and-permissions.md)
- Discovery curation: [`docs/discovery-curation-api.md`](docs/discovery-curation-api.md)
- **Planned epic (not scheduled):** [`docs/catalogue-content-types-epic.md`](docs/catalogue-content-types-epic.md) — separate TvShow/TvShowEpisode, **Film** (made-as-film one-off, cinema or TV; no parent), NewsOrganisation/NewsReport containers + unified search `contentKind` (`Episode | TvShowEpisode | Film | NewsReport`). Phase 0 sign-off 2026-09-23: [ADRs](./docs/adr/README.md), [search storage impact](./docs/catalogue-content-types-search-storage-impact.md). Do not use product name Movie.

## Catalogue migrate CLIs (HARD)

1. **This checkout:** `CatalogueMigrateIdentify` and `CatalogueMigrate` **always refuse `--apply` (exit 2)**. They do not write Cosmos or upload Search. Do not implement a write path to “honour” named consent. Exit 2 is intended.
2. **If writes exist later:** never pass `--apply` unless the user names `--apply` (or “apply CatalogueMigrate…”) **and** production writes **in this conversation**. GATE 5 / “continue” / deploy / merge is **not** that consent. News still needs S-008.

Rule: [`.cursor/rules/catalogue-migrate-cli-dry-run.mdc`](.cursor/rules/catalogue-migrate-cli-dry-run.mdc).

## Unit tests (HARD)

Any agent (Cursor, Codex, Copilot, etc.) editing tests **MUST** follow:

- [`.cursor/rules/unit-tests-enforcement.mdc`](.cursor/rules/unit-tests-enforcement.mdc) (always-on summary)
- [`.cursor/rules/unit-tests.mdc`](.cursor/rules/unit-tests.mdc) (full contract)

Mechanical check (local + CI):

```powershell
pwsh ./scripts/assert-unit-test-guardrails.ps1 -GitChanged
# or against main:
pwsh ./scripts/assert-unit-test-guardrails.ps1 -GitChanged -BaseRef origin/main
```

Cursor also runs this via `.cursor/hooks.json` on `stop` / `afterFileEdit`.

## Streaming submit orchestration (contracts)

Cross-repo streaming ingest contract (membership `service`, prepare/submit): [`docs/streaming-submit-orchestration.md`](docs/streaming-submit-orchestration.md). JSON copy under `docs/contracts/` must match Api fixture — `pwsh ./scripts/assert-streaming-submit-contract-copy.ps1`.

## CQRS (HARD)

A command changes state and returns an acknowledgement (empty 202) or a command outcome. It does not return the resource read model. A query returns the read model and changes nothing.

After an acknowledgement, the client GETs the resource and binds that body. A write that returns the resource, or a query that changes state, is a CQRS breach. A command result carries status and command details, not the saved aggregate.

Authoritative: [`Cloud/Api/architecture.md`](Cloud/Api/architecture.md) § CQRS.

## Episode language (HARD)

`Episode.Language` **null = English**. Never read-time coalesce to `Podcast.Language`.

Podcast API default-language changes must use `ApplyPodcastDefaultLanguageChange(previous, new)` —
update only episodes that still follow the **previous** default. Do **not** use
`inheritLanguageIfUnset: true` for that path (it treats English null as unset).

Authoritative: [docs/episode-language.md](docs/episode-language.md) ·
[`EpisodeLanguageResolution`](Class-Libraries/RedditPodcastPoster.Models/Episodes/EpisodeLanguageResolution.cs).

## Catalogue & playlist pagination (HARD for related changes)

Before changing Spotify paginators, YouTube playlist walks, expensive-query flags,
`PlaylistOrder`, or `SkipExpensive*` gates, read:

- [docs/catalogue-pagination.md](docs/catalogue-pagination.md) — Spotify + YouTube + Apple cold-start design, caps, flag lifecycle, test matrix
- [docs/youtube-playlist-order.md](docs/youtube-playlist-order.md) — YouTube Arbitrary / curated depth

Keep circuit-breaker and flag-flip **log message prefixes** stable (App Insights keys off them).
Every behaviour change in those areas **MUST** ship with a `BusinessRules/**` test whose
`DisplayName` states the rule.

## Domain conventions

- Search documents store compact service identifiers and short key names. The UI reconstructs URLs. Call the type `CompactSearchRecord`. Do not add `V2` or `Legacy` names.
- Do not add Episode members for compact IDs. Use the existing Spotify, YouTube, and Apple IDs. Derive the Apple episode slug from the Apple URL.
- Cosmos container name is `LookUps` (not `knownTerms`). `KnownTerms` and elimination terms are items in `LookUps`. Infrastructure includes a `PushSubscriptions` container.
- Use detached `Podcast` / `Episode` models and the `PodcastEpisode` pair. Canonical repositories are the split-container implementations in `RedditPodcastPoster.Persistence`. Do not reintroduce `Persistence.Legacy` or embedded-container repositories.
- Episode ID is globally unique. Azure Search indexing does not use soft-delete.
- `podcast.Removed` is the removal flag. Do not trust episode-level `podcastRemoved` when deciding whether an episode can be posted.
- Container factories are explicit: `CreatePodcastsContainer()` and `CreateEpisodesContainer()`.
- Cosmos settings bind to the `cosmosdb` configuration section.
- `docs/migration` is historical. When persistence architecture changes, update `docs/migration/README.md` and `docs/post-migration/README.md`. Keep `docs/post-migration/cost-analysis.md` current and name the next step.
- Do not change Reddit posting or Twitter posting while doing cost-reduction work unless that change is requested.
- Indexer cost investigation must cover every activity in the orchestration, not only Indexer. Cost-probe logs are Warning or above. For the Indexer cost probe, log `updateMs` only.
- Before judging an Azure Search indexer rerun, wait until the newly triggered run has started.
- In duplicate-episode verification, do not modify backup files. A canonical episode must not be ignored or removed when any deleted duplicate had ignored or removed set to false.
- Azure Functions Flex Consumption `instanceMemoryMB` is 512, 2048, or 4096.
- Memory probing goes through `IMemoryProbeOrchestrator`: `Start(nameof(Class))` and `End()`. `MemoryProbeOptions` decides whether a session is created.

## Cursor Cloud specific instructions

Multi-repo workspace: this repo is at `/agent/repos/redditpodcastposter` alongside `/agent/repos/api`
and `/agent/repos/website`. Uses the **.NET 10 SDK** (installed at `~/.dotnet`; login shells get it
from `~/.bashrc`, else prepend `$HOME/.dotnet` and set `DOTNET_ROOT=$HOME/.dotnet`). `pwsh` is
available for the guardrail scripts.

- **HARD**: Never use the `pin-github-identity` skill (or `GH_TOKEN` process-override from `gh auth token --user …`) on Cursor Cloud / Cloud Agent VMs; GitHub identity is whatever Cursor assigned to the run.
- **Vendored Reddit.NET removed (unused)**: the old `Third-Party/sirkris-Reddit.NET-1.5.3` project is no
  longer used and its `RedditPodcastPoster.slnx` entry has been dropped. No repo code references
  `Reddit.NET` (there is no `using Reddit;`). Do **not** re-add a `Third-Party/**` project or a solution
  reference to it. (Historical note: it was a maintainer fork never committed to git, so any solution
  reference to it breaks `dotnet restore` on a clean checkout.)
- **`Discover.slnf` does not load on Linux**: it lists projects with Windows backslash paths, which the
  slnx parser rejects. Build/test the full `RedditPodcastPoster.slnx` instead.
- **Build / test** (matches CI, no cloud services needed — unit tests are fully mocked):
  `dotnet build RedditPodcastPoster.slnx -c Release` then
  `dotnet test RedditPodcastPoster.slnx -c Release --no-build` (~1762 tests). The startup update script
  pre-runs `dotnet restore` when the vendored project exists.
- **Running the Functions** (`func start` in `Cloud/Api|Discovery|Indexer`) additionally needs Azure
  Functions Core Tools + Azurite + Cosmos DB + Auth0 + provider secrets — none are provisioned in the
  local snapshot, so default to build + unit tests for local verification.