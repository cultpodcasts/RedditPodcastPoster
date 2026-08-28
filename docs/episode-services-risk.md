<!-- pragma: allowlist secret -->
# Risk assessment: service catalog + nested ids rollout

Scope: Phase 0–2 of [episode-services-migration.md](episode-services-migration.md). Phase 3 (stop dual-write / strip `urls`) is **out of scope** and is the highest future delete risk — do not combine it with this exercise.

This is not approval to write production Cosmos, republish the feed, or recreate search.

## Lesson from the last data-loss exercise

The language default-change path treated Cosmos `null` as “unset” and **wrote a new value over real English rows**. The failure mode was not “the job crashed”. It was **a typed save that thought it was filling a gap**.

This rollout has the same shape of danger:

- In-memory hydrate makes a typed object look complete even when stored JSON is not.
- `Save` is a **full document upsert**, not a patch of two properties.
- Dual-write **assigns** legacy slots from the catalog (`SyncLegacy`), it does not merge-if-empty on the way back.

Rule for this exercise: **never infer “safe to overwrite” from a hydrated object.** Select from raw JSON. Persist only additive `services` / `ids` unless a human has signed a field-level diff.

## Verdict

| Question | Answer |
| --- | --- |
| Does Phase 0–1 **by itself** delete Cosmos listen URLs, ids, language, titles? | No, if we do not run backfill apply and do not strip. New/updated saves dual-write. |
| Can Phase 2 (backfill apply) lose data anyway? | **Yes.** Full upsert of every candidate can drop unmapped JSON properties and can last-write-wins over a concurrent curator edit. |
| Can Phase 1 lose **user-visible** listen/watch links without touching Cosmos? | **Yes.** Publishing the new feed shape before production site/admin understand it removes leftover named URL fields from R2. That is functionality loss, not Cosmos delete — visitors still experience “the links are gone”. |
| Is Phase 3 safe in this exercise? | **No. Do not run it.** |

Residual risk after the hardened controls below is **low for Cosmos field delete**, **medium for published-feed outage** if publish order slips, **high if anyone runs a typed GetAll + Save or a strip job**.

## Invariants we must not violate

These fields on a stored document must still be present and equal (or strictly additive) after any apply:

- `urls.*` (spotify, apple, youtube, bbc, internetArchive)
- top-level `spotifyId`, `appleId`, `youTubeId`
- `images.*`
- `lang` (null still means English — do not “fill” it)
- title, description, release, duration, flags (`posted`, `tweeted`, `bluesky` / `blueskyPost`, ignored, removed)
- subject tags, guests, searchTerms, hashTag <!-- pragma: allowlist secret -->

Allowed adds only: `services`, `ids`.

## Data-loss risks

