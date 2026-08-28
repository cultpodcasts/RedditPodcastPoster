<!-- pragma: allowlist secret -->
# Ops runbook: service catalog + nested ids

Human-in-the-middle rollout. **You** decide each gate. An agent must not deploy Workers/Pages, must not write production Cosmos, and must not republish the feed unless you name that action.

Companion docs: [risk](episode-services-risk.md) · [mechanics](episode-services-migration.md) · [canvas](episode-services-canvas.md).

**Out of scope this exercise:** Phase 3 (stop dual-write / strip `urls`). Do not combine with a language job.

No new Worker secrets. Ignore GitHub Actions as a go/no-go.

---

## Roles and freeze

| You | Agent / automation |
| --- | --- |
| Merge PRs, deploy site / Worker / Functions, admin publish, Azure Search schema, Cosmos export, any `apply: true` | Local tests, diffs, runbook edits only |

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
| 1 | **website** | site PR | After step 0 backups. Merge **first**. Then deploy Pages. | Before R2 snapshot exists |
| 2 | **Api** (Worker) | Worker PR | After production site is live and step 1 checks pass. Merge, then deploy **preview**, then top-level Worker `api` (not `--env production`). | Before the new site is serving production |
| 3 | **Functions** (this repo) | Functions PR | After Worker production is live and step 1 feed `GET` still matches leftover-field R2. Merge **last**. Then deploy `api-infra` / `indexer-infra` under the publish freeze. | Before site + Worker are in production |

**Why this order.** Merge to `main` is how this org ships. `deploy.yml` on the Functions repo can put writers live when Actions is healthy. If you merge Functions first, the next successful Functions deploy (CI or script) can publish a new-shape feed while production still runs the **old** site. Old site cannot read `ids`+`services` only.

**Rules**

- [ ] Do **not** merge all three “because they are one feature.” Merge is a release step, not a paperwork step.
- [ ] Do **not** merge Functions to `main` “to keep the branch tidy” and then wait to deploy. Someone else, or CI, can deploy `main` without you.
- [ ] Preview Worker may be updated from the Worker branch **before** merge if you want a staging look. Production Worker merge waits for the site.
- [ ] Leave the Functions PR **open** until step 2. After merge, treat `main` as deployable writers — start the publish freeze the same day.
- [ ] A later Phase 3 / F4-fix PR is **not** part of this merge train. Merge that only after this rollout is done.

**If you already merged Functions first**

Do not deploy Functions. Do not publish the feed. Merge and deploy site + Worker immediately, then continue from step 2. If Functions already deployed and R2 changed, restore feed backup A first.

---

## 0. Before any production deploy

### 0.1 Confirm what you are shipping

- [ ] Functions PR (this repo) includes dual-write + new public/feed JSON (`ids` + `services` only)
- [ ] Api Worker PR: OpenAPI allows leftover named URL fields **and** `ids`/`services`; Worker still returns R2 **bytes unchanged**
- [ ] Site PR: helpers read `services` → leftover named fields → `ids` / search compact ids / `svc`
- [ ] Known bug **F4** still open: clearing Spotify/Apple/YouTube in the form can be undone by `SyncLegacy`. Treat “clear a link” as a soak item; do not rely on clear until a follow-up fix ships
- [ ] Local: `dotnet test` on this repo; site + Api unit tests green on their branches

### 0.2 Backups (do these first; keep until Phase 2 is done or abandoned)

**A. Published feed (R2)** — copy the live feed object to a dated local file **and** a second R2 key you will not overwrite (example name `content/feed.bak-YYYYMMDD`). This is the rollback for F1. Confirm the copy still has leftover `spotify` / `apple` / `youtube` (or `urls`) fields.

**B. Cosmos stored items** — `CosmosDbDownloader` for the **item container only** (not a full account dump unless you want one). Use overwrite-off so a second run cannot clobber the snapshot. Store off-box (disk + one other place). Spot-check 5 JSON files still have `urls` and `lang` as you expect (null `lang` is English — that is valid).

**C. Search index schema** — Azure portal: export or screenshot field list (`spotifyId`, `youtubeId`, `appleId`, `podcastAppleId`, `bbc`, `internetArchive`, `image`). You will **add** `svc` later, never drop these.

**D. Record “what is on prod”** (blob `lastModified`, not GitHub):

```powershell
az storage blob show --account-name <stg> --container-name api-deployment --name released-package.zip --auth-mode login --query properties.lastModified
az storage blob show --account-name <stg> --container-name indexer-deployment --name released-package.zip --auth-mode login --query properties.lastModified
```

Note current site and Worker release (Pages / Worker dashboard).

### 0.3 Gate — human

- [ ] Snapshot A + B exist and you opened at least one file from each
- [ ] You accept: no feed publish until step 4
- [ ] You accept: no Cosmos `apply: true` until step 6
- [ ] You accept: merge order website → Api → Functions (Functions PR stays open until step 2)

---

## 1. Readers first (site + Api Worker)

Old feed stays on R2. New site can read it. Old site cannot read a new feed — that is why writers come later.

1. **Merge the website PR to `main`.** Deploy Pages (your usual production flow). Do not drop leftover fallbacks.
2. **After** production site checks below pass: **merge the Api PR to `main`.** Deploy Worker preview if not already, then production Worker `api` (not `--env production`).
3. No new secrets. Parity script only if you added keys (this change should not).
4. Leave the Functions PR **unmerged**.

### Checks (human)

- [ ] Production site still shows listen/watch on the feed (Spotify/YouTube/Apple and a BBC row if you have one)
- [ ] Search result still opens a platform URL
- [ ] Public episode detail / saved item still has outbound links
- [ ] `GET` feed from the Worker is **byte-identical** to the R2 backup (or same leftover fields). Worker must not reshape.

**Stop** if links are missing. Do not deploy Functions yet.

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

---

## Emergency

| Symptom | Likely | Action |
| --- | --- | --- |
| Feed cards have no platform icons; Cosmos `urls` still there | Publish before new site (F1) | Restore R2 from backup A; or finish site deploy |
| Raw JSON lost `urls` or `lang` after a save | Upsert / wrong job (D1 / language-class bug) | Restore that id from snapshot; stop batch apply |
| Curator clear Spotify does not stick | F4 | Leave the URL; fix in a later PR — not a backfill |
| Tweet “No link found” | Missing legacy `urls` and SyncLegacy did not run | Check Functions version; do not strip `urls` |
