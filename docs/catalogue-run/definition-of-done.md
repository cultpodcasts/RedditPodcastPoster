# Catalogue content types — definition of done

This is the close-out checklist for the TV show / Film / News migration epic. **Nothing here has been run.** Every production step needs Jon to name it in the conversation. For `CatalogueMigrate --apply` and `--rollback --apply`, he must also type the consent phrase himself. Agents never supply `--consent` (see [`.cursor/rules/catalogue-migrate-cli-dry-run.mdc`](../../.cursor/rules/catalogue-migrate-cli-dry-run.mdc)).

Commands are PowerShell from the RedditPodcastPoster repo root on Jon's machine, with production user-secrets. `CatalogueMigrate` and `CatalogueMigrateIdentify` mean the published exes in `artifacts\tools` (on PATH).

## A. Code (on PR #1049 unless noted)

- [x] Identify: newspapers, named TV, channel programmes, and the TMDB/TheTVDB authority preview (dry-run).
- [x] S-008 allowlist, frozen 2 Oct 2026. **Curator removals still pending.**
- [x] `CatalogueMigrate` writer. It runs News → Film → TV, keeps ids, swaps search, writes evidence per batch, can resume, and has a rollback. The consent gate and the S-008 apply gate are in place. Auto-indexed podcasts are blocked.
- [ ] Website and Api Worker follow-ups, listed in the PR description (consumer audit).
- [ ] Decide on indexing and social posting for migrated parents (see "Decisions" in the PR).

## B. Deploy (only when the human names each deploy)

```powershell
git switch main; git pull
dotnet build RedditPodcastPoster.slnx -c Release
.\scripts\publish-console-apps.ps1 -App CatalogueMigrate,CatalogueMigrateIdentify,CosmosDbDownloader,CultPodcasts.DatabasePublisher
.\scripts\deploy-indexer.ps1
.\scripts\deploy-discover.ps1
.\scripts\deploy-api.ps1
```

Check: `CatalogueMigrate --version` shows the merged commit.

## C. Backup (before any apply)

```powershell
$stamp = Get-Date -Format yyyyMMdd-HHmm
New-Item -ItemType Directory "..\backups\catalogue-$stamp" | Out-Null
Push-Location "..\backups\catalogue-$stamp"
CosmosDbDownloader
Pop-Location
```

Check: `podcasts/`, `episodes/`, `film/`, `tvshow/`, `tvshowepisode/`, `newsorganisation/` and `newsreport/` exist, and the podcast and episode file counts match the Cosmos counts.

## D. Dry-run and review (no writes)

```powershell
CatalogueMigrateIdentify | Tee-Object identify-$stamp.log
CatalogueMigrate --kind NewsReport | Tee-Object news-plan-$stamp.log
CatalogueMigrate --kind TvShowEpisode | Tee-Object tv-plan-$stamp.log
# Film is per podcast:
CatalogueMigrate --kind Film --podcast-id <podcast-guid> | Tee-Object film-plan-<podcast-guid>.log
```

Review each `Catalogue migrate plan` line. `apply=True` lines will move. `block=` lines will not move, and each one says why: not on S-008, partial news, auto-indexed, or a Film with other episodes.

## E. Apply (S-007 order: News, then Film, then TV)

Start with a small batch, spot-check it, then do the rest. Each command writes `catalogue-migrate-evidence\<runId>-<kind>-batch-NNN.jsonl`.

```powershell
CatalogueMigrate --kind NewsReport --apply --limit 5 --consent "I approve CatalogueMigrate --kind NewsReport production writes"
# spot-check (section F), then:
CatalogueMigrate --kind NewsReport --apply --batch-size 25 --consent "I approve CatalogueMigrate --kind NewsReport production writes"

CatalogueMigrate --kind Film --podcast-id <podcast-guid> --apply --consent "I approve CatalogueMigrate --kind Film production writes"

CatalogueMigrate --kind TvShowEpisode --apply --limit 5 --consent "I approve CatalogueMigrate --kind TvShowEpisode production writes"
CatalogueMigrate --kind TvShowEpisode --apply --batch-size 25 --consent "I approve CatalogueMigrate --kind TvShowEpisode production writes"
```

Exit codes:
- `0`: done.
- `2`: refused (no consent, or the wrong phrase).
- `3`: one podcast failed and the run stopped. The source rows are kept. Re-run the same command to resume, or roll that batch back.

Rollback of one batch (dry-run first):

```powershell
CatalogueMigrate --rollback catalogue-migrate-evidence\<file>.jsonl
CatalogueMigrate --rollback catalogue-migrate-evidence\<file>.jsonl --apply --consent "I approve CatalogueMigrate rollback of <file>.jsonl in production"
```

## F. Spot-check after each kind

- [ ] `https://cultpodcasts.com/news/<org-slug>` lists the moved reports. The old `/podcast/<name>/<shortId>` link redirects (`redirectMovedKind`).
- [ ] Search shows `contentKind` NewsReport / Film / TvShowEpisode hits. The kind facet appears.
- [ ] `/film/<slug>/<shortId>` renders. `/tv/<show-slug>` lists its episodes.
- [ ] A share link for a moved item opens the new route.
- [ ] The PublicDatabase preview includes the new kinds:

  ```powershell
  Push-Location ..\..\PublicDatabase; pwsh .\update.ps1; Pop-Location
  ```

  Run it from the PublicDatabase root. With no `-Apply`, this is a preview only. `-Apply` is a separate, named step that Jon reviews before any commit.

## G. Drop the Episode dual-key / bridges (GATE 5 step 5)

Only after F is green for all three kinds and the audit in [gate-5.md](./gate-5.md) is still green. This is a separate PR: remove the `podcastRemoved`/`releaseSort` dual-key predicates and the deserialize bridges, then redeploy:

```powershell
.\scripts\deploy-indexer.ps1; .\scripts\deploy-discover.ps1; .\scripts\deploy-api.ps1
```

## H. Close

- [ ] Worker and Site follow-ups deployed (each named).
- [ ] Old per-kind metrics deprecated.
- [ ] `docs/catalogue-run/README.md` marks GATE 5 done, and the epic doc status reads "Closed".
