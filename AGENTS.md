# Cult Podcasts / RedditPodcastPoster — agent notes

## Auth0 permissions (api-infra)

Azure `HandleRequest` checks JWT **`permissions`** / OAuth **`scope`** via `ClientPrincipal.HasScope` — not ID-token roles. Submit URL: `["curate", "submit"]` on `SubmitUrlController`.

- Cross-repo map: [`website/cultpodcasts/docs/auth0-roles-and-permissions.md`](../../website/cultpodcasts/docs/auth0-roles-and-permissions.md)
- Discovery curation: [`docs/discovery-curation-api.md`](docs/discovery-curation-api.md)
- **Planned epic (not scheduled):** [`docs/catalogue-content-types-epic.md`](docs/catalogue-content-types-epic.md) — separate TvShow/TvShowEpisode, Movie, NewsOrganisation/NewsReport containers + unified search facets. Phase 0: [ADRs](./docs/adr/README.md), [search storage impact](./docs/catalogue-content-types-search-storage-impact.md)

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

## Cursor Cloud specific instructions

Multi-repo workspace: this repo is at `/agent/repos/redditpodcastposter` alongside `/agent/repos/api`
and `/agent/repos/website`. Uses the **.NET 10 SDK** (installed at `~/.dotnet`; login shells get it
from `~/.bashrc`, else prepend `$HOME/.dotnet` and set `DOTNET_ROOT=$HOME/.dotnet`). `pwsh` is
available for the guardrail scripts.

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