| ID | Risk | How it happens | Severity | In current code? | Control |
| --- | --- | --- | --- | --- | --- |
| D1 | **Full upsert strips unknown JSON** | `EpisodeRepository.Save` → `UpsertItemAsync(episode)`. `Episode` has **no** `JsonExtensionData`. Any property not on the class (historical extras, manual edits, future fields) is omitted (`WhenWritingNull`). A “backfill” that only meant to add `services` **rewrites the whole item**. | **Critical** | Yes, any `Save` | Do **not** apply Phase 2 as typed Save until we either (a) export a full snapshot and diff every item, or (b) persist with a **JSON patch / Merge** of `services`+`ids` only. Default remains dry-run. |
| D2 | **Last-write-wins during batch apply** | Processor `Get` then `Save`. A curator PATCH in between is overwritten by the stale loaded document (title, URLs they just fixed, language). | **High** | Yes | Small batches; skip if `_ts` changed since the raw scan; never run apply during a curation window. |
| D3 | **Divergent URL: catalog wins, legacy slot overwritten** | `Hydrate` does `link.Url ??= urls` (catalog kept). `SyncLegacy` then **assigns** `urls.spotify = services.spotify.url`. If they already disagreed, the `urls` value is replaced. | Medium | Yes, on every serialize after this branch | Pre-apply report: count documents where `urls.X` ≠ `services.X.url`. Spot-check before apply. Do not treat “shape changed” as harmless. |
| D4 | **Divergent ids: nested `ids` wins** | `SyncIds`: if `ids.spotify` is set, it writes `spotifyId`. A mismatch drops the top-level value. | Medium | Yes | Same: report mismatches before apply. Expect them to be rare. |
| D5 | **BBC collapse into one legacy slot** | `urls.bbc` can hold one URI. `SyncLegacy` prefers iPlayer over Sounds. Sounds stays on `services.bbcSounds` but **`urls.bbc` loses Sounds** when both exist. Tweets and search SQL that still read `e.urls.bbc` see only iPlayer. | Medium (legacy field) / Low (catalog) | Yes | Accept as known dual-write limit. Do not call it “delete of Sounds” in Cosmos catalog. Do not strip `urls` until posters/SQL read `services`. |
| D6 | **`images.other` collapse** | One legacy `other` image. Sync picks iPlayer ?? Sounds ?? Archive ?? Vimeo ?? Netflix ?? Prime. The stored `images.other` string can **change** even though art remains under `services.*.image`. Search coalesce that still uses `images.other` can swap cover. | Medium | Yes | Include `images` in apply snapshot/diff (today `Capture` does **not** include images). |
| D7 | **Language / English-null overwrite** | Same class as the last incident: a job that “fills empty lang from show default”. This migration code does **not** touch `lang`. A mistaken reuse of `inheritLanguageIfUnset: true` on a bulk save would. | Critical if combined | Not in this processor | Do not compose this apply with language backfill. Never use inherit-if-unset on existing rows. |
| D8 | **Phase 3 strip of `urls` / `images`** | Explicit delete of legacy fields. Old SQL, tweets, curator PATCH, matching still need them. | **Critical** (future) | Not implemented | Separate PR, `NeedsStrip`, dry-run, never same day as Phase 2. |
| D9 | **Search index recreate** | “Add `svc`” done as drop-and-create, or dropping `spotifyId` / `bbc` / `internetArchive`. | High | Not this PR | Additive field only. Keep compact ids and legacy bbc/archive fields. |

`Apply` + `Capture` compare services, nested ids, top-level ids, and `urls` string forms. They do **not** compare `images` or `lang`. A save that also runs `OnSerializing` can still persist image rewrite as a side effect of D1.

## Functionality-loss risks

| ID | Risk | How it happens | Severity | Control |
| --- | --- | --- | --- | --- |
| F1 | **Published feed goes new-shape while production site is old** | Functions deploy first. Scheduled or admin publish writes `ids`+`services` **without** leftover `spotify` / `apple` / `youtube` / `bbc` URL fields. Old site cards only read those leftover fields. Listen/watch icons disappear. Cosmos still has URLs. | **Critical** (user-visible) | **Do not republish** until the new site is live. **Disable or skip** the Monday refresh-window publish after Functions deploy. Safer: **dual-emit** leftover named URL fields on the feed until a soak period after the site ships. The current publisher does **not** dual-emit. |
| F2 | **Public episode API new-shape, old detail/saved-item page** | `PublicEpisodeDto` is `ids`+`services` only. An old client that only reads `urls` or flat `spotify` loses outbound links on GET-by-id. | High | Same deploy gate: site (or at least the public display helper) before relying on Functions-generated public JSON. |
| F3 | **Site shipped without leftover fallbacks, feed not republished** | Inverse of F1. New helpers if someone deletes leftover reads; old R2 has no `services`. | High | Do not remove leftover fields from the site until R2 is new-shape and soaked. |
| F4 | **Curator cannot clear Spotify/Apple/YouTube** | Form sends empty `urls.spotify` and does **not** send `services.spotify` (default keys are excluded from the services PATCH). Applier clears `urls` then `Hydrate`+`SyncLegacy` **copies the URL back from `services`**. After the first dual-write save, “clear link” silently fails. Not Cosmos loss — **cannot remove** a destination. | High (ops) | Fix applier: clearing a default `urls` slot must `Upsert(..., null, null)` and clear nested/top-level ids. Treat as a deploy blocker for curator trust. |
| F5 | **Admin “Post” dialog still requires `urls.*`** | Post dialog checks `resp.urls?.spotify` etc. Admin DTO still has `urls` while dual-write is on. Breaks only if we drop `urls` from admin GET early. | Medium | Keep admin `urls` until Phase 3. |
| F6 | **Tweets / Bluesky pick `Episode.Urls` only** | Safe while `OnSerializing` hydrates then `SyncLegacy`. Breaks if Phase 3 stops SyncLegacy or a document has catalog-only destinations (Vimeo) and no legacy URL — those never tweeted anyway. Both BBC products: tweet gets iPlayer only (D5). | Medium after Phase 3 / Low now | Do not stop SyncLegacy until poster factories read `services`. |
| F7 | **Search cards lose Sounds / Vimeo / Netflix** | `svc` not in the live index yet. Compact ids still power Spotify/Apple/YouTube. Extra catalog keys absent on search until `svc` is added **after** Functions write it. | Low (additive gap) | Add field, then reindex. Do not recreate the index. |
| F8 | **Matching / enrichers** | Still read top-level ids. Dual-write keeps them. Risk only if SyncIds is skipped or Phase 3 drops top-level ids first. | Low now | Keep dual-write. |

