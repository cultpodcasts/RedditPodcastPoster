# This org (Cult Podcasts)

Use when the PR is under `cultpodcasts/*`. Other remotes: discover the repo/org project with `gh project list` and use that board’s first actionable column (not a fictional Backlog).

## Board

- Project: [cultpodcasts features](https://github.com/orgs/cultpodcasts/projects/1)
- Columns: **Todo** / **In Progress** / **Done** (no Backlog)
- New leftover issues: **Todo**

```text
gh issue create --repo <owner/repo> --title "…" --body "Deferred from PR <n> review. …"
gh project item-add 1 --owner cultpodcasts --url <issue-url>
```

Set status to Todo if the CLI requires a field update after add.

## Freeze this org usually has

- Do not merge website PRs unless the user said merge in this conversation.
- Do not `wrangler deploy` / Pages deploy unless the user named that exact deploy.
- RedditPodcastPoster: unit-tests.mdc if the PR touches tests; no episode Cosmos writes without explicit apply.
- Api PRs: bump `package.json` + lock patch when shipping Worker code.

Always-on `.cursor/rules` beat this file if they conflict.
