<!-- pragma: allowlist secret -->
# Ops runbook: service catalog + nested ids

Human-in-the-middle rollout. **You** decide each gate. Website and Api Worker ship by **completing those PRs** — this agent does not run Wrangler/Pages deploy. An agent must not write production Cosmos, must not republish the feed, and must not run `PublishR2` against production R2 unless you name that action.

Companion docs: [deploy plan + diagram](episode-services-deploy-plan.md) · [risk](episode-services-risk.md) · [mechanics](episode-services-migration.md) · [canvas](episode-services-canvas.md).

**Out of scope this exercise:** Phase 3 (stop dual-write / strip `urls`). Do not combine with a language job.

No new Worker secrets. Ignore GitHub Actions as a go/no-go.

---

## Roles and freeze

| You | Agent / operator |
| --- | --- |
| Inspect Cosmos dump; name the next step; merge PRs; any `apply: true`; feed publish | After you name a step: build CLIs, dump Cosmos into a **new** PrivateDatabase dated folder, later backups/deploys as named. **Never** write `2026-08-15` or `--overwrite` existing JSON |

**Publish freeze** starts when `api-infra` (publisher) is on this code and ends only after the **new site is live** and you explicitly publish. Until then:

- Do **not** use admin “publish feed”
- Do **not** edit an episode released in the last 7 days (that path also republishes)
- If deploy day is **Sunday or Monday UTC**, wait or keep someone watching 00:00–00:20 UTC (publisher refresh window)

If a publish slips out before the new site: restore the R2 feed object from the backup in step 0. Visitors will see missing listen/watch icons until that restore or the new site is live.

---

## When to merge PRs

Three PRs, one branch name (`cursor/episode-service-links-18b4`):

| Order | Repo | Typical PR | Merge when | Do not merge when |
| --- | --- | --- | --- | --- |
| 1 | **website** | #481 | After step 0 backups. Complete/merge **first**. Production site ships from that PR. | Before R2 snapshot exists |
| 2 | **Api** (Worker) | #141 | After production site is live and step 1 checks pass. Complete/merge; production Worker ships from that PR (top-level `api`). | Before the new site is serving production |
| 3 | **Functions** (this repo) | #966 | After Worker production is live and step 1 feed `GET` still matches leftover-field R2. Merge **last**. Then **script-deploy** `api-infra` / `indexer-infra` (PR complete does **not** deploy Functions). | Before site + Worker are in production |

**Why this order.** Merge to `main` is how this org ships. `deploy.yml` on the Functions repo can put writers live when Actions is healthy. If you merge Functions first, the next successful Functions deploy (CI or script) can publish a new-shape feed while production still runs the **old** site. Old site cannot read `ids`+`services` only.

**Rules**

- [ ] Do **not** merge all three “because they are one feature.” Merge is a release step, not a paperwork step.
- [ ] Do **not** merge Functions to `main` “to keep the branch tidy” and then wait to deploy. Someone else, or CI, can deploy `main` without you.
- [ ] Preview Worker may already be on the branch via Git. Production Worker is **completing Api PR #141**, not `wrangler deploy` from this agent.
- [ ] Leave the Functions PR **open** until step 2. After merge, **script-deploy** Functions the same day (publish freeze). GitHub Actions is not go/no-go.
- [ ] A later Phase 3 / F4-fix PR is **not** part of this merge train. Merge that only after this rollout is done.

**If you already merged Functions first**

Do not deploy Functions. Do not publish the feed. Complete site + Worker PRs immediately, then continue from step 2. If Functions already deployed and R2 changed, restore feed backup A first.

---

## 0. Before any production deploy

### 0.1 Confirm what you are shipping

- [ ] Functions PR (this repo) includes dual-write + new public/feed JSON (`ids` + `services` only)
- [ ] Api Worker PR: OpenAPI allows leftover named URL fields **and** `ids`/`services`; Worker still returns R2 **bytes unchanged**
- [ ] Site PR: helpers read `services` → leftover named fields → `ids` / search compact ids / `svc`
- [ ] Known bug **F4** still open: clearing Spotify/Apple/YouTube in the form can be undone by `SyncLegacy`. Treat “clear a link” as a soak item; do not rely on clear until a follow-up fix ships
- [ ] Local: `dotnet test` on this repo; site + Api unit tests green on their branches

### 0.2 Backups (do these first; keep until Phase 2 is done or abandoned)

