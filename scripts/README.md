Deploy scripts: see [docs/deployment.md](../docs/deployment.md) for architecture, guardrails, and usage.

Script tests: offline Pester tests live in [tests/](tests/) (fake `gh`, no network). Run `Invoke-Pester ./scripts/tests` (Pester 5+), or `pwsh -c 'Invoke-Pester ./scripts/tests'` from bash; not yet wired into CI (the proposed `dotnet.yml` step is in the body of [#1224](https://github.com/cultpodcasts/RedditPodcastPoster/pull/1224)).
