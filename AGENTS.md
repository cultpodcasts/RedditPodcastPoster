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

## Catalogue & playlist pagination (HARD for related changes)

Before changing Spotify paginators, YouTube playlist walks, expensive-query flags,
`PlaylistOrder`, or `SkipExpensive*` gates, read:

- [docs/catalogue-pagination.md](docs/catalogue-pagination.md) — Spotify + YouTube cold-start design, caps, flag lifecycle, test matrix
- [docs/youtube-playlist-order.md](docs/youtube-playlist-order.md) — YouTube Arbitrary / curated depth

Keep circuit-breaker and flag-flip **log message prefixes** stable (App Insights keys off them).
Every behaviour change in those areas **MUST** ship with a `BusinessRules/**` test whose
`DisplayName` states the rule.