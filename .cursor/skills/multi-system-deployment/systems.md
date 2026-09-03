# Multi-system deployment — this org

Workspace roots: `Api`, `website`, `cultpodcasts/RedditPodcastPoster`. Git for website is the **parent** of `cultpodcasts/`.

## Local ports (not production)

| Port | Service |
| --- | --- |
| 8788 | Website Pages local |
| 8787 | Api Worker local |
| 7071 | Azure Functions API |
| 4200 | Website `ng serve` |

## Functions script-deploy

From RedditPodcastPoster, order is mandatory. Pass all four Azure args so the script is non-interactive:

1. `scripts/deploy-indexer.ps1` → `indexer-infra` / `indexer-deployment`
2. `scripts/deploy-discover.ps1` → `discover-infra` / `discovery-deployment`
3. `scripts/deploy-api.ps1` → `api-infra` / `api-deployment`

Shared: `-ResourceGroup AutomatedInfra` `-StorageAccount cultpodcastsstg` `-Confirm:$false`

Resume mid-sequence from the **next** app; do not restart Indexer if it already succeeded this conversation.

Blob truth (account `cultpodcastsstg`, blob `released-package.zip`):

- indexer → container `indexer-deployment`
- discover → `discovery-deployment`
- api → `api-deployment`

## CLIs

```powershell
# all (except ThrowawayConsole)
.\scripts\publish-console-apps.ps1 -Confirm:$false

# subset after a failed parallel publish
.\scripts\publish-console-apps.ps1 -App CosmosDbDownloader,PublishR2 -Sequential -NoRestore -Confirm:$false
```

Output: `artifacts/tools/`. Parallel publish can fail with `MSBuild server unavailable`; retry sequential subset.

`PublishR2 lookups` = languages + people + search-suggestions + subjects, then flairs. Not `all`, not homepage, not feed.

## Cosmos dump

```powershell
$dest = "C:\Users\jonbr\source\repos\CultPodcasts-PrivateDatabase\yyyy-MM-dd"
if (Test-Path -LiteralPath $dest) { throw "Folder already exists — will not overwrite: $dest" }
New-Item -ItemType Directory -Path $dest | Out-Null
Set-Location -LiteralPath $dest
& "<repo>\artifacts\tools\CosmosDbDownloader.exe"
```

No `--overwrite`. Default containers include people when the freeze-branch tool supports it. Activities are not downloaded.

## Version bumps on PRs

- Api: bump `package.json` + `package-lock.json` (semver patch unless the change warrants more) before opening/pushing.
- Website: bump `cultpodcasts/package.json` + lockfile the same way.
- Website tests before push: `npm run test:all` from `cultpodcasts/` (do not `--no-verify`).

## Website no-merge

Do not `gh pr merge` unless the user explicitly says to merge in this conversation. “Complete the PR” in a freeze plan means they are the merge actor unless they told the agent to complete it.

## Secrets

Parity script (Api): `pwsh ./scripts/assert-secrets-example-parity.ps1`. Docs: Api `docs/worker-secrets.md`.
