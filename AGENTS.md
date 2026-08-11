# Cult Podcasts / RedditPodcastPoster — agent notes

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

`Episode.Language` **null = English**. Never read-time coalesce to `Podcast.Language`
(`episode.Language ?? podcast.Language` or the IsNullOrWhiteSpace ternary). That corrupts
English subject search and enrichment for English episodes on non-English shows.

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