**Location:** `C:\Users\jonbr\source\repos\CultPodcasts-PrivateDatabase`. Layout is one dated folder per dump (`YYYY-MM-DD` → `episode\`, `podcast\`, `lookups\`, …).

**HARD: do not impact existing backups.** Do not write into `2026-08-15` (or any other folder that already exists). Do not `--overwrite`. Do not delete, rename, or git-commit those trees unless you explicitly ask. New dump = **new sibling folder** only (`2026-08-28` if that directory does not yet exist).

#### 0pre. Build CLIs

From this repo on `cursor/episode-service-links-18b4`:

```powershell
.\scripts\publish-console-apps.ps1 -Confirm:$false
```

Publishes to `RedditPodcastPoster\artifacts\tools\` (not PrivateDatabase). Use those exes for 0a / 1c.

#### 0a. Cosmos dump — then **hard stop**

```powershell
$dest = "C:\Users\jonbr\source\repos\CultPodcasts-PrivateDatabase\$(Get-Date -Format 'yyyy-MM-dd')"
if (Test-Path $dest) { throw "Folder already exists — pick a new name, do not overwrite: $dest" }
New-Item -ItemType Directory -Path $dest | Out-Null
Set-Location $dest
& "<repo>\artifacts\tools\CosmosDbDownloader.exe"
```

No `--overwrite`. Activities are not downloaded (tool does not include them).

## 0a. Cosmos backup — hard stop

Same as 0.2 §0a. Agent stops after the new dated folder is written. You inspect it. `2026-08-15` must be untouched.

**HITL:** agent **stops**. You open the new folder, confirm `2026-08-15` is untouched, spot-check episode JSON (`urls`, `lang`). Next step only when you say so (e.g. “Cosmos backup looks good — continue 0b”).

**B. Cosmos stored items** — the 0a dump (all downloader containers, overwrite off), not a write into an old dated folder.

**A. Published feed (R2)** — copy the live feed object to a dated local file **and** a second R2 key you will not overwrite (example name `content/feed.bak-YYYYMMDD`). This is the rollback for F1. Confirm leftover `spotify` / `apple` / `youtube` (or `urls`). Do this in **0b**, after 0a pass.

**C. Search index schema** — Azure portal: export or screenshot field list (`spotifyId`, `youtubeId`, `appleId`, `podcastAppleId`, `bbc`, `internetArchive`, `image`). You will **add** `svc` later, never drop these.

**D. Record “what is on prod”** (blob `lastModified`, not GitHub):

```powershell
az storage blob show --account-name <stg> --container-name api-deployment --name released-package.zip --auth-mode login --query properties.lastModified
az storage blob show --account-name <stg> --container-name indexer-deployment --name released-package.zip --auth-mode login --query properties.lastModified
```

Note current site and Worker release (Pages / Worker dashboard).

### 0.3 Gate — human

**0a (first):** you opened the **new** dated PrivateDatabase folder; `2026-08-15` is untouched.

- [ ] Snapshot A + A2 exist (0b) and you opened at least one file from each
- [ ] You accept: no feed publish until step 4
- [ ] You accept: lookup republish (step 1c) uses this branch’s local `PublishR2`, not PATH tools / not `PublishR2 all`
- [ ] You accept: no Cosmos `apply: true` until step 6
- [ ] You accept: merge order website → Api → Functions (Functions PR stays open until step 2)

---

## 1. Readers first (site + Api Worker)

Old feed stays on R2. New site can read it. Old site cannot read a new feed — that is why writers come later.

1. **Complete the website PR (#481) to `main`.** Production Pages ships from that. Do not `wrangler pages deploy` / `npm run deploy`. Do not drop leftover fallbacks.
2. **After** production site checks below pass: **complete the Api PR (#141) to `main`.** Production Worker ships from that (top-level `api`). Do not `wrangler deploy` / `npm run deploy`.
3. No new secrets. Parity script only if you added keys (this change should not).
4. Leave the Functions PR **unmerged**.

### Checks (human)

- [ ] Production site still shows listen/watch on the feed (Spotify/YouTube/Apple and a BBC row if you have one)
- [ ] Search result still opens a platform URL
- [ ] Public episode detail / saved item still has outbound links
- [ ] `GET` feed from the Worker is **byte-identical** to the R2 backup (or same leftover fields). Worker must not reshape.

**Stop** if links are missing. Do not deploy Functions yet.

---

## 1c. Republish R2 lookup JSON from this branch (local build)

After production **site + Worker** are live and feed GET still matches leftover-field R2. **Before** merging Functions.

These Worker keys are Cosmos-derived catalogs, not the episode feed:

| R2 key | Source | CLI |
| --- | --- | --- |
| `languages` | LookUps `SupportedLanguagesConfig` | `PublishR2 languages` |
| `people` | People container | `PublishR2 people` |
| `search-suggestions` | generated from Cosmos | `PublishR2 search-suggestions` |
| `subjects` | Subjects container | `PublishR2 subjects` |
| `flairs` | Subject flair fields | `PublishR2 flairs` |

**Out of this step:** `homepage`, `homepage-ssr`, `discovery-info`, the **feed**. Do not `PublishR2 all` (that includes homepage). Do not admin-publish the feed.

Build from **this branch**, not an older published exe:

```powershell
cd <RedditPodcastPoster>
git checkout cursor/episode-service-links-18b4
dotnet run --project Console-Apps/PublishR2 -- lookups
```

`lookups` runs languages + people + search-suggestions + subjects, then flairs. It does **not** write homepage.

### Checks (human)

- [ ] Command was `dotnet run --project Console-Apps/PublishR2 -- lookups` on this branch (not PATH `PublishR2`)
- [ ] Worker `GET` `/languages`, `/people`, `/subjects`, `/flairs`, `/search-suggestions` still 200
- [ ] Feed `GET` still matches backup A (etag/shape)
- [ ] Homepage object unchanged vs step 0

**Stop** if feed or homepage changed. Restore those keys from backups. Do not merge Functions.

**Rollback:** put A2 files back on the matching R2 keys.

---

## 2. Writers (Azure Functions) under publish freeze

1. **Merge the Functions PR to `main` only now** (site + Worker already production).
2. Same day, deploy code that **dual-writes** Cosmos and **would** emit new feed JSON if anyone publishes. Start/confirm the publish freeze before the deploy finishes.

```powershell
az login
.\scripts\deploy-api.ps1
.\scripts\deploy-indexer.ps1
```

Deploy the third function app too if that host saves the same item type (shared model). Use `-WhatIf` first if you want a dry package.

### Checks (human)

- [ ] `api-infra` / `indexer-infra` blob `lastModified` is **now** (this release)
- [ ] Function list still looks normal (`az functionapp function list -g AutomatedInfra -n api-infra -o table`)
- [ ] **No** feed publish ran (R2 object timestamp / etag still matches backup A)
- [ ] Pick one item you will **not** edit in the 7-day window. In Cosmos Data Explorer, open **raw JSON** (not a hydrated app view). Confirm `urls` still present. `services` / `ids` may be absent until that row is saved or backfilled — that is OK

**Stop** if R2 feed already changed. Restore backup A onto the live feed key before continuing.

---

## 3. Soak (writers on, old feed still live)

Keep the freeze. Exercise paths that **Save** without publishing the full feed if you can; skip 7-day-window edits.

- [ ] Admin GET an item: form still shows Spotify/Apple/YouTube slots and extra URL rows
- [ ] Change a **description** (or another non-URL field) on an item **older than 7 days**; save; reload raw JSON
  - Must still have `urls.*` and top-level ids
  - Should now also have `services` and `ids` (additive)
  - `lang` unchanged (null still English)
- [ ] Optional: add a Vimeo/other URL; confirm `services.<key>.url` and that `urls` for the three defaults did not vanish
- [ ] Known F4: try clear Spotify on a dual-written row. If the URL comes back, that is the known bug — do not “fix” by running a bulk job
- [ ] Tweet or Bluesky on a row that already has `urls.youtube` or `urls.spotify` — one link still posts
- [ ] Indexer/search of that saved row still has compact ids

**Stop** if any `urls.*`, top-level id, `lang`, or title disappeared on the raw document. Restore that item from backup B. Do not proceed to publish or backfill.

---

## 4. Republish the feed (only after site + soak)

Human trigger: admin publish (or one intentional 7-day edit).

### Immediately after

- [ ] Download live feed JSON. Expect `ids` + `services`. Leftover named URL fields may be **absent**
- [ ] Production site: cards, hero, search, detail — listen/watch still work (helpers use `services` / `ids`)
- [ ] Hard-refresh / another browser (no stale R2)
- [ ] Keep site leftover fallbacks in code until you have soaked this feed for a few days

**Rollback:** put backup A back on the live R2 key. Site (new) still reads leftover fields. Do **not** revert Functions unless Cosmos looks wrong (it should not from publish alone).

---

## 5. Search field `svc` (additive, later the same week)

- [ ] Azure Search: **add** retrievable string `svc`. Do not delete `spotifyId` / `youtubeId` / `appleId` / `podcastAppleId` / `bbc` / `internetArchive` / `image`
- [ ] Confirm Functions that write `svc` are already live (step 2)
- [ ] Run indexer / reindex
- [ ] Search UI: a Sounds/Vimeo/Netflix (or similar) row shows the extra destination if `svc` is populated
- [ ] Spotify/YouTube/Apple still resolve from compact ids

**Rollback:** leave `svc` unused; do not drop the new field in a panic (harmless if empty). Never recreate the index to “undo.”

---

## 6. Cosmos backfill (separate approval — default is dry-run only)

Do this only when you explicitly want stored JSON to carry `services`/`ids` on rows that have not been saved since step 2. **Not required** for the site after step 4 (publish hydrates in memory from `urls`).

### 6.1 Backup again

- [ ] Fresh `CosmosDbDownloader` snapshot of the item container (dated; do not overwrite step 0.2 B)
- [ ] Note `_ts` on 5 sample ids you will canary

### 6.2 Dry-run

- [ ] Export or page **raw** JSON (not typed `GetAll()`)
- [ ] Run the backfill processor with `apply: false`
- [ ] Record `Candidates`
- [ ] Spot-check 10 candidate files: they should have `urls` or top-level ids **without** a complete `services`/`ids` cover
- [ ] Mismatch report (manual): any row where `urls.spotify` and `services.spotify.url` both exist and differ — decide before apply (catalog will win on serialize)

### 6.3 Canary apply (human says “apply these ids”)

- [ ] Curation freeze: no admin edits during the canary
- [ ] Apply **10–50** ids only
- [ ] For each: diff **raw JSON after** vs snapshot: `urls.*`, top-level ids, `images.*`, `lang`, title, description **byte-equal or additive only**. Fail the exercise if any of those disappeared
- [ ] If a row’s `_ts` changed between scan and apply, skip it

### 6.4 Batch apply

Only if canary diffs are clean.

- [ ] Small batches, same invariant diff on a sample each batch
- [ ] Re-run dry-run; candidates ≈ 0 (except in-flight writes)
- [ ] Optional: republish feed so R2 picks up backfilled catalog keys

**Default if unsure:** leave `apply: false`. Dual-write on ordinary saves will fill rows over time.

**Rollback:** restore individual items from the step-6 snapshot (or 0.2 B). There is no automatic delete of `services`; rollback is “put the old JSON back,” not “run strip.”

---

## 7. Done / do not do

**Done when**

- [ ] PRs merged in order: website, then Api, then Functions
- [ ] Site + Worker + `api-infra` + `indexer-infra` on this code
- [ ] R2 lookup keys republished from this branch’s local `PublishR2 lookups`
- [ ] Feed republished after the site was live
- [ ] Feed and a public detail page show the right destinations
- [ ] Search compact ids still work; `svc` added only if you chose step 5
- [ ] Backfill either skipped or canary-diffed; Phase 3 not started

**Do not**

- Strip `urls` / `images`
- Recreate the search index
- Run language inherit / “fill empty lang” in the same window
- Use typed `GetAll()` + `Save` as a migration
- Deploy Functions and publish the feed on the same breath as an old site
- `PublishR2 all` / `homepage` as a substitute for step 1c (that writes homepage)

---

## Emergency

| Symptom | Likely | Action |
| --- | --- | --- |
| Feed cards have no platform icons; Cosmos `urls` still there | Publish before new site (F1) | Restore R2 from backup A; or finish site deploy |
| Languages/people/subjects Worker routes broken after 1c | Bad local `PublishR2` or old PATH exe | Restore those keys from backup A2 |
| Homepage or feed changed during 1c | Used `all` / `homepage` / admin publish | Restore feed from A and homepage from its step-0 copy; do not merge Functions |
| Raw JSON lost `urls` or `lang` after a save | Upsert / wrong job (D1 / language-class bug) | Restore that id from snapshot; stop batch apply |
| Curator clear Spotify does not stick | F4 | Leave the URL; fix in a later PR — not a backfill |
| Tweet “No link found” | Missing legacy `urls` and SyncLegacy did not run | Check Functions version; do not strip `urls` |