Compatibility is **not symmetric**:

- New site **can** read an old feed (leftover named fields).
- Old site **cannot** read a new feed (no leftover fields).

The plan’s “Functions first, then site, then republish” is correct **only if republish cannot happen in between**. The publisher has a Monday 00:00 UTC refresh window. That is an automatic F1.

## What the plan already gets right

- Selection uses **raw JSON** + `NeedsBackfill` (not typed `GetAll()`).
- Dry-run is the processor default (`apply: false`).
- Hydrate-then-SyncLegacy on serialize so tweets/SQL keep `urls` for **new writes**.
- Cheap `NOT IS_DEFINED(c.services)` is documented as **insufficient** (partial maps).
- Phase 3 strip is a later, separate, tested job.
- Rollback of **code** does not delete `services`/`ids` once added.

## Gaps to add before any apply or republish

1. **Full snapshot** of the container (or at least every candidate’s raw JSON) before Phase 2. Keep it until a second dry-run is ~0 and a sample of production pages is checked.
2. **Field-level diff** after a 10–50 document canary apply: assert D1 invariants; fail the job if `lang`, title, or any `urls.*` disappeared.
3. **`_ts` / etag guard** on apply, or patch-only writes.
4. **Publish freeze** after Functions deploy until the new site is live; or **dual-emit leftover URL fields** on the feed (recommended — matches how Cosmos dual-writes).
5. **Fix F4** (clear default URL must clear `services` + ids) before curators use the new Functions.
6. **Mismatch report** for D3/D4/D5 before bulk apply.
7. **Do not** run language jobs, index rebuilds, or Phase 3 in the same window.

## Hardened sequence (functionality first, then additive data)

1. Merge / deploy **site** (leftover fallbacks on) and **Api Worker** (pass-through). Old R2 still works.
2. Deploy **Functions** with dual-write. **Do not publish the feed.** Confirm Monday window will not fire, or dual-emit leftovers.
3. Soak: curator edit (including **clear** a URL), one tweet/Bsky, one search, one public detail page.
4. **Then** republish the feed. Confirm cards still have listen/watch. Keep leftover fallbacks on the site.
5. Add search `svc` (additive). Reindex. Do not drop compact ids.
6. Export snapshot. Dry-run backfill. Canary apply 10–50 with invariant diffs. Only then batch apply.
7. Second dry-run ~0. Phase 3 stays a later PR.

## Explicit non-goals for this exercise

- Do not strip `urls` or `images`.
- Do not treat empty/null as “fill from the other object” except the documented Hydrate `??=` into `services`.
- Do not run apply from an agent session against production unless that write is explicitly requested.
- Ignore GitHub Actions status as a rollout gate (per current working agreement). Use local tests + the invariant diffs above